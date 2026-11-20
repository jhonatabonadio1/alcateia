import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
	return <div className={cn("rounded-lg border border-line bg-surface", className)} {...props} />;
}

/** O cabeçalho de um bloco: o título, o que ele explica e, à direita, a ação. */
export function CardHeader({ title, description, action, className }: { title: string; description?: ReactNode; action?: ReactNode; className?: string }) {
	return (
		<div className={cn("flex items-start justify-between gap-4 px-4 pt-4 pb-3", className)}>
			<div className="min-w-0">
				<h2 className="display text-[15px]">{title}</h2>
				{description && <p className="mt-0.5 text-[13px] text-ink-3">{description}</p>}
			</div>
			{action}
		</div>
	);
}

export function CardBody({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
	return <div className={cn("px-4 pb-4", className)} {...props} />;
}
