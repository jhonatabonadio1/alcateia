using Godot;
using System.Collections.Generic;

/// <summary>
/// O modo alvo do especial do lobo (pedido do Jhon, 18/11).
///
/// Q arma o especial (<see cref="Weapon.SpecialArmed"/>) e abre o modo
/// alvo: o lobo anda livre e a mira continua do mouse; os inimigos à
/// vista e no alcance ganham um losango (<see cref="TargetCandidates"/>),
/// e o mais perto do centro da tela, até <see cref="LockAngle"/> graus,
/// fica travado (<see cref="LockTarget"/>, cantos vermelhos). A trava
/// segura o mesmo alvo até <see cref="LockKeepAngle"/> graus, para não
/// pular de um para outro com dois juntos. O clique salta em cima do
/// travado — e o voo o persegue (<see cref="Weapon.TryPounce"/>). Sem
/// ninguém travado, o salto vai para onde a mira toca o mapa.
/// </summary>
public partial class Player
{
	[ExportGroup("Lobo: alvo do especial")]
	/// <summary>O quanto um inimigo pode estar fora do centro da tela para
	/// ser travado, em graus.</summary>
	[Export] public float LockAngle = 20f;

	/// <summary>Já travado, até onde ele pode sair do centro antes de a
	/// trava passar para outro (ou soltar), em graus.</summary>
	[Export] public float LockKeepAngle = 32f;

	/// <summary>Até onde o especial pega alvo, em metros (o alcance do
	/// salto, <see cref="Weapon.PounceMaxDistance"/>, e uma folga).</summary>
	[Export] public float LockRange = 14f;

	/// <summary>Até onde o losango aparece, em graus do centro da tela.</summary>
	[Export] public float CandidateAngle = 60f;

	/// <summary>O modo alvo está aberto: o especial está armado.</summary>
	public bool TargetMode => _weapon != null && _weapon.SpecialArmed && _form.IsWolf;

	/// <summary>Os inimigos que podem ser travados agora.</summary>
	public IReadOnlyList<Node3D> TargetCandidates => _candidates;

	public Node3D TargetHover => null;

	/// <summary>O inimigo em que o especial vai cair; nulo fora do modo
	/// alvo ou sem ninguém perto do centro.</summary>
	public Node3D LockTarget { get; private set; }

	/// <summary>O Tab é sempre do placar.</summary>
	public bool TabTaken => false;

	private readonly List<Node3D> _candidates = new();

	private void UpdateLock(double delta)
	{
		_candidates.Clear();
		if (!TargetMode || AimCamera is not Camera3D camera)
		{
			LockTarget = null;
			return;
		}

		Vector3 eye = camera.GlobalPosition;
		Vector3 forward = -camera.GlobalBasis.Z;
		Node3D best = null;
		float bestAngle = LockAngle;
		bool keep = false;

		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node == this || node is not Node3D body || !IsEnemy(body)) continue;
				if (body.GlobalPosition.DistanceTo(GlobalPosition) > LockRange) continue;

				float angle = Mathf.RadToDeg(forward.AngleTo(body.GlobalPosition - eye));
				if (angle > CandidateAngle || !InSight(eye, body)) continue;
				_candidates.Add(body);

				if (body == LockTarget && angle <= LockKeepAngle) keep = true;
				if (angle < bestAngle)
				{
					bestAngle = angle;
					best = body;
				}
			}
		}

		if (!keep) LockTarget = best;
	}

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
}
