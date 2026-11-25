/** O que o servidor manda, como ele manda (servidor/Alcateia.Servidor/Rotas/Admin*.cs). */

export type Permission = "view" | "moderate" | "content" | "economy" | "manage";

export interface Admin {
	id: number;
	name: string;
	role: string;
	totp: boolean;
	disabled: boolean;
	createdAt: string;
	lastLoginAt: string | null;
	lastIp: string;
}

export interface Me extends Admin {
	permissions: Permission[];
	mustEnableTotp: boolean;
	version: string;
}

export interface EventBucket {
	hour: string;
	info: number;
	warning: number;
	high: number;
	critical: number;
}

export interface SecuritySummary {
	byHour: EventBucket[];
	byKind: { kind: string; count: number }[];
	topIps: { ip: string; count: number; worst: number }[];
}

export interface Overview {
	online: number;
	rooms: number;
	roomsPlaying: number;
	fleet: { servers: number; slots: number; matches: number };
	accounts: number;
	newToday: number;
	activeDay: number;
	matchesToday: number;
	matches: number;
	presas: number;
	sangue: number;
	activeBans: number;
	ipBlocks: number;
	days: { day: string; accounts: number; matches: number }[];
	security: SecuritySummary;
	counters: { requests: number; refused: number; rateLimited: number; autoBlocks: number; events: number };
	version: string;
}

export interface PlayerRow {
	id: number;
	name: string;
	xp: number;
	rank: number;
	presas: number;
	sangue: number;
	matches: number;
	createdAt: string;
	lastLoginAt: string | null;
	banned: boolean;
	bannedUntil: string | null;
	online: boolean;
}

export interface OwnedItem {
	id: string;
	acquiredAt: string;
	expiresAt: string | null;
	source: string;
	uses: number;
}

export interface LedgerLine {
	id: number;
	at: string;
	currency: string;
	amount: number;
	balance: number;
	reason: string;
	ref: string;
}

export interface Ban {
	id: number;
	accountId: number;
	account: string;
	reason: string;
	createdAt: string;
	until: string | null;
	admin: string;
	liftedAt: string | null;
	liftedBy: string | null;
	active: boolean;
}

export interface SecurityEvent {
	id: number;
	at: string;
	kind: string;
	severity: number;
	ip: string;
	accountId: number | null;
	actor: string;
	path: string;
	detail: string;
}

export interface MatchLine {
	map: string;
	winner: number;
	scoreBlue: number;
	scoreOrange: number;
	endedAt: string;
	team: number;
	kills: number;
	deaths: number;
	assists: number;
	mvp: boolean;
	xp: number;
	presas: number;
}

export interface PlayerFile {
	player: PlayerRow & { lastIp: string; banReason: string; stats: Record<string, number> };
	inventory: OwnedItem[];
	ledger: LedgerLine[];
	bans: Ban[];
	matches: MatchLine[];
	events: SecurityEvent[];
	sameIp: { id: number; name: string }[];
}

export interface CatalogItem {
	id: string;
	name: string;
	type: string;
	kind: string;
	rarity: number;
	minRank: number;
	currency: string;
	prices: number[];
	starter: boolean;
	enabled: boolean;
	hot: boolean;
	consumable: boolean;
	team: number;
	image: string;
	sort: number;
	forSale: boolean;
	discount: number;
}

export interface GameMap {
	id: string;
	name: string;
	size: string;
	description: string;
	modes: string[];
	enabled: boolean;
	image: string;
	sort: number;
}

export interface Promotion {
	id: number;
	itemId: string;
	title: string;
	percent: number;
	startsAt: string;
	endsAt: string;
	running: boolean;
	ended: boolean;
}

export interface Challenge {
	id: string;
	period: string;
	text: string;
	metric: string;
	target: number;
	presas: number;
	xp: number;
	sangue: number;
	itemId: string | null;
	itemDays: number;
	title: string | null;
	startsAt: string | null;
	endsAt: string | null;
	active: boolean;
	sort: number;
	running: boolean;
	/** Está no rodízio de agora (diários e semanais revezam). */
	today: boolean;
}

export interface Settings {
	manutencao: { on: boolean; message: string };
	versao_minima: { version: string };
	aviso: { text: string };
	bonus: { xp: number; presas: number; until: string | null };
	recompensas: { base: number; kill: number; assist: number; win: number; draw: number; mvp: number; starter: number };
	desafios: { daily: number; weekly: number };
	vitrine: { count: number; percent: number };
}

export interface Content {
	catalog: CatalogItem[];
	maps: GameMap[];
	promotions: Promotion[];
	challenges: Challenge[];
	settings: Settings;
	kinds: string[];
	metrics: string[];
	periods: string[];
	durations: number[];
	/** Os itens da vitrine de hoje (com desconto até o dia virar). */
	dailyItems: string[];
	/** Os modos que o jogo deixa escolher hoje. */
	releasedModes: string[];
}

export interface IpBlock {
	id: number;
	cidr: string;
	reason: string;
	source: string;
	createdAt: string;
	until: string | null;
	admin: string;
}

export interface AuditEntry {
	id: number;
	at: string;
	admin: string;
	action: string;
	target: string;
	detail: string;
	ip: string;
}

export interface FleetServer {
	id: string;
	address: string;
	port: number;
	players: number;
	capacity: number;
	lastSeen: string;
	matches: { matchKey: string; title: string; map: string; startedAt: string; players: number }[];
}

export interface Room {
	id: number;
	settings: { title?: string; map?: string; teamSize?: number; play?: number; minutes?: number };
	hasPassword: boolean;
	players: number;
	bots: number;
	capacity: number;
	state: string;
	host: string;
}
