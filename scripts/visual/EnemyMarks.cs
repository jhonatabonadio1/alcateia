using Godot;

/// <summary>
/// Inimigos com a borda levemente vermelha (<see cref="Silhouette.Enemy"/>),
/// para separar de aliado à primeira vista (decisão do Jhon). Só no seu
/// cliente, só no time contrário ao seu, e sem atravessar parede.
///
/// Varre os corpos de tempo em tempo: o lobo e a arma na mão são malhas
/// que aparecem depois do corpo, e o time pode ser trocado entre partidas.
/// Uma marca que outro dono pôs (a do matador) não é tocada.
/// </summary>
public partial class EnemyMarks : Node
{
	private Health _local;
	private double _timer;

	public override void _Process(double delta)
	{
		_timer -= delta;
		if (_timer > 0) return;
		_timer = 0.5;

		if (_local == null || !IsInstanceValid(_local))
			_local = Groups.LocalPlayer(GetTree())?.GetNodeOrNull<Health>("Health");
		if (_local == null) return;

		foreach (string group in Groups.Fighters)
		{
			foreach (Node body in GetTree().GetNodesInGroup(group))
			{
				if (body.GetNodeOrNull<Health>("Health") is not Health health || health == _local) continue;
				bool enemy = health.Team != _local.Team;

				foreach (Node node in body.FindChildren("*", nameof(MeshInstance3D), true, false))
				{
					if (node is not MeshInstance3D mesh) continue;

					if (enemy && mesh.MaterialOverlay == null)
						mesh.MaterialOverlay = Silhouette.Enemy;
					else if (!enemy && mesh.MaterialOverlay == Silhouette.Enemy)
						mesh.MaterialOverlay = null;
				}
			}
		}
	}
}
