using Godot;

/// <summary>
/// O corpo durante os golpes especiais do lobo — o mesmo para o jogador e
/// para o bot. Enquanto a arma está com o corpo (o bote, o voo do salto,
/// o lobo preso ao chão no salto e na aterrissagem), é ela quem diz para
/// onde ele vai; o resto do movimento (andar, pular, parede) espera.
///
/// Antes cada um tinha a sua cópia, e toda correção precisava ser feita
/// duas vezes (a parada depois do voo foi).
/// </summary>
public sealed class WolfMoveMotor
{
	/// <summary>Teto da velocidade no voo do salto: o corpo vai ao ponto
	/// do arco no quadro seguinte, mas nunca rápido a ponto de atravessar
	/// parede.</summary>
	public const float MaxFlightSpeed = 28f;

	private readonly CharacterBody3D _body;
	private readonly Weapon _weapon;
	private bool _pounceFlying;

	/// <summary>Freada de quem fica preso no lugar, em m/s².</summary>
	public float FreezeBrake { get; set; } = 30f;

	public WolfMoveMotor(CharacterBody3D body, Weapon weapon)
	{
		_body = body;
		_weapon = weapon;
	}

	/// <summary>A arma está com o corpo neste quadro?</summary>
	public bool Controls => _weapon.IsDiving || _weapon.IsPounceFlying || _weapon.IsRooted;

	/// <summary>Move o corpo se a arma estiver com ele. Devolve verdadeiro
	/// quando moveu — aí o resto do movimento fica para o próximo
	/// quadro.</summary>
	public bool Step(double delta, float gravity)
	{
		if (_weapon.IsDiving)
		{
			Dive();
			return true;
		}

		if (_weapon.IsPounceFlying)
		{
			Fly(delta);
			_pounceFlying = true;
			return true;
		}

		// Acabou o voo: o lobo fica onde caiu. Sem isto, o embalo de um voo
		// barrado no caminho (alguém na frente) virava um deslize extra
		// depois da queda, e o salto andava mais do que a barra mandou.
		if (_pounceFlying)
		{
			_pounceFlying = false;
			_body.Velocity = new Vector3(0, Mathf.Min(_body.Velocity.Y, 0), 0);
		}

		// O salto e a aterrissagem do bote prendem o lobo: golpe pesado não
		// se faz andando.
		if (_weapon.IsRooted)
		{
			Freeze(delta, gravity);
			return true;
		}

		return false;
	}

	/// <summary>
	/// O bote: o corpo segue a velocidade fixada no clique, em linha reta,
	/// até encostar em algo. Encostou, para seco e avisa a arma, que bate.
	/// Parede só conta se o bote vai de encontro a ela: saindo de uma
	/// corrida na parede, o corpo ainda roça nela no primeiro quadro.
	/// </summary>
	private void Dive()
	{
		_body.Velocity = _weapon.DiveVelocity;
		_body.MoveAndSlide();

		Vector3 heading = _weapon.DiveVelocity.Normalized();
		bool wall = _body.IsOnWall() && _body.GetWallNormal().Dot(heading) < -0.3f;

		if (!_body.IsOnFloor() && !wall && !_body.IsOnCeiling()) return;

		_weapon.DiveContact();
		_body.Velocity = Vector3.Zero;
	}

	/// <summary>O voo do salto: por posição, não por força, para cair onde
	/// foi planejado. A física ainda manda: bater em algo para o corpo ali.</summary>
	private void Fly(double delta)
	{
		Vector3 next = _weapon.PounceFlightPoint(delta);
		_body.Velocity = ((next - _body.GlobalPosition) / (float)delta).LimitLength(MaxFlightSpeed);
		_body.MoveAndSlide();
	}

	/// <summary>Preso no lugar: sem andar nem pular, só a gravidade. O
	/// golpe pode andar pelo corpo (o passo da garrada).</summary>
	private void Freeze(double delta, float gravity)
	{
		Vector3 frozen = _body.Velocity;
		Vector3 flat = _weapon.IsStepping
			? _weapon.StepVelocity
			: new Vector3(frozen.X, 0, frozen.Z).MoveToward(Vector3.Zero, FreezeBrake * (float)delta);

		frozen.X = flat.X;
		frozen.Z = flat.Z;
		if (!_body.IsOnFloor())
			frozen.Y -= gravity * (float)delta;

		_body.Velocity = frozen;
		_body.MoveAndSlide();
	}
}
