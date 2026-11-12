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

	/// <summary>Soltou a arma da mão (B) ou deixou cair ao morrer.</summary>
	[Signal] public delegate void DroppedEventHandler(string id);

	/// <summary>Um golpe do lobo começou (<see cref="WolfMove"/>). O corpo
	/// toca a animação daqui; o dano sai depois, no instante do golpe.</summary>
	[Signal] public delegate void WolfMoveStartedEventHandler(int move);

	/// <summary>O bote encostou em algo (chão, parede, alguém) e bateu. O
	/// corpo troca a queda pela aterrissagem neste instante.</summary>
	[Signal] public delegate void WolfDiveLandedEventHandler();

	// --- Soltar e pegar ---

	/// <summary>Velocidade do arremesso ao soltar com B, para frente.</summary>
	[Export] public float DropThrow = 5.5f;

	/// <summary>Quanto sobe o arremesso.</summary>
	[Export] public float DropLift = 1.8f;

	// Os números das armas de fogo vêm de WeaponDefinition (o arsenal).
	// Aqui ficam só faca, granada e garras, que não são itens de compra.

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
	// diferentes. No chão é o salto inteiro do "jump attack", parado no
	// lugar, com o dano só quando o lobo cai. No ar é o bote: um
	// mergulho reto na direção da mira, que bate no instante em que
	// encosta em algo.

	/// <summary>Salto no chão: dano alto, o lobo preso no lugar até a
	/// animação acabar.</summary>
	[Export] public float HeavyDamageBonus = 2.1f;

	[Export] public float HeavyCooldown = 1f;

	/// <summary>Dano do bote, num golpe só, na hora do impacto.</summary>
	[Export] public float LeapDamageBonus = 3f;

	/// <summary>Tempo mínimo entre um bote e outro, contado do clique.</summary>
	[Export] public float LeapCooldown = 2f;

	/// <summary>Velocidade do mergulho, em m/s. Constante, em linha reta:
	/// sem gravidade, sem freio e sem desvio no meio. O ponto de impacto
	/// é escolhido no clique, e é isso que deixa o bote preciso.</summary>
	[Export] public float DiveSpeed = 17f;

	/// <summary>Inclinação mínima para baixo do mergulho. Mirando reto ou
	/// para cima, o bote ainda desce: é um ataque de cima para baixo, não
	/// um segundo pulo.</summary>
	[Export] public float DiveMinDrop = 0.35f;

	/// <summary>Teto do mergulho, em segundos. Mirou no vazio e não
	/// encostou em nada: bate no ar e volta a cair.</summary>
	[Export] public float DiveMaxTime = 0.9f;

	/// <summary>Volume do bote: um pouco maior que o do salto e das
	/// garras (<see cref="ClawReach"/>, <see cref="ClawRadius"/>). Cair
	/// do alto em cima de alguém é a jogada que o jogo quer premiar, e
	/// um impacto que erra por um palmo não premia ninguém.</summary>
	[Export] public float LeapReach = 1.7f;

	[Export] public float LeapRadius = 1.5f;

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

	/// <summary>Tempo sem atacar, depois do fim do golpe, até o combo
	/// voltar ao primeiro. O intervalo entre golpes é a própria animação
	/// (<see cref="WolfMoves"/>).</summary>
	[Export] public float ComboReset = 1.1f;

	/// <summary>Empurrão para frente a cada golpe. É o que faz mira
	/// ruim ainda conectar — o golpe leva o lobo até o alvo.</summary>
	[Export] public float ClawLunge = 6.5f;

	/// <summary>Clique na garrada guardado, em segundos: clicou com o golpe
	/// anterior ainda no fim, o próximo sai no quadro em que ele acaba. Sem
	/// isso o clique dado um pouco cedo se perdia e o lobo parecia não
	/// responder.</summary>
	[Export] public float AttackBuffer = 0.3f;

	/// <summary>Até onde o salto no chão leva o lobo, em metros. É a
	/// arma do lobo para fechar distância: apertar de longe e cair em
	/// cima do humano. Ele cai com as garras no ponto da mira, até este
	/// limite.</summary>
	[Export] public float PounceMaxDistance = 12f;

	/// <summary>Segurando W, o salto anda pelo menos isto, mesmo mirando
	/// o chão perto: quem aperta para frente quer avançar.</summary>
	[Export] public float PounceAdvanceMin = 6f;

	/// <summary>O que o salto passa por cima, em metros acima dos pés: um
	/// contêiner (2,2 m) passa; um bloco (5 m) ou o muro, não — o salto
	/// para antes dele.</summary>
	[Export] public float PounceMaxClear = 2.6f;

	/// <summary>Altura mínima do arco do salto, em metros. Cresce com a
	/// distância e com o que houver no caminho.</summary>
	[Export] public float PounceArc = 0.6f;

	/// <summary>Quanto o ponto de queda acompanha o alvo durante o voo, em
	/// m/s. O alvo anda até 3 m no 1,1 s do salto e o golpe errava quem só
	/// caminhava; mais devagar que o humano correndo (4,2), quem corre de
	/// lado desde o começo ainda escapa.</summary>
	[Export] public float PounceHoming = 3f;

	/// <summary>Quão longe do centro da tela, em graus, um inimigo ainda
	/// conta como "mirado" pelo salto. O lobo não tem mira na tela, e
	/// acertar um corpo a 12 m só com o centro da câmera seria loteria.</summary>
	[Export] public float PounceAimAssist = 10f;

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

	/// <summary>Dispara segurando o botão? Quem decide é a arma.</summary>
	public bool IsAutomatic => Held != null && Held.IsAutomatic;

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

	/// <summary>Armas em uso. Trocar reescreve as armas sem recriar nada.</summary>
	public WeaponDefinition PrimaryDef { get; private set; }
	public WeaponDefinition SecondaryDef { get; private set; }

	// --- Munição ---
	//
	// Reserva infinita: o custo de atirar é o tempo de recarga, não o
	// estoque. Contar balas no inventário pune quem já está perdendo.
	// A reserva do documento existe na definição só para exibição.

	public int PrimaryMag => PrimaryDef?.Magazine ?? 0;
	public int SecondaryMag => SecondaryDef?.Magazine ?? 0;

	public int PrimaryAmmo { get; private set; }
	public int SecondaryAmmo { get; private set; }

	public bool IsReloading => _reloadTimer > 0;

	private double _reloadTimer;
	private double _reloadTotal = 1;
	private bool _shellReload;
	private int _comboIndex;

	// --- Golpes do lobo sincronizados com a animação ---

	/// <summary>Próxima garrada com o outro braço.</summary>
	private bool _nextPunch;

	/// <summary>Tempo que falta da animação do golpe em curso.</summary>
	private double _strikeLeft;

	/// <summary>Muda a cada golpe e na morte: dano agendado de um golpe
	/// que não vale mais (morreu, voltou a humano) não sai.</summary>
	private ulong _strikeId;
	private double _comboTimer;

	/// <summary>Tempo que o lobo ainda fica preso no lugar: o salto
	/// inteiro, e a aterrissagem do bote.</summary>
	private double _rootLeft;

	private bool _attackQueued;
	private double _stepLeft;
	private double _stepTotal = 1;
	private float _stepPeak;
	private Vector3 _stepForward;
	private bool _diving;
	private double _diveLeft;
	private double _diveElapsed;

	/// <summary>Consulta de "tem alguém na frente do bote", reaproveitada
	/// a cada quadro do mergulho em vez de montada de novo.</summary>
	private PhysicsShapeQueryParameters3D _diveProbe;

	// --- Dispersão e recuo da arma na mão ---

	/// <summary>Dispersão acumulada pelos tiros, acima da base.</summary>
	private float _bloom;
	private double _sinceShot = 10;

	/// <summary>Tiros seguidos: é o que decide em que fase do recuo a
	/// arma está (o M16 coiceia forte nos quatro primeiros, o AK fica
	/// imprevisível depois do sexto).</summary>
	private int _streak;

	/// <summary>Arma de fogo na mão; nula com faca, granada ou de lobo.</summary>
	public WeaponDefinition Held =>
		_form == null || _form.IsWolf ? null
		: HumanSlot == Slot.Primary ? PrimaryDef
		: HumanSlot == Slot.Secondary ? SecondaryDef
		: null;

	/// <summary>Recuo vertical do último tiro, em graus.</summary>
	public float Recoil { get; private set; }

	/// <summary>Recuo horizontal do último tiro, em graus.</summary>
	public float RecoilYaw { get; private set; }

	/// <summary>Velocidade de caminhada com a arma da mão (1 = base).</summary>
	public float MoveSpeedMultiplier => Held?.SpeedMultiplier ?? 1f;

	/// <summary>Dispersão de agora, em graus — para a mira poder crescer junto.</summary>
	public float CurrentSpread => Held == null ? 0f : SpreadNow(Held);

	/// <summary>Quanto da recarga em curso já passou, de 0 a 1. Na
	/// escopeta é o ciclo do cartucho atual. A animação da recarga segue
	/// este relógio, então recarga vazia mais longa dura mais na tela.</summary>
	public float ReloadProgress =>
		IsReloading ? Mathf.Clamp(1f - (float)(_reloadTimer / Mathf.Max(_reloadTotal, 0.01)), 0f, 1f) : 0f;

	/// <summary>Duração total da recarga em curso, em segundos.</summary>
	public float ReloadDuration => (float)_reloadTotal;

	/// <summary>No meio de um golpe do lobo. A animação não é cortada:
	/// nem por outro golpe nem pela volta a humano.</summary>
	public bool IsStriking => _form != null && _form.IsWolf && (_strikeLeft > 0 || _diving);

	/// <summary>No meio do bote: o corpo segue <see cref="DiveVelocity"/>
	/// e mais nada até encostar.</summary>
	public bool IsDiving => _diving && _form != null && _form.IsWolf;

	/// <summary>Velocidade do mergulho em curso, fixa desde o clique.</summary>
	public Vector3 DiveVelocity { get; private set; }

	/// <summary>Quanto o mergulho deve levar até encostar no chão, medido
	/// no clique. O corpo usa para a queda da animação chegar junto.</summary>
	public float DiveEta { get; private set; }

	/// <summary>Preso no lugar: o salto no chão, ou a aterrissagem do bote.
	/// O corpo não anda nem pula, só gira a mira.</summary>
	public bool IsRooted => _form != null && _form.IsWolf && _rootLeft > 0;

	/// <summary>No passo da garrada: o corpo anda nesta velocidade, por
	/// cima do que o jogador estiver apertando.</summary>
	public bool IsStepping => _form != null && _form.IsWolf && _stepLeft > 0;

	/// <summary>Velocidade do passo neste instante: sobe e desce em
	/// sino, sem degrau no começo nem no fim — o degrau era o que jogava
	/// as pernas na pose de correr de uma vez.</summary>
	public Vector3 StepVelocity =>
		_stepForward * (_stepPeak * Mathf.Sin(Mathf.Pi * (float)(1.0 - _stepLeft / _stepTotal)));

	/// <summary>Abaixo disto o lobo conta como parado ao bater.</summary>
	private const float StandingSpeed = 1.5f;

	public bool HasPrimary => PrimaryDef != null;
	public bool HasSecondary => SecondaryDef != null;

	public int AmmoInMag => HumanSlot == Slot.Primary ? PrimaryAmmo : SecondaryAmmo;
	public int MagSize => HumanSlot == Slot.Primary ? PrimaryMag : SecondaryMag;

	/// <summary>Recarrega o carregador em uso. Só faz sentido com arma
	/// de fogo na mão e com o carregador incompleto.</summary>
	public bool TryReload()
	{
		if (_form.IsWolf || IsReloading) return false;

		WeaponDefinition held = Held;
		if (held == null || AmmoInMag >= MagSize) return false;

		// Vazia demora mais que tática: é o preço de esvaziar o pente.
		_shellReload = held.ShellByShell;
		_reloadTimer = held.ShellByShell
			? held.ShellEnter + held.ShellTime
			: AmmoInMag == 0 ? held.ReloadEmpty : held.ReloadTactical;
		_reloadTotal = _reloadTimer;

		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, true);
		return true;
	}

	private void FinishReload()
	{
		if (_shellReload)
		{
			// Um cartucho por ciclo, e o ciclo seguinte começa sozinho
			// até encher. O tiro interrompe — é a decisão sob pressão que
			// define a escopeta: mais um cartucho ou atirar com o que tem.
			AddRound();

			if (AmmoInMag < MagSize && Held != null)
			{
				_reloadTimer = Held.ShellTime;
				_reloadTotal = _reloadTimer;
				EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, true);
				return;
			}
		}
		else if (HumanSlot == Slot.Primary) PrimaryAmmo = PrimaryMag;
		else SecondaryAmmo = SecondaryMag;

		_reloadTimer = 0;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	private void AddRound()
	{
		if (HumanSlot == Slot.Primary) PrimaryAmmo = Mathf.Min(PrimaryAmmo + 1, PrimaryMag);
		else SecondaryAmmo = Mathf.Min(SecondaryAmmo + 1, SecondaryMag);
	}

	private void CancelReload()
	{
		_reloadTimer = 0;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	/// <summary>Equipa primária, secundária e granadas. Nulo cai no
	/// conjunto padrão, que é o que bot e jogador sem escolha usam.</summary>
	public void Equip(WeaponDefinition primary, WeaponDefinition secondary, int grenades)
	{
		PrimaryDef = primary ?? Arsenal.Get(Arsenal.DefaultPrimary);
		SecondaryDef = secondary ?? Arsenal.Get(Arsenal.DefaultSecondary);

		PrimaryAmmo = PrimaryMag;
		SecondaryAmmo = SecondaryMag;
		_reloadTimer = 0;
		_bloom = 0;
		_streak = 0;

		MaxGrenades = grenades;
		Grenades = grenades;

		HumanSlot = Slot.Primary;

		EmitSignal(SignalName.GrenadesChanged, Grenades);
		EmitSignal(SignalName.SlotChanged, (int)Slot.Primary);
		EmitSignal(SignalName.AmmoChanged, PrimaryAmmo, PrimaryMag, false);
	}

	public void Equip(LoadoutSet set)
	{
		if (set == null)
		{
			Equip(null, null, 0);
			return;
		}

		Equip(Arsenal.Get(set.Primary), Arsenal.Get(set.Secondary), set.GrenadeCount());
	}

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

		if (_flying)
		{
			HomePounce((float)delta);
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
			_bloom = Mathf.Max(0f, _bloom - held.SpreadRecovery * (float)delta);

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
		_bloom = Mathf.Min(_bloom + held.SpreadPerShot, Mathf.Max(held.SpreadMax - held.SpreadBase, 0f));

		Recoil = _streak <= held.RecoilEarlyShots ? held.RecoilVertical : held.RecoilVerticalLate;

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

			float damage = held.Damage * Arsenal.DamageScale * held.Falloff(from.DistanceTo(impact));
			if (headshot) damage *= held.HeadshotMultiplier;

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
		float spread = held.SpreadBase + _bloom;

		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		if (flat.Length() > 0.6f) spread *= held.MoveSpreadMultiplier;

		if (_body is Player player && player.IsCrouching)
			spread *= held.CrouchSpreadMultiplier;

		return spread;
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
	/// Botão direito. No chão, o salto; no ar, o bote. Os dois prendem o
	/// lobo até o fim — o salto no lugar, o bote na trajetória — e o
	/// dano dos dois sai quando o lobo de fato cai.
	/// </summary>
	/// <param name="advancing">O jogador está segurando para frente: o
	/// salto no chão avança mesmo sem alvo.</param>
	public void TrySpecial(bool advancing = false)
	{
		if (_cooldown > 0) return;
		if (!_form.IsWolf || !_form.CanAttack) return;

		if (_body.IsOnFloor())
			Pounce(advancing);
		else
			Leap();
	}

	/// <summary>
	/// Salto no chão: o "jump attack" inteiro — agacha, salta, cai — com
	/// o lobo preso no lugar. O dano sai no quadro em que os pés tocam o
	/// chão na animação, não no clique. É o golpe de quem deixou o lobo
	/// chegar parado: forte, mas lento e fácil de ver vindo.
	/// </summary>
	private void Pounce(bool advancing)
	{
		_comboIndex = 0;
		_attackQueued = false;
		_rootLeft = WolfMoves.Duration(WolfMove.Pounce);
		_pounceFlew = false;
		ulong id = StartMove(WolfMove.Pounce, CooldownHeavy, HeavyCooldown,
			() => Claw(HeavyDamageBonus, ClawReach, ClawRadius, false, _pounceFlew ? _flyDir : null));

		// O salto é um pulo de verdade: do impulso até os pés tocarem o
		// chão, o corpo voa num arco até as garras caírem no alvo. O alvo é
		// escolhido no clique; onde ele está é lido no impulso, 0,32 s
		// depois.
		_pounceTarget = FindPounceTarget();
		_pounceAdvance = advancing;
		var (delay, duration, clipDistance) = WolfMoves.StepOf(WolfMove.Pounce);
		After(delay, id, () => BeginPounceFlight(duration, clipDistance));
	}

	private Node3D _pounceTarget;
	private bool _pounceAdvance;
	private bool _pounceFlew;

	// --- O voo do salto ---

	private bool _flying;
	private double _flyTime;
	private double _flyTotal = 1;
	private Vector3 _flyStart;
	private Vector3 _flyDir;
	private float _flyDistance;
	private float _flyLand;
	private float _flyArc;

	/// <summary>No ar no salto: o corpo segue <see cref="PounceFlightPoint"/>.</summary>
	public bool IsPounceFlying => _flying && _form != null && _form.IsWolf;

	/// <summary>
	/// Onde o corpo deve estar daqui a <paramref name="ahead"/> segundos
	/// de voo. Para a frente em sino (sai devagar, voa, assenta), como o
	/// quadril do clipe; para cima em parábola no tempo, subindo rápido no
	/// impulso — é o que passa por cima de um caixote logo à frente sem
	/// precisar de um arco enorme.
	/// </summary>
	public Vector3 PounceFlightPoint(double ahead)
	{
		float u = Mathf.Clamp((float)((_flyTime + ahead) / _flyTotal), 0f, 1f);
		float s = (1f - Mathf.Cos(Mathf.Pi * u)) * 0.5f;
		float y = _flyLand * u + 4f * _flyArc * u * (1f - u);
		return _flyStart + _flyDir * (_flyDistance * s) + Vector3.Up * y;
	}

	/// <summary>Puxa o ponto de queda para onde o alvo está agora, até
	/// <see cref="PounceHoming"/> m/s e sem passar do alcance.</summary>
	private void HomePounce(float delta)
	{
		if (_pounceTarget == null || !IsInstanceValid(_pounceTarget)) return;
		if (_pounceTarget.GetNodeOrNull<Health>("Health") is not { IsDead: false }) return;

		Vector3 end = _flyStart + _flyDir * _flyDistance;
		Vector3 to = _pounceTarget.GlobalPosition - _flyStart;
		to.Y = 0;
		float reach = to.Length();
		if (reach < 0.01f) return;

		Vector3 wanted = _flyStart + to / reach * Mathf.Clamp(reach - ClawReach, 0f, PounceMaxDistance);
		wanted.Y = end.Y;
		end = end.MoveToward(wanted, PounceHoming * delta);

		Vector3 flat = end - _flyStart;
		flat.Y = 0;
		_flyDistance = flat.Length();
		if (_flyDistance > 0.01f) _flyDir = flat / _flyDistance;
	}

	private void BeginPounceFlight(float duration, float clipDistance)
	{
		var (distance, direction) = PounceAim(clipDistance);

		// Segurando W, avança mesmo mirando o chão perto.
		if (_pounceAdvance && _pounceTarget == null)
			distance = Mathf.Max(distance, Mathf.Min(PounceAdvanceMin, PounceMaxDistance));

		float wanted = distance;
		PlanPounce(ref distance, direction, out float land, out float arc);

		if (Debug)
			GD.Print($"salto: alvo {(_pounceTarget != null ? _pounceTarget.Name : "nenhum")}, " +
				$"anda {distance:0.0} m{(distance < wanted - 0.05f ? $" (queria {wanted:0.0}: obstáculo no caminho)" : "")}, " +
				$"arco {arc:0.0} m, cai {land:+0.0;-0.0} m");

		_flyStart = _body.GlobalPosition;
		_flyDir = direction;
		_flyDistance = distance;
		_flyLand = land;
		_flyArc = arc;
		_flyTotal = Mathf.Max(duration, 0.05f);
		_flyTime = 0;
		_flying = true;
		_pounceFlew = distance > 0.05f;
	}

	/// <summary>
	/// Mede o chão no caminho do salto e escolhe o arco. O que tem até
	/// <see cref="PounceMaxClear"/> acima dos pés é pulado por cima (o
	/// arco sobe o bastante para passar); o que é mais alto (bloco, muro)
	/// encurta o salto para cair antes dele. Cair em cima de um contêiner
	/// também vale: o arco termina na altura dele.
	/// </summary>
	private void PlanPounce(ref float distance, Vector3 direction, out float land, out float arc)
	{
		var space = _body.GetWorld3D().DirectSpaceState;
		float halfHeight = -((_body as Player)?.FeetOffset ?? -0.875f);
		Vector3 feet = _body.GlobalPosition + Vector3.Down * halfHeight;
		float top = PounceMaxClear + 0.4f;

		var path = new List<(float X, float H)>();
		for (float x = 0.4f; x <= distance + 0.01f; x += 0.4f)
		{
			float h = GroundAt(space, feet + direction * x, top);
			if (h > PounceMaxClear)
			{
				distance = Mathf.Max(x - 0.8f, 0f);
				break;
			}
			path.Add((x, h));
		}

		float arcMax = PounceMaxClear + 0.6f;
		land = 0f;
		arc = PounceArc;

		// Até três tentativas: o arco que passa por tudo; se algo no fim do
		// caminho não dá para passar com esse alcance (um contêiner logo
		// antes do ponto de queda), cair em cima dele; se nem isso, cair
		// antes dele.
		for (int attempt = 0; attempt < 3; attempt++)
		{
			// O ponto de queda não pode cair dentro de algo alto (a quina de
			// um pilar, a borda de um muro entre duas amostras): recua até
			// achar chão.
			land = 0f;
			while (distance > 0.05f)
			{
				land = GroundAt(space, feet + direction * distance, top);
				if (land <= PounceMaxClear) break;
				distance = Mathf.Max(distance - 0.4f, 0f);
			}
			land = distance > 0.05f ? Mathf.Clamp(land, -1.5f, PounceMaxClear) : 0f;
			arc = Mathf.Clamp(PounceArc + 0.07f * distance, PounceArc, 1.6f);

			float failX = -1f;
			float failH = 0f;

			foreach (var (x, h) in path)
			{
				if (x >= distance) break;

				// Em que instante do voo o corpo passa por x, e quanto o arco
				// precisa subir para os pés passarem 15 cm acima.
				float s = Mathf.Clamp(x / Mathf.Max(distance, 0.01f), 0.01f, 0.99f);
				float u = Mathf.Acos(1f - 2f * s) / Mathf.Pi;
				float need = (h + 0.15f - land * u) / (4f * u * (1f - u));

				if (need > arcMax)
				{
					failX = x;
					failH = h;
					break;
				}

				arc = Mathf.Max(arc, need);
			}

			if (failX < 0f) return;

			float onTop = GroundAt(space, feet + direction * (failX + 0.4f), top);
			distance = Mathf.Abs(onTop - failH) < 0.35f && failX + 0.4f <= PounceMaxDistance
				? failX + 0.4f
				: Mathf.Max(failX - 0.8f, 0f);
		}
	}

	/// <summary>Altura do chão em <paramref name="at"/>, relativa a ele:
	/// raio de cima para baixo. Começar dentro de algo (um muro mais alto
	/// que o raio) devolve a altura do raio inteira — é obstáculo. Gente
	/// não conta: cair em cima de alguém é o objetivo, não um degrau.</summary>
	private float GroundAt(PhysicsDirectSpaceState3D space, Vector3 at, float top)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * top, at + Vector3.Down * 1.6f);
		query.HitFromInside = true;
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };

		for (int tries = 0; tries < 4; tries++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return -1.6f;

			if (hit["collider"].Obj is CharacterBody3D)
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}

			return ((Vector3)hit["position"]).Y - at.Y;
		}

		return -1.6f;
	}

	/// <summary>
	/// Para onde e quanto o salto anda: até as garras (o volume,
	/// <see cref="ClawReach"/> à frente do corpo) caírem no alvo. Com um
	/// inimigo mirado, em cima dele, onde ele estiver no impulso. Sem
	/// ninguém, no ponto para onde a câmera aponta: chão perto, quase não
	/// sai do lugar; céu, vai até <see cref="PounceMaxDistance"/>. Sem
	/// câmera (bot), a distância do próprio clipe.
	/// </summary>
	private (float Distance, Vector3 Direction) PounceAim(float fallback)
	{
		Vector3 forward = -_body.GlobalTransform.Basis.Z;
		forward.Y = 0;
		forward = forward.Normalized();

		if (_pounceTarget != null && IsInstanceValid(_pounceTarget)
			&& _pounceTarget.GetNodeOrNull<Health>("Health") is { IsDead: false })
		{
			Vector3 to = _pounceTarget.GlobalPosition - _body.GlobalPosition;
			to.Y = 0;
			float flat = to.Length();
			if (flat > 0.01f)
				return (Mathf.Clamp(flat - ClawReach, 0f, PounceMaxDistance), to / flat);
		}

		Camera3D camera = (_body as Player)?.AimCamera;
		if (camera == null) return (Mathf.Min(fallback, PounceMaxDistance), forward);

		Vector3 from = camera.GlobalPosition;
		Vector3 end = from - camera.GlobalBasis.Z * 80f;
		var query = PhysicsRayQueryParameters3D.Create(from, end);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return (PounceMaxDistance, forward);

		float along = ((Vector3)hit["position"] - _body.GlobalPosition).Dot(forward);
		return (Mathf.Clamp(along - ClawReach, 0f, PounceMaxDistance), forward);
	}

	/// <summary>
	/// O inimigo que o jogador está mirando para o salto: o mais perto do
	/// centro da tela, até <see cref="PounceAimAssist"/> graus, ao alcance
	/// do salto e à vista (sem parede no meio). Nenhum: null.
	/// </summary>
	private Node3D FindPounceTarget()
	{
		Camera3D camera = (_body as Player)?.AimCamera;
		if (camera == null) return null;

		Vector3 eye = camera.GlobalPosition;
		Vector3 look = -camera.GlobalBasis.Z;
		float reach = PounceMaxDistance + ClawReach + 1f;
		float bestAngle = Mathf.DegToRad(PounceAimAssist);
		Node3D best = null;

		foreach (string group in new[] { "player", "bot" })
		{
			foreach (Node node in _body.GetTree().GetNodesInGroup(group))
			{
				if (node is not CharacterBody3D other || other == _body) continue;

				Health health = other.GetNodeOrNull<Health>("Health");
				if (health == null || health.IsDead) continue;
				if (health.Team == _health.Team && !RoomConfig.Rules.FriendlyFire) continue;

				Vector3 flat = other.GlobalPosition - _body.GlobalPosition;
				flat.Y = 0;
				if (flat.Length() > reach) continue;

				float angle = look.AngleTo((other.GlobalPosition - eye).Normalized());
				if (angle > bestAngle) continue;

				var sight = PhysicsRayQueryParameters3D.Create(eye, other.GlobalPosition, 1);
				sight.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid(), other.GetRid() };
				if (_body.GetWorld3D().DirectSpaceState.IntersectRay(sight).Count > 0) continue;

				best = other;
				bestAngle = angle;
			}
		}

		return best;
	}

	/// <summary>
	/// Bote: mergulho reto na direção da mira, com a velocidade fixa
	/// desde o clique. Bate no instante em que encosta — no chão, numa
	/// parede ou em alguém —, não num tempo marcado. A animação da queda
	/// é acelerada para o impacto dela cair no mesmo quadro.
	/// </summary>
	private void Leap()
	{
		_comboIndex = 0;

		// A mira inteira, com o pitch: olhar para baixo dá um bote curto e
		// mergulhado, olhar à frente, um longo e raso. Sempre descendo.
		Vector3 aim = -_head.GlobalTransform.Basis.Z;
		aim.Y = Mathf.Min(aim.Y, -DiveMinDrop);
		aim = aim.Normalized();

		DiveVelocity = aim * DiveSpeed;
		DiveEta = TimeToContact(aim);

		_diving = true;
		_diveLeft = DiveMaxTime;
		_diveElapsed = 0;
		_strikeId++;
		_strikeLeft = 0;
		_cooldown = DiveMaxTime + WolfMoves.LandDuration;

		EmitSignal(SignalName.WolfMoveStarted, (int)WolfMove.Leap);
		EmitSignal(SignalName.AttackCooldown, CooldownLeap,
			Mathf.Max(DiveEta + WolfMoves.LandDuration, LeapCooldown));
	}

	/// <summary>Em quanto tempo o mergulho encosta no cenário, pelo raio
	/// dos pés na direção dele.</summary>
	private float TimeToContact(Vector3 direction)
	{
		float range = DiveSpeed * DiveMaxTime;
		Vector3 feet = _body.GlobalPosition + Vector3.Down * 0.8f;

		var query = PhysicsRayQueryParameters3D.Create(feet, feet + direction * range, 1);
		query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		var hit = _body.GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count == 0) return DiveMaxTime;
		return feet.DistanceTo((Vector3)hit["position"]) / DiveSpeed;
	}

	/// <summary>Durante o mergulho: alguém na frente encerra o bote em
	/// cima dele; o tempo acabando encerra no ar. O chão e a parede quem
	/// avisa é o corpo (<see cref="DiveContact"/>), que é quem se move.</summary>
	private void UpdateDive(double delta)
	{
		if (!_form.IsWolf || _health.IsDead)
		{
			_diving = false;
			return;
		}

		_diveLeft -= delta;
		_diveElapsed += delta;

		if (_diveLeft <= 0 || EnemyAhead())
			DiveImpact();
	}

	/// <summary>O corpo encostou no chão, numa parede ou no teto.</summary>
	public void DiveContact()
	{
		if (_diving) DiveImpact();
	}

	private bool EnemyAhead()
	{
		_diveProbe ??= new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = 0.9f },
			CollideWithBodies = true,
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() }
		};

		Vector3 ahead = DiveVelocity.Normalized() * 0.7f;
		_diveProbe.Transform = new Transform3D(Basis.Identity, _body.GlobalPosition + ahead);

		foreach (var entry in _body.GetWorld3D().DirectSpaceState.IntersectShape(_diveProbe, 4))
		{
			if (entry["collider"].Obj is not Node collider) continue;
			Health target = collider.GetNodeOrNull<Health>("Health");
			if (target == null || target.IsDead) continue;
			if (target.Team == _health.Team && !RoomConfig.Rules.FriendlyFire) continue;
			return true;
		}

		return false;
	}

	/// <summary>
	/// O impacto do bote: um golpe só, no volume maior do bote, e o lobo
	/// preso no chão durante a aterrissagem. A recarga conta do clique.
	/// </summary>
	private void DiveImpact()
	{
		_diving = false;
		_diveLeft = 0;

		bool aerial = _fallStartY - _body.GlobalPosition.Y >= AirAttackMinFall;

		EmitSignal(SignalName.Fired);
		Claw(LeapDamageBonus, LeapReach, LeapRadius, aerial);

		float land = WolfMoves.LandDuration;
		_strikeLeft = land;
		_rootLeft = land;
		_cooldown = Mathf.Max(land, LeapCooldown - (float)_diveElapsed);

		EmitSignal(SignalName.WolfDiveLanded);
		EmitSignal(SignalName.AttackCooldown, CooldownLeap, (float)_cooldown);
	}

	/// <summary>
	/// Garrada do clique esquerdo. Os braços alternam — o "swiping" e o
	/// "punch" — e o combo continua contando: o terceiro golpe seguido
	/// machuca mais.
	/// </summary>
	private void WolfAttack()
	{
		if (_comboTimer <= 0) _comboIndex = 0;

		float multiplier = ComboDamage[Mathf.Min(_comboIndex, ComboDamage.Length - 1)];
		_comboIndex = (_comboIndex + 1) % ComboDamage.Length;

		WolfMove move = _nextPunch ? WolfMove.Punch : WolfMove.Claw;
		_nextPunch = !_nextPunch;

		// O combo zera se passar ComboReset depois do fim do golpe.
		_comboTimer = WolfMoves.Duration(move) + ComboReset;

		_attackQueued = false;

		// Parado, o corpo dá o passo do golpe, em sino: as pernas andam
		// um passo junto com o braço. Correndo, o empurrão de sempre leva
		// o lobo até o alvo — as pernas já estão correndo. Só o jogador: o
		// bot anda pela própria cabeça.
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		bool standing = _body is Player && _body.IsOnFloor() && flat.Length() < StandingSpeed;

		ulong id = StartMove(move, CooldownClaw, 0f, () =>
		{
			if (!standing) Lunge();
			Claw(multiplier, ClawReach, ClawRadius, false);
		});

		var (delay, duration, distance) = WolfMoves.StepOf(move);
		if (standing && distance > 0f)
			After(delay, id, () => BeginStep(duration, distance));
	}

	/// <summary>O passo da garrada e o avanço do salto: anda
	/// <paramref name="distance"/> em <paramref name="duration"/>, para a
	/// frente do corpo ou na <paramref name="direction"/> dada. O pico do
	/// sino é π/2 da média, para a distância total ser a mesma.</summary>
	private void BeginStep(float duration, float distance, Vector3? direction = null)
	{
		Vector3 forward = direction ?? -_body.GlobalTransform.Basis.Z;
		forward.Y = 0;
		_stepForward = forward.Normalized();
		_stepTotal = Mathf.Max(duration, 0.01f);
		_stepPeak = Mathf.Pi / 2f * distance / (float)_stepTotal;
		_stepLeft = _stepTotal;
	}

	/// <summary>
	/// Começa um golpe do lobo. A animação dura
	/// <see cref="WolfMoves.Duration"/> e o próximo golpe só sai depois
	/// dela — golpe não corta golpe. O dano (<paramref name="impact"/>)
	/// sai no instante em que a garra chega na animação, não no clique.
	/// </summary>
	private ulong StartMove(WolfMove move, int kind, float minCooldown, System.Action impact)
	{
		float duration = WolfMoves.Duration(move);
		float cooldown = Mathf.Max(duration, minCooldown);

		_cooldown = cooldown;
		_strikeLeft = duration;
		ulong id = ++_strikeId;

		EmitSignal(SignalName.WolfMoveStarted, (int)move);
		EmitSignal(SignalName.AttackCooldown, kind, cooldown);

		After(WolfMoves.ImpactDelay(move), id, () =>
		{
			// O som do golpe sai junto com o golpe.
			EmitSignal(SignalName.Fired);
			impact();
		});

		return id;
	}

	/// <summary>Roda <paramref name="action"/> depois de
	/// <paramref name="delay"/> segundos, se o golpe ainda valer.
	///
	/// Fila própria, contada no passo de física, e não um timer da
	/// árvore por golpe: um bot de lobo bate o tempo todo, e criar um
	/// objeto do Godot a cada golpe acumulava objetos do lado C# que
	/// derrubavam o jogo ao fechar.</summary>
	private void After(float delay, ulong id, System.Action action) =>
		_pending.Add(new PendingHit(delay, id, action));

	private readonly record struct PendingHit(double Left, ulong Id, System.Action Action);

	private readonly List<PendingHit> _pending = new();

	private void RunPending(double delta)
	{
		for (int i = _pending.Count - 1; i >= 0; i--)
		{
			PendingHit hit = _pending[i];
			double left = hit.Left - delta;

			if (left > 0)
			{
				_pending[i] = hit with { Left = left };
				continue;
			}

			_pending.RemoveAt(i);
			if (hit.Id != _strikeId || _health.IsDead || !_form.IsWolf) continue;
			hit.Action();
		}
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
	///
	/// <paramref name="aerial"/>: o golpe veio de uma queda alta e vale
	/// o bônus aéreo mesmo já no chão — o bote bate quando aterrissa.
	/// </summary>
	private void Claw(float multiplier, float reach, float radius, bool aerial, Vector3? facing = null)
	{
		WolfBreed breed = _form.Breed;

		// O volume nasce à frente do CORPO, na altura do peito. Preso
		// ao corpo e não ao olhar, é o que dá à garra um alcance que
		// varia com a postura em vez de seguir a mira. O salto passa a
		// direção em que voou: ele pode ter curvado atrás do alvo.
		Vector3 forward = facing ?? -_body.GlobalTransform.Basis.Z;
		Vector3 center = _body.GlobalPosition + forward * reach + Vector3.Up * 0.2f;

		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = radius },
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
			if (aerial || (!_body.IsOnFloor() && fallen >= AirAttackMinFall))
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
		// Fogo amigo desligado por padrão: num jogo de duas formas,
		// confundir aliado com inimigo já acontece demais pela silhueta.
		// A sala pode ligar.
		if (_health != null && target.Team == _health.Team && target != _health
			&& !RoomConfig.Rules.FriendlyFire) return;

		bool wasAlive = !target.IsDead;

		target.NoteAttacker(_body);
		target.TakeDamage(amount, 0, from, headshot);

		EmitSignal(SignalName.DamageDealt, amount);
		EmitSignal(SignalName.HitConfirmed, headshot);
		_form.OnDamageDealt(amount);

		if (!wasAlive || !target.IsDead) return;

		_form.OnKill(_form.IsWolf);

		// Quem ajudou nos últimos segundos também ganha. Sem assistência,
		// quem tira 90% da vida e perde o abate para um tiro alheio sai
		// sem nada — e o jogo passa a premiar roubar o último tiro.
		var assisters = new System.Collections.Generic.List<string>();
		foreach (Node helper in target.TakeAssists(_body))
		{
			helper.GetNodeOrNull<FormController>("FormController")?.OnAssist();
			if (helper.GetNodeOrNull<Health>("Health") is Health h) assisters.Add(h.PublicName);
		}

		KillFeed.Report(new Kill(
			_health?.PublicName ?? _body.Name.ToString(),
			_health?.Team ?? 0,
			target.PublicName,
			target.Team,
			CauseOf(headshot),
			KillWeapon(),
			assisters.ToArray()));
	}

	/// <summary>Id do que matou, para o feed e as estatísticas.</summary>
	private string KillWeapon()
	{
		if (_form.IsWolf) return "lobo";
		if (HumanSlot == Slot.Knife) return "faca";
		return Held?.Id ?? "";
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
		if (grenade is Grenade armed) armed.Thrower = _health;
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
		HumanSlot = Usable(_slotBeforeGrenade) ? _slotBeforeGrenade : BestSlot();
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
	}

	// ------------------------------------------------------------------
	// Soltar e pegar

	private bool Usable(Slot slot) => slot switch
	{
		Slot.Primary => PrimaryDef != null,
		Slot.Secondary => SecondaryDef != null,
		Slot.Grenade => Grenades > 0,
		Slot.Knife => true,
		_ => false
	};

	/// <summary>A melhor arma que sobrou: primária, secundária, faca.</summary>
	private Slot BestSlot() =>
		PrimaryDef != null ? Slot.Primary : SecondaryDef != null ? Slot.Secondary : Slot.Knife;

	/// <summary>
	/// Solta a arma da mão (B), jogando-a para frente, e passa para a
	/// próxima: da primária vai à secundária, da secundária à faca.
	/// Faca e granada não se soltam. A arma cai no chão com o pente que
	/// tinha, e quem passar por cima com o espaço vazio fica com ela.
	/// </summary>
	public bool TryDrop()
	{
		if (_form.IsWolf || _health.IsDead) return false;
		if (HumanSlot != Slot.Primary && HumanSlot != Slot.Secondary) return false;

		WeaponDefinition def = HumanSlot == Slot.Primary ? PrimaryDef : SecondaryDef;
		if (def == null) return false;

		int ammo = AmmoInMag;
		if (IsReloading) CancelReload();

		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		Vector3 from = _camera.GlobalPosition + forward * 0.45f + Vector3.Down * 0.25f;
		Vector3 velocity = _body.Velocity * 0.6f + forward * DropThrow + Vector3.Up * DropLift;
		WeaponPickup.Spawn(_body, def, ammo, from, velocity);

		Remove(HumanSlot);
		EmitSignal(SignalName.Dropped, def.Id);

		HumanSlot = BestSlot();
		_cooldown = 0.25f;
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
		return true;
	}

	/// <summary>Cabe esta arma? Só se o espaço dela estiver vazio — como
	/// no CS: para trocar, solte a sua primeiro.</summary>
	public bool CanTake(WeaponDefinition def)
	{
		if (def == null || _form.IsWolf || _health.IsDead) return false;
		return Arsenal.IsSecondary(def.Id) ? SecondaryDef == null : PrimaryDef == null;
	}

	/// <summary>
	/// Pega uma arma do chão para o espaço vazio. Vai para a mão quando
	/// for a melhor que você tem — quem soltou a primária para pegar
	/// outra quer atirar com ela, não continuar de pistola.
	/// </summary>
	public bool TryPickUp(WeaponDefinition def, int ammo)
	{
		if (!CanTake(def)) return false;

		bool secondary = Arsenal.IsSecondary(def.Id);
		if (secondary)
		{
			SecondaryDef = def;
			SecondaryAmmo = Mathf.Clamp(ammo, 0, SecondaryMag);
		}
		else
		{
			PrimaryDef = def;
			PrimaryAmmo = Mathf.Clamp(ammo, 0, PrimaryMag);
		}

		Slot target = secondary ? Slot.Secondary : Slot.Primary;
		bool take = secondary ? HumanSlot == Slot.Knife : HumanSlot != Slot.Primary;

		EmitSignal(SignalName.PickedUp, def.Id);

		if (take && HumanSlot != Slot.Grenade)
		{
			if (IsReloading) CancelReload();
			HumanSlot = target;
			_cooldown = 0.25f;
		}

		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
		return true;
	}

	private void Remove(Slot slot)
	{
		if (slot == Slot.Primary)
		{
			PrimaryDef = null;
			PrimaryAmmo = 0;
		}
		else if (slot == Slot.Secondary)
		{
			SecondaryDef = null;
			SecondaryAmmo = 0;
		}
	}

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
