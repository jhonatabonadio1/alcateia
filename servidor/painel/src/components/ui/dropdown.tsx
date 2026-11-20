import * as Menu from "@radix-ui/react-dropdown-menu";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

export const Dropdown = Menu.Root;
export const DropdownTrigger = Menu.Trigger;

export function DropdownContent({ className, ...props }: ComponentProps<typeof Menu.Content>) {
	return (
		<Menu.Portal>
			<Menu.Content sideOffset={6} align="end" className={cn("z-[60] min-w-48 rounded-md border border-line-strong bg-surface-2 p-1 shadow-xl", className)} {...props} />
		</Menu.Portal>
	);
}

export function DropdownItem({ className, ...props }: ComponentProps<typeof Menu.Item>) {
	return (
		<Menu.Item
			className={cn("flex cursor-default items-center gap-2 rounded-sm px-2 py-1.5 outline-none select-none data-[highlighted]:bg-surface-3 [&_svg]:size-4", className)}
			{...props}
		/>
	);
}

export const DropdownSeparator = () => <Menu.Separator className="my-1 h-px bg-line" />;
