import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ArrowRight, BookOpen, ChevronRight, FileText, Search } from "lucide-react";
import { type MouseEvent, type RefObject, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { Empty, ErrorNote, Loading, PageHeader } from "@/components/common";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api } from "@/lib/api";
import { relative } from "@/lib/format";
import { catalog, type HelpDoc, headingsOf, highlight, type RawDoc, render, search, sectionsOf, topics } from "@/lib/help";
import { cn } from "@/lib/utils";

/**
 * A central de ajuda: os documentos do repositório, com busca. O servidor lê
 * os arquivos a cada pedido; aqui só se arruma e desenha. Para mudar um
 * texto, mude o .md no repositório — não há cópia no painel.
 */
function useHelp() {
	const query = useQuery({
		queryKey: ["help"],
		queryFn: () => api.get<{ docs: RawDoc[] }>("/help"),
		staleTime: 5 * 60_000,
	});
	const docs = useMemo(() => (query.data ? catalog(query.data.docs) : []), [query.data]);
	return { ...query, docs };
}

export function Help() {
	const { slug } = useParams();
	const help = useHelp();

	if (help.isPending) return <Loading label="Carregando a central de ajuda…" />;
	if (help.isError) return <ErrorNote error={help.error} retry={() => void help.refetch()} />;
	if (help.docs.length === 0)
		return (
			<Card>
				<Empty title="Nenhum documento encontrado">
					O servidor não achou a pasta dos documentos. Na imagem do Docker ela é copiada para <code>ajuda/</code>; fora dele, aponte{" "}
					<code>ALCATEIA_ADMIN_DOCS</code> para a raiz do repositório.
				</Empty>
			</Card>
		);

	if (!slug) return <HelpHome docs={help.docs} />;
	const doc = help.docs.find((d) => d.slug === slug);
	if (!doc)
		return (
			<Card>
				<Empty title="Documento não encontrado">
					Ele pode ter mudado de nome. <Link to="/ajuda" className="text-primary hover:underline">Voltar à central</Link>
				</Empty>
			</Card>
		);
	return <HelpArticle doc={doc} docs={help.docs} />;
}

// ------------------------------------------------------------ a entrada

/** As perguntas mais comuns, cada uma apontando para onde se responde. */
const shortcuts = [
	{ slug: "calibragem", label: "Mudar velocidade, dano, recuo ou física", note: "Guia de calibragem" },
	{ slug: "decisoes-0020-teste-novo-se-propoe-antes", label: "Criar ou mudar um teste", note: "Política de testes" },
	{ slug: "guia", label: "As regras que não se violam", note: "Guia do projeto" },
	{ slug: "painel", label: "Usar este painel", note: "Cada tela e quem pode mexer" },
	{ slug: "pendencias", label: "O que falta fazer", note: "Defeitos e pedidos em aberto" },
	{ slug: "mapas", label: "Fazer um mapa novo", note: "Mapas" },
];

function HelpHome({ docs }: { docs: HelpDoc[] }) {
	const [query, setQuery] = useState(() => new URLSearchParams(window.location.search).get("q") ?? "");
	const field = useRef<HTMLInputElement>(null);
	const sections = useMemo(() => sectionsOf(docs), [docs]);
	const hits = useMemo(() => search(sections, query), [sections, query]);
	const searching = query.trim().length > 1;

	// "/" põe o cursor na busca, como nos sites de documentação.
	useEffect(() => {
		const onKey = (event: KeyboardEvent) => {
			const typing = event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement;
			if (event.key === "/" && !typing) {
				event.preventDefault();
				field.current?.focus();
			}
		};
		window.addEventListener("keydown", onKey);
		return () => window.removeEventListener("keydown", onKey);
	}, []);

	// A busca fica no endereço: voltar de um documento devolve os resultados.
	useEffect(() => {
		const url = new URL(window.location.href);
		if (query.trim()) url.searchParams.set("q", query);
		else url.searchParams.delete("q");
		window.history.replaceState(window.history.state, "", url);
	}, [query]);

	return (
		<>
			<PageHeader
				title="Central de ajuda"
				description="Os documentos do projeto, lidos do repositório a cada visita: regras, calibragem, design, código, servidor e o que falta fazer. Para uso interno."
			/>

			<div className="relative mb-6 max-w-[720px]">
				<Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-3" />
				<Input
					ref={field}
					autoFocus
					value={query}
					onChange={(event) => setQuery(event.target.value)}
					onKeyDown={(event) => event.key === "Escape" && setQuery("")}
					placeholder="Buscar em todos os documentos — ex.: recuo, velocidade do lobo, granada"
					className="h-11 pr-10 pl-9 text-[15px]"
					aria-label="Buscar na central de ajuda"
				/>
				<kbd className="absolute top-1/2 right-3 hidden -translate-y-1/2 rounded border border-line px-1.5 text-xs text-ink-3 sm:block">/</kbd>
			</div>

			{searching ? <Results hits={hits} query={query} /> : <Topics docs={docs} />}
		</>
	);
}

function Results({ hits, query }: { hits: ReturnType<typeof search>; query: string }) {
	if (hits.length === 0)
		return (
			<Card>
				<Empty title={`Nada encontrado para "${query}"`}>Tente outra palavra, ou uma só. A busca ignora acento e maiúscula.</Empty>
			</Card>
		);
	return (
		<div className="max-w-[880px]">
			<p className="mb-2 text-[13px] text-ink-3">
				{hits.length === 40 ? "Os 40 trechos mais relevantes" : `${hits.length} ${hits.length === 1 ? "trecho" : "trechos"}`}
			</p>
			<Card className="divide-y divide-line">
				{hits.map((hit, i) => (
					<Link
						key={`${hit.doc.path}-${hit.heading?.id ?? "topo"}-${i}`}
						to={`/ajuda/${hit.doc.slug}${hit.heading ? `#${hit.heading.id}` : ""}`}
						className="block px-4 py-3 hover:bg-surface-2"
					>
						<p className="flex flex-wrap items-center gap-1 text-[13px] text-ink-3">
							{hit.doc.title}
							{hit.heading && (
								<>
									<ChevronRight className="size-3.5" />
									<span className="text-ink-2">{hit.heading.text}</span>
								</>
							)}
						</p>
						{/* O trecho já vem escapado de highlight(). */}
						<p className="help-snippet mt-1 text-ink" dangerouslySetInnerHTML={{ __html: highlight(hit.snippet, query) }} />
					</Link>
				))}
			</Card>
		</div>
	);
}

function Topics({ docs }: { docs: HelpDoc[] }) {
	const bySlug = new Set(docs.map((d) => d.slug));
	return (
		<>
			<section className="mb-8" aria-labelledby="atalhos">
				<h2 id="atalhos" className="display mb-3 text-[15px]">
					Começar por uma pergunta
				</h2>
				<div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
					{shortcuts
						.filter((s) => bySlug.has(s.slug))
						.map((s) => (
							<Link key={s.slug} to={`/ajuda/${s.slug}`} className="group">
								<Card className="flex h-full items-center gap-3 px-4 py-3 transition-colors group-hover:border-line-strong group-hover:bg-surface-2">
									<BookOpen className="size-5 shrink-0 text-primary" />
									<span className="min-w-0 flex-1">
										<span className="block font-medium">{s.label}</span>
										<span className="block text-[13px] text-ink-3">{s.note}</span>
									</span>
									<ArrowRight className="size-4 shrink-0 text-ink-3 group-hover:text-ink" />
								</Card>
							</Link>
						))}
				</div>
			</section>

			<div className="grid gap-4 lg:grid-cols-2">
				{topics.map((topic) => {
					const list = docs.filter((d) => d.topic === topic.key);
					if (list.length === 0) return null;
					const compact = topic.key === "decisoes";
					return (
						<Card key={topic.key} className={cn(compact && "lg:col-span-2")}>
							<div className="px-4 pt-4 pb-2">
								<h2 className="display text-[15px]">{topic.name}</h2>
								<p className="mt-0.5 text-[13px] text-ink-3">{topic.about}</p>
							</div>
							<ul className={cn("px-2 pb-3", compact && "grid gap-x-4 md:grid-cols-2")}>
								{list.map((doc) => (
									<li key={doc.path}>
										<Link to={`/ajuda/${doc.slug}`} className="flex gap-2.5 rounded-md px-2 py-1.5 hover:bg-surface-2">
											<FileText className="mt-0.5 size-4 shrink-0 text-ink-3" />
											<span className="min-w-0">
												<span className="block text-ink">{doc.title}</span>
												{!compact && doc.about && <span className="block text-[13px] text-ink-3">{doc.about}</span>}
											</span>
										</Link>
									</li>
								))}
							</ul>
						</Card>
					);
				})}
			</div>
		</>
	);
}

// ------------------------------------------------------------ um documento

function HelpArticle({ doc, docs }: { doc: HelpDoc; docs: HelpDoc[] }) {
	const html = useMemo(() => render(doc, docs), [doc, docs]);
	const toc = useMemo(() => headingsOf(doc.text).filter((h) => h.depth === 2 || h.depth === 3), [doc.text]);
	const topic = topics.find((t) => t.key === doc.topic);
	const siblings = docs.filter((d) => d.topic === doc.topic);
	const at = siblings.indexOf(doc);
	const previous = at > 0 ? siblings[at - 1] : null;
	const next = at >= 0 && at < siblings.length - 1 ? siblings[at + 1] : null;
	const location = useLocation();
	const navigate = useNavigate();
	const article = useRef<HTMLElement>(null);
	const current = useCurrentHeading(article, html);

	// Abriu com "#seção" (da busca ou de um link): vai até ela; sem, volta ao topo.
	useEffect(() => {
		const id = decodeURIComponent(location.hash.slice(1));
		const target = id ? document.getElementById(id) : null;
		if (target) target.scrollIntoView();
		else window.scrollTo(0, 0);
	}, [html, location.hash]);

	// Link para outro documento troca de tela sem recarregar o painel.
	function onClick(event: MouseEvent<HTMLElement>) {
		const link = (event.target as HTMLElement).closest("a");
		const href = link?.getAttribute("href");
		if (!href || event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) return;
		if (href.startsWith("/ajuda")) {
			event.preventDefault();
			navigate(href);
		} else if (href.startsWith("#")) {
			event.preventDefault();
			navigate({ hash: href });
		}
	}

	return (
		<>
			<nav className="mb-4 flex flex-wrap items-center gap-1 text-[13px] text-ink-3" aria-label="Onde você está">
				<Link to="/ajuda" className="hover:text-ink">
					Central de ajuda
				</Link>
				<ChevronRight className="size-3.5" />
				<span>{topic?.name}</span>
			</nav>

			<div className="grid gap-8 xl:grid-cols-[minmax(0,1fr)_240px]">
				<div className="min-w-0">
					<Card className="px-5 py-6 sm:px-8">
						<p className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
							<code className="rounded bg-surface-2 px-1.5 py-0.5">{doc.path}</code>
							<span>atualizado {relative(doc.updatedAt)}</span>
						</p>
						{/* HTML do Markdown do repositório; HTML cru dentro dele sai escapado (lib/help.ts). */}
						<article ref={article} className="help-prose" onClick={onClick} dangerouslySetInnerHTML={{ __html: html }} />
					</Card>

					{(previous || next) && (
						<nav className="mt-4 grid gap-3 sm:grid-cols-2" aria-label="Outros documentos do tema">
							{previous ? (
								<Link to={`/ajuda/${previous.slug}`} className="rounded-lg border border-line bg-surface px-4 py-3 hover:bg-surface-2">
									<span className="flex items-center gap-1 text-xs text-ink-3">
										<ArrowLeft className="size-3.5" /> Anterior
									</span>
									<span className="mt-0.5 block font-medium">{previous.title}</span>
								</Link>
							) : (
								<span />
							)}
							{next && (
								<Link to={`/ajuda/${next.slug}`} className="rounded-lg border border-line bg-surface px-4 py-3 text-right hover:bg-surface-2">
									<span className="flex items-center justify-end gap-1 text-xs text-ink-3">
										Próximo <ArrowRight className="size-3.5" />
									</span>
									<span className="mt-0.5 block font-medium">{next.title}</span>
								</Link>
							)}
						</nav>
					)}
				</div>

				{toc.length > 1 && (
					<aside className="hidden xl:block">
						<nav className="scroll-thin sticky top-20 max-h-[calc(100dvh-6rem)] overflow-y-auto" aria-label="Nesta página">
							<p className="mb-2 text-xs text-ink-3">Nesta página</p>
							<ul className="border-l border-line text-[13px]">
								{toc.map((h) => (
									<li key={h.id}>
										<Link
											to={{ hash: h.id }}
											className={cn(
												"-ml-px block border-l py-1 pr-2 text-ink-3 hover:text-ink",
												h.depth === 3 ? "pl-6" : "pl-3",
												current === h.id ? "border-primary text-ink" : "border-transparent",
											)}
										>
											{h.text}
										</Link>
									</li>
								))}
							</ul>
						</nav>
					</aside>
				)}
			</div>
		</>
	);
}

/** O título da seção que está no alto da tela, para marcar no índice ao lado. */
function useCurrentHeading(article: RefObject<HTMLElement | null>, html: string) {
	const [current, setCurrent] = useState<string>("");
	useEffect(() => {
		const root = article.current;
		if (!root) return;
		const headings = Array.from(root.querySelectorAll<HTMLElement>("h2[id], h3[id]"));
		const onScroll = () => {
			let id = headings[0]?.id ?? "";
			for (const h of headings) {
				if (h.getBoundingClientRect().top > 96) break;
				id = h.id;
			}
			setCurrent(id);
		};
		onScroll();
		window.addEventListener("scroll", onScroll, { passive: true });
		return () => window.removeEventListener("scroll", onScroll);
	}, [article, html]);
	return current;
}
