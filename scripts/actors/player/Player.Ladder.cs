using Godot;

/// <summary>
/// A escada (<see cref="Ladder"/>), igual em todo mapa e nas duas formas:
///
/// - agarra andando para ela, no pé da escada, ou caindo dentro dela (quem
///   sai da passarela pela abertura da escada fica preso nela, não cai);
/// - na escada não há gravidade: W sobe, S desce, e olhando bem para baixo
///   o W desce (como nos jogos de tiro de sempre); parado, fica parado;
/// - A e D andam de lado, devagar, para acertar o corpo nos degraus;
/// - no alto, subindo, o corpo é empurrado para a frente, para cima do
///   piso de cima; no pé, descendo, volta a andar;
/// - o pulo solta da escada para trás.
/// </summary>
public partial class Player
{
	[ExportGroup("Escada")]
	/// <summary>Velocidade de subida e descida, em m/s (3,6 m em 1,6 s).</summary>
	[Export] public float LadderSpeed = 2.2f;

	/// <summary>Olhando mais para baixo que isto (radianos, ~35°), o W desce.</summary>
	[Export] public float LadderLookDown = 0.6f;

	/// <summary>Velocidade para a frente ao chegar no alto: leva o corpo para
	/// cima do piso de cima.</summary>
	[Export] public float LadderExitPush = 2.5f;

	/// <summary>Velocidade do pulo para trás, que solta da escada.</summary>
	[Export] public float LadderJumpOff = 3f;

	private bool _onLadder;
	private double _ladderCooldown;

	/// <summary>Está na escada (sem gravidade, subindo ou descendo).</summary>
	public bool OnLadder => _onLadder;

	private bool TryLadder(double delta)
	{
		if (_ladderCooldown > 0) _ladderCooldown -= delta;
		Ladder ladder = _ladderCooldown > 0 ? null : Ladder.At(GlobalPosition);
		if (ladder == null)
		{
			_onLadder = false;
			return false;
		}

		Vector2 input = _intent.Move;
		float forward = -input.Y;
		float climb = forward * (_head.Rotation.X < -LadderLookDown ? -1f : 1f);
		Vector3 wish = Transform.Basis * new Vector3(input.X, 0, input.Y);
		bool grounded = IsOnFloor();

		// Pisando no piso de cima (chegou, ou está na beira da passarela):
		// anda normal. Para descer, sai da beira pela abertura e cai na escada.
		if (grounded && GlobalPosition.Y > ladder.Bottom + 1.2f)
		{
			_onLadder = false;
			return false;
		}

		// Do chão só se agarra no pé da escada, subindo e andando para ela.
		if (!_onLadder && grounded && (climb <= 0f || wish.Dot(ladder.Facing) < 0.3f))
			return false;

		if (_intent.Jump && _onLadder)
		{
			_onLadder = false;
			_ladderCooldown = 0.35;
			Velocity = -ladder.Facing * LadderJumpOff + Vector3.Up * JumpVelocity * 0.5f;
			MoveAndSlide();
			return true;
		}

		// No pé da escada, descendo: chegou, volta a andar.
		if (_onLadder && grounded && climb < 0f)
		{
			_onLadder = false;
			return false;
		}

		_onLadder = true;
		ReleaseWall();
		IsJumpAirborne = false;

		Vector3 velocity = Vector3.Up * Mathf.Clamp(climb, -1f, 1f) * LadderSpeed;
		velocity += ladder.Right * wish.Dot(ladder.Right) * LadderSpeed * 0.5f;
		// Encostado nos degraus: sem isto o corpo deriva para fora do volume.
		velocity += ladder.Facing * 0.5f;
		// No alto, subindo: para a frente, para cima do piso.
		if (climb > 0f && GlobalPosition.Y > ladder.Top - 0.15f)
			velocity += ladder.Facing * LadderExitPush;

		Velocity = velocity;
		MoveAndSlide();
		return true;
	}
}
