using Godot;
using System.Collections.Generic;

/// <summary>
/// O mundo de uma partida dentro do servidor de partida (modo
/// autoritativo, decisão 0011): o mapa e os corpos dos jogadores.
///
/// Cada partida tem o próprio mundo de física (um <see cref="SubViewport"/>
/// com mundo próprio): o servidor joga várias ao mesmo tempo, e um corpo
/// de uma não pode esbarrar no de outra. Nada se desenha
/// (<see cref="SubViewport.RenderTargetUpdateMode"/> desligado).
///
/// Do mapa fica só o que tem colisão: malhas, luzes, sondas, partículas,
/// sons, céu, marcadores, a malha de navegação (que seria assada de novo
/// a cada partida) e as rotas dos bots saem antes de entrar na árvore. O
/// Vila tem 1.600 malhas e 200 corpos sólidos — o servidor precisa só
/// dos 200.
/// </summary>
public partial class MatchWorld : SubViewport
{
	public string MapId { get; }
	public int StrippedNodes { get; private set; }

	public MatchWorld() : this(Maps.All[0].Id) { }

	public MatchWorld(string mapId)
	{
		MapId = mapId;
		Name = "Mundo";
		OwnWorld3D = true;
		Disable3D = false;
		Size = new Vector2I(2, 2);
		RenderTargetUpdateMode = UpdateMode.Disabled;
		AudioListenerEnable3D = false;
	}

	public override void _Ready()
	{
		MapInfo info = Maps.Get(MapId);
		var scene = ResourceLoader.Load<PackedScene>(info.Scene) ?? ResourceLoader.Load<PackedScene>(Maps.All[0].Scene);
		Node map = scene.Instantiate();
		StrippedNodes = Strip(map);
		AddChild(map);
	}

	/// <summary>Um corpo de jogador movido pela rede, ainda fora do mundo
	/// (sem colisão) até o jogador dizer onde nasceu.</summary>
	public Player SpawnBody(string name, int team)
	{
		var body = GD.Load<PackedScene>("res://scenes/player.tscn").Instantiate<Player>();
		body.Driven = true;
		body.Name = name;

		// A réplica automática do Godot não é usada: quem conta o estado é
		// o estado do mundo (SnapshotWire). Fora antes de entrar na árvore,
		// senão ela tenta replicar pela ligação do servidor de partida.
		body.GetNodeOrNull("MultiplayerSynchronizer")?.Free();
		body.RemoveFromGroup("player");
		body.GetNode<Health>("Health").Team = team;
		AddChild(body);
		body.GlobalPosition = new Vector3(0, -500, 0);
		return body;
	}

	/// <summary>Tira do mapa tudo o que não colide. Devolve quantos nós saíram.</summary>
	public static int Strip(Node root)
	{
		int removed = 0;
		var doomed = new List<Node>();
		Collect(root, doomed);
		foreach (Node node in doomed)
		{
			node.GetParent()?.RemoveChild(node);
			node.Free();
			removed++;
		}
		return removed;
	}

	private static void Collect(Node node, List<Node> doomed)
	{
		foreach (Node child in node.GetChildren())
		{
			// Sem nada sólido dentro: não serve ao servidor.
			if (!HoldsCollision(child))
			{
				doomed.Add(child);
				continue;
			}

			// Malha com corpo sólido dentro: fica a malha (vazia), some o desenho.
			if (child is MeshInstance3D mesh)
			{
				mesh.Mesh = null;
				mesh.Visible = false;
			}
			Collect(child, doomed);
		}
	}

	private static bool HoldsCollision(Node n)
	{
		if (n is CollisionObject3D or CollisionShape3D or CollisionPolygon3D) return true;
		foreach (Node child in n.GetChildren())
			if (HoldsCollision(child)) return true;
		return false;
	}
}
