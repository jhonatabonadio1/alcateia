using Godot;

/// <summary>
/// A trava do salto do lobo (pedido do Jhon, 17/11): carregando o salto
/// (botão direito seguro no chão), segurar o botão do meio trava no
/// inimigo mais perto do centro da tela. Travado, a mira segue o inimigo
/// sozinha (o mouse não gira) e o salto vai nos pés dele; a tela marca
/// quem está travado (<see cref="LockMark"/>). Soltar o botão do meio, ou
/// soltar o salto, destrava.
///
/// A trava gira o corpo e a cabeça, não o salto: o salto continua mirando
/// pela câmera, como sempre. Por isso ela vale igual na rede — o que vai
/// no pacote é para onde se olha.
/// </summary>
public partial class Player
{
	[ExportGroup("Lobo: trava do salto")]
	/// <summary>O quanto o inimigo pode estar fora do centro da tela para
	/// ser travado, em graus.</summary>
	[Export] public float LockAngle = 30f;

	/// <summary>Até onde se trava, em metros. Mais longe que o salto
	/// (<see cref="Weapon.PounceMaxDistance"/>), o lobo salta até o alcance
	/// na direção dele.</summary>
	[Export] public float LockRange = 18f;

	/// <summary>Travado e o inimigo se afastando: a trava cai a esta
	/// distância.</summary>
	[Export] public float LockKeepRange = 24f;

	/// <summary>Quão rápido a mira vai até o travado (por segundo; maior,
	/// mais seco).</summary>
	[Export] public float LockTurnRate = 16f;

	/// <summary>O inimigo travado, ou nulo.</summary>
	public Node3D LockTarget { get; private set; }

	private void UpdateLock(double delta)
	{
		bool wants = _intent.LockHeld && _form.IsWolf && _weapon.IsChargingPounce && !_health.IsDead;
		if (!wants)
		{
			LockTarget = null;
			return;
		}

		if (LockTarget != null && !StillLocked(LockTarget)) LockTarget = null;

		// Apertou sem ninguém na mira: continua procurando enquanto segura.
		LockTarget ??= PickLock();
		if (LockTarget != null) TurnToLock(delta);
	}

	/// <summary>O inimigo vivo, à vista, mais perto do centro da tela.</summary>
	private Node3D PickLock()
	{
		if (AimCamera is not Camera3D camera) return null;

		Vector3 eye = camera.GlobalPosition;
		Vector3 forward = -camera.GlobalBasis.Z;
		Node3D best = null;
		float bestAngle = LockAngle;

		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node == this || node is not Node3D body || !IsEnemy(body)) continue;

				Vector3 to = body.GlobalPosition - eye;
				if (to.Length() > LockRange) continue;

				float angle = Mathf.RadToDeg(forward.AngleTo(to));
				if (angle >= bestAngle || !InSight(eye, body)) continue;

				bestAngle = angle;
				best = body;
			}
		}

		return best;
	}

	private bool StillLocked(Node3D body) =>
		IsInstanceValid(body) && body.IsInsideTree() && IsEnemy(body)
		&& body.GlobalPosition.DistanceTo(GlobalPosition) <= LockKeepRange;

	private bool IsEnemy(Node3D body)
	{
		if (body is Bot { RemoteAbsent: true }) return false;
		var health = body.GetNodeOrNull<Health>("Health");
		return health != null && !health.IsDead && health.Team != _health.Team;
	}

	private bool InSight(Vector3 eye, Node3D body)
	{
		var query = PhysicsRayQueryParameters3D.Create(eye, body.GlobalPosition);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count == 0 || hit["collider"].Obj == body;
	}

	/// <summary>Gira o corpo (para os lados) e a cabeça (para cima e para
	/// baixo) até o centro da tela ficar no travado.</summary>
	private void TurnToLock(double delta)
	{
		if (AimCamera is not Camera3D camera) return;

		Vector3 to = LockTarget.GlobalPosition - camera.GlobalPosition;
		if (to.LengthSquared() < 0.01f) return;

		float t = 1f - Mathf.Exp(-LockTurnRate * (float)delta);
		float yaw = Mathf.Atan2(-to.X, -to.Z);
		Rotation = Rotation with { Y = Mathf.LerpAngle(Rotation.Y, yaw, t) };

		float flat = new Vector2(to.X, to.Z).Length();
		float pitch = Mathf.Clamp(Mathf.Atan2(to.Y, flat), Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
		_head.Rotation = _head.Rotation with { X = Mathf.Lerp(_head.Rotation.X, pitch, t) };
	}
}
