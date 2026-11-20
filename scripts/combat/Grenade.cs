using Godot;

/// <summary>
/// A granada do jogo: **explosiva** desde 19/11 (decisão do Jhon: "a
/// granada do jogo deve ser a explosiva, não a de energia"). Mata de
/// perto e fere até a borda do raio; não atordoa. A de choque (o estouro
/// elétrico que desacelera) continua aqui, pronta, em
/// <see cref="GrenadeKind.Shock"/>.
///
/// Fase 2: o arremesso vira intenção enviada ao servidor, e a
/// explosão é resolvida lá. Projétil com física é justamente o caso
/// em que cliente e servidor divergem se a simulação não for a mesma.
/// </summary>
public partial class Grenade : RigidBody3D
{
	/// <summary>Tempo até explodir depois de encostar em algo, em segundos.</summary>
	[Export] public float Fuse = 1.2f;

	/// <summary>O pavio só começa a contar quando a granada encosta em
	/// algo (pedido do Jhon, 18/11: "só começa o timer de explodir ao cair
	/// no chão"). Se nunca encostar (caiu para fora do mapa), explode
	/// depois deste tempo de voo.</summary>
	[Export] public float MaxFlight = 6f;

	/// <summary>Quanto da velocidade sobra no primeiro toque: a granada
	/// fica onde caiu.</summary>
	[Export] public float LandingKeep = 0.08f;

	/// <summary>Já encostou: o pavio está contando. Para os testes.</summary>
	public bool Landed { get; private set; }

	[Export] public float Radius = 6.5f;

	/// <summary>Dano no centro da explosão. Cai linearmente até a borda.</summary>
	[Export] public float MaxDamage = 260f;

	/// <summary>
	/// O tipo da granada. **Fragmentação** (a do jogo desde 19/11): a
	/// explosão de fogo que mata de perto (<see cref="MaxDamage"/>).
	/// **Choque** (a de 18/11, guardada): um estouro elétrico que
	/// desacelera quem está perto e atordoa o inimigo, com pouco dano
	/// (<see cref="ShockDamage"/>).
	/// </summary>
	public enum GrenadeKind { Shock, Frag }

	[Export] public GrenadeKind Kind = GrenadeKind.Frag;

	/// <summary>Dano da granada de choque no centro (o ponto dela é o
	/// atordoamento, não o dano).</summary>
	[Export] public float ShockDamage = 60f;

	/// <summary>O dano no centro, pelo tipo.</summary>
	public float CenterDamage => Kind == GrenadeKind.Frag ? MaxDamage : ShockDamage;

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
		_timer = MaxFlight;
		ContactMonitor = true;
		MaxContactsReported = 4;

		// No ar, sem freio: o arremesso é calculado para cair onde se mira
		// (Weapon.Grenade). Encostou, fica: perde quase toda a velocidade no
		// primeiro toque e não rola (19/11: "o arremesso da granada deve ser
		// extremamente preciso" — com o freio de antes ela ainda escorregava
		// mais de um metro depois de cair no ponto).
		LinearDampMode = DampMode.Replace;
		LinearDamp = 0f;
		PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.05f, Friction = 1f };
		BodyEntered += _ =>
		{
			if (!Landed)
			{
				Landed = true;
				_timer = Fuse;
				LinearVelocity *= LandingKeep;
				AngularVelocity *= LandingKeep;
			}
			LinearDamp = 12f;
			AngularDamp = 12f;
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
				health.SendHit(CenterDamage * falloff, GlobalPosition, false, KillCause.Grenade, Arsenal.GrenadeId, Thrower?.GetParent());
			else
			{
				// Online: veja Weapon.Damage — o juiz anota a autoria e o
				// abate volta do servidor.
				NetMatch net = Thrower != null && health != null && health != Thrower ? NetMatch.Current : null;
				net?.BeginLocalHit(Thrower.GetParent(), health, CenterDamage * falloff);
				if (Thrower != null && health != Thrower) health?.NoteAttacker(Thrower.GetParent());
				health?.TakeDamage(CenterDamage * falloff, 0, GlobalPosition);
				net?.EndLocalHit();

				if (wasAlive && health.IsDead && Thrower != null && health != Thrower)
				{
					if (net != null) net.LocalKill(health, Thrower.GetParent(), KillCause.Grenade, Arsenal.GrenadeId, false);
					else ReportKill(health);
				}
			}

			// O atordoamento é o ponto da granada, não o dano.
			// Só a de choque atordoa: jogador e bot, na medida da distância.
			if (Kind == GrenadeKind.Shock)
			{
				if (body is Player player) player.ApplyStun(StunDuration * falloff, StunSlowFactor);
				else if (body is Bot bot) bot.ApplyStun(StunDuration * falloff, StunSlowFactor);
			}
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
	/// <summary>A explosão que se vê. Fragmentação: o fogo
	/// (<see cref="Explosion"/>). Choque: o estouro elétrico
	/// (<see cref="ShockBlast"/>).</summary>
	private void SpawnFlash()
	{
		if (Kind == GrenadeKind.Frag) Explosion.Spawn(GetParent(), GlobalPosition, Radius);
		else ShockBlast.Spawn(GetParent(), GlobalPosition, Radius);
	}
}
