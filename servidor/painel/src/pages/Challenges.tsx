import { Pencil, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { ActionDialog } from "@/components/ConfirmDialog";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api, messageOf } from "@/lib/api";
import { metricNames, n, periodNames, toLocalInput, when } from "@/lib/format";
import { useContent, useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { Challenge } from "@/lib/types";

const periodHelp: Record<string, string> = {
	daily: "Recomeçam todo dia à meia-noite de Brasília.",
	weekly: "Recomeçam toda segunda-feira à meia-noite de Brasília.",
	permanent: "Fazem-se uma vez na vida da conta e podem dar um título.",
	event: "Valem entre duas datas: o fim de semana, o feriado, o lançamento.",
};

const blank = (period: string): Challenge => ({
	id: "",
	period,
	text: "",
	metric: "kills",
	target: 10,
	presas: 200,
	xp: 20,
	sangue: 0,
	itemId: null,
	itemDays: 0,
	title: null,
	startsAt: period === "event" ? new Date().toISOString() : null,
	endsAt: period === "event" ? new Date(Date.now() + 3 * 86_400_000).toISOString() : null,
	active: true,
	sort: 9999,
	running: true,
});

/** Um identificador a partir do texto: "Faça 20 abates" vira "faca_20_abates". */
const slug = (text: string) =>
	text
		.normalize("NFD")
		.replace(/[̀-ͯ]/g, "")
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, "_")
		.replace(/^_+|_+$/g, "")
		.slice(0, 40);

function Reward({ c, itemName }: { c: Challenge; itemName: (id: string) => string }) {
	const parts = [
		c.presas > 0 && `${n(c.presas)} Presas`,
		c.xp > 0 && `${n(c.xp)} XP`,
		c.sangue > 0 && `${n(c.sangue)} de Sangue`,
		c.itemId && `${itemName(c.itemId)}${c.itemDays > 0 ? ` por ${c.itemDays} dias` : ""}`,
		c.title && `título “${c.title}”`,
	].filter(Boolean);
	return <>{parts.length > 0 ? parts.join(", ") : "Sem prêmio"}</>;
}

export function Challenges() {
	const { can } = useSession();
	const refresh = useRefresh();
	const content = useContent();
	const [period, setPeriod] = useState("daily");
	const [draft, setDraft] = useState<Challenge | null>(null);
	const [isNew, setIsNew] = useState(false);
	const [removing, setRemoving] = useState<Challenge | null>(null);

	if (content.isPending) return <Loading />;
	if (content.isError) return <ErrorNote error={content.error} retry={() => void content.refetch()} />;
	const { challenges, catalog, metrics, periods } = content.data;
	const shown = challenges.filter((c) => c.period === period);
	const itemName = (id: string) => catalog.find((i) => i.id === id)?.name ?? id;
	const set = (patch: Partial<Challenge>) => draft && setDraft({ ...draft, ...patch });

	async function toggle(c: Challenge, active: boolean) {
		try {
			await api.put(`/challenges/${c.id}`, { active });
			toast.success(active ? "Desafio ligado." : "Desafio desligado.");
			await refresh("content");
		} catch (e) {
			toast.error(messageOf(e));
		}
	}

	const id = draft ? (isNew ? draft.id || slug(draft.text) : draft.id) : "";

	return (
		<>
			<PageHeader
				title="Desafios"
				description="As missões do jogo. O progresso anda com o resultado que o servidor de partida manda — o cliente não consegue mentir — e o prêmio sai uma vez por período."
			>
				{can("content") && (
					<Button
						variant="primary"
						onClick={() => {
							setIsNew(true);
							setDraft(blank(period));
						}}
					>
						<Plus />
						Novo desafio
					</Button>
				)}
			</PageHeader>

			<Tabs value={period} onValueChange={setPeriod}>
				<TabsList>
					{periods.map((p) => (
						<TabsTrigger key={p} value={p}>
							{periodNames[p] ?? p} ({challenges.filter((c) => c.period === p).length})
						</TabsTrigger>
					))}
				</TabsList>
			</Tabs>
			<p className="my-3 text-[13px] text-ink-3">{periodHelp[period]}</p>

			<Card>
				{shown.length === 0 ? (
					<Empty title={period === "event" ? "Nenhum desafio de evento" : "Nenhum desafio aqui"}>
						{period === "event" && "Crie um para o próximo fim de semana: “Faça 50 abates como lobo” com um prêmio que só sai nesses dias."}
					</Empty>
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Desafio</Th>
								<Th>O que conta</Th>
								<Th className="text-right">Alvo</Th>
								<Th>Prêmio</Th>
								{period === "event" && <Th>Vale</Th>}
								<Th>Ligado</Th>
								<Th />
							</tr>
						</thead>
						<tbody>
							{shown.map((c) => (
								<Tr key={c.id}>
									<Td>
										<p className="font-medium">{c.text}</p>
										<p className="text-xs text-ink-3">{c.id}</p>
									</Td>
									<Td className="text-ink-2">{metricNames[c.metric] ?? c.metric}</Td>
									<Td className="text-right">{n(c.target)}</Td>
									<Td className="max-w-[40ch] text-ink-2">
										<Reward c={c} itemName={itemName} />
									</Td>
									{period === "event" && (
										<Td className="text-ink-2">
											{when(c.startsAt)} a {when(c.endsAt)} {c.running ? <Badge tone="good">Valendo</Badge> : c.endsAt && new Date(c.endsAt) < new Date() ? <Badge>Acabou</Badge> : <Badge tone="primary">Agendado</Badge>}
										</Td>
									)}
									<Td>
										<Switch checked={c.active} disabled={!can("content")} onCheckedChange={(active) => void toggle(c, active)} aria-label={c.active ? "Desligar desafio" : "Ligar desafio"} />
									</Td>
									<Td className="text-right whitespace-nowrap">
										{can("content") && (
											<>
												<Button
													size="icon"
													variant="ghost"
													onClick={() => {
														setIsNew(false);
														setDraft(c);
													}}
													aria-label="Editar desafio"
												>
													<Pencil />
												</Button>
												<Button size="icon" variant="ghost" onClick={() => setRemoving(c)} aria-label="Remover desafio">
													<Trash2 />
												</Button>
											</>
										)}
									</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>

			{draft && (
				<ActionDialog
					open
					onOpenChange={(o) => !o && setDraft(null)}
					title={isNew ? "Novo desafio" : "Editar desafio"}
					action={isNew ? "Criar desafio" : "Salvar"}
					done={isNew ? "Desafio criado." : "Desafio atualizado."}
					disabled={!draft.text.trim() || !id || draft.target < 1}
					onConfirm={() =>
						api
							.put(`/challenges/${encodeURIComponent(id)}`, {
								period: draft.period,
								text: draft.text.trim(),
								metric: draft.metric,
								target: draft.target,
								presas: draft.presas,
								xp: draft.xp,
								sangue: draft.sangue,
								itemId: draft.itemId || null,
								itemDays: draft.itemDays,
								title: draft.title || null,
								active: draft.active,
								startsAt: draft.period === "event" ? draft.startsAt : null,
								endsAt: draft.period === "event" ? draft.endsAt : null,
								sort: draft.sort,
							})
							.then(() => refresh("content"))
							.then(() => setPeriod(draft.period))
					}
				>
					<Field label="O que o jogador lê">
						<Input value={draft.text} onChange={(e) => set({ text: e.target.value })} placeholder="Ex.: Faça 20 abates como lobo" maxLength={120} required autoFocus />
					</Field>
					<div className="grid grid-cols-[1fr_1fr_90px] gap-3">
						<Field label="Período">
							<Select
								value={draft.period}
								onChange={(p) => set({ period: p, startsAt: p === "event" ? (draft.startsAt ?? new Date().toISOString()) : null, endsAt: p === "event" ? (draft.endsAt ?? new Date(Date.now() + 3 * 86_400_000).toISOString()) : null })}
								options={periods.map((p) => ({ value: p, label: periodNames[p] ?? p }))}
							/>
						</Field>
						<Field label="O que conta">
							<Select value={draft.metric} onChange={(metric) => set({ metric })} options={metrics.map((m) => ({ value: m, label: metricNames[m] ?? m }))} />
						</Field>
						<Field label="Alvo">
							<Input type="number" min={1} value={draft.target} onChange={(e) => set({ target: Number(e.target.value) })} className="num" required />
						</Field>
					</div>

					{draft.period === "event" && (
						<div className="grid grid-cols-2 gap-3">
							<Field label="Começa">
								<Input type="datetime-local" value={draft.startsAt ? toLocalInput(new Date(draft.startsAt)) : ""} onChange={(e) => set({ startsAt: e.target.value ? new Date(e.target.value).toISOString() : null })} required />
							</Field>
							<Field label="Acaba">
								<Input type="datetime-local" value={draft.endsAt ? toLocalInput(new Date(draft.endsAt)) : ""} onChange={(e) => set({ endsAt: e.target.value ? new Date(e.target.value).toISOString() : null })} required />
							</Field>
						</div>
					)}

					<fieldset>
						<legend className="mb-1.5 text-[13px] font-medium text-ink-2">Prêmio</legend>
						<div className="grid grid-cols-3 gap-3">
							<Field label="Presas">
								<Input type="number" min={0} value={draft.presas} onChange={(e) => set({ presas: Number(e.target.value) })} className="num" />
							</Field>
							<Field label="XP">
								<Input type="number" min={0} value={draft.xp} onChange={(e) => set({ xp: Number(e.target.value) })} className="num" />
							</Field>
							<Field label="Sangue">
								<Input type="number" min={0} value={draft.sangue} onChange={(e) => set({ sangue: Number(e.target.value) })} className="num" />
							</Field>
						</div>
						<div className="mt-3 grid grid-cols-[1fr_110px] gap-3">
							<Field label="Item (opcional)">
								<Select
									value={draft.itemId ?? "nenhum"}
									onChange={(v) => set({ itemId: v === "nenhum" ? null : v })}
									options={[{ value: "nenhum", label: "Nenhum item" }, ...catalog.filter((i) => !i.starter).map((i) => ({ value: i.id, label: i.name }))]}
								/>
							</Field>
							<Field label="Dias do item" hint="0 = para sempre">
								<Input type="number" min={0} value={draft.itemDays} onChange={(e) => set({ itemDays: Number(e.target.value) })} className="num" disabled={!draft.itemId} />
							</Field>
						</div>
						{draft.period === "permanent" && (
							<Field label="Título no perfil (opcional)" className="mt-3">
								<Input value={draft.title ?? ""} onChange={(e) => set({ title: e.target.value })} placeholder="Ex.: Matador" maxLength={40} />
							</Field>
						)}
					</fieldset>

					{isNew && (
						<Field label="Identificador" hint="Não muda depois: é por ele que o progresso de cada jogador fica guardado.">
							<Input value={id} onChange={(e) => set({ id: slug(e.target.value) })} className="num" required />
						</Field>
					)}
				</ActionDialog>
			)}

			<ActionDialog
				open={removing !== null}
				onOpenChange={(o) => !o && setRemoving(null)}
				title="Remover desafio"
				description={removing ? `“${removing.text}” some para todos os jogadores. Quem já recebeu o prêmio fica com ele. Para tirar só por um tempo, desligue em vez de remover.` : ""}
				action="Remover"
				danger
				done="Desafio removido."
				onConfirm={() => api.delete(`/challenges/${encodeURIComponent(removing?.id ?? "")}`).then(() => refresh("content"))}
			/>
		</>
	);
}
