import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { Toaster } from "sonner";
import { App } from "@/App";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ApiError } from "@/lib/api";
import "@/index.css";

const queryClient = new QueryClient({
	defaultOptions: {
		queries: {
			staleTime: 5_000,
			refetchOnWindowFocus: true,
			// Recusa do servidor (4xx) não se resolve tentando de novo.
			retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
		},
	},
});

createRoot(document.getElementById("root")!).render(
	<StrictMode>
		<QueryClientProvider client={queryClient}>
			<TooltipProvider delayDuration={300}>
				<BrowserRouter>
					<App />
				</BrowserRouter>
				<Toaster position="bottom-right" theme="system" toastOptions={{ style: { background: "var(--surface-3)", color: "var(--ink)", border: "1px solid var(--line-strong)" } }} />
			</TooltipProvider>
		</QueryClientProvider>
	</StrictMode>,
);
