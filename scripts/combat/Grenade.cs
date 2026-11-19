using Godot;

/// <summary>
/// Granada de atordoamento — a ferramenta anti-lobo.
///
/// Sem ela o humano sozinho num corredor não tem jogada: perde no
/// corpo-a-corpo e não tem distância para usar a arma. A granada não
/// mata o lobo, ela tira a mobilidade dele — que é de onde vem todo
/// o poder da forma.
///
/// Fase 2: o arremesso vira intenção enviada ao servidor, e a
/// explosão é resolvida lá. Projétil com física é justamente o caso
/// em que cliente e servidor divergem se a simulação não for a mesma.
/// </summary>
public partial class Grenade : RigidBody3D
{
	/// <summary>Tempo até explodir, em segundos.</summary>
	[Export] public float Fuse = 1.8f;

	[Export] public float Radius = 6.5f;

	/// <summary>Dano no centro da explosão. Cai linearmente até a borda.</summary>
	[Export] public float MaxDamage = 260f;

	/// <summary>Duração do atordoamento no centro.</summary>
	[Export] public float StunDuration = 2.5f;

	/// <summary>Fração da velocidade enquanto atordoado.</summary>
	[Export] public float StunSlowFactor = 0.3f;

	/// <summary>
	/// Quem lançou. Sem isto a explosão não sabe de que lado está: feria
	/// aliado, e um abate de granada não contava para ninguém — o que,
	/// numa partida decidida por pontos, é um abate jogado fora.
	/// </summary>
	public Health Thrower { get; set; }

	/// <summary>Online: a granada de outro jogador, encenada aqui. Faz o
	/// clarão, o barulho e o atordoamento em você; o dano vem da máquina
	/// de quem arremessou.</summary>
	public bool Cosmetic { get; set; }

	private double _timer;
	private bool _exploded;

	public override void _Ready()
	{
		_timer = Fuse;
		ContactMonitor = true;
		MaxContactsReported = 4;

		// No ar, sem freio: o arremesso é calculado para cair onde se mira
		// (Weapon.Grenade). Encostou, quica pouco e para logo, para ficar
		// perto de onde caiu.
		LinearDampMode = DampMode.Replace;
		LinearDamp = 0f;
		PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.05f, Friction = 1f };
		BodyEntered += _ =>
		{
			LinearDamp = 7f;
			AngularDamp = 9f;
		};

		// A granada de verdade no lugar da bola (pedido do Jhon, 18/11).
		Node3D model = WeaponModel.Build(Arsenal.GrenadeId);
		if (model.GetChildCount() > 0)
		{
			model.Name = "Modelo";
			AddChild(model);
			if (GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D ball) ball.Visible = false;
		}
		else model.Free();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_exploded) return;

		_timer -= delta;
		if (_timer <= 0)
			Explode();
	}

	private void Explode()
	{
		_exploded = true;

		var shape = new SphereShape3D { Radius = Radius };
		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = shape,
			Transform = GlobalTransform,
			CollideWithBodies = true,
			CollideWithAreas = false
		};

		var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 32);

		foreach (var hit in hits)
		{
			if (hit["collider"].Obj is not Node3D body) continue;
			if (body == this) continue;

			float distance = GlobalPosition.DistanceTo(body.GlobalPosition);
			float falloff = Mathf.Clamp(1f - distance / Radius, 0f, 1f);
			if (falloff <= 0) continue;

			var health = body.GetNodeOrNull<Health>("Health");

			// Aliado passa ileso, dano e atordoamento. Quem lançou não:
			// granada aos próprios pés tem que custar alguma coisa.
			if (health != null && Thrower != null && health != Thrower
				&& health.Team == Thrower.Team && !RoomConfig.Rules.FriendlyFire)
				continue;

			bool wasAlive = health != null && !health.IsDead;

			if (Cosmetic)
			{
				// Nada de dano: só o atordoamento, logo abaixo.
			}
			else if (health != null && health.IsRemote)
				health.SendHit(MaxDamage * falloff, GlobalPosition, false, KillCause.Grenade, Arsenal.GrenadeId, Thrower?.GetParent());
			else
			{
				// Online: veja Weapon.Damage — o juiz anota a autoria e o
				// abate volta do servidor.
				NetMatch net = Thrower != null && health != null && health != Thrower ? NetMatch.Current : null;
				net?.BeginLocalHit(Thrower.GetParent(), health, MaxDamage * falloff);
				if (Thrower != null && health != Thrower) health?.NoteAttacker(Thrower.GetParent());
				health?.TakeDamage(MaxDamage * falloff, 0, GlobalPosition);
				net?.EndLocalHit();

				if (wasAlive && health.IsDead && Thrower != null && health != Thrower)
				{
					if (net != null) net.LocalKill(health, Thrower.GetParent(), KillCause.Grenade, Arsenal.GrenadeId, false);
					else ReportKill(health);
				}
			}

			// O atordoamento é o ponto da granada, não o dano.
			if (body is Player player)
				player.ApplyStun(StunDuration * falloff, StunSlowFactor);
		}

		NoiseBus.Emit(GlobalPosition, Radius * 6f, NoiseKind.Explosion, this);
		SpawnFlash();
		QueueFree();
	}

	/// <summary>Abate de granada conta como qualquer outro: entra no
	/// feed, no placar e rende o WP de quem lançou.</summary>
	private void ReportKill(Health victim)
	{
		Thrower.GetParent()?.GetNodeOrNull<FormController>("FormController")?.OnKill();

		KillFeed.Report(new Kill(
			Thrower.PublicName, Thrower.Team,
			victim.PublicName, victim.Team,
			KillCause.Grenade, Arsenal.GrenadeId));
	}

	/// <summary>Clarão que cresce e some. Peça descartável — vira
	/// partícula de verdade na fase de polimento.</summary>
	/// <summary>A explosão que se vê (<see cref="Explosion"/>): clarão,
	/// bola de fogo, onda de choque até o raio, fagulhas, fumaça e
	/// estilhaços.</summary>
	private void SpawnFlash() => Explosion.Spawn(GetParent(), GlobalPosition, Radius);
}
