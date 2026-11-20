import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Content, Overview } from "@/lib/types";

/** A visão geral, renovada sozinha: é o que o alto do painel e a primeira tela mostram. */
export const useOverview = () => useQuery({ queryKey: ["overview"], queryFn: () => api.get<Overview>("/overview"), refetchInterval: 5_000 });

/** O conteúdo do jogo (catálogo, mapas, promoções, desafios, configurações). */
export const useContent = () => useQuery({ queryKey: ["content"], queryFn: () => api.get<Content>("/content"), staleTime: 15_000 });

/** Depois de mudar algo: diz quais listas ficaram velhas. */
export function useRefresh() {
	const client = useQueryClient();
	return (...keys: string[]) => Promise.all(keys.map((key) => client.invalidateQueries({ queryKey: [key] })));
}
