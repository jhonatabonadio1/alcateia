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
	[Export] public float AttackBuffer = 0.45f;

	/// <summary>Quanto depois do impacto a próxima garrada pode sair, em
	/// segundos. Um clique, um golpe inteiro de um braço (pedido do Jhon,
	/// 18/11): o golpe chega em 0,15 s e a mão ainda termina de passar;
	/// com 0,19 o outro braço entra em 0,34 s — até 2,9 garradas por
	/// segundo, cada uma visível do começo ao fim do corte. Antes (0,08)
	/// a próxima cortava a atual no meio do movimento, e como os dois
	/// braços tocavam um por cima do outro, uma garrada em cada duas nem
	/// aparecia. O clique dado durante o golpe fica guardado
	/// (<see cref="AttackBuffer"/>) e sai nesse instante.</summary>
	[Export] public float ClawCancelAfter = 0.19f;

	private int _comboIndex;

	/// <summary>Segundos desde o último golpe do lobo que bateu (garrada,
	/// bote, especial). É o relógio do teto contra o clique em rajada.</summary>
	private double _sinceStrike = 99;

	/// <summary>O menor intervalo entre dois golpes do lobo que ferem: o ritmo
	/// da garrada encadeada (<see cref="CombatRules.WolfStrikeSpacing"/>, 0,34 s).
	/// Nenhum golpe — garrada, bote — sai antes disso depois do anterior, por
	/// mais rápido que se clique (alpha fechado, 26/11: "clique repetido nos
	/// ataques do lobo, principal e secundário, parece não ter limite").</summary>
	/// <summary>Até quanto uma pancada pode cair antes do compasso sem esperar (meio passo de física).</summary>
	private const double SpacingSlack = 0.5 / 60.0;

	public float StrikeSpacing => CombatRules.WolfStrikeSpacing(WolfMoves.ImpactDelay(WolfMove.Claw), ClawCancelAfter);

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
	/// <summary>Cliques guardados à espera do golpe em curso acabar: um
	/// clique, um golpe (pedido do Jhon, 18/11). Dois cliques rápidos são
	/// duas garradas, uma de cada braço; mais que isso não empilha (soltar
	/// o botão tem de parar o lobo logo).</summary>
	public const int MaxQueuedAttacks = 2;

	private int _attackQueue;

	/// <summary>Zera a fila de cliques (golpe pesado, morte).</summary>
	private bool _attackQueued { set { if (!value) _attackQueue = 0; } }
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
		CutDiveLanding();
		if (_comboTimer <= 0) _comboIndex = 0;

		float multiplier = ComboDamage[Mathf.Min(_comboIndex, ComboDamage.Length - 1)];
		_comboIndex = (_comboIndex + 1) % ComboDamage.Length;

		WolfMove move = _nextPunch ? WolfMove.Punch : WolfMove.Claw;
		_nextPunch = !_nextPunch;

		// O combo zera se passar ComboReset depois do fim do golpe.
		_comboTimer = WolfMoves.Duration(move) + ComboReset;

		// Parado, o corpo dá o passo do golpe, em sino: as pernas andam
		// um passo junto com o braço. Correndo, o empurrão de sempre leva
		// o lobo até o alvo — as pernas já estão correndo. Só o jogador: o
		// bot anda pela própria cabeça.
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		bool standing = _body is Player && _body.IsOnFloor() && flat.Length() < StandingSpeed;

		ulong id = StartMove(move, CooldownClaw, 0f, () =>
		{
			if (!standing) Lunge();
			_wolfHit = Kill.WolfClawId;
			Claw(multiplier, ClawReach, ClawRadius, false);
		});

		var (delay, duration, distance) = WolfMoves.StepOf(move);
		if (standing && distance > 0f)
			After(delay, id, () => BeginStep(duration, distance));

		// A próxima garrada pode cortar esta logo depois do impacto.
		_cooldown = Mathf.Min(_cooldown, WolfMoves.ImpactDelay(move) + ClawCancelAfter);
		EmitSignal(SignalName.AttackCooldown, CooldownClaw, (float)_cooldown);
	}

	/// <summary>Corta a garrada em curso e os cliques guardados (o lobo grudou
	/// na parede, onde não se bate — Jhon, 24/11): o dano que ainda não saiu
	/// não sai, a garra some da tela e o próximo clique já pode arranhar. O
	/// bote, o salto e a carga não são garrada e seguem.</summary>
	public void CutClaw()
	{
		if (_form == null || !_form.IsWolf || _diving || _flying || _charging) return;
		_attackQueue = 0;
		if (_strikeLeft <= 0 && _cooldown <= 0) return;
		_strikeId++;
		_strikeLeft = 0;
		_cooldown = 0;
		_stepLeft = 0;
		EmitSignal(SignalName.WolfMoveCancelled, (int)WolfMove.Claw);
		EmitSignal(SignalName.WolfMoveCancelled, (int)WolfMove.Punch);
		EmitSignal(SignalName.AttackCooldown, CooldownClaw, 0f);
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
	/// <param name="length">Quanto o golpe dura daqui em diante, se não
	/// for o do clipe (o especial acelera a queda).</param>
	/// <param name="impactAt">Daqui a quanto o dano sai, idem.</param>
	private ulong StartMove(WolfMove move, int kind, float minCooldown, System.Action impact, float elapsed = 0f,
		float? length = null, float? impactAt = null)
	{
		float duration = length ?? WolfMoves.Duration(move) - elapsed;
		float cooldown = Mathf.Max(duration, minCooldown);

		_cooldown = cooldown;
		_strikeLeft = duration;
		ulong id = ++_strikeId;

		if (elapsed <= 0f) EmitSignal(SignalName.WolfMoveStarted, (int)move);
		EmitSignal(SignalName.AttackCooldown, kind, cooldown);

		After(Mathf.Max(impactAt ?? WolfMoves.ImpactDelay(move) - elapsed, 0f), id, () =>
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

	/// <summary>A esfera do golpe encosta na cápsula do corpo? Pela forma
	/// de colisão dele (cápsula em pé); sem ela, a medida padrão do jogo
	/// (1,75 m de altura, 0,4 de raio).</summary>
	private static bool TouchesBody(Vector3 center, float radius, CharacterBody3D body)
	{
		float capsuleRadius = 0.4f, half = 0.875f;
		Vector3 origin = body.GlobalPosition;
		foreach (Node child in body.GetChildren())
		{
			if (child is not CollisionShape3D { Disabled: false, Shape: CapsuleShape3D capsule } shape) continue;
			capsuleRadius = capsule.Radius;
			half = capsule.Height * 0.5f;
			origin = shape.GlobalPosition;
			break;
		}

		// O ponto do eixo da cápsula mais perto do centro da esfera.
		float spine = Mathf.Max(half - capsuleRadius, 0f);
		float y = Mathf.Clamp(center.Y, origin.Y - spine, origin.Y + spine);
		var nearest = new Vector3(origin.X, y, origin.Z);
		float reach = radius + capsuleRadius;
		return nearest.DistanceSquaredTo(center) <= reach * reach;
	}

	/// <summary>Nada sólido do mapa entre o peito do lobo e o do outro (os
	/// corpos não contam): a área do especial não passa parede.</summary>
	private bool InSight(Node3D other)
	{
		var query = PhysicsRayQueryParameters3D.Create(_body.GlobalPosition + Vector3.Up * 0.3f,
			other.GlobalPosition + Vector3.Up * 0.3f, 1);
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		if (other is CollisionObject3D co) exclude.Add(co.GetRid());
		query.Exclude = exclude;
		for (int i = 0; i < 6; i++)
		{
			var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0) return true;
			if (hit["collider"].Obj is not CharacterBody3D) return false;
			exclude.Add((Rid)hit["rid"]);
			query.Exclude = exclude;
		}
		return true;
	}

	/// <summary>
	/// Golpe de garra: um volume curto à frente do corpo, não um raio
	/// a partir da câmera. Pega quem estiver perto da garra.
	///
	/// <paramref name="aerial"/>: o golpe veio de uma queda alta e vale
	/// o bônus aéreo mesmo já no chão — o bote bate quando aterrissa.
	///
	/// <paramref name="splash"/> abaixo de 1: o golpe é de um alvo só (o
	/// salto). O principal — <paramref name="focus"/>, se estiver na área,
	/// senão o inimigo mais perto do centro — leva o dano cheio; os outros
	/// na área levam só esta fração.
	///
	/// <paramref name="area"/> acima do raio: a área do especial (26/11). O
	/// principal continua escolhido dentro de <paramref name="radius"/>; quem
	/// estiver até <paramref name="area"/> m do lobo leva uma fração que cai com
	/// a distância (<see cref="CombatRules.PounceSplash"/>), e só se não houver
	/// parede entre o lobo e ele.
	/// </summary>
	private void Claw(float multiplier, float reach, float radius, bool aerial, Vector3? facing = null,
		Node3D focus = null, float splash = 1f, float area = 0f)
	{
		// O compasso do dano (27/11): as animações se cortam na hora (garra e bote), mas duas pancadas não
		// saem a menos de StrikeSpacing (0,34 s) uma da outra — o que o servidor também confere
		// (WolfStrikePace). A que cairia cedo demais espera o resto do compasso, presa a este golpe: se outro
		// golpe o cortar nesse meio, ela não sai.
		double early = StrikeSpacing - _sinceStrike;
		if (early > SpacingSlack)
		{
			After((float)early, _strikeId, () => Claw(multiplier, reach, radius, aerial, facing, focus, splash, area));
			return;
		}

		WolfBreed breed = _form.Breed;

		// O volume nasce à frente do CORPO, na altura do peito. Preso
		// ao corpo e não ao olhar, é o que dá à garra um alcance que
		// varia com a postura em vez de seguir a mira. O salto passa a
		// direção em que voou: ele pode ter curvado atrás do alvo.
		Vector3 forward = facing ?? -_body.GlobalTransform.Basis.Z;
		Vector3 center = _body.GlobalPosition + forward * reach + Vector3.Up * 0.2f;

		// Quem está na área. Os lutadores (jogadores e bots) entram por
		// conta de geometria — a esfera do golpe contra a cápsula do corpo —
		// e não pela consulta de física.
		//
		// 18/11 (Jhon: "dei o especial, dei lock num inimigo, mas não deu
		// dano algum; isso não pode acontecer"): a consulta devolve no máximo
		// N contatos, de qualquer coisa. Perto de um contêiner, a área do
		// salto pegava 48 pedaços de cenário e a lista acabava antes de
		// chegar no inimigo — o golpe saía, caía em cima dele e não feria. A
		// garrada, com teto de 8, tinha o mesmo furo. A consulta fica só
		// para o que tem vida e não é lutador (o boneco do treino).
		var struckNow = new List<Node3D>();
		var inArea = new List<Node3D>();
		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in _body.GetTree().GetNodesInGroup(group))
			{
				if (node == _body || node is not CharacterBody3D other || !other.IsInsideTree()) continue;
				// Boneco de jogador que ainda não chegou (sem camada) não está no mundo.
				if (other.CollisionLayer == 0 || other.GetNodeOrNull<Health>("Health") == null) continue;
				// O travado, se o lobo caiu perto dele, sempre leva: um palmo
				// de folga para o passo que ele deu entre a queda e a pancada.
				float slack = other == focus ? 0.6f : 0f;
				if (TouchesBody(center, radius + slack, other)) struckNow.Add(other);
				else if (area > radius && other.GlobalPosition.DistanceTo(_body.GlobalPosition) <= area) inArea.Add(other);
			}
		}

		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = radius },
			Transform = new Transform3D(Basis.Identity, center),
			CollideWithBodies = true,
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() }
		};
		foreach (var entry in _body.GetWorld3D().DirectSpaceState.IntersectShape(query, 32))
		{
			if (entry["collider"].Obj is Node3D thing && !struckNow.Contains(thing)
				&& !thing.IsInGroup(Groups.Players) && !thing.IsInGroup(Groups.Bots)
				&& thing.GetNodeOrNull<Health>("Health") != null)
				struckNow.Add(thing);
		}

		_sinceStrike = 0;

		Node main = null;
		if (splash < 1f)
		{
			float best = float.MaxValue;
			foreach (Node3D body in struckNow)
			{
				Health h = body.GetNode<Health>("Health");
				if (h.IsDead || (_health != null && h.Team == _health.Team)) continue;
				if (body == focus) { main = body; break; }
				float d = body.GlobalPosition.DistanceSquaredTo(center);
				if (d < best) { best = d; main = body; }
			}
		}

		// A área do especial: quem está em volta, à vista do lobo, entra com a
		// fração da distância (a do raio do golpe também segue a distância).
		var areaShare = new Dictionary<Node3D, float>();
		if (area > radius)
		{
			foreach (Node3D body in inArea) struckNow.Add(body);
			foreach (Node3D body in struckNow)
			{
				if (body == main) continue;
				Health h = body.GetNode<Health>("Health");
				if (_health != null && h.Team == _health.Team && !RoomConfig.Rules.FriendlyFire) continue;
				float share = CombatRules.PounceSplash(body.GlobalPosition.DistanceTo(_body.GlobalPosition),
					PounceSplashInner, area, splash, PounceSplashFar);
				areaShare[body] = share > 0f && InSight(body) ? share : 0f;
			}
		}

		foreach (Node3D collider in struckNow)
		{

			Health target = collider.GetNodeOrNull<Health>("Health");
			if (target == null) continue;

			// Contra humano a patada vale mais: o lobo é feito para caçar
			// gente, não para duelar com outro lobo. E cair em cima de
			// alguém é a jogada que as pessoas lembram: vale o bônus aéreo.
			float fallen = _fallStartY - _body.GlobalPosition.Y;
			bool fromAbove = aerial || (!_body.IsOnFloor() && fallen >= AirAttackMinFall);
			float damage = CombatRules.WolfStrike(breed.AttackDamage, multiplier,
				IsHumanForm(collider), ClawVsHumanBonus, fromAbove, breed.AirAttackBonus);
			if (area > radius && collider != main)
			{
				if (!areaShare.TryGetValue(collider, out float share) || share <= 0f) continue;
				damage *= share;
			}
			else if (splash < 1f && collider != main) damage *= splash;

			// Sangue no peito de quem levou, saindo para o lado do corte (a
			// mão que bate alterna) e um pouco para a frente. O salto, que
			// cai em cima, espirra mais.
			Node3D struck = collider;
			{
				Vector3 ahead = (struck.GlobalPosition - _body.GlobalPosition) with { Y = 0 };
				if (ahead.LengthSquared() < 0.01f) ahead = -_body.GlobalBasis.Z;
				ahead = ahead.Normalized();
				Vector3 side = ahead.Cross(Vector3.Up) * (_comboIndex % 2 == 0 ? 1f : -1f);
				Bleed(target, struck.GlobalPosition + Vector3.Up * 0.35f,
					ahead + side * 0.8f + Vector3.Up * 0.25f, multiplier > 1.5f ? 2.2f : 1.7f);
			}

			Damage(target, damage, _body.GlobalPosition, false);
		}
	}
}
