import { useEffect, useRef, useState } from "react";
import { n, short } from "@/lib/format";

export interface Column {
	label: string;
	/** Os segmentos, de baixo para cima. Uma série só é um segmento. */
	values: number[];
	/** O título do balão (o rótulo por extenso). */
	title?: string;
}

export interface Series {
	name: string;
	color: string;
}

function useWidth() {
	const ref = useRef<HTMLDivElement>(null);
	const [width, setWidth] = useState(0);
	useEffect(() => {
		if (!ref.current) return;
		const observer = new ResizeObserver(([entry]) => setWidth(Math.floor(entry.contentRect.width)));
		observer.observe(ref.current);
		return () => observer.disconnect();
	}, []);
	return [ref, width] as const;
}

/** O teto do eixo: um número redondo acima do maior valor, com 4 divisões. */
function ceiling(max: number) {
	if (max <= 4) return 4;
	const step = Math.pow(10, Math.floor(Math.log10(max / 4)));
	const nice = [1, 2, 2.5, 5, 10].map((m) => m * step).find((s) => s * 4 >= max) ?? step * 10;
	return nice * 4;
}

/**
 * Colunas ao longo do tempo: uma série, ou segmentos empilhados. Colunas
 * finas com a ponta arredondada, grade discreta, e o valor de cada coluna
 * no balão ao passar o mouse (ou ao focar com o teclado).
 */
export function ColumnChart({ columns, series, height = 170, labelEvery }: { columns: Column[]; series: Series[]; height?: number; labelEvery?: number }) {
	const [ref, width] = useWidth();
	const [active, setActive] = useState<number | null>(null);

	const left = 34, bottom = 20, top = 8;
	const plotW = Math.max(0, width - left - 4);
	const plotH = height - top - bottom;
	const totals = columns.map((c) => c.values.reduce((a, b) => a + b, 0));
	const max = ceiling(Math.max(0, ...totals));
	const band = columns.length > 0 ? plotW / columns.length : 0;
	const bar = Math.max(2, Math.min(24, band - 4));
	const every = labelEvery ?? Math.max(1, Math.ceil(columns.length / Math.max(1, Math.floor(plotW / 46))));
	const y = (value: number) => top + plotH - (value / max) * plotH;
	const empty = totals.every((t) => t === 0);

	return (
		<div ref={ref} className="relative" style={{ height }}>
			{width > 0 && (
				<svg width={width} height={height} role="img" aria-label={series.map((s) => s.name).join(", ")}>
					{[0, 1, 2, 3, 4].map((i) => {
						const value = (max / 4) * i;
						return (
							<g key={i}>
								<line x1={left} x2={width} y1={y(value)} y2={y(value)} stroke="var(--chart-grid)" strokeWidth={1} />
								<text x={left - 6} y={y(value) + 4} textAnchor="end" fontSize={11} fill="var(--ink-3)">
									{short(value)}
								</text>
							</g>
						);
					})}

					{columns.map((column, i) => {
						const x = left + band * i + (band - bar) / 2;
						let base = 0;
						const last = column.values.reduce((at, v, k) => (v > 0 ? k : at), -1);
						return (
							<g key={i} opacity={active === null || active === i ? 1 : 0.45}>
								{column.values.map((value, k) => {
									if (value <= 0) return null;
									const y1 = y(base + value), y0 = y(base);
									base += value;
									// Um vão de 2 px entre segmentos; só o de cima tem a ponta arredondada.
									const h = Math.max(1, y0 - y1 - (k > 0 ? 2 : 0));
									const r = k === last ? Math.min(4, bar / 2, h) : 0;
									return (
										<path
											key={k}
											d={`M${x},${y1 + h} V${y1 + r} Q${x},${y1} ${x + r},${y1} H${x + bar - r} Q${x + bar},${y1} ${x + bar},${y1 + r} V${y1 + h} Z`}
											fill={series[k]?.color}
										/>
									);
								})}
								{i % every === 0 && (
									<text x={left + band * i + band / 2} y={height - 5} textAnchor="middle" fontSize={11} fill="var(--ink-3)">
										{column.label}
									</text>
								)}
								{/* O alvo do mouse é a faixa inteira, não só a coluna. */}
								<rect
									x={left + band * i}
									y={top}
									width={band}
									height={plotH}
									fill="transparent"
									tabIndex={0}
									aria-label={`${column.title ?? column.label}: ${series.map((s, k) => `${s.name} ${n(column.values[k] ?? 0)}`).join(", ")}`}
									onMouseEnter={() => setActive(i)}
									onMouseLeave={() => setActive(null)}
									onFocus={() => setActive(i)}
									onBlur={() => setActive(null)}
									style={{ outline: "none" }}
								/>
							</g>
						);
					})}
				</svg>
			)}

			{empty && width > 0 && <p className="absolute inset-x-0 top-1/2 -translate-y-1/2 text-center text-[13px] text-ink-3">Nada no período.</p>}

			{active !== null && columns[active] && (
				<div
					className="pointer-events-none absolute top-0 z-10 min-w-32 rounded-md border border-line-strong bg-surface-3 px-2.5 py-1.5 text-xs shadow-lg"
					style={left + band * active > width / 2 ? { right: width - (left + band * active) + 4 } : { left: left + band * (active + 1) + 4 }}
				>
					<p className="mb-1 font-medium text-ink">{columns[active].title ?? columns[active].label}</p>
					{series.map((s, k) => (
						<p key={s.name} className="num flex items-center justify-between gap-4 text-ink-2">
							<span className="flex items-center gap-1.5">
								<span className="size-2 rounded-[2px]" style={{ background: s.color }} />
								{s.name}
							</span>
							<span className="text-ink">{n(columns[active].values[k] ?? 0)}</span>
						</p>
					))}
				</div>
			)}
		</div>
	);
}

/** A legenda: sempre presente quando há mais de uma série. */
export function Legend({ series }: { series: Series[] }) {
	return (
		<ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-2">
			{series.map((s) => (
				<li key={s.name} className="flex items-center gap-1.5">
					<span className="size-2.5 rounded-[2px]" style={{ background: s.color }} />
					{s.name}
				</li>
			))}
		</ul>
	);
}
