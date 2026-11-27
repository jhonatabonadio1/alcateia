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

	/// <summary>As nuvens do céu do mapa (<see cref="SkyClouds"/>): "dia",
	/// "dia_seco" (o deserto: poucas e altas), "tarde" (douradas) ou "noite"
	/// (escuras, com o roxo da cidade). Vazio: mapa fechado (o Armazém), ou
	/// mapa que já traz as dele na cena (o Setor Zero).</summary>
	public string Clouds = "";

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
		// Os mapas de 19/11 (pedido do Jhon: "inclua esses mapas no game"): montados
		// com as peças dos kits "Low Poly FPS Map" Vol. 1, 2, 3, 5 e 6 (JustCreate),
		// com planta própria — o pacote em FBX vem sem os mapas montados
		// (tools/mapas/kits; docs/MAPAS.md).
		new()
		{
			Id = "cidade",
			Clouds = "dia",
			Name = "Cidade",
			Size = "Grande",
			Scene = "res://src/scenes/maps/cidade.tscn",
			Thumbnail = "res://src/assets/ui/mapas/cidade.png",
			Cover = "res://src/assets/ui/mapas/cidade_foto.png",
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
			Clouds = "dia_seco",
			Name = "Cidade do Deserto",
			Size = "Grande",
			Scene = "res://src/scenes/maps/deserto.tscn",
			Thumbnail = "res://src/assets/ui/mapas/deserto.png",
			Cover = "res://src/assets/ui/mapas/deserto_foto.png",
			Description = "Grande, 110 × 75 m, sol alto. Uma cidadela talhada dentro da muralha: três vielas de ponta a ponta ligadas por becos de 2,5 m, a rua da feira e a praça do chafariz no meio. Casas de laje com escada de pedra; por cima da muralha, só o lobo.",
			// (vento seco e pássaros longe; nenhum carro)
			RoomSize = 0.4f, RoomWet = 0.08f, Ambience = "ambiente_deserto", AmbienceDb = -14f,
			EventSound = "evento_passaros", EventEvery = new Vector2(45, 100),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "estacao",
			Clouds = "tarde",
			Name = "Estação de Trem",
			Size = "Grande",
			Scene = "res://src/scenes/maps/estacao.tscn",
			Thumbnail = "res://src/assets/ui/mapas/estacao.png",
			Cover = "res://src/assets/ui/mapas/estacao_foto.png",
			Description = "Grande, 120 × 95 m, fim de tarde, no centro da cidade. A rua e a praça, o prédio da estação com as torres de relógio — saguão, duas alas com corredor e salas —, duas plataformas, três linhas com trens parados e o pátio de carga. No centro, a passarela cruza as linhas por cima dos trens.",
			// (a cidade de dia, o zumbido dos trilhos, pássaros sob a cobertura; de vez em quando um trem passa ao longe)
			RoomSize = 0.6f, RoomWet = 0.2f, Ambience = "ambiente_estacao", AmbienceDb = -15f,
			EventSound = "evento_trem", EventEvery = new Vector2(70, 140),
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "obra",
			Clouds = "dia",
			Name = "Obra",
			Size = "Grande",
			Scene = "res://src/scenes/maps/obra.tscn",
			Thumbnail = "res://src/assets/ui/mapas/obra.png",
			Cover = "res://src/assets/ui/mapas/obra_foto.png",
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
			Clouds = "noite",
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

	/// <summary>
	/// O campo de treinamento (o Setor Zero, 26/11): um mapa do jogo, mas não de
	/// partida. Fica fora de <see cref="All"/> de propósito — tudo o que lista
	/// mapas (a escolha na sala, o servidor de partida, o painel, a semente do
	/// servidor) lê <see cref="All"/> e não o vê, sem precisar de filtro em cada
	/// lugar. Abre só pelo tutorial e por Jogar → Treinamento, pelo id
	/// (<see cref="Get"/>). Roda offline: não tem modo de sala, rota de ronda
	/// nem ponto de nascer por time (docs/MAPAS.md, "Setor Zero").
	/// </summary>
	public static readonly MapInfo Training = new()
	{
		Id = "setor_zero",
		Name = "Setor Zero",
		Size = "Grande",
		Scene = "res://src/scenes/maps/setor_zero.tscn",
		Thumbnail = "res://src/assets/ui/mapas/setor_zero.png",
		Cover = "res://src/assets/ui/mapas/setor_zero_foto.png",
		Description = "O centro de treinamento da WolfLeague, no alto de uma montanha, entre picos gelados: a Sala de Ativação, o corredor de calibragem, o Núcleo da Iris, a Linha de Fogo à beira do abismo, a Câmara de Metamorfose, a Arena da Matilha, a Rua Ignição e o Ponto de Retorno.",
		// (aberto e limpo, pouco eco; provisório: o fundo da Cidade, bem baixo — falta um vento de altitude, que não
		// foi gerado; o treino é de falar)
		RoomSize = 0.45f, RoomWet = 0.08f, Ambience = "ambiente_cidade", AmbienceDb = -18f,
		EventSound = "evento_passaros", EventEvery = new Vector2(70, 140),
		Modes = System.Array.Empty<GameMode>()
	};

	/// <summary>Um mapa pelo id: os da lista e o de treino. Id que o jogo não tem
	/// (sala antiga, mapa que saiu) cai no primeiro da lista.</summary>
	public static MapInfo Get(string id) =>
		All.FirstOrDefault(m => m.Id == id) ?? (id == Training.Id ? Training : All[0]);

	/// <summary>O mapa é o de treino (não entra em sala, rotação nem servidor).</summary>
	public static bool IsTraining(string id) => id == Training.Id;
}
