using Godot;
using System.Collections.Generic;

/// <summary>
/// O alvo do especial do lobo.
///
/// O especial sai num toque (Q; decisão do Jhon, 18/11), então não há mais
/// a carga em que se escolhia o alvo com o Tab (o "modo alvo" de 17/11).
/// No lugar: **se há um inimigo na mira** quando o Q é apertado — à
/// vista, dentro do alcance e a poucos graus do centro da tela
/// (<see cref="PounceAssistAngle"/>) —, o salto vai em cima dele e ele é o
/// alvo principal do golpe. Sem ninguém na mira, o salto vai para onde a
/// mira toca o mapa, como sempre.
///
/// O alvo vale só no instante do disparo: a arma guarda quem é
/// (<see cref="Weapon.TryPounce"/>) e a mira volta a ser do mouse.
/// </summary>
public partial class Player
{
	[ExportGroup("Lobo: alvo do especial")]
	/// <summary>O quanto um inimigo pode estar fora do centro da tela para
	/// o especial ir nele, em graus. Pequeno de propósito: é para quem
	/// mirou no inimigo e errou por pouco, não para mirar sozinho.</summary>
	[Export] public float PounceAssistAngle = 8f;

	/// <summary>Até onde o especial pega alvo, em metros (o alcance do
	/// salto, <see cref="Weapon.PounceMaxDistance"/>, e uma folga).</summary>
	[Export] public float LockRange = 14f;

	/// <summary>Travado e o inimigo se afastando: a trava cai a esta
	/// distância.</summary>
	[Export] public float LockKeepRange = 24f;

	/// <summary>O modo alvo do Tab não existe mais (o especial é num
	/// toque); fica a propriedade, sempre falsa, para quem a consulta.</summary>
	public bool TargetMode => false;

	/// <summary>Sem modo alvo, não há lista de candidatos.</summary>
	public IReadOnlyList<Node3D> TargetCandidates => _candidates;

	public Node3D TargetHover => null;

	/// <summary>O inimigo em que o especial vai cair, só no instante do
	/// disparo; fora dele, nulo.</summary>
	public Node3D LockTarget { get; private set; }

	/// <summary>O Tab é sempre do placar.</summary>
	public bool TabTaken => false;

	private readonly List<Node3D> _candidates = new();

	private void UpdateLock(double delta)
	{
		if (LockTarget != null && !StillLocked(LockTarget)) LockTarget = null;
	}

	/// <summary>Escolhe o alvo do especial: o inimigo à vista mais perto do
	/// centro da tela, dentro do ângulo e do alcance. Fica em
	/// <see cref="LockTarget"/> (nulo se não há ninguém na mira).</summary>
	private void AimPounce()
	{
		LockTarget = null;
		if (AimCamera is not Camera3D camera) return;

		Vector3 eye = camera.GlobalPosition;
		Vector3 forward = -camera.GlobalBasis.Z;
		float bestAngle = PounceAssistAngle;

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
				LockTarget = body;
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
}
