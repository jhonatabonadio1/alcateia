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

	/// <summary>Munição do carregador mudou, ou a recarga começou.</summary>
	[Signal] public delegate void AmmoChangedEventHandler(int inMag, int magSize, bool reloading);

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

	// --- Ataques especiais (botão direito) ---
	//
	// No chão e no ar são golpes diferentes, não o mesmo em lugares
	// diferentes. No chão é uma patada pesada e lenta; no ar é um bote
	// de três golpes na direção da mira.

	/// <summary>Patada pesada no chão: dano alto, um segundo parado.</summary>
	[Export] public float HeavyDamageBonus = 2.1f;

	[Export] public float HeavyCooldown = 1f;

	/// <summary>Bote aéreo: três golpes, dois segundos de recuperação.</summary>
	[Export] public int LeapHits = 3;

	[Export] public float LeapDamageBonus = 1.3f;
	[Export] public float LeapCooldown = 2f;

	/// <summary>Intervalo entre os golpes do bote.</summary>
	[Export] public float SpecialSecondDelay = 0.13f;

	// --- Combo de garras ---
	//
	// O ataque de chão é uma sequência de três golpes, e cada um cria
	// um VOLUME curto à frente do lobo em vez de disparar um raio.
	//
	// A diferença importa: um raio sai da câmera e acerta exatamente
	// onde se mira, enquanto um volume preso ao corpo acerta o que
	// estiver perto da garra. É isso que faz a garra parecer alcançar
	// mais de baixo para cima do que de cima para baixo — o volume
	// acompanha o corpo, não o olhar.

	/// <summary>Raio do volume de acerto da garra.</summary>
	[Export] public float ClawRadius = 1.15f;

	/// <summary>Quanto à frente do corpo o volume nasce.</summary>
	[Export] public float ClawReach = 1.5f;

	/// <summary>Multiplicador de dano de cada golpe do combo.</summary>
	[Export] public float[] ComboDamage = { 1f, 1f, 1.5f };

	/// <summary>Tempo entre golpes dentro do combo. Mais curto que o
	/// cooldown normal: encadear é mais rápido que recomeçar.</summary>
	[Export] public float ComboCooldown = 0.32f;

	/// <summary>Tempo sem atacar até o combo voltar ao primeiro golpe.</summary>
	[Export] public float ComboReset = 1.1f;

	/// <summary>Empurrão para frente a cada golpe. É o que faz mira
	/// ruim ainda conectar — o golpe leva o lobo até o alvo.</summary>
	[Export] public float ClawLunge = 6.5f;

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

	public Slot CurrentSlot => _form.IsWolf ? Slot.Wolf : HumanSlot;

	/// <summary>Granadas restantes. Reabastece ao renascer.</summary>
	public int Grenades { get; private set; }

	/// <summary>Só a principal dispara segurando o botão.</summary>
	public bool IsAutomatic => CurrentSlot == Slot.Primary;

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

	/// <summary>Lote em uso. Trocar reescreve as armas sem recriar nada.</summary>
	public Loadout Current { get; private set; }

	// --- Munição ---
	//
	// Reserva infinita: o custo de atirar é o tempo de recarga, não o
	// estoque. Contar balas no inventário pune quem já está perdendo.

	public int PrimaryMag { get; private set; } = 40;
	public int SecondaryMag { get; private set; } = 12;

	public int PrimaryAmmo { get; private set; }
	public int SecondaryAmmo { get; private set; }

	public bool IsReloading => _reloadTimer > 0;

	private float _primaryReload = 2f;
	private float _secondaryReload = 1.4f;
	private float _primaryRecoil = 0.22f;
	private float _secondaryRecoil = 0.5f;
	private double _reloadTimer;
	private int _comboIndex;
	private double _comboTimer;

	/// <summary>Recuo da arma na mão, em graus por tiro.</summary>
	public float Recoil => HumanSlot == Slot.Primary ? _primaryRecoil : _secondaryRecoil;

	private int AmmoInMag => HumanSlot == Slot.Primary ? PrimaryAmmo : SecondaryAmmo;
	private int MagSize => HumanSlot == Slot.Primary ? PrimaryMag : SecondaryMag;

	/// <summary>Recarrega o carregador em uso. Só faz sentido com arma
	/// de fogo na mão e com o carregador incompleto.</summary>
	public bool TryReload()
	{
		if (_form.IsWolf || IsReloading) return false;
		if (HumanSlot != Slot.Primary && HumanSlot != Slot.Secondary) return false;
		if (AmmoInMag >= MagSize) return false;

		_reloadTimer = HumanSlot == Slot.Primary ? _primaryReload : _secondaryReload;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, true);

		return true;
	}

	private void FinishReload()
	{
		if (HumanSlot == Slot.Primary) PrimaryAmmo = PrimaryMag;
		else SecondaryAmmo = SecondaryMag;

		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	public void ApplyLoadout(Loadout loadout)
	{
		if (loadout == null) return;

		Current = loadout;

		PrimaryMag = loadout.PrimaryMag;
		SecondaryMag = loadout.SecondaryMag;
		PrimaryAmmo = PrimaryMag;
		SecondaryAmmo = SecondaryMag;

		_primaryReload = loadout.PrimaryReload;
		_secondaryReload = loadout.SecondaryReload;
		_primaryRecoil = loadout.PrimaryRecoil;
		_secondaryRecoil = loadout.SecondaryRecoil;

		_reloadTimer = 0;

		PrimaryDamage = loadout.PrimaryDamage;
		PrimaryRange = loadout.PrimaryRange;
		PrimaryCooldown = loadout.PrimaryCooldown;

		SecondaryDamage = loadout.SecondaryDamage;
		SecondaryRange = loadout.SecondaryRange;
		SecondaryCooldown = loadout.SecondaryCooldown;

		MaxGrenades = loadout.Grenades;
		Grenades = loadout.Grenades;

		HumanSlot = Slot.Primary;

		EmitSignal(SignalName.GrenadesChanged, Grenades);
		EmitSignal(SignalName.SlotChanged, (int)Slot.Primary);
		EmitSignal(SignalName.AmmoChanged, PrimaryAmmo, PrimaryMag, false);
	}

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

		if (_comboTimer > 0)
			_comboTimer -= delta;

		if (_reloadTimer > 0)
		{
			_reloadTimer -= delta;

			if (_reloadTimer <= 0)
				FinishReload();
		}

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
		_reloadTimer = 0;

		if (_form.IsWolf)
			_form.ToggleForm();

		_cooldown = 0.2f;
		EmitSignal(SignalName.SlotChanged, (int)slot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
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
		if (IsReloading) return;

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
	/// <summary>
	/// Botão direito. No chão, patada pesada da direita. No ar, bote de
	/// três golpes — e só esse avisa ao chamador, porque só esse move o
	/// corpo.
	/// </summary>
	public bool TrySpecial()
	{
		if (_cooldown > 0) return false;
		if (!_form.IsWolf || !_form.CanAttack) return false;

		if (_body.IsOnFloor())
		{
			Heavy();
			return false;
		}

		Leap();
		return true;
	}

	/// <summary>Patada pesada no chão: um golpe, dano alto, lenta. É o
	/// castigo de quem deixou o lobo chegar parado.</summary>
	private void Heavy()
	{
		_cooldown = HeavyCooldown;
		_comboIndex = 0;

		EmitSignal(SignalName.Fired);

		Lunge();
		Claw(HeavyDamageBonus);
	}

	/// <summary>Bote aéreo: três golpes em sequência enquanto o corpo
	/// viaja na direção da mira.</summary>
	private void Leap()
	{
		_cooldown = LeapCooldown;
		_comboIndex = 0;

		EmitSignal(SignalName.Fired);

		Claw(LeapDamageBonus);

		for (int i = 1; i < LeapHits; i++)
		{
			SceneTreeTimer timer = _body.GetTree().CreateTimer(SpecialSecondDelay * i);
			timer.Timeout += () => Claw(LeapDamageBonus);
		}
	}

	private void WolfAttack()
	{
		// Combo: o golpe seguinte sai mais rápido que o
		// primeiro, e o terceiro machuca mais.
		if (_comboTimer <= 0) _comboIndex = 0;

		float multiplier = ComboDamage[Mathf.Min(_comboIndex, ComboDamage.Length - 1)];

		_comboIndex = (_comboIndex + 1) % ComboDamage.Length;
		_comboTimer = ComboReset;
		_cooldown = ComboCooldown;

		EmitSignal(SignalName.Fired);

		Lunge();
		Claw(multiplier);
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

	/// <summary>
	/// Golpe de garra: um volume curto à frente do corpo, não um raio
	/// a partir da câmera. Pega quem estiver perto da garra.
	/// </summary>
	private void Claw(float multiplier)
	{
		WolfBreed breed = _form.Breed;

		// O volume nasce à frente do CORPO, na altura do peito. Preso
		// ao corpo e não ao olhar, é o que dá à garra um alcance que
		// varia com a postura em vez de seguir a mira.
		Vector3 forward = -_body.GlobalTransform.Basis.Z;
		Vector3 center = _body.GlobalPosition + forward * ClawReach + Vector3.Up * 0.2f;

		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = ClawRadius },
			Transform = new Transform3D(Basis.Identity, center),
			CollideWithBodies = true,
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() }
		};

		var hits = _body.GetWorld3D().DirectSpaceState.IntersectShape(query, 8);

		foreach (var entry in hits)
		{
			if (entry["collider"].Obj is not Node collider) continue;

			Health target = collider.GetNodeOrNull<Health>("Health");
			if (target == null) continue;

			float damage = breed.AttackDamage * multiplier;

			// Contra humano a patada vale mais: o lobo é feito para
			// caçar gente, não para duelar com outro lobo.
			if (IsHumanForm(collider))
				damage *= ClawVsHumanBonus;

			// Ataque aéreo: cair em cima de alguém é a jogada que as
			// pessoas lembram. Vale bônus.
			float fallen = _fallStartY - _body.GlobalPosition.Y;
			if (!_body.IsOnFloor() && fallen >= AirAttackMinFall)
				damage *= breed.AirAttackBonus;

			Damage(target, damage, _body.GlobalPosition, false);
		}
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

		target.TakeDamage(amount, 0, from, headshot);

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
