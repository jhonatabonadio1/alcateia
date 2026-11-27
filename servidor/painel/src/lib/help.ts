/**
 * A central de ajuda: os documentos do projeto que o servidor entrega
 * (GET /admin/v1/help), arrumados em temas, viram páginas e busca.
 *
 * O texto é sempre o do repositório — aqui só se decide em que tema cada
 * arquivo aparece e uma linha que diz para que ele serve. Documento novo
 * em docs/ aparece sozinho, em "Outros documentos", até ganhar um tema.
 */
import { Marked, type Tokens } from "marked";

export interface RawDoc {
	path: string;
	updatedAt: string;
	text: string;
}

export interface HelpDoc extends RawDoc {
	slug: string;
	title: string;
	topic: string;
	about: string;
}

export interface Topic {
	key: string;
	name: string;
	about: string;
}

export const topics: Topic[] = [
	{ key: "inicio", name: "Comece aqui", about: "As regras do projeto, como trabalhar nele e como usar este painel." },
	{ key: "calibrar", name: "Calibrar o jogo", about: "Onde mudar cada número: lobo, humano, armas, recuo, física, bots." },
	{ key: "design", name: "Design e conteúdo", about: "As decisões de jogo e como fazer mapa, personagem, som e tela." },
	{ key: "codigo", name: "Código e arquitetura", about: "Como o código está montado e por que foi decidido assim." },
	{ key: "servidor", name: "Servidor e operação", about: "O servidor, a frota, a segurança e como subir em produção." },
	{ key: "andamento", name: "Andamento", about: "O que falta fazer e o que entrou em cada versão." },
	{ key: "decisoes", name: "Decisões de arquitetura", about: "Uma decisão por arquivo: o contexto, o que se decidiu e o custo." },
	{ key: "outros", name: "Outros documentos", about: "Documentos que ainda não ganharam um tema." },
];

/** Em que tema cada arquivo entra, na ordem em que aparece, e para que serve. */
const known: Record<string, { topic: string; about: string; slug?: string }> = {
	"CLAUDE.md": { topic: "inicio", slug: "guia", about: "As regras que não se violam, a arquitetura, as convenções, os testes e o estado do projeto." },
	"CONTRIBUTING.md": { topic: "inicio", slug: "contribuir", about: "O passo a passo para mexer no jogo sem quebrar o que foi aprovado." },
	"docs/decisoes/0020-teste-novo-se-propoe-antes.md": {
		topic: "inicio",
		about: "Política de testes: teste novo se propõe antes; não se cria teste para cada mudança.",
	},
	"docs/PAINEL.md": { topic: "inicio", about: "Este painel: cada tela, quem pode mexer e o que o jogo enxerga." },

	"docs/CALIBRAGEM.md": { topic: "calibrar", about: "Guia de calibragem: quero mudar X — qual arquivo, qual campo, o que muda junto." },
	"docs/AJUSTES.md": { topic: "calibrar", about: "Todo número regulável, com o valor atual e onde mudar." },
	"docs/MEDIDAS.md": { topic: "calibrar", about: "As velocidades e alturas medidas no jogo, que os testes seguem." },
	"docs/ARSENAL.md": { topic: "calibrar", about: "As armas: dano, cadência, papel de cada uma e os testes de validação." },
	"docs/CONJUNTOS.md": { topic: "calibrar", about: "A economia: conjuntos, preços e o que se compra." },

	"docs/DESIGN.md": { topic: "design", about: "Cada sistema do jogo e o porquê de cada decisão." },
	"docs/UI.md": { topic: "design", about: "A interface: lobby, HUD, telas e componentes." },
	"docs/MAPAS.md": { topic: "design", about: "Os mapas, como são gerados e como fazer um novo." },
	"docs/PERSONAGENS.md": { topic: "design", about: "Como fazer um personagem jogável nas proporções certas." },
	"docs/ANIMACAO_ARMAS.md": { topic: "design", about: "As armas em primeira pessoa e as animações por arma." },
	"docs/EFEITOS.md": { topic: "design", about: "Efeitos visuais." },
	"docs/AUDIO.md": { topic: "design", about: "O som do jogo e de onde vem." },
	"docs/SONS_IA.md": { topic: "design", about: "Sons gerados por IA." },
	"docs/PACOTES.md": { topic: "design", about: "De onde vem a arte (texturas, objetos, céus, braços, armas) e as licenças." },

	"docs/ARQUITETURA.md": { topic: "codigo", about: "Como o código está montado e onde mexer em cada coisa." },
	"docs/decisoes/README.md": { topic: "codigo", slug: "decisoes", about: "A lista das decisões de arquitetura (ADR)." },

	"docs/SERVIDOR.md": { topic: "servidor", about: "O servidor: contas, salas, partida em rede, frota e testes." },
	"servidor/LEIA-ME.md": { topic: "servidor", slug: "servidor-leia-me", about: "Como rodar o servidor." },
	"docs/SERVIDOR_DEDICADO.md": { topic: "servidor", about: "O servidor de partida dedicado." },
	"docs/FLY.md": { topic: "servidor", about: "Subir e operar no Fly.io." },
	"docs/SEGURANCA.md": { topic: "servidor", about: "O escudo, a trapaça e os eventos de segurança." },

	"docs/PENDENCIAS.md": { topic: "andamento", about: "Defeitos relatados, pedidos ainda não feitos e pendências técnicas." },
	"docs/CHANGELOG.md": { topic: "andamento", about: "O que entrou em cada versão." },
};
const order = Object.keys(known);

/** "docs/decisoes/0007-testes.md" → "decisoes-0007-testes". */
function slugOf(path: string) {
	return (
		known[path]?.slug ??
		path
			.replace(/^docs\//, "")
			.replace(/\.md$/i, "")
			.replace(/[/_]+/g, "-")
			.toLowerCase()
	);
}

function titleOf(text: string, path: string) {
	const line = text.match(/^#\s+(.+)$/m)?.[1] ?? path.split("/").pop()!.replace(/\.md$/i, "");
	return line.replace(/[`*_]/g, "").trim();
}

/** Arruma o que veio do servidor: tema, título, nome na barra de endereço. */
export function catalog(raw: RawDoc[]): HelpDoc[] {
	const docs = raw.map((doc) => {
		const decision = /^docs\/decisoes\/\d{4}-/.test(doc.path);
		const meta = known[doc.path];
		const topic = meta?.topic ?? (decision ? "decisoes" : "outros");
		const firstParagraph = doc.text
			.split(/\n\s*\n/)
			.find((block) => block.trim() && !/^\s*(#|>|\||\*\*Situa|```)/.test(block));
		const about = meta?.about ?? plain(firstParagraph ?? "").slice(0, 160);
		return { ...doc, slug: slugOf(doc.path), title: titleOf(doc.text, doc.path), topic, about };
	});
	const rank = (doc: HelpDoc) => {
		const i = order.indexOf(doc.path);
		return i < 0 ? order.length : i;
	};
	return docs.sort((a, b) => rank(a) - rank(b) || a.path.localeCompare(b.path));
}

// ------------------------------------------------------------ o texto

export function escapeHtml(text: string) {
	return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

/** Markdown sem marcação, para busca e resumo. */
export function plain(text: string) {
	return text
		.replace(/```[\s\S]*?```/g, " ")
		.replace(/!?\[([^\]]*)\]\([^)]*\)/g, "$1")
		.replace(/[`*_>#|]/g, "")
		.replace(/\s+/g, " ")
		.trim();
}

/**
 * O nome da âncora de um título, igual ao do GitHub — os documentos já
 * têm links como "#9-bots-e-dificuldade", e eles precisam continuar valendo.
 */
export function anchorOf(text: string) {
	return text
		.toLowerCase()
		.trim()
		.replace(/<[^>]*>/g, "")
		.replace(/[^\p{L}\p{N}\s_-]/gu, "")
		.replace(/\s/g, "-");
}

export interface Heading {
	depth: number;
	text: string;
	id: string;
}

/** Os títulos de um documento, com a âncora de cada um (repetidos ganham -1, -2...). */
export function headingsOf(text: string): Heading[] {
	const seen = new Map<string, number>();
	const out: Heading[] = [];
	let fence = false;
	for (const line of text.split("\n")) {
		if (/^\s*```/.test(line)) fence = !fence;
		if (fence) continue;
		const match = line.match(/^(#{1,4})\s+(.+?)\s*#*\s*$/);
		if (!match) continue;
		const label = plain(match[2]);
		out.push({ depth: match[1].length, text: label, id: unique(seen, anchorOf(label)) });
	}
	return out;
}

function unique(seen: Map<string, number>, base: string) {
	const count = seen.get(base) ?? 0;
	seen.set(base, count + 1);
	return count === 0 ? base : `${base}-${count}`;
}

/** Junta um caminho relativo ("../src/x", "AJUSTES.md") ao do documento onde está. */
function resolve(from: string, href: string) {
	const parts = from.split("/").slice(0, -1);
	for (const piece of href.split("/")) {
		if (piece === "..") parts.pop();
		else if (piece !== "." && piece !== "") parts.push(piece);
	}
	return parts.join("/");
}

/**
 * O documento em HTML. Link para outro .md vira link da central; link para
 * arquivo do código fica como o caminho (o painel não abre código); HTML
 * cru é mostrado como texto — os documentos usam "<pasta>" e afins.
 */
export function render(doc: HelpDoc, all: HelpDoc[]) {
	const byPath = new Map(all.map((d) => [d.path, d]));
	const byName = new Map<string, HelpDoc>();
	for (const d of all) {
		byName.set(d.path, d);
		if (!byName.has(d.path.split("/").pop()!)) byName.set(d.path.split("/").pop()!, d);
	}
	const seen = new Map<string, number>();

	const marked = new Marked({
		gfm: true,
		renderer: {
			heading({ tokens, depth, text }: Tokens.Heading) {
				const id = unique(seen, anchorOf(plain(text)));
				const inner = this.parser.parseInline(tokens);
				return `<h${depth} id="${escapeHtml(id)}"><a class="help-anchor" href="#${escapeHtml(id)}" aria-hidden="true">#</a>${inner}</h${depth}>\n`;
			},
			link({ href, title, tokens }: Tokens.Link) {
				const inner = this.parser.parseInline(tokens);
				const titled = title ? ` title="${escapeHtml(title)}"` : "";
				if (/^(https?:|mailto:)/i.test(href))
					return `<a href="${escapeHtml(href)}"${titled} target="_blank" rel="noreferrer noopener">${inner}</a>`;
				if (href.startsWith("#")) return `<a href="${escapeHtml(href)}"${titled}>${inner}</a>`;
				const [file, hash] = href.split("#");
				const target = byPath.get(resolve(doc.path, decodeURI(file)));
				if (target) return `<a href="/ajuda/${target.slug}${hash ? `#${escapeHtml(hash)}` : ""}"${titled}>${inner}</a>`;
				return `<span class="help-file" title="${escapeHtml(resolve(doc.path, file))} — abra no repositório">${inner}</span>`;
			},
			codespan({ text }: Tokens.Codespan) {
				// `docs/AJUSTES.md`, `CLAUDE.md`... viram link para o próprio documento.
				const target = /\.md$/i.test(text) ? byName.get(text) ?? byName.get(text.replace(/^\.?\//, "")) : undefined;
				const code = `<code>${escapeHtml(text)}</code>`;
				return target && target.path !== doc.path ? `<a href="/ajuda/${target.slug}">${code}</a>` : code;
			},
			html({ text }: Tokens.HTML | Tokens.Tag) {
				return escapeHtml(text);
			},
			image({ text }: Tokens.Image) {
				return `<span class="help-file">[imagem: ${escapeHtml(text)}]</span>`;
			},
		},
	});
	return marked.parse(doc.text, { async: false }) as string;
}

// ------------------------------------------------------------ a busca

/** Sem acento e em minúscula: "calibração" acha "calibracao" e vice-versa. */
export function fold(text: string) {
	return text.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();
}

interface Section {
	doc: HelpDoc;
	heading: Heading | null;
	text: string;
	folded: string;
}

/** Cada documento cortado nos títulos: a busca leva direto à seção. */
export function sectionsOf(docs: HelpDoc[]): Section[] {
	const out: Section[] = [];
	for (const doc of docs) {
		const headings = headingsOf(doc.text);
		let h = -1;
		let lines: string[] = [];
		let fence = false;
		const flush = () => {
			const text = plain(lines.join("\n"));
			if (text) out.push({ doc, heading: h >= 0 ? headings[h] : null, text, folded: fold(text) });
			lines = [];
		};
		for (const line of doc.text.split("\n")) {
			if (/^\s*```/.test(line)) fence = !fence;
			if (!fence && /^#{1,4}\s+/.test(line)) {
				flush();
				h++;
				continue;
			}
			lines.push(line);
		}
		flush();
	}
	return out;
}

export interface Hit {
	doc: HelpDoc;
	heading: Heading | null;
	snippet: string;
	score: number;
}

/** Todas as palavras precisam aparecer; título e nome do documento pesam mais. */
export function search(sections: Section[], query: string, limit = 40): Hit[] {
	const words = fold(query)
		.split(/\s+/)
		.filter((w) => w.length > 1);
	if (words.length === 0) return [];
	const hits: Hit[] = [];
	for (const s of sections) {
		const head = fold(`${s.doc.title} ${s.heading?.text ?? ""}`);
		const all = `${head} ${s.folded}`;
		if (!words.every((w) => all.includes(w))) continue;
		let score = 0;
		for (const w of words) {
			if (fold(s.heading?.text ?? "").includes(w)) score += 8;
			if (fold(s.doc.title).includes(w)) score += 4;
			score += Math.min(s.folded.split(w).length - 1, 6);
		}
		if (s.doc.topic === "andamento") score *= 0.6; // o changelog cita tudo; não deve abafar o guia
		hits.push({ doc: s.doc, heading: s.heading, snippet: snippetOf(s, words[0]), score });
	}
	return hits.sort((a, b) => b.score - a.score).slice(0, limit);
}

function snippetOf(s: Section, word: string) {
	const at = s.folded.indexOf(word);
	if (at < 0) return s.text.slice(0, 180);
	// Começa numa palavra inteira, não no meio dela.
	let start = Math.max(0, at - 70);
	if (start > 0) {
		const space = s.text.indexOf(" ", start);
		if (space >= 0 && space < at) start = space + 1;
	}
	return (start > 0 ? "…" : "") + s.text.slice(start, start + 200).trim() + (start + 200 < s.text.length ? "…" : "");
}

/** O trecho com as palavras buscadas marcadas (já escapado). */
export function highlight(text: string, query: string) {
	const words = fold(query)
		.split(/\s+/)
		.filter((w) => w.length > 1);
	const folded = fold(text);
	const marks: [number, number][] = [];
	for (const w of words) {
		for (let i = folded.indexOf(w); i >= 0; i = folded.indexOf(w, i + w.length)) marks.push([i, i + w.length]);
	}
	marks.sort((a, b) => a[0] - b[0]);
	let out = "";
	let at = 0;
	for (const [from, to] of marks) {
		if (from < at) continue;
		out += escapeHtml(text.slice(at, from)) + `<mark>${escapeHtml(text.slice(from, to))}</mark>`;
		at = to;
	}
	return out + escapeHtml(text.slice(at));
}
