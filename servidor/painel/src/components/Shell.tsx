import { useQuery } from "@tanstack/react-query";
import {
	Ban,
	Crosshair,
	Gauge,
	LogOut,
	Map as MapIcon,
	Menu,
	Moon,
	Network,
	ScrollText,
	Server,
	Settings as SettingsIcon,
	ShieldAlert,
	ShieldCheck,
	Sun,
	Tag,
	Target,
	UserCog,
	Users,
	X,
} from "lucide-react";
import { type ReactNode, useEffect, useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Dropdown, DropdownContent, DropdownItem, DropdownSeparator, DropdownTrigger } from "@/components/ui/dropdown";
import { api } from "@/lib/api";
import { n, roleNames } from "@/lib/format";
import { useSession } from "@/lib/session";
import type { Overview, Permission } from "@/lib/types";
import { cn } from "@/lib/utils";

interface Entry {
	to: string;
	label: string;
	icon: typeof Gauge;
	needs?: Permission;
}

const groups: { name: string; entries: Entry[] }[] = [
	{
		name: "Operação",
		entries: [
			{ to: "/", label: "Visão geral", icon: Gauge },
			{ to: "/jogadores", label: "Jogadores", icon: Users },
			{ to: "/frota", label: "Frota e salas", icon: Server },
		],
	},
	{
		name: "Jogo",
		entries: [
			{ to: "/itens", label: "Armas, skins e itens", icon: Crosshair },
			{ to: "/mapas", label: "Mapas", icon: MapIcon },
			{ to: "/promocoes", label: "Promoções", icon: Tag },
			{ to: "/desafios", label: "Desafios", icon: Target },
		],
	},
	{
		name: "Segurança",
		entries: [
			{ to: "/seguranca", label: "Eventos", icon: ShieldAlert },
			{ to: "/seguranca/ips", label: "Blacklist de IP", icon: Network },
			{ to: "/seguranca/banimentos", label: "Banimentos", icon: Ban },
			{ to: "/auditoria", label: "Auditoria", icon: ScrollText },
		],
	},
	{
		name: "Sistema",
		entries: [
			{ to: "/configuracoes", label: "Configurações", icon: SettingsIcon, needs: "manage" },
			{ to: "/administradores", label: "Administradores", icon: UserCog, needs: "manage" },
		],
	},
];

function Mark() {
	return (
		<svg viewBox="0 0 32 32" className="size-7" aria-hidden>
			<path d="M8 6c2 6 2.6 13 1 20M15.5 5c2.2 7 2.8 15 1 22M23 6c2 6 2.6 13 1 20" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
		</svg>
	);
}

/** O que está acontecendo agora, sempre à vista no alto. */
function Pulse() {
	const overview = useQuery({ queryKey: ["overview"], queryFn: () => api.get<Overview>("/overview"), refetchInterval: 5_000 });
	const o = overview.data;
	const items = [
		{ label: "online", value: o?.online },
		{ label: "salas", value: o?.rooms },
		{ label: "partidas", value: o?.fleet.matches },
	];
	return (
		<dl className="flex items-center gap-5" aria-live="polite">
			<div className="flex items-center gap-2">
				<span className={cn("size-2 rounded-full", overview.isError ? "bg-danger" : "bg-good")} />
				<span className="text-[13px] text-ink-3">{overview.isError ? "Servidor sem resposta" : "Servidor no ar"}</span>
			</div>
			{items.map((item) => (
				<div key={item.label} className="hidden items-baseline gap-1.5 sm:flex">
					<dd className="display num text-base">{item.value === undefined ? "–" : n(item.value)}</dd>
					<dt className="text-[13px] text-ink-3">{item.label}</dt>
				</div>
			))}
		</dl>
	);
}

export function Shell({ children, theme, locked }: { children: ReactNode; theme: { theme: string; toggle: () => void }; locked?: boolean }) {
	const { me, can, signOut } = useSession();
	const [open, setOpen] = useState(false);
	const location = useLocation();
	const navigate = useNavigate();

	useEffect(() => setOpen(false), [location.pathname]);

	return (
		<div className="min-h-dvh lg:grid lg:grid-cols-[232px_1fr]">
			<aside
				className={cn(
					"scroll-thin fixed inset-y-0 left-0 z-30 w-[232px] overflow-y-auto border-r border-line bg-surface lg:sticky lg:top-0 lg:block lg:h-dvh",
					open ? "block" : "hidden",
				)}
			>
				<div className="flex h-14 items-center justify-between gap-2 px-4">
					<NavLink to="/" className="flex items-center gap-2.5">
						<Mark />
						<span className="leading-none">
							<span className="display block text-[17px]">WolfLeague</span>
							<span className="text-xs text-ink-3">painel</span>
						</span>
					</NavLink>
					<button type="button" className="rounded-md p-1 text-ink-3 lg:hidden" onClick={() => setOpen(false)} aria-label="Fechar o menu">
						<X className="size-5" />
					</button>
				</div>

				{!locked && (
					<nav className="px-2 pb-6">
						{groups.map((group) => {
							const entries = group.entries.filter((entry) => !entry.needs || can(entry.needs));
							if (entries.length === 0) return null;
							return (
								<div key={group.name} className="mt-4">
									<p className="px-2 pb-1 text-xs text-ink-3">{group.name}</p>
									{entries.map((entry) => (
										<NavLink
											key={entry.to}
											to={entry.to}
											end={entry.to === "/" || entry.to === "/seguranca"}
											className={({ isActive }) =>
												cn(
													"flex items-center gap-2.5 rounded-md px-2 py-1.5 text-ink-2 hover:bg-surface-2 hover:text-ink",
													isActive && "bg-primary-soft text-ink hover:bg-primary-soft [&_svg]:text-primary",
												)
											}
										>
											<entry.icon className="size-4 shrink-0" />
											{entry.label}
										</NavLink>
									))}
								</div>
							);
						})}
					</nav>
				)}
			</aside>
			{open && <button type="button" className="fixed inset-0 z-20 bg-black/50 lg:hidden" onClick={() => setOpen(false)} aria-label="Fechar o menu" />}

			<div className="min-w-0">
				<header className="sticky top-0 z-10 flex h-14 items-center justify-between gap-3 border-b border-line bg-bg/90 px-4 backdrop-blur lg:px-6">
					<div className="flex items-center gap-3">
						<button type="button" className="rounded-md p-1 text-ink-2 lg:hidden" onClick={() => setOpen(true)} aria-label="Abrir o menu">
							<Menu className="size-5" />
						</button>
						{!locked && <Pulse />}
					</div>
					<div className="flex items-center gap-1">
						<Button variant="ghost" size="icon" onClick={theme.toggle} aria-label={theme.theme === "dark" ? "Mudar para o tema claro" : "Mudar para o tema escuro"}>
							{theme.theme === "dark" ? <Sun /> : <Moon />}
						</Button>
						<Dropdown>
							<DropdownTrigger asChild>
								<Button variant="ghost" className="gap-2 px-2">
									<span className="grid size-6 place-items-center rounded-full bg-primary-soft text-xs font-semibold text-primary">{me.name.slice(0, 1).toUpperCase()}</span>
									<span className="hidden text-left leading-tight sm:block">
										<span className="block text-[13px] text-ink">{me.name}</span>
										<span className="block text-[11px] text-ink-3">{roleNames[me.role] ?? me.role}</span>
									</span>
								</Button>
							</DropdownTrigger>
							<DropdownContent>
								<DropdownItem onSelect={() => navigate("/conta")}>
									{me.totp ? <ShieldCheck /> : <ShieldAlert className="text-warn" />}
									Minha conta{!me.totp && " (ligue as duas etapas)"}
								</DropdownItem>
								<DropdownSeparator />
								<DropdownItem onSelect={() => void signOut()}>
									<LogOut />
									Sair do painel
								</DropdownItem>
							</DropdownContent>
						</Dropdown>
					</div>
				</header>
				<main className="mx-auto w-full max-w-[1400px] px-4 py-6 lg:px-6">{children}</main>
				<footer className="px-6 pb-6 text-xs text-ink-3">Servidor {me.version}</footer>
			</div>
		</div>
	);
}

