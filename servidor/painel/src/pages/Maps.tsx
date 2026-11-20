import { Pencil } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { ActionDialog } from "@/components/ConfirmDialog";
import { ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input, Textarea } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { api, messageOf } from "@/lib/api";
import { modeNames } from "@/lib/format";
import { useContent, useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { GameMap } from "@/lib/types";
import { cn } from "@/lib/utils";

const sizes = ["Pequeno", "Médio", "Grande"].map((s) => ({ value: s, label: s }));

function MapPhoto({ map }: { map: GameMap }) {
	return (
		<div className="relative aspect-video overflow-hidden bg-surface-2">
			{map.image && (
				<img
					src={`/jogo/${map.image}`}
					alt=""
					loading="lazy"
					className={cn("size-full", map.image.includes("_foto") ? "object-cover" : "object-contain p-3", !map.enabled && "opacity-40 grayscale")}
					onError={(e) => {
						e.currentTarget.style.display = "none";
					}}
				/>
			)}
			{!map.image && <span className="absolute inset-0 grid place-items-center text-[13px] text-ink-3">Sem foto ainda</span>}
			<div className="absolute top-2 left-2">{map.enabled ? <Badge tone="good">Em rotação</Badge> : <Badge tone="neutral">Desligado</Badge>}</div>
		</div>
	);
}

const list = (modes: string[]) => modes.map((m) => modeNames[m] ?? m).join(" e ");

/**
 * O tamanho e os modos do mapa. Só anuncia modo que o jogo já tem: mapa
 * feito para um modo que ainda não existe diz isso, e em que modo se joga
 * nele enquanto isso.
 */
function modesOf(map: GameMap, released: string[]) {
	const playable = map.modes.filter((m) => released.includes(m));
	if (playable.length > 0) return `${map.size}, ${list(playable)}`;
	const waiting = map.modes.filter((m) => !released.includes(m));
	if (waiting.length === 0) return map.size;
	return `${map.size}. Feito para ${list(waiting)}, que ainda não existe no jogo: por enquanto joga em ${list(released)}.`;
}

export function Maps() {
	const { can } = useSession();
	const refresh = useRefresh();
	const content = useContent();
	const [editing, setEditing] = useState<GameMap | null>(null);
	const [draft, setDraft] = useState<GameMap | null>(null);
	useEffect(() => setDraft(editing), [editing]);

	if (content.isPending) return <Loading />;
	if (content.isError) return <ErrorNote error={content.error} retry={() => void content.refetch()} />;
	const maps = content.data.maps;
	const on = maps.filter((m) => m.enabled).length;

	async function toggle(map: GameMap, enabled: boolean) {
		try {
			await api.put(`/maps/${map.id}`, { enabled });
			toast.success(enabled ? `${map.name} voltou para a rotação.` : `${map.name} saiu da rotação.`);
			await refresh("content");
		} catch (e) {
			toast.error(messageOf(e));
		}
	}

	return (
		<>
			<PageHeader
				title="Mapas"
				description={`${on} de ${maps.length} mapas em rotação. Mapa desligado não abre sala nova — as partidas que já estão nele terminam normalmente. Pelo menos um mapa precisa ficar ligado.`}
			/>

			<div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
				{maps.map((map) => (
					<Card key={map.id} className="flex flex-col overflow-hidden">
						<MapPhoto map={map} />
						<div className="flex flex-1 flex-col gap-2 p-4">
							<div className="flex items-start justify-between gap-3">
								<div>
									<h2 className="display text-lg leading-tight">{map.name}</h2>
									<p className="text-[13px] text-ink-3">{modesOf(map, content.data.releasedModes)}</p>
								</div>
								{can("content") && <Switch checked={map.enabled} onCheckedChange={(enabled) => void toggle(map, enabled)} aria-label={map.enabled ? `Desligar ${map.name}` : `Ligar ${map.name}`} />}
							</div>
							<p className="line-clamp-3 text-[13px] text-ink-2">{map.description}</p>
							{can("content") && (
								<div className="mt-auto pt-1">
									<Button size="sm" variant="ghost" className="-ml-2" onClick={() => setEditing(map)}>
										<Pencil />
										Editar nome e descrição
									</Button>
								</div>
							)}
						</div>
					</Card>
				))}
			</div>

			{draft && (
				<ActionDialog
					open={editing !== null}
					onOpenChange={(o) => !o && setEditing(null)}
					title={`Editar ${editing?.name}`}
					action="Salvar"
					done="Mapa atualizado."
					onConfirm={() => api.put(`/maps/${draft.id}`, { name: draft.name, size: draft.size, description: draft.description }).then(() => refresh("content"))}
				>
					<Field label="Nome">
						<Input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} required />
					</Field>
					<Field label="Tamanho">
						<Select value={draft.size} onChange={(size) => setDraft({ ...draft, size })} options={sizes} />
					</Field>
					<Field label="Descrição">
						<Textarea value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} rows={5} />
					</Field>
				</ActionDialog>
			)}
		</>
	);
}
