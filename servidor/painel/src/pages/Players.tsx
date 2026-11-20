import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api } from "@/lib/api";
import { n, relative, when } from "@/lib/format";
import type { PlayerRow } from "@/lib/types";

export function PlayerState({ player }: { player: Pick<PlayerRow, "banned" | "online"> }) {
	if (player.banned) return <Badge tone="danger">Banido</Badge>;
	if (player.online) return <Badge tone="good">Online</Badge>;
	return <span className="text-[13px] text-ink-3">Fora</span>;
}

export function Players() {
	const [query, setQuery] = useState("");
	const [banned, setBanned] = useState(false);
	const navigate = useNavigate();
	const players = useQuery({
		queryKey: ["players", query, banned],
		queryFn: () => api.get<{ players: PlayerRow[] }>(`/players?q=${encodeURIComponent(query)}${banned ? "&banned=1" : ""}`),
		placeholderData: (previous) => previous,
	});

	return (
		<>
			<PageHeader title="Jogadores" description="Procure pelo começo do nome, pelo número da conta ou pelo último IP. Sem busca, aparecem os que entraram por último." />

			<div className="mb-3 flex flex-wrap items-center gap-4">
				<div className="relative w-full max-w-md">
					<Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-3" />
					<Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Nome, número da conta ou IP" className="pl-9" autoFocus aria-label="Procurar jogador" />
				</div>
				<label className="flex items-center gap-2 text-ink-2">
					<Switch checked={banned} onCheckedChange={setBanned} />
					Só os banidos
				</label>
			</div>

			<Card>
				{players.isPending ? (
					<Loading />
				) : players.isError ? (
					<div className="p-4">
						<ErrorNote error={players.error} retry={() => void players.refetch()} />
					</div>
				) : players.data.players.length === 0 ? (
					<Empty title="Ninguém encontrado">{query ? "Confira a grafia: a busca é pelo começo do nome." : "Ainda não há contas no servidor."}</Empty>
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Jogador</Th>
								<Th>Estado</Th>
								<Th className="text-right">Patente</Th>
								<Th className="text-right">XP</Th>
								<Th className="text-right">Presas</Th>
								<Th className="text-right">Sangue</Th>
								<Th className="text-right">Partidas</Th>
								<Th>Última entrada</Th>
								<Th>Conta criada</Th>
							</tr>
						</thead>
						<tbody>
							{players.data.players.map((p) => (
								<Tr key={p.id} className="cursor-pointer" onClick={() => navigate(`/jogadores/${p.id}`)}>
									<Td>
										<span className="font-medium">{p.name}</span> <span className="num text-[13px] text-ink-3">#{p.id}</span>
									</Td>
									<Td>
										<PlayerState player={p} />
									</Td>
									<Td className="text-right">{p.rank}</Td>
									<Td className="text-right">{n(p.xp)}</Td>
									<Td className="text-right">{n(p.presas)}</Td>
									<Td className="text-right">{n(p.sangue)}</Td>
									<Td className="text-right">{n(p.matches)}</Td>
									<Td className="text-ink-2">{relative(p.lastLoginAt)}</Td>
									<Td className="text-ink-2">{when(p.createdAt)}</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>
		</>
	);
}
