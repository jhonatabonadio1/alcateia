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

	/// <summary>Os pontos de nascimento do mapa, com o time de cada um (0:
	/// qualquer time), guardados antes de os marcadores saírem: o servidor
	/// confere onde o jogador diz que renasceu (decisão 0016).</summary>
	public List<(int Team, Vector3 Point)> Spawns { get; } = new();

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
		CollectSpawns(map, Transform3D.Identity);
		StrippedNodes = Strip(map);
		AddChild(map);

		// O que o servidor tem para colidir. No servidor exportado (com a
		// arte trocada por cascas) os números têm de bater com os do jogo:
		// se a colisão tivesse sumido junto com o desenho, apareceria aqui.
		var (shapes, faces) = CountCollision(map);
		GD.Print($"Mundo {MapId}: {shapes} formas de colisão, {faces} triângulos, {Spawns.Count} pontos de nascimento");
	}

	/// <summary>Quantas formas de colisão o mapa tem e quantos triângulos
	/// há nas que são malha.</summary>
	public static (int Shapes, int Faces) CountCollision(Node root)
	{
		int shapes = 0, faces = 0;
		foreach (Node node in root.FindChildren("*", nameof(CollisionShape3D), true, false))
		{
			if (node is not CollisionShape3D { Shape: Shape3D shape }) continue;
			shapes++;
			if (shape is ConcavePolygonShape3D concave) faces += concave.Data.Length / 3;
			else if (shape is ConvexPolygonShape3D convex) faces += convex.Points.Length;
		}
		return (shapes, faces);
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

	/// <summary>Os pontos que servem a um time (os do time e os de qualquer um).</summary>
	public IEnumerable<Vector3> SpawnsFor(int team)
	{
		foreach (var (t, point) in Spawns)
			if (t == 0 || t == team) yield return point;
	}

	/// <summary>O espaço de física desta partida (só o que colide: o mapa e os corpos).</summary>
	public PhysicsDirectSpaceState3D Space => FindWorld3D()?.DirectSpaceState;

	private void CollectSpawns(Node node, Transform3D parent)
	{
		Transform3D here = node is Node3D n3 ? parent * n3.Transform : parent;
		if (node is SpawnPoints set)
		{
			foreach (Node child in set.GetChildren())
				if (child is Node3D marker) Spawns.Add((set.Team, (here * marker.Transform).Origin));
			return;
		}
		foreach (Node child in node.GetChildren()) CollectSpawns(child, here);
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
