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
	public GameMode[] Modes;
}

/// <summary>Os mapas jogáveis. Hoje, o Hangar. Mapa novo entra aqui e já
/// aparece na sala e na lista.</summary>
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
		}
	};

	public static MapInfo Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? All[0];
}
