using Godot;
using System.Collections.Generic;

/// <summary>
/// As garras do lobo (clique esquerdo): o combo, o passo de cada golpe e
/// o relógio dos golpes — o dano sai no instante medido na animação
/// (WolfMoves), e o próximo golpe espera ela acabar.
/// </summary>
public partial class Weapon
{
	/// <summary>Dano extra da patada contra um humano. O lobo é feito
	/// para caçar gente, não para duelar com outro lobo.</summary>
	[ExportGroup("Lobo: garras")]
	[Export] public float ClawVsHumanBonus = 1.6f;

	/// <summary>Raio do volume de acerto da garra.</summary>
	[Export] public float ClawRadius = 1.15f;

	/// <summary>Quanto à frente do corpo o volume nasce.</summary>
	[Export] public float ClawReach = 1.5f;

	/// <summary>Multiplicador de dano de cada golpe do combo.</summary>
	[Export] public float[] ComboDamage = { 1f, 1f, 1.5f };

	/// <summary>Tempo sem atacar, depois do fim do golpe, até o combo
	/// voltar ao primeiro. O intervalo entre golpes é a própria animação
	/// (<see cref="WolfMoves"/>).</summary>
	[Export] public float ComboReset = 1.1f;

	/// <summary>Empurrão para frente a cada golpe. É o que faz mira
	/// ruim ainda conectar — o golpe leva o lobo até o alvo.</summary>
	[Export] public float ClawLunge = 6.5f;

	/// <summary>Clique na garrada guardado, em segundos: clicou com o golpe
	/// anterior ainda no fim, o próximo sai no quadro em que ele acaba. Sem
	/// isso o clique dado um pouco cedo se perdia e o lobo parecia não
	/// responder.</summary>
	[Export] public float AttackBuffer = 0.3f;

	private int _comboIndex;

	/// <summary>Próxima garrada com o outro braço.</summary>
	private bool _nextPunch;

	/// <summary>Tempo que falta da animação do golpe em curso.</summary>
	private double _strikeLeft;

	/// <summary>Muda a cada golpe e na morte: dano agendado de um golpe
	/// que não vale mais (morreu, voltou a humano) não sai.</summary>
	private ulong _strikeId;
	private double _comboTimer;

	/// <summary>Tempo que o lobo ainda fica preso no lugar: o salto
	/// inteiro, e a aterrissagem do bote.</summary>
	private double _rootLeft;
	private bool _attackQueued;
	private double _stepLeft;
	private double _stepTotal = 1;
	private float _stepPeak;
	private Vector3 _stepForward;

	/// <summary>No meio de um golpe do lobo. A animação não é cortada:
	/// nem por outro golpe nem pela volta a humano.</summary>
	public bool IsStriking => _form != null && _form.IsWolf && (_strikeLeft > 0 || _diving);

	/// <summary>Preso no lugar: o salto no chão, ou a aterrissagem do bote.
	/// O corpo não anda nem pula, só gira a mira.</summary>
	public bool IsRooted => _form != null && _form.IsWolf && _rootLeft > 0;

	/// <summary>No passo da garrada: o corpo anda nesta velocidade, por
	/// cima do que o jogador estiver apertando.</summary>
	public bool IsStepping => _form != null && _form.IsWolf && _stepLeft > 0;

	/// <summary>Velocidade do passo neste instante: sobe e desce em
	/// sino, sem degrau no começo nem no fim — o degrau era o que jogava
	/// as pernas na pose de correr de uma vez.</summary>
	public Vector3 StepVelocity =>
		_stepForward * (_stepPeak * Mathf.Sin(Mathf.Pi * (float)(1.0 - _stepLeft / _stepTotal)));

	/// <summary>Abaixo disto o lobo conta como parado ao bater.</summary>
	private const float StandingSpeed = 1.5f;

	/// <summary>
	/// Garrada do clique esquerdo. Os braços alternam — o "swiping" e o
	/// "punch" — e o combo continua contando: o terceiro golpe seguido
	/// machuca mais.
	/// </summary>
	private void WolfAttack()
	{
		if (_comboTimer <= 0) _comboIndex = 0;

		float multiplier = ComboDamage[Mathf.Min(_comboIndex, ComboDamage.Length - 1)];
		_comboIndex = (_comboIndex + 1) % ComboDamage.Length;

		WolfMove move = _nextPunch ? WolfMove.Punch : WolfMove.Claw;
		_nextPunch = !_nextPunch;

		// O combo zera se passar ComboReset depois do fim do golpe.
		_comboTimer = WolfMoves.Duration(move) + ComboReset;

		_attackQueued = false;

		// Parado, o corpo dá o passo do golpe, em sino: as pernas andam
		// um passo junto com o braço. Correndo, o empurrão de sempre leva
		// o lobo até o alvo — as pernas já estão correndo. Só o jogador: o
		// bot anda pela própria cabeça.
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		bool standing = _body is Player && _body.IsOnFloor() && flat.Length() < StandingSpeed;

		ulong id = StartMove(move, CooldownClaw, 0f, () =>
		{
			if (!standing) Lunge();
			Claw(multiplier, ClawReach, ClawRadius, false);
		});

		var (delay, duration, distance) = WolfMoves.StepOf(move);
		if (standing && distance > 0f)
			After(delay, id, () => BeginStep(duration, distance));
	}

	/// <summary>O passo da garrada e o avanço do salto: anda
	/// <paramref name="distance"/> em <paramref name="duration"/>, para a
	/// frente do corpo ou na <paramref name="direction"/> dada. O pico do
	/// sino é π/2 da média, para a distância total ser a mesma.</summary>
	private void BeginStep(float duration, float distance, Vector3? direction = null)
	{
		Vector3 forward = direction ?? -_body.GlobalTransform.Basis.Z;
		forward.Y = 0;
		_stepForward = forward.Normalized();
		_stepTotal = Mathf.Max(duration, 0.01f);
		_stepPeak = Mathf.Pi / 2f * distance / (float)_stepTotal;
		_stepLeft = _stepTotal;
	}

	/// <summary>
	/// Começa um golpe do lobo. A animação dura
	/// <see cref="WolfMoves.Duration"/> e o próximo golpe só sai depois
	/// dela — golpe não corta golpe. O dano (<paramref name="impact"/>)
	/// sai no instante em que a garra chega na animação, não no clique.
	/// </summary>
	/// <param name="elapsed">Segundos do golpe que já tocaram (a carga do
	/// salto): o clipe já começou, e o dano e o fim contam do resto.</param>
	private ulong StartMove(WolfMove move, int kind, float minCooldown, System.Action impact, float elapsed = 0f)
	{
		float duration = WolfMoves.Duration(move) - elapsed;
		float cooldown = Mathf.Max(duration, minCooldown);

		_cooldown = cooldown;
		_strikeLeft = duration;
		ulong id = ++_strikeId;

		if (elapsed <= 0f) EmitSignal(SignalName.WolfMoveStarted, (int)move);
		EmitSignal(SignalName.AttackCooldown, kind, cooldown);

		After(Mathf.Max(WolfMoves.ImpactDelay(move) - elapsed, 0f), id, () =>
		{
			// O som do golpe sai junto com o golpe.
			EmitSignal(SignalName.Fired);
			impact();
		});

		return id;
	}

	/// <summary>Roda <paramref name="action"/> depois de
	/// <paramref name="delay"/> segundos, se o golpe ainda valer.
	///
	/// Fila própria, contada no passo de física, e não um timer da
	/// árvore por golpe: um bot de lobo bate o tempo todo, e criar um
	/// objeto do Godot a cada golpe acumulava objetos do lado C# que
	/// derrubavam o jogo ao fechar.</summary>
	private void After(float delay, ulong id, System.Action action) =>
		_pending.Add(new PendingHit(delay, id, action));
	private readonly record struct PendingHit(double Left, ulong Id, System.Action Action);
	private readonly List<PendingHit> _pending = new();

	private void RunPending(double delta)
	{
		for (int i = _pending.Count - 1; i >= 0; i--)
		{
			PendingHit hit = _pending[i];
			double left = hit.Left - delta;

			if (left > 0)
			{
				_pending[i] = hit with { Left = left };
				continue;
			}

			_pending.RemoveAt(i);
			if (hit.Id != _strikeId || _health.IsDead || !_form.IsWolf) continue;
			hit.Action();
		}
	}

	/// <summary>
	/// Empurra o lobo para frente durante o golpe. Sem isso o combo
	/// deixa o jogador parado batendo no ar enquanto o alvo anda.
	/// </summary>
	private void Lunge()
	{
		if (_body is not Player player) return;

		Vector3 forward = -_body.GlobalTransform.Basis.Z;
		Vector3 velocity = player.Velocity;

		velocity.X = forward.X * ClawLunge;
		velocity.Z = forward.Z * ClawLunge;

		player.Velocity = velocity;
	}

	/// <summary>
	/// Golpe de garra: um volume curto à frente do corpo, não um raio
	/// a partir da câmera. Pega quem estiver perto da garra.
	///
	/// <paramref name="aerial"/>: o golpe veio de uma queda alta e vale
	/// o bônus aéreo mesmo já no chão — o bote bate quando aterrissa.
	/// </summary>
	private void Claw(float multiplier, float reach, float radius, bool aerial, Vector3? facing = null)
	{
		WolfBreed breed = _form.Breed;

		// O volume nasce à frente do CORPO, na altura do peito. Preso
		// ao corpo e não ao olhar, é o que dá à garra um alcance que
		// varia com a postura em vez de seguir a mira. O salto passa a
		// direção em que voou: ele pode ter curvado atrás do alvo.
		Vector3 forward = facing ?? -_body.GlobalTransform.Basis.Z;
		Vector3 center = _body.GlobalPosition + forward * reach + Vector3.Up * 0.2f;

		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = radius },
			Transform = new Transform3D(Basis.Identity, center),
			CollideWithBodies = true,
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() }
		};

		var hits = _body.GetWorld3D().DirectSpaceState.IntersectShape(query, 8);

		foreach (var entry in hits)
		{
			if (entry["collider"].Obj is not Node collider) continue;

			Health target = collider.GetNodeOrNull<Health>("Health");
			if (target == null) continue;

			// Contra humano a patada vale mais: o lobo é feito para caçar
			// gente, não para duelar com outro lobo. E cair em cima de
			// alguém é a jogada que as pessoas lembram: vale o bônus aéreo.
			float fallen = _fallStartY - _body.GlobalPosition.Y;
			bool fromAbove = aerial || (!_body.IsOnFloor() && fallen >= AirAttackMinFall);
			float damage = CombatRules.WolfStrike(breed.AttackDamage, multiplier,
				IsHumanForm(collider), ClawVsHumanBonus, fromAbove, breed.AirAttackBonus);

			Damage(target, damage, _body.GlobalPosition, false);
		}
	}
}
