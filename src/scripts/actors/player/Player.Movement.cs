using Godot;

/// <summary>
/// Andar, pular, agachar e o atordoamento da granada: o movimento no
/// chão e no ar, fora da parede e dos golpes do lobo.
/// </summary>
public partial class Player
{
	/// <summary>A passada da corrida do humano (o "run_fwd" anda 3,0 m/s
	/// a 1×): o corpo anda o que as pernas andam, sem acelerar o clipe
	/// (decisão do Jhon). Era 4,2, e o pé patinava.</summary>
	[ExportGroup("Movimento")]
	[Export] public float HumanSpeed = 3.0f;

	/// <summary>Impulso do pulo do lobo (vezes o da raça) e do salto da
	/// parede. O humano tem o dele, abaixo.</summary>
	[Export] public float JumpVelocity = 4.4f;

	/// <summary>
	/// O pulo do humano: mais baixo e bem mais rápido que o do lobo (pedido
	/// do Jhon, 17/11), com a gravidade do humano a 1,45×. Em 18/11 subiu
	/// um pouco ("o pulo do humano pode ser um pouco mais alto"): de 0,80 m
	/// para 0,99 m, 0,73 s no ar — volta a subir num caixote de 1 m. A
	/// gravidade maior vale para todo o humano no ar: cair de uma beirada
	/// também é mais seco. O lobo não muda.
	/// </summary>
	[Export] public float HumanJumpVelocity = 5.2f;
	[Export] public float HumanGravityScale = 1.45f;

	/// <summary>Aceleração no chão, em m/s². Alta deixa o controle
	/// responsivo sem virar teletransporte.</summary>
	[Export] public float GroundAccel = 60f;

	/// <summary>Desaceleração no chão ao soltar as teclas.</summary>
	[Export] public float GroundFriction = 50f;

	/// <summary>Aceleração no ar. Baixa, para o pulo ser um
	/// compromisso: você escolhe a trajetória antes de saltar.</summary>
	[Export] public float AirAccel = 18f;

	/// <summary>Fração da gravidade ao segurar o pulo durante a queda.
	/// Pouco abaixo de 1: dá um respiro no controle do salto sem virar
	/// planagem.</summary>
	[Export] public float HoldFallGravity = 0.78f;

	/// <summary>Arrasto no ar quando nenhuma tecla está pressionada.
	/// É o que dá controle sobre o alcance do pulo: segurar mantém o
	/// avanço, soltar encurta. Zero conserva tudo e tira a escolha.</summary>
	[Export] public float AirFriction = 7f;

	/// <summary>Quanto do passo o lobo leva para o pulo do chão. Vale para
	/// o pulo todo, até tocar o chão ou grudar numa parede — o salto da
	/// parede não entra, ele conserva o avanço da corrida. Com o Bruto a
	/// 5,7 m/s era 0,55, para ele não planar 6 m parado na pose de correr;
	/// com ele a 3,6 m/s, 0,9 mantém o pulo que o Jhon aprovou: uns 3,8 m
	/// para a frente.</summary>
	[Export] public float WolfJumpCarry = 0.9f;

	/// <summary>Fração da velocidade enquanto agachado.</summary>
	[Export] public float CrouchSpeedFactor = 0.45f;

	/// <summary>A corrida do humano (Shift, 26/11): × a velocidade normal,
	/// só para a frente e com a arma abaixada — correndo ele não atira, não
	/// mira e não recarrega. 1,9176 dá 4,91 m/s com o fuzil (o andar a
	/// 2,56): o "Rifle Run" do Mixamo (3,35 m/s a 1×) toca a 1,47×. Desde
	/// 27/11, em dois pedidos do Jhon ("aumente a velocidade do sprint do
	/// humano, em 15%"; depois, "pode aumentar a velocidade de corrida do
	/// humano em mais 15%"): era 1,45, 3,71 m/s, e passou por 1,6675, 4,27.
	/// Antes disso foi 4,3 m/s (rápido demais, "parece o Sonic") e 3,46
	/// ("pode deixar um pouco mais rápida", Jhon, 26/11). **Passa do lobo a
	/// pé** (4,33), por decisão dele. O andar devagar e em silêncio, que
	/// morava no Shift, saiu: quem quer silêncio agacha.</summary>
	[Export] public float HumanSprintFactor = 1.9176f;

	/// <summary>Quanto tempo a arma leva para voltar da corrida, em
	/// segundos: parou de correr (ou clicou correndo), só atira depois
	/// disto. É o preço da corrida: quem chega correndo chega de arma
	/// baixa. O clique dado no caminho fica guardado e sai ao fim. É o
	/// tempo de os braços trazerem a arma de volta
	/// (<see cref="ViewModel.RigSprintOut"/>): 0,28 s desde o fim de 27/11 ("a animação de sair da corrida não combinou, muito rápido"; de manhã tinha ido a 0,18: Jhon,
	/// "a velocidade de resposta da corrida... ao parar também"); era
	/// 0,23, e antes 0,2.</summary>
	[Export] public float SprintRecover = 0.28f;
	[Export] public float StandHeight = 1.75f;

	/// <summary>A cápsula agachada do lobo.</summary>
	[Export] public float CrouchHeight = 1.0f;

	/// <summary>O olho do lobo (o pivô da câmera de 3ª pessoa) em pé e agachado,
	/// a partir do centro da cápsula. Em pé, 0,6 é o da cena (o 0,62 de antes só
	/// valia depois de agachar e levantar uma vez).</summary>
	[Export] public float StandEyeHeight = 0.6f;
	[Export] public float CrouchEyeHeight = 0.1f;

	/// <summary>A cápsula agachada do humano (26/11). Era a do lobo, 1,0 m, com o
	/// olho a 60 cm do chão; agora o olho é o do corpo visto agachado (~140 cm,
	/// "precisam ficar mais altos") e a cápsula leva a cabeça dele (uns 15 cm
	/// acima do olho). Passar agachado por baixo de algo pede 1,5 m de vão. Desde 27/11, 1,42 m: o agachado
	/// desce 8 cm a mais (CharacterAnimator.CrouchSink; Jhon: "um pouco mais baixo, primeira e terceira
	/// pessoa"), e a cápsula desce junto com a cabeça.</summary>
	public const float DefaultHumanCrouchHeight = 1.42f;
	[Export] public float HumanCrouchHeight = DefaultHumanCrouchHeight;

	/// <summary>O olho do humano em pé e agachado, em metros acima dos pés.
	/// É o olho do corpo que os outros veem (Jhon, 26/11: "o nível de
	/// agachamento deve ser idêntico ao de 3ª pessoa, extremamente preciso"):
	/// o <see cref="CharacterAnimator"/> lê no corpo desenhado, parado e de
	/// cabeça reta, e ajusta estes (<see cref="TuneHumanEye"/>). Os de fábrica
	/// são os medidos na Eve e no Barut de fuzil na mão (159,2 e ~140 cm), para
	/// o corpo sem modelo (o servidor de partida) e o primeiro instante. Era
	/// 147,5 cm em pé (12 abaixo dos olhos do corpo) e 60 cm agachado (50 abaixo).</summary>
	[Export] public float HumanEyeHeight = 1.592f;
	[Export] public float HumanCrouchEyeHeight = 1.26f;   // (1,34 até 27/11, antes do CrouchSink)

	/// <summary>Quão rápido o olho do humano vai até o que o corpo visto mostra,
	/// em m/s: trocar de arma muda o tronco, e a câmera não dá tranco.</summary>
	[Export] public float EyeTuneSpeed = 0.4f;

	/// <summary>O olho do corpo visto, lido parado (em pé ou agachado), em metros
	/// acima dos pés. Chamado pelo <see cref="CharacterAnimator"/> do dono.</summary>
	public void TuneHumanEye(bool crouched, float height, float delta)
	{
		if (!float.IsFinite(height) || height < 0.3f || height > 2.5f) return;
		float step = EyeTuneSpeed * delta;
		if (crouched) HumanCrouchEyeHeight = Mathf.MoveToward(HumanCrouchEyeHeight, height, step);
		else HumanEyeHeight = Mathf.MoveToward(HumanEyeHeight, height, step);
	}

	/// <summary>A altura da cápsula agora (em pé, agachado ou no meio).</summary>
	public float BodyHeight => _height;

	/// <summary>Velocidade da transição entre em pé e agachado.</summary>
	[Export] public float CrouchLerpSpeed = 22f;

	/// <summary>Degrau que se sobe andando, sem pular, em metros: a borda
	/// do contêiner (14 cm), um estrado, um degrau de escada (pedido do
	/// Jhon, 17/11: o lobo correndo parava na borda do contêiner). O
	/// lobo sobe mais alto, de quatro.</summary>
	[Export] public float StepHeight = 0.3f;
	[Export] public float WolfStepHeight = 0.45f;

	/// <summary>Quão rápido a câmera alcança o corpo depois de um degrau
	/// sozinho (o meio-fio, a borda do contêiner), em m/s: o corpo sobe de uma
	/// vez, o olho vem logo atrás. Numa escada de degraus a câmera vai no ritmo
	/// dela (<see cref="StepRhythmWindow"/>).</summary>
	[Export] public float StepEase = 2.2f;

	/// <summary>
	/// Degraus seguidos com menos que isto entre um e outro (em segundos) são
	/// uma escada: a câmera sobe (ou desce) na velocidade em que os degraus
	/// vêm, constante, em vez de alcançar o corpo a <see cref="StepEase"/> e
	/// esperar o degrau seguinte. Era assim até 27/11 (Jhon: "ao subir escada
	/// com o humano, tá mexendo muito a câmera para cima e para baixo"):
	/// andando numa escada de 21 cm a velocidade vertical da câmera pulava de
	/// 0 a 2,2 m/s oito vezes por segundo, e correndo o olho ficava até 72 cm
	/// abaixo de onde devia (a escada subia mais rápido que os 2,2 m/s).
	/// </summary>
	[Export] public float StepRhythmWindow = 0.4f;

	/// <summary>O quanto a velocidade da câmera atrás do degrau muda por
	/// segundo (m/s²): ela não sai nem para de uma vez. Com 18, o arranque e a
	/// chegada mudam no máximo 0,3 m/s num quadro.</summary>
	[Export] public float StepAccel = 18f;

	/// <summary>Atrás do corpo mais que isto (m), a câmera apressa
	/// (<see cref="StepCatchUp"/> por metro a mais): o olho nunca fica muito
	/// abaixo de onde está o corpo.</summary>
	[Export] public float StepLag = 0.25f;
	[Export] public float StepCatchUp = 10f;

	private double _stunTimer;
	private float _stunSlow = 1f;

	/// <summary>Atordoado pela granada: lento e sem corrida em parede.
	/// É a única resposta que o humano tem contra o lobo.</summary>
	public bool IsStunned => _stunTimer > 0;

	/// <summary>No ar por um pulo do chão, de lobo: o passo fica em
	/// <see cref="WolfJumpCarry"/>.</summary>
	private bool _wolfJump;

	/// <summary>No ar por um pulo (do chão ou da parede), até tocar o
	/// chão, grudar numa parede ou começar um golpe. É o que o corpo do
	/// lobo lê para tocar o clipe de pulo.</summary>
	public bool IsJumpAirborne { get; private set; }

	/// <summary>Velocidade vertical na saída do último pulo: o clipe de
	/// pulo anda pela subida e pela queda medindo a velocidade de agora
	/// contra esta.</summary>
	public float JumpSpeed { get; private set; } = 1f;

	/// <summary>No ar por um salto da parede: o bote daqui avança menos
	/// (<see cref="Weapon.DiveForwardFromWall"/>).</summary>
	public bool JumpedFromWall => IsJumpAirborne && _jumpFromWall;
	private bool _jumpFromWall;

	/// <summary>A queda do passo anterior (<see cref="FallTracker"/>): na
	/// parede e na escada o corpo está apoiado; no salto da parede, no bote e
	/// no especial a queda não fere (Jhon, 26/11).</summary>
	private void UpdateFall() =>
		_fall.Update(this, _health, _fallProfile, _form.IsWolf, IsOnFloor() || _onWall || _onLadder,
			JumpedFromWall || _weapon.FallExempt);
	private CapsuleShape3D _capsule;
	private bool _crouching;

	/// <summary>Está agachado? Leitura para quem desenha o corpo. O
	/// agachamento é decidido aqui e em lugar nenhum mais.</summary>
	public bool IsCrouching => _crouching;

	/// <summary>O humano correndo (Shift): mais rápido, de arma abaixada.
	/// Leitura para o corpo animado, para os braços na tela e para a rede.
	/// A corrida de quatro do lobo é outra coisa: <see cref="IsSprinting"/>.</summary>
	public bool IsHumanSprinting => _humanSprint && !_health.IsDead;

	/// <summary>A arma ainda está voltando da corrida: não atira.</summary>
	public bool IsWeaponLowered => !_form.IsWolf && (_humanSprint || _sprintRecover > 0);

	/// <summary>
	/// Onde ficam os pés, em relação ao centro do corpo.
	///
	/// A cápsula encolhe ao agachar e o corpo desce metade da
	/// diferença, então a base **se move**: −0,875 de pé, −0,5
	/// agachado. Quem desenha o corpo precisa seguir isso, senão o
	/// modelo fica fixo na base de pé e afunda 37 cm no chão quando o
	/// jogador agacha.
	/// </summary>
	public float FeetOffset => -_height * 0.5f;
	private bool _humanSprint;
	private double _sprintRecover;
	private double _jumpLock;
	private float _height;

	/// <summary>Aplica atordoamento. O mais forte vence: uma segunda
	/// granada nao pode enfraquecer a primeira.</summary>
	public void ApplyStun(float duration, float slowFactor)
	{
		if (duration <= 0) return;

		if (duration > _stunTimer)
			_stunTimer = duration;

		_stunSlow = Mathf.Min(_stunSlow, Mathf.Clamp(slowFactor, 0.05f, 1f));
	}

	/// <summary>Só o humano: × a base (<see cref="HumanSpeed"/>, que é também
	/// a base do lobo e não muda). 0,8 desde 26/11: 2,56 m/s com o fuzil
	/// (Jhon: "o personagem pode andar mais lento"; a 3,0, "a corrida tá
	/// muito muito rápida, e o andar do humano também... deixar na
	/// velocidade de um humano"). Abaixo da passada da corrida do X Bot
	/// (3,02 a 1×): o corpo anda numa mistura do andar com a corrida, sem
	/// acelerar clipe. Era 1,08 (3,46 m/s) desde a v0.27.1.</summary>
	[Export] public float HumanSpeedBonus = 0.8f;

	/// <summary>A corrida de quatro do lobo: W duas vezes seguidas e ele
	/// cai nas quatro patas, isto mais rápido que a corrida normal
	/// (decisão do Jhon: 1,3 na v0.27.1; 15% a mais em 15/11, 1,495 — o
	/// Run_Quad toca a ~1,58×). Soltou o W, volta a correr em pé.
	/// Agachar corta. Na parede não soma: a parede tem a velocidade dela.</summary>
	[Export] public float WolfSprintFactor = 1.495f;

	/// <summary>De lobo, andando para trás: × a velocidade normal (pedido
	/// do Jhon, 15/11: recuar costuma ser mais lento que avançar, e recuar
	/// batendo na velocidade de quem persegue deixava o lobo intocável).
	/// Na diagonal de trás vale em parte, pela fração do passo que é para
	/// trás. O humano não muda.</summary>
	[Export] public float WolfBackFactor = 0.75f;

	/// <summary>De lobo, carregando o salto: × a velocidade normal. Anda
	/// agachado, devagar, à espreita (pedido do Jhon, 15/11: posicionar o
	/// salto andando; 16/11: mais devagar). 0,91 m/s no Bruto, a passada do
	/// Charge_Walk.</summary>
	[Export] public float WolfChargeMoveFactor = 0.234f;

	/// <summary>Tempo máximo entre os dois toques no W, em segundos.</summary>
	[Export] public float DoubleTapWindow = 0.3f;

	private bool _sprinting;
	private double _sinceForwardTap = double.MaxValue;

	/// <summary>Correndo de quatro? Leitura para o corpo animado.</summary>
	public bool IsSprinting => _sprinting;

	/// <summary>Liga a corrida de quatro no segundo toque do W e desliga
	/// quando ele solta, quando agacha ou quando deixa de ser lobo. Se
	/// estava carregando o salto, o W duplo cancela a carga.</summary>
	private void UpdateSprint(double delta)
	{
		if (!_form.IsWolf || !_intent.Forward || _crouching)
			_sprinting = false;

		if (_sinceForwardTap < double.MaxValue)
			_sinceForwardTap += delta;

		if (!_intent.ForwardTap) return;

		if (_form.IsWolf && !_crouching && _sinceForwardTap <= DoubleTapWindow)
		{
			// Carregando o salto, o W duplo desiste dele e sai correndo de
			// quatro (pedido do Jhon, 17/11): é o jeito de largar o especial
			// sem saltar. Não gasta a recarga.
			if (_weapon.IsChargingPounce) _weapon.CancelCharge();
			_sprinting = true;
		}

		_sinceForwardTap = 0;
	}

	/// <summary>
	/// A corrida do humano: Shift segurado, andando para a frente, em pé,
	/// sem atordoamento e com as mãos livres — apertar o gatilho, mirar,
	/// recarregar, armar a granada ou estocar tira da corrida. Começa no
	/// chão e segue no ar (o pulo correndo leva o embalo).
	///
	/// Só entra aqui o que o servidor autoritativo também sabe (o comando e
	/// o estado da arma que ele simula): a coronhada (o V não vai no comando)
	/// e a granada saindo da mão (só existe em quem tem a mão na tela)
	/// deixavam o jogo parar de correr e o servidor não, e o corpo era
	/// puxado de volta.
	/// </summary>
	private void UpdateHumanSprint(double delta)
	{
		bool ahead = _intent.Forward && _intent.Move.Y < -0.6f;
		// O clique guardado (Player.Actions) também segura a corrida até o tiro sair: um
		// clique rápido com o Shift apertado soltava o botão antes de a arma voltar, a
		// corrida voltava, e o tiro só saía quando o jogador parasse de correr.
		bool hands = !_intent.AttackHeld && !_intent.AimHeld && !_weapon.IsReloading && !_weapon.IsGrenadeReady
			&& !_weapon.IsStabbing && !_sprintClick;
		bool wants = !_form.IsWolf && _intent.SprintHeld && ahead && !_crouching && !_onWall && !IsStunned && hands;
		// O fôlego (SprintBreath, 27/11): acabou, para de correr e espera um pouco.
		bool now = wants && (_humanSprint || IsOnFloor()) && Breath.CanSprint;
		Breath.Step((float)delta, now);
		if (!Breath.CanSprint) now = false;

		if (_humanSprint && !now) _sprintRecover = SprintRecover;
		else if (_sprintRecover > 0) _sprintRecover -= delta;
		if (_form.IsWolf) _sprintRecover = 0;
		_humanSprint = now;
		ReportBreath();
	}

	/// <summary>Morreu ou ficou preso na contagem: a corrida acaba, sem a
	/// espera da arma.</summary>
	private void StopHumanSprint()
	{
		_humanSprint = false;
		_sprintRecover = 0;
		_sprintClick = false;
		_breath?.Reset();
		ReportBreath();
	}

	/// <summary>O fôlego da corrida: segundos de corrida, segundos para encher de novo e a espera sem fôlego
	/// (<see cref="SprintBreath"/>; Jhon, 27/11).</summary>
	[Export] public float SprintBreathTime = 4f;
	[Export] public float SprintBreathRefill = 3f;
	[Export] public float SprintWindedWait = 1.5f;

	/// <summary>O fôlego mudou (de 0 a 1), acabou, ou a corrida começou ou parou: para o HUD.</summary>
	[Signal] public delegate void BreathChangedEventHandler(float fraction, bool winded, bool running);

	private SprintBreath _breath;
	public SprintBreath Breath => _breath ??= new SprintBreath(SprintBreathTime, SprintBreathRefill, SprintWindedWait);
	private float _breathShown = -1f;
	private bool _windedShown, _runningShown;

	/// <summary>Avisa o HUD quando o fôlego muda de verdade (o HUD não pergunta).</summary>
	private void ReportBreath()
	{
		SprintBreath b = Breath;
		if (Mathf.Abs(b.Fraction - _breathShown) < 0.01f && b.Winded == _windedShown && _humanSprint == _runningShown) return;
		_breathShown = b.Fraction;
		_windedShown = b.Winded;
		_runningShown = _humanSprint;
		EmitSignal(SignalName.BreathChanged, b.Fraction, b.Winded, _humanSprint);
	}

	private float SpeedScale =>
		(_form.IsWolf ? _form.Breed.SpeedMultiplier : HumanSpeedBonus)
		* (IsStunned ? _stunSlow : 1f)
		* (_crouching ? CrouchSpeedFactor : 1f)
		* (_humanSprint ? HumanSprintFactor : 1f)
		// A arma na mão pesa: 1,12 com a M9, 0,9 com a M3, e a faca alivia.
		// É metade do que faz trocar para a secundária ser fuga e não só
		// munição.
		// Correndo, a arma leve não soma (Jhon, 27/11: "velocidade da corrida com faca pode diminuir... tem que
		// ser igual o sprint com fuzil"; "o sprint do humano com pistola tá muito rápido também"): faca (× 1,28),
		// pistola (× 1,08) e fuzil correm a mesma coisa, 4,91 m/s; a arma pesada (AK, SCAR, SVD) segue mais lenta.
		// Andando, cada uma continua com a sua.
		* (_form.IsWolf ? 1f : _humanSprint ? Mathf.Min(_weapon.MoveSpeedMultiplier, 1f) : _weapon.MoveSpeedMultiplier);
	private float JumpScale => _form.IsWolf ? _form.Breed.JumpMultiplier : 1f;

	private void NormalMovement(double delta)
	{
		EaseStep(delta);
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			// Segurar o pulo durante a queda alivia um pouco a
			// gravidade. É sutil de propósito: dá controle fino sobre
			// onde aterrissar sem transformar o salto em planagem.
			bool holding = velocity.Y < 0 && _intent.JumpHeld;
			float scale = (holding ? HoldFallGravity : 1f) * (_form.IsWolf ? 1f : HumanGravityScale);

			velocity.Y -= _gravity * scale * (float)delta;
		}
		else if (WantsJump())
		{
			velocity.Y = _form.IsWolf ? JumpVelocity * JumpScale : HumanJumpVelocity;
			IsJumpAirborne = true;
			_jumpFromWall = false;
			JumpSpeed = velocity.Y;

			// O lobo pula para cima, não para a frente.
			if (_form.IsWolf)
			{
				_wolfJump = true;
				velocity.X *= WolfJumpCarry;
				velocity.Z *= WolfJumpCarry;
			}

			_animator ??= GetNodeOrNull<CharacterAnimator>("Human");
			_animator?.Jump();
		}

		// Rugindo no chão o lobo fica preso no lugar: o rugido é de corpo
		// inteiro, ele para para rugir (Jhon, 15/11). No ar ruge também, e o
		// corpo segue o pulo para a frente como vinha até pousar (16/11).
		Vector2 input = _howling && IsOnFloor() ? Vector2.Zero : _intent.Move;
		Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
		float speed = HumanSpeed * SpeedScale;
		if (_sprinting) speed *= WolfSprintFactor;
		if (_weapon.IsChargingPounce) speed *= WolfChargeMoveFactor;
		if (_form.IsWolf && input.Y > 0f)
			speed *= Mathf.Lerp(1f, WolfBackFactor, input.Y / Mathf.Max(input.Length(), 0.001f));
		if (_wolfJump) speed *= WolfJumpCarry;

		bool grounded = IsOnFloor();
		var horizontal = new Vector3(velocity.X, 0, velocity.Z);

		if (direction != Vector3.Zero)
		{
			float accel = grounded ? GroundAccel : AirAccel;
			horizontal = horizontal.MoveToward(direction * speed, accel * (float)delta);
		}
		else
		{
			// No chão freia rápido. No ar o arrasto é suave: o corpo
			// segue a trajetória do pulo, mas soltar a tecla encurta o
			// alcance — é assim que o jogador controla a distância.
			float drag = grounded ? GroundFriction : AirFriction;
			horizontal = horizontal.MoveToward(Vector3.Zero, drag * (float)delta);
		}

		// O passo da garrada parada: o corpo anda junto com o pé do clipe.
		if (_weapon.IsStepping)
			horizontal = _weapon.StepVelocity;

		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;

		Velocity = velocity;
		Vector3 before = GlobalPosition;
		// Descer degrau (27/11): andando no chão, sem pular, o corpo fica grudado
		// nele até um degrau abaixo (o ímã do Godot, que fora daqui fica no padrão).
		bool walking = grounded && velocity.Y <= 0.01f;
		// (o ímã volta ao que era: a corrida na parede e o resto contam com o
		// valor do corpo — repor "o padrão" à mão soltava o lobo da parede.)
		float snap = FloorSnapLength;
		// Só o humano: no lobo o ímã maior o arrancava da corrida na parede ao
		// passar rente a um degrau ou batente (ParedeNoArmazem, ParedeNoNeon), e a
		// câmera dele é de terceira pessoa — a escada não sacode a tela.
		bool glued = walking && !_form.IsWolf;
		if (glued) FloorSnapLength = Mathf.Max(snap, StepHeight);
		MoveAndSlide();
		FloorSnapLength = snap;
		if (walking && direction != Vector3.Zero)
			TryStepUp(before, horizontal * (float)delta);
		if (glued && IsOnFloor()) SteppedDown(before.Y - GlobalPosition.Y);
	}

	/// <summary>A câmera atrás do corpo depois de um degrau (m; negativo: abaixo
	/// dele, subindo), a velocidade com que ela o alcança, e o ritmo da escada.</summary>
	private float _stepLift, _stepSpeed, _stepRate;
	private double _sinceStep = double.MaxValue;

	/// <summary>
	/// Subir um degrau baixo andando. O <c>MoveAndSlide</c> trata a quina
	/// do degrau como parede e para o corpo nela. Então, se o corpo no
	/// chão andou bem menos do que queria e bateu em parede: tenta o mesmo
	/// passo de um degrau acima e desce até achar chão. Achou chão, firme e
	/// mais alto que antes, o corpo fica lá; a câmera desce o que subiu e
	/// vem atrás (<see cref="EaseStep"/>), para o degrau não dar tranco.
	/// </summary>
	private void TryStepUp(Vector3 from, Vector3 wanted)
	{
		var flatWanted = new Vector3(wanted.X, 0, wanted.Z);
		float want = flatWanted.Length();
		if (want < 0.0005f || !IsOnWall()) return;

		Vector3 moved = GlobalPosition - from;
		if (new Vector2(moved.X, moved.Z).Length() > want * 0.6f) return;

		float height = _form.IsWolf ? WolfStepHeight : StepHeight;
		Vector3 up = Vector3.Up * height;
		// No mínimo alguns centímetros: andando devagar o passo do quadro
		// não passaria da quina.
		Vector3 ahead = flatWanted.Normalized() * Mathf.Max(want, 0.12f);

		if (!BodyMotion.ProbeStep(this, from, up, ahead, FloorMaxAngle, out Vector3 landed)) return;
		float rise = landed.Y - from.Y;
		if (rise < 0.02f) return;

		GlobalPosition = landed;
		Stepped(rise);
	}

	/// <summary>Quanto o corpo pode descer num passo, andando no chão, e ainda
	/// ser declive (a rampa, a escada com chapa): o lobo de quatro a 7,4 m/s
	/// numa rampa de 35° desce uns 7 cm por passo. Mais que isto foi o ímã
	/// puxando o corpo um degrau abaixo.</summary>
	private const float SlopeDropPerStep = 0.08f;

	/// <summary>
	/// O corpo desceu <paramref name="drop"/> neste passo, andando no chão (27/11).
	/// Até então o corpo só grudava no chão até 10 cm, e num degrau de escada (21
	/// cm) saía do chão e caía, pulando degraus, e a câmera ia junto aos trancos;
	/// agora o ímã o segura até um degrau abaixo (<see cref="StepHeight"/>), e o que
	/// passa de um declive vira um degrau descido: a câmera fica onde estava e
	/// vem atrás (<see cref="EaseStep"/>).
	/// </summary>
	private void SteppedDown(float drop)
	{
		if (drop > SlopeDropPerStep) Stepped(-drop);
	}

	/// <summary>O corpo subiu (<paramref name="rise"/> positivo) ou desceu um
	/// degrau de uma vez: a câmera fica onde estava e passa a ir atrás dele; e
	/// dois degraus seguidos dão o ritmo da escada.</summary>
	private void Stepped(float rise)
	{
		_stepRate = _sinceStep < StepRhythmWindow ? Mathf.Abs(rise) / (float)Mathf.Max(_sinceStep, 0.001) : 0f;
		_sinceStep = 0;
		_stepLift -= rise;
		Vector3 head = _head.Position;
		head.Y -= rise;
		_head.Position = head;
		// o alisamento do desenho não reparte este pedaço pelo quadro: o olho já compensa
		_smoothing ??= GetNodeOrNull<VisualSmoothing>("VisualSmoothing");
		_smoothing?.Stepped(Vector3.Up * rise);
	}

	private VisualSmoothing _smoothing;

	/// <summary>O olho alcança o corpo depois de um degrau: no ritmo da escada,
	/// se há uma (<see cref="StepRhythmWindow"/>), ou a <see cref="StepEase"/>;
	/// apressando se ficou longe (<see cref="StepLag"/>); e sem sair nem parar
	/// de uma vez (<see cref="StepAccel"/>).</summary>
	private void EaseStep(double delta)
	{
		_sinceStep += delta;
		if (_sinceStep > StepRhythmWindow) _stepRate = 0f;
		if (_stepLift == 0f) { _stepSpeed = 0f; return; }

		float dt = (float)delta, distance = Mathf.Abs(_stepLift);
		float want = (_stepRate > 0f ? _stepRate : StepEase) + Mathf.Max(0f, distance - StepLag) * StepCatchUp;
		// chegando, freia para parar no lugar (v² = 2·a·d)
		want = Mathf.Min(want, Mathf.Sqrt(2f * StepAccel * distance));
		_stepSpeed = Mathf.MoveToward(_stepSpeed, want, StepAccel * dt);
		// (sempre anda um pouco: a freada não pode deixar a câmera parada a um fio do corpo)
		float previous = _stepLift;
		_stepLift = Mathf.MoveToward(_stepLift, 0f, Mathf.Max(_stepSpeed, 0.05f) * dt);
		Vector3 head = _head.Position;
		head.Y += _stepLift - previous;
		_head.Position = head;
	}

	/// <summary>
	/// Agachar vale nas duas formas: encolhe a cápsula, baixa o olho e
	/// corta a velocidade. Na parede não se agacha — a corrida já é um
	/// estado de corpo inteiro.
	/// </summary>
	private void UpdateCrouch(double delta)
	{
		_crouching = !_onWall && _intent.CrouchHeld;

		float crouchHeight = _form.IsWolf ? CrouchHeight : HumanCrouchHeight;
		float target = _crouching ? crouchHeight : StandHeight;
		float previous = _height;

		_height = Mathf.MoveToward(_height, target, CrouchLerpSpeed * (float)delta);

		// O olho a cada passo: muda com a forma (o humano e o lobo têm olhos
		// diferentes), não só quando a cápsula muda.
		PlaceEye(crouchHeight);

		if (Mathf.IsEqualApprox(previous, _height))
			return;

		_capsule.Height = _height;

		// A cápsula encolhe em torno do próprio centro, então os pés
		// sobem no ar e a gravidade os traz de volta — o que aparece
		// como um agachamento em dois tempos. Descer o corpo metade da
		// diferença mantém os pés plantados e o movimento contínuo.
		Vector3 position = GlobalPosition;
		position.Y += (_height - previous) * 0.5f;
		GlobalPosition = position;

		PlaceEye(crouchHeight);
	}

	/// <summary>
	/// O olho acompanha a cápsula na mesma proporção, entre o de pé e o
	/// agachado da forma de agora. O do humano é medido acima dos pés (o olho do
	/// corpo visto); a cabeça é posta a partir do centro da cápsula.
	/// </summary>
	private void PlaceEye(float crouchHeight)
	{
		float t = Mathf.Clamp(Mathf.InverseLerp(crouchHeight, StandHeight, _height), 0f, 1f);
		float eye = _form.IsWolf
			? Mathf.Lerp(CrouchEyeHeight, StandEyeHeight, t)
			: Mathf.Lerp(HumanCrouchEyeHeight, HumanEyeHeight, t) - _height * 0.5f;

		Vector3 head = _head.Position;
		head.Y = eye + _stepLift;
		_head.Position = head;
	}

	/// <summary>
	/// Um toque, um pulo. Segurar a tecla não repete, e a trava curta
	/// impede que o mesmo toque dispare o pulo do chão e o da parede.
	/// </summary>
	/// <summary>Quanto a tecla de pulo fica guardada, em segundos: apertou
	/// um pouco antes de pousar (ou com o lobo ainda se levantando de um
	/// golpe), o pulo sai no primeiro passo em que pode. Sem isso a tecla
	/// apertada cedo se perdia e o lobo parecia não responder (Jhon, 20/11).</summary>
	[Export] public float JumpBuffer = 0.15f;

	private double _jumpBuffered;

	/// <summary>Guarda a tecla de pulo deste passo (ou desconta o tempo da guardada).</summary>
	private void BufferJump(double delta)
	{
		if (_intent.Jump) _jumpBuffered = JumpBuffer;
		else if (_jumpBuffered > 0) _jumpBuffered -= delta;
	}

	/// <summary>Tem pulo pedido (agora ou guardado)?</summary>
	private bool JumpPending => _jumpBuffered > 0;

	private bool WantsJump()
	{
		if (_jumpLock > 0) return false;
		if (_jumpBuffered <= 0) return false;

		_jumpBuffered = 0;
		_jumpLock = 0.2;
		return true;
	}
}
