using Godot;

/// <summary>
/// A arma na mão do boneco de outro jogador (partida online).
///
/// O boneco encena o que o dono fez — o tiro, a facada, o golpe do lobo,
/// o arremesso — com o mesmo código de sempre, para ter o mesmo som, o
/// mesmo clarão e a mesma animação. Só não acerta ninguém: o dano sai da
/// máquina de quem atacou (<see cref="Damage"/> ignora o boneco).
/// </summary>
public partial class Weapon
{
	/// <summary>Esta arma é de um boneco: encena, não fere.</summary>
	public bool Puppet { get; set; }

	/// <summary>Põe na mão do boneco o que o dono tem na dele.</summary>
	public void RemoteHold(string primary, string secondary, Slot slot)
	{
		if (PrimaryDef?.Id != primary || SecondaryDef?.Id != secondary)
		{
			PrimaryDef = Arsenal.Get(primary) ?? PrimaryDef;
			SecondaryDef = Arsenal.Get(secondary) ?? SecondaryDef;
			PrimaryAmmo = PrimaryMag;
			SecondaryAmmo = SecondaryMag;
		}
		else if (slot == HumanSlot) return;

		if (slot is Slot.Primary or Slot.Secondary or Slot.Knife or Slot.Grenade) HumanSlot = slot;
		AnnounceHeld();
	}

	/// <summary>O dono atacou: o boneco repete, sem esperar recarga nem
	/// faltar munição (isso é assunto do dono).</summary>
	public void RemoteAttack(Slot slot)
	{
		if (!_form.IsWolf && slot != HumanSlot && slot != Slot.Wolf)
		{
			HumanSlot = slot;
			AnnounceHeld();
		}

		_cooldown = 0;
		_reloadTimer = 0;
		if (PrimaryAmmo <= 0) PrimaryAmmo = PrimaryMag;
		if (SecondaryAmmo <= 0) SecondaryAmmo = SecondaryMag;
		if (Grenades <= 0) Grenades = 1;

		TryAttack();
	}

	// ------------------------------------------------------------ o boneco online

	private bool _mirrorReloading;

	/// <summary>
	/// O boneco de outro jogador, online: o pente, a recarga, a carga do
	/// especial e as granadas que o dono informa (o status, a cada 0,1 s).
	/// É o que o HUD dele mostra a quem o assiste no espectador.
	/// </summary>
	public void MirrorGear(int ammo, bool reloading, float charge, int grenades)
	{
		if (!Puppet) return;
		if (ammo >= 0 && (ammo != AmmoInMag || reloading != _mirrorReloading))
		{
			if (HumanSlot == Slot.Secondary) SecondaryAmmo = ammo;
			else if (HumanSlot == Slot.Primary) PrimaryAmmo = ammo;
			_mirrorReloading = reloading;
			EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, reloading);
		}
		if (!Mathf.IsEqualApprox(charge, SpecialCharge)) SetSpecialCharge(charge);
		if (grenades != Grenades)
		{
			Grenades = grenades;
			EmitSignal(SignalName.GrenadesChanged, Grenades);
		}
	}
}
