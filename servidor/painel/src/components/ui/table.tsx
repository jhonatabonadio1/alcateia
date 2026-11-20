import type { HTMLAttributes, TdHTMLAttributes, ThHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

export function Table({ className, ...props }: HTMLAttributes<HTMLTableElement>) {
	return (
		<div className="scroll-thin w-full overflow-x-auto">
			<table className={cn("w-full border-collapse text-left", className)} {...props} />
		</div>
	);
}

export function Th({ className, ...props }: ThHTMLAttributes<HTMLTableCellElement>) {
	return <th className={cn("border-b border-line px-3 py-2 text-[13px] font-medium whitespace-nowrap text-ink-3", className)} {...props} />;
}

export function Td({ className, ...props }: TdHTMLAttributes<HTMLTableCellElement>) {
	return <td className={cn("border-b border-line/60 px-3 py-2 align-middle", className)} {...props} />;
}

export function Tr({ className, ...props }: HTMLAttributes<HTMLTableRowElement>) {
	return <tr className={cn("hover:bg-surface-2/60", className)} {...props} />;
}
