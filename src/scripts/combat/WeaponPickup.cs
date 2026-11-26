using Godot;
using System.Collections.Generic;

/// <summary>
/// Arma caída no chão: solta com B ou deixada por quem morreu.
///
/// Quem passa por cima com o espaço daquela arma vazio fica com ela, com
/// o pente e a reserva como estavam (<see cref="Weapon.TryPickUp"/>).
/// Com o espaço ocupado pela mesma arma, junta a munição dela na reserva
/// (até o teto). Com outra arma, nada acontece — para trocar, solte a sua
/// primeiro. É a regra do CS, e é o que impede o jogador de trocar de
/// arma sem querer ao passar por cima de um corpo.
///
/// O voo é feito à mão, com raio de um quadro para o outro, e não com
/// corpo rígido. Corpo rígido no chão vira obstáculo para todo raio do
/// jogo — o tiro, a visão do bot, a câmera do lobo — e uma arma caída
/// não pode segurar bala nem esconder ninguém. Aqui ela é só uma área:
/// os raios passam direto.
///
/// Some sozinha depois de <see cref="Lifetime"/> segundos, e o mapa
/// guarda no máximo <see cref="MaxOnMap"/>: uma partida de dez minutos
/// com dezesseis pessoas morrendo encheria o chão.
///
/// **Online** (Jhon, 26/11: "arma largada não aparece pros outros
/// jogadores... devo conseguir pegar a arma de qualquer jogador"): a arma
/// no chão é da partida, não de uma máquina. Quem soltou avisa
/// (<see cref="IPickupLink.Announce"/>) e todas as máquinas a mostram, no
/// mesmo lugar e com a mesma munição. Pegar é pedir
/// (<see cref="IPickupLink.Claim"/>): só o corpo desta máquina (o jogador,
/// ou um bot dela) pede, e quem decide é o servidor — o primeiro pedido
/// leva, a arma some de todas as telas e ninguém pega duas vezes
/// (<see cref="Grant"/>). O boneco de quem está em outra máquina nunca
/// pega nem solta: isso é da máquina dele. Sem ligação (offline, treino),
/// tudo fica como sempre: quem passa por cima leva.
/// </summary>
public partial class WeaponPickup : Area3D
{
	/// <summary>A arma no chão pela rede. Quem implementa é a partida online
	/// (<see cref="NetMatch"/>); fora dela, nulo. A arma no chão não sabe de
	/// rede: só pergunta quem manda e avisa o que acontece.</summary>
	public interface IPickupLink
	{
		/// <summary>O corpo é desta máquina (o jogador, ou um bot dela)? Só
		/// ele solta e pega: o boneco de outra máquina, não.</summary>
		bool Controls(Node3D body);

		/// <summary>Uma arma saiu de um corpo desta máquina: o número dela na
		/// partida (0: não vai pela rede).</summary>
		long Announce(WeaponPickup pickup, Node3D dropper, Vector3 from, Vector3 velocity);

		/// <summary>Este corpo quer a arma: o pedido vai ao servidor.</summary>
		void Claim(WeaponPickup pickup, Node3D taker);
	}

	public static IPickupLink Link { get; set; }

	/// <summary>Quanto se espera a resposta de um pedido antes de pedir de novo.</summary>
	public const double ClaimTimeout = 2.0;
	public const float Lifetime = 30f;
	public const int MaxOnMap = 20;

	/// <summary>Raio em que se pega a arma.</summary>
	public const float Reach = 0.9f;

	/// <summary>Quem soltou não pega de volta logo em seguida. Sem isto,
	/// soltar andando para frente devolveria a arma na mão no quadro
	/// seguinte.</summary>
	public const float DropperDelay = 1.2f;

	private const float FallAcceleration = 14f;

	private static readonly List<WeaponPickup> Live = new();

	public WeaponDefinition Definition { get; private set; }
	public int Ammo { get; private set; }

	/// <summary>A reserva que vinha com a arma.</summary>
	public int Reserve { get; private set; }

	/// <summary>O número desta arma na partida online (0 offline).</summary>
	public long NetId { get; private set; }

	/// <summary>Os corpos com um pedido em espera: um pedido por vez, para
	/// duas armas do mesmo espaço não chegarem juntas (a segunda se perderia).</summary>
	private static readonly Dictionary<ulong, WeaponPickup> Claiming = new();
	private double _claimedAt = -1;
	private ulong _claimer;

	private Node3D _dropper;
	private Node3D _model;
	private Vector3 _velocity;
	private bool _landed;
	private double _age;
	private float _spin;

	public WeaponPickup()
	{
		CollisionLayer = 0;
		CollisionMask = 1;
		Monitorable = false;
	}

	/// <summary>Joga uma arma no mundo a partir de <paramref name="from"/>.</summary>
	public static WeaponPickup Spawn(Node3D dropper, WeaponDefinition def, int ammo, int reserve, Vector3 from, Vector3 velocity)
	{
		if (def == null || dropper == null || !dropper.IsInsideTree()) return null;

		Node parent = dropper.GetTree().CurrentScene;
		if (parent == null) return null;

		var pickup = new WeaponPickup
		{
			Definition = def,
			Ammo = ammo,
			Reserve = reserve,
			_dropper = dropper,
			_velocity = velocity,
			_spin = (float)GD.RandRange(-1.0, 1.0) >= 0 ? 1 : -1,
			Name = $"Arma_{def.Id}"
		};

		parent.AddChild(pickup);
		pickup.GlobalPosition = from;
		pickup.Rotation = new Vector3(0, dropper.GlobalRotation.Y, 0);
		pickup.NetId = Link?.Announce(pickup, dropper, from, velocity) ?? 0;

		Keep(pickup);
		return pickup;
	}

	/// <summary>Uma arma que outra máquina soltou (ou que já estava no chão
	/// quando esta entrou): cai de onde saiu, com a mesma força, e já voou
	/// <paramref name="age"/> segundos. <paramref name="dropper"/> é o boneco
	/// de quem soltou, se houver (a arma não bate nele ao sair).</summary>
	public static WeaponPickup SpawnRemote(SceneTree tree, long netId, WeaponDefinition def, int ammo, int reserve,
		Vector3 from, Vector3 velocity, double age, Node3D dropper)
	{
		if (def == null || netId == 0 || Find(netId) != null) return null;
		Node parent = tree?.CurrentScene;
		if (parent == null) return null;

		var pickup = new WeaponPickup
		{
			Definition = def,
			Ammo = ammo,
			Reserve = reserve,
			NetId = netId,
			_dropper = dropper,
			_velocity = velocity,
			_spin = 1,
			_age = Mathf.Max(0, age),
			Name = $"Arma_{def.Id}"
		};
		parent.AddChild(pickup);
		pickup.GlobalPosition = from;
		if (dropper != null) pickup.Rotation = new Vector3(0, dropper.GlobalRotation.Y, 0);

		// Já no chão há tempo: assenta de uma vez, em vez de voar de novo.
		if (age > 1.5) pickup.SettleNow();

		Keep(pickup);
		return pickup;
	}

	/// <summary>A arma com este número na partida (nula se não está no chão).</summary>
	public static WeaponPickup Find(long netId)
	{
		if (netId == 0) return null;
		foreach (WeaponPickup p in Live)
			if (IsInstanceValid(p) && p.NetId == netId && !p.IsQueuedForDeletion()) return p;
		return null;
	}

	/// <summary>As armas no chão agora (para os testes).</summary>
	public static IReadOnlyList<WeaponPickup> OnGround => Live;

	/// <summary>
	/// O servidor decidiu quem levou. Se foi <paramref name="taker"/> (um
	/// corpo desta máquina), a arma vai para ele com a munição dela; se foi
	/// outro (ou ninguém: sumiu no chão), ela só some daqui. Sem a arma no
	/// chão daqui (já sumiu por tempo), quem levou recebe do mesmo jeito.
	/// </summary>
	public static void Grant(long netId, Node3D taker, WeaponDefinition def, int ammo, int reserve)
	{
		WeaponPickup pickup = Find(netId);
		pickup?.Release();
		pickup?.QueueFree();
		taker?.GetNodeOrNull<Weapon>("Weapon")?.TryPickUp(def, ammo, reserve);
	}

	private static void Keep(WeaponPickup pickup)
	{
		Live.Add(pickup);
		while (Live.Count > MaxOnMap)
		{
			WeaponPickup oldest = Live[0];
			Live.RemoveAt(0);
			if (IsInstanceValid(oldest)) oldest.QueueFree();
		}
	}

	/// <summary>Põe no chão de uma vez, onde o voo terminaria.</summary>
	private void SettleNow()
	{
		for (int i = 0; i < 240 && !_landed; i++) Fly(1f / 60f);
	}

	private void Release()
	{
		if (_claimer != 0 && Claiming.TryGetValue(_claimer, out WeaponPickup waiting) && waiting == this) Claiming.Remove(_claimer);
		_claimer = 0;
	}

	public override void _Ready()
	{
		AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Reach } });

		_model = WeaponModel.Build(Definition.Id);
		AddChild(_model);
	}

	public override void _ExitTree()
	{
		Live.Remove(this);
		Release();
	}

	public override void _PhysicsProcess(double delta)
	{
		_age += delta;

		if (!_landed) Fly((float)delta);

		if (_age > Lifetime)
		{
			QueueFree();
			return;
		}

		// O último segundo encolhe: sumir de uma vez parece defeito.
		double left = Lifetime - _age;
		if (left < 1.0) _model.Scale = Vector3.One * Mathf.Max(0.05f, (float)left);

		TryGive();
	}

	private void Fly(float dt)
	{
		_velocity.Y -= FallAcceleration * dt;

		Vector3 from = GlobalPosition;
		Vector3 to = from + _velocity * dt;

		var query = PhysicsRayQueryParameters3D.Create(from, to + _velocity.Normalized() * 0.04f, 1);
		if (_dropper != null && IsInstanceValid(_dropper) && _dropper is CollisionObject3D body)
			query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };

		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count == 0)
		{
			GlobalPosition = to;
			_model.Rotation = new Vector3(_model.Rotation.X + 7f * dt * _spin, _model.Rotation.Y + 4f * dt, 0);
			return;
		}

		var point = (Vector3)hit["position"];
		var normal = (Vector3)hit["normal"];

		// Chão: deita de lado e para. Parede (ou alguém): quica fraco e
		// continua caindo.
		if (normal.Y > 0.55f)
		{
			_landed = true;
			GlobalPosition = point + normal * 0.03f;
			_model.Rotation = new Vector3(0, (float)GD.RandRange(-Mathf.Pi, Mathf.Pi), Mathf.Pi / 2f);
			return;
		}

		_velocity = _velocity.Bounce(normal) * 0.35f;
		GlobalPosition = point + normal * 0.06f;
	}

	/// <summary>Entrega a arma ao primeiro que passar e tiver espaço.
	/// Online, o corpo desta máquina pede ao servidor e espera a resposta
	/// (<see cref="Grant"/>); o boneco de outra máquina não pega nada aqui
	/// (a máquina dele é que pede).</summary>
	private void TryGive()
	{
		if (_age < 0.25) return;

		bool online = NetId != 0 && Link != null;
		if (online && _claimer != 0)
		{
			if (_age - _claimedAt < ClaimTimeout) return;
			Release();   // sem resposta: pede de novo
		}

		foreach (Node3D body in GetOverlappingBodies())
		{
			if (body is not CharacterBody3D) continue;
			if (body == _dropper && _age < DropperDelay) continue;

			var weapon = body.GetNodeOrNull<Weapon>("Weapon");
			if (weapon == null) continue;

			if (online)
			{
				if (!Link.Controls(body) || !weapon.CanTake(Definition)) continue;
				ulong who = body.GetInstanceId();
				if (Claiming.TryGetValue(who, out WeaponPickup other) && IsInstanceValid(other) && other != this) continue;
				Claiming[who] = this;
				_claimer = who;
				_claimedAt = _age;
				Link.Claim(this, body);
				return;
			}

			if (!weapon.TryPickUp(Definition, Ammo, Reserve)) continue;

			QueueFree();
			return;
		}
	}
}
