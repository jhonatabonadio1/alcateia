import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Em desenvolvimento (npm run dev) o painel roda no Vite e fala com o
// servidor pela porta do painel (no docker compose, 8443 na máquina; ALCATEIA_ADMIN_PORT).
// Compilado (npm run build), quem entrega os arquivos é o próprio servidor.
const servidor = process.env.PAINEL_SERVIDOR ?? "http://127.0.0.1:8443";

export default defineConfig({
	plugins: [react(), tailwindcss()],
	resolve: { alias: { "@": path.resolve(import.meta.dirname, "src") } },
	server: {
		port: 5173,
		proxy: { "/admin": { target: servidor, changeOrigin: false, headers: { Origin: servidor } } },
	},
	build: { outDir: "dist", sourcemap: false, chunkSizeWarningLimit: 900 },
});
