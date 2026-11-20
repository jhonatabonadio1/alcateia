import { useQuery } from "@tanstack/react-query";
import { ShieldBan, X } from "lucide-react";
import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ColumnChart, Legend } from "@/components/charts/ColumnChart";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api } from "@/lib/api";
import { eventGroups, eventNames, severityNames, when } from "@/lib/format";
import { useSession } from "@/lib/session";
import type { SecurityEvent, SecuritySummary } from "@/lib/types";
import { BlockDialog } from "@/pages/IpBlocks";
import { lastHours } from "@/pages/Overview";

const tones = ["neutral", "warn", "accent", "danger"] as const;

/** A gravidade com o nome escrito — a cor só reforça. */
export function SeverityBadge({ value }: { value: number }) {
	return <Badge tone={tones[value] ?? "neutral"}>{severityNames[value] ?? value}</Badge>;
}

const series = [
	{ name: "Informativo", color: "var(--chart-neutral)" },
	{ name: "Atenção", color: "var(--chart-warn)" },
	{ name: "Grave ou crítico", color: "var(--chart-critical)" },
];

export function Security() {
	const { can } = useSession();
	const [params, setParams] = useSearchParams();
	const ip = params.get("ip") ?? "";
	const [kind, setKind] = useState("todos");
	const [severity, setSeverity] = useState("0");
	const [block, setBlock] = useState<string | null>(null);

	const summary = useQuery({ queryKey: ["security-summary"], queryFn: () => api.get<SecuritySummary>("/security/overview?hours=24"), refetchInterval: 15_000 });
	const events = useQuery({
		queryKey: ["events", kind, severity, ip],
		queryFn: () =>
			api.get<{ events: SecurityEvent[] }>(`/security/events?severity=${severity}${kind === "todos" ? "" : `&kind=${encodeURIComponent(kind)}`}${ip ? `&ip=${encodeURIComponent(ip)}` : ""}`),
		refetchInterval: 10_000,
		placeholderData: (previous) => previous,
	});

	return (
		<>
			<PageHeader
				title="Eventos de segurança"
				description="Tudo o que o escudo do servidor viu: logins certos e errados, varreduras, tentativas de injeção, bloqueios e resultados de partida suspeitos. A senha tentada nunca é guardada."
			/>

			<Card className="mb-4">
				<CardHeader title="Por hora, nas últimas 24 horas" />
				<CardBody>
					{summary.isPending ? <Loading /> : summary.isError ? <ErrorNote error={summary.error} /> : <ColumnChart columns={lastHours(summary.data.byHour)} series={series} height={170} labelEvery={2} />}
					<div className="mt-2">
						<Legend series={series} />
					</div>
				</CardBody>
			</Card>

			<div className="mb-3 flex flex-wrap items-end gap-3">
				<div className="w-56">
					<Select value={kind} onChange={setKind} options={eventGroups} label="Tipo de evento" />
				</div>
				<div className="w-52">
					<Select value={severity} onChange={setSeverity} label="Gravidade mínima" options={severityNames.map((name, i) => ({ value: String(i), label: i === 0 ? "Qualquer gravidade" : `${name} para cima` }))} />
				</div>
				<div className="relative w-56">
					<Input value={ip} onChange={(e) => setParams(e.target.value ? { ip: e.target.value } : {}, { replace: true })} placeholder="Filtrar por IP" aria-label="Filtrar por IP" className="num pr-8" />
					{ip && (
						<button type="button" className="absolute top-1/2 right-2 -translate-y-1/2 text-ink-3 hover:text-ink" onClick={() => setParams({}, { replace: true })} aria-label="Limpar o filtro de IP">
							<X className="size-4" />
						</button>
					)}
				</div>
				{ip && can("moderate") && (
					<Button variant="danger-quiet" onClick={() => setBlock(ip)}>
						<ShieldBan />
						Bloquear {ip}
					</Button>
				)}
			</div>

			<Card>
				{events.isPending ? (
					<Loading />
				) : events.isError ? (
					<div className="p-4">
						<ErrorNote error={events.error} retry={() => void events.refetch()} />
					</div>
				) : events.data.events.length === 0 ? (
					<Empty title="Nenhum evento com esses filtros" />
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Quando</Th>
								<Th>Gravidade</Th>
								<Th>O quê</Th>
								<Th>Endereço</Th>
								<Th>Quem</Th>
								<Th>Rota</Th>
								<Th>Detalhe</Th>
							</tr>
						</thead>
						<tbody>
							{events.data.events.map((e) => (
								<Tr key={e.id}>
									<Td className="whitespace-nowrap text-ink-2">{when(e.at)}</Td>
									<Td>
										<SeverityBadge value={e.severity} />
									</Td>
									<Td className="whitespace-nowrap">{eventNames[e.kind] ?? e.kind}</Td>
									<Td>
										{e.ip && (
											<button type="button" className="num text-ink-2 hover:text-ink hover:underline" onClick={() => setParams({ ip: e.ip }, { replace: true })}>
												{e.ip}
											</button>
										)}
									</Td>
									<Td>
										{e.accountId ? (
											<Link to={`/jogadores/${e.accountId}`} className="hover:underline">
												{e.actor || `#${e.accountId}`}
											</Link>
										) : (
											<span className="text-ink-2">{e.actor}</span>
										)}
									</Td>
									<Td className="max-w-[28ch] truncate text-ink-3" title={e.path}>
										{e.path}
									</Td>
									<Td className="max-w-[44ch] text-ink-2">{e.detail}</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>

			<BlockDialog open={block !== null} onOpenChange={(o) => !o && setBlock(null)} initial={block ?? ""} />
		</>
	);
}
