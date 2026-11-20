import * as LabelPrimitive from "@radix-ui/react-label";
import type { ComponentProps, ReactNode } from "react";
import { cn } from "@/lib/utils";

export function Label({ className, ...props }: ComponentProps<typeof LabelPrimitive.Root>) {
	return <LabelPrimitive.Root className={cn("text-[13px] font-medium text-ink-2", className)} {...props} />;
}

/** Um campo de formulário: o rótulo, o controle e, se houver, a ajuda. */
export function Field({ label, hint, children, className }: { label: string; hint?: string; children: ReactNode; className?: string }) {
	return (
		<label className={cn("flex flex-col gap-1.5", className)}>
			<span className="text-[13px] font-medium text-ink-2">{label}</span>
			{children}
			{hint && <span className="text-xs text-ink-3">{hint}</span>}
		</label>
	);
}
