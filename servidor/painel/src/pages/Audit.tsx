import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useState } from "react";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, Td, Th, Tr } from "@/components/ui/table";
import { api } from "@/lib/api";
import { when } from "@/lib/format";
import type { AuditEntry } from "@/lib/types";

const actions: Record<string, string> = {
	"painel.entrou": "Entrou no painel",
	"painel.trocou_senha": "Trocou a própria senha",
	"painel.ligou_2fa": "Ligou as duas etapas",
	"painel.desligou_2fa": "Desligou as duas etapas",
	"admin.criou": "Criou administrador",
	"admin.alterou": "Alterou administrador",
	"jogador.baniu": "Baniu jogador",
	"jogador.desbaniu": "Desbaniu jogador",
	"jogador.derrubou": "Derrubou a conexão",
	"jogador.deu_moeda": "Deu moeda",
	"jogador.tirou_moeda": "Tirou moeda",
	"jogador.deu_item": "Deu item",
	"jogador.tirou_item": "Tirou item",
	"catalogo.criou": "Criou item do catálogo",
	"catalogo.alterou": "Alterou item do catálogo",
	"mapa.ligado": "Ligou mapa",
	"mapa.desligado": "Desligou mapa",
	"mapa.alterado": "Alterou mapa",
	"mapa.criado": "Criou mapa",
	"promocao.criou": "Criou promoção",
	"promocao.removeu": "Removeu promoção",
	"desafio.criou": "Criou desafio",
	"desafio.alterou": "Alterou desafio",
	"desafio.removeu": "Removeu desafio",
	"configuracao.alterou": "Alterou configuração",
	"ip.bloqueou": "Bloqueou IP",
	"ip.desbloqueou": "Desbloqueou IP",
};

export function Audit() {
	const [query, setQuery] = useState("");
	const audit = useQuery({
		queryKey: ["audit", query],
		queryFn: () => api.get<{ entries: AuditEntry[] }>(`/audit?q=${encodeURIComponent(query)}`),
		placeholderData: (previous) => previous,
	});

	return (
		<>
			<PageHeader title="Auditoria" description="Tudo o que foi feito pelo painel: quem fez, em quem, quando e de que endereço. Ninguém apaga nem edita esta lista pelo painel." />
			<div className="relative mb-3 w-full max-w-md">
				<Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-3" />
				<Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Procurar por pessoa, ação ou alvo" className="pl-9" aria-label="Procurar na auditoria" />
			</div>
			<Card>
				{audit.isPending ? (
					<Loading />
				) : audit.isError ? (
					<div className="p-4">
						<ErrorNote error={audit.error} retry={() => void audit.refetch()} />
					</div>
				) : audit.data.entries.length === 0 ? (
					<Empty title="Nada encontrado" />
				) : (
					<Table>
						<thead>
							<tr>
								<Th>Quando</Th>
								<Th>Quem</Th>
								<Th>O que fez</Th>
								<Th>Em quem ou no quê</Th>
								<Th>Detalhe</Th>
								<Th>De onde</Th>
							</tr>
						</thead>
						<tbody>
							{audit.data.entries.map((a) => (
								<Tr key={a.id}>
									<Td className="whitespace-nowrap text-ink-2">{when(a.at)}</Td>
									<Td className="font-medium">{a.admin}</Td>
									<Td className="whitespace-nowrap">{actions[a.action] ?? a.action}</Td>
									<Td>{a.target}</Td>
									<Td className="max-w-[56ch] text-ink-2">{a.detail}</Td>
									<Td className="num text-ink-3">{a.ip}</Td>
								</Tr>
							))}
						</tbody>
					</Table>
				)}
			</Card>
		</>
	);
}
