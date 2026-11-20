import { useQuery } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ActionDialog } from "@/components/ConfirmDialog";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api } from "@/lib/api";
import { relative, when } from "@/lib/format";
import { useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { IpBlock } from "@/lib/types";

const durations = [
	{ value: "1", label: "1 hora" },
	{ value: "24", label: "1 dia" },
	{ value: "168", label: "7 dias" },
	{ value: "720", label: "30 dias" },
	{ value: "0", label: "Para sempre" },
];

/** A janela de bloquear um endereço — usada aqui e na tela de eventos. */
export function BlockDialog({ open, onOpenChange, initial = "" }: { open: boolean; onOpenChange: (open: boolean) => void; initial?: string }) {
	const refresh = useRefresh();
	const [cidr, setCidr] = useState(initial);
	const [reason, setReason] = useState("");
	const [hours, setHours] = useState("24");

	useEffect(() => {
		if (open) {
			setCidr(initial);
			setReason("");
		}
	}, [open, initial]);

	return (
		<ActionDialog
			open={open}
			onOpenChange={onOpenChange}
			title="Bloquear endereço"
			description="Vale na hora: todo pedido desse endereço recebe 403, no jogo e no painel. Confira que não é o seu."
			action="Bloquear"
			danger
			done="Endereço bloqueado."
			disabled={!cidr.trim() || reason.trim().length < 3}
			onConfirm={() => api.post("/security/blocks", { cidr: cidr.trim(), reason, hours: Number(hours) }).then(() => refresh("blocks", "overview"))}
		>
			<Field label="Endereço ou faixa" hint="Um IP (203.0.113.7) ou uma faixa em CIDR (203.0.113.0/24). IPv6 também.">
				<Input value={cidr} onChange={(e) => setCidr(e.target.value)} className="num" placeholder="203.0.113.7" required />
			</Field>
			<Field label="Por quanto tempo">
				<Select value={hours} onChange={setHours} options={durations} />
			</Field>
			<Field label="Motivo">
				<Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ex.: força bruta no login em 20/11" required />
			</Field>
		</ActionDialog>
	);
}

export function IpBlocks() {
	const { can } = useSession();
	const refresh = useRefresh();
	const blocks = useQuery({ queryKey: ["blocks"], queryFn: () => api.get<{ blocks: IpBlock[] }>("/security/blocks"), refetchInterval: 15_000 });
	const [adding, setAdding] = useState(false);
	const [removing, setRemoving] = useState<IpBlock | null>(null);

	return (
		<>
			<PageHeader
				title="Blacklist de IP"
				description="Endereços que o servidor recusa antes de qualquer coisa. O escudo bloqueia sozinho quem faz força bruta, varredura ou injeção — por 15 minutos na primeira vez, e mais a cada reincidência."
			>
				{can("moderate") && (
					<Button variant="primary" onClick={() => setAdding(true)}>
						<Plus />
						Bloquear endereço
					</Button>
				)}
			</PageHeader>

			<Card>
				{blocks.isPending ? (
					<Loading />
				) : blocks.isError ? (
					<div className="p-4">
						<ErrorNote error={blocks.error} retry={() => void blocks.refetch()} />
					</div>
				) : blocks.data.blocks.length === 0 ? (
					<Empty title="Nenhum endereço bloqueado">Quando o escudo ou alguém da equipe bloquear um endereço, ele aparece aqui.</Empty>
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Endereço</Th>
								<Th>Origem</Th>
								<Th>Motivo</Th>
								<Th>Bloqueado em</Th>
								<Th>Acaba</Th>
								<Th />
							</tr>
						</thead>
						<tbody>
							{blocks.data.blocks.map((b) => (
								<Tr key={b.id}>
									<Td>
										<Link to={`/seguranca?ip=${encodeURIComponent(b.cidr)}`} className="num font-medium hover:underline">
											{b.cidr}
										</Link>
									</Td>
									<Td>{b.source === "auto" ? <Badge tone="accent">Escudo</Badge> : <Badge tone="primary">{b.admin || "Painel"}</Badge>}</Td>
									<Td className="max-w-[52ch] text-ink-2">{b.reason}</Td>
									<Td className="text-ink-2">{when(b.createdAt)}</Td>
									<Td className="text-ink-2">{b.until ? relative(b.until) : "Nunca"}</Td>
									<Td className="text-right">
										{can("moderate") && (
											<Button size="sm" onClick={() => setRemoving(b)}>
												Desbloquear
											</Button>
										)}
									</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>

			<BlockDialog open={adding} onOpenChange={setAdding} />
			<ActionDialog
				open={removing !== null}
				onOpenChange={(o) => !o && setRemoving(null)}
				title={`Desbloquear ${removing?.cidr}`}
				description="O endereço volta a ser atendido agora."
				action="Desbloquear"
				done="Endereço desbloqueado."
				onConfirm={() => api.delete(`/security/blocks/${removing?.id}`).then(() => refresh("blocks", "overview"))}
			/>
		</>
	);
}
