using Godot;
using System.Collections.Generic;

/// <summary>
/// Armas de fogo: o disparo, a dispersão que cresce com a rajada, o
/// recuo, o traçante e o estampido. Os números de cada arma vêm do
/// arsenal (WeaponDefinition, em resources/weapons).
/// </summary>
public partial class Weapon
{
	/// <summary>Traçante do disparo. Sem nenhum retorno visual não há
	/// como saber que alguém atirou em você — o bot parecia inofensivo
	/// enquanto acertava. O efeito (a bala que viaja, o rastro, o clarão do
	/// cano) e os números dele estão em <see cref="BulletTracer"/>.</summary>
	[ExportGroup("Armas de fogo")]
	[Export] public bool ShowTracer = true;
	[Export] public Color TracerColor = new Color(1f, 0.72f, 0.32f, 0.95f);

	/// <summary>Raio do estampido. Atirar sempre entrega a posição —
	/// é o custo de usar a arma.</summary>
	[Export] public float ShotNoise = 60f;

	/// <summary>Dispara segurando o botão? Quem decide é a arma.</summary>
	public bool IsAutomatic => Held != null && Held.IsAutomatic;

	/// <summary>Dispersão acumulada pelos tiros, acima da base.</summary>
	private float _bloom;
	private double _sinceShot = 10;

	/// <summary>Tiros seguidos: é o que decide em que fase do recuo a
	/// arma está (o M16 coiceia forte nos quatro primeiros, o AK fica
	/// imprevisível depois do sexto).</summary>
	private int _streak;

	/// <summary>Recuo vertical do último tiro, em graus.</summary>
	public float Recoil { get; private set; }

	/// <summary>Recuo horizontal do último tiro, em graus.</summary>
	public float RecoilYaw { get; private set; }

	/// <summary>Velocidade de caminhada com a arma da mão (1 = base).</summary>
	public float MoveSpeedMultiplier => (Held?.SpeedMultiplier ?? (HumanSlot == Slot.Knife ? KnifeSpeedMultiplier : 1f)) * AimSpeedFactor;

	/// <summary>Dispersão de agora, em graus — para a mira poder crescer junto.</summary>
	public float CurrentSpread => Held == null ? 0f : SpreadNow(Held);

	private void Shoot()
	{
		WeaponDefinition held = Held;
		if (held == null) return;

		// Na escopeta o tiro interrompe a recarga cartucho a cartucho; nas
		// outras, recarga é recarga.
		if (IsReloading)
		{
			if (_shellReload && AmmoInMag > 0) CancelReload();
			else return;
		}

		bool primary = HumanSlot == Slot.Primary;

		// Carregador vazio recarrega sozinho: obrigar o jogador a
		// apertar R depois de ouvir o clique só adiciona um passo que
		// ele já quis dar. Sem reserva, é o clique seco.
		if (AmmoInMag <= 0)
		{
			if (!TryReload())
			{
				_cooldown = 0.3f;
				EmitSignal(SignalName.DryFired);
			}
			return;
		}

		if (primary) PrimaryAmmo--;
		else SecondaryAmmo--;

		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);

		_cooldown = held.Interval;

		// A mira deste tiro é a de antes dele. O recuo sobe a câmera no
		// sinal Fired (Player.OnWeaponFired), então ela tem de ser lida
		// antes: lida depois, todo tiro saía 1° acima da cruz — a 15 m,
		// 26 cm, por cima da cabeça de quem se mirou nela (bug do Jhon,
		// 14/11: "mirei escopado na cabeça e não deu dano").
		Vector3 from = _camera.GlobalPosition;
		Basis aim = _camera.GlobalTransform.Basis;

		// A dispersão deste tiro também é a de antes dele; o tiro só
		// piora o próximo. Primeiro tiro parado é sempre o mais preciso.
		float spread = SpreadNow(held);

		_streak++;
		_sinceShot = 0;
		_bloom = CombatRules.BloomAfterShot(_bloom, held.SpreadPerShot, held.SpreadMax, held.SpreadBase);

		Recoil = CombatRules.RecoilVertical(_streak, held.RecoilEarlyShots, held.RecoilVertical, held.RecoilVerticalLate);

		// Horizontal alternado é decorável; depois do limite, sorteado. É
		// o que faz o AK ser arma de rajada e não de spray.
		bool wild = held.RecoilHorizontalAfter > 0 && _streak >= held.RecoilHorizontalAfter;
		float side = wild ? held.RecoilHorizontalLate : held.RecoilHorizontal;
		RecoilYaw = wild ? _rng.Range(-side, side) : (_streak % 2 == 0 ? side : -side);
		// Na mira, o coice da arma que pede menos (a SCAR escopada).
		float steady = Mathf.Lerp(1f, held.AimRecoilMultiplier, AimEase);
		Recoil *= steady;
		RecoilYaw *= steady;

		EmitSignal(SignalName.Fired);

		NoiseBus.Emit(_body.GlobalPosition, held.NoiseRadius, NoiseKind.Shot, _body);

		// Um raio por chumbo — um só nas armas que não são escopeta. O
		// dano de todos os chumbos que acertaram o mesmo alvo é somado
		// antes de aplicar, para o abate e o marcador saírem uma vez por
		// disparo, e não nove.
		var dealt = new Dictionary<Health, (float Damage, bool Headshot)>();
		Vector3 tracerEnd = from - aim.Z * held.Range;

		int pellets = Mathf.Max(held.Pellets, 1);
		List<Target> targets = Targets();

		for (int i = 0; i < pellets; i++)
		{
			Vector3 direction = Scatter(aim, spread);
			Vector3 to = from + direction * held.Range;

			Health hit = Trace(from, to, targets, true, out Vector3 impact, out Node collider, out bool headshot);

			// A linha para na parede em vez de atravessá-la.
			if (i == 0)
			{
				tracerEnd = impact;
				Whiz(from, impact);
			}
			if (hit == null)
			{
				if (collider != null)
				{
					// O furo de cada bala (de cada chumbo, na escopeta); o estalo, um por disparo.
					BulletMarks.Add(_body.GetParent(), impact, _lastNormal, collider);
					if (i == 0) EmitSignal(SignalName.WorldHit, impact, _lastNormal, collider);
				}
				continue;
			}

			float damage = CombatRules.FirearmDamage(
				held.Damage, held.Falloff(from.DistanceTo(impact)), headshot, held.HeadshotMultiplier);

			// Sangue no ponto do acerto, uma vez por alvo por disparo (a
			// escopeta não faz nove esguichos), empurrado na direção do
			// tiro. Antes do dano, porque o boneco de outro jogador
			// também atira e não aplica dano — mas o sangue se vê.
			if (!dealt.ContainsKey(hit))
				Bleed(hit, impact, direction, headshot ? 1.6f : 1f);

			dealt.TryGetValue(hit, out var sum);
			dealt[hit] = (sum.Damage + damage, sum.Headshot || headshot);
		}

		// A mira sai da câmera, porque é dela que vem a precisão; a
		// linha sai do cano, porque é de lá que o jogador espera vê-la.
		Tracer(MuzzleOrigin(from), tracerEnd);

		foreach (var (target, result) in dealt)
		{
			Damage(target, result.Damage, from, result.Headshot);

			if (held.WolfStun > 0)
				StunWolf(target, held.WolfStun);
		}
	}

	/// <summary>
	/// Dispersão do próximo tiro: base, mais o acumulado, vezes o
	/// movimento e a postura. Andar multiplica — e quanto multiplica é a
	/// assinatura de cada arma: 1,3 na UMP, 3,0 no AK.
	/// </summary>
	private float SpreadNow(WeaponDefinition held)
	{
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		return CombatRules.Spread(held.SpreadBase, _bloom,
			flat.Length() > CombatRules.MovingSpreadSpeed, held.MoveSpreadMultiplier,
			_body is Player player && player.IsCrouching, held.CrouchSpreadMultiplier)
			// Na mira, o cone fecha (Weapon.Aim).
			* AimSpreadFactor;
	}

	/// <summary>Direção sorteada num cone de meio-ângulo dado. A raiz no
	/// raio distribui por área; sem ela os tiros se amontoam no centro e o
	/// cone vale menos do que o número diz.</summary>
	private Vector3 Scatter(Basis aim, float halfAngleDegrees)
	{
		Vector3 forward = -aim.Z;
		if (halfAngleDegrees <= 0.001f) return forward.Normalized();

		float radius = Mathf.Tan(Mathf.DegToRad(halfAngleDegrees)) * Mathf.Sqrt(_rng.Unit());
		float angle = _rng.Unit() * Mathf.Tau;

		return (forward + aim.X * (Mathf.Cos(angle) * radius) + aim.Y * (Mathf.Sin(angle) * radius)).Normalized();
	}

	/// <summary>Escopeta no lobo: lentidão curta. Só em lobo, e só em
	/// quem tem corpo que sabe ficar lento.</summary>
	/// <summary>Até que distância da cabeça a bala "passa de raspão" (m).</summary>
	[Export] public float WhizRange = 1.7f;

	private static ulong _lastWhiz;

	/// <summary>
	/// A bala passando de raspão (pedido do Jhon, 18/11): se o tiro de
	/// outro corpo passa a menos de <see cref="WhizRange"/> da cabeça de
	/// quem joga nesta máquina — pegando ou não —, toca o "vuum" no ponto
	/// mais próximo da linha do tiro, mais alto quanto mais perto. Tiro de
	/// aliado (sem fogo amigo) não assobia. No máximo um a cada 70 ms: uma
	/// rajada vira uma sequência, não um chiado só.
	/// </summary>
	private void Whiz(Vector3 from, Vector3 to)
	{
		if (_body is Player shooter && shooter.IsMultiplayerAuthority()) return;
		Camera3D ear = _body.GetViewport()?.GetCamera3D();
		if (ear == null) return;

		Player me = null;
		foreach (Node node in _body.GetTree().GetNodesInGroup(Groups.Players))
			if (node is Player player && player.IsMultiplayerAuthority()) { me = player; break; }
		if (me == null || me == _body || me.GetNodeOrNull<Health>("Health") is not Health mine || mine.IsDead) return;
		if (_health != null && mine.Team == _health.Team && !RoomConfig.Rules.FriendlyFire) return;

		Vector3 head = ear.GlobalPosition;
		Vector3 line = to - from;
		float length = line.Length();
		if (length < 2f) return;
		float along = Mathf.Clamp((head - from).Dot(line) / (length * length), 0f, 1f);
		// O tiro que morre antes de chegar perto (parede no caminho) não passou.
		if (along * length < 1.5f) return;
		Vector3 nearest = from + line * along;
		float miss = nearest.DistanceTo(head);
		if (miss > WhizRange) return;

		ulong now = Time.GetTicksMsec();
		if (now - _lastWhiz < 70) return;
		_lastWhiz = now;
		Sfx.At("bala_raspao", nearest, 5f, Mathf.Lerp(1f, -7f, miss / WhizRange), 8f);
	}

	private static void StunWolf(Health target, float seconds)
	{
		if (target.GetParent() is not Player player) return;

		var form = player.GetNodeOrNull<FormController>("FormController");
		if (form == null || !form.IsWolf) return;

		player.ApplyStun(seconds, 0.5f);
	}

	// o último cano era o da arma da primeira pessoa (que tem o clarão dela)
	private bool _muzzleFirstPerson;

	/// <summary>Ponta do cano, quando há arma visível na mão.</summary>
	private Vector3 MuzzleOrigin(Vector3 fallback)
	{
		// A arma na mão em primeira pessoa (a de quem joga, ou a que a
		// câmera de abate e o espectador põem no olho de um bot) pode
		// entrar e sair: confere de novo quando some.
		if (_viewModel != null && !IsInstanceValid(_viewModel)) _viewModel = null;
		_viewModel ??= _body.GetNodeOrNull<Node3D>("Head/Camera3D/ViewModel") as ViewModel;

		_muzzleFirstPerson = false;
		if (_viewModel != null && _viewModel.TryGetMuzzle(out Vector3 muzzle))
		{
			_muzzleFirstPerson = true;
			return muzzle;
		}

		// Visto de fora (o bot, o boneco de outro jogador): o cano da arma
		// na mão do corpo. Antes saía do olho, e o tiro dos bots parecia
		// não sair da arma.
		if (!_form.IsWolf && _body.GetNodeOrNull("Human/HeldWeapon") is Node held
			&& held.FindChild(WeaponModel.MuzzleName, true, false) is Node3D tip && tip.IsVisibleInTree())
			return tip.GlobalPosition;

		return fallback;
	}

	/// <summary>O traçante, do cano até onde o tiro parou (<see cref="BulletTracer"/>).</summary>
	private void Tracer(Vector3 from, Vector3 to)
	{
		// No servidor sem tela ninguém vê: só gastaria um nó e uma malha por disparo.
		if (!ShowTracer || DisplayServer.GetName() == "headless") return;
		BulletTracer.Spawn(_body.GetTree().CurrentScene, from, to, TracerColor, flash: !_muzzleFirstPerson);
	}

	/// <summary>A normal da superfície do último raio que bateu.</summary>
	private Vector3 _lastNormal = Vector3.Up;

	/// <summary>
	/// Aliado com o fogo amigo desligado: o tiro passa por ele (19/11, Jhon:
	/// "a bala deve atravessar o meu time"). Antes o raio parava no aliado
	/// da frente e o tiro se perdia sem ferir ninguém.
	/// </summary>
	private bool PassesThrough(Health target) =>
		target != null && _health != null && target != _health
		&& target.Team == _health.Team && !RoomConfig.Rules.FriendlyFire;

	/// <param name="skip">Os corpos com modelo: levam tiro pelas peças, não pela cápsula.</param>
	private Health Raycast(Vector3 from, Vector3 to, out Vector3 impact, out Node collider, List<Target> skip = null)
	{
		impact = to;
		collider = null;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		var excluded = new Godot.Collections.Array<Rid> { _body.GetRid() };
		if (skip != null)
			foreach (Target target in skip)
				if (target.Body is CollisionObject3D solid) excluded.Add(solid.GetRid());
		query.Exclude = excluded;
		query.CollideWithAreas = false;

		PhysicsDirectSpaceState3D space = _body.GetWorld3D().DirectSpaceState;
		// Um aliado atrás do outro: no máximo alguns, o resto é parede.
		for (int pass = 0; pass < 8; pass++)
		{
			var result = space.IntersectRay(query);

			if (result.Count == 0)
			{
				if (Debug) GD.Print("attack: hit nothing");
				return null;
			}

			impact = (Vector3)result["position"];
			_lastNormal = (Vector3)result["normal"];

			if (result["collider"].Obj is not Node node)
				return null;

			Health health = node.GetNodeOrNull<Health>("Health");
			if (PassesThrough(health) && node is CollisionObject3D ally)
			{
				Godot.Collections.Array<Rid> exclude = query.Exclude;
				exclude.Add(ally.GetRid());
				query.Exclude = exclude;
				impact = to;
				continue;
			}

			collider = node;

			if (Debug)
			{
				GD.Print(health == null
					? $"attack: hit '{node.Name}', which has no child named Health"
					: $"attack: hit '{node.Name}'");
			}

			return health;
		}

		return null;
	}
}
