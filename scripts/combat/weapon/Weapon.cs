using Godot;
using System.Collections.Generic;

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

	/// <summary>Um ataque de lobo entrou em recarga: <paramref name="kind"/>
	/// é 0 garrada, 1 salto, 2 bote. O HUD do lobo anima a volta a partir
	/// daqui, sem consultar a arma a cada quadro.</summary>
	[Signal] public delegate void AttackCooldownEventHandler(int kind, float seconds);

	public const int CooldownClaw = 0, CooldownHeavy = 1, CooldownLeap = 2;

	/// <summary>Pegou uma arma do chão. O HUD avisa qual.</summary>
	[Signal] public delegate void PickedUpEventHandler(string id);

	/// <summary>Soltou a arma da mão (G) ou deixou cair ao morrer.</summary>
	[Signal] public delegate void DroppedEventHandler(string id);

	/// <summary>Um golpe do lobo começou (<see cref="WolfMove"/>). O corpo
	/// toca a animação daqui; o dano sai depois, no instante do golpe.</summary>
	[Signal] public delegate void WolfMoveStartedEventHandler(int move);

	/// <summary>O bote encostou em algo (chão, parede, alguém) e bateu. O
	/// corpo troca a queda pela aterrissagem neste instante.</summary>
	[Signal] public delegate void WolfDiveLandedEventHandler();

	//
	// No chão e no ar são golpes diferentes, não o mesmo em lugares
	// diferentes. No chão é o salto inteiro do "jump attack", parado no
	// lugar, com o dano só quando o lobo cai. No ar é o bote: um
	// mergulho reto na direção da mira, que bate no instante em que
	// encosta em algo.

	//
	// O ataque de chão é uma sequência de três golpes, e cada um cria
	// um VOLUME curto à frente do lobo em vez de disparar um raio.
	//
	// A diferença importa: um raio sai da câmera e acerta exatamente
	// onde se mira, enquanto um volume preso ao corpo acerta o que
	// estiver perto da garra. É isso que faz a garra parecer alcançar
	// mais de baixo para cima do que de cima para baixo — o volume
	// acompanha o corpo, não o olhar.

	[ExportGroup("Diagnóstico")]
	/// <summary>Diagnóstico. Desligue quando o tiro estiver confirmado.</summary>
	[Export] public bool Debug = true;

	/// <summary>Slot de arma guardado enquanto você está de lobo, para
	/// voltar nele ao destransformar.</summary>
	public Slot HumanSlot { get; private set; } = Slot.Primary;

	public Slot CurrentSlot => _form.IsWolf ? Slot.Wolf : HumanSlot;

	private Camera3D _camera;
	private Node3D _head;
	private ViewModel _viewModel;
	private Health _health;
	private FormController _form;
	private CharacterBody3D _body;
	private double _cooldown;
	private float _fallStartY;
	private bool _wasOnFloor = true;

	// --- Munição ---
	//
	// Reserva infinita: o custo de atirar é o tempo de recarga, não o
	// estoque. Contar balas no inventário pune quem já está perdendo.
	// A reserva do documento existe na definição só para exibição.

	/// <summary>Arma de fogo na mão; nula com faca, granada ou de lobo.</summary>
	public WeaponDefinition Held =>
		_form == null || _form.IsWolf ? null
		: HumanSlot == Slot.Primary ? PrimaryDef
		: HumanSlot == Slot.Secondary ? SecondaryDef
		: null;

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_form = _body.GetNode<FormController>("FormController");
		_camera = _body.GetNode<Camera3D>("Head/Camera3D");
		_head = _body.GetNode<Node3D>("Head");
		_health = _body.GetNode<Health>("Health");

		// Quem ainda não escolheu nada — bot, ou o jogador antes da tela
		// de conjunto — começa com o padrão.
		if (PrimaryDef == null)
			Equip(null, null, MaxGrenades);

		_body.GetNode<Health>("Health").Changed += OnHealthChanged;
		_health.Died += OnDied;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_cooldown > 0)
			_cooldown -= delta;

		if (_comboTimer > 0)
			_comboTimer -= delta;

		if (_strikeLeft > 0)
			_strikeLeft -= delta;

		if (_rootLeft > 0)
			_rootLeft -= delta;

		if (_stepLeft > 0)
			_stepLeft -= delta;

		if (_diving)
			UpdateDive(delta);

		if (_charging)
		{
			_chargeTime += delta;
			if (!_form.IsWolf || _health.IsDead) CancelCharge();
			else if (_chargeTime >= PounceChargeTime + PounceChargeHold) ReleaseCharge();
		}

		if (_flying)
		{
			_flyTime += delta;
			if (_flyTime >= _flyTotal || !_form.IsWolf || _health.IsDead)
				_flying = false;
		}

		if (_pending.Count > 0)
			RunPending(delta);

		// O clique guardado sai assim que o golpe anterior acaba.
		if (_attackQueued && _cooldown <= 0)
		{
			_attackQueued = false;
			if (_form.IsWolf && _form.CanAttack && !_health.IsDead) WolfAttack();
		}

		if (_reloadTimer > 0)
		{
			_reloadTimer -= delta;

			if (_reloadTimer <= 0)
				FinishReload();
		}

		// A dispersão volta à base depois de uma pausa curta. É a pausa
		// que ensina a atirar em rajadas: quem segura o gatilho nunca
		// deixa a arma descansar.
		_sinceShot += delta;

		WeaponDefinition held = Held;
		if (held != null && _sinceShot > held.SpreadDelay)
			_bloom = CombatRules.BloomRecovered(_bloom, held.SpreadRecovery, (float)delta);

		if (_sinceShot > 0.3) _streak = 0;

		bool onFloor = _body.IsOnFloor();
		if (_wasOnFloor && !onFloor)
			_fallStartY = _body.GlobalPosition.Y;
		_wasOnFloor = onFloor;
	}

	/// <summary>Chamado ao apertar 1, 2, 3 ou 4.</summary>
	public void SelectSlot(Slot slot)
	{
		// Golpe de lobo não se interrompe, nem para voltar a humano.
		if (IsStriking) return;

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

		// Espaço vazio (a arma foi solta): não há o que pôr na mão. De
		// lobo, a tecla ainda serve para voltar a humano — com a melhor
		// arma que sobrou.
		if ((slot == Slot.Primary && PrimaryDef == null) || (slot == Slot.Secondary && SecondaryDef == null))
		{
			if (!_form.IsWolf) return;

			slot = BestSlot();
			if (slot == Slot.Knife)
			{
				SelectSlot(Slot.Knife);
				return;
			}
		}

		if (slot != HumanSlot && IsReloading) CancelReload();

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
		if (_cooldown > 0)
		{
			// De lobo, o clique no fim de um golpe fica guardado.
			if (_form.IsWolf && !_diving && _cooldown <= AttackBuffer) _attackQueued = true;
			return;
		}

		if (!_form.CanAttack)
		{
			if (Debug) GD.Print($"ataque bloqueado: forma atual é {_form.Current}");
			return;
		}

		if (_form.IsWolf)
			WolfAttack();
		else if (HumanSlot == Slot.Grenade)
		{
			// O jogador prepara e só arremessa ao soltar (decisão do Jhon);
			// o bot arremessa direto.
			if (_body is Player) PrepareGrenade();
			else ThrowGrenade();
		}
		else if (HumanSlot == Slot.Knife)
			Knife();
		else
			Shoot();
	}

	// ------------------------------------------------------------------
	// Soltar e pegar

	/// <summary>
	/// Morto em forma humana deixa a arma cair: a primária, ou a pistola
	/// se só tinha ela. É o que permite pegar a arma de quem você
	/// derrubou. O lobo não carrega arma na mão e não deixa nada.
	///
	/// Quem morreu não perde o que tinha — renasce com o conjunto que
	/// escolher. O que cai é uma cópia, com o pente como estava.
	/// </summary>
	private void OnDied(int killerId)
	{
		_strikeId++;
		_strikeLeft = 0;
		_rootLeft = 0;
		_stepLeft = 0;
		_diving = false;
		_flying = false;
		_charging = false;
		_grenadeReady = false;
		_attackQueued = false;
		_pending.Clear();
		if (IsReloading) CancelReload();
		if (_form.IsWolf) return;

		bool primary = PrimaryDef != null;
		WeaponDefinition def = primary ? PrimaryDef : SecondaryDef;
		if (def == null) return;

		int ammo = primary ? PrimaryAmmo : SecondaryAmmo;
		Vector3 from = _body.GlobalPosition + Vector3.Up * 1.1f;
		var spread = new Vector3((float)GD.RandRange(-1.0, 1.0), 0, (float)GD.RandRange(-1.0, 1.0));
		Vector3 velocity = _body.Velocity * 0.4f + spread + Vector3.Up * 1.2f;
		WeaponPickup.Spawn(_body, def, ammo, from, velocity);
		EmitSignal(SignalName.Dropped, def.Id);
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
}
