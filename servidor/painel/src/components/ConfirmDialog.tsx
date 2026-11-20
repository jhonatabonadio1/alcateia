import { type ReactNode, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter } from "@/components/ui/dialog";
import { messageOf } from "@/lib/api";

/**
 * A janela de uma ação: os campos, o botão que diz o que vai acontecer e o
 * erro do servidor, se houver, escrito ali mesmo. Fecha sozinha quando dá certo.
 */
export function ActionDialog({
	open,
	onOpenChange,
	title,
	description,
	action,
	danger,
	disabled,
	onConfirm,
	done,
	children,
}: {
	open: boolean;
	onOpenChange: (open: boolean) => void;
	title: string;
	description?: ReactNode;
	/** O texto do botão: o verbo do que acontece. */
	action: string;
	danger?: boolean;
	disabled?: boolean;
	onConfirm: () => Promise<unknown>;
	/** O aviso de que deu certo. */
	done?: string;
	children?: ReactNode;
}) {
	const [busy, setBusy] = useState(false);
	const [error, setError] = useState("");

	async function confirm() {
		setBusy(true);
		setError("");
		try {
			await onConfirm();
			if (done) toast.success(done);
			onOpenChange(false);
		} catch (e) {
			setError(messageOf(e));
		} finally {
			setBusy(false);
		}
	}

	return (
		<Dialog
			open={open}
			onOpenChange={(next) => {
				if (!next) setError("");
				onOpenChange(next);
			}}
		>
			<DialogContent title={title} description={description}>
				<form
					onSubmit={(event) => {
						event.preventDefault();
						void confirm();
					}}
				>
					{children && <div className="flex flex-col gap-3">{children}</div>}
					{error && <p className="mt-3 rounded-md bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>}
					<DialogFooter>
						<Button variant="ghost" onClick={() => onOpenChange(false)}>
							Cancelar
						</Button>
						<Button type="submit" variant={danger ? "danger" : "primary"} disabled={busy || disabled}>
							{busy ? "Enviando…" : action}
						</Button>
					</DialogFooter>
				</form>
			</DialogContent>
		</Dialog>
	);
}
