import type { ReactNode } from "react";
import { n } from "@/lib/format";

/** Uma lista em ordem, com a barra de cada linha e o valor na ponta. */
export function BarList({ rows, color = "var(--chart-1)", empty }: { rows: { key: string; label: ReactNode; value: number; color?: string }[]; color?: string; empty: string }) {
	if (rows.length === 0) return <p className="py-6 text-center text-[13px] text-ink-3">{empty}</p>;
	const max = Math.max(...rows.map((r) => r.value), 1);
	return (
		<ul className="flex flex-col gap-2.5">
			{rows.map((row) => (
				<li key={row.key}>
					<div className="mb-1 flex items-baseline justify-between gap-3 text-[13px]">
						<span className="min-w-0 truncate text-ink-2">{row.label}</span>
						<span className="num text-ink">{n(row.value)}</span>
					</div>
					<div className="h-1.5 rounded-full bg-surface-3">
						<div className="h-full rounded-full" style={{ width: `${Math.max(2, (row.value / max) * 100)}%`, background: row.color ?? color }} />
					</div>
				</li>
			))}
		</ul>
	);
}
