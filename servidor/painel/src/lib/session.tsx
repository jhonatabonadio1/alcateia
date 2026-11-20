import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createContext, type ReactNode, useContext, useEffect } from "react";
import { api, ApiError, setOnSignedOut } from "@/lib/api";
import type { Me, Permission } from "@/lib/types";

interface Session {
	me: Me;
	can: (permission: Permission) => boolean;
	signOut: () => Promise<void>;
	refresh: () => Promise<unknown>;
}

const SessionContext = createContext<Session | null>(null);

export function useSession() {
	const session = useContext(SessionContext);
	if (!session) throw new Error("useSession fora do painel");
	return session;
}

/** Quem está no painel. Sem sessão, mostra a tela de entrada no lugar do conteúdo. */
export function SessionGate({ login, children }: { login: (onDone: () => void) => ReactNode; children: ReactNode }) {
	const client = useQueryClient();
	const me = useQuery({
		queryKey: ["me"],
		queryFn: () => api.get<Me>("/me"),
		retry: (count, error) => !(error instanceof ApiError && error.status === 401) && count < 2,
		staleTime: 60_000,
	});

	useEffect(() => {
		setOnSignedOut(() => client.setQueryData(["me"], null));
	}, [client]);

	if (me.isPending) return <div className="grid min-h-dvh place-items-center text-ink-3">Abrindo o painel…</div>;

	const signedOut = !me.data || (me.error instanceof ApiError && me.error.status === 401);
	if (signedOut) {
		if (me.error && !(me.error instanceof ApiError && me.error.status === 401))
			return (
				<div className="grid min-h-dvh place-items-center p-6 text-center">
					<div>
						<p className="display text-lg">O servidor não respondeu</p>
						<p className="mt-1 text-ink-2">{me.error.message}</p>
					</div>
				</div>
			);
		return <>{login(() => client.invalidateQueries({ queryKey: ["me"] }))}</>;
	}

	const session: Session = {
		me: me.data,
		can: (permission) => me.data.permissions.includes(permission),
		signOut: async () => {
			await api.post("/logout").catch(() => undefined);
			client.clear();
			client.setQueryData(["me"], null);
		},
		refresh: () => client.invalidateQueries({ queryKey: ["me"] }),
	};
	return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}
