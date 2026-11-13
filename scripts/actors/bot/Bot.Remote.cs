using Godot;

/// <summary>
/// O bot como boneco de outro jogador (partida online).
///
/// Mesmo corpo, mesma animação, mesma arma na mão — mas quem decide tudo
/// é a máquina do dono: aqui só se segue o que a rede conta. A posição é
/// alisada entre uma notícia e a seguinte, e adiantada um pouco pela
/// velocidade, para o boneco não andar aos trancos.
/// </summary>
public partial class Bot
{
	/// <summary>Este corpo é de outro jogador. Definido antes de entrar
	/// na cena, por quem monta a partida.</summary>
	public bool Remote { get; set; }

	/// <summary>A conta do dono e a patente dele, para o placar. Boneco
	/// de bot (o bot que a máquina do dono da sala move) tem número
	/// negativo e não tem patente.</summary>
	public long RemoteId { get; set; }
	public int RemoteRank { get; set; }

	/// <summary>Partida online, bot de verdade, movido por esta máquina:
	/// o número dele na escalação (negativo). Zero fora disso.</summary>
	public long NetId { get; set; }

	/// <summary>Como o servidor conhece este corpo: o número do boneco, ou
	/// o do bot que esta máquina move.</summary>
	public long OnlineId => Remote ? RemoteId : NetId;

	/// <summary>Boneco de um bot, não de gente.</summary>
	public bool RemoteIsBot => Remote && RemoteId < 0;

	/// <summary>O boneco está no mundo? Antes de o dono dar notícia (ou
	/// depois de sair) ele não existe para ninguém — nem para os bots.</summary>
	public bool RemoteAbsent => Remote && !Visible;

	/// <summary>O que a rede precisa saber deste bot, movido aqui.</summary>
	public NetState NetSnapshot() => new()
	{
		Position = GlobalPosition,
		Velocity = Velocity,
		Yaw = Rotation.Y,
		Pitch = _head.Rotation.X,
		Form = (byte)_form.Current,
		Slot = (byte)_weapon.HumanSlot,
		Grounded = IsOnFloor(),
		Dead = _health.IsDead,
		Hp = _health.Hp,
		MaxHp = _health.MaxHp,
		Shield = _health.Shield,
		MaxShield = _health.MaxShield,
		Primary = _weapon.PrimaryDef?.Id ?? "",
		Secondary = _weapon.SecondaryDef?.Id ?? ""
	};

	public float HeadPitch => _head.Rotation.X;

	/// <summary>Quão rápido o boneco alcança a posição noticiada, e o
	/// quanto se adianta pela velocidade, em segundos.</summary>
	private const float RemoteCatchUp = 14f, RemoteLead = 0.2f, RemoteSnap = 6f;

	private Vector3 _netPosition, _netVelocity;
	private float _netYaw, _netPitch;
	private bool _netGrounded = true, _netKnown;
	private double _netAge;

	private void BecomeRemote()
	{
		_health.AutoRespawn = false;
		_weapon.Puppet = true;
		_netPosition = GlobalPosition;
		_netYaw = Rotation.Y;
		_layer = CollisionLayer;

		// Até o dono dar notícia, o boneco não existe para ninguém.
		RemotePresent(false);
	}

	private uint _layer;

	/// <summary>Põe ou tira o boneco do mundo: o dono ainda não chegou,
	/// ou saiu da partida.</summary>
	public void RemotePresent(bool present)
	{
		Visible = present;
		CollisionLayer = present ? _layer : 0;
		if (!present) _netKnown = false;
	}

	/// <summary>Chegou notícia do dono: onde está, para onde olha, em
	/// que forma, com o quê na mão e com quanta vida.</summary>
	public void RemoteUpdate(Vector3 position, Vector3 velocity, float yaw, float pitch, bool grounded,
		Form form, Slot slot, string primary, string secondary,
		float hp, float maxHp, float shield, float maxShield, bool dead)
	{
		_netPosition = position;
		_netVelocity = velocity;
		_netYaw = yaw;
		_netPitch = pitch;
		_netGrounded = grounded;
		_netAge = 0;

		if (!_netKnown)
		{
			_netKnown = true;
			GlobalPosition = position;
			Rotation = new Vector3(0, yaw, 0);
			RemotePresent(true);
			_netKnown = true;
		}

		_form.RemoteSet(form);
		_weapon.RemoteHold(primary, secondary, slot);
		_health.Mirror(hp, maxHp, shield, maxShield, dead);
	}

	/// <summary>O dono atacou: o boneco encena.</summary>
	public void RemoteAttack(Slot slot, float yaw, float pitch)
	{
		if (_health.IsDead) return;

		Rotation = new Vector3(0, yaw, 0);
		_head.Rotation = new Vector3(pitch, 0, 0);
		_weapon.RemoteAttack(slot);
	}

	private void RemoteStep(double delta)
	{
		if (!_netKnown) return;

		if (_health.IsDead)
		{
			Settle(delta);
			return;
		}

		float dt = (float)delta;
		_netAge += delta;

		Vector3 target = _netPosition + _netVelocity * Mathf.Min((float)_netAge, RemoteLead);
		GlobalPosition = GlobalPosition.DistanceTo(target) > RemoteSnap
			? target
			: GlobalPosition.Lerp(target, 1f - Mathf.Exp(-RemoteCatchUp * dt));

		float turn = 1f - Mathf.Exp(-18f * dt);
		Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _netYaw, turn), 0);
		_head.Rotation = new Vector3(Mathf.LerpAngle(_head.Rotation.X, _netPitch, turn), 0, 0);

		// Um toque no chão (ou para fora dele) só para o corpo saber se
		// está pisando: a animação e o som do passo perguntam isso.
		Velocity = _netGrounded ? Vector3.Down * 0.5f : Vector3.Up * 0.05f;
		MoveAndSlide();
		Velocity = _netVelocity;

		UpdateNoise(delta);
	}
}
