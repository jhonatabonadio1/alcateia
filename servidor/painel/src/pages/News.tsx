import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Empty, ErrorNote, Loading, PageHeader, Toggle } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader } from "@/components/ui/card";
import { Input, Textarea } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { api, messageOf } from "@/lib/api";
import { useContent, useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { GameMap, NewsBanner, NewsStory } from "@/lib/types";

/** Os tetos do servidor (ContentService.MaxBanners, MaxStories) e o que o jogo mostra de cada lista. */
const MAX_BANNERS = 8;
const MAX_STORIES = 12;
const SHOWN_BANNERS = 6;
const SHOWN_STORIES = 3;

const TARGETS = [
	{ value: "none", label: "Sem botão" },
	{ value: "jogar", label: "Jogar" },
	{ value: "loja", label: "Loja" },
	{ value: "inventario", label: "Inventário" },
	{ value: "missoes", label: "Missões" },
];

type Draft = { banners: NewsBanner[]; noticias: NewsStory[] };

/** Hoje, como aparece no cartão do jogo ("27/11"). */
function today() {
	const now = new Date();
	return `${String(now.getDate()).padStart(2, "0")}/${String(now.getMonth() + 1).padStart(2, "0")}`;
}

/**
 * As notícias da tela inicial do jogo: os banners grandes, que giram, e as
 * notícias curtas embaixo deles. O que se salva aqui chega ao jogo na
 * próxima vez que ele pedir os dados (ao entrar e de minuto em minuto).
 * Sem nada publicado, o jogo mostra as que vêm nele.
 */
export function News() {
	const content = useContent();
	const refresh = useRefresh();
	const { can } = useSession();
	const [draft, setDraft] = useState<Draft | null>(null);
	const [saving, setSaving] = useState(false);
	const [dirty, setDirty] = useState(false);

	useEffect(() => {
		if (content.data && !draft) {
			const saved = content.data.settings.noticias;
			setDraft(structuredClone({ banners: saved?.banners ?? [], noticias: saved?.noticias ?? [] }));
		}
	}, [content.data, draft]);

	if (content.isPending || !draft) return <Loading />;
	if (content.isError) return <ErrorNote error={content.error} retry={() => void content.refetch()} />;

	const maps = content.data.maps;
	const editable = can("content");

	function change(next: Draft) {
		setDraft(next);
		setDirty(true);
	}

	async function save() {
		if (!draft) return;
		const untitled = [...draft.banners, ...draft.noticias].some((item) => item.title.trim() === "");
		if (untitled) {
			toast.error("Toda notícia precisa de título.");
			return;
		}
		setSaving(true);
		try {
			await api.put("/settings/noticias", draft);
			toast.success("Notícias publicadas.");
			setDirty(false);
			await refresh("content");
		} catch (e) {
			toast.error(messageOf(e));
		} finally {
			setSaving(false);
		}
	}

	function move<T>(list: T[], index: number, step: number) {
		const next = [...list];
		const to = index + step;
		if (to < 0 || to >= next.length) return next;
		[next[index], next[to]] = [next[to], next[index]];
		return next;
	}

	const firstMap = maps[0]?.id ?? "";

	return (
		<>
			<PageHeader
				title="Notícias"
				description="O que a tela inicial do jogo anuncia. A ordem daqui é a ordem de lá; o que está desligado fica guardado e não aparece. Sem nada publicado, o jogo mostra as notícias que vêm nele."
			>
				{editable && (
					<Button variant="primary" disabled={saving || !dirty} onClick={() => void save()}>
						{dirty ? "Publicar" : "Publicado"}
					</Button>
				)}
			</PageHeader>

			<div className="grid max-w-4xl gap-4">
				<Card>
					<CardHeader
						title="Banners"
						description={`Os anúncios grandes, que trocam sozinhos a cada 7 segundos. O jogo mostra os ${SHOWN_BANNERS} primeiros publicados.`}
						action={
							editable && (
								<Button
									disabled={draft.banners.length >= MAX_BANNERS}
									onClick={() => change({ ...draft, banners: [{ category: "", title: "", body: "", image: firstMap, cta: "Jogar agora", go: "jogar", on: true }, ...draft.banners] })}
								>
									<Plus className="size-4" /> Novo banner
								</Button>
							)
						}
					/>
					<CardBody className="flex flex-col gap-3">
						{draft.banners.length === 0 && <Empty title="Nenhum banner">O jogo está mostrando os banners que vêm nele.</Empty>}
						{draft.banners.map((banner, index) => (
							<Item
								key={index}
								item={banner}
								maps={maps}
								editable={editable}
								position={index}
								count={draft.banners.length}
								shown={SHOWN_BANNERS}
								published={draft.banners.slice(0, index).filter((other) => other.on).length}
								onChange={(next) => change({ ...draft, banners: draft.banners.map((other, i) => (i === index ? (next as NewsBanner) : other)) })}
								onMove={(step) => change({ ...draft, banners: move(draft.banners, index, step) })}
								onRemove={() => change({ ...draft, banners: draft.banners.filter((_, i) => i !== index) })}
							>
								<div className="grid gap-3 sm:grid-cols-2">
									<Field label="O botão leva a">
										<Select
											value={banner.go === "" ? "none" : banner.go}
											onChange={(go) => change({ ...draft, banners: draft.banners.map((other, i) => (i === index ? { ...other, go: go === "none" ? "" : go } : other)) })}
											options={TARGETS}
											disabled={!editable}
											label="Destino do botão"
										/>
									</Field>
									<Field label="Texto do botão" hint="Até 32 letras">
										<Input
											value={banner.cta}
											maxLength={32}
											disabled={!editable || banner.go === ""}
											placeholder="Jogar agora"
											onChange={(e) => change({ ...draft, banners: draft.banners.map((other, i) => (i === index ? { ...other, cta: e.target.value } : other)) })}
										/>
									</Field>
								</div>
							</Item>
						))}
					</CardBody>
				</Card>

				<Card>
					<CardHeader
						title="Notícias curtas"
						description={`Os cartões embaixo do banner. O jogo mostra as ${SHOWN_STORIES} primeiras publicadas: ponha a mais nova em cima.`}
						action={
							editable && (
								<Button
									disabled={draft.noticias.length >= MAX_STORIES}
									onClick={() => change({ ...draft, noticias: [{ category: "", title: "", body: "", image: firstMap, date: today(), on: true }, ...draft.noticias] })}
								>
									<Plus className="size-4" /> Nova notícia
								</Button>
							)
						}
					/>
					<CardBody className="flex flex-col gap-3">
						{draft.noticias.length === 0 && <Empty title="Nenhuma notícia">O jogo está mostrando as notícias que vêm nele.</Empty>}
						{draft.noticias.map((story, index) => (
							<Item
								key={index}
								item={story}
								maps={maps}
								editable={editable}
								position={index}
								count={draft.noticias.length}
								shown={SHOWN_STORIES}
								published={draft.noticias.slice(0, index).filter((other) => other.on).length}
								onChange={(next) => change({ ...draft, noticias: draft.noticias.map((other, i) => (i === index ? (next as NewsStory) : other)) })}
								onMove={(step) => change({ ...draft, noticias: move(draft.noticias, index, step) })}
								onRemove={() => change({ ...draft, noticias: draft.noticias.filter((_, i) => i !== index) })}
							>
								<Field label="Data" hint="Como aparece no cartão" className="w-40">
									<Input
										value={story.date}
										maxLength={16}
										disabled={!editable}
										placeholder="27/11"
										onChange={(e) => change({ ...draft, noticias: draft.noticias.map((other, i) => (i === index ? { ...other, date: e.target.value } : other)) })}
									/>
								</Field>
							</Item>
						))}
					</CardBody>
				</Card>
			</div>
		</>
	);
}

type Common = { category: string; title: string; body: string; image: string; on: boolean };

/** Um banner ou uma notícia: a capa ao lado, os campos que os dois têm, e os de cada um por baixo. */
function Item<T extends Common>({
	item,
	maps,
	editable,
	position,
	count,
	shown,
	published,
	onChange,
	onMove,
	onRemove,
	children,
}: {
	item: T;
	maps: GameMap[];
	editable: boolean;
	position: number;
	count: number;
	/** Quantos o jogo mostra desta lista. */
	shown: number;
	/** Quantos publicados há antes deste. */
	published: number;
	onChange: (next: T) => void;
	onMove: (step: number) => void;
	onRemove: () => void;
	children?: React.ReactNode;
}) {
	const map = maps.find((m) => m.id === item.image);
	const state = !item.on ? "off" : published < shown ? "live" : "over";

	return (
		<div className="rounded-lg border border-line bg-bg-2/40 p-3">
			<div className="flex flex-col gap-3 md:flex-row">
				<div className="flex w-full shrink-0 flex-col gap-2 md:w-52">
					<div className="relative aspect-video overflow-hidden rounded-md border border-line bg-bg">
						{map && <img src={`/jogo/${map.image}`} alt={map.name} className={`size-full object-cover ${item.on ? "" : "opacity-40 grayscale"}`} />}
						<div className="absolute left-2 top-2">
							{state === "live" && <Badge tone="good">No jogo</Badge>}
							{state === "over" && <Badge tone="warn">Além do que cabe</Badge>}
							{state === "off" && <Badge>Desligado</Badge>}
						</div>
					</div>
					<Select
						value={item.image}
						onChange={(image) => onChange({ ...item, image })}
						options={maps.map((m) => ({ value: m.id, label: `Capa: ${m.name}` }))}
						placeholder="Escolha a capa"
						disabled={!editable}
						label="Imagem"
					/>
				</div>

				<div className="flex min-w-0 flex-1 flex-col gap-3">
					<div className="grid gap-3 sm:grid-cols-[10rem_1fr]">
						<Field label="Categoria" hint="A etiqueta">
							<Input value={item.category} maxLength={24} disabled={!editable} placeholder="Ex.: Evento" onChange={(e) => onChange({ ...item, category: e.target.value })} />
						</Field>
						<Field label="Título" hint="Até 60 letras">
							<Input value={item.title} maxLength={60} disabled={!editable} placeholder="Ex.: Mapa novo no ar" onChange={(e) => onChange({ ...item, title: e.target.value })} />
						</Field>
					</div>
					<Field label="Descrição" hint="Até 240 letras">
						<Textarea value={item.body} maxLength={240} rows={2} disabled={!editable} placeholder="Uma ou duas frases." onChange={(e) => onChange({ ...item, body: e.target.value })} />
					</Field>
					{children}

					{editable && (
						<div className="flex flex-wrap items-center gap-2">
							<Toggle label="Publicado">
								<Switch checked={item.on} onCheckedChange={(on) => onChange({ ...item, on })} />
							</Toggle>
							<div className="ml-auto flex items-center gap-1">
								<Button size="icon" variant="ghost" disabled={position === 0} onClick={() => onMove(-1)} aria-label="Subir">
									<ArrowUp className="size-4" />
								</Button>
								<Button size="icon" variant="ghost" disabled={position === count - 1} onClick={() => onMove(1)} aria-label="Descer">
									<ArrowDown className="size-4" />
								</Button>
								<Button size="icon" variant="ghost" onClick={onRemove} aria-label="Apagar">
									<Trash2 className="size-4" />
								</Button>
							</div>
						</div>
					)}
				</div>
			</div>
		</div>
	);
}
