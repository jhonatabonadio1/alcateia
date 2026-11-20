import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes } from "react";
import { cn } from "@/lib/utils";

const badgeVariants = cva("inline-flex items-center gap-1 rounded-sm px-1.5 py-0.5 text-xs font-medium whitespace-nowrap [&_svg]:size-3", {
	variants: {
		tone: {
			neutral: "bg-surface-3 text-ink-2",
			primary: "bg-primary-soft text-primary",
			good: "bg-good-soft text-good",
			warn: "bg-warn-soft text-warn",
			accent: "bg-accent-soft text-accent",
			danger: "bg-danger-soft text-danger",
		},
	},
	defaultVariants: { tone: "neutral" },
});

export function Badge({ className, tone, ...props }: HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badgeVariants>) {
	return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}
