using Godot;
using System;

/// <summary>
/// Andar pelo mapa: o caminho da malha de navegação, desviar, pular
/// obstáculo baixo e virar o corpo para onde vai ou mira.
/// </summary>
public partial class Bot
{
	/// <summary>Impulso do pulo do bot. O mesmo do humano: bot que
	/// pula mais alto que o jogador alcança lugares que o jogador não
	/// alcança, e isso não é dificuldade, é trapaça.</summary>
	[ExportGroup("Movimento")]
	[Export] public float JumpVelocity = 4.4f;

	/// <summary>Altura máxima do obstáculo que o bot tenta vencer
	/// pulando.</summary>
	[Export] public float JumpMaxHeight = 1.1f;

	/// <summary>Distância à frente em que ele procura o obstáculo.</summary>
	[Export] public float JumpProbe = 1.1f;
	[Export] public float Speed = 3.0f;
	[Export] public float TurnSpeed = 8f;

	/// <summary>Raio de chegada ao destino de investigação.</summary>
	[Export] public float ArriveRadius = 2.5f;

	/// <summary>Tempo preso no mesmo lugar antes de desistir do
	/// destino. Sem pathfinding, encostar numa quina é questão de
	/// tempo — o que não pode é ficar empurrando a parede para sempre.</summary>
	[Export] public float StuckTime = 1.2f;

	private NavigationAgent3D _nav;
	private double _stuckTimer;
	private Vector3 _lastPosition;

	private double _stunLeft;
	private float _stunSlow = 1f;

	/// <summary>Atordoado (granada de choque): anda devagar e não ataca.</summary>
	public bool IsStunned => _stunLeft > 0;

	/// <summary>O atordoamento da granada de choque, como o do jogador
	/// (<see cref="Player.ApplyStun"/>): o mais forte vence.</summary>
	public void ApplyStun(float duration, float slowFactor)
	{
		if (duration <= 0) return;
		if (duration > _stunLeft) _stunLeft = duration;
		_stunSlow = Mathf.Min(_stunSlow, Mathf.Clamp(slowFactor, 0.05f, 1f));
	}

	private void MoveTowards(Vector3 direction, float speed, double delta)
	{
		if (_stunLeft > 0)
		{
			_stunLeft -= delta;
			speed *= _stunSlow;
			if (_stunLeft <= 0) _stunSlow = 1f;
		}

		Vector3 velocity = Velocity;

		velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, speed * 6f * (float)delta);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, speed * 6f * (float)delta);
		velocity.Y = IsOnFloor() ? 0 : velocity.Y - _gravity * (float)delta;

		bool dodge = _dodge && !_form.IsWolf;

		if (IsOnFloor() && (dodge || ShouldJump(direction)))
		{
			_dodge = false;
			velocity.Y = JumpVelocity;

			_animator ??= GetNodeOrNull<CharacterAnimator>("Human");
			_animator?.Jump();
		}

		Velocity = velocity;
		Vector3 before = GlobalPosition;
		bool grounded = IsOnFloor();
		// (descendo uma escada de degraus o corpo desce grudado nela, como o do jogador)
		bool walking = grounded && velocity.Y <= 0.01f;
		BodyMotion.MoveOnSteps(this, walking, new Vector3(direction.X, 0, direction.Z).Normalized(), StepHeight);
		if (walking)
			TryStepUp(before, new Vector3(velocity.X, 0, velocity.Z) * (float)delta);
	}

	/// <summary>Degrau que o bot sobe andando, sem pular. Igual ao da
	/// navegação (<see cref="MapLoader.AgentStepHeight"/>): o caminho que
	/// ela traça conta com isso.</summary>
	[Export] public float StepHeight = 0.5f;

	/// <summary>
	/// Subir um degrau baixo andando, como o jogador
	/// (<see cref="Player"/>, <c>TryStepUp</c>). O bot não tinha isto: a
	/// navegação mandava subir a borda de um contêiner baixo (ela aceita
	/// até 0,5 m) e o corpo parava na quina, empurrando (relato do Jhon,
	/// 18/11: "os bots não estão conseguindo subir pequenos degraus dos
	/// contêineres").
	/// </summary>
	private void TryStepUp(Vector3 from, Vector3 wanted)
	{
		float want = wanted.Length();
		if (want < 0.0005f || !IsOnWall()) return;

		Vector3 moved = GlobalPosition - from;
		if (new Vector2(moved.X, moved.Z).Length() > want * 0.6f) return;

		Vector3 up = Vector3.Up * StepHeight;
		Vector3 ahead = wanted.Normalized() * Mathf.Max(want, 0.12f);

		if (!BodyMotion.ProbeStep(this, from, up, ahead, FloorMaxAngle, out Vector3 landed)) return;
		if (landed.Y - from.Y < 0.02f) return;
		GlobalPosition = landed;
	}

	/// <summary>
	/// Há degrau à frente que valha um pulo?
	///
	/// A navegação resolve o chão e ignora o resto: ela devolve um
	/// caminho pelo piso e nada diz sobre o caixote de um metro no
	/// meio dele. O bot então empurrava contra o caixote até o
	/// detector de travamento desistir e mandá-lo para outro lugar —
	/// e de fora isso parece um bot que não entende o mapa.
	///
	/// A leitura é em três perguntas, nessa ordem, porque cada uma só
	/// faz sentido se a anterior passou: existe algo à frente na
	/// altura da canela? O topo desse algo está dentro do alcance do
	/// pulo? E existe piso logo depois, para onde cair?
	///
	/// A terceira é a que importa. Sem ela o bot pula em parede.
	/// </summary>
	private bool ShouldJump(Vector3 direction)
	{
		Vector3 flat = new Vector3(direction.X, 0, direction.Z);
		if (flat.LengthSquared() < 0.01f) return false;

		flat = flat.Normalized();

		var space = GetWorld3D().DirectSpaceState;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };

		// 1. obstáculo na canela
		Vector3 shin = GlobalPosition + Vector3.Down * 0.6f;
		var ahead = PhysicsRayQueryParameters3D.Create(shin, shin + flat * JumpProbe);
		ahead.Exclude = exclude;

		if (space.IntersectRay(ahead).Count == 0) return false;

		// 2. passa por cima na altura do pulo?
		Vector3 over = GlobalPosition + Vector3.Up * JumpMaxHeight;
		var clear = PhysicsRayQueryParameters3D.Create(over, over + flat * (JumpProbe + 0.4f));
		clear.Exclude = exclude;

		if (space.IntersectRay(clear).Count > 0) return false;

		// 3. tem chão do outro lado?
		Vector3 beyond = over + flat * (JumpProbe + 0.4f);
		var ground = PhysicsRayQueryParameters3D.Create(
			beyond, beyond + Vector3.Down * (JumpMaxHeight + 1.6f));
		ground.Exclude = exclude;

		return space.IntersectRay(ground).Count > 0;
	}

	/// <summary>
	/// Direção para chegar a um ponto, seguindo o mapa de navegação.
	///
	/// Devolve Zero quando já chegou. Se a navegação não tiver mapa
	/// assado, cai no vetor direto — melhor andar torto que ficar
	/// parado esperando uma malha que pode não existir.
	/// </summary>
	private Vector3 PathTo(Vector3 destination)
	{
		_nav.TargetPosition = destination;

		if (_nav.IsNavigationFinished())
			return Vector3.Zero;

		Vector3 next = _nav.GetNextPathPosition();
		Vector3 delta = next - GlobalPosition;
		var flat = new Vector3(delta.X, 0, delta.Z);

		if (flat.LengthSquared() < 0.0001f)
		{
			Vector3 direct = destination - GlobalPosition;
			flat = new Vector3(direct.X, 0, direct.Z);
		}

		return flat.Normalized();
	}

	/// <summary>Desvio raso: se o caminho está fechado, tenta os lados.
	/// Não é pathfinding e não resolve labirinto.</summary>
	private Vector3 Avoid(Vector3 desired)
	{
		if (desired == Vector3.Zero) return desired;
		if (!Blocked(desired)) return desired;

		for (int sign = -1; sign <= 1; sign += 2)
		{
			for (int step = 1; step <= 3; step++)
			{
				Vector3 candidate = desired.Rotated(Vector3.Up, sign * step * Mathf.Pi / 6f);
				if (!Blocked(candidate)) return candidate;
			}
		}

		return Vector3.Zero;
	}

	private bool Blocked(Vector3 direction)
	{
		Vector3 origin = GlobalPosition;

		var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * 1.6f);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		return GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
	}

	private void FaceDirection(Vector3 point, double delta)
	{
		Vector3 flat = new Vector3(point.X - GlobalPosition.X, 0, point.Z - GlobalPosition.Z);
		if (flat.LengthSquared() < 0.01f) return;

		float desired = Mathf.Atan2(-flat.X, -flat.Z) + _aimOffset.Y;

		// Pego de surpresa, gira mais devagar até ficar de frente.
		float turn = _alerted ? AlertTurnSpeed : TurnSpeed;
		float next = Mathf.LerpAngle(Rotation.Y, desired, turn * (float)delta);

		if (_alerted && Mathf.Abs(Mathf.AngleDifference(next, desired)) < 0.15f)
			_alerted = false;

		Rotation = new Vector3(0, next, 0);
		_scanAngle = next;

		float height = point.Y - GlobalPosition.Y - _head.Position.Y;
		float pitch = Mathf.Atan2(height, flat.Length()) + _aimOffset.X;

		_head.Rotation = new Vector3(
			Mathf.Clamp(pitch, Mathf.DegToRad(-70f), Mathf.DegToRad(70f)), 0, 0);
	}
}
