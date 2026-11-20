import { useQuery } from "@tanstack/react-query";
import { Lock } from "lucide-react";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Card, CardHeader } from "@/components/ui/card";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api } from "@/lib/api";
import { relative } from "@/lib/format";
import { useContent } from "@/lib/queries";
import type { FleetServer, Room } from "@/lib/types";

const states: Record<string, string> = { waiting: "Esperando", starting: "Começando", playing: "Em partida" };

export function Fleet() {
	const content = useContent();
	const fleet = useQuery({ queryKey: ["fleet"], queryFn: () => api.get<{ servers: FleetServer[] }>("/fleet"), refetchInterval: 5_000 });
	const rooms = useQuery({ queryKey: ["rooms"], queryFn: () => api.get<{ rooms: Room[] }>("/rooms"), refetchInterval: 5_000 });
	const mapName = (id?: string) => content.data?.maps.find((m) => m.id === id)?.name ?? id ?? "";

	return (
		<>
			<PageHeader title="Frota e salas" description="Os servidores de partida que estão batendo no servidor central e as salas abertas no saguão, ao vivo." />

			<Card className="mb-4">
				<CardHeader title="Servidores de partida" description="Cada um avisa que está vivo a cada poucos segundos. Vinte segundos sem aviso e ele sai da lista." />
				{fleet.isPending ? (
					<Loading />
				) : fleet.isError ? (
					<div className="p-4">
						<ErrorNote error={fleet.error} retry={() => void fleet.refetch()} />
					</div>
				) : fleet.data.servers.length === 0 ? (
					<Empty title="Nenhum servidor de partida ligado">Sem servidor de partida, as salas não conseguem começar. Suba um com a mesma chave da frota do servidor central.</Empty>
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Servidor</Th>
								<Th>Endereço</Th>
								<Th className="text-right">Partidas</Th>
								<Th className="text-right">Jogadores</Th>
								<Th>Último aviso</Th>
								<Th>Rodando agora</Th>
							</tr>
						</thead>
						<tbody>
							{fleet.data.servers.map((s) => (
								<Tr key={s.id}>
									<Td className="font-medium">{s.id}</Td>
									<Td className="num text-ink-2">
										{s.address}:{s.port}
									</Td>
									<Td className="text-right">
										{s.matches.length} de {s.capacity}
									</Td>
									<Td className="text-right">{s.players}</Td>
									<Td className="text-ink-2">{relative(s.lastSeen)}</Td>
									<Td className="text-ink-2">
										{s.matches.length === 0
											? "Livre"
											: s.matches.map((m) => `${m.title || "Sala"} em ${mapName(m.map)} (${m.players} jogadores, começou ${relative(m.startedAt)})`).join("; ")}
									</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>

			<Card>
				<CardHeader title="Salas no saguão" description="As 200 mais recentes." />
				{rooms.isPending ? (
					<Loading />
				) : rooms.isError ? (
					<div className="p-4">
						<ErrorNote error={rooms.error} retry={() => void rooms.refetch()} />
					</div>
				) : rooms.data.rooms.length === 0 ? (
					<Empty title="Nenhuma sala aberta" />
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Sala</Th>
								<Th>Dono</Th>
								<Th>Mapa</Th>
								<Th>Estado</Th>
								<Th className="text-right">Jogadores</Th>
								<Th className="text-right">Bots</Th>
							</tr>
						</thead>
						<tbody>
							{rooms.data.rooms.map((r) => (
								<Tr key={r.id}>
									<Td>
										<span className="flex items-center gap-1.5 font-medium">
											{r.hasPassword && <Lock className="size-3.5 text-ink-3" aria-label="Com senha" />}
											{r.settings.title || `Sala ${r.id}`}
										</span>
									</Td>
									<Td className="text-ink-2">{r.host}</Td>
									<Td className="text-ink-2">{mapName(r.settings.map)}</Td>
									<Td>
										<Badge tone={r.state === "playing" ? "good" : "neutral"}>{states[r.state] ?? r.state}</Badge>
									</Td>
									<Td className="text-right">
										{r.players} de {r.capacity}
									</Td>
									<Td className="text-right">{r.bots}</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>
		</>
	);
}
