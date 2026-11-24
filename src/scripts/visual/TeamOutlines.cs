using Godot;
using System.Collections.Generic;

/// <summary>
/// As bordas dos corpos para quem joga (pedidos do Jhon, 18 e 19/11):
/// **aliado com borda branca**, vista em qualquer parte do mapa, e
/// **inimigo com borda vermelha suave**, só à vista — humano e lobo. Só a
/// borda, nunca por cima da arma na mão (<see cref="Silhouette"/>). Só na
/// tela de quem joga, nunca no próprio corpo.
///
/// A borda vai pela passada de cima da malha junto com o brilho do lobo
/// (<see cref="BodyOverlay"/>); a de quem te matou (<see cref="KillCam"/>)
/// tem preferência. Corpo morto perde a borda.
/// </summary>
public partial class TeamOutlines : Node
{
	/// <summary>De quanto em quanto tempo confere os corpos (s).</summary>
	[Export] public double Every = 0.25;

	private double _left;
	private readonly List<MeshInstance3D> _scratch = new();

	public override void _Process(double delta)
	{
		_left -= delta;
		if (_left > 0) return;
		_left = Every;

		Node me = Groups.LocalPlayer(GetTree());
		int team = me?.GetNodeOrNull<Health>("Health")?.Team ?? RoomConfig.PlayerTeam;

		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node == me || node is not Node3D body || body.GetNodeOrNull<Health>("Health") is not Health health) continue;
				Material want = health.IsDead ? null : health.Team == team ? Silhouette.Ally : Silhouette.Enemy;
				Apply(body, want);
			}
	}

	private void Apply(Node3D body, Material want)
	{
		_scratch.Clear();
		foreach (string part in new[] { "Human", "WolfVisual" })
			if (body.GetNodeOrNull(part) is Node visual)
				foreach (Node node in visual.FindChildren("*", nameof(MeshInstance3D), true, false))
					_scratch.Add((MeshInstance3D)node);

		foreach (MeshInstance3D mesh in _scratch)
		{
			if (mesh.IsInGroup(EnemyMarks.MarkGroup)) continue;
			// A de quem te matou tem preferência.
			Material now = BodyOverlay.Edge(mesh);
			if (now == Silhouette.Killer) continue;
			if (now != want) BodyOverlay.SetEdge(mesh, want);
		}
	}
}
