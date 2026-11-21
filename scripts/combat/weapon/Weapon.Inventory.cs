using Godot;
using System.Collections.Generic;

/// <summary>
/// O que está na mão: equipar o conjunto, munição, recarga, trocar,
/// soltar (G) e pegar do chão. A munição acaba: pente e reserva, a
/// reserva enche ao renascer e cresce com a de uma arma igual caída.
/// </summary>
public partial class Weapon
{
	/// <summary>Velocidade do arremesso ao soltar com B, para frente.</summary>
	[ExportGroup("Soltar e pegar")]
	[Export] public float DropThrow = 5.5f;

	/// <summary>Quanto sobe o arremesso.</summary>
	[Export] public float DropLift = 1.8f;

	/// <summary>Armas em uso. Trocar reescreve as armas sem recriar nada.</summary>
	public WeaponDefinition PrimaryDef { get; private set; }
	public WeaponDefinition SecondaryDef { get; private set; }
	public int PrimaryMag => PrimaryDef?.Magazine ?? 0;
	public int SecondaryMag => SecondaryDef?.Magazine ?? 0;
	public int PrimaryAmmo { get; private set; }
	public int SecondaryAmmo { get; private set; }

	/// <summary>Balas de reserva de cada arma, fora o pente.</summary>
	public int PrimaryReserve { get; private set; }
	public int SecondaryReserve { get; private set; }

	/// <summary>A reserva da arma na mão.</summary>
	public int Reserve => HumanSlot == Slot.Primary ? PrimaryReserve : HumanSlot == Slot.Secondary ? SecondaryReserve : 0;

	/// <summary>A arma na mão tem bala em algum lugar (pente ou reserva).</summary>
	public bool HasAmmo => AmmoInMag > 0 || Reserve > 0 || InfiniteAmmo;

	/// <summary>A arma deste espaço existe e tem bala (pente ou reserva).</summary>
	public bool HasAmmoIn(Slot slot) => slot switch
	{
		Slot.Primary => PrimaryDef != null && (PrimaryAmmo > 0 || PrimaryReserve > 0 || InfiniteAmmo),
		Slot.Secondary => SecondaryDef != null && (SecondaryAmmo > 0 || SecondaryReserve > 0 || InfiniteAmmo),
		_ => false
	};
	public bool IsReloading => _reloadTimer > 0;
	private double _reloadTimer;
	private double _reloadTotal = 1;
	private bool _shellReload;

	/// <summary>Quanto da recarga em curso já passou, de 0 a 1. Na
	/// escopeta é o ciclo do cartucho atual. A animação da recarga segue
	/// este relógio, então recarga vazia mais longa dura mais na tela.</summary>
	public float ReloadProgress =>
		IsReloading ? Mathf.Clamp(1f - (float)(_reloadTimer / Mathf.Max(_reloadTotal, 0.01)), 0f, 1f) : 0f;

	/// <summary>Duração total da recarga em curso, em segundos.</summary>
	public float ReloadDuration => (float)_reloadTotal;
	public bool HasPrimary => PrimaryDef != null;
	public bool HasSecondary => SecondaryDef != null;
	public int AmmoInMag => HumanSlot == Slot.Primary ? PrimaryAmmo : SecondaryAmmo;
	public int MagSize => HumanSlot == Slot.Primary ? PrimaryMag : SecondaryMag;

	/// <summary>Recarrega o carregador em uso. Só faz sentido com arma
	/// de fogo na mão e com o carregador incompleto.</summary>
	public bool TryReload()
	{
		if (_form.IsWolf || IsReloading) return false;

		WeaponDefinition held = Held;
		if (held == null || AmmoInMag >= MagSize || (Reserve <= 0 && !InfiniteAmmo)) return false;

		// Vazia demora mais que tática: é o preço de esvaziar o pente.
		_shellReload = held.ShellByShell;
		_reloadTimer = held.ShellByShell
			? held.ShellEnter + held.ShellTime
			: AmmoInMag == 0 ? held.ReloadEmpty : held.ReloadTactical;
		_reloadTotal = _reloadTimer;

		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, true);
		return true;
	}

	private void FinishReload()
	{
		if (_shellReload)
		{
			// Um cartucho por ciclo, e o ciclo seguinte começa sozinho
			// até encher. O tiro interrompe — é a decisão sob pressão que
			// define a escopeta: mais um cartucho ou atirar com o que tem.
			AddRound();

			if (AmmoInMag < MagSize && Held != null && (Reserve > 0 || InfiniteAmmo))
			{
				_reloadTimer = Held.ShellTime;
				_reloadTotal = _reloadTimer;
				EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, true);
				return;
			}
		}
		else if (HumanSlot == Slot.Primary)
		{
			int moved = InfiniteAmmo ? PrimaryMag - PrimaryAmmo : Mathf.Min(PrimaryMag - PrimaryAmmo, PrimaryReserve);
			PrimaryAmmo += moved;
			if (!InfiniteAmmo) PrimaryReserve -= moved;
		}
		else
		{
			int moved = InfiniteAmmo ? SecondaryMag - SecondaryAmmo : Mathf.Min(SecondaryMag - SecondaryAmmo, SecondaryReserve);
			SecondaryAmmo += moved;
			if (!InfiniteAmmo) SecondaryReserve -= moved;
		}

		_reloadTimer = 0;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	private void AddRound()
	{
		if (HumanSlot == Slot.Primary && (PrimaryReserve > 0 || InfiniteAmmo) && PrimaryAmmo < PrimaryMag)
		{
			PrimaryAmmo++;
			if (!InfiniteAmmo) PrimaryReserve--;
		}
		else if (HumanSlot == Slot.Secondary && (SecondaryReserve > 0 || InfiniteAmmo) && SecondaryAmmo < SecondaryMag)
		{
			SecondaryAmmo++;
			if (!InfiniteAmmo) SecondaryReserve--;
		}
	}

	/// <summary>Pente e reserva cheios nas duas armas (ao renascer).</summary>
	public void RefillAmmo()
	{
		PrimaryAmmo = PrimaryMag;
		SecondaryAmmo = SecondaryMag;
		PrimaryReserve = PrimaryDef?.Reserve ?? 0;
		SecondaryReserve = SecondaryDef?.Reserve ?? 0;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	private void CancelReload()
	{
		_reloadTimer = 0;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}

	/// <summary>Equipa primária, secundária e granadas. Nulo cai no
	/// conjunto padrão, que é o que bot e jogador sem escolha usam.</summary>
	public void Equip(WeaponDefinition primary, WeaponDefinition secondary, int grenades)
	{
		PrimaryDef = primary ?? Arsenal.Get(Arsenal.DefaultPrimary);
		SecondaryDef = secondary ?? Arsenal.Get(Arsenal.DefaultSecondary);

		PrimaryAmmo = PrimaryMag;
		SecondaryAmmo = SecondaryMag;
		PrimaryReserve = PrimaryDef?.Reserve ?? 0;
		SecondaryReserve = SecondaryDef?.Reserve ?? 0;
		_reloadTimer = 0;
		_bloom = 0;
		_streak = 0;

		MaxGrenades = grenades;
		Grenades = grenades;

		HumanSlot = Slot.Primary;

		EmitSignal(SignalName.GrenadesChanged, Grenades);
		EmitSignal(SignalName.SlotChanged, (int)Slot.Primary);
		EmitSignal(SignalName.AmmoChanged, PrimaryAmmo, PrimaryMag, false);
	}

	/// <summary>A faca na mão: a comum ou uma versão dela (a do conjunto).</summary>
	public string MeleeId { get; private set; } = Arsenal.KnifeId;

	/// <summary>A granada do conjunto: a comum ou uma versão dela (só a
	/// pintura muda — <see cref="Arsenal.Grenades"/>). É a que aparece na
	/// mão e a que voa.</summary>
	public string GrenadeModelId { get; private set; } = Arsenal.GrenadeId;

	public void Equip(LoadoutSet set)
	{
		MeleeId = set != null && Arsenal.IsKnife(set.Melee) ? set.Melee : Arsenal.KnifeId;
		GrenadeModelId = Arsenal.GrenadeId;
		if (set?.Utilities != null)
		{
			foreach (string utility in set.Utilities)
			{
				if (!Arsenal.IsGrenade(utility)) continue;
				GrenadeModelId = utility;
				break;
			}
		}
		if (set == null)
		{
			Equip(null, null, 0);
			return;
		}

		Equip(Arsenal.Get(set.Primary), Arsenal.Get(set.Secondary), set.GrenadeCount());
	}

	private bool Usable(Slot slot) => slot switch
	{
		Slot.Primary => PrimaryDef != null,
		Slot.Secondary => SecondaryDef != null,
		Slot.Grenade => Grenades > 0,
		Slot.Knife => true,
		_ => false
	};

	/// <summary>A melhor arma que sobrou: primária, secundária, faca.</summary>
	private Slot BestSlot() =>
		PrimaryDef != null ? Slot.Primary : SecondaryDef != null ? Slot.Secondary : Slot.Knife;

	/// <summary>
	/// Solta a arma da mão (G), jogando-a para frente, e passa para a
	/// próxima: da primária vai à secundária, da secundária à faca.
	/// Faca e granada não se soltam. A arma cai no chão com o pente que
	/// tinha, e quem passar por cima com o espaço vazio fica com ela.
	/// </summary>
	public bool TryDrop()
	{
		if (_form.IsWolf || _health.IsDead) return false;
		if (HumanSlot != Slot.Primary && HumanSlot != Slot.Secondary) return false;

		WeaponDefinition def = HumanSlot == Slot.Primary ? PrimaryDef : SecondaryDef;
		if (def == null) return false;

		int ammo = AmmoInMag, reserve = Reserve;
		if (IsReloading) CancelReload();

		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		Vector3 from = _camera.GlobalPosition + forward * 0.45f + Vector3.Down * 0.25f;
		Vector3 velocity = _body.Velocity * 0.6f + forward * DropThrow + Vector3.Up * DropLift;
		WeaponPickup.Spawn(_body, def, ammo, reserve, from, velocity);

		Remove(HumanSlot);
		EmitSignal(SignalName.Dropped, def.Id);

		HumanSlot = BestSlot();
		_cooldown = 0.25f;
		AnnounceHeld();
		return true;
	}

	/// <summary>Cabe esta arma? Só se o espaço dela estiver vazio — como
	/// no CS: para trocar, solte a sua primeiro. Ou é igual à sua e a
	/// reserva não está cheia: aí vale a munição dela.</summary>
	public bool CanTake(WeaponDefinition def)
	{
		if (def == null || _form.IsWolf || _health.IsDead) return false;
		bool secondary = Arsenal.IsSecondary(def.Id);
		WeaponDefinition mine = secondary ? SecondaryDef : PrimaryDef;
		if (mine == null) return true;
		return mine.Id == def.Id && (secondary ? SecondaryReserve : PrimaryReserve) < mine.Reserve;
	}

	/// <summary>
	/// Pega uma arma do chão para o espaço vazio. Vai para a mão quando
	/// for a melhor que você tem — quem soltou a primária para pegar
	/// outra quer atirar com ela, não continuar de pistola.
	/// </summary>
	public bool TryPickUp(WeaponDefinition def, int ammo, int reserve)
	{
		if (!CanTake(def)) return false;

		bool secondary = Arsenal.IsSecondary(def.Id);

		// Igual à sua: só a munição dela (pente e reserva) entra na reserva.
		WeaponDefinition mine = secondary ? SecondaryDef : PrimaryDef;
		if (mine != null)
		{
			int gain = Mathf.Max(ammo, 0) + Mathf.Max(reserve, 0);
			if (secondary) SecondaryReserve = Mathf.Min(SecondaryReserve + gain, mine.Reserve);
			else PrimaryReserve = Mathf.Min(PrimaryReserve + gain, mine.Reserve);
			EmitSignal(SignalName.AmmoTaken, def.Id);
			EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, IsReloading);
			return true;
		}

		if (secondary)
		{
			SecondaryDef = def;
			SecondaryAmmo = Mathf.Clamp(ammo, 0, SecondaryMag);
			SecondaryReserve = Mathf.Clamp(reserve, 0, def.Reserve);
		}
		else
		{
			PrimaryDef = def;
			PrimaryAmmo = Mathf.Clamp(ammo, 0, PrimaryMag);
			PrimaryReserve = Mathf.Clamp(reserve, 0, def.Reserve);
		}

		Slot target = secondary ? Slot.Secondary : Slot.Primary;
		bool take = secondary ? HumanSlot == Slot.Knife : HumanSlot != Slot.Primary;

		EmitSignal(SignalName.PickedUp, def.Id);

		if (take && HumanSlot != Slot.Grenade)
		{
			if (IsReloading) CancelReload();
			HumanSlot = target;
			_cooldown = 0.25f;
		}

		AnnounceHeld();
		return true;
	}

	private void Remove(Slot slot)
	{
		if (slot == Slot.Primary)
		{
			PrimaryDef = null;
			PrimaryAmmo = 0;
			PrimaryReserve = 0;
		}
		else if (slot == Slot.Secondary)
		{
			SecondaryDef = null;
			SecondaryAmmo = 0;
			SecondaryReserve = 0;
		}
	}
}
