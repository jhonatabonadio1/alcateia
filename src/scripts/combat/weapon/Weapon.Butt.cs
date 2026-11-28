using Godot;
using System.Collections.Generic;

/// <summary>
/// O corpo a corpo da tecla V (<see cref="TryMelee"/>) e a coronhada.
///
/// A coronhada (V; pedido do Jhon, 18/11: "crie a animação de coronhada,
/// ao apertar V, bem rápida"): com uma arma de fogo na mão, um golpe curto
/// com a arma no que está colado na cara. É a saída para o lobo que
/// chegou perto demais ou para o pente vazio — bate rápido, tira pouco e
/// não gasta munição.
///
/// O golpe sai em <see cref="ButtHitDelay"/> (o tempo de a arma ir para a
/// frente) e a arma fica ocupada por <see cref="ButtCooldown"/>: não
/// atira nem mira nesse tempo. Corta a recarga. Sem tiro na cabeça: é
/// pancada, não mira.
/// </summary>
public partial class Weapon
{
	[ExportGroup("Coronhada")]
	[Export] public float ButtDamage = 10f;
	[Export] public float ButtRange = 1.9f;
	[Export] public float ButtCooldown = 0.4f;
	[Export] public float ButtHitDelay = 0.075f;

	/// <summary>
	/// O golpe é um leque, não um raio (Jhon, 28/11: "a coronhada não está
	/// pegando"): um raio só, do olho para a frente, passava por cima de
	/// quem estava agachado (a cabeça dele fica ~40 cm abaixo do olho de quem
	/// bate) e errava quem estava um palmo de lado a 1,6 m. Como o corpo a
	/// corpo dos FPS (o CS varre um volume), o golpe procura em
	/// <see cref="ButtFanYaw"/> graus para cada lado e até
	/// <see cref="ButtFanDown"/> abaixo da mira, em <see cref="ButtFanSteps"/>
	/// passos de cada lado e para baixo; leva o inimigo mais perto que algum
	/// raio encostar. Parede no caminho de um raio para ele.
	/// </summary>
	[Export] public float ButtFanYaw = 20f;
	[Export] public float ButtFanDown = 30f;
	[Export] public int ButtFanSteps = 2;

	/// <summary>A coronhada começou (a arma na mão anima, o som sai).</summary>
	[Signal] public delegate void ButtedEventHandler();

	private double _buttLeft, _buttHitIn = -1;

	/// <summary>No meio da coronhada: a arma está ocupada.</summary>
	public bool IsButting => _buttLeft > 0;

	/// <summary>
	/// V com o que estiver na mão (Jhon, 26/11: "da faca deve dar uma facada
	/// normal, golpe normal. granada deve lançar a granada"): com arma de
	/// fogo, a coronhada; com a faca, o golpe do clique; com a granada, o
	/// arremesso direto, sem segurar armada (e a que já estava armada sai).
	/// </summary>
	public bool TryMelee()
	{
		if (_form == null || _form.IsWolf || _health.IsDead) return false;
		switch (HumanSlot)
		{
			case Slot.Knife:
				TryAttack();
				return true;
			case Slot.Grenade:
				if (_grenadeReady) ReleaseGrenade();
				else ThrowGrenade();
				return true;
			default:
				return TryButt();
		}
	}

	/// <summary>A coronhada, se há arma de fogo na mão e ela está livre.</summary>
	public bool TryButt()
	{
		if (_form == null || _form.IsWolf || !_form.CanAttack || _health.IsDead) return false;
		if (Held == null || _cooldown > 0 || _buttLeft > 0 || !Match.IsLive) return false;

		if (IsReloading) CancelReload();
		_cooldown = ButtCooldown;
		_buttLeft = ButtCooldown;
		_buttHitIn = ButtHitDelay;
		EmitSignal(SignalName.Butted);
		ButtSound();
		return true;
	}

	/// <summary>O dono do boneco deu a coronhada: o boneco repete o gesto e
	/// o som (o dano é da máquina do dono; aqui não fere).</summary>
	public void RemoteButt(Slot slot)
	{
		if (_form.IsWolf || _health.IsDead) return;
		if (slot is Slot.Primary or Slot.Secondary && HumanSlot != slot)
		{
			HumanSlot = slot;
			AnnounceHeld();
		}
		_buttLeft = ButtCooldown;
		_buttHitIn = -1;
		EmitSignal(SignalName.Butted);
		ButtSound();
	}

	/// <summary>O som da coronhada, no tom da voz de quem bate (a Eve mais aguda).</summary>
	private void ButtSound()
	{
		float pitch = _body.GetNodeOrNull<CharacterAnimator>("Human")?.VoicePitch ?? 1f;
		Sfx.At("coronhada", _body.GlobalPosition, 6f, -2f, 16f, pitch);
	}

	private void TickButt(double delta)
	{
		if (_buttLeft > 0) _buttLeft -= delta;
		if (_buttHitIn < 0) return;

		_buttHitIn -= delta;
		if (_buttHitIn > 0) return;
		_buttHitIn = -1;
		if (_health.IsDead || _form.IsWolf) return;

		Vector3 from = _head.GlobalPosition;
		Health hit = ButtFan(from);
		if (hit != null) Damage(hit, ButtDamage, from, false);
	}

	/// <summary>O inimigo mais perto que o leque da coronhada encosta (ou null).</summary>
	private Health ButtFan(Vector3 from)
	{
		Basis aim = _head.GlobalTransform.Basis;
		List<Target> targets = Targets();
		int steps = Mathf.Max(ButtFanSteps, 0);
		Health best = null;
		float bestAt = float.MaxValue;
		for (int down = 0; down <= steps; down++)
			for (int side = -steps; side <= steps; side++)
			{
				float pitch = steps == 0 ? 0f : -ButtFanDown * down / steps;
				float yaw = steps == 0 ? 0f : ButtFanYaw * side / steps;
				Vector3 forward = (-aim.Z).Rotated(aim.Y.Normalized(), Mathf.DegToRad(yaw));
				forward = forward.Rotated(aim.X.Normalized(), Mathf.DegToRad(pitch)).Normalized();
				Health hit = Trace(from, from + forward * ButtRange, targets, false, out Vector3 impact, out _, out _);
				if (hit == null) continue;
				float at = from.DistanceTo(impact);
				if (at >= bestAt) continue;
				best = hit;
				bestAt = at;
			}
		return best;
	}
}
