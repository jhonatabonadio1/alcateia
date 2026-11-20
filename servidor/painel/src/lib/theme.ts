import { useEffect, useState } from "react";

type Theme = "dark" | "light";
const key = "wl-painel-tema";

function stored(): Theme {
	try {
		const saved = localStorage.getItem(key);
		if (saved === "dark" || saved === "light") return saved;
	} catch {
		// Sem armazenamento (navegação privada): vale o do sistema.
	}
	return window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
}

/** Noite ou dia. Fica guardado no navegador de quem escolheu. */
export function useTheme() {
	const [theme, setTheme] = useState<Theme>(stored);
	useEffect(() => {
		document.documentElement.dataset.theme = theme;
		try {
			localStorage.setItem(key, theme);
		} catch {
			// Idem.
		}
	}, [theme]);
	return { theme, toggle: () => setTheme((t) => (t === "dark" ? "light" : "dark")) };
}
