import QRCode from "qrcode";
import { type FormEvent, useState } from "react";
import { toast } from "sonner";
import { PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { api, messageOf } from "@/lib/api";
import { roleHelp, roleNames } from "@/lib/format";
import { useSession } from "@/lib/session";

/** A própria conta: a senha e a verificação em duas etapas. */
export function Account() {
	const { me, refresh, signOut } = useSession();
	const [current, setCurrent] = useState("");
	const [next, setNext] = useState("");
	const [enrolling, setEnrolling] = useState<{ secret: string; qr: string } | null>(null);
	const [code, setCode] = useState("");
	const [password, setPassword] = useState("");
	const [busy, setBusy] = useState(false);

	async function run(work: () => Promise<unknown>, done: string) {
		setBusy(true);
		try {
			await work();
			toast.success(done);
		} catch (e) {
			toast.error(messageOf(e));
		} finally {
			setBusy(false);
		}
	}

	const changePassword = (event: FormEvent) => {
		event.preventDefault();
		void run(async () => {
			await api.post("/me/password", { current, next });
			await signOut();
		}, "Senha trocada. Entre de novo com a senha nova.");
	};

	const start = () =>
		run(async () => {
			const { secret, uri } = await api.post<{ secret: string; uri: string }>("/me/totp/start");
			setEnrolling({ secret, qr: await QRCode.toDataURL(uri, { margin: 1, width: 220 }) });
			setCode("");
		}, "Aponte o aplicativo autenticador para o código.");

	const confirm = (event: FormEvent) => {
		event.preventDefault();
		void run(async () => {
			await api.post("/me/totp/confirm", { code });
			setEnrolling(null);
			await refresh();
		}, "Verificação em duas etapas ligada.");
	};

	const disable = (event: FormEvent) => {
		event.preventDefault();
		void run(async () => {
			await api.post("/me/totp/disable", { password });
			setPassword("");
			await refresh();
		}, "Verificação em duas etapas desligada.");
	};

	return (
		<>
			<PageHeader title="Minha conta" description={`${me.name}, ${roleNames[me.role] ?? me.role}. ${roleHelp[me.role] ?? ""}`} />

			{me.mustEnableTotp && (
				<p className="mb-4 max-w-3xl rounded-lg border border-warn/50 bg-warn-soft px-4 py-3">
					Este servidor exige a verificação em duas etapas. Ligue a sua abaixo para abrir o resto do painel.
				</p>
			)}

			<div className="grid max-w-3xl gap-4">
				<Card>
					<CardHeader
						title="Verificação em duas etapas"
						description="Além da senha, o painel pede um código de seis dígitos que muda a cada 30 segundos, de um aplicativo como Google Authenticator, Authy ou 1Password. Senha vazada sozinha não entra."
						action={me.totp ? <Badge tone="good">Ligada</Badge> : <Badge tone="warn">Desligada</Badge>}
					/>
					<CardBody>
						{me.totp ? (
							<form onSubmit={disable} className="flex flex-wrap items-end gap-3">
								<Field label="Para desligar, confirme a sua senha" className="w-72">
									<Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
								</Field>
								<Button type="submit" variant="danger-quiet" disabled={busy}>
									Desligar as duas etapas
								</Button>
							</form>
						) : enrolling ? (
							<form onSubmit={confirm} className="flex flex-wrap gap-5">
								<img src={enrolling.qr} alt="Código QR para o aplicativo autenticador" width={220} height={220} className="rounded-md bg-white p-1" />
								<div className="flex max-w-xs flex-col gap-3">
									<p className="text-[13px] text-ink-2">
										Abra o aplicativo autenticador, escolha adicionar conta e aponte para o código. Sem câmera? Digite esta chave:
									</p>
									<code className="num rounded-md bg-surface-2 px-2 py-1.5 text-[13px] break-all select-all">{enrolling.secret}</code>
									<Field label="O código que o aplicativo mostra">
										<Input value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))} inputMode="numeric" className="num text-lg tracking-[0.3em]" required autoFocus />
									</Field>
									<div className="flex gap-2">
										<Button type="submit" variant="primary" disabled={busy || code.length !== 6}>
											Confirmar e ligar
										</Button>
										<Button variant="ghost" onClick={() => setEnrolling(null)}>
											Cancelar
										</Button>
									</div>
								</div>
							</form>
						) : (
							<Button variant="primary" onClick={() => void start()} disabled={busy}>
								Ligar as duas etapas
							</Button>
						)}
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Senha" description="Doze caracteres ou mais. Ao trocar, todas as suas sessões caem e você entra de novo." />
					<CardBody>
						<form onSubmit={changePassword} className="flex flex-wrap items-end gap-3">
							<Field label="Senha atual" className="w-60">
								<Input type="password" value={current} onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" required />
							</Field>
							<Field label="Senha nova" className="w-60">
								<Input type="password" value={next} onChange={(e) => setNext(e.target.value)} autoComplete="new-password" minLength={12} required />
							</Field>
							<Button type="submit" disabled={busy || next.length < 12}>
								Trocar senha
							</Button>
						</form>
					</CardBody>
				</Card>
			</div>
		</>
	);
}
