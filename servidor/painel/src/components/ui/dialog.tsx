import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import type { ComponentProps, ReactNode } from "react";
import { cn } from "@/lib/utils";

export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;
export const DialogClose = DialogPrimitive.Close;

export function DialogContent({
	className,
	children,
	title,
	description,
	...props
}: ComponentProps<typeof DialogPrimitive.Content> & { title: string; description?: ReactNode }) {
	return (
		<DialogPrimitive.Portal>
			<DialogPrimitive.Overlay className="fixed inset-0 z-40 bg-black/60" />
			<DialogPrimitive.Content
				className={cn(
					"scroll-thin fixed top-1/2 left-1/2 z-50 max-h-[90dvh] w-[min(92vw,520px)] -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-line-strong bg-surface p-5 shadow-2xl",
					className,
				)}
				{...props}
			>
				<div className="mb-4 flex items-start justify-between gap-4">
					<div>
						<DialogPrimitive.Title className="display text-lg">{title}</DialogPrimitive.Title>
						{description ? (
							<DialogPrimitive.Description className="mt-1 text-[13px] text-ink-2">{description}</DialogPrimitive.Description>
						) : (
							<DialogPrimitive.Description className="sr-only">{title}</DialogPrimitive.Description>
						)}
					</div>
					<DialogPrimitive.Close className="rounded-md p-1 text-ink-3 hover:bg-surface-2 hover:text-ink" aria-label="Fechar">
						<X className="size-4" />
					</DialogPrimitive.Close>
				</div>
				{children}
			</DialogPrimitive.Content>
		</DialogPrimitive.Portal>
	);
}

export function DialogFooter({ children }: { children: ReactNode }) {
	return <div className="mt-5 flex justify-end gap-2">{children}</div>;
}
