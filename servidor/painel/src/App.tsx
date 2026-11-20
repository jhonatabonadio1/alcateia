import { Navigate, Route, Routes } from "react-router-dom";
import { Shell } from "@/components/Shell";
import { SessionGate, useSession } from "@/lib/session";
import { useTheme } from "@/lib/theme";
import { Account } from "@/pages/Account";
import { Admins } from "@/pages/Admins";
import { Audit } from "@/pages/Audit";
import { Bans } from "@/pages/Bans";
import { Catalog } from "@/pages/Catalog";
import { Challenges } from "@/pages/Challenges";
import { Fleet } from "@/pages/Fleet";
import { IpBlocks } from "@/pages/IpBlocks";
import { Login } from "@/pages/Login";
import { Maps } from "@/pages/Maps";
import { Overview } from "@/pages/Overview";
import { PlayerDetail } from "@/pages/PlayerDetail";
import { Players } from "@/pages/Players";
import { Promotions } from "@/pages/Promotions";
import { Security } from "@/pages/Security";
import { Settings } from "@/pages/Settings";

export function App() {
	const theme = useTheme();
	return (
		<SessionGate login={(onDone) => <Login onDone={onDone} />}>
			<Panel theme={theme} />
		</SessionGate>
	);
}

function Panel({ theme }: { theme: ReturnType<typeof useTheme> }) {
	const { me } = useSession();

	// Com a segunda etapa obrigatória e ainda desligada, só a própria conta abre.
	if (me.mustEnableTotp)
		return (
			<Shell theme={theme} locked>
				<Account />
			</Shell>
		);

	return (
		<Shell theme={theme}>
			<Routes>
				<Route path="/" element={<Overview />} />
				<Route path="/jogadores" element={<Players />} />
				<Route path="/jogadores/:id" element={<PlayerDetail />} />
				<Route path="/frota" element={<Fleet />} />
				<Route path="/itens" element={<Catalog />} />
				<Route path="/mapas" element={<Maps />} />
				<Route path="/promocoes" element={<Promotions />} />
				<Route path="/desafios" element={<Challenges />} />
				<Route path="/seguranca" element={<Security />} />
				<Route path="/seguranca/ips" element={<IpBlocks />} />
				<Route path="/seguranca/banimentos" element={<Bans />} />
				<Route path="/auditoria" element={<Audit />} />
				<Route path="/configuracoes" element={<Settings />} />
				<Route path="/administradores" element={<Admins />} />
				<Route path="/conta" element={<Account />} />
				<Route path="*" element={<Navigate to="/" replace />} />
			</Routes>
		</Shell>
	);
}
