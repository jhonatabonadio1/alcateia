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

	/// <summary>Arremessou a granada.</summary>
	[Signal] public delegate void ThrownEventHandler();
	[Signal] public delegate void SlotChangedEventHandler(int slot);

	/// <summary>Acertou alguém. O HUD usa para marcar no crosshair —
	/// sem confirmação visual não se sabe se o tiro pegou.</summary>
	[Signal] public delegate void HitConfirmedEventHandler(bool headshot);
	[Signal] public delegate void GrenadesChangedEventHandler(int left);

	/// <summary>O tiro parou no cenário, neste ponto. Uma vez por
	/// disparo (na escopeta, o primeiro chumbo). Só enfeite: o som e a
	/// marca da bala na parede.</summary>
	[Signal] public delegate void WorldHitEventHandler(Vector3 position, Vector3 normal, Node collider);

	/// <summary>Munição do carregador mudou, ou a recarga começou.</summary>
	[Signal] public delegate void AmmoChangedEventHandler(int inMag, int magSize, bool reloading);

	/// <summary>Gatilho apertado sem bala no pente nem na reserva: o
	/// clique seco.</summary>
	[Signal] public delegate void DryFiredEventHandler();

	/// <summary>Um ataque de lobo entrou em recarga: <paramref name="kind"/>
	/// é 0 garrada, 1 salto, 2 bote. O HUD do lobo anima a volta a partir
	/// daqui, sem consultar a arma a cada quadro.</summary>
	[Signal] public delegate void AttackCooldownEventHandler(int kind, float seconds);

	public const int CooldownClaw = 0, CooldownHeavy = 1, CooldownLeap = 2;

	/// <summary>Pegou uma arma do chão. O HUD avisa qual.</summary>
	[Signal] public delegate void PickedUpEventHandler(string id);

	/// <summary>Juntou a munição de uma arma igual à sua, caída.</summary>
	[Signal] public delegate void AmmoTakenEventHandler(string id);

	/// <summary>Soltou a arma da mão (G) ou deixou cair ao morrer.</summary>
	[Signal] public delegate void DroppedEventHandler(string id);

	/// <summary>Um golpe do lobo começou (<see cref="WolfMove"/>). O corpo
	/// toca a animação daqui; o dano sai depois, no instante do golpe.</summary>
	[Signal] public delegate void WolfMoveStartedEventHandler(int move);

	/// <summary>Um golpe do lobo foi largado antes de sair (a carga do
	/// salto cancelada): o corpo para o clipe dele.</summary>
	[Signal] public delegate void WolfMoveCancelledEventHandler(int move);

	/// <summary>O bote encostou em algo (chão, parede, alguém) e bateu. O
	/// corpo troca a queda pela aterrissagem neste instante.</summary>
	[Signal] public delegate void WolfDiveLandedEventHandler();

	/// <summary>A aterrissagem do bote ou do salto foi cortada (pelo pulo,
	/// pela corrida, pela garra): o corpo sai da pose de levantar.</summary>
	[Signal] public delegate void WolfRecoveryCutEventHandler();

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

	/// <summary>Diagnóstico. Desligue quando o tiro estiver confirmado.</summary>
	[ExportGroup("Diagnóstico")]
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
	// Cada arma tem o pente e a reserva (WeaponDefinition.Reserve). A
	// munição **voltou a ser infinita** (decisão do Jhon, 18/11; de 13/11
	// até aí ela acabava): recarregar enche o pente e a reserva não desce.
	// Com InfiniteAmmo desligado vale a regra de 13/11: recarregar tira da
	// reserva; sem reserva, não recarrega; renascer enche tudo, e passar
	// por cima de uma arma igual à sua, caída, junta a munição dela.

	/// <summary>A reserva não acaba: recarregar não tira nada dela.</summary>
	[Export] public bool InfiniteAmmo = true;

	/// <summary>Arma de fogo na mão; nula com faca, granada ou de lobo.</summary>
	public WeaponDefinition Held =>
		_form == null || _form.IsWolf ? null
		: HumanSlot == Slot.Primary ? PrimaryDef
		: HumanSlot == Slot.Secondary ? SecondaryDef
		: null;

	/// <summary>Id do que está na mão humana — a arma de fogo, a faca ou a
	/// granada; vazio de lobo. É o que a arma na tela, a arma na mão do
	/// corpo e o HUD desenham.</summary>
	public string HeldId =>
		_form == null || _form.IsWolf ? ""
		: HumanSlot switch
		{
			Slot.Primary => PrimaryDef?.Id ?? "",
			Slot.Secondary => SecondaryDef?.Id ?? "",
			Slot.Knife => MeleeId,
			Slot.Grenade => GrenadeModelId,
			_ => ""
		};

	/// <summary>Avisa quem desenha (HUD, arma na tela) do que está na mão
	/// agora: o slot e o pente.</summary>
	private void AnnounceHeld()
	{
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	/// <summary>O sorteio desta arma (<see cref="GameRandom"/>): dispersão,
	/// recuo sorteado, a arma solta. Fluxo próprio, pelo nome do dono.</summary>
	private GameRandom _rng;

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();
		_form = _body.GetNode<FormController>("FormController");
		_camera = _body.GetNode<Camera3D>("Head/Camera3D");
		_head = _body.GetNode<Node3D>("Head");
		_health = _body.GetNode<Health>("Health");
		_rng = MatchRandom.Stream($"{_body.Name}/arma");

		// Só a cena do jogador trazia a granada atribuída. A do bot — que
		// também é o boneco dos outros jogadores no online — ficava sem:
		// o bot não tinha o que arremessar, e a granada de outro jogador
		// não aparecia para ninguém.
		GrenadeScene ??= GD.Load<PackedScene>(DefaultGrenadeScene);

		// Quem ainda não escolheu nada — bot, ou o jogador antes da tela
		// de conjunto — começa com o padrão.
		if (PrimaryDef == null)
			Equip(null, null, MaxGrenades);

		_health.Respawned += RefillGrenades;
		_health.Died += OnDied;
		_health.Respawned += RefillAmmo;
		_form.Scored += OnScored;
	}

	public override void _PhysicsProcess(double delta)
	{
		// O corpo do servidor de partida (Driven) dá o passo da arma junto com
		// o do corpo, um por comando (Player.Simulate), e não um por quadro do
		// servidor: com a rede oscilando, o servidor aplica dois comandos num
		// quadro ou nenhum, e o voo do salto (o relógio dele mora aqui) andava
		// fora do passo do corpo — o salto do servidor saía diferente do que o
		// jogo previu, e a correção puxava o lobo para trás (Jhon, 25/11).
		if (_body is Player { Driven: true }) return;
		Tick(delta);
	}

	/// <summary>Um passo da arma: no jogo, um por quadro de física, logo
	/// depois do passo do corpo; no servidor, um por comando
	/// (<see cref="Player.Simulate"/>), na mesma ordem.</summary>
	public void Tick(double delta)
	{
		UpdateAim(delta);
		TickSpecial(delta);

		if (_cooldown > 0)
			_cooldown -= delta;

		if (_comboTimer > 0)
			_comboTimer -= delta;

		if (_strikeLeft > 0)
			_strikeLeft -= delta;

		if (_rootLeft > 0)
			_rootLeft -= delta;

		TickRecovery(delta);

		if (_stepLeft > 0)
			_stepLeft -= delta;

		if (_diving)
			UpdateDive(delta);

		if (_charging)
		{
			_chargeTime += delta;
			if (!_form.IsWolf || _health.IsDead) CancelCharge();
			else if (PounceChargeHold >= 0f && _chargeTime >= PounceChargeTime + PounceChargeHold) ReleaseCharge();
		}

		if (_flying)
		{
			_flyTime += delta;
			if (_flyTime >= _flyTotal || !_form.IsWolf || _health.IsDead)
			{
				_flying = false;
				_pounceLanded = true;
			}
			else ChaseFocus(delta);
		}

		UpdateArmed(delta);
		TickButt(delta);
		TickStab(delta);
		TickThrow((float)delta);

		if (_pending.Count > 0)
			RunPending(delta);

		// O clique guardado sai assim que o golpe anterior acaba.
		if (_attackQueue > 0 && _cooldown <= 0)
		{
			_attackQueue--;
			if (_form.IsWolf && _form.CanAttack && !_health.IsDead) WolfAttack();
			else if (!_form.IsWolf && HumanSlot == Slot.Knife && _form.CanAttack && !_health.IsDead)
			{
				_attackQueue = 0;
				Knife();
			}
			else _attackQueue = 0;
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

		// Modo em que este time é lobo a partida toda: as armas não saem.
		if (_form.Rule == FormRule.Lobo && slot != Slot.Wolf)
		{
			_form.ToggleForm();      // só avisa (o HUD diz por quê)
			return;
		}

		if (slot == Slot.Knife)
		{
			TakeInHand(Slot.Knife);
			return;
		}

		if (slot == Slot.Grenade)
		{
			// Equipa, não arremessa. Apertar 4 e já jogar tira do
			// jogador a chance de mirar — e granada mal mirada é pior
			// que granada não usada.
			if (Grenades <= 0)
			{
				if (Debug) GD.Print("grenade: none left");
				EmitSignal(SignalName.GrenadesChanged, Grenades);
				return;
			}

			_slotBeforeGrenade = HumanSlot == Slot.Grenade ? _slotBeforeGrenade : HumanSlot;
			TakeInHand(Slot.Grenade);
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

		_reloadTimer = 0;
		TakeInHand(slot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	/// <summary>Põe na mão: volta a humano se estava de lobo, espera o
	/// tempo da troca e avisa quem desenha.</summary>
	private void TakeInHand(Slot slot)
	{
		// A arma que se guarda com animação (o AR-15) leva o tempo dela
		// antes de a próxima subir; a próxima atira 0,2 s depois de subir.
		float holster = !_form.IsWolf && slot != HumanSlot ? Held?.HolsterTime ?? 0f : 0f;
		HumanSlot = slot;

		if (_form.IsWolf)
			_form.ToggleForm();

		_cooldown = holster + 0.2f;
		EmitSignal(SignalName.SlotChanged, (int)slot);
	}

	public void TryAttack()
	{
		if (_cooldown > 0)
		{
			// De lobo, o clique no fim de um golpe fica guardado.
			if (_form.IsWolf && !_diving && _cooldown <= AttackBuffer)
				_attackQueue = Mathf.Min(_attackQueue + 1, MaxQueuedAttacks);
			// Com a faca também (19/11): um clique, um golpe — o que cai no
			// meio do golpe anterior sai assim que ele acaba.
			else if (!_form.IsWolf && HumanSlot == Slot.Knife && _cooldown <= AttackBuffer)
				_attackQueue = 1;
			return;
		}

		if (!_form.CanAttack)
		{
			if (Debug) GD.Print($"attack blocked: current form is {_form.Current}");
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
		SpecialArmed = false;
		_pounceGuard = false;
		_grenadeReady = false;
		_throwPending = 0;
		_attackQueued = false;
		_pending.Clear();
		if (IsReloading) CancelReload();
		if (_form.IsWolf) return;

		// O corpo do servidor de partida não larga arma no chão: a arma
		// que cai é a do jogo de quem morreu. Nem o boneco de outra máquina
		// (26/11): a máquina do dono solta e avisa a partida, e a arma
		// aparece igual em todas as telas. Antes o boneco soltava uma cópia
		// só desta tela, que só esta tela via.
		if (_body is Player { Driven: true } || Puppet) return;

		bool primary = PrimaryDef != null;
		WeaponDefinition def = primary ? PrimaryDef : SecondaryDef;
		if (def == null) return;

		int ammo = primary ? PrimaryAmmo : SecondaryAmmo;
		int reserve = primary ? PrimaryReserve : SecondaryReserve;
		Vector3 from = _body.GlobalPosition + Vector3.Up * 1.1f;
		var spread = new Vector3(_rng.Range(-1f, 1f), 0, _rng.Range(-1f, 1f));
		Vector3 velocity = _body.Velocity * 0.4f + spread + Vector3.Up * 1.2f;
		WeaponPickup.Spawn(_body, def, ammo, reserve, from, velocity);
		EmitSignal(SignalName.Dropped, def.Id);
	}

	/// <summary>Renascer devolve o estoque de granadas. Antes era "a vida
	/// chegou ao máximo", o que com a vida voltando sozinha (17/11) daria
	/// granada de graça a quem espera.</summary>
	private void RefillGrenades()
	{
		if (Grenades < MaxGrenades)
		{
			Grenades = MaxGrenades;
			EmitSignal(SignalName.GrenadesChanged, Grenades);
		}
	}
}
