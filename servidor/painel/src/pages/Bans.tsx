import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api } from "@/lib/api";
import { when } from "@/lib/format";
import type { Ban } from "@/lib/types";

export function BanList({ bans, hideAccount }: { bans: Ban[]; hideAccount?: boolean }) {
	return (
		<Table>
			<thead>
				<tr>
					{!hideAccount && <Th>Conta</Th>}
					<Th>Situação</Th>
					<Th>Motivo</Th>
					<Th>Banido em</Th>
					<Th>Até</Th>
					<Th>Por</Th>
				</tr>
			</thead>
			<tbody>
				{bans.map((b) => (
					<Tr key={b.id}>
						{!hideAccount && (
							<Td>
								<Link to={`/jogadores/${b.accountId}`} className="font-medium hover:underline">
									{b.account}
								</Link>{" "}
								<span className="num text-[13px] text-ink-3">#{b.accountId}</span>
							</Td>
						)}
						<Td>{b.active ? <Badge tone="danger">Em vigor</Badge> : b.liftedAt ? <Badge>Levantado por {b.liftedBy}</Badge> : <Badge>Cumprido</Badge>}</Td>
						<Td className="max-w-[48ch] text-ink-2">{b.reason}</Td>
						<Td className="text-ink-2">{when(b.createdAt)}</Td>
						<Td className="text-ink-2">{b.until ? when(b.until) : "Para sempre"}</Td>
						<Td className="text-ink-2">{b.admin}</Td>
					</Tr>
				))}
			</tbody>
		</Table>
	);
}

export function Bans() {
	const [all, setAll] = useState(false);
	const bans = useQuery({ queryKey: ["bans", all], queryFn: () => api.get<{ bans: Ban[] }>(`/security/bans${all ? "?all=1" : ""}`) });

	return (
		<>
			<PageHeader title="Banimentos" description="As contas que não entram no jogo. Para banir ou desbanir alguém, abra a ficha do jogador.">
				<label className="flex items-center gap-2 text-ink-2">
					<Switch checked={all} onCheckedChange={setAll} />
					Mostrar os que já acabaram
				</label>
			</PageHeader>
			<Card>
				{bans.isPending ? (
					<Loading />
				) : bans.isError ? (
					<div className="p-4">
						<ErrorNote error={bans.error} retry={() => void bans.refetch()} />
					</div>
				) : bans.data.bans.length === 0 ? (
					<Empty title={all ? "Ninguém foi banido até hoje" : "Nenhum banimento em vigor"} />
				) : (
					<BanList bans={bans.data.bans} />
				)}
			</Card>
		</>
	);
}
