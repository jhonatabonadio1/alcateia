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
	/// (tools/gerar_mapas.py). Fica de reserva enquanto não há a foto.</summary>
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
/// Todos nascem de tools/gerar_mapas.py.</summary>
public static class Maps
{
	public static readonly List<MapInfo> All = new()
	{
		new()
		{
			// O Armazém maior e mais alto (pedido do Jhon, 17/11): o mesmo miolo,
			// paredes para fora, teto a 13 m e alas novas com contêineres até
			// 8,7 m. Desde 17/11 (22h) é "o" Armazém; o original, menor, está
			// em Guardados. O id continua armazem_estendido (salas e perfis
			// antigos com "armazem" caem nele por Get).
			Id = "armazem_estendido",
			Name = "Armazém",
			Size = "Médio",
			Scene = "res://scenes/maps/armazem_estendido.tscn",
			Thumbnail = "res://assets/ui/mapas/armazem_estendido.png",
			Cover = "res://assets/ui/mapas/armazem_estendido_foto.png",
			Description = "Médio, 75 × 45 m, galpão de contêineres de 13 m de altura. Túneis, pilhas com pranchas por cima, duas alas com pirâmides de contêineres até 8,7 m e corredores compridos junto às paredes.",
			// O vento entrando pelo galpão (18/11): o zumbido antigo (ambiente_hangar)
			// era todo grave e em caixa pequena não se ouvia nada.
			RoomSize = 0.75f, RoomWet = 0.35f, Ambience = "ambiente_armazem", AmbienceDb = -12f,
			// (de vez em quando, os pássaros do lado de fora do galpão — 20/11)
			EventSound = "evento_passaros", EventEvery = new Vector2(60, 130),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		// Os mapas de 19/11 (pedido do Jhon: "inclua esses mapas no game"): montados
		// com as peças dos kits "Low Poly FPS Map" Vol. 1, 2, 3, 5 e 6 (JustCreate),
		// com planta própria — o pacote em FBX vem sem os mapas montados
		// (tools/mapas/kits; docs/MAPAS.md).
		new()
		{
			Id = "cidade",
			Name = "Cidade",
			Size = "Grande",
			Scene = "res://scenes/maps/cidade.tscn",
			Thumbnail = "res://assets/ui/mapas/cidade.png",
			Cover = "res://assets/ui/mapas/cidade_foto.png",
			Description = "Grande, 120 × 80 m, de dia. Um bairro inteiro: três ruas de ponta a ponta, o bulevar do centro e os becos de 2,5 m por dentro dos quarteirões — entre as lojas, em volta do prédio em obras de três andares, atrás do banco. Cada time nasce numa ponta da avenida.",
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
			Size = "Grande",
			Scene = "res://scenes/maps/deserto.tscn",
			Thumbnail = "res://assets/ui/mapas/deserto.png",
			Cover = "res://assets/ui/mapas/deserto_foto.png",
			Description = "Grande, 110 × 75 m, sol alto. Uma cidadela talhada dentro da muralha: três vielas de ponta a ponta ligadas por becos de 2,5 m, a rua da feira e a praça do chafariz no meio. Casas de laje com escada de pedra; por cima da muralha, só o lobo.",
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
			Scene = "res://scenes/maps/estacao.tscn",
			Thumbnail = "res://assets/ui/mapas/estacao.png",
			Cover = "res://assets/ui/mapas/estacao_foto.png",
			Description = "Grande, 120 × 95 m, fim de tarde, no centro da cidade. A rua e a praça, o prédio da estação com as torres de relógio — saguão, duas alas com corredor e salas —, duas plataformas, três linhas com trens parados e o pátio de carga. No centro, a passarela cruza as linhas por cima dos trens.",
			// (a cidade de dia, o zumbido dos trilhos, pássaros sob a cobertura; de vez em quando um trem passa ao longe)
			RoomSize = 0.6f, RoomWet = 0.2f, Ambience = "ambiente_estacao", AmbienceDb = -15f,
			EventSound = "evento_trem", EventEvery = new Vector2(70, 140),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "obra",
			Name = "Obra",
			Size = "Grande",
			Scene = "res://scenes/maps/obra.tscn",
			Thumbnail = "res://assets/ui/mapas/obra.png",
			Cover = "res://assets/ui/mapas/obra_foto.png",
			Description = "Grande, 100 × 70 m, de dia. Um prédio de concreto em construção no meio do canteiro: corredor de 60 m, salas e salões no térreo, duas lajes abertas em cima (4 e 8 m) com escadas e muretas de tijolo. Em volta, material empilhado, andaimes e gruas.",
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
			Scene = "res://scenes/maps/neon.tscn",
			Thumbnail = "res://assets/ui/mapas/neon.png",
			Cover = "res://assets/ui/mapas/neon_foto.png",
			Description = "Grande, 124 × 100 m, de noite. Dois quarteirões de uma cidade do futuro, um de cada time, ligados pela avenida do meio e por duas ruas: lojas, o bar, a feira coberta, a praça do parque e um prédio de três lajes com escada por fora. Letreiros e faixas de LED iluminam as ruas; em volta, torres de até 100 m.",
			RoomSize = 0.6f, RoomWet = 0.22f, Ambience = "ambiente_neon", AmbienceDb = -13f,
			EventSound = "evento_drone", EventEvery = new Vector2(40, 90),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		}
	};

	/// <summary>Fora da lista por decisão do Jhon (17/11): só o Armazém
	/// (o estendido) está disponível. O Armazém original, menor, saiu na
	/// noite de 17/11. As cenas e os geradores continuam no projeto; para
	/// voltar com um, passe a linha dele para <see cref="All"/> (e o teste
	/// para tests/regression/MapTests.cs).</summary>
	public static readonly List<MapInfo> Guardados = new()
	{
		new()
		{
			Id = "armazem",
			Name = "Armazém (original)",
			Size = "Pequeno",
			Scene = "res://scenes/maps/armazem.tscn",
			Thumbnail = "res://assets/ui/mapas/armazem.png",
			Cover = "res://assets/ui/mapas/armazem_foto.png",
			Description = "Pequeno, 46 × 30 m, galpão fechado. Contêineres que viram túnel, pilhas de dois com pranchas por cima e claraboias no teto.",
			RoomSize = 0.6f, RoomWet = 0.3f, Ambience = "ambiente_armazem", AmbienceDb = -12f,
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "vila",
			Name = "Vila",
			Size = "Médio",
			Scene = "res://scenes/maps/vila.tscn",
			Thumbnail = "res://assets/ui/mapas/vila.png",
			Description = "Médio, 72 × 52 m, fim de tarde. Praça com chafariz e feira, casas de reboco com terraço e vielas estreitas entre elas.",
			RoomSize = 0.45f, RoomWet = 0.1f, Ambience = "ambiente_vila", AmbienceDb = -12f,
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "polar",
			Name = "Estação Polar",
			Size = "Grande",
			Scene = "res://scenes/maps/polar.tscn",
			Thumbnail = "res://assets/ui/mapas/polar.png",
			Description = "Grande, 110 × 70 m, neve e crepúsculo. Garagem central atravessável, alojamentos em fileiras, passarelas suspensas, tanques e torre de rádio.",
			RoomSize = 0.3f, RoomWet = 0.05f, Ambience = "ambiente_polar", AmbienceDb = -12f,
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		}
	};

	public static MapInfo Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? All[0];
}
