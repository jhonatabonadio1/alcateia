using Godot;
using System.Collections.Generic;

/// <summary>
/// O bote do lobo (botão direito no ar): um mergulho em linha reta na
/// direção da mira, que bate quando encosta em algo.
/// </summary>
public partial class Weapon
{
	/// <summary>Dano do bote, num golpe só, na hora do impacto.</summary>
	[ExportGroup("Lobo: bote")]
	[Export] public float LeapDamageBonus = 3f;

	/// <summary>Tempo mínimo entre um bote e outro, contado do clique. Era
	/// 0 ("todo pulo pode ter o seu"): pulando e apertando o secundário sem
	/// parar, saía um bote de 360 a cada 0,7 s (540 de dano por segundo; alpha
	/// fechado, 26/11: "dá pra farmar clique"). Com 1 s, quase todo pulo ainda
	/// tem o seu; o que cai antes fica guardado no ar e sai quando pode
	/// (Player.Actions), e o HUD mostra a espera.</summary>
	[Export] public float LeapCooldown = 1f;

	/// <summary>Segundos desde o último bote (o clique).</summary>
	private double _sinceLeap = 99;

	/// <summary>Segundos entre o dano do bote e a garra poder sair. Era 0,05:
	/// a garrada batia 0,2 s depois do bote, e o ciclo pulo-bote-garra dava
	/// 870 de dano por segundo (alpha fechado, 26/11). Agora a garra pode sair
	/// quando a próxima pancada chega a <see cref="StrikeSpacing"/> do bote
	/// (o clique antes disso fica guardado e sai na hora).</summary>
	[Export] public float DiveFollowUp = 0.05f;

	/// <summary>Quanto ainda dura a aterrissagem do último bote, em tempo
	/// de jogo: nesse tempo a garra corta a aterrissagem.</summary>
	private double _diveLandLeft;

	/// <summary>Quanto ainda falta do levantar de um golpe que já bateu (o
	/// bote que encostou, o salto que caiu), em tempo de jogo, como o
	/// <c>_rootLeft</c>. É o que o pulo e a corrida cortam. Era um relógio de
	/// parede (ms): um engasgo no impacto (o efeito do bote compilando na
	/// primeira vez) passava do prazo antes do passo seguinte, a corrida não
	/// cortava, e o lobo ficava preso correndo no lugar (Jhon, 21/11).</summary>
	private double _recoverLeft;

	/// <summary>A garra saindo por cima da aterrissagem do bote: solta o
	/// lobo do chão.</summary>
	private void CutDiveLanding()
	{
		if (_diveLandLeft <= 0) return;
		CutRecovery();
	}

	/// <summary>
	/// O golpe já bateu e o lobo só está se levantando (a aterrissagem do
	/// bote, o fim do salto). Pedido do Jhon (20/11): "um anula o outro,
	/// desde que tenham feito o movimento completo" — o pulo e a corrida de
	/// quatro cortam essa parte na hora.
	/// </summary>
	public bool IsRecovering => _form != null && _form.IsWolf && _rootLeft > 0 && !_diving && !_flying && !_charging
		&& _recoverLeft > 0;

	/// <summary>Solta o lobo do levantar: anda, pula e corre neste mesmo passo.</summary>
	public void CutRecovery()
	{
		if (_rootLeft <= 0 && _diveLandLeft <= 0 && _recoverLeft <= 0) return;
		_diveLandLeft = 0;
		_recoverLeft = 0;
		_rootLeft = 0;
		_strikeLeft = 0;
		EmitSignal(SignalName.WolfRecoveryCut);
	}

	private void MarkRecovery(double seconds) => _recoverLeft = Mathf.Max((float)seconds, 0f);

	/// <summary>Os prazos do levantar correm com o jogo (Weapon._PhysicsProcess).</summary>
	private void TickRecovery(double delta)
	{
		if (_recoverLeft > 0) _recoverLeft -= delta;
		if (_diveLandLeft > 0) _diveLandLeft -= delta;
	}

	/// <summary>Velocidade do mergulho, em m/s. Constante, em linha reta:
	/// sem gravidade, sem freio e sem desvio no meio. O ponto de impacto
	/// é escolhido no clique, e é isso que deixa o bote preciso. Era 17:
	/// rápido demais para quem está embaixo ver o lobo chegando.</summary>
	[Export] public float DiveSpeed = 13f;

	/// <summary>Inclinação mínima para baixo do mergulho. Mirando reto ou
	/// para cima, o bote ainda desce: é um ataque de cima para baixo, não
	/// um segundo pulo.</summary>
	[Export] public float DiveMinDrop = 0.35f;

	/// <summary>Teto do mergulho, em segundos. Mirou no vazio e não
	/// encostou em nada: bate no ar e volta a cair. Com o mergulho mais
	/// lento, 1,1 s mantém o alcance de antes (14 m).</summary>
	[Export] public float DiveMaxTime = 1.1f;

	/// <summary>Quanto do avanço horizontal o bote mantém (decisão do
	/// Jhon): 0,8 — o mergulho desce igual e avança 20% menos.</summary>
	[Export] public float DiveForward = 0.8f;

	/// <summary>O mesmo depois de um salto da parede: dali o lobo sai mais
	/// alto e o bote ia longe demais. Metade.</summary>
	[Export] public float DiveForwardFromWall = 0.5f;

	/// <summary>Volume do bote: um pouco maior que o do salto e das
	/// garras (<see cref="ClawReach"/>, <see cref="ClawRadius"/>). Cair
	/// do alto em cima de alguém é a jogada que o jogo quer premiar, e
	/// um impacto que erra por um palmo não premia ninguém.</summary>
	[Export] public float LeapReach = 1.7f;
	[Export] public float LeapRadius = 1.5f;

	private bool _diving;
	private double _diveLeft;
	private double _diveElapsed;

	/// <summary>Consulta de "tem alguém na frente do bote", reaproveitada
	/// a cada quadro do mergulho em vez de montada de novo.</summary>
	private PhysicsShapeQueryParameters3D _diveProbe;

	/// <summary>No meio do bote: o corpo segue <see cref="DiveVelocity"/>
	/// e mais nada até encostar.</summary>
	public bool IsDiving => _diving && _form != null && _form.IsWolf;

	/// <summary>Num golpe que voa — o bote, o especial do começo ao fim —: a
	/// queda que passa por ele não fere (Jhon, 26/11: "não deve tomar dano de
	/// queda no ataque secundário e com a habilidade suprema").</summary>
	public bool FallExempt => _form != null && _form.IsWolf && (_diving || _flying || _pounceGuard || _diveLandLeft > 0);

	/// <summary>Velocidade do mergulho em curso, fixa desde o clique.</summary>
	public Vector3 DiveVelocity { get; private set; }

	/// <summary>Quanto o mergulho deve levar até encostar no chão, medido
	/// no clique. O corpo usa para a queda da animação chegar junto.</summary>
	public float DiveEta { get; private set; }

	/// <summary>
	/// Bote: mergulho reto na direção da mira, com a velocidade fixa
	/// desde o clique. Bate no instante em que encosta — no chão, numa
	/// parede ou em alguém —, não num tempo marcado. A animação da queda
	/// é acelerada para o impacto dela cair no mesmo quadro.
	/// </summary>
	private void Leap()
	{
		_comboIndex = 0;

		// A mira inteira, com o pitch: olhar para baixo dá um bote curto e
		// mergulhado, olhar à frente, um longo e raso. Sempre descendo.
		Vector3 aim = -_head.GlobalTransform.Basis.Z;
		aim.Y = Mathf.Min(aim.Y, -DiveMinDrop);
		aim = aim.Normalized();

		DiveVelocity = aim * DiveSpeed;
		float forward = (_body as Player)?.JumpedFromWall == true ? DiveForwardFromWall : DiveForward;
		DiveVelocity = new Vector3(DiveVelocity.X * forward, DiveVelocity.Y, DiveVelocity.Z * forward);
		DiveEta = TimeToContact(DiveVelocity);

		_diving = true;
		_diveLeft = DiveMaxTime;
		_diveElapsed = 0;
		_sinceLeap = 0;
		_strikeId++;
		_strikeLeft = 0;
		_cooldown = DiveMaxTime + WolfMoves.LandDuration;

		EmitSignal(SignalName.WolfMoveStarted, (int)WolfMove.Leap);
		EmitSignal(SignalName.AttackCooldown, CooldownLeap,
			Mathf.Max(DiveEta + WolfMoves.LandDuration, LeapCooldown));
	}

	/// <summary>Em quanto tempo o mergulho encosta no cenário, pelo raio
	/// dos pés na direção dele.</summary>
	private float TimeToContact(Vector3 velocity)
	{
		float speed = Mathf.Max(velocity.Length(), 0.1f);
		Vector3 direction = velocity / speed;
		float range = speed * DiveMaxTime;
		Vector3 feet = _body.GlobalPosition + Vector3.Down * 0.8f;

		var query = PhysicsRayQueryParameters3D.Create(feet, feet + direction * range, 1);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count == 0) return DiveMaxTime;
		return feet.DistanceTo((Vector3)hit["position"]) / speed;
	}

	/// <summary>Durante o mergulho: alguém na frente encerra o bote em
	/// cima dele; o tempo acabando encerra no ar. O chão e a parede quem
	/// avisa é o corpo (<see cref="DiveContact"/>), que é quem se move.</summary>
	private void UpdateDive(double delta)
	{
		if (!_form.IsWolf || _health.IsDead)
		{
			_diving = false;
			return;
		}

		_diveLeft -= delta;
		_diveElapsed += delta;

		if (_diveLeft <= 0 || EnemyAhead())
			DiveImpact();
	}

	/// <summary>Quanto da velocidade do bote o corpo guarda quando a garra o corta no ar: sem isto o lobo
	/// seguia mergulhando a 13 m/s, agora sem o bote, até bater no chão.</summary>
	[Export] public float DiveCutCarry = 0.45f;

	/// <summary>A garra cortou o bote no ar: o mergulho para, o dano dele não sai (o golpe é outro) e o
	/// corpo cai com parte do embalo. O bote gasto conta na espera entre botes (LeapCooldown).</summary>
	private void CancelDive()
	{
		if (!_diving) return;
		_diving = false;
		_diveLeft = 0;
		_strikeId++;
		_strikeLeft = 0;
		_cooldown = 0;
		_attackQueue = 0;
		if (_body is CharacterBody3D body) body.Velocity = DiveVelocity * DiveCutCarry;
		EmitSignal(SignalName.WolfMoveCancelled, (int)WolfMove.Leap);
	}

	/// <summary>O corpo encostou no chão, numa parede ou no teto.</summary>
	public void DiveContact()
	{
		if (_diving) DiveImpact();
	}

	private bool EnemyAhead()
	{
		_diveProbe ??= new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = 0.9f },
			CollideWithBodies = true,
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() }
		};

		Vector3 ahead = DiveVelocity.Normalized() * 0.7f;
		_diveProbe.Transform = new Transform3D(Basis.Identity, _body.GlobalPosition + ahead);

		foreach (var entry in _body.GetWorld3D().DirectSpaceState.IntersectShape(_diveProbe, 4))
		{
			if (entry["collider"].Obj is not Node collider) continue;
			Health target = collider.GetNodeOrNull<Health>("Health");
			if (target == null || target.IsDead) continue;
			if (target.Team == _health.Team && !RoomConfig.Rules.FriendlyFire) continue;
			return true;
		}

		return false;
	}

	/// <summary>
	/// O impacto do bote: um golpe só, no volume maior do bote, e o lobo
	/// preso no chão durante a aterrissagem. Acabou a aterrissagem, o
	/// próximo pulo já pode ter outro bote.
	/// </summary>
	private void DiveImpact()
	{
		_diving = false;
		_diveLeft = 0;

		bool aerial = _fallStartY - _body.GlobalPosition.Y >= AirAttackMinFall;

		EmitSignal(SignalName.Fired);
		_wolfHit = Kill.WolfDiveId;
		Claw(LeapDamageBonus, LeapReach, LeapRadius, aerial);

		// Bateu, a garra já pode sair (pedido do Jhon, 18/11: "na hora que
		// eu der o dano com o secundário no pulo, já posso apertar o
		// primário para continuar batendo; é um jogo rápido"). A
		// aterrissagem segue tocando e prendendo o lobo só enquanto ele não
		// clica: o clique corta a aterrissagem (WolfAttack).
		float land = WolfMoves.LandDuration;
		_strikeLeft = land;
		_rootLeft = land;
		// (desde 27/11 a garra sai quase na hora — DiveFollowUp —, e é a pancada dela que espera o
		// compasso, se cair cedo demais: Claw)
		_cooldown = DiveFollowUp;
		_diveLandLeft = land;
		MarkRecovery(land);

		EmitSignal(SignalName.WolfDiveLanded);
		EmitSignal(SignalName.AttackCooldown, CooldownLeap, (float)_cooldown);
	}
}
