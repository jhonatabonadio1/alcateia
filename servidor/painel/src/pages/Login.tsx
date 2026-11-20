import { type FormEvent, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { api, ApiError, messageOf } from "@/lib/api";

/** A entrada do painel: nome e senha e, se a conta tem, o código das duas etapas. */
export function Login({ onDone }: { onDone: () => void }) {
	const [name, setName] = useState("");
	const [password, setPassword] = useState("");
	const [code, setCode] = useState("");
	const [needsCode, setNeedsCode] = useState(false);
	const [error, setError] = useState("");
	const [busy, setBusy] = useState(false);

	async function submit(event: FormEvent) {
		event.preventDefault();
		setBusy(true);
		setError("");
		try {
			await api.post("/login", { name, password, code: needsCode ? code : undefined });
			onDone();
		} catch (e) {
			if (e instanceof ApiError && e.code === "codigo_necessario") setNeedsCode(true);
			else setError(messageOf(e));
		} finally {
			setBusy(false);
		}
	}

	return (
		<div className="grid min-h-dvh lg:grid-cols-[1fr_minmax(380px,460px)]">
			{/* A noite do jogo: a lua e as três garras, desenhadas aqui mesmo. */}
			<div className="relative hidden overflow-hidden bg-[#0b101a] lg:block" aria-hidden>
				<svg viewBox="0 0 800 800" preserveAspectRatio="xMidYMid slice" className="absolute inset-0 size-full">
					<defs>
						<radialGradient id="lua" cx="66%" cy="26%" r="46%">
							<stop offset="0" stopColor="#2a3d66" />
							<stop offset="1" stopColor="#0b101a" stopOpacity="0" />
						</radialGradient>
					</defs>
					<rect width="800" height="800" fill="url(#lua)" />
					<circle cx="540" cy="210" r="96" fill="#e9e6dc" />
					<circle cx="500" cy="186" r="96" fill="#131b2b" />
					<g fill="none" strokeWidth="24" strokeLinecap="round">
						<path d="M210 420c40 110 50 230 18 350" stroke="#4f8df7" />
						<path d="M340 390c44 130 56 270 18 400" stroke="#e9e6dc" />
						<path d="M470 420c40 110 50 230 18 350" stroke="#f08c3e" />
					</g>
				</svg>
				<p className="display absolute bottom-10 left-10 text-4xl text-[#e9e6dc]">WolfLeague</p>
			</div>

			<div className="flex items-center justify-center bg-bg p-6">
				<form onSubmit={submit} className="w-full max-w-sm">
					<h1 className="display text-2xl">Entrar no painel</h1>
					<p className="mt-1 mb-6 text-ink-2">Acesso só para a equipe. Toda entrada fica registrada.</p>

					<div className="flex flex-col gap-4">
						<Field label="Nome">
							<Input value={name} onChange={(e) => setName(e.target.value)} autoComplete="username" autoFocus required disabled={needsCode} />
						</Field>
						<Field label="Senha">
							<Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required disabled={needsCode} />
						</Field>
						{needsCode && (
							<Field label="Código do aplicativo autenticador" hint="Os seis dígitos que o aplicativo mostra agora.">
								<Input
									value={code}
									onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
									inputMode="numeric"
									autoComplete="one-time-code"
									autoFocus
									required
									className="num text-lg tracking-[0.3em]"
								/>
							</Field>
						)}
					</div>

					{error && <p className="mt-4 rounded-md bg-danger-soft px-3 py-2 text-[13px] text-danger">{error}</p>}

					<Button type="submit" variant="primary" className="mt-6 w-full" disabled={busy}>
						{busy ? "Entrando…" : needsCode ? "Confirmar código" : "Entrar"}
					</Button>
					{needsCode && (
						<Button
							variant="ghost"
							className="mt-2 w-full"
							onClick={() => {
								setNeedsCode(false);
								setCode("");
							}}
						>
							Voltar
						</Button>
					)}
				</form>
			</div>
		</div>
	);
}
