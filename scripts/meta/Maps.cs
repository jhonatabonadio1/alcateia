using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class MapInfo
{
	public string Id;
	public string Name;
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
	public string Description;
	/// <summary>Pequeno, Médio ou Grande.</summary>
	public string Size = "Médio";
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
			RoomSize = 0.75f, RoomWet = 0.35f, Ambience = "ambiente_hangar", AmbienceDb = -14f,
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
			RoomSize = 0.6f, RoomWet = 0.3f, Ambience = "ambiente_hangar", AmbienceDb = -14f,
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
