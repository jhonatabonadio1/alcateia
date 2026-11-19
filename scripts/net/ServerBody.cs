using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O corpo de um jogador no servidor de partida (modo autoritativo,
/// decisão 0011): o <see cref="Player"/> de sempre, movido pelos comandos
/// que chegam (<see cref="Commands"/>), um por passo, com o mesmo código
/// que o jogo do jogador usou para prever.
///
/// O que ainda vem do jogo nesta leva — e por isso o servidor só copia:
/// a forma, a arma na mão, a vida (<see cref="ApplyStatus"/>), se está
/// morto e onde renasceu (no próprio comando). A leva seguinte traz a vida
/// e o tiro para cá.
/// </summary>
public sealed class ServerBody
{
	public readonly long Account;
	public readonly byte Index;
	public readonly Player Body;
	public readonly CommandBuffer Commands = new();
	public readonly PoseHistory History = new();

	private readonly Health _health;
	private readonly FormController _form;
	private readonly Weapon _weapon;

	private string _primary = "", _secondary = "";
	private float _hp = 100, _maxHp = 100, _shield, _maxShield;

	/// <summary>A vida do jogador em que o corpo está (dá a volta em 256).</summary>
	public byte Epoch { get; private set; }

	/// <summary>Já sabe onde o jogador nasceu e está no mundo.</summary>
	public bool Placed { get; private set; }

	public int Simulated { get; private set; }

	public ServerBody(long account, byte index, Player body)
	{
		Account = account;
		Index = index;
		Body = body;
		_health = body.GetNode<Health>("Health");
		_form = body.GetNode<FormController>("FormController");
		_weapon = body.GetNode<Weapon>("Weapon");
		Hide();
	}

	/// <summary>Um passo do servidor: consome o comando deste passo (ou
	/// dois, se o jogo está à frente) e guarda onde o corpo ficou.</summary>
	public void Step(uint tick, double delta)
	{
		int wanted = Commands.WantedThisTick;
		for (int i = 0; i < wanted; i++)
		{
			if (!Commands.Next(out InputCommand command, out _)) break;
			Apply(command, delta);
		}

		if (Placed)
			History.Record(tick, Body.GlobalPosition.ToNumerics(), Body.Rotation.Y, Body.HeadPitch, Body.IsCrouching ? Body.CrouchHeight : Body.StandHeight);
	}

	private void Apply(in InputCommand c, double delta)
	{
		// Vida nova (ou a primeira): o corpo vai para onde o jogo diz que
		// nasceu. Sem a posição ainda, espera — comando de outra vida não
		// move o corpo desta.
		if (!Placed || c.Epoch != Epoch)
		{
			if (!c.HasSpawn) return;
			Epoch = c.Epoch;
			if (_health.IsDead) _health.Mirror(_maxHp, _maxHp, _maxShield, _maxShield, false);
			Show();
			Body.PlaceDriven(c.Spawn.ToGodot());
		}

		if (c.Dead && !_health.IsDead) _health.Mirror(0, _maxHp, 0, _maxShield, true);

		Form form = System.Enum.IsDefined(typeof(Form), (int)c.Form) ? (Form)c.Form : Form.Human;
		_form.RemoteSet(form);
		if (!_form.IsWolf) _weapon.RemoteHold(_primary, _secondary, (Slot)c.HeldSlot);

		Body.Simulate(NetCommands.ToIntent(c), Quant.Yaw(c.Yaw), Quant.Pitch(c.Pitch), delta, c.Frozen);
		Simulated++;
	}

	/// <summary>Vida, escudo e armas, como o jogo conta (Status).</summary>
	public void ApplyStatus(float hp, float maxHp, float shield, float maxShield, string primary, string secondary)
	{
		_hp = hp;
		_maxHp = maxHp;
		_shield = shield;
		_maxShield = maxShield;
		_primary = primary ?? "";
		_secondary = secondary ?? "";
		if (Placed && !_health.IsDead) _health.Mirror(hp, maxHp, shield, maxShield, false);
	}

	/// <summary>A classe de lobo e as melhorias (Loadout): mudam a
	/// velocidade e o pulo do lobo, então o servidor precisa delas.</summary>
	public void ApplyLoadout(string breed, IEnumerable<string> tattoos)
	{
		var valid = tattoos.Where(id => Catalog.Get(id)?.Kind == ItemKind.Tattoo).Distinct().Take(Profile.MaxTattoos);
		WolfBreed built = Profile.BuildBreed(breed, valid);
		if (built != null) _form.Breed = built;
	}

	/// <summary>O corpo no estado do mundo.</summary>
	public EntityState State()
	{
		var flags = EntityFlags.None;
		if (Placed) flags |= EntityFlags.Present;
		if (Body.IsOnFloor()) flags |= EntityFlags.Grounded;
		if (Body.IsCrouching) flags |= EntityFlags.Crouching;
		if (_health.IsDead) flags |= EntityFlags.Dead;
		if (Body.IsWallRunning) flags |= EntityFlags.WallRunning;
		if (Body.IsSprinting) flags |= EntityFlags.Sprinting;
		if (_weapon.IsAiming) flags |= EntityFlags.Aiming;

		return new EntityState
		{
			Index = Index,
			Flags = flags,
			Form = (byte)_form.Current,
			Slot = (byte)_weapon.HumanSlot,
			Epoch = Epoch,
			Position = Body.GlobalPosition.ToNumerics(),
			Velocity = Body.Velocity.ToNumerics(),
			Yaw = Quant.Yaw(Body.Rotation.Y),
			Pitch = Quant.Pitch(Body.HeadPitch)
		};
	}

	private uint _layer, _mask;

	/// <summary>Fora do mundo até saber onde nasceu: sem colisão.</summary>
	private void Hide()
	{
		_layer = Body.CollisionLayer;
		_mask = Body.CollisionMask;
		Body.CollisionLayer = 0;
		Placed = false;
	}

	private void Show()
	{
		Body.CollisionLayer = _layer;
		Body.CollisionMask = _mask;
		Placed = true;
	}
}
