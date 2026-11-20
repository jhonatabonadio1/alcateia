import * as SwitchPrimitive from "@radix-ui/react-switch";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

export function Switch({ className, ...props }: ComponentProps<typeof SwitchPrimitive.Root>) {
	return (
		<SwitchPrimitive.Root
			className={cn(
				"inline-flex h-5 w-9 shrink-0 items-center rounded-full border border-line-strong bg-surface-3 transition-colors disabled:opacity-50 data-[state=checked]:border-primary data-[state=checked]:bg-primary",
				className,
			)}
			{...props}
		>
			<SwitchPrimitive.Thumb className="block size-3.5 translate-x-0.5 rounded-full bg-ink transition-transform data-[state=checked]:translate-x-[18px] data-[state=checked]:bg-primary-ink" />
		</SwitchPrimitive.Root>
	);
}
