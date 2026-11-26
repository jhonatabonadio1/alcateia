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

	/// <summary>Fração da velocidade andando com Shift. Só no humano:
	/// o lobo é puro movimento e não teria por que abrir mão disso.</summary>
	[Export] public float WalkSpeedFactor = 0.42f;
	[Export] public float StandHeight = 1.75f;
	[Export] public float CrouchHeight = 1.0f;

	/// <summary>Altura do olho em pé e agachado.</summary>
	[Export] public float StandEyeHeight = 0.62f;
	[Export] public float CrouchEyeHeight = 0.1f;

	/// <summary>Velocidade da transição entre em pé e agachado.</summary>
	[Export] public float CrouchLerpSpeed = 22f;

	/// <summary>Degrau que se sobe andando, sem pular, em metros: a borda
	/// do contêiner (14 cm), um estrado, um degrau de escada (pedido do
	/// Jhon, 17/11: o lobo correndo parava na borda do contêiner). O
	/// lobo sobe mais alto, de quatro.</summary>
	[Export] public float StepHeight = 0.3f;
	[Export] public float WolfStepHeight = 0.45f;

	/// <summary>Quão rápido a câmera alcança o corpo depois de um degrau,
	/// em m/s: o corpo sobe de uma vez, o olho vem logo atrás.</summary>
	[Export] public float StepEase = 2.2f;

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

	/// <summary>Andando devagar (Shift, só humano): o passo não faz barulho.
	/// A rede conta aos outros (o passo do boneco também fica quieto).</summary>
	public bool IsWalking => _walking;

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
	private bool _walking;
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

	/// <summary>Só o humano: anda isto acima da base (<see cref="HumanSpeed"/>,
	/// que é também a base do lobo e não muda). 1,08 desde a v0.27.1
	/// (decisão do Jhon): 3,24 m/s com o fuzil, e agachado e com Shift
	/// junto. A corrida do X Bot anda 3,02 a 1×, então o pé desliza um
	/// pouco acima disso.</summary>
	[Export] public float HumanSpeedBonus = 1.08f;

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

	private float SpeedScale =>
		(_form.IsWolf ? _form.Breed.SpeedMultiplier : HumanSpeedBonus)
		* (IsStunned ? _stunSlow : 1f)
		* (_crouching ? CrouchSpeedFactor : 1f)
		* (_walking ? WalkSpeedFactor : 1f)
		// A arma na mão pesa: 1,12 com a M9, 0,9 com a M3, e a faca alivia.
		// É metade do que faz trocar para a secundária ser fuga e não só
		// munição.
		* (_form.IsWolf ? 1f : _weapon.MoveSpeedMultiplier);
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
		MoveAndSlide();
		if (grounded && direction != Vector3.Zero && velocity.Y <= 0.01f)
			TryStepUp(before, horizontal * (float)delta);
	}

	private float _stepLift;

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
		_stepLift -= rise;
		Vector3 head = _head.Position;
		head.Y -= rise;
		_head.Position = head;
	}

	/// <summary>O olho alcança o corpo depois de um degrau.</summary>
	private void EaseStep(double delta)
	{
		if (_stepLift == 0f) return;

		float previous = _stepLift;
		_stepLift = Mathf.MoveToward(_stepLift, 0f, StepEase * (float)delta);
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

		// Andar devagar é ferramenta do humano. O lobo vive de
		// movimento; dar a ele a opção de abrir mão disso só criaria
		// um lobo furtivo, que é o oposto da forma.
		_walking = !_onWall && !_form.IsWolf && _intent.WalkHeld;

		float target = _crouching ? CrouchHeight : StandHeight;
		float previous = _height;

		_height = Mathf.MoveToward(_height, target, CrouchLerpSpeed * (float)delta);

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

		// O olho acompanha a cápsula na mesma proporção.
		float t = Mathf.InverseLerp(CrouchHeight, StandHeight, _height);

		Vector3 head = _head.Position;
		head.Y = Mathf.Lerp(CrouchEyeHeight, StandEyeHeight, t) + _stepLift;
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
