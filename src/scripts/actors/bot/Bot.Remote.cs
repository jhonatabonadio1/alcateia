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
	/// <summary>A foto de perfil do jogador (no placar).</summary>
	public string RemotePhoto { get; set; }

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
		// O boneco deste bot nas outras máquinas corre de quatro e ruge
		// quando ele corre e ruge aqui (21/11: antes adivinhava pela velocidade).
		Sprinting = IsSprinting,
		Howling = Action == BotAction.Howl,
		Hp = _health.Hp,
		MaxHp = _health.MaxHp,
		Shield = _health.Shield,
		MaxShield = _health.MaxShield,
		Primary = _weapon.PrimaryDef?.Id ?? "",
		Secondary = _weapon.SecondaryDef?.Id ?? "",
		HasGear = true,
		Ammo = (short)_weapon.AmmoInMag,
		Reloading = _weapon.IsReloading,
		Grenades = (byte)Mathf.Clamp(_weapon.Grenades, 0, 255),
		Wp = _form.Wp,
		Charge = _weapon.SpecialCharge
	};

	public float HeadPitch => _head.Rotation.X;

	/// <summary>A passada do corpo (a mesma conta do som do passo): a arma
	/// na mão balança com ela quando alguém assiste este bot em primeira
	/// pessoa (o espectador).</summary>
	public int Steps => _noise.Steps;
	public float StridePhase => _noise.StridePhase;

	/// <summary>Quão rápido o boneco alcança a posição noticiada, e até
	/// quanto se adianta pela velocidade, em segundos.
	///
	/// 21/11 (Jhon: "sinto um leve delay"): o boneco corria atrás da posição
	/// com 1/14 s de atraso a mais (a perseguição suave sempre fica uma
	/// fração para trás) e só se adiantava o tempo desde a última notícia.
	/// Agora ele se adianta também esse atraso da perseguição
	/// (<see cref="RemoteChaseLag"/>): fica onde o dono estava ao mandar,
	/// sem somar o próprio atraso.</summary>
	private const float RemoteCatchUp = 18f, RemoteLead = 0.2f, RemoteSnap = 6f;

	/// <summary>O atraso da perseguição suave, em segundos (1/RemoteCatchUp).</summary>
	private const float RemoteChaseLag = 1f / RemoteCatchUp;

	/// <summary>Quão rápido o boneco vira para onde o dono olha.</summary>
	private const float RemoteTurn = 26f;

	/// <summary>O que o dono está fazendo e o boneco não tem como adivinhar
	/// (o estado que chegou, desde 21/11): correndo de quatro, agachado,
	/// rugindo, andando quieto. Antes a corrida de quatro era achada pela
	/// velocidade (o lobo de quem andava em pé aparecia de quatro), e o
	/// agachar e o rugido não apareciam para ninguém.</summary>
	public bool RemoteSprinting { get; private set; }
	public bool RemoteCrouching { get; private set; }
	public bool RemoteHowling { get; private set; }
	public bool RemoteQuiet { get; private set; }

	/// <summary>Chegou o que o dono está fazendo (o estado).</summary>
	public void RemoteMotion(bool sprinting, bool crouching, bool howling, bool quiet, bool grenadeReady, bool aiming = false)
	{
		RemoteSprinting = sprinting;
		RemoteCrouching = crouching;
		RemoteHowling = howling;
		RemoteQuiet = quiet;
		_weapon.RemoteGrenadeReady = grenadeReady;
		// Na mira: a arma do boneco sobe como a do dono (Weapon.UpdateAim
		// anda igual; recarregando ou sem arma de fogo, não sobe).
		_weapon.SetAiming(aiming);
	}

	// ---- agachado: a cápsula do boneco encolhe como a do dono

	/// <summary>A altura da cápsula agachada, a do dono: a do humano
	/// (<see cref="Player.HumanCrouchHeight"/>, 1,5 m desde 26/11) ou a do lobo
	/// (<see cref="Player.CrouchHeight"/>, 1,0). O desenho sobe a diferença para
	/// os pés ficarem no chão (RemoteCrouchDrop): com a altura errada, o boneco
	/// agachado flutuava ou afundava.</summary>
	private float RemoteCrouchHeight => _form != null && _form.IsWolf ? 1.0f : Player.DefaultHumanCrouchHeight;

	private CapsuleShape3D _remoteCapsule;
	private float _remoteStand = 1.75f, _remoteHeight = 1.75f;

	/// <summary>Quanto o centro do corpo desceu agachado, em metros: o
	/// desenho sobe isso para os pés continuarem no chão (a posição que
	/// chega é o centro da cápsula do dono, que desce ao agachar).</summary>
	public float RemoteCrouchDrop => (_remoteStand - _remoteHeight) * 0.5f;

	/// <summary>A cápsula do boneco é só dele (a da cena é uma para todos
	/// os bots): agachar um não encolhe os outros.</summary>
	private void OwnCapsule()
	{
		var shape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (shape?.Shape is not CapsuleShape3D capsule) return;
		_remoteCapsule = (CapsuleShape3D)capsule.Duplicate();
		shape.Shape = _remoteCapsule;
		_remoteStand = _remoteHeight = capsule.Height;
	}

	/// <summary>
	/// O dono agachou: a cápsula encolhe em torno do centro, como a dele
	/// (o tiro por cima da cabeça abaixada passa; antes passava pelo boneco
	/// em pé e acertava o ar acima de quem estava agachado), e o desenho
	/// desce o corpo com os pés no chão. Na mesma velocidade do jogador.
	/// </summary>
	private void UpdateRemoteCrouch(float dt)
	{
		if (_remoteCapsule == null) return;
		float target = RemoteCrouching && !_health.IsDead ? Mathf.Min(RemoteCrouchHeight, _remoteStand) : _remoteStand;
		if (Mathf.IsEqualApprox(_remoteHeight, target)) return;
		_remoteHeight = Mathf.MoveToward(_remoteHeight, target, 22f * dt);
		_remoteCapsule.Height = _remoteHeight;
	}

	/// <summary>O dono está correndo na parede (o estado que chegou). O
	/// lobo do boneco deita na parede e engatinha, como o de quem joga
	/// (Jhon, 21/11: na partida online o lobo dos outros na parede saía
	/// com a animação de pulo).</summary>
	public bool RemoteWallRun { get; set; }

	/// <summary>A parede em que o boneco corre: a normal (para fora dela)
	/// e o sentido da corrida, achados aqui, perto do corpo — a rede não
	/// manda. Zero fora da parede.</summary>
	public Vector3 RemoteWallNormal { get; private set; }
	public Vector3 RemoteWallAlong { get; private set; }

	/// <summary>Até onde, de cada lado do corpo, procurar a parede.</summary>
	private const float RemoteWallReach = 1.1f;

	private Vector3 _netPosition, _netVelocity;
	private float _netYaw, _netPitch;
	private bool _netGrounded = true, _netKnown;
	private double _netAge;

	// Partida autoritativa: a pose já vem interpolada (NetMatch), e a
	// vida e as armas chegam à parte (Status).
	private bool _netInterpolated;
	private string _netPrimary = "", _netSecondary = "";

	/// <summary>
	/// Partida autoritativa (decisão 0011): a pose que o servidor deu,
	/// já interpolada entre dois estados do mundo, um pouco no passado.
	/// O boneco vai exatamente para lá a cada passo — o desenho entre
	/// passos é do <see cref="VisualSmoothing"/>.
	/// </summary>
	public void RemoteSnapshot(Vector3 position, Vector3 velocity, float yaw, float pitch, bool grounded, Form form, Slot slot)
	{
		RemoteInterpolated(position, velocity, yaw, pitch, grounded);
		_form.RemoteSet(form);
		if (!_form.IsWolf) _weapon.RemoteHold(_netPrimary, _netSecondary, slot);
	}

	/// <summary>
	/// A pose do dono já interpolada entre dois estados, um pouco no passado
	/// (a partida autoritativa, e a de repasse quando o dono conta o instante
	/// de cada estado, desde 26/11). O boneco vai exatamente para lá a cada
	/// passo; a notícia que chega (<see cref="RemoteUpdate"/>) passa a dizer só
	/// a vida, a forma e as armas.
	/// </summary>
	public void RemoteInterpolated(Vector3 position, Vector3 velocity, float yaw, float pitch, bool grounded)
	{
		_netInterpolated = true;
		_netPosition = position;
		_netVelocity = velocity;
		_netYaw = yaw;
		_netPitch = pitch;
		_netGrounded = grounded;

		if (!_netKnown)
		{
			GlobalPosition = position;
			Rotation = new Vector3(0, yaw, 0);
			RemotePresent(true);
			_netKnown = true;
		}
	}

	/// <summary>Partida autoritativa: vida, escudo e armas do dono (Status).</summary>
	public void RemoteStatus(float hp, float maxHp, float shield, float maxShield, bool dead, string primary, string secondary)
	{
		_netPrimary = primary ?? "";
		_netSecondary = secondary ?? "";
		_health.Mirror(hp, maxHp, shield, maxShield, dead);
	}

	/// <summary>Partida autoritativa: o pente, a recarga, a carga do
	/// especial, as granadas e o WP do dono (o fim do Status, desde 19/11).</summary>
	public void RemoteGear(int ammo, bool reloading, int grenades, float wp, float charge)
	{
		_weapon.MirrorGear(ammo, reloading, charge, grenades);
		_form.MirrorWp(wp);
	}

	private void BecomeRemote()
	{
		_health.AutoRespawn = false;
		_weapon.Puppet = true;
		_netPosition = GlobalPosition;
		_netYaw = Rotation.Y;
		_layer = CollisionLayer;
		OwnCapsule();

		// Até o dono dar notícia, o boneco não existe para ninguém.
		RemotePresent(false);
	}

	private uint _layer;

	/// <summary>Põe ou tira o boneco do mundo: o dono ainda não chegou,
	/// ou saiu da partida.</summary>
	public void RemotePresent(bool present)
	{
		Visible = present;
		// A cópia do replay nunca colide: tiro de verdade passa por ela.
		CollisionLayer = present ? _layer : 0;
		if (!present) _netKnown = _netHasLast = false;
	}

	/// <summary>Chegou notícia do dono: onde está, para onde olha, em
	/// que forma, com o quê na mão e com quanta vida.</summary>
	public void RemoteUpdate(Vector3 position, Vector3 velocity, float yaw, float pitch, bool grounded,
		Form form, Slot slot, string primary, string secondary,
		float hp, float maxHp, float shield, float maxShield, bool dead)
	{
		// Desenhado pelo caminho interpolado (o dono conta o instante): a pose
		// vem de lá; a notícia diz o resto.
		if (!_netInterpolated)
		{
			_netPosition = position;
			_netVelocity = velocity;
			_netYaw = yaw;
			_netPitch = pitch;
			_netGrounded = grounded;
		}
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

	/// <summary>O dono atacou: o boneco encena. Com <paramref name="move"/>
	/// (o golpe do lobo + 1, desde 21/11), toca exatamente aquele golpe, com
	/// os números dele (NetWire).</summary>
	public void RemoteAttack(Slot slot, float yaw, float pitch, int move = 0, float a = 0f, float b = 0f)
	{
		if (_health.IsDead) return;

		Rotation = new Vector3(0, yaw, 0);
		_head.Rotation = new Vector3(pitch, 0, 0);
		if (move == NetWire.KnifeStabMove)
			_weapon.RemoteStab();
		else if (move == NetWire.ButtMove)
			_weapon.RemoteButt(slot);
		else if (move > 0 && System.Enum.IsDefined(typeof(WolfMove), move - 1))
			_weapon.RemoteWolfMove((WolfMove)(move - 1), a, b);
		else
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

		if (_netInterpolated)
		{
			SmoothNetJump(dt);
			GlobalPosition = _netPosition;
			Rotation = new Vector3(0, _netYaw, 0);
			_head.Rotation = new Vector3(_netPitch, 0, 0);
		}
		else
		{
			Vector3 target = _netPosition + _netVelocity * Mathf.Min((float)_netAge + RemoteChaseLag, RemoteLead);
			GlobalPosition = GlobalPosition.DistanceTo(target) > RemoteSnap
				? target
				: GlobalPosition.Lerp(target, 1f - Mathf.Exp(-RemoteCatchUp * dt));

			float turn = 1f - Mathf.Exp(-RemoteTurn * dt);
			Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, _netYaw, turn), 0);
			_head.Rotation = new Vector3(Mathf.LerpAngle(_head.Rotation.X, _netPitch, turn), 0, 0);
		}

		UpdateRemoteCrouch(dt);

		// Um toque no chão (ou para fora dele) só para o corpo saber se
		// está pisando: a animação e o som do passo perguntam isso.
		Velocity = _netGrounded ? Vector3.Down * 0.5f : Vector3.Up * 0.05f;
		MoveAndSlide();
		Velocity = _netVelocity;

		UpdateRemoteWall();
		UpdateNoise(delta);
	}

	/// <summary>
	/// Correndo na parede, a parede: o sentido é o do movimento (parado,
	/// agarrado, o de antes), e a parede é a mais perto de um dos lados
	/// dele. Sem parede achada, fica a última — numa quina a sonda pode
	/// errar um passo.
	/// </summary>
	/// <summary>Um passo do boneco que destoa do anterior mais que isto (em
	/// metros, na horizontal: 3 m/s de mudança num passo) é a rede, não o corpo.</summary>
	private const float NetJumpTolerance = 0.05f;

	private Vector3 _netLast, _netLastStep;
	private bool _netHasLast;

	/// <summary>
	/// O desenho entre um estado e outro é liso, mas a pose que vem da rede
	/// ainda pode dar um degrau: o desenho que adivinhou à frente quando os
	/// estados pararam e o caminho de verdade que chega depois, um estado que
	/// chegou fora de hora. O corpo vai para a pose certa (o tiro e a colisão
	/// a usam); o desenho (<see cref="VisualSmoothing"/>) não pula junto: fica
	/// e escorrega até ela em uma fração de segundo (26/11, "os personagens
	/// voltam no tempo e a tela fica tremendo"). Só na horizontal: pulo e
	/// pouso mudam a velocidade de uma vez de verdade.
	/// </summary>
	private void SmoothNetJump(float dt)
	{
		Vector3 step = _netPosition - _netLast;
		if (_netHasLast)
		{
			Vector3 odd = step - _netLastStep;
			odd.Y = 0;
			float size = odd.Length();
			// Teleporte (renasceu, voltou à partida): o desenho vai direto.
			if (size > NetJumpTolerance && size < VisualSmoothing.TeleportStep)
			{
				if (!_health.IsDead) GetNodeOrNull<VisualSmoothing>("VisualSmoothing")?.Absorb(odd);
				// O que se absorveu não conta como o passo de agora (o seguinte compara com o normal).
				step -= odd;
			}
		}
		_netLast = _netPosition;
		_netLastStep = step;
		_netHasLast = true;
	}

	private void UpdateRemoteWall()
	{
		if (!RemoteWallRun || _health.IsDead)
		{
			RemoteWallNormal = RemoteWallAlong = Vector3.Zero;
			return;
		}

		var flat = new Vector3(_netVelocity.X, 0, _netVelocity.Z);
		Vector3 along = flat.LengthSquared() > 0.04f ? flat.Normalized()
			: RemoteWallAlong != Vector3.Zero ? RemoteWallAlong
			: new Vector3(-GlobalBasis.Z.X, 0, -GlobalBasis.Z.Z).Normalized();
		if (along == Vector3.Zero) return;

		Vector3 side = along.Cross(Vector3.Up).Normalized();
		Vector3 bestNormal = Vector3.Zero;
		float best = float.MaxValue;
		foreach (Vector3 dir in new[] { side, -side })
			if (ProbeRemoteWall(dir, out Vector3 normal, out float distance) && distance < best)
			{
				best = distance;
				bestNormal = normal;
			}

		if (bestNormal == Vector3.Zero)
		{
			if (RemoteWallNormal == Vector3.Zero) return;
			bestNormal = RemoteWallNormal;
		}

		Vector3 onWall = along - bestNormal * along.Dot(bestNormal);
		RemoteWallNormal = bestNormal;
		RemoteWallAlong = onWall.LengthSquared() > 1e-4f ? onWall.Normalized() : along;
	}

	private bool ProbeRemoteWall(Vector3 dir, out Vector3 normal, out float distance)
	{
		normal = Vector3.Zero;
		distance = 0f;
		var space = GetWorld3D().DirectSpaceState;
		Vector3 from = GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(from, from + dir * RemoteWallReach);
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };
		for (int i = 0; i < 4; i++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return false;
			// Gente e os limites invisíveis do mapa não são parede.
			if (hit["collider"].Obj is CharacterBody3D || Player.IsInvisibleLimit(hit["collider"].Obj))
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}
			Vector3 n = (Vector3)hit["normal"];
			n.Y = 0;
			if (n.LengthSquared() < 0.25f) return false;   // chão ou teto inclinado
			normal = n.Normalized();
			distance = from.DistanceTo((Vector3)hit["position"]);
			return true;
		}
		return false;
	}
}
