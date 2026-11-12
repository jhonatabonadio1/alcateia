using Godot;

public enum Slot
{
	Primary = 1,
	Secondary = 2,
	Wolf = 3,
	Knife = 4,
	Grenade = 5
}

/// <summary>
/// Sistema de slots e ataque.
///
/// Transformar não é um botão à parte: o lobo ocupa o slot 3, na mesma
/// memória muscular de trocar de arma. Apertar 1, 2 ou 4 estando lobo
/// volta para humano e seleciona aquele slot.
///
/// Fase 2: o cliente manda "ataquei, olhando para cá, no tick N" e o
/// SERVIDOR rebobina as hitboxes para o instante que o atirador viu
/// (lag compensation) e decide o acerto. Nada aqui é autoritativo.
/// </summary>
public partial class Weapon : Node3D
{
	[Signal] public delegate void DamageDealtEventHandler(float amount);
	[Signal] public delegate void FiredEventHandler();
	[Signal] public delegate void SlotChangedEventHandler(int slot);

	/// <summary>Acertou alguém. O HUD usa para marcar no crosshair —
	/// sem confirmação visual não se sabe se o tiro pegou.</summary>
	[Signal] public delegate void HitConfirmedEventHandler(bool headshot);
	[Signal] public delegate void GrenadesChangedEventHandler(int left);

	// --- Arma principal: cadência alta, dano baixo, automática ---
	[Export] public float PrimaryDamage = 28f;
	[Export] public float PrimaryRange = 120f;
	[Export] public float PrimaryCooldown = 0.11f;

	// --- Arma secundária: precisa, lenta, semiautomática ---
	[Export] public float SecondaryDamage = 65f;
	[Export] public float SecondaryRange = 150f;
	[Export] public float SecondaryCooldown = 0.5f;

	// --- Granada de atordoamento: a resposta do humano contra o lobo ---
	[Export] public PackedScene GrenadeScene;
	[Export] public int MaxGrenades = 2;
	[Export] public float ThrowForce = 11f;
	[Export] public float ThrowArc = 3.5f;
	[Export] public float ThrowCooldown = 0.9f;

	// --- Faca: saída silenciosa e último recurso ---
	[Export] public float KnifeDamage = 85f;
	[Export] public float KnifeRange = 2.2f;
	[Export] public float KnifeCooldown = 0.55f;

	// --- Garras do lobo ---

	/// <summary>Dano extra da patada contra um humano. O lobo é feito
	/// para caçar gente, não para duelar com outro lobo.</summary>
	[Export] public float ClawVsHumanBonus = 1.6f;

	/// <summary>Patada dupla no ar: mais dano e mais lenta. É um
	/// compromisso — errar custa o dobro do tempo.</summary>
	[Export] public float SpecialDamageBonus = 1.45f;

	[Export] public float SpecialCooldownBonus = 1.9f;

	/// <summary>Intervalo entre as duas patadas do especial.</summary>
	[Export] public float SpecialSecondDelay = 0.16f;

	/// <summary>Multiplicador de dano na cabeça.</summary>
	[Export] public float HeadshotMultiplier = 2.5f;

	/// <summary>
	/// Altura da zona de cabeça, medida do centro do corpo para cima.
	/// A cápsula tem 1,75, então o topo fica em ~0,87: uma zona a
	/// partir de 0,72 deixa a cabeça com cerca de 15cm, proporcional a
	/// uma cabeça humana, e não o terço superior do corpo.
	/// </summary>
	[Export] public float HeadZone = 0.72f;

	/// <summary>Altura mínima de queda para contar como ataque aéreo.</summary>
	[Export] public float AirAttackMinFall = 2.5f;

	/// <summary>Diagnóstico. Desligue quando o tiro estiver confirmado.</summary>
	[Export] public bool Debug = true;

	/// <summary>Traçante do disparo. Sem nenhum retorno visual não há
	/// como saber que alguém atirou em você — o bot parecia inofensivo
	/// enquanto acertava.</summary>
	[Export] public bool ShowTracer = true;

	[Export] public Color TracerColor = new Color(1f, 0.9f, 0.5f, 0.9f);
	[Export] public float TracerLife = 0.08f;

	/// <summary>Raio do estampido. Atirar sempre entrega a posição —
	/// é o custo de usar a arma.</summary>
	[Export] public float ShotNoise = 60f;

	/// <summary>Slot de arma guardado enquanto você está de lobo, para
	/// voltar nele ao destransformar.</summary>
	public Slot HumanSlot { get; private set; } = Slot.Primary;

	public Slot Current => _form.IsWolf ? Slot.Wolf : HumanSlot;

	/// <summary>Granadas restantes. Reabastece ao renascer.</summary>
	public int Grenades { get; private set; }

	/// <summary>Só a principal dispara segurando o botão.</summary>
	public bool IsAutomatic => Current == Slot.Primary;

	/// <summary>Volta para a arma anterior depois de arremessar: ficar
	/// com a mão vazia no meio de um tiroteio não é escolha de
	/// ninguém.</summary>
	private Slot _slotBeforeGrenade = Slot.Primary;

	private Camera3D _camera;
	private Node3D _head;
	private ViewModel _viewModel;
	private Health _health;
	private FormController _form;
	private CharacterBody3D _body;
	private double _cooldown;
	private float _fallStartY;
	private bool _wasOnFloor = true;

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_form = _body.GetNode<FormController>("FormController");
		_camera = _body.GetNode<Camera3D>("Head/Camera3D");
		_head = _body.GetNode<Node3D>("Head");
		_health = _body.GetNode<Health>("Health");

		Grenades = MaxGrenades;

		_body.GetNode<Health>("Health").Changed += OnHealthChanged;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_cooldown > 0)
			_cooldown -= delta;

		bool onFloor = _body.IsOnFloor();
		if (_wasOnFloor && !onFloor)
			_fallStartY = _body.GlobalPosition.Y;
		_wasOnFloor = onFloor;
	}

	/// <summary>Chamado ao apertar 1, 2, 3 ou 4.</summary>
	public void SelectSlot(Slot slot)
	{
		if (slot == Slot.Knife)
		{
			HumanSlot = Slot.Knife;

			if (_form.IsWolf)
				_form.ToggleForm();

			_cooldown = 0.2f;
			EmitSignal(SignalName.SlotChanged, (int)Slot.Knife);
			return;
		}

		if (slot == Slot.Grenade)
		{
			// Equipa, não arremessa. Apertar 4 e já jogar tira do
			// jogador a chance de mirar — e granada mal mirada é pior
			// que granada não usada.
			if (Grenades <= 0)
			{
				if (Debug) GD.Print("granada: acabou");
				EmitSignal(SignalName.GrenadesChanged, Grenades);
				return;
			}

			_slotBeforeGrenade = HumanSlot == Slot.Grenade ? _slotBeforeGrenade : HumanSlot;
			HumanSlot = Slot.Grenade;

			if (_form.IsWolf)
				_form.ToggleForm();

			_cooldown = 0.2f;
			EmitSignal(SignalName.SlotChanged, (int)Slot.Grenade);
			return;
		}

		if (slot == Slot.Wolf)
		{
			// Já lobo: apertar 3 de novo não faz nada. Para voltar a
			// humano, escolha uma arma.
			if (!_form.IsWolf)
				_form.ToggleForm();

			EmitSignal(SignalName.SlotChanged, (int)Slot.Wolf);
			return;
		}

		HumanSlot = slot;

		if (_form.IsWolf)
			_form.ToggleForm();

		_cooldown = 0.2f;
		EmitSignal(SignalName.SlotChanged, (int)slot);
	}

	public void TryAttack()
	{
		if (_cooldown > 0) return;

		if (!_form.CanAttack)
		{
			if (Debug) GD.Print($"ataque bloqueado: forma atual é {_form.Current}");
			return;
		}

		if (_form.IsWolf)
			WolfAttack();
		else if (HumanSlot == Slot.Grenade)
			ThrowGrenade();
		else if (HumanSlot == Slot.Knife)
			Knife();
		else
			Shoot();
	}

	/// <summary>
	/// Facada. Não faz ruído de tiro: é a arma de quem está se movendo
	/// em silêncio e não quer entregar a posição por um abate.
	/// </summary>
	private void Knife()
	{
		_cooldown = KnifeCooldown;
		EmitSignal(SignalName.Fired);

		Vector3 from = _head.GlobalPosition;
		Vector3 to = from - _head.GlobalTransform.Basis.Z * KnifeRange;

		Health hit = Raycast(from, to, out Vector3 impact, out Node collider);
		if (hit == null) return;

		bool headshot = IsHeadshot(collider, impact);
		float damage = KnifeDamage * (headshot ? HeadshotMultiplier : 1f);

		Damage(hit, damage, from, headshot);
	}

	private void Shoot()
	{
		bool primary = HumanSlot == Slot.Primary;
		float damage = primary ? PrimaryDamage : SecondaryDamage;
		float range = primary ? PrimaryRange : SecondaryRange;

		_cooldown = primary ? PrimaryCooldown : SecondaryCooldown;
		EmitSignal(SignalName.Fired);

		NoiseBus.Emit(_body.GlobalPosition, ShotNoise, NoiseKind.Shot, _body);

		Vector3 from = _camera.GlobalPosition;
		Vector3 to = from - _camera.GlobalTransform.Basis.Z * range;

		Health hit = Raycast(from, to, out Vector3 impact, out Node collider);

		// A mira sai da câmera, porque é dela que vem a precisão; a
		// linha sai do cano, porque é de lá que o jogador espera vê-la.
		Tracer(MuzzleOrigin(from), hit == null ? to : impact);

		if (hit == null) return;

		bool headshot = IsHeadshot(collider, impact);
		if (headshot) damage *= HeadshotMultiplier;

		Damage(hit, damage, from, headshot);
	}

	/// <summary>
	/// Patada especial: só no ar, botão direito. Duas patadas seguidas,
	/// mais dano e cooldown bem maior.
	///
	/// A condição de estar no ar não é decoração: amarra o golpe mais
	/// forte do lobo ao salto, que é a jogada que o jogo quer premiar.
	/// </summary>
	public bool TrySpecial()
	{
		if (_cooldown > 0) return false;
		if (!_form.IsWolf || !_form.CanAttack) return false;
		if (_body.IsOnFloor()) return false;

		WolfAttack(true);
		return true;
	}

	private void WolfAttack(bool special = false)
	{
		WolfBreed breed = _form.Breed;

		_cooldown = special
			? breed.AttackCooldown * SpecialCooldownBonus
			: breed.AttackCooldown;

		EmitSignal(SignalName.Fired);

		if (special)
		{
			// A segunda patada sai um instante depois, para o golpe ter
			// dois tempos em vez de ser um número maior.
			SceneTreeTimer timer = _body.GetTree().CreateTimer(SpecialSecondDelay);
			timer.Timeout += () => Claw(special: true);
		}

		Claw(special);
	}

	private void Claw(bool special)
	{
		WolfBreed breed = _form.Breed;

		// A garrada sai do corpo, não da câmera. Em terceira pessoa a
		// câmera está metros atrás — partir dela faria o lobo acertar
		// coisas que estão atrás dele.
		Vector3 from = _head.GlobalPosition;
		Vector3 to = from - _head.GlobalTransform.Basis.Z * breed.AttackRange;

		Health hit = Raycast(from, to, out _, out Node collider);
		if (hit == null) return;

		float damage = breed.AttackDamage;

		if (special)
			damage *= SpecialDamageBonus;

		// Contra humano a patada vale mais: o lobo é feito para caçar
		// gente, não para duelar com outro lobo.
		if (IsHumanForm(collider))
			damage *= ClawVsHumanBonus;

		// Ataque aéreo: cair em cima de alguém é a jogada que as
		// pessoas lembram. Vale bônus.
		float fallen = _fallStartY - _body.GlobalPosition.Y;
		if (!_body.IsOnFloor() && fallen >= AirAttackMinFall)
			damage *= breed.AirAttackBonus;

		Damage(hit, damage, from, false);
	}

	private static bool IsHumanForm(Node collider)
	{
		FormController form = collider?.GetNodeOrNull<FormController>("FormController");

		return form == null || !form.IsWolf;
	}

	/// <summary>
	/// Aplica o dano e, se o alvo cair, credita o abate. O crédito
	/// sai daqui porque é aqui que se sabe quem bateu — fazer a vítima
	/// descobrir o autor acopla Health a quem atira.
	/// </summary>
	private void Damage(Health target, float amount, Vector3 from, bool headshot)
	{
		// Fogo amigo desligado: num jogo de duas formas, confundir
		// aliado com inimigo já acontece demais pela silhueta.
		if (_health != null && target.Team == _health.Team) return;

		bool wasAlive = !target.IsDead;

		target.TakeDamage(amount, 0, from);

		EmitSignal(SignalName.DamageDealt, amount);
		EmitSignal(SignalName.HitConfirmed, headshot);
		_form.OnDamageDealt(amount);

		if (!wasAlive || !target.IsDead) return;

		_form.OnKill();

		KillFeed.Report(new Kill(
			_health?.DisplayName ?? _body.Name,
			_health?.Team ?? 0,
			target.DisplayName,
			target.Team,
			CauseOf(headshot)));
	}

	private KillCause CauseOf(bool headshot)
	{
		if (_form.IsWolf) return KillCause.Wolf;
		if (headshot) return KillCause.Headshot;

		return HumanSlot == Slot.Knife ? KillCause.Knife : KillCause.Gun;
	}

	/// <summary>
	/// Cabeça é a faixa superior do corpo. Sem hitbox dedicada, a
	/// altura do impacto resolve — e continua valendo quando o modelo
	/// chegar, porque a zona é medida do centro do corpo.
	/// </summary>
	private bool IsHeadshot(Node collider, Vector3 impact)
	{
		if (collider is not Node3D body) return false;

		return impact.Y - body.GlobalPosition.Y >= HeadZone;
	}

	/// <summary>
	/// Arremesso em arco. A granada e um slot de acao, nao de arma:
	/// apertar 4 joga na hora e volta para a arma anterior, em vez de
	/// deixar voce segurando granada no meio de um tiroteio.
	/// </summary>
	private void ThrowGrenade()
	{
		if (_form.IsWolf || !_form.CanAttack)
		{
			if (Debug) GD.Print("granada: so na forma humana, fora de transicao");
			return;
		}

		if (Grenades <= 0)
		{
			if (Debug) GD.Print("granada: acabou");
			EmitSignal(SignalName.GrenadesChanged, Grenades);
			return;
		}

		if (GrenadeScene == null)
		{
			GD.PrintErr("Weapon: GrenadeScene nao atribuida.");
			return;
		}

		if (_cooldown > 0) return;

		Grenades--;
		_cooldown = ThrowCooldown;

		var grenade = GrenadeScene.Instantiate<RigidBody3D>();
		_body.GetTree().CurrentScene.AddChild(grenade);

		// Nasce a frente da camera para nao colidir com o proprio corpo.
		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		grenade.GlobalPosition = _camera.GlobalPosition + forward * 0.8f;
		grenade.AddCollisionExceptionWith(_body);

		// Soma a velocidade do jogador: correr e arremessar joga mais longe.
		Vector3 impulse = forward * ThrowForce + Vector3.Up * ThrowArc;
		grenade.LinearVelocity = _body.Velocity + impulse;
		grenade.AngularVelocity = new Vector3(4f, 2f, 1f);

		EmitSignal(SignalName.GrenadesChanged, Grenades);

		// Sem granadas restantes, volta sozinho para a arma.
		HumanSlot = _slotBeforeGrenade;
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
	}

	/// <summary>Renascer devolve o estoque de granadas.</summary>
	private void OnHealthChanged(float hp, float maxHp, float shield)
	{
		var health = _body.GetNode<Health>("Health");

		if (!health.IsDead && Grenades < MaxGrenades && Mathf.IsEqualApprox(hp, maxHp))
		{
			Grenades = MaxGrenades;
			EmitSignal(SignalName.GrenadesChanged, Grenades);
		}
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
