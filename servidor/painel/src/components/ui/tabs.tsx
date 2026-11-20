import * as TabsPrimitive from "@radix-ui/react-tabs";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

export const Tabs = TabsPrimitive.Root;
export const TabsContent = TabsPrimitive.Content;

export function TabsList({ className, ...props }: ComponentProps<typeof TabsPrimitive.List>) {
	return <TabsPrimitive.List className={cn("flex flex-wrap gap-1 border-b border-line", className)} {...props} />;
}

export function TabsTrigger({ className, ...props }: ComponentProps<typeof TabsPrimitive.Trigger>) {
	return (
		<TabsPrimitive.Trigger
			className={cn(
				"-mb-px border-b-2 border-transparent px-3 py-2 text-ink-3 hover:text-ink data-[state=active]:border-primary data-[state=active]:text-ink",
				className,
			)}
			{...props}
		/>
	);
}
