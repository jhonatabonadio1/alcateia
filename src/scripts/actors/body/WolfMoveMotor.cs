using Godot;
using System.Collections.Generic;

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
	/// <summary>Teto da velocidade no voo do salto, em m/s: o corpo vai ao
	/// ponto do arco no quadro seguinte. O MoveAndSlide varre o caminho, então
	/// não atravessa parede; o teto é só contra um ponto absurdo. Era 45, e o
	/// salto de 13 m do especial (58 m/s no meio do sino) ficava para trás e
	/// corrigia no fim.</summary>
	public const float MaxFlightSpeed = 90f;

	private readonly CharacterBody3D _body;
	private readonly Weapon _weapon;
	private bool _pounceFlying;

	/// <summary>O que o salto atravessou no caminho (quem não é o alvo
	/// travado, postes e placas), até o lobo sair de dentro dele
	/// (<see cref="ReleasePassed"/>).</summary>
	private readonly List<PhysicsBody3D> _passed = new();

	/// <summary>A cápsula do corpo, um pouco mais gorda, para saber se o lobo
	/// já saiu de dentro do que atravessou.</summary>
	private PhysicsShapeQueryParameters3D _overlap;

	/// <summary>Freada de quem fica preso no lugar, em m/s².</summary>
	public float FreezeBrake { get; set; } = 30f;

	public WolfMoveMotor(CharacterBody3D body, Weapon weapon)
	{
		_body = body;
		_weapon = weapon;
	}

	/// <summary>A arma está com o corpo neste quadro?</summary>
	public bool Controls => _weapon.IsDiving || _weapon.IsPounceFlying || (_weapon.IsRooted && !_weapon.IsChargingPounce);

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

		ReleasePassed();

		// Acabou o voo: o lobo fica onde caiu. Sem isto, o embalo de um voo
		// barrado no caminho (alguém na frente) virava um deslize extra
		// depois da queda, e o salto andava mais do que a barra mandou.
		if (_pounceFlying)
		{
			_pounceFlying = false;
			_body.Velocity = new Vector3(0, Mathf.Min(_body.Velocity.Y, 0), 0);
		}

		// O salto e a aterrissagem do bote prendem o lobo: golpe pesado não
		// se faz andando. A carga não: carregando, ele anda encolhido para
		// ajeitar o salto (pedido do Jhon, 15/11); prende ao soltar.
		if (_weapon.IsRooted && !_weapon.IsChargingPounce)
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
	/// foi planejado. A física ainda manda: bater em algo para o corpo ali.
	///
	/// Bater em alguém para no chão, de verdade: o avanço do quadro vai até
	/// encostar e o resto do voo é só descer. O deslizar da física (o certo
	/// numa parede) fazia o lobo escorregar de lado na cápsula do outro, dar
	/// a volta nele e cair do outro lado, de costas para ele — o "vai e volta
	/// no ar" que errava o alvo (Jhon, 25/11).
	///
	/// Com alvo travado (o especial), quem mais estiver no caminho é
	/// atravessado: o salto é dele, e o lobo cai em quem travou. Sem trava,
	/// vale a regra de sempre: para em quem encostar.</summary>
	private void Fly(double delta)
	{
		Vector3 motion = _weapon.PounceFlightPoint(delta) - _body.GlobalPosition;
		Vector3 flat = motion with { Y = 0 };
		var contact = new KinematicCollision3D();
		for (int tries = 0; tries < 3 && flat.LengthSquared() > 1e-8f; tries++)
		{
			if (!_body.TestMove(_body.GlobalTransform, flat, contact) || contact.GetCollider() is not CharacterBody3D someone) break;

			Node3D focus = _weapon.PounceFocus;
			if (focus != null && someone != focus)
			{
				Pass(someone);
				continue;
			}

			Vector3 travel = contact.GetTravel() with { Y = 0 };
			motion = travel + Vector3.Up * motion.Y;
			_weapon.PounceBlocked(_body.GlobalPosition + travel);
			break;
		}

		// Poste, placa, haste: o voo atravessa (o caminho do salto não conta
		// com eles, Weapon.WolfPouncePath). Um poste de 12 cm no meio do
		// caminho parava o lobo no ar e o fazia escorregar em volta dele.
		for (int tries = 0; tries < 4 && motion.LengthSquared() > 1e-8f; tries++)
		{
			if (!_body.TestMove(_body.GlobalTransform, motion, contact)) break;
			if (contact.GetCollider() is not PhysicsBody3D prop
				|| !Weapon.IsThinProp(prop, contact.GetColliderShapeIndex(), _weapon.PounceThinProp)) break;
			Pass(prop);
		}

		_body.Velocity = (motion / (float)delta).LimitLength(MaxFlightSpeed);
		_body.MoveAndSlide();
	}

	/// <summary>Quem o salto atravessou volta a ser sólido assim que o lobo
	/// sai de cima dele — e não no quadro da queda, quando os dois ainda
	/// podiam estar encostados e a física os separaria num tranco.</summary>
	private void ReleasePassed()
	{
		if (_passed.Count == 0) return;

		var inside = new HashSet<Rid>();
		_overlap ??= new PhysicsShapeQueryParameters3D { Shape = new CapsuleShape3D { Radius = 0.36f, Height = 1.8f }, CollideWithAreas = false };
		_overlap.Transform = new Transform3D(Basis.Identity, _body.GlobalPosition);
		_overlap.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		foreach (var hit in _body.GetWorld3D().DirectSpaceState.IntersectShape(_overlap, 16))
			inside.Add((Rid)hit["rid"]);

		for (int i = _passed.Count - 1; i >= 0; i--)
		{
			PhysicsBody3D other = _passed[i];
			bool gone = !GodotObject.IsInstanceValid(other) || !other.IsInsideTree();
			if (!gone && inside.Contains(other.GetRid())) continue;

			if (!gone) _body.RemoveCollisionExceptionWith(other);
			_passed.RemoveAt(i);
		}
	}

	/// <summary>Atravessa <paramref name="other"/> até sair de dentro dele.</summary>
	private void Pass(PhysicsBody3D other)
	{
		_body.AddCollisionExceptionWith(other);
		if (!_passed.Contains(other)) _passed.Add(other);
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
