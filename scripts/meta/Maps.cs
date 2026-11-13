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
			Modes = new[] { GameMode.Equipe, GameMode.MataMata }
		}
	};

	public static MapInfo Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? All[0];
}
