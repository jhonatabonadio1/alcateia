import { AlertTriangle, Loader2 } from "lucide-react";
import type { ReactNode } from "react";
import { messageOf } from "@/lib/api";
import { rarityNames } from "@/lib/format";
import { cn } from "@/lib/utils";

/** O alto de cada tela: o nome dela, uma linha que diz para que serve e as ações. */
export function PageHeader({ title, description, children }: { title: string; description?: ReactNode; children?: ReactNode }) {
	return (
		<header className="mb-5 flex flex-wrap items-end justify-between gap-3">
			<div className="min-w-0">
				<h1 className="display text-2xl">{title}</h1>
				{description && <p className="mt-1 max-w-[72ch] text-ink-2">{description}</p>}
			</div>
			{children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
		</header>
	);
}

export function Loading({ label = "Carregando…" }: { label?: string }) {
	return (
		<div className="flex items-center justify-center gap-2 py-16 text-ink-3">
			<Loader2 className="size-4 animate-spin" />
			{label}
		</div>
	);
}

export function ErrorNote({ error, retry }: { error: unknown; retry?: () => void }) {
	return (
		<div className="flex items-start gap-3 rounded-lg border border-danger/40 bg-danger-soft px-4 py-3">
			<AlertTriangle className="mt-0.5 size-4 shrink-0 text-danger" />
			<div className="min-w-0 flex-1">
				<p className="font-medium">Não deu para carregar</p>
				<p className="text-[13px] text-ink-2">{messageOf(error)}</p>
			</div>
			{retry && (
				<button type="button" className="text-[13px] font-medium text-primary hover:underline" onClick={retry}>
					Tentar de novo
				</button>
			)}
		</div>
	);
}

export function Empty({ title, children }: { title: string; children?: ReactNode }) {
	return (
		<div className="px-4 py-12 text-center">
			<p className="font-medium">{title}</p>
			{children && <p className="mx-auto mt-1 max-w-[52ch] text-[13px] text-ink-3">{children}</p>}
		</div>
	);
}

/** Um número do servidor com o nome do que ele conta. */
export function Stat({ label, value, note, tone }: { label: string; value: ReactNode; note?: ReactNode; tone?: "good" | "warn" | "danger" }) {
	return (
		<div className="min-w-0 px-4 py-3">
			<p className="truncate text-[13px] text-ink-3">{label}</p>
			<p className={cn("display num mt-0.5 text-[26px] leading-tight", tone === "good" && "text-good", tone === "warn" && "text-warn", tone === "danger" && "text-danger")}>
				{value}
			</p>
			{note && <p className="mt-0.5 truncate text-xs text-ink-3">{note}</p>}
		</div>
	);
}

/** A raridade com cor e nome — nunca só a cor. */
export function Rarity({ value, className }: { value: number; className?: string }) {
	return (
		<span className={cn("inline-flex items-center gap-1.5 text-xs text-ink-2", className)}>
			<span className="size-2 rotate-45" style={{ background: `var(--rarity-${value})` }} />
			{rarityNames[value] ?? value}
		</span>
	);
}

/**
 * A arte de um item sobre a placa da raridade dele: o chanfro colorido no
 * canto é a raridade. Sem imagem (item sem arte ainda), fica o nome.
 */
export function ItemArt({ image, name, rarity, dim, className }: { image: string; name: string; rarity: number; dim?: boolean; className?: string }) {
	return (
		<div className={cn("relative isolate overflow-hidden bg-surface-2", className)}>
			<div
				className="absolute inset-0 -z-10 opacity-25"
				style={{ background: `var(--rarity-${rarity})`, clipPath: "polygon(0 62%, 100% 18%, 100% 100%, 0 100%)" }}
			/>
			<div className="absolute inset-x-0 bottom-0 -z-10 h-[3px]" style={{ background: `var(--rarity-${rarity})` }} />
			{image ? (
				<img
					src={`/jogo/${image}`}
					alt=""
					loading="lazy"
					className={cn("size-full object-contain p-2", dim && "opacity-40 grayscale")}
					onError={(event) => {
						event.currentTarget.style.display = "none";
						event.currentTarget.nextElementSibling?.classList.remove("hidden");
					}}
				/>
			) : null}
			<span className={cn("absolute inset-0 grid place-items-center px-3 text-center text-[13px] text-ink-3", image && "hidden")}>{name}</span>
		</div>
	);
}

/** Um interruptor com o que ele liga escrito ao lado. */
export function Toggle({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
	return (
		<div className="flex items-center justify-between gap-4 py-2">
			<div className="min-w-0">
				<p className="font-medium">{label}</p>
				{hint && <p className="text-[13px] text-ink-3">{hint}</p>}
			</div>
			{children}
		</div>
	);
}
