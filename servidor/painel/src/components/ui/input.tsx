import type { ComponentProps, TextareaHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

const field =
	"w-full rounded-md border border-line-strong bg-bg px-3 text-ink placeholder:text-ink-3 disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-ring";

export function Input({ className, ...props }: ComponentProps<"input">) {
	return <input className={cn(field, "h-9", className)} {...props} />;
}

export function Textarea({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
	return <textarea className={cn(field, "min-h-20 py-2", className)} {...props} />;
}
