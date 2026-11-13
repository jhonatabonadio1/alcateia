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
	/// enquanto acertava.</summary>
	[ExportGroup("Armas de fogo")]
	[Export] public bool ShowTracer = true;
	[Export] public Color TracerColor = new Color(1f, 0.9f, 0.5f, 0.9f);
	[Export] public float TracerLife = 0.08f;

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
	public float MoveSpeedMultiplier => Held?.SpeedMultiplier ?? 1f;

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
		// ele já quis dar.
		if (AmmoInMag <= 0)
		{
			TryReload();
			return;
		}

		if (primary) PrimaryAmmo--;
		else SecondaryAmmo--;

		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);

		_cooldown = held.Interval;

		// A dispersão deste tiro é a de antes dele; o tiro só piora o
		// próximo. Primeiro tiro parado é sempre o mais preciso.
		float spread = SpreadNow(held);

		_streak++;
		_sinceShot = 0;
		_bloom = CombatRules.BloomAfterShot(_bloom, held.SpreadPerShot, held.SpreadMax, held.SpreadBase);

		Recoil = CombatRules.RecoilVertical(_streak, held.RecoilEarlyShots, held.RecoilVertical, held.RecoilVerticalLate);

		// Horizontal alternado é decorável; depois do limite, sorteado. É
		// o que faz o AK ser arma de rajada e não de spray.
		bool wild = held.RecoilHorizontalAfter > 0 && _streak >= held.RecoilHorizontalAfter;
		float side = wild ? held.RecoilHorizontalLate : held.RecoilHorizontal;
		RecoilYaw = wild ? (float)GD.RandRange(-side, side) : (_streak % 2 == 0 ? side : -side);

		EmitSignal(SignalName.Fired);

		NoiseBus.Emit(_body.GlobalPosition, held.NoiseRadius, NoiseKind.Shot, _body);

		Vector3 from = _camera.GlobalPosition;
		Basis aim = _camera.GlobalTransform.Basis;

		// Um raio por chumbo — um só nas armas que não são escopeta. O
		// dano de todos os chumbos que acertaram o mesmo alvo é somado
		// antes de aplicar, para o abate e o marcador saírem uma vez por
		// disparo, e não nove.
		var dealt = new Dictionary<Health, (float Damage, bool Headshot)>();
		Vector3 tracerEnd = from - aim.Z * held.Range;

		int pellets = Mathf.Max(held.Pellets, 1);

		for (int i = 0; i < pellets; i++)
		{
			Vector3 direction = Scatter(aim, spread);
			Vector3 to = from + direction * held.Range;

			Health hit = Raycast(from, to, out Vector3 impact, out Node collider);

			// A linha para na parede em vez de atravessá-la.
			if (i == 0) tracerEnd = impact;
			if (hit == null) continue;

			bool headshot = IsHeadshot(collider, impact);

			float damage = CombatRules.FirearmDamage(
				held.Damage, held.Falloff(from.DistanceTo(impact)), headshot, held.HeadshotMultiplier);

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
			_body is Player player && player.IsCrouching, held.CrouchSpreadMultiplier);
	}

	/// <summary>Direção sorteada num cone de meio-ângulo dado. A raiz no
	/// raio distribui por área; sem ela os tiros se amontoam no centro e o
	/// cone vale menos do que o número diz.</summary>
	private static Vector3 Scatter(Basis aim, float halfAngleDegrees)
	{
		Vector3 forward = -aim.Z;
		if (halfAngleDegrees <= 0.001f) return forward.Normalized();

		float radius = Mathf.Tan(Mathf.DegToRad(halfAngleDegrees)) * Mathf.Sqrt(GD.Randf());
		float angle = GD.Randf() * Mathf.Tau;

		return (forward + aim.X * (Mathf.Cos(angle) * radius) + aim.Y * (Mathf.Sin(angle) * radius)).Normalized();
	}

	/// <summary>Escopeta no lobo: lentidão curta. Só em lobo, e só em
	/// quem tem corpo que sabe ficar lento.</summary>
	private static void StunWolf(Health target, float seconds)
	{
		if (target.GetParent() is not Player player) return;

		var form = player.GetNodeOrNull<FormController>("FormController");
		if (form == null || !form.IsWolf) return;

		player.ApplyStun(seconds, 0.5f);
	}

	/// <summary>
	/// Material único, compartilhado por todos os traçantes do jogo.
	///
	/// Criar um material por disparo força o Godot a montar pipeline de
	/// render na hora. Com dois bots atirando nove vezes por segundo,
	/// isso trava o quadro — o jogador sente como congelamento no
	/// instante em que levam tiro nele.
	/// </summary>
	private static StandardMaterial3D _tracerMaterial;

	private static StandardMaterial3D TracerMaterial(Color color)
	{
		return _tracerMaterial ??= new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			AlbedoColor = color,
			NoDepthTest = false
		};
	}

	/// <summary>Ponta do cano, quando há arma visível na mão.</summary>
	private Vector3 MuzzleOrigin(Vector3 fallback)
	{
		_viewModel ??= _body.GetNodeOrNull<Node3D>("Head/Camera3D/ViewModel") as ViewModel;

		if (_viewModel != null && _viewModel.TryGetMuzzle(out Vector3 muzzle))
			return muzzle;

		return fallback;
	}

	/// <summary>Linha fina que aparece e some. Peça descartável — vira
	/// efeito de verdade na fase de polimento.</summary>
	private void Tracer(Vector3 from, Vector3 to)
	{
		if (!ShowTracer) return;

		var mesh = new ImmediateMesh();

		mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, TracerMaterial(TracerColor));
		mesh.SurfaceAddVertex(Vector3.Zero);
		mesh.SurfaceAddVertex(to - from);
		mesh.SurfaceEnd();

		var node = new MeshInstance3D { Mesh = mesh };
		_body.GetTree().CurrentScene.AddChild(node);
		node.GlobalPosition = from;

		// Timer simples em vez de Tween: nada a animar, já que o
		// material é compartilhado e não pode ser esmaecido sozinho.
		SceneTreeTimer timer = _body.GetTree().CreateTimer(TracerLife);
		timer.Timeout += node.QueueFree;
	}

	private Health Raycast(Vector3 from, Vector3 to, out Vector3 impact, out Node collider)
	{
		impact = to;
		collider = null;

		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		query.CollideWithAreas = false;

		var result = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (result.Count == 0)
		{
			if (Debug) GD.Print("ataque: não acertou nada");
			return null;
		}

		impact = (Vector3)result["position"];

		if (result["collider"].Obj is not Node node)
			return null;

		collider = node;

		Health health = node.GetNodeOrNull<Health>("Health");

		if (Debug)
		{
			GD.Print(health == null
				? $"ataque: acertou '{node.Name}', que não tem um filho chamado Health"
				: $"ataque: acertou '{node.Name}'");
		}

		return health;
	}
}
