using Godot;
using System.Collections.Generic;

/// <summary>
/// As bordas dos corpos para quem joga (pedido do Jhon, 18/11): **aliado
/// com borda branca suave** (vista também através das paredes, para achar
/// o time) e **inimigo com borda vermelha suave e leve** (só à vista).
/// Só na tela de quem joga, nunca no próprio corpo.
///
/// A borda é a segunda passada da malha (<c>MaterialOverlay</c>,
/// <see cref="Silhouette.Ally"/> e <see cref="Silhouette.Enemy"/>). Quem já
/// usa essa passada tem preferência e não é coberto: a silhueta vermelha
/// de quem te matou (<see cref="KillCam"/>) e o brilho do lobo
/// (<see cref="WolfFx"/>). Corpo morto perde a borda.
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
			Material now = mesh.MaterialOverlay;
			// Só mexe na passada vazia ou numa borda nossa.
			if (now != null && now != Silhouette.Ally && now != Silhouette.Enemy) continue;
			if (now != want) mesh.MaterialOverlay = want;
		}
	}
}
