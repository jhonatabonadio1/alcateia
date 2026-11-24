using Godot;

/// <summary>
/// O bot joga granada.
///
/// Não jogava: nenhuma linha do bot tocava na granada, e ela é a resposta
/// do humano ao lobo. Agora ele arremessa em três situações — o alvo à
/// vista a meia distância, o alvo que acabou de sumir atrás de uma parede
/// (para tirá-lo de lá) e, com mais vontade, o lobo que vem correndo.
///
/// O arremesso é um gesto com começo e fim, como o do jogador: tira a
/// granada, mira um instante parado (<see cref="GrenadeWindup"/> — é o
/// aviso para quem está do outro lado) e solta. A física é a mesma da
/// arma (<see cref="Weapon.ThrowForce"/>); o bot só escolhe o ângulo.
/// </summary>
public partial class Bot
{
	/// <summary>Chance de arremessar a cada segundo em que dá para
	/// arremessar. Sobe com a dificuldade; dobra contra lobo.</summary>
	[ExportGroup("Granada")]
	[Export] public float GrenadeRate = 0.3f;

	/// <summary>De quão perto a quão longe ele arremessa, em metros. O
	/// mínimo fica além do raio da explosão: bot que se explode não é
	/// difícil nem fácil, é quebrado.</summary>
	[Export] public float GrenadeMinRange = 8f;
	[Export] public float GrenadeMaxRange = 20f;

	/// <summary>Tempo mirando com a granada na mão antes de soltar.</summary>
	[Export] public float GrenadeWindup = 0.4f;

	/// <summary>Descanso entre uma granada e a próxima, em segundos.</summary>
	[Export] public float GrenadeRest = 9f;

	/// <summary>Não arremessa com aliado a menos disto do ponto de queda.</summary>
	[Export] public float GrenadeFriendRadius = 6f;

	private bool _throwing;
	private Vector3 _throwAt;
	private float _throwYawError;
	private double _throwTimer;
	private int _grenadesBefore;
	private double _grenadeRoll;
	private double _grenadeRest;

	/// <summary>Decide, uma vez por segundo, se é hora de granada.</summary>
	private void ConsiderGrenade(double delta)
	{
		_grenadeRest -= delta;
		_grenadeRoll -= delta;
		if (_grenadeRoll > 0) return;
		_grenadeRoll = 1.0;

		if (_grenadeRest > 0 || _target == null) return;
		if (_form.IsWolf || !_form.CanAttack || _weapon.Grenades <= 0 || _weapon.IsReloading) return;
		if (Action == BotAction.Howl || Action == BotAction.Transform) return;

		// Onde: no alvo à vista, ou onde ele sumiu agora há pouco.
		bool sees = CanSeeTarget();
		if (!sees && !(State == BotState.Search && _beliefAge < 3.0)) return;

		Vector3 at = sees ? _target.GlobalPosition : _lastKnown;

		// Alvo andando: joga onde ele vai estar. É o que põe a granada na
		// frente do lobo que vem correndo, e não atrás dele.
		if (sees && _target is CharacterBody3D runner)
			at += new Vector3(runner.Velocity.X, 0, runner.Velocity.Z) * 0.6f;

		Vector3 to = at - GlobalPosition;
		float distance = new Vector3(to.X, 0, to.Z).Length();
		if (distance < GrenadeMinRange || distance > GrenadeMaxRange) return;

		if (FriendNear(at) || ThrowBlocked(at, distance)) return;

		bool wolf = _target.GetNodeOrNull<FormController>("FormController")?.IsWolf ?? false;
		float chance = GrenadeRate * (wolf ? 2f : 1f) * (sees ? 1f : 1.4f);
		if (!_rng.Chance(Mathf.Min(chance, 0.95f))) return;

		_throwing = true;
		_throwAt = at;
		_throwYawError = Mathf.DegToRad(_rng.Range(-AimError, AimError) * 0.6f);
		_throwTimer = GrenadeWindup;
		_grenadesBefore = _weapon.Grenades;
		_weapon.SelectSlot(Slot.Grenade);

		if (Debug) GD.Print($"{Name}: grenade at {distance:0.0} m{(wolf ? " at the wolf" : "")}{(sees ? "" : " behind the wall")}");
	}

	/// <summary>
	/// O arremesso em curso. Devolve verdadeiro enquanto o corpo é dele.
	///
	/// A arma não solta a granada no quadro em que ela chega à mão (guardar
	/// a outra leva um tempo), então o bot insiste por um segundo e meio e,
	/// se não saiu, desiste e volta para a arma.
	/// </summary>
	private bool GrenadeStep(double delta)
	{
		if (!_throwing) return false;

		if (_form.IsWolf || _weapon.Grenades <= 0)
		{
			DropGrenade();
			return false;
		}

		Vector3 to = _throwAt - GlobalPosition;
		var flat = new Vector3(to.X, 0, to.Z);
		float distance = flat.Length();

		float yaw = Mathf.Atan2(-flat.X, -flat.Z) + _throwYawError;
		float next = Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(14f * (float)delta, 1f));
		Rotation = new Vector3(0, next, 0);
		_scanAngle = next;
		_head.Rotation = new Vector3(ThrowPitch(distance, _throwAt.Y - _head.GlobalPosition.Y), 0, 0);

		MoveTowards(Vector3.Zero, Speed, delta);

		_throwTimer -= delta;
		if (_throwTimer > 0) return true;

		if (Mathf.Abs(Mathf.AngleDifference(next, yaw)) < 0.1f)
			_weapon.TryAttack();

		if (_weapon.Grenades < _grenadesBefore)
		{
			// Saiu. A arma volta sozinha para a mão (Weapon.ThrowGrenade).
			_throwing = false;
			_grenadeRest = GrenadeRest * _rng.Range(0.7, 1.5);
			return true;
		}

		if (_throwTimer < -1.5)
		{
			DropGrenade();
			_grenadeRest = 3.0;
		}

		return true;
	}

	/// <summary>Larga o arremesso e volta para a arma.</summary>
	private void DropGrenade()
	{
		if (!_throwing) return;
		_throwing = false;

		if (!_form.IsWolf && !_health.IsDead && _weapon.HumanSlot == Slot.Grenade)
			_weapon.SelectSlot(LoadedFirearm());
	}

	/// <summary>
	/// Ângulo da cabeça para a granada cair a <paramref name="distance"/>
	/// metros, <paramref name="rise"/> acima (ou abaixo) dela.
	///
	/// Testa ângulos de −25° a 12° e fica com o que cai mais perto. O teto
	/// baixo é de propósito: arco alto alcança mais, mas bate no teto dos
	/// mapas fechados e devolve a granada para o pé de quem jogou.
	/// </summary>
	private float ThrowPitch(float distance, float rise)
	{
		float best = 0f;
		float bestError = float.MaxValue;

		for (float degrees = -25f; degrees <= 12f; degrees += 2.5f)
		{
			float pitch = Mathf.DegToRad(degrees);
			float forward = _weapon.ThrowForce * Mathf.Cos(pitch);
			float up = _weapon.ThrowForce * Mathf.Sin(pitch) + _weapon.ThrowArc;

			float root = up * up - 2f * _gravity * rise;
			if (root < 0) continue;

			float time = (up + Mathf.Sqrt(root)) / _gravity;
			float error = Mathf.Abs(forward * time - distance);
			if (error >= bestError) continue;

			bestError = error;
			best = pitch;
		}

		return best;
	}

	/// <summary>Tem aliado vivo perto de onde a granada vai cair?</summary>
	private bool FriendNear(Vector3 point)
	{
		foreach (var (body, health) in OtherFighters())
		{
			if (health.Team == _health.Team && body.GlobalPosition.DistanceTo(point) < GrenadeFriendRadius)
				return true;
		}

		return false;
	}

	/// <summary>Tem parede (ou teto) nos primeiros metros do arremesso?
	/// Sem esta pergunta o bot atrás de um caixote joga a granada no
	/// caixote.</summary>
	private bool ThrowBlocked(Vector3 at, float distance)
	{
		Vector3 origin = _head.GlobalPosition;
		Vector3 to = at - origin;
		var flat = new Vector3(to.X, 0, to.Z).Normalized();

		float pitch = ThrowPitch(distance, to.Y);
		Vector3 launch = (flat * Mathf.Cos(pitch) * _weapon.ThrowForce
			+ Vector3.Up * (Mathf.Sin(pitch) * _weapon.ThrowForce + _weapon.ThrowArc)).Normalized();

		var query = PhysicsRayQueryParameters3D.Create(origin, origin + launch * Mathf.Min(distance * 0.6f, 6f));
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 && hit["collider"].Obj != _target;
	}
}
