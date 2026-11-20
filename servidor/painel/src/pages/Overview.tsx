import { Link } from "react-router-dom";
import { BarList } from "@/components/charts/BarList";
import { ColumnChart, Legend } from "@/components/charts/ColumnChart";
import { ErrorNote, Loading, PageHeader, Stat } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Card, CardBody, CardHeader } from "@/components/ui/card";
import { day, eventNames, hourOf, n, severityNames, short } from "@/lib/format";
import { useOverview } from "@/lib/queries";
import type { EventBucket } from "@/lib/types";

const severitySeries = [
	{ name: "Informativo", color: "var(--chart-neutral)" },
	{ name: "Atenção", color: "var(--chart-warn)" },
	{ name: "Grave ou crítico", color: "var(--chart-critical)" },
];

/** As últimas 24 horas, uma coluna por hora — as horas sem evento entram zeradas. */
export function lastHours(buckets: EventBucket[], hours = 24) {
	const byHour = new Map(buckets.map((b) => [new Date(b.hour).getTime(), b]));
	const now = new Date();
	now.setMinutes(0, 0, 0);
	return Array.from({ length: hours }, (_, i) => {
		const at = now.getTime() - (hours - 1 - i) * 3_600_000;
		const b = byHour.get(at);
		const iso = new Date(at).toISOString();
		return { label: hourOf(iso).slice(0, 2) + "h", title: `${day(iso.slice(0, 10))} às ${hourOf(iso)}`, values: [b?.info ?? 0, b?.warning ?? 0, (b?.high ?? 0) + (b?.critical ?? 0)] };
	});
}

export function Overview() {
	const overview = useOverview();
	if (overview.isPending) return <Loading />;
	if (overview.isError) return <ErrorNote error={overview.error} retry={() => void overview.refetch()} />;
	const o = overview.data;
	const serious = o.security.byHour.reduce((sum, b) => sum + b.high + b.critical, 0);

	return (
		<>
			<PageHeader title="Visão geral" description="O que está acontecendo no servidor agora. Atualiza sozinha a cada cinco segundos." />

			<Card className="mb-4 grid grid-cols-2 divide-line sm:grid-cols-3 lg:grid-cols-6 lg:divide-x">
				<Stat label="Online agora" value={n(o.online)} note={`${n(o.activeDay)} entraram nas últimas 24 h`} />
				<Stat label="Salas abertas" value={n(o.rooms)} note={`${n(o.roomsPlaying)} em partida`} />
				<Stat
					label="Partidas em curso"
					value={n(o.fleet.matches)}
					note={o.fleet.servers === 0 ? "nenhum servidor de partida ligado" : `${n(o.fleet.slots - o.fleet.matches)} lugares livres em ${n(o.fleet.servers)} ${o.fleet.servers === 1 ? "servidor" : "servidores"}`}
					tone={o.fleet.servers === 0 ? "warn" : undefined}
				/>
				<Stat label="Partidas hoje" value={n(o.matchesToday)} note={`${short(o.matches)} desde o começo`} />
				<Stat label="Contas" value={short(o.accounts)} note={`${n(o.newToday)} novas nas últimas 24 h`} />
				<Stat label="Eventos graves em 24 h" value={n(serious)} note={`${n(o.activeBans)} contas banidas, ${n(o.ipBlocks)} IPs bloqueados`} tone={serious > 0 ? "danger" : "good"} />
			</Card>

			<div className="mb-4 grid gap-4 lg:grid-cols-2">
				<Card>
					<CardHeader title="Contas novas por dia" description="Os últimos 14 dias." />
					<CardBody>
						<ColumnChart columns={o.days.map((d) => ({ label: day(d.day), values: [d.accounts] }))} series={[{ name: "Contas novas", color: "var(--chart-1)" }]} />
					</CardBody>
				</Card>
				<Card>
					<CardHeader title="Partidas por dia" description="Os últimos 14 dias." />
					<CardBody>
						<ColumnChart columns={o.days.map((d) => ({ label: day(d.day), values: [d.matches] }))} series={[{ name: "Partidas", color: "var(--chart-2)" }]} />
					</CardBody>
				</Card>
			</div>

			<div className="grid items-start gap-4 lg:grid-cols-[2fr_1fr]">
				<Card>
					<CardHeader
						title="Eventos de segurança por hora"
						description="As últimas 24 horas, por gravidade."
						action={
							<Link to="/seguranca" className="text-[13px] font-medium text-primary hover:underline">
								Ver os eventos
							</Link>
						}
					/>
					<CardBody>
						<ColumnChart columns={lastHours(o.security.byHour)} series={severitySeries} height={260} labelEvery={3} />
						<div className="mt-2">
							<Legend series={severitySeries} />
						</div>
					</CardBody>
				</Card>

				<div className="flex flex-col gap-4">
					<Card>
						<CardHeader title="O que mais aconteceu" description="Tipos de evento em 24 horas." />
						<CardBody>
							<BarList
								empty="Nenhum evento nas últimas 24 horas."
								rows={o.security.byKind.slice(0, 6).map((k) => ({ key: k.kind, label: eventNames[k.kind] ?? k.kind, value: k.count, color: "var(--chart-neutral)" }))}
							/>
						</CardBody>
					</Card>
					<Card>
						<CardHeader title="Endereços que mais aparecem" description="Só eventos de atenção para cima." />
						<CardBody>
							<BarList
								empty="Nenhum endereço suspeito nas últimas 24 horas."
								color="var(--chart-critical)"
								rows={o.security.topIps.slice(0, 5).map((t) => ({
									key: t.ip,
									value: t.count,
									label: (
										<span className="flex items-center gap-2">
											<Link to={`/seguranca?ip=${encodeURIComponent(t.ip)}`} className="num hover:underline">
												{t.ip}
											</Link>
											{t.worst >= 2 && <Badge tone="danger">{severityNames[t.worst]}</Badge>}
										</span>
									),
								}))}
							/>
						</CardBody>
					</Card>
				</div>
			</div>

			<Card className="mt-4 grid grid-cols-2 divide-line sm:grid-cols-3 lg:grid-cols-6 lg:divide-x">
				<Stat label="Presas em circulação" value={short(o.presas)} note="a soma de todas as carteiras" />
				<Stat label="Sangue em circulação" value={short(o.sangue)} note="a moeda paga" />
				<Stat label="Pedidos atendidos" value={short(o.counters.requests)} note="desde que o servidor subiu" />
				<Stat label="Recusados por bloqueio" note="pedidos de IP na blacklist" value={short(o.counters.refused)} />
				<Stat label="Segurados pelo freio" value={short(o.counters.rateLimited)} note="pedidos demais por minuto" />
				<Stat label="Bloqueios automáticos" value={n(o.counters.autoBlocks)} note="desde que o servidor subiu" />
			</Card>
		</>
	);
}
