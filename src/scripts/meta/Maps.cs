using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class MapInfo
{
	public string Id;

	// O nome, a descrição e o tamanho saem no idioma do jogo (assets/i18n,
	// pedido do Jhon, 21/11): o que está aqui é a chave, em português.
	private string _name, _description, _size = "Médio";

	public string Name { get => Loc.T(_name); set => _name = value; }
	public string Scene;
	/// <summary>Planta vista de cima, gerada pelo gerador do mapa
	/// (src/tools/gerar_mapas.py). Fica de reserva enquanto não há a foto.</summary>
	public string Thumbnail;

	/// <summary>A foto do mapa, de um bom ângulo, tirada no próprio jogo por
	/// tools/fotos_mapas.gd (1920 × 1080, PNG). Aparece na escolha do mapa, na
	/// sala e em tela cheia no carregamento (decisão do Jhon, 17/11).</summary>
	public string Cover;

	/// <summary>A imagem a mostrar: a foto, ou a planta se ainda não há foto.</summary>
	public string Picture => !string.IsNullOrEmpty(Cover) && ResourceLoader.Exists(Cover) ? Cover : Thumbnail;
	public string Description { get => Loc.T(_description); set => _description = value; }
	/// <summary>Pequeno, Médio ou Grande.</summary>
	public string Size { get => Loc.T(_size); set => _size = value; }
	public GameMode[] Modes;

	/// <summary>A acústica do lugar (<see cref="Sfx.SetRoom"/>): tamanho
	/// de 0 a 1 e quanto de reverberação entra. Fechado e duro, muito;
	/// aberto e com neve, quase nada.</summary>
	public float RoomSize = 0.4f;
	public float RoomWet = 0.1f;

	/// <summary>Som de fundo em laço (assets/audio) e o volume, em dB.</summary>
	public string Ambience;
	public float AmbienceDb = -14f;

	/// <summary>Um som que toca de vez em quando, e de quanto em quanto
	/// tempo (mínimo e máximo, em segundos).</summary>
	public string EventSound;
	public Vector2 EventEvery = new(60, 120);
}

/// <summary>Os mapas jogáveis. Mapa novo entra aqui e já aparece na sala e
/// na lista; a cena da partida carrega o da sala (<see cref="MapSlot"/>).
/// A fonte de cada um é a cena (docs/MAPAS.md).</summary>
public static class Maps
{
	public static readonly List<MapInfo> All = new()
	{
		new()
		{
			// O Armazém maior e mais alto (pedido do Jhon, 17/11): o mesmo miolo,
			// paredes para fora, teto a 13 m e alas novas com contêineres até
			// 8,7 m. Desde 17/11 (22h) é "o" Armazém. O original, menor, a Vila
			// e a Estação Polar saíram do projeto em 24/11 (Jhon: "mapas que não
			// são utilizados no jogo podem ser removidos"). O id continua
			// armazem_estendido (salas e perfis antigos com "armazem", "vila" ou
			// "polar" caem nele por Get).
			Id = "armazem_estendido",
			Name = "Armazém",
			Size = "Médio",
			Scene = "res://src/scenes/maps/armazem_estendido.tscn",
			Thumbnail = "res://src/assets/ui/mapas/armazem_estendido.png",
			Cover = "res://src/assets/ui/mapas/armazem_estendido_foto.png",
			Description = "Médio, 75 × 45 m, galpão de contêineres de 13 m de altura. Túneis, pilhas com pranchas por cima, duas alas com pirâmides de contêineres até 8,7 m e corredores compridos junto às paredes.",
			// O vento entrando pelo galpão (18/11): o zumbido antigo (ambiente_hangar)
			// era todo grave e em caixa pequena não se ouvia nada.
			RoomSize = 0.75f, RoomWet = 0.35f, Ambience = "ambiente_armazem", AmbienceDb = -12f,
			// (de vez em quando, os pássaros do lado de fora do galpão — 20/11)
			EventSound = "evento_passaros", EventEvery = new Vector2(60, 130),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		// Os mapas dos kits "Low Poly FPS Map" (JustCreate). Desde 27/11 a Cidade, a Cidade do Deserto, a Estação de
		// Trem e a Obra são a cena de demonstração de cada kit, lida do pacote do Unreal e no tamanho dela (pedido do
		// Jhon: "use o que tem lá de demo"; src/tools/mapas; docs/MAPAS.md). Até 26/11 eram plantas próprias, montadas
		// com as peças soltas do kit.
		new()
		{
			Id = "cidade",
			Name = "Cidade",
			Size = "Grande",
			Scene = "res://src/scenes/maps/cidade.tscn",
			Thumbnail = "res://src/assets/ui/mapas/cidade.png",
			Cover = "res://src/assets/ui/mapas/cidade_foto.png",
			Description = "Grande, 86 × 92 m, de dia. Um bairro em volta de uma avenida de 80 m, entre a prefeitura e a torre do heliponto: casas de três e quatro andares, o estacionamento, a praça, a oficina, o pátio do prédio em obras e os becos por trás das casas. O azul nasce junto à prefeitura, o laranja no fim da avenida.",
			// O fundo de cada mapa de kit é o dele desde 20/11 (pedido do Jhon: "som de ambiente em todos os mapas,
			// pássaros, buzinas de carros — deserto não tem carro"): tools/sons/ambiente_mapas.py. Aqui, a cidade ao
			// longe com pássaros e buzinas; de vez em quando, duas buzinadas mais perto.
			RoomSize = 0.5f, RoomWet = 0.12f, Ambience = "ambiente_cidade", AmbienceDb = -13f,
			EventSound = "evento_buzina", EventEvery = new Vector2(35, 80),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "deserto",
			Name = "Cidade do Deserto",
			Size = "Médio",
			Scene = "res://src/scenes/maps/deserto.tscn",
			Thumbnail = "res://src/assets/ui/mapas/deserto.png",
			Cover = "res://src/assets/ui/mapas/deserto_foto.png",
			Description = "Médio, 64 × 64 m, sol alto. Uma cidadela dentro da muralha: casas de adobe de um e dois andares com escada de pedra, toldos, bancas de feira, a praça do chafariz e vielas de 3 a 6 m entre os blocos de casas. Os times nascem em cantos opostos; por cima da muralha ninguém passa.",
			// (vento seco e pássaros longe; nenhum carro)
			RoomSize = 0.4f, RoomWet = 0.08f, Ambience = "ambiente_deserto", AmbienceDb = -14f,
			EventSound = "evento_passaros", EventEvery = new Vector2(45, 100),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "estacao",
			Name = "Estação de Trem",
			Size = "Grande",
			Scene = "res://src/scenes/maps/estacao.tscn",
			Thumbnail = "res://src/assets/ui/mapas/estacao.png",
			Cover = "res://src/assets/ui/mapas/estacao_foto.png",
			Description = "Grande, 105 × 65 m, fim de tarde. Um pátio de estação fechado por paredes de 9 m: três linhas com trens parados ao norte, o saguão coberto no meio, mais duas linhas ao sul, e os prédios da estação em volta, com duas escadas para o piso de cima. O azul nasce a oeste, o laranja na ala leste.",
			// (a cidade de dia, o zumbido dos trilhos, pássaros sob a cobertura; de vez em quando um trem passa ao longe)
			RoomSize = 0.6f, RoomWet = 0.2f, Ambience = "ambiente_estacao", AmbienceDb = -15f,
			EventSound = "evento_trem", EventEvery = new Vector2(70, 140),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "obra",
			Name = "Obra",
			Size = "Pequeno",
			Scene = "res://src/scenes/maps/obra.tscn",
			Thumbnail = "res://src/assets/ui/mapas/obra.png",
			Cover = "res://src/assets/ui/mapas/obra_foto.png",
			Description = "Pequeno, 40 × 40 m em três pisos, de dia, no alto de um prédio acima das nuvens. Um canteiro de obras na laje: paredes de concreto pela metade, escadas para o segundo piso, um pedaço de terceiro, gruas, andaimes e material empilhado. Em volta, só o céu do pôr do sol e o mar de nuvens lá embaixo.",
			// (o canteiro ao longe — máquina, batidas —, vento e pássaros; de vez em quando um cachorro late na vizinhança)
			RoomSize = 0.55f, RoomWet = 0.18f, Ambience = "ambiente_obra", AmbienceDb = -15f,
			EventSound = "evento_cachorro", EventEvery = new Vector2(50, 110),
			// só mata-mata (todos contra todos, nascendo espalhados: os pontos são o nó NascerLivre da cena) — Jhon, 20/11
			Modes = new[] { GameMode.MataMata }
		},
		new()
		{
			// A cidade do futuro do pacote "Low Poly Sci-Fi City" (pedido do Jhon, 20/11: "implemente esse mapa no
			// jogo, refaça a luz e as texturas e deixe ele maior; use LED para iluminar as partes que representam
			// LED"): a cena de demonstração do pacote, lida do arquivo do Unity, duas vezes — a cópia girada 180° —,
			// de noite, com os LEDs acesos (tools/mapas/neon.py; docs/MAPAS.md).
			Id = "neon",
			Name = "Distrito Neon",
			Size = "Grande",
			Scene = "res://src/scenes/maps/neon.tscn",
			Thumbnail = "res://src/assets/ui/mapas/neon.png",
			Cover = "res://src/assets/ui/mapas/neon_foto.png",
			Description = "Grande, 124 × 100 m, de noite. Dois quarteirões de uma cidade do futuro, um de cada time, ligados pela avenida do meio e por duas ruas: lojas, o bar, a feira coberta, a praça do parque e um prédio de três lajes com escada por fora. Letreiros e faixas de LED iluminam as ruas; em volta, torres de até 100 m.",
			RoomSize = 0.6f, RoomWet = 0.22f, Ambience = "ambiente_neon", AmbienceDb = -13f,
			EventSound = "evento_drone", EventEvery = new Vector2(40, 90),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		}
	};

	public static MapInfo Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? All[0];
}
