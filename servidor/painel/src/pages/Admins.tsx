import { useQuery } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { ActionDialog } from "@/components/ConfirmDialog";
import { ErrorNote, Loading, PageHeader } from "@/components/common";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api, messageOf } from "@/lib/api";
import { relative, roleHelp, roleNames } from "@/lib/format";
import { useRefresh } from "@/lib/queries";
import { useSession } from "@/lib/session";
import type { Admin } from "@/lib/types";

const roles = Object.entries(roleNames).map(([value, label]) => ({ value, label }));

export function Admins() {
	const { me } = useSession();
	const refresh = useRefresh();
	const admins = useQuery({ queryKey: ["admins"], queryFn: () => api.get<{ admins: Admin[] }>("/admins") });
	const [adding, setAdding] = useState(false);
	const [resetting, setResetting] = useState<Admin | null>(null);
	const [name, setName] = useState("");
	const [password, setPassword] = useState("");
	const [role, setRole] = useState("moderador");

	async function update(admin: Admin, patch: { role?: string; disabled?: boolean }, done: string) {
		try {
			await api.put(`/admins/${admin.id}`, patch);
			toast.success(done);
			await refresh("admins");
		} catch (e) {
			toast.error(messageOf(e));
		}
	}

	return (
		<>
			<PageHeader title="Administradores" description="Quem entra no painel e o que cada um pode fazer. As contas do painel são separadas das contas de jogador.">
				<Button
					variant="primary"
					onClick={() => {
						setName("");
						setPassword("");
						setRole("moderador");
						setAdding(true);
					}}
				>
					<Plus />
					Novo administrador
				</Button>
			</PageHeader>

			<Card>
				{admins.isPending ? (
					<Loading />
				) : admins.isError ? (
					<div className="p-4">
						<ErrorNote error={admins.error} retry={() => void admins.refetch()} />
					</div>
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Nome</Th>
								<Th>Papel</Th>
								<Th>Duas etapas</Th>
								<Th>Última entrada</Th>
								<Th>De onde</Th>
								<Th />
							</tr>
						</thead>
						<tbody>
							{admins.data.admins.map((a) => (
								<Tr key={a.id} className={a.disabled ? "text-ink-3" : undefined}>
									<Td>
										<span className="font-medium">{a.name}</span> {a.id === me.id && <Badge tone="primary">você</Badge>} {a.disabled && <Badge>Desligado</Badge>}
									</Td>
									<Td>
										<div className="w-44">
											<Select value={a.role} onChange={(next) => void update(a, { role: next }, `${a.name} agora é ${roleNames[next]}.`)} options={roles} disabled={a.id === me.id} label={`Papel de ${a.name}`} />
										</div>
									</Td>
									<Td>{a.totp ? <Badge tone="good">Ligada</Badge> : <Badge tone="warn">Desligada</Badge>}</Td>
									<Td className="text-ink-2">{relative(a.lastLoginAt)}</Td>
									<Td className="num text-ink-3">{a.lastIp}</Td>
									<Td className="text-right whitespace-nowrap">
										{a.id !== me.id && (
											<>
												<Button
													size="sm"
													variant="ghost"
													onClick={() => {
														setPassword("");
														setResetting(a);
													}}
												>
													Trocar senha
												</Button>
												<Button size="sm" variant={a.disabled ? "secondary" : "danger-quiet"} onClick={() => void update(a, { disabled: !a.disabled }, a.disabled ? `${a.name} religado.` : `${a.name} desligado: a sessão dele caiu.`)}>
													{a.disabled ? "Religar" : "Desligar"}
												</Button>
											</>
										)}
									</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>

			<ActionDialog
				open={adding}
				onOpenChange={setAdding}
				title="Novo administrador"
				description="Passe a senha por um canal seguro e peça para a pessoa trocar e ligar as duas etapas na primeira entrada."
				action="Criar administrador"
				done="Administrador criado."
				disabled={name.trim().length < 3 || password.length < 12}
				onConfirm={() => api.post("/admins", { name: name.trim(), password, role }).then(() => refresh("admins"))}
			>
				<Field label="Nome">
					<Input value={name} onChange={(e) => setName(e.target.value)} autoComplete="off" required />
				</Field>
				<Field label="Senha provisória" hint="Doze caracteres ou mais.">
					<Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" minLength={12} required />
				</Field>
				<Field label="Papel" hint={roleHelp[role]}>
					<Select value={role} onChange={setRole} options={roles} />
				</Field>
			</ActionDialog>

			<ActionDialog
				open={resetting !== null}
				onOpenChange={(o) => !o && setResetting(null)}
				title={`Trocar a senha de ${resetting?.name}`}
				description="A sessão dele cai e as duas etapas são desligadas: ele entra com a senha nova e liga de novo."
				action="Trocar senha"
				done="Senha trocada."
				disabled={password.length < 12}
				onConfirm={() => api.put(`/admins/${resetting?.id}`, { password }).then(() => refresh("admins"))}
			>
				<Field label="Senha nova" hint="Doze caracteres ou mais.">
					<Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" minLength={12} required />
				</Field>
			</ActionDialog>
		</>
	);
}
