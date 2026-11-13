using Godot;
using System.Collections.Generic;

/// <summary>
/// O que está na mão: equipar o conjunto, munição, recarga, trocar,
/// soltar (G) e pegar do chão. A reserva é infinita — o custo de atirar
/// é o tempo de recarga, não o estoque.
/// </summary>
public partial class Weapon
{
	[ExportGroup("Soltar e pegar")]
	/// <summary>Velocidade do arremesso ao soltar com B, para frente.</summary>
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
		if (held == null || AmmoInMag >= MagSize) return false;

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

			if (AmmoInMag < MagSize && Held != null)
			{
				_reloadTimer = Held.ShellTime;
				_reloadTotal = _reloadTimer;
				EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, true);
				return;
			}
		}
		else if (HumanSlot == Slot.Primary) PrimaryAmmo = PrimaryMag;
		else SecondaryAmmo = SecondaryMag;

		_reloadTimer = 0;
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
	}
	private void AddRound()
	{
		if (HumanSlot == Slot.Primary) PrimaryAmmo = Mathf.Min(PrimaryAmmo + 1, PrimaryMag);
		else SecondaryAmmo = Mathf.Min(SecondaryAmmo + 1, SecondaryMag);
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
	public void Equip(LoadoutSet set)
	{
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

		int ammo = AmmoInMag;
		if (IsReloading) CancelReload();

		Vector3 forward = -_camera.GlobalTransform.Basis.Z;
		Vector3 from = _camera.GlobalPosition + forward * 0.45f + Vector3.Down * 0.25f;
		Vector3 velocity = _body.Velocity * 0.6f + forward * DropThrow + Vector3.Up * DropLift;
		WeaponPickup.Spawn(_body, def, ammo, from, velocity);

		Remove(HumanSlot);
		EmitSignal(SignalName.Dropped, def.Id);

		HumanSlot = BestSlot();
		_cooldown = 0.25f;
		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
		return true;
	}
	/// <summary>Cabe esta arma? Só se o espaço dela estiver vazio — como
	/// no CS: para trocar, solte a sua primeiro.</summary>
	public bool CanTake(WeaponDefinition def)
	{
		if (def == null || _form.IsWolf || _health.IsDead) return false;
		return Arsenal.IsSecondary(def.Id) ? SecondaryDef == null : PrimaryDef == null;
	}
	/// <summary>
	/// Pega uma arma do chão para o espaço vazio. Vai para a mão quando
	/// for a melhor que você tem — quem soltou a primária para pegar
	/// outra quer atirar com ela, não continuar de pistola.
	/// </summary>
	public bool TryPickUp(WeaponDefinition def, int ammo)
	{
		if (!CanTake(def)) return false;

		bool secondary = Arsenal.IsSecondary(def.Id);
		if (secondary)
		{
			SecondaryDef = def;
			SecondaryAmmo = Mathf.Clamp(ammo, 0, SecondaryMag);
		}
		else
		{
			PrimaryDef = def;
			PrimaryAmmo = Mathf.Clamp(ammo, 0, PrimaryMag);
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

		EmitSignal(SignalName.SlotChanged, (int)HumanSlot);
		EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
		return true;
	}
	private void Remove(Slot slot)
	{
		if (slot == Slot.Primary)
		{
			PrimaryDef = null;
			PrimaryAmmo = 0;
		}
		else if (slot == Slot.Secondary)
		{
			SecondaryDef = null;
			SecondaryAmmo = 0;
		}
	}
}
