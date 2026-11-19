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

	// ------------------------------------------------------------ o replay

	/// <summary>
	/// A cópia do replay (<see cref="KillCam"/>) espelha o que a gravação
	/// diz: a munição no pente, a carga do especial e as granadas — o que o
	/// HUD mostra. Só avisa quem escuta quando muda.
	/// </summary>
	public void ReplayMirror(int ammo, float charge, int grenades)
	{
		if (!Puppet) return;
		if (!IsReloading && ammo != AmmoInMag && ammo >= 0)
		{
			if (HumanSlot == Slot.Secondary) SecondaryAmmo = ammo;
			else PrimaryAmmo = ammo;
			EmitSignal(SignalName.AmmoChanged, AmmoInMag, MagSize, false);
		}
		if (!Mathf.IsEqualApprox(charge, SpecialCharge)) SetSpecialCharge(charge);
		if (grenades != Grenades)
		{
			Grenades = grenades;
			EmitSignal(SignalName.GrenadesChanged, Grenades);
		}
	}

	/// <summary>A cópia do replay repete um acontecimento gravado: o golpe
	/// do lobo, a recarga de habilidade, o acerto, a coronhada, a recarga.</summary>
	public void ReplayEvent(ReplayTape.Kind kind, int a, float b)
	{
		if (!Puppet) return;
		switch (kind)
		{
			case ReplayTape.Kind.Fired: RemoteAttack((Slot)a); break;
			case ReplayTape.Kind.Thrown: RemoteAttack(Slot.Grenade); break;
			case ReplayTape.Kind.WolfMove: EmitSignal(SignalName.WolfMoveStarted, a); break;
			case ReplayTape.Kind.Cooldown: EmitSignal(SignalName.AttackCooldown, a, b); break;
			case ReplayTape.Kind.Hit: EmitSignal(SignalName.HitConfirmed, a != 0); break;
			case ReplayTape.Kind.Butt:
				_cooldown = 0;
				TryButt();
				break;
			case ReplayTape.Kind.Reload:
				if (AmmoInMag >= MagSize)
				{
					if (HumanSlot == Slot.Secondary) SecondaryAmmo = Mathf.Max(0, MagSize - 1);
					else PrimaryAmmo = Mathf.Max(0, MagSize - 1);
				}
				TryReload();
				break;
		}
	}
}
