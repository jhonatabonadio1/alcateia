import { useEffect, useState } from "react";
import { toast } from "sonner";
import { ErrorNote, Loading, PageHeader, Toggle } from "@/components/common";
import { Button } from "@/components/ui/button";
import { Card, CardBody, CardHeader } from "@/components/ui/card";
import { Input, Textarea } from "@/components/ui/input";
import { Field } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { api, messageOf } from "@/lib/api";
import { toLocalInput } from "@/lib/format";
import { useContent, useRefresh } from "@/lib/queries";
import type { Settings as ServerSettings } from "@/lib/types";

export function Settings() {
	const content = useContent();
	const refresh = useRefresh();
	const [s, setS] = useState<ServerSettings | null>(null);
	const [saving, setSaving] = useState("");

	useEffect(() => {
		if (content.data && !s) setS(structuredClone(content.data.settings));
	}, [content.data, s]);

	if (content.isPending || !s) return <Loading />;
	if (content.isError) return <ErrorNote error={content.error} retry={() => void content.refetch()} />;

	async function save<K extends keyof ServerSettings>(key: K, value: ServerSettings[K], done: string) {
		setSaving(key);
		try {
			await api.put(`/settings/${key}`, value);
			toast.success(done);
			await refresh("content");
		} catch (e) {
			toast.error(messageOf(e));
		} finally {
			setSaving("");
		}
	}

	const bonusOn = s.bonus.xp > 1 || s.bonus.presas > 1;

	return (
		<>
			<PageHeader title="Configurações" description="O que vale para o servidor inteiro. Cada bloco se salva sozinho, e toda mudança vai para a auditoria." />

			<div className="grid max-w-3xl gap-4">
				<Card className={s.manutencao.on ? "border-warn/60" : undefined}>
					<CardHeader title="Manutenção" description="Ligada, ninguém entra nem cria conta. Quem já está numa partida termina a partida. O painel continua funcionando." />
					<CardBody className="flex flex-col gap-3">
						<Toggle label="Servidor em manutenção">
							<Switch checked={s.manutencao.on} onCheckedChange={(on) => setS({ ...s, manutencao: { ...s.manutencao, on } })} />
						</Toggle>
						<Field label="Recado para quem tenta entrar">
							<Input value={s.manutencao.message} onChange={(e) => setS({ ...s, manutencao: { ...s.manutencao, message: e.target.value } })} placeholder="Ex.: Voltamos às 18h com o mapa novo." maxLength={200} />
						</Field>
						<div>
							<Button variant={s.manutencao.on ? "danger" : "primary"} disabled={saving === "manutencao"} onClick={() => void save("manutencao", s.manutencao, s.manutencao.on ? "Manutenção ligada." : "Manutenção desligada.")}>
								{s.manutencao.on ? "Salvar e fechar a entrada" : "Salvar"}
							</Button>
						</div>
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Evento de bônus" description="Multiplica o XP e as Presas que as partidas pagam, até a data marcada. De 1 (normal) a 5." />
					<CardBody className="flex flex-col gap-3">
						<div className="grid grid-cols-3 gap-3">
							<Field label="XP vezes">
								<Input type="number" min={1} max={5} step={0.5} value={s.bonus.xp} onChange={(e) => setS({ ...s, bonus: { ...s.bonus, xp: Number(e.target.value) } })} className="num" />
							</Field>
							<Field label="Presas vezes">
								<Input type="number" min={1} max={5} step={0.5} value={s.bonus.presas} onChange={(e) => setS({ ...s, bonus: { ...s.bonus, presas: Number(e.target.value) } })} className="num" />
							</Field>
							<Field label="Até" hint="Em branco: até desligar.">
								<Input
									type="datetime-local"
									value={s.bonus.until ? toLocalInput(new Date(s.bonus.until)) : ""}
									onChange={(e) => setS({ ...s, bonus: { ...s.bonus, until: e.target.value ? new Date(e.target.value).toISOString() : null } })}
								/>
							</Field>
						</div>
						<div className="flex gap-2">
							<Button variant="primary" disabled={saving === "bonus"} onClick={() => void save("bonus", s.bonus, bonusOn ? "Bônus valendo." : "Bônus desligado.")}>
								Salvar
							</Button>
							{bonusOn && (
								<Button
									onClick={() => {
										const off = { xp: 1, presas: 1, until: null };
										setS({ ...s, bonus: off });
										void save("bonus", off, "Bônus desligado.");
									}}
								>
									Desligar o bônus
								</Button>
							)}
						</div>
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Recompensas por partida" description="Quanto Cash cada coisa rende numa partida online, e com quanto Cash a conta nova começa. O XP segue a regra do jogo. Vale para as próximas partidas e para o resultado que o jogo mostra." />
					<CardBody className="flex flex-wrap items-end gap-3">
						{(
							[
								["base", "Por jogar"],
								["kill", "Por abate"],
								["assist", "Por assistência"],
								["win", "Vitória"],
								["draw", "Empate"],
								["mvp", "MVP"],
								["starter", "Conta nova começa com"]
							] as const
						).map(([key, label]) => (
							<Field key={key} label={label} className="w-36">
								<Input type="number" min={0} max={100000} value={s.recompensas[key]} onChange={(e) => setS({ ...s, recompensas: { ...s.recompensas, [key]: Number(e.target.value) } })} className="num" />
							</Field>
						))}
						<Button variant="primary" disabled={saving === "recompensas"} onClick={() => void save("recompensas", s.recompensas, "Recompensas salvas.")}>
							Salvar
						</Button>
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Vitrine do dia" description="Todo dia, alguns itens à venda entram em promoção até a meia-noite de Brasília. O sorteio é pela data, igual para todo mundo; consumível não entra. Zero itens desliga a vitrine." />
					<CardBody className="flex flex-wrap items-end gap-3">
						<Field label="Itens por dia" className="w-36">
							<Input type="number" min={0} max={50} value={s.vitrine.count} onChange={(e) => setS({ ...s, vitrine: { ...s.vitrine, count: Number(e.target.value) } })} className="num" />
						</Field>
						<Field label="Desconto (%)" className="w-36">
							<Input type="number" min={0} max={90} value={s.vitrine.percent} onChange={(e) => setS({ ...s, vitrine: { ...s.vitrine, percent: Number(e.target.value) } })} className="num" />
						</Field>
						<Button variant="primary" disabled={saving === "vitrine"} onClick={() => void save("vitrine", s.vitrine, "Vitrine salva.")}>
							Salvar
						</Button>
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Rodízio dos desafios" description="Quantos desafios diários e semanais valem por vez. O resto descansa e entra nos dias seguintes; o sorteio é pela data, igual para todo mundo. Zero: valem todos os que estão ligados." />
					<CardBody className="flex flex-wrap items-end gap-3">
						<Field label="Diários por dia" className="w-36">
							<Input type="number" min={0} max={50} value={s.desafios.daily} onChange={(e) => setS({ ...s, desafios: { ...s.desafios, daily: Number(e.target.value) } })} className="num" />
						</Field>
						<Field label="Semanais por semana" className="w-44">
							<Input type="number" min={0} max={50} value={s.desafios.weekly} onChange={(e) => setS({ ...s, desafios: { ...s.desafios, weekly: Number(e.target.value) } })} className="num" />
						</Field>
						<Button variant="primary" disabled={saving === "desafios"} onClick={() => void save("desafios", s.desafios, "Rodízio salvo.")}>
							Salvar
						</Button>
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Aviso no saguão" description="Um recado curto que o jogo mostra para todo mundo. Em branco, não aparece nada." />
					<CardBody className="flex flex-col gap-3">
						<Textarea value={s.aviso.text} onChange={(e) => setS({ ...s, aviso: { text: e.target.value } })} maxLength={300} placeholder="Ex.: Mapa novo no ar: Estação de Trem." aria-label="Aviso no saguão" />
						<div>
							<Button variant="primary" disabled={saving === "aviso"} onClick={() => void save("aviso", s.aviso, "Aviso salvo.")}>
								Salvar
							</Button>
						</div>
					</CardBody>
				</Card>

				<Card>
					<CardHeader title="Versão mínima do jogo" description="Quem estiver com uma versão mais velha que esta é avisado para atualizar." />
					<CardBody className="flex items-end gap-3">
						<Field label="Versão" className="w-40">
							<Input value={s.versao_minima.version} onChange={(e) => setS({ ...s, versao_minima: { version: e.target.value } })} placeholder="0.0.0" className="num" />
						</Field>
						<Button variant="primary" disabled={saving === "versao_minima"} onClick={() => void save("versao_minima", s.versao_minima, "Versão mínima salva.")}>
							Salvar
						</Button>
					</CardBody>
				</Card>
			</div>
		</>
	);
}
