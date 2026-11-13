using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class MapInfo
{
	public string Id;
	public string Name;
	public string Scene;
	/// <summary>Miniatura vista de cima, gerada por tools/gerar_miniaturas.</summary>
	public string Thumbnail;
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
/// O Hangar nasce de tools/gerar_hangar.py; os outros, de
/// tools/gerar_mapas.py.</summary>
public static class Maps
{
	public static readonly List<MapInfo> All = new()
	{
		new()
		{
			Id = "hangar",
			Name = "Hangar",
			Scene = "res://scenes/maps/hangar.tscn",
			Thumbnail = "res://assets/ui/mapas/hangar.png",
			Description = "Simétrico, 84 × 48 m. Heliponto no centro, quatro blocos ligados por pontes e becos estreitos onde o lobo sobe correndo pela parede.",
			RoomSize = 0.75f, RoomWet = 0.16f, Ambience = "ambiente_hangar", AmbienceDb = -26f,
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		},
		new()
		{
			Id = "metro",
			Name = "Metrô",
			Size = "Pequeno",
			Scene = "res://scenes/maps/metro.tscn",
			Thumbnail = "res://assets/ui/mapas/metro.png",
			Description = "Pequeno, 48 × 30 m, fechado. Estação de metrô com um trem parado no meio: plataformas, galerias a 5 m e corredores de serviço onde o lobo sobe pela parede.",
			RoomSize = 0.7f, RoomWet = 0.24f, Ambience = "ambiente_metro", AmbienceDb = -16f,
			EventSound = "metro_trem", EventEvery = new Vector2(70, 140),
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
