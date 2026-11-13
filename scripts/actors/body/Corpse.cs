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

	/// <summary>Renasceu: a colisão volta.</summary>
	public void Revive()
	{
		Settled = false;
		_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
	}
}
