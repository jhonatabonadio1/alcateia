using Godot;

/// <summary>
/// O corpo de quem morreu: a gravidade trabalha, com a colisão ligada,
/// até ele tocar o chão — quem morre no ar precisa cair. Só então a
/// colisão desliga e o corpo para onde parou.
///
/// Desligar antes derrubava o corpo pelo cenário. Nunca desligar deixava o
/// cadáver sendo empurrado para fora do chão quadro a quadro, e com
/// colisão de dois lados isso podia resolver para o lado errado. E depois
/// de assentado nem a gravidade age: sem colisão não existe chão, e ela
/// puxaria o corpo através do mapa.
/// </summary>
public sealed class Corpse
{
	private readonly CharacterBody3D _body;
	private readonly CollisionShape3D _shape;

	public bool Settled { get; private set; }

	public Corpse(CharacterBody3D body, CollisionShape3D shape)
	{
		_body = body;
		_shape = shape;
	}

	public void Settle(double delta, float gravity)
	{
		if (Settled) return;

		if (_body.IsOnFloor())
		{
			_body.Velocity = Vector3.Zero;
			Settled = true;
			_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
		}
		else
		{
			_body.Velocity = new Vector3(0, _body.Velocity.Y - gravity * (float)delta, 0);
		}

		_body.MoveAndSlide();
	}

	/// <summary>
	/// Sai da física na hora, sem assentar: o corpo do servidor (partida
	/// autoritativa), que morto não dá passo — e por isso nunca passava
	/// por <see cref="Settle"/>. A cápsula ficava sólida, invisível, onde
	/// ele morreu, até renascer: quem passava por ali era barrado no
	/// servidor e puxado de volta no jogo, que já não via ninguém (Jhon,
	/// 22/11: "o servidor não deixava eu andar para frente, por um tempo").
	/// </summary>
	public void Drop()
	{
		Settled = true;
		_body.Velocity = Vector3.Zero;
		_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
	}

	private bool _fallingAway;

	/// <summary>
	/// Quem morreu caindo do mapa continua caindo (Jhon, 27/11: "ao cair no
	/// precipício, o corpo deve continuar caindo"): o embalo que tinha, mais a
	/// gravidade, sem colisão — embaixo só há nuvem. Até 27/11 o corpo parava
	/// pendurado no limite de queda. Abaixo de <paramref name="floor"/>, já
	/// sumido na neblina, ele para (<see cref="Drop"/>): cair para sempre só
	/// faria os números crescerem.
	/// </summary>
	public void FallAway(double delta, float gravity, float floor)
	{
		if (Settled) return;
		if (!_fallingAway)
		{
			_fallingAway = true;
			_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
		}

		Vector3 velocity = _body.Velocity;
		velocity.Y -= gravity * (float)delta;
		_body.Velocity = velocity;
		_body.GlobalPosition += velocity * (float)delta;
		if (_body.GlobalPosition.Y < floor) Drop();
	}

	/// <summary>Renasceu: a colisão volta.</summary>
	public void Revive()
	{
		Settled = false;
		_fallingAway = false;
		_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
	}
}
