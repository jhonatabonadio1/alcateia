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
	[Export] public float JumpVelocity = 4.4f;

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
	private CapsuleShape3D _capsule;
	private bool _crouching;

	/// <summary>Está agachado? Leitura para quem desenha o corpo. O
	/// agachamento é decidido aqui e em lugar nenhum mais.</summary>
	public bool IsCrouching => _crouching;

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

	private float SpeedScale =>
		(_form.IsWolf ? _form.Breed.SpeedMultiplier : 1f)
		* (IsStunned ? _stunSlow : 1f)
		* (_crouching ? CrouchSpeedFactor : 1f)
		* (_walking ? WalkSpeedFactor : 1f)
		// A arma na mão pesa: 1,12 com a M9, 0,9 com a M3. É metade do
		// que faz trocar para a secundária ser fuga e não só munição.
		* (_form.IsWolf ? 1f : _weapon.MoveSpeedMultiplier);
	private float JumpScale => _form.IsWolf ? _form.Breed.JumpMultiplier : 1f;

	private void NormalMovement(double delta)
	{
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			// Segurar o pulo durante a queda alivia um pouco a
			// gravidade. É sutil de propósito: dá controle fino sobre
			// onde aterrissar sem transformar o salto em planagem.
			bool holding = velocity.Y < 0 && _intent.JumpHeld;
			float scale = holding ? HoldFallGravity : 1f;

			velocity.Y -= _gravity * scale * (float)delta;
		}
		else if (WantsJump())
		{
			velocity.Y = JumpVelocity * JumpScale;
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

		Vector2 input = _intent.Move;
		Vector3 direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
		float speed = HumanSpeed * SpeedScale;
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
		MoveAndSlide();
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
		head.Y = Mathf.Lerp(CrouchEyeHeight, StandEyeHeight, t);
		_head.Position = head;
	}

	/// <summary>
	/// Um toque, um pulo. Segurar a tecla não repete, e a trava curta
	/// impede que o mesmo toque dispare o pulo do chão e o da parede.
	/// </summary>
	private bool WantsJump()
	{
		if (_jumpLock > 0) return false;
		if (!_intent.Jump) return false;

		_jumpLock = 0.2;
		return true;
	}
}
