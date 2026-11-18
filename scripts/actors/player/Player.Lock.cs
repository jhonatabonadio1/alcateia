using Godot;
using System.Collections.Generic;

/// <summary>
/// O modo alvo do salto do lobo (pedido do Jhon, 17/11):
///
/// - Carregando o salto (botão direito seguro no chão), o **Tab** liga o
///   modo alvo — e não abre o placar. Os inimigos à vista ganham uma marca;
///   o mais perto do centro da tela fica destacado.
/// - Com o modo ligado, o **clique** trava no destacado: a mira segue ele
///   sozinha (o mouse não gira), o salto vai nos pés dele e o anel da mira
///   no chão some.
/// - **Tab de novo** desliga: destrava e o anel no chão volta.
/// - Soltar o salto, cancelar a carga ou morrer também desliga.
///
/// A trava gira o corpo e a cabeça, não o salto: o salto continua mirando
/// pela câmera, como sempre. Por isso vale igual na rede — o que vai no
/// pacote é para onde se olha.
/// </summary>
public partial class Player
{
	[ExportGroup("Lobo: alvo do salto")]
	/// <summary>O quanto um inimigo pode estar fora do centro da tela para
	/// ser destacado, em graus.</summary>
	[Export] public float LockAngle = 35f;

	/// <summary>Até onde se escolhe alvo, em metros. Mais longe que o salto
	/// (<see cref="Weapon.PounceMaxDistance"/>), o lobo salta até o alcance
	/// na direção dele.</summary>
	[Export] public float LockRange = 18f;

	/// <summary>Travado e o inimigo se afastando: a trava cai a esta
	/// distância.</summary>
	[Export] public float LockKeepRange = 24f;

	/// <summary>Quão rápido a mira vai até o travado (por segundo; maior,
	/// mais seco).</summary>
	[Export] public float LockTurnRate = 16f;

	/// <summary>O modo alvo está ligado (Tab durante a carga).</summary>
	public bool TargetMode { get; private set; }

	/// <summary>Os inimigos que podem ser escolhidos agora (modo alvo).</summary>
	public IReadOnlyList<Node3D> TargetCandidates => _candidates;

	/// <summary>O destacado: o que o clique trava.</summary>
	public Node3D TargetHover { get; private set; }

	/// <summary>O inimigo travado, ou nulo.</summary>
	public Node3D LockTarget { get; private set; }

	/// <summary>O Tab é do modo alvo, não do placar: carregando o salto ou
	/// com o modo ligado.</summary>
	public bool TabTaken => TargetMode || (_form.IsWolf && _weapon.IsChargingPounce);

	private readonly List<Node3D> _candidates = new();

	private void UpdateLock(double delta)
	{
		bool charging = _form.IsWolf && _weapon.IsChargingPounce && !_health.IsDead;
		if (!charging)
		{
			ExitTargetMode();
			return;
		}

		if (_intent.TargetToggle)
		{
			if (TargetMode) ExitTargetMode();
			else TargetMode = true;
		}
		if (!TargetMode) return;

		if (LockTarget != null && !StillLocked(LockTarget)) LockTarget = null;

		FindCandidates();

		// Clique: trava no destacado.
		if (_intent.Attack && TargetHover != null) LockTarget = TargetHover;

		if (LockTarget != null) TurnToLock(delta);
	}

	private void ExitTargetMode()
	{
		TargetMode = false;
		LockTarget = null;
		TargetHover = null;
		_candidates.Clear();
	}

	/// <summary>Os inimigos vivos e à vista dentro do alcance; o destacado é
	/// o mais perto do centro da tela (travado, é o próprio travado).</summary>
	private void FindCandidates()
	{
		_candidates.Clear();
		TargetHover = LockTarget;
		if (AimCamera is not Camera3D camera) return;

		Vector3 eye = camera.GlobalPosition;
		Vector3 forward = -camera.GlobalBasis.Z;
		float bestAngle = LockAngle;

		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node == this || node is not Node3D body || !IsEnemy(body)) continue;

				Vector3 to = body.GlobalPosition - eye;
				if (to.Length() > LockRange || !InSight(eye, body)) continue;
				_candidates.Add(body);

				if (LockTarget != null) continue;
				float angle = Mathf.RadToDeg(forward.AngleTo(to));
				if (angle >= bestAngle) continue;
				bestAngle = angle;
				TargetHover = body;
			}
		}
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
