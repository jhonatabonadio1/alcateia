// Copia para o painel as imagens que o jogo já tem: as armas e skins
// (assets/ui/armas), os mapas (assets/ui/mapas) e os personagens
// (assets/personagens/<pasta>/thumb.jpg). Roda sozinho antes de
// "npm run dev" e "npm run build". Sem a pasta do jogo (um build fora do
// repositório), não faz nada: o painel mostra o nome no lugar da imagem.
import { cpSync, existsSync, mkdirSync, readdirSync } from "node:fs";
import path from "node:path";

const here = import.meta.dirname;
const game = process.env.JOGO_ASSETS ?? path.resolve(here, "../../../assets");
const out = path.resolve(here, "../public/jogo");

function copyImages(from, to, keep = () => true) {
	if (!existsSync(from)) return 0;
	mkdirSync(to, { recursive: true });
	let count = 0;
	for (const file of readdirSync(from)) {
		if (!/\.(png|jpe?g|webp)$/i.test(file) || !keep(file)) continue;
		cpSync(path.join(from, file), path.join(to, file));
		count++;
	}
	return count;
}

if (!existsSync(game)) {
	console.log(`imagens: ${game} não existe; o painel fica sem as imagens do jogo`);
} else {
	const weapons = copyImages(path.join(game, "ui/armas"), path.join(out, "armas"));
	const maps = copyImages(path.join(game, "ui/mapas"), path.join(out, "mapas"));
	let people = 0;
	const folder = path.join(game, "personagens");
	if (existsSync(folder)) {
		mkdirSync(path.join(out, "personagens"), { recursive: true });
		for (const name of readdirSync(folder)) {
			const thumb = path.join(folder, name, "thumb.jpg");
			if (!existsSync(thumb)) continue;
			cpSync(thumb, path.join(out, "personagens", `${name}.jpg`));
			people++;
		}
	}
	console.log(`imagens: ${weapons} armas, ${maps} mapas, ${people} personagens`);
}
