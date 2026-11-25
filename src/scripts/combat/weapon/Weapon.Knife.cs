using Godot;
using System.Collections.Generic;

/// <summary>
/// A faca: saída silenciosa e último recurso. Não faz ruído para os bots.
///
/// Dois golpes (Jhon, 24/11): o clique esquerdo, rápido, e a
/// <b>estocada</b> no botão direito — mais lenta e mais forte, e mata
/// quem é pego pelas costas (<see cref="CombatRules.KnifeStab"/>). A
/// estocada é o começo do "inspecionar" do conjunto (a mão sobe, crava
/// para a frente e volta); a tecla F, que tocava o inspecionar, saiu.
/// </summary>
public partial class Weapon
{
	[ExportGroup("Faca")]
	[Export] public float KnifeDamage = 85f;

	/// <summary>Velocidade com a faca na mão, sobre a do humano. 3,36 m/s
	/// desde a v0.27.1, 12% acima dos 3,0 de antes (decisão do Jhon): como o
	/// humano já anda 8% a mais, a faca soma 1,12 / 1,08.</summary>
	[Export] public float KnifeSpeedMultiplier = 1.037037f;
	[Export] public float KnifeRange = 2.2f;
	/// <summary>Tempo entre dois golpes. 0,40 s desde 19/11 (era 0,55: "o
	/// ataque da faca precisa responder clique a clique, sendo um pouco
	/// mais rápido"). O clique dado no meio de um golpe não se perde: fica
	/// guardado e sai quando o golpe acaba (<see cref="TryAttack"/>).</summary>
	[Export] public float KnifeCooldown = 0.40f;

	/// <summary>A estocada (botão direito): o dano de frente, quanto vale
	/// pelas costas, o cone das costas (graus entre o golpe e a frente do
	/// alvo), quando o dano sai (a lâmina chega à frente no quadro 6 do
	/// clipe) e quanto a faca fica ocupada. Pelas costas, 240 mata um humano
	/// de vida cheia (200); de frente, 120 não mata sozinho.</summary>
	[ExportGroup("Faca: estocada")]
	[Export] public float KnifeStabDamage = 120f;
	[Export] public float KnifeStabBehind = 2f;
	[Export] public float KnifeStabCone = 60f;
	[Export] public float KnifeStabHitDelay = 0.2f;
	[Export] public float KnifeStabCooldown = 0.8f;

	/// <summary>A estocada começou (a mão anima, o som sai).</summary>
	[Signal] public delegate void StabbedEventHandler();

	private double _stabHitIn = -1;

	/// <summary>No meio da estocada: o dano ainda vai sair.</summary>
	public bool IsStabbing => _stabHitIn >= 0;

	/// <summary>Botão direito com a faca: a estocada, se a faca está livre.</summary>
	public bool TryStab()
	{
		if (_form == null || _form.IsWolf || !_form.CanAttack || _health.IsDead) return false;
		if (HumanSlot != Slot.Knife || _cooldown > 0 || _stabHitIn >= 0 || !Match.IsLive) return false;

		var knife = Arsenal.KnifeStats(MeleeId);
		_cooldown = KnifeStabCooldown * knife.Cooldown;
		_attackQueue = 0;
		_stabHitIn = KnifeStabHitDelay;
		EmitSignal(SignalName.Stabbed);
		return true;
	}

	/// <summary>O dono do boneco estocou: o boneco repete (o dano é da
	/// máquina do dono; aqui o golpe só anima e tira sangue).</summary>
	public void RemoteStab()
	{
		if (_form.IsWolf || _health.IsDead) return;
		if (HumanSlot != Slot.Knife)
		{
			HumanSlot = Slot.Knife;
			AnnounceHeld();
		}
		_cooldown = 0;
		_stabHitIn = -1;
		TryStab();
	}

	private void TickStab(double delta)
	{
		if (_stabHitIn < 0) return;
		_stabHitIn -= delta;
		if (_stabHitIn > 0) return;
		_stabHitIn = -1;
		if (_health.IsDead || _form.IsWolf || HumanSlot != Slot.Knife) return;

		var knife = Arsenal.KnifeStats(MeleeId);
		Vector3 from = _head.GlobalPosition;
		Vector3 forward = -_head.GlobalTransform.Basis.Z;
		Vector3 to = from + forward * (KnifeRange * knife.Range);
		Health hit = Trace(from, to, Targets(), false, out Vector3 impact, out _, out bool headshot);
		if (hit == null) return;

		// As costas: a frente do corpo de quem levou (o giro dele) contra o golpe.
		bool behind = false;
		if (hit.GetParent() is Node3D target)
		{
			Vector3 facing = -target.GlobalTransform.Basis.Z;
			behind = CombatRules.FromBehind(facing.X, facing.Z, forward.X, forward.Z, KnifeStabCone);
		}
		float damage = CombatRules.KnifeStab(KnifeStabDamage * knife.Damage, behind, KnifeStabBehind, headshot, HeadshotMultiplier);
		Bleed(hit, impact, forward, behind ? 1.6f : 1.2f);
		Damage(hit, damage, from, headshot);
	}

	/// <summary>
	/// Facada. Não faz ruído de tiro: é a arma de quem está se movendo
	/// em silêncio e não quer entregar a posição por um abate.
	/// </summary>
	private void Knife()
	{
		// A versão da faca muda um pouco o dano, o ritmo e o alcance (Arsenal.KnifeStats).
		var knife = Arsenal.KnifeStats(MeleeId);
		_cooldown = KnifeCooldown * knife.Cooldown;
		EmitSignal(SignalName.Fired);

		Vector3 from = _head.GlobalPosition;
		Vector3 to = from - _head.GlobalTransform.Basis.Z * (KnifeRange * knife.Range);

		Health hit = Trace(from, to, Targets(), false, out Vector3 impact, out _, out bool headshot);
		if (hit == null) return;

		float damage = KnifeDamage * knife.Damage * (headshot ? HeadshotMultiplier : 1f);
		// A facada tira sangue, como o tiro e a garra (Jhon, 24/11: "as facadas tiram sangue do oponente?").
		Bleed(hit, impact, (to - from).Normalized(), headshot ? 1.4f : 1f);

		Damage(hit, damage, from, headshot);
	}
}
