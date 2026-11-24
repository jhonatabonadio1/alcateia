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

	// ------------------------------------------------------------ os golpes do lobo

	/// <summary>
	/// O dono do boneco começou um golpe de lobo (21/11): o boneco toca
	/// aquele golpe, do começo, no tempo da tabela (<see cref="WolfMoves"/>),
	/// com o som no impacto — e não fere ninguém (o dano é da máquina do
	/// dono). O corpo anda pela rede; aqui só correm os relógios que a
	/// animação lê (em golpe, preso, no voo, no bote).
	///
	/// Antes o boneco só sabia "atacou", no instante do impacto, e tocava
	/// uma garrada: o salto do especial e o bote nunca apareciam para os
	/// outros, e a garrada saía atrasada.
	/// </summary>
	/// <param name="a">Salto: quanto do clipe ele pula (o agachar). Bote: em
	/// quanto tempo encosta.</param>
	/// <param name="b">Salto: o ritmo do voo.</param>
	public void RemoteWolfMove(WolfMove move, float a, float b)
	{
		if (!Puppet || _form == null || !_form.IsWolf || _health.IsDead) return;

		switch (move)
		{
			case WolfMove.Claw:
			case WolfMove.Punch:
				// O braço que o dono usou, não o que o boneco acharia.
				_nextPunch = move == WolfMove.Punch;
				_cooldown = 0;
				_attackQueue = 0;
				_diving = false;
				_flying = false;
				WolfAttack();
				break;

			case WolfMove.Pounce:
				RemotePounce(a, b);
				break;

			case WolfMove.Leap:
				RemoteLeap(a);
				break;
		}
	}

	/// <summary>O salto do especial no boneco: as mesmas contas do
	/// <see cref="Pounce"/> (agachar, voo, pancada e levantar), com o que o
	/// dono mandou no lugar da carga e da mira.</summary>
	private void RemotePounce(float skip, float rate)
	{
		var (crouch, airborne, _) = WolfMoves.StepOf(WolfMove.Pounce);
		skip = Mathf.Clamp(skip, 0f, crouch);
		rate = rate > 0.01f ? Mathf.Clamp(rate, 0.2f, 6f) : 1f;

		float wait = Mathf.Max(crouch - skip, 0f);
		float air = airborne / rate;
		float recover = Mathf.Max(PounceRecoverRate, 0.1f);
		float landed = crouch + airborne;
		float impactAt = wait + air + Mathf.Max(WolfMoves.ImpactDelay(WolfMove.Pounce) - landed, 0f) / recover;
		float total = wait + air + Mathf.Max(WolfMoves.Duration(WolfMove.Pounce) - landed, 0f) / recover;

		_charging = false;
		_diving = false;
		_flying = false;
		_pounceLanded = false;
		_comboIndex = 0;
		_attackQueue = 0;
		_rootLeft = total;
		PounceSkip = skip;
		PounceFlightRate = rate;

		// O clipe começa já no impulso (o WolfVisual lê o PounceSkip agora).
		EmitSignal(SignalName.WolfMoveStarted, (int)WolfMove.Pounce);
		ulong id = StartMove(WolfMove.Pounce, CooldownHeavy, HeavyCooldown,
			() => MarkRecovery(_rootLeft), Mathf.Max(skip, 0.001f), total, impactAt);

		// O voo: só o relógio (o corpo voa pela rede).
		After(wait, id, () =>
		{
			_flyStart = _flyEnd = _body.GlobalPosition;
			_flyTotal = Mathf.Max(air, 0.05f);
			_flyTime = 0;
			_flying = true;
		});
	}

	/// <summary>O bote no boneco: o mergulho na mira que chegou com o
	/// ataque, batendo no tempo que o dono previu (o boneco não sente o
	/// chão: quem o move é a rede).</summary>
	private void RemoteLeap(float eta)
	{
		_comboIndex = 0;
		_charging = false;
		_flying = false;

		Vector3 aim = -_head.GlobalTransform.Basis.Z;
		aim.Y = Mathf.Min(aim.Y, -DiveMinDrop);
		aim = aim.Normalized();
		DiveVelocity = aim * DiveSpeed;
		DiveEta = eta > 0.01f ? Mathf.Min(eta, DiveMaxTime) : TimeToContact(DiveVelocity);

		_diving = true;
		_diveLeft = DiveEta;
		_diveElapsed = 0;
		_strikeId++;
		_strikeLeft = 0;
		_cooldown = DiveMaxTime + WolfMoves.LandDuration;

		EmitSignal(SignalName.WolfMoveStarted, (int)WolfMove.Leap);
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
