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

		if (_sliding)
		{
			Slide(delta, gravity);
			return;
		}

		if (_body.IsOnFloor())
		{
			// Parou numa saliência (a viga que sai do muro, um peitoril, a quina de
			// um toldo), com o chão de verdade mais abaixo? Continua descendo.
			if (Perched(out _slideFloor))
			{
				_sliding = true;
				_body.Velocity = Vector3.Zero;
				_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
				return;
			}
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

	/// <summary>Quanto os pés podem ficar acima do chão que há debaixo do meio do
	/// corpo e ainda ser "no chão" (m).</summary>
	public const float PerchGap = 0.08f;

	/// <summary>Até onde se procura o chão debaixo do corpo que parou numa saliência (m).</summary>
	public const float PerchReach = 6f;

	private bool _sliding;
	private float _slideFloor;

	/// <summary>Metade da altura da cápsula: do meio do corpo aos pés.</summary>
	private float HalfHeight => _shape.Shape is CapsuleShape3D capsule ? capsule.Height * 0.5f : 0.875f;

	/// <summary>
	/// O corpo parou em cima de alguma coisa, mas o chão debaixo do meio dele está
	/// mais abaixo (27/11; Jhon: "quando o lobo morre ou o humano, ele tá ficando
	/// no ar"). Medido no Deserto: o lobo que morre correndo na parede cai rente a
	/// ela, a cápsula encosta numa saliência do muro (as vigas que saem do adobe) e
	/// assentava ali, 28 cm acima do chão, com o corpo deitado no ar. Só o cenário
	/// conta: um raio do meio do corpo para baixo, sem os outros corpos.
	/// </summary>
	private bool Perched(out float floor)
	{
		floor = 0f;
		World3D world = _body.GetWorld3D();
		if (world == null) return false;
		Vector3 from = _body.GlobalPosition;
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		for (int tries = 0; tries < 4; tries++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * (HalfHeight + PerchReach), 1, exclude);
			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0) return false;
			if (hit["collider"].AsGodotObject() is CharacterBody3D other)
			{
				exclude.Add(other.GetRid());
				continue;
			}
			floor = ((Vector3)hit["position"]).Y;
			return from.Y - HalfHeight - floor > PerchGap;
		}
		return false;
	}

	/// <summary>Desce reto, sem colisão, até o chão achado debaixo do corpo.</summary>
	private void Slide(double delta, float gravity)
	{
		Vector3 velocity = new(0, _body.Velocity.Y - gravity * (float)delta, 0);
		Vector3 at = _body.GlobalPosition + velocity * (float)delta;
		if (at.Y - HalfHeight <= _slideFloor)
		{
			at.Y = _slideFloor + HalfHeight;
			velocity = Vector3.Zero;
			Settled = true;
		}
		_body.Velocity = velocity;
		_body.GlobalPosition = at;
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
		_sliding = false;
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
		_sliding = false;
		_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
	}
}
