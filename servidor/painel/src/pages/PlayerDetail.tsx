import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Ban, Coins, Gift, PlugZap, ShieldCheck, Trash2 } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ActionDialog } from "@/components/ConfirmDialog";
import { Empty, ErrorNote, ItemArt, Loading, Rarity, Stat } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader } from "@/components/ui/card";
import { Input, Textarea } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api } from "@/lib/api";
import { currencyNames, eventNames, n, relative, when, xpForRank } from "@/lib/format";
import { useContent, useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { CatalogItem, PlayerFile } from "@/lib/types";
import { BanList } from "@/pages/Bans";
import { PlayerState } from "@/pages/Players";
import { SeverityBadge } from "@/pages/Security";

const banDurations = [
	{ value: "1", label: "1 hora" },
	{ value: "24", label: "1 dia" },
	{ value: "72", label: "3 dias" },
	{ value: "168", label: "7 dias" },
	{ value: "720", label: "30 dias" },
	{ value: "0", label: "Para sempre" },
];

const itemDurations = [
	{ value: "1", label: "1 dia" },
	{ value: "7", label: "7 dias" },
	{ value: "30", label: "30 dias" },
	{ value: "90", label: "90 dias" },
	{ value: "0", label: "Para sempre" },
];

const statNames: Record<string, string> = {
	matches: "Partidas",
	wins: "Vitórias",
	losses: "Derrotas",
	draws: "Empates",
	kills: "Abates",
	deaths: "Mortes",
	assists: "Assistências",
	headshots: "Na cabeça",
	mvps: "MVP",
};

const ledgerReasons: Record<string, string> = { compra: "Compra na loja", partida: "Prêmio de partida", desafio: "Prêmio de desafio", promocao: "Promoção de patente", painel: "Painel" };
const sources: Record<string, string> = { loja: "Comprado", painel: "Dado pelo painel", desafio: "Prêmio de desafio" };

export function PlayerDetail() {
	const id = Number(useParams().id);
	const { can } = useSession();
	const refresh = useRefresh();
	const content = useContent();
	const file = useQuery({ queryKey: ["player", id], queryFn: () => api.get<PlayerFile>(`/players/${id}`), refetchInterval: 15_000 });
	const [dialog, setDialog] = useState<"ban" | "unban" | "kick" | "currency" | "item" | null>(null);
	const [revoke, setRevoke] = useState<string | null>(null);

	const [reason, setReason] = useState("");
	const [hours, setHours] = useState("24");
	const [currency, setCurrency] = useState("presas");
	const [direction, setDirection] = useState("dar");
	const [amount, setAmount] = useState("");
	const [itemId, setItemId] = useState("");
	const [days, setDays] = useState("30");

	if (file.isPending) return <Loading />;
	if (file.isError) return <ErrorNote error={file.error} retry={() => void file.refetch()} />;
	const { player, inventory, ledger, bans, matches, events, sameIp } = file.data;

	const items = new Map<string, CatalogItem>((content.data?.catalog ?? []).map((i) => [i.id, i]));
	const done = () => refresh("player", "players", "overview", "bans");
	const open = (which: typeof dialog) => {
		setReason("");
		setAmount("");
		setDialog(which);
	};
	const nextRank = xpForRank(player.rank + 1);
	const kd = player.stats.deaths > 0 ? (player.stats.kills / player.stats.deaths).toFixed(2) : player.stats.kills > 0 ? "∞" : "–";
	const headshotRate = player.stats.kills > 0 ? Math.round((player.stats.headshots / player.stats.kills) * 100) : 0;

	return (
		<>
			<Link to="/jogadores" className="mb-3 inline-flex items-center gap-1.5 text-[13px] text-ink-2 hover:text-ink">
				<ArrowLeft className="size-3.5" />
				Jogadores
			</Link>

			<header className="mb-5 flex flex-wrap items-start justify-between gap-3">
				<div>
					<div className="flex flex-wrap items-center gap-3">
						<h1 className="display text-2xl">{player.name}</h1>
						<span className="num text-ink-3">#{player.id}</span>
						<PlayerState player={player} />
					</div>
					<p className="mt-1 text-ink-2">
						Conta criada em {when(player.createdAt)}. Última entrada {relative(player.lastLoginAt)}
						{player.lastIp && (
							<>
								, do endereço{" "}
								<Link to={`/seguranca?ip=${encodeURIComponent(player.lastIp)}`} className="num text-ink hover:underline">
									{player.lastIp}
								</Link>
							</>
						)}
						.
					</p>
					{player.banned && (
						<p className="mt-2 max-w-[70ch] rounded-md bg-danger-soft px-3 py-2 text-[13px]">
							<span className="font-medium text-danger">Banido {player.bannedUntil ? `até ${when(player.bannedUntil)}` : "para sempre"}.</span>{" "}
							{player.banReason && <>Motivo: {player.banReason}</>}
						</p>
					)}
				</div>

				<div className="flex flex-wrap gap-2">
					{can("economy") && (
						<>
							<Button onClick={() => open("currency")}>
								<Coins />
								Dar ou tirar moeda
							</Button>
							<Button onClick={() => open("item")}>
								<Gift />
								Dar item
							</Button>
						</>
					)}
					{can("moderate") && (
						<>
							<Button onClick={() => open("kick")}>
								<PlugZap />
								Derrubar conexão
							</Button>
							{player.banned ? (
								<Button variant="primary" onClick={() => open("unban")}>
									<ShieldCheck />
									Desbanir
								</Button>
							) : (
								<Button variant="danger" onClick={() => open("ban")}>
									<Ban />
									Banir
								</Button>
							)}
						</>
					)}
				</div>
			</header>

			<Card className="mb-4 grid grid-cols-2 divide-line sm:grid-cols-3 lg:grid-cols-6 lg:divide-x">
				<Stat label="Patente" value={player.rank} note={nextRank > player.xp ? `faltam ${n(nextRank - player.xp)} XP para a próxima` : "no topo"} />
				<Stat label="XP" value={n(player.xp)} />
				<Stat label="Presas" value={n(player.presas)} />
				<Stat label="Sangue" value={n(player.sangue)} />
				<Stat label="Abates por morte" value={kd} note={`${n(player.stats.kills)} abates, ${n(player.stats.deaths)} mortes`} />
				<Stat
					label="Abates na cabeça"
					value={`${headshotRate}%`}
					note={`${n(player.stats.headshots)} de ${n(player.stats.kills)}`}
					tone={player.stats.kills >= 50 && headshotRate >= 70 ? "danger" : undefined}
				/>
			</Card>

			<Tabs defaultValue={events.some((e) => e.severity >= 2) ? "alertas" : "inventario"}>
				<TabsList>
					<TabsTrigger value="inventario">Inventário ({inventory.length})</TabsTrigger>
					<TabsTrigger value="extrato">Extrato</TabsTrigger>
					<TabsTrigger value="partidas">Partidas</TabsTrigger>
					<TabsTrigger value="alertas">Alertas e eventos ({events.length})</TabsTrigger>
					<TabsTrigger value="banimentos">Banimentos ({bans.length})</TabsTrigger>
					<TabsTrigger value="numeros">Números</TabsTrigger>
				</TabsList>

				<TabsContent value="inventario" className="pt-4">
					{inventory.length === 0 ? (
						<Card>
							<Empty title="Nenhum item além dos que vêm com a conta">O que ele comprar, ganhar em desafio ou receber pelo painel aparece aqui.</Empty>
						</Card>
					) : (
						<div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
							{inventory.map((owned) => {
								const item = items.get(owned.id);
								return (
									<Card key={owned.id} className="overflow-hidden">
										<ItemArt image={item?.image ?? ""} name={item?.name ?? owned.id} rarity={item?.rarity ?? 0} className="aspect-[16/9]" />
										<div className="p-3">
											<div className="flex items-start justify-between gap-2">
												<p className="font-medium">{item?.name ?? owned.id}</p>
												{can("economy") && (
													<button type="button" className="rounded p-1 text-ink-3 hover:bg-danger-soft hover:text-danger" onClick={() => setRevoke(owned.id)} aria-label={`Tirar ${item?.name ?? owned.id}`}>
														<Trash2 className="size-3.5" />
													</button>
												)}
											</div>
											{item && <Rarity value={item.rarity} />}
											<p className="mt-1.5 text-[13px] text-ink-2">
												{owned.uses > 0 ? `${owned.uses} ${owned.uses === 1 ? "uso" : "usos"}` : owned.expiresAt ? `Vence ${relative(owned.expiresAt)}` : "Para sempre"}
											</p>
											<p className="text-xs text-ink-3">
												{sources[owned.source] ?? owned.source} em {when(owned.acquiredAt)}
											</p>
										</div>
									</Card>
								);
							})}
						</div>
					)}
				</TabsContent>

				<TabsContent value="extrato" className="pt-4">
					<Card>
						{ledger.length === 0 ? (
							<Empty title="Nenhuma movimentação">Compras, prêmios e canetadas do painel aparecem aqui.</Empty>
						) : (
							<Table>
								<thead>
									<tr>
										<Th>Quando</Th>
										<Th>O quê</Th>
										<Th>Referência</Th>
										<Th className="text-right">Valor</Th>
										<Th className="text-right">Saldo depois</Th>
									</tr>
								</thead>
								<tbody>
									{ledger.map((l) => (
										<Tr key={l.id}>
											<Td className="text-ink-2">{when(l.at)}</Td>
											<Td>{ledgerReasons[l.reason] ?? l.reason}</Td>
											<Td className="max-w-[40ch] truncate text-ink-2">{items.get(l.ref)?.name ?? l.ref}</Td>
											<Td className={`text-right font-medium ${l.amount < 0 ? "text-danger" : "text-good"}`}>
												{l.amount > 0 ? "+" : "−"}
												{n(Math.abs(l.amount))} {currencyNames[l.currency] ?? l.currency}
											</Td>
											<Td className="text-right">{n(l.balance)}</Td>
										</Tr>
									))}
								</tbody>
							</Table>
						)}
					</Card>
				</TabsContent>

				<TabsContent value="partidas" className="pt-4">
					<Card>
						{matches.length === 0 ? (
							<Empty title="Ainda não jogou online" />
						) : (
							<Table>
								<thead>
									<tr>
										<Th>Quando</Th>
										<Th>Mapa</Th>
										<Th>Resultado</Th>
										<Th className="text-right">Abates</Th>
										<Th className="text-right">Mortes</Th>
										<Th className="text-right">Assist.</Th>
										<Th className="text-right">XP</Th>
										<Th className="text-right">Presas</Th>
									</tr>
								</thead>
								<tbody>
									{matches.map((m, i) => {
										const won = m.winner !== 0 && m.winner === m.team;
										return (
											<Tr key={i}>
												<Td className="text-ink-2">{when(m.endedAt)}</Td>
												<Td>{content.data?.maps.find((map) => map.id === m.map)?.name ?? m.map}</Td>
												<Td>
													<Badge tone={m.winner === 0 ? "neutral" : won ? "good" : "danger"}>{m.winner === 0 ? "Empate" : won ? "Vitória" : "Derrota"}</Badge>{" "}
													<span className="num text-ink-2">
														{m.scoreBlue} a {m.scoreOrange}
													</span>{" "}
													{m.mvp && <Badge tone="accent">MVP</Badge>}
												</Td>
												<Td className="text-right">{m.kills}</Td>
												<Td className="text-right">{m.deaths}</Td>
												<Td className="text-right">{m.assists}</Td>
												<Td className="text-right">+{n(m.xp)}</Td>
												<Td className="text-right">+{n(m.presas)}</Td>
											</Tr>
										);
									})}
								</tbody>
							</Table>
						)}
					</Card>
				</TabsContent>

				<TabsContent value="alertas" className="pt-4">
					<Card>
						{events.length === 0 ? (
							<Empty title="Nada registrado para esta conta" />
						) : (
							<Table>
								<thead>
									<tr>
										<Th>Quando</Th>
										<Th>Gravidade</Th>
										<Th>O quê</Th>
										<Th>Endereço</Th>
										<Th>Detalhe</Th>
									</tr>
								</thead>
								<tbody>
									{events.map((e) => (
										<Tr key={e.id}>
											<Td className="text-ink-2">{when(e.at)}</Td>
											<Td>
												<SeverityBadge value={e.severity} />
											</Td>
											<Td>{eventNames[e.kind] ?? e.kind}</Td>
											<Td className="num text-ink-2">{e.ip}</Td>
											<Td className="text-ink-2">{e.detail}</Td>
										</Tr>
									))}
								</tbody>
							</Table>
						)}
					</Card>
					{sameIp.length > 0 && (
						<Card className="mt-4">
							<CardHeader title="Outras contas no mesmo endereço" description="Entraram por último do mesmo IP. Pode ser a mesma casa — ou a mesma pessoa com outra conta." />
							<CardBody className="flex flex-wrap gap-2">
								{sameIp.map((other) => (
									<Button key={other.id} asChild size="sm">
										<Link to={`/jogadores/${other.id}`}>
											{other.name} <span className="num text-ink-3">#{other.id}</span>
										</Link>
									</Button>
								))}
							</CardBody>
						</Card>
					)}
				</TabsContent>

				<TabsContent value="banimentos" className="pt-4">
					<Card>{bans.length === 0 ? <Empty title="Nunca foi banido" /> : <BanList bans={bans} hideAccount />}</Card>
				</TabsContent>

				<TabsContent value="numeros" className="pt-4">
					<Card className="grid grid-cols-3 sm:grid-cols-5 lg:grid-cols-9">
						{Object.entries(statNames).map(([key, label]) => (
							<Stat key={key} label={label} value={n(player.stats[key] ?? 0)} />
						))}
					</Card>
				</TabsContent>
			</Tabs>

			<ActionDialog
				open={dialog === "ban"}
				onOpenChange={(o) => !o && setDialog(null)}
				title={`Banir ${player.name}`}
				description="A conexão cai agora, os logins dele param de valer e ele não entra até o fim do prazo. O motivo aparece para ele ao tentar entrar."
				action="Banir"
				danger
				done={`${player.name} foi banido.`}
				disabled={reason.trim().length < 3}
				onConfirm={() => api.post(`/players/${id}/ban`, { reason, hours: Number(hours) }).then(done)}
			>
				<Field label="Por quanto tempo">
					<Select value={hours} onChange={setHours} options={banDurations} />
				</Field>
				<Field label="Motivo" hint="Fica no histórico e na auditoria.">
					<Textarea value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ex.: aimbot na partida de 20/11, 18 abates na cabeça em 2 minutos" required />
				</Field>
			</ActionDialog>

			<ActionDialog
				open={dialog === "unban"}
				onOpenChange={(o) => !o && setDialog(null)}
				title={`Desbanir ${player.name}`}
				description="Ele volta a poder entrar com a senha dele."
				action="Desbanir"
				done={`${player.name} foi desbanido.`}
				onConfirm={() => api.post(`/players/${id}/unban`).then(done)}
			/>

			<ActionDialog
				open={dialog === "kick"}
				onOpenChange={(o) => !o && setDialog(null)}
				title={`Derrubar a conexão de ${player.name}`}
				description="Ele sai do saguão e todos os logins dele param de valer. Pode entrar de novo com a senha."
				action="Derrubar conexão"
				done="Conexão derrubada."
				onConfirm={() => api.post(`/players/${id}/kick`).then(done)}
			/>

			<ActionDialog
				open={dialog === "currency"}
				onOpenChange={(o) => !o && setDialog(null)}
				title={`Moeda de ${player.name}`}
				description={`Hoje ele tem ${n(player.presas)} Presas e ${n(player.sangue)} de Sangue.`}
				action={direction === "dar" ? "Dar" : "Tirar"}
				danger={direction === "tirar"}
				done="Saldo atualizado."
				disabled={!(Number(amount) > 0) || reason.trim().length < 3}
				onConfirm={() => api.post(`/players/${id}/currency`, { currency, amount: (direction === "dar" ? 1 : -1) * Math.round(Number(amount)), reason }).then(done)}
			>
				<div className="grid grid-cols-3 gap-3">
					<Field label="Ação">
						<Select value={direction} onChange={setDirection} options={[{ value: "dar", label: "Dar" }, { value: "tirar", label: "Tirar" }]} />
					</Field>
					<Field label="Quanto">
						<Input type="number" min={1} value={amount} onChange={(e) => setAmount(e.target.value)} required className="num" />
					</Field>
					<Field label="Moeda">
						<Select value={currency} onChange={setCurrency} options={[{ value: "presas", label: "Presas" }, { value: "sangue", label: "Sangue" }]} />
					</Field>
				</div>
				<Field label="Motivo" hint="Fica no extrato dele e na auditoria.">
					<Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ex.: compensação pela queda do servidor" required />
				</Field>
			</ActionDialog>

			<ActionDialog
				open={dialog === "item"}
				onOpenChange={(o) => !o && setDialog(null)}
				title={`Dar item para ${player.name}`}
				action="Dar item"
				done="Item entregue."
				disabled={!itemId}
				onConfirm={() => api.post(`/players/${id}/items`, { itemId, days: Number(days) }).then(done)}
			>
				<Field label="Item">
					<Select
						value={itemId}
						onChange={setItemId}
						placeholder="Escolha um item"
						options={(content.data?.catalog ?? []).filter((i) => !i.starter).map((i) => ({ value: i.id, label: i.name }))}
					/>
				</Field>
				<Field label="Por quanto tempo" hint="Se ele já tem o item, vale o prazo novo.">
					<Select value={days} onChange={setDays} options={itemDurations} />
				</Field>
			</ActionDialog>

			<ActionDialog
				open={revoke !== null}
				onOpenChange={(o) => !o && setRevoke(null)}
				title={`Tirar ${items.get(revoke ?? "")?.name ?? revoke} do inventário`}
				description="O item some do inventário dele. O que ele pagou não volta sozinho."
				action="Tirar item"
				danger
				done="Item tirado."
				onConfirm={() => api.delete(`/players/${id}/items/${encodeURIComponent(revoke ?? "")}`).then(done)}
			/>
		</>
	);
}
