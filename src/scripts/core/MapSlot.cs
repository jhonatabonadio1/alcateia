using Godot;

/// <summary>
/// O lugar do mapa na cena da partida: carrega o mapa que a sala escolheu
/// (<see cref="RoomConfig"/>, <see cref="Maps"/>) como filho deste nó.
///
/// No _EnterTree, e não no _Ready: o mapa precisa estar na árvore antes
/// de quem depende dele ficar pronto — os pontos de nascimento se
/// registram no _Ready deles, e o <see cref="MatchSetup"/> distribui os
/// jogadores no dele, que vem depois.
///
/// O mapa pode trazer o próprio céu ("Ambiente") e o próprio sol ("Sol").
/// Quando traz, os padrões da cena da partida saem: dois ambientes na
/// mesma cena brigam, e dois sóis somam.
/// </summary>
public partial class MapSlot : Node3D
{
	/// <summary>Mapa a carregar em vez do da sala (para testar um mapa
	/// direto no editor). Vazio usa o da sala.</summary>
	[Export] public string MapId = "";

	/// <summary>O mapa carregado.</summary>
	public MapInfo Info { get; private set; }

	public override void _EnterTree()
	{
		if (GetChildCount() > 0) return;

		Info = Maps.Get(string.IsNullOrEmpty(MapId) ? RoomConfig.Rules.Map : MapId);
		var scene = ResourceLoader.Load<PackedScene>(Info.Scene);
		if (scene == null)
		{
			GD.PushError($"MapSlot: map '{Info.Id}' not found at {Info.Scene}; loading the first one in the list.");
			Info = Maps.All[0];
			scene = ResourceLoader.Load<PackedScene>(Info.Scene);
		}

		Node map = scene.Instantiate();
		AddChild(map);
		// As nuvens do céu, pelo clima que o mapa declara (os mapas a céu aberto; o Setor Zero já traz as dele).
		SkyClouds.AddTo(map, Info.Clouds);

		Node parent = GetParent();
		// A sombra de apoio dos personagens segue o sol que vale: o do mapa, ou o da cena da partida.
		GroundingShadow.AddTo(map, map.GetNodeOrNull<DirectionalLight3D>("Sol")
			?? (map.HasNode("Ambiente") ? null : parent?.GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D")));
		if (parent == null) return;

		if (map.HasNode("Ambiente"))
			parent.GetNodeOrNull("WorldEnvironment")?.QueueFree();
		if (map.HasNode("Sol") || map.HasNode("Ambiente"))
			parent.GetNodeOrNull("DirectionalLight3D")?.QueueFree();
	}
}
