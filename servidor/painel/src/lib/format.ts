/** Como o painel escreve datas, números e os nomes das coisas do jogo. */

const number = new Intl.NumberFormat("pt-BR");
const compact = new Intl.NumberFormat("pt-BR", { notation: "compact", maximumFractionDigits: 1 });
const dateTime = new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit", year: "2-digit", hour: "2-digit", minute: "2-digit" });
const dayMonth = new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit" });
const hour = new Intl.DateTimeFormat("pt-BR", { hour: "2-digit", minute: "2-digit" });

export const n = (value: number) => number.format(value);
export const short = (value: number) => (Math.abs(value) < 10_000 ? number.format(value) : compact.format(value));

export function when(iso: string | null | undefined) {
	if (!iso) return "nunca";
	// O Postgres manda "2026-11-20 12:00:00+00"; o navegador quer o T.
	const date = new Date(iso.includes("T") ? iso : iso.replace(" ", "T").replace(/([+-]\d\d)$/, "$1:00"));
	return Number.isNaN(date.getTime()) ? iso : dateTime.format(date);
}

export const day = (iso: string) => dayMonth.format(new Date(`${iso}T12:00:00`));
export const hourOf = (iso: string) => hour.format(new Date(iso));

/** "há 5 min", "em 2 dias". */
export function relative(iso: string | null | undefined) {
	if (!iso) return "nunca";
	const seconds = (new Date(iso).getTime() - Date.now()) / 1000;
	const abs = Math.abs(seconds);
	const [value, unit] =
		abs < 60 ? [Math.round(abs), "s"] : abs < 3600 ? [Math.round(abs / 60), "min"] : abs < 86400 ? [Math.round(abs / 3600), "h"] : [Math.round(abs / 86400), "d"];
	return seconds < 0 ? `há ${value} ${unit}` : `em ${value} ${unit}`;
}

/** Para um campo datetime-local, no horário de quem está usando o painel. */
export function toLocalInput(date: Date) {
	const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
	return local.toISOString().slice(0, 16);
}

export const rarityNames = ["Comum", "Incomum", "Rara", "Épica", "Lendária"];

export const kindNames: Record<string, string> = {
	weapon: "Armas e skins",
	utility: "Utilitários",
	tattoo: "Melhorias de lobo",
	breed: "Classes de lobo",
	character: "Personagens",
	service: "Serviços",
	photo: "Fotos de perfil",
};

export const durationNames = ["1 dia", "7 dias", "30 dias", "90 dias", "Para sempre"];

export const periodNames: Record<string, string> = { daily: "Diário", weekly: "Semanal", permanent: "Permanente", event: "Evento" };

export const metricNames: Record<string, string> = {
	matches: "Partidas jogadas",
	wins: "Vitórias",
	kills: "Abates",
	kills_wolf: "Abates como lobo",
	kills_human: "Abates como humano",
	headshots: "Abates com tiro na cabeça",
	assists: "Assistências",
	minutes: "Minutos jogados",
	mvps: "Vezes como MVP",
};

export const modeNames: Record<string, string> = {
	equipe: "Equipe",
	mata_mata: "Mata-mata",
	dominacao: "Dominação",
	cacada: "Caçada",
	sobrevivencia: "Sobrevivência",
};

export const roleNames: Record<string, string> = { dono: "Dono", admin: "Administrador", moderador: "Moderador", leitor: "Leitor" };

export const roleHelp: Record<string, string> = {
	dono: "Faz tudo, inclusive configurações e administradores.",
	admin: "Modera, edita o jogo e mexe em moeda e itens.",
	moderador: "Bane, derruba conexão e bloqueia IP.",
	leitor: "Só vê.",
};

export const currencyNames: Record<string, string> = { presas: "Presas", sangue: "Sangue" };

export const teamNames = ["Os dois times", "Vanguarda", "Sindicato"];

/** O nome de cada tipo de evento de segurança, em língua de gente. */
export const eventNames: Record<string, string> = {
	"login.ok": "Login",
	"login.falhou": "Login errado",
	"login.ok_apos_falhas": "Login certo depois de vários errados",
	"login.banido": "Banido tentou entrar",
	"conta.criada": "Conta criada",
	"conta.trocou_nome": "Troca de nome",
	"ataque.forca_bruta": "Força bruta no login",
	"ataque.varios_nomes": "Uma senha em várias contas",
	"ataque.varredura": "Varredura de rotas",
	"ataque.injecao": "Tentativa de injeção",
	"ataque.enumeracao": "Enumeração",
	"freio.pedidos": "Pedidos demais",
	"bloqueio.pedido_recusado": "Pedido de IP bloqueado",
	"bloqueio.automatico": "IP bloqueado sozinho",
	"frota.chave_invalida": "Chave da frota errada",
	"erro.servidor": "Erro no servidor",
	"painel.login.ok": "Entrada no painel",
	"painel.login.falhou": "Login errado no painel",
	"painel.conta_travada": "Conta do painel travada",
	"painel.sem_permissao": "Painel: acesso negado",
	"trapaca.suspeita": "Resultado suspeito de trapaça",
};

export const severityNames = ["Informativo", "Atenção", "Grave", "Crítico"];

export const eventGroups = [
	{ value: "todos", label: "Todos os tipos" },
	{ value: "login.", label: "Logins do jogo" },
	{ value: "ataque.", label: "Ataques" },
	{ value: "bloqueio.", label: "Bloqueios" },
	{ value: "painel.", label: "Painel" },
	{ value: "trapaca.", label: "Trapaça" },
	{ value: "freio.", label: "Freio de pedidos" },
];

/** O XP em que cada patente começa — a mesma conta do jogo e do servidor. */
export function xpForRank(rank: number) {
	if (rank <= 0) return 0;
	return Math.round((230 * Math.pow(rank, 2.6)) / 10) * 10;
}
