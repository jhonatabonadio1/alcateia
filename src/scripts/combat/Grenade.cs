using Godot;
using System.Collections.Generic;

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

	/// <summary>Quanto ela quica (0 = nada, 1 = sem perder nada), o atrito
	/// com o chão e o freio de quem já está rolando (pedido do Jhon,
	/// 20/11: "a granada deve quicar mais no chão, mais natural e seguir a
	/// física"). Antes ela perdia 92% da velocidade no primeiro toque e
	/// ficava parada onde caiu; agora cai no ponto mirado, quica, rola um
	/// pouco e para — o pavio conta desde o primeiro toque.</summary>
	[Export] public float Bounciness = 0.42f;
	[Export] public float GroundFriction = 0.85f;
	[Export] public float RollDamp = 1.4f;
	[Export] public float SpinDamp = 7f;

	/// <summary>Já encostou: o pavio está contando. Para os testes.</summary>
	public bool Landed { get; private set; }

	/// <summary>Onde foi o primeiro toque (o ponto da superfície). Para os testes.</summary>
	public Vector3 LandedAt { get; private set; }

	/// <summary>O raio da explosão (m). Era 6,5; o Jhon pediu maior (20/11).</summary>
	[Export] public float Radius = 9f;

	/// <summary>Dano no centro da explosão. Cai linearmente até a borda: com 420
	/// no centro, mata quem tem 200 de vida a até 4,7 m; a 7 m ainda tira 93.
	/// (Era 260, que só matava colado; o Jhon pediu "bem maior", 20/11.)</summary>
	[Export] public float MaxDamage = 420f;

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

	/// <summary>Qual granada se vê voando: a comum ou a versão que quem
	/// lançou tem no conjunto (<see cref="Arsenal.Grenades"/>). Só a
	/// aparência; a explosão é a mesma.</summary>
	public string ModelId { get; set; } = Arsenal.GrenadeId;

	/// <summary>Online: a granada de outro jogador, encenada aqui. Faz o
	/// clarão, o barulho e o atordoamento em você; o dano vem da máquina
	/// de quem arremessou.</summary>
	public bool Cosmetic { get; set; }

	/// <summary>De que tamanho a granada se vê voando, em relação à de verdade (só a aparência:
	/// a colisão não muda). A da mão em primeira pessoa é 1,40 da de verdade
	/// (tools/armas/conjuntos.py, GRANADA; Jhon, 23/11: "granada está muito pequena"), e a que
	/// sai dela tem o mesmo tamanho.</summary>
	[Export] public float ModelScale = 1.4f;

	/// <summary>O som de cada batida (pedido do Jhon, 25/11: "a granada, ao
	/// cair, tem que fazer um barulho de algo caindo no chão e respeitando a
	/// física a cada batida"). Um por batida de verdade, no volume da
	/// velocidade com que ela bateu: o primeiro toque soa cheio, cada quique
	/// mais baixo, e rolando não soa.</summary>
	[ExportGroup("Batida")]
	[Export] public string BounceSound = "granada_quique";

	/// <summary>A velocidade de batida (m/s, na direção da superfície) que
	/// soa a 0 dB; o arremesso longo bate a uns 12.</summary>
	[Export] public float BounceLoudSpeed = 7f;

	/// <summary>Abaixo disto a batida não soa (é rolar, não bater).</summary>
	[Export] public float BounceMinSpeed = 0.45f;

	/// <summary>Até onde a batida se ouve (m).</summary>
	[Export] public float BounceRange = 25f;

	/// <summary>Tempo mínimo entre duas batidas que soam (s): o mesmo toque
	/// pode durar dois passos de física.</summary>
	[Export] public float BounceGap = 0.07f;

	/// <summary>Cada batida que soou: o tempo de voo e a velocidade dela. Para os testes.</summary>
	public List<(double Time, float Speed)> Bounces { get; } = new();

	private double _timer;
	private double _alive;
	private double _lastBounce = -1;
	private Vector3 _lastVelocity;
	private bool _exploded;

	public override void _Ready()
	{
		_timer = MaxFlight;
		ContactMonitor = true;
		MaxContactsReported = 4;

		// No ar, sem freio: o arremesso é calculado para cair onde se mira
		// (Weapon.Grenade) — o primeiro toque é no ponto da cruz. Dali em
		// diante é física: quica, rola e para (20/11). Rápida e pequena, ela
		// atravessava quina fina num passo: colisão contínua.
		LinearDampMode = DampMode.Replace;
		AngularDampMode = DampMode.Replace;
		LinearDamp = 0f;
		AngularDamp = 0f;
		ContinuousCd = true;
		PhysicsMaterialOverride = new PhysicsMaterial { Bounce = Bounciness, Friction = GroundFriction };
		// O primeiro toque também se vê pela frente (ver _PhysicsProcess): quicando,
		// o toque e a saída cabem num passo só e o aviso de contato pode não vir.
		BodyEntered += _ => Land(GlobalPosition);

		// A granada de verdade no lugar da bola (pedido do Jhon, 18/11).
		Node3D model = WeaponModel.Build(Arsenal.IsGrenade(ModelId) ? ModelId : Arsenal.GrenadeId);
		if (model.GetChildCount() > 0)
		{
			model.Name = "Modelo";
			model.Scale = Vector3.One * ModelScale;
			AddChild(model);
			if (GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D ball) ball.Visible = false;
		}
		else model.Free();
	}

	private void Land(Vector3 at)
	{
		if (!Landed)
		{
			Landed = true;
			LandedAt = at;
			_timer = Fuse;
		}
		LinearDamp = RollDamp;
		AngularDamp = SpinDamp;
	}

	// O toque de verdade, no ponto de contato: quicando, quando o aviso
	// BodyEntered chega ela já subiu e andou.
	public override void _IntegrateForces(PhysicsDirectBodyState3D state)
	{
		if (Landed || state.GetContactCount() == 0) return;
		for (int i = 0; i < state.GetContactCount(); i++)
			if (state.GetContactColliderObject(i) is not CharacterBody3D)
			{
				Land(state.GetContactColliderPosition(i));
				return;
			}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_exploded) return;
		_alive += delta;
		Knock((float)delta);

		// No ar: o que está no caminho deste passo? Se há, o toque é agora, ali.
		if (!Landed)
		{
			Vector3 step = LinearVelocity * (float)delta;
			var query = PhysicsRayQueryParameters3D.Create(GlobalPosition, GlobalPosition + step + step.Normalized() * 0.16f);
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
			if (Thrower?.GetParent() is CollisionObject3D owner) query.Exclude.Add(owner.GetRid());
			var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (hit.Count > 0 && hit["collider"].Obj is not CharacterBody3D) Land((Vector3)hit["position"]);
		}

		_timer -= delta;
		if (_timer <= 0)
			Explode();
	}

	/// <summary>
	/// A batida, pela física: o que a velocidade mudou neste passo além da
	/// gravidade e do freio é o que o chão (ou a parede) devolveu. Quicando,
	/// a mudança é (1 + quique) × a velocidade com que ela chegou, na
	/// direção da superfície; rolando, quase nada. Lida assim vale para
	/// chão, parede e teto, e não depende de o aviso de contato chegar.
	/// </summary>
	private void Knock(float delta)
	{
		Vector3 velocity = LinearVelocity;
		Vector3 expected = _lastVelocity + GetGravity() * GravityScale * delta;
		expected *= Mathf.Max(0f, 1f - LinearDamp * delta);
		_lastVelocity = velocity;
		if (_alive < 2 * delta) return;              // o primeiro passo é o arremesso, não batida

		float speed = (velocity - expected).Length() / (1f + Bounciness);
		if (speed < BounceMinSpeed || _alive - _lastBounce < BounceGap) return;

		_lastBounce = _alive;
		Bounces.Add((_alive, speed));
		// O volume segue a energia da batida: metade da velocidade, 6 dB a menos.
		float db = Mathf.Clamp(20f * Mathf.Log(speed / BounceLoudSpeed) / Mathf.Log(10f), -26f, 3f);
		Sfx.At(BounceSound, GlobalPosition, BounceRange, db);
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

		// Quem a explosão pega. Jogadores e bots vêm direto da lista de quem
		// luta: pela física, a consulta devolve no máximo N corpos, e com o
		// mapa feito de centenas de peças (cada parede, degrau e caixa é um
		// corpo) as respostas acabavam em parede e chão antes de chegar em
		// alguém — a granada não feria ninguém, nem quem lançou (Jhon, 20/11:
		// "nunca matei alguém com granada"). A física fica só para o resto
		// que tem vida (alvo de treino, objeto que quebra).
		var targets = new List<Node3D>();
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				// Boneco de jogador que ainda não chegou (sem camada) não está no mundo.
				if (node is CharacterBody3D fighter && fighter.IsInsideTree() && fighter.CollisionLayer != 0 && !targets.Contains(fighter))
					targets.Add(fighter);
		foreach (var hit in GetWorld3D().DirectSpaceState.IntersectShape(query, 64))
			if (hit["collider"].Obj is Node3D thing && thing != this && !targets.Contains(thing)
				&& !thing.IsInGroup(Groups.Players) && !thing.IsInGroup(Groups.Bots)
				&& thing.GetNodeOrNull<Health>("Health") != null)
				targets.Add(thing);

		foreach (Node3D body in targets)
		{

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
			// Acertou inimigo vivo: quem lançou ouve o acerto, como no tiro.
			bool enemy = wasAlive && Thrower != null && health != Thrower && health.Team != Thrower.Team;

			if (Cosmetic)
			{
				// Nada de dano: só o atordoamento, logo abaixo.
			}
			else if (health != null && health.IsRemote)
			{
				health.SendHit(CenterDamage * falloff, GlobalPosition, false, KillCause.Grenade, Arsenal.GrenadeId, Thrower?.GetParent());
				if (enemy) ThrowerWeapon?.ConfirmHit(CenterDamage * falloff);
			}
			else
			{
				// Online: veja Weapon.Damage — o juiz anota a autoria e o
				// abate volta do servidor.
				NetMatch net = Thrower != null && health != null && health != Thrower ? NetMatch.Current : null;
				net?.BeginLocalHit(Thrower.GetParent(), health, CenterDamage * falloff);
				if (Thrower != null && health != Thrower) health?.NoteAttacker(Thrower.GetParent());

				// A própria granada: fere mesmo com a proteção de quem nasceu
				// (a proteção é contra os outros) e, se matar, o feed diz "de
				// granada" (Health.TakeOwnDamage).
				if (health != null && health == Thrower) health.TakeOwnDamage(CenterDamage * falloff, GlobalPosition, KillCause.Grenade, Arsenal.GrenadeId);
				else health?.TakeDamage(CenterDamage * falloff, 0, GlobalPosition);
				net?.EndLocalHit();
				if (enemy) ThrowerWeapon?.ConfirmHit(CenterDamage * falloff);

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

	private Weapon ThrowerWeapon => Thrower?.GetParent()?.GetNodeOrNull<Weapon>("Weapon");

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
