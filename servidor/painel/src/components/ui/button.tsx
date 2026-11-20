import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

const buttonVariants = cva(
	"inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-md font-medium transition-colors disabled:pointer-events-none disabled:opacity-45 [&_svg]:size-4 [&_svg]:shrink-0",
	{
		variants: {
			variant: {
				primary: "bg-primary text-primary-ink hover:brightness-110",
				secondary: "border border-line-strong bg-surface-2 text-ink hover:bg-surface-3",
				ghost: "text-ink-2 hover:bg-surface-2 hover:text-ink",
				danger: "bg-danger text-white hover:brightness-110",
				"danger-quiet": "border border-line-strong text-danger hover:bg-danger-soft",
			},
			size: { sm: "h-7 px-2.5 text-[13px]", md: "h-9 px-3.5", icon: "size-8" },
		},
		defaultVariants: { variant: "secondary", size: "md" },
	},
);

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
	asChild?: boolean;
}

export function Button({ className, variant, size, asChild, type, ...props }: ButtonProps) {
	const Comp = asChild ? Slot : "button";
	return <Comp type={asChild ? undefined : (type ?? "button")} className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}
