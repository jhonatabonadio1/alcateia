import { Plus, Trash2 } from "lucide-react";
import { useState } from "react";
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
import { currencyNames, durationNames, n, toLocalInput, when } from "@/lib/format";
import { useContent, useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { Promotion } from "@/lib/types";

export function Promotions() {
	const { can } = useSession();
	const refresh = useRefresh();
	const content = useContent();
	const [adding, setAdding] = useState(false);
	const [removing, setRemoving] = useState<Promotion | null>(null);

	const [itemId, setItemId] = useState("");
	const [title, setTitle] = useState("");
	const [percent, setPercent] = useState("30");
	const [startsAt, setStartsAt] = useState("");
	const [endsAt, setEndsAt] = useState("");

	if (content.isPending) return <Loading />;
	if (content.isError) return <ErrorNote error={content.error} retry={() => void content.refetch()} />;
	const { catalog, promotions } = content.data;
	const item = (id: string) => catalog.find((i) => i.id === id);
	const chosen = item(itemId);

	function open() {
		setItemId("");
		setTitle("");
		setPercent("30");
		setStartsAt(toLocalInput(new Date()));
		setEndsAt(toLocalInput(new Date(Date.now() + 3 * 86_400_000)));
		setAdding(true);
	}

	return (
		<>
			<PageHeader title="Promoções" description="Desconto com hora para começar e acabar. Vale na loja sozinho, no horário marcado; se um item tiver duas promoções ao mesmo tempo, vale o maior desconto.">
				{can("content") && (
					<Button variant="primary" onClick={open}>
						<Plus />
						Nova promoção
					</Button>
				)}
			</PageHeader>

			<Card>
				{promotions.length === 0 ? (
					<Empty title="Nenhuma promoção até agora">Crie a primeira para baixar o preço de um item por alguns dias.</Empty>
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Item</Th>
								<Th>Nome da promoção</Th>
								<Th className="text-right">Desconto</Th>
								<Th>Situação</Th>
								<Th>Começa</Th>
								<Th>Acaba</Th>
								<Th />
							</tr>
						</thead>
						<tbody>
							{promotions.map((p) => (
								<Tr key={p.id} className={p.ended ? "text-ink-3" : undefined}>
									<Td className="font-medium">{item(p.itemId)?.name ?? p.itemId}</Td>
									<Td>{p.title}</Td>
									<Td className="text-right">−{p.percent}%</Td>
									<Td>{p.running ? <Badge tone="good">Valendo</Badge> : p.ended ? <Badge>Acabou</Badge> : <Badge tone="primary">Agendada</Badge>}</Td>
									<Td>{when(p.startsAt)}</Td>
									<Td>{when(p.endsAt)}</Td>
									<Td className="text-right">
										{can("content") && (
											<Button size="icon" variant="ghost" onClick={() => setRemoving(p)} aria-label="Remover promoção">
												<Trash2 />
											</Button>
										)}
									</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>

			<ActionDialog
				open={adding}
				onOpenChange={setAdding}
				title="Nova promoção"
				action="Criar promoção"
				done="Promoção criada."
				disabled={!itemId || !(Number(percent) >= 1)}
				onConfirm={() =>
					api
						.post("/promotions", { itemId, title, percent: Math.round(Number(percent)), startsAt: new Date(startsAt).toISOString(), endsAt: new Date(endsAt).toISOString() })
						.then(() => refresh("content"))
				}
			>
				<Field label="Item">
					<Select value={itemId} onChange={setItemId} placeholder="Escolha um item à venda" options={catalog.filter((i) => i.forSale).map((i) => ({ value: i.id, label: i.name }))} />
				</Field>
				<div className="grid grid-cols-[1fr_110px] gap-3">
					<Field label="Nome da promoção" hint="Aparece na loja. Opcional.">
						<Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Ex.: Fim de semana da AK" maxLength={60} />
					</Field>
					<Field label="Desconto (%)">
						<Input type="number" min={1} max={90} value={percent} onChange={(e) => setPercent(e.target.value)} className="num" required />
					</Field>
				</div>
				<div className="grid grid-cols-2 gap-3">
					<Field label="Começa">
						<Input type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} required />
					</Field>
					<Field label="Acaba">
						<Input type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} required />
					</Field>
				</div>
				{chosen && Number(percent) >= 1 && (
					<div className="rounded-md bg-surface-2 p-3 text-[13px]">
						<p className="mb-1.5 text-ink-3">Como fica na loja, em {currencyNames[chosen.currency]}</p>
						<ul className="num grid grid-cols-5 gap-2">
							{chosen.prices.map((price, i) =>
								price < 0 ? null : (
									<li key={i}>
										<span className="block text-xs text-ink-3">{durationNames[i]}</span>
										<span className="text-ink-3 line-through">{n(price)}</span>{" "}
										<span className="font-semibold">{n(Math.max(1, Math.round((price * (100 - Number(percent))) / 100)))}</span>
									</li>
								),
							)}
						</ul>
					</div>
				)}
			</ActionDialog>

			<ActionDialog
				open={removing !== null}
				onOpenChange={(o) => !o && setRemoving(null)}
				title="Remover promoção"
				description={removing ? `${item(removing.itemId)?.name ?? removing.itemId} volta ao preço cheio agora.` : ""}
				action="Remover"
				danger
				done="Promoção removida."
				onConfirm={() => api.delete(`/promotions/${removing?.id}`).then(() => refresh("content"))}
			/>
		</>
	);
}
