import * as SelectPrimitive from "@radix-ui/react-select";
import { Check, ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";

/** Uma lista de escolha: as opções entram como dados. */
export function Select({
	value,
	onChange,
	options,
	placeholder,
	className,
	disabled,
	label,
}: {
	value: string;
	onChange: (value: string) => void;
	options: { value: string; label: string }[];
	placeholder?: string;
	className?: string;
	disabled?: boolean;
	label?: string;
}) {
	return (
		<SelectPrimitive.Root value={value} onValueChange={onChange} disabled={disabled}>
			<SelectPrimitive.Trigger
				aria-label={label}
				className={cn(
					"inline-flex h-9 w-full items-center justify-between gap-2 rounded-md border border-line-strong bg-bg px-3 text-left text-ink disabled:opacity-50 data-[placeholder]:text-ink-3",
					className,
				)}
			>
				<SelectPrimitive.Value placeholder={placeholder} />
				<SelectPrimitive.Icon>
					<ChevronDown className="size-4 text-ink-3" />
				</SelectPrimitive.Icon>
			</SelectPrimitive.Trigger>
			<SelectPrimitive.Portal>
				<SelectPrimitive.Content
					position="popper"
					sideOffset={4}
					className="scroll-thin z-[60] max-h-72 min-w-[var(--radix-select-trigger-width)] overflow-y-auto rounded-md border border-line-strong bg-surface-2 p-1 shadow-xl"
				>
					<SelectPrimitive.Viewport>
						{options.map((option) => (
							<SelectPrimitive.Item
								key={option.value}
								value={option.value}
								className="flex cursor-default items-center justify-between gap-3 rounded-sm px-2 py-1.5 outline-none select-none data-[highlighted]:bg-surface-3"
							>
								<SelectPrimitive.ItemText>{option.label}</SelectPrimitive.ItemText>
								<SelectPrimitive.ItemIndicator>
									<Check className="size-3.5 text-primary" />
								</SelectPrimitive.ItemIndicator>
							</SelectPrimitive.Item>
						))}
					</SelectPrimitive.Viewport>
				</SelectPrimitive.Content>
			</SelectPrimitive.Portal>
		</SelectPrimitive.Root>
	);
}
