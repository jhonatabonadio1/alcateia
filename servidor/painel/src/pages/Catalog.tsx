import { Flame, Search } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { ActionDialog } from "@/components/ConfirmDialog";
import { Empty, ErrorNote, ItemArt, Loading, PageHeader, Rarity, Toggle } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { api, messageOf } from "@/lib/api";
import { currencyNames, durationNames, kindNames, n, rarityNames, teamNames } from "@/lib/format";
import { useContent, useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { CatalogItem } from "@/lib/types";
import { cn } from "@/lib/utils";

const filters = [
	{ value: "todos", label: "Todos" },
	{ value: "venda", label: "À venda" },
	{ value: "serie", label: "Vêm com a conta" },
	{ value: "desligados", label: "Desligados" },
];

/** O menor preço à venda, já com a promoção que estiver valendo. */
function fromPrice(item: CatalogItem) {
	const prices = item.prices.filter((p) => p >= 0);
	if (prices.length === 0) return null;
	const lowest = Math.min(...prices);
	return item.discount > 0 ? Math.max(1, Math.round((lowest * (100 - item.discount)) / 100)) : lowest;
}

function ItemCard({ item, daily, editable, onEdit, onToggle }: { item: CatalogItem; daily: boolean; editable: boolean; onEdit: () => void; onToggle: (enabled: boolean) => void }) {
	const price = fromPrice(item);
	return (
		<Card className={cn("group flex flex-col overflow-hidden", !item.enabled && "opacity-70")}>
			<button type="button" onClick={onEdit} disabled={!editable} className="text-left disabled:cursor-default" aria-label={`Editar ${item.name}`}>
				<ItemArt image={item.image} name={item.name} rarity={item.rarity} dim={!item.enabled} className="aspect-[16/9]" />
			</button>
			<div className="flex flex-1 flex-col gap-2 p-3">
				<div>
					<p className="display flex items-center gap-1.5 text-[15px] leading-snug">
						{item.name}
						{item.hot && <Flame className="size-3.5 text-accent" aria-label="Em destaque na loja" />}
					</p>
					<p className="text-[13px] text-ink-3">{item.type || kindNames[item.kind]}</p>
				</div>
				<div className="flex flex-wrap items-center gap-x-3 gap-y-1">
					<Rarity value={item.rarity} />
					{item.minRank > 0 && <span className="text-xs text-ink-2">Patente {item.minRank}</span>}
					{item.team > 0 && <span className="text-xs text-ink-2">{teamNames[item.team]}</span>}
				</div>
				<div className="mt-auto flex items-end justify-between gap-2 pt-1">
					<div className="min-w-0">
						{item.starter ? (
							<Badge>Vem com a conta</Badge>
						) : price !== null ? (
							<p className="num text-[13px] text-ink-2">
								a partir de <span className="font-semibold text-ink">{n(price)}</span> {currencyNames[item.currency]}{" "}
								{item.discount > 0 && <Badge tone="accent">{daily ? `Item do dia −${item.discount}%` : `−${item.discount}%`}</Badge>}
							</p>
						) : (
							<Badge>{item.minRank > 0 ? `Libera na patente ${item.minRank}` : "Fora da loja"}</Badge>
						)}
					</div>
					{editable && <Switch checked={item.enabled} onCheckedChange={onToggle} aria-label={item.enabled ? `Desligar ${item.name}` : `Ligar ${item.name}`} />}
				</div>
			</div>
		</Card>
	);
}

function EditItem({ item, onClose }: { item: CatalogItem | null; onClose: () => void }) {
	const refresh = useRefresh();
	const [draft, setDraft] = useState<CatalogItem | null>(item);
	const [prices, setPrices] = useState<string[]>([]);

	useEffect(() => {
		setDraft(item);
		setPrices(durationNames.map((_, i) => (item && item.prices[i] >= 0 ? String(item.prices[i]) : "")));
	}, [item]);

	if (!draft) return null;
	const set = (patch: Partial<CatalogItem>) => setDraft({ ...draft, ...patch });
	const sellable = prices.some((p) => p !== "");
	const paidAllowed = draft.kind === "character" || draft.kind === "service";

	return (
		<ActionDialog
			open={item !== null}
			onOpenChange={(o) => !o && onClose()}
			title={draft.name}
			description="O que muda aqui vale na loja na hora, para todo mundo. Os números de combate da arma ficam no jogo; aqui é a economia."
			action="Salvar"
			done={`${draft.name} atualizado.`}
			onConfirm={() =>
				api
					.put(`/catalog/${draft.id}`, {
						name: draft.name,
						rarity: draft.rarity,
						minRank: draft.minRank,
						currency: draft.currency,
						starter: draft.starter,
						enabled: draft.enabled,
						hot: draft.hot,
						prices: sellable ? prices.map((p) => (p === "" ? -1 : Math.max(0, Math.round(Number(p))))) : [],
					})
					.then(() => refresh("content"))
			}
		>
			<ItemArt image={draft.image} name={draft.name} rarity={draft.rarity} className="aspect-[16/7] rounded-md" />
			<Field label="Nome">
				<Input value={draft.name} onChange={(e) => set({ name: e.target.value })} required />
			</Field>
			<div className="grid grid-cols-3 gap-3">
				<Field label="Raridade">
					<Select value={String(draft.rarity)} onChange={(v) => set({ rarity: Number(v) })} options={rarityNames.map((name, i) => ({ value: String(i), label: name }))} />
				</Field>
				<Field label="Patente mínima">
					<Input type="number" min={0} max={29} value={draft.minRank} onChange={(e) => set({ minRank: Number(e.target.value) })} className="num" />
				</Field>
				<Field label="Moeda">
					<Select
						value={draft.currency}
						onChange={(v) => set({ currency: v })}
						disabled={!paidAllowed}
						options={[{ value: "presas", label: "Presas" }, ...(paidAllowed ? [{ value: "sangue", label: "Sangue" }] : [])]}
					/>
				</Field>
			</div>
			{!paidAllowed && <p className="-mt-1 text-xs text-ink-3">Só personagem e serviço podem custar Sangue: moeda paga nunca compra poder.</p>}

			<fieldset>
				<legend className="mb-1.5 text-[13px] font-medium text-ink-2">Preço por duração</legend>
				<div className="grid grid-cols-5 gap-2">
					{durationNames.map((name, i) => (
						<label key={name} className="flex flex-col gap-1">
							<span className="text-xs text-ink-3">{name}</span>
							<Input
								type="number"
								min={0}
								value={prices[i] ?? ""}
								onChange={(e) => setPrices(prices.map((p, k) => (k === i ? e.target.value : p)))}
								placeholder="–"
								className="num px-2"
								disabled={draft.starter}
							/>
						</label>
					))}
				</div>
				<p className="mt-1.5 text-xs text-ink-3">Deixe em branco a duração que não se vende. Tudo em branco tira o item da loja.</p>
			</fieldset>

			<div className="divide-y divide-line border-y border-line">
				<Toggle label="Ligado" hint="Desligado, o item some da loja e do jogo.">
					<Switch checked={draft.enabled} onCheckedChange={(enabled) => set({ enabled })} />
				</Toggle>
				<Toggle label="Vem com a conta" hint="Todo jogador tem, de graça e para sempre.">
					<Switch checked={draft.starter} onCheckedChange={(starter) => set({ starter })} />
				</Toggle>
				<Toggle label="Em destaque" hint="Aparece na aba de destaque da loja.">
					<Switch checked={draft.hot} onCheckedChange={(hot) => set({ hot })} />
				</Toggle>
			</div>
		</ActionDialog>
	);
}

export function Catalog() {
	const { can } = useSession();
	const refresh = useRefresh();
	const content = useContent();
	const [kind, setKind] = useState("weapon");
	const [filter, setFilter] = useState("todos");
	const [query, setQuery] = useState("");
	const [editing, setEditing] = useState<CatalogItem | null>(null);

	const catalog = content.data?.catalog ?? [];
	const shown = useMemo(
		() =>
			catalog.filter(
				(i) =>
					i.kind === kind &&
					(filter === "todos" || (filter === "venda" && i.forSale) || (filter === "serie" && i.starter) || (filter === "desligados" && !i.enabled)) &&
					(query === "" || i.name.toLowerCase().includes(query.toLowerCase()) || i.id.includes(query.toLowerCase())),
			),
		[catalog, kind, filter, query],
	);

	if (content.isPending) return <Loading />;
	if (content.isError) return <ErrorNote error={content.error} retry={() => void content.refetch()} />;

	async function toggle(item: CatalogItem, enabled: boolean) {
		try {
			await api.put(`/catalog/${item.id}`, { enabled });
			toast.success(enabled ? `${item.name} ligado.` : `${item.name} desligado.`);
			await refresh("content");
		} catch (e) {
			toast.error(messageOf(e));
		}
	}

	return (
		<>
			<PageHeader
				title="Armas, skins e itens"
				description={`Tudo o que existe para possuir no jogo: ${n(catalog.length)} itens, ${n(catalog.filter((i) => i.forSale).length)} à venda. Clique num item para mudar preço, raridade ou patente.`}
			/>

			<Tabs value={kind} onValueChange={setKind}>
				<TabsList>
					{content.data.kinds
						.filter((k) => catalog.some((i) => i.kind === k))
						.map((k) => (
							<TabsTrigger key={k} value={k}>
								{kindNames[k] ?? k} ({catalog.filter((i) => i.kind === k).length})
							</TabsTrigger>
						))}
				</TabsList>
			</Tabs>

			<div className="my-3 flex flex-wrap items-center gap-3">
				<div className="relative w-full max-w-xs">
					<Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-3" />
					<Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Procurar item" className="pl-9" aria-label="Procurar item" />
				</div>
				<div className="flex gap-1" role="group" aria-label="Filtro">
					{filters.map((f) => (
						<button
							key={f.value}
							type="button"
							onClick={() => setFilter(f.value)}
							aria-pressed={filter === f.value}
							className={cn("rounded-md px-2.5 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2", filter === f.value && "bg-surface-3 text-ink")}
						>
							{f.label}
						</button>
					))}
				</div>
			</div>

			{shown.length === 0 ? (
				<Card>
					<Empty title="Nenhum item com esses filtros" />
				</Card>
			) : (
				<div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
					{shown.map((item) => (
						<ItemCard key={item.id} item={item} daily={content.data.dailyItems.includes(item.id)} editable={can("content")} onEdit={() => setEditing(item)} onToggle={(enabled) => void toggle(item, enabled)} />
					))}
				</div>
			)}

			<EditItem item={editing} onClose={() => setEditing(null)} />
		</>
	);
}
