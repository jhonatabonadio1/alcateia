using System;

/// <summary>
/// As contas do combate, em C# puro: sem Godot, sem estado, sem sorteio.
/// Quem tem corpo e física (a arma, a vida) decide o que aconteceu e
/// pede o número aqui — então toda fórmula de dano, dispersão e salto
/// mora num lugar só e tem teste de unidade (tests/unit/CombatRulesTests).
///
/// Os pesos vêm de quem chama (os [Export] da arma, o .tres da arma e da
/// classe): aqui só as regras.
/// </summary>
public static class CombatRules
{
	// ------------------------------------------------------------ armas de fogo

	/// <summary>O dano das armas de fogo está todo multiplicado por isto:
	/// os números do documento são por tiro de uma vida de 100, e a vida
	/// no jogo é 200.</summary>
	public const float FirearmDamageScale = 2f;

	/// <summary>Fração do dano que sobra a esta distância: inteiro até o
	/// começo da queda, o mínimo depois do fim, e em linha reta entre os
	/// dois.</summary>
	public static float Falloff(float distance, float start, float end, float min)
	{
		if (distance <= start) return 1f;
		if (distance >= end) return min;

		float t = (distance - start) / MathF.Max(end - start, 0.01f);
		return Lerp(1f, min, t);
	}

	/// <summary>Dano de um projétil de arma de fogo.</summary>
	public static float FirearmDamage(float baseDamage, float falloff, bool headshot, float headshotMultiplier) =>
		baseDamage * FirearmDamageScale * falloff * (headshot ? headshotMultiplier : 1f);

	/// <summary>Dispersão do próximo tiro, em graus: a base mais o que a
	/// rajada acumulou, vezes o movimento e a postura.</summary>
	public static float Spread(float baseSpread, float bloom, bool moving, float moveMultiplier,
		bool crouching, float crouchMultiplier)
	{
		float spread = baseSpread + bloom;
		if (moving) spread *= moveMultiplier;
		if (crouching) spread *= crouchMultiplier;
		return spread;
	}

	/// <summary>Andando mais rápido que isto (m/s), a dispersão de
	/// movimento vale.</summary>
	public const float MovingSpreadSpeed = 0.6f;

	/// <summary>O acumulado depois de um tiro: cresce o passo da arma, até
	/// o teto dela.</summary>
	public static float BloomAfterShot(float bloom, float perShot, float spreadMax, float spreadBase) =>
		MathF.Min(bloom + perShot, MathF.Max(spreadMax - spreadBase, 0f));

	/// <summary>O acumulado voltando à base enquanto não se atira.</summary>
	public static float BloomRecovered(float bloom, float recoveryPerSecond, float delta) =>
		MathF.Max(0f, bloom - recoveryPerSecond * delta);

	/// <summary>Recuo vertical do tiro número <paramref name="streak"/> da
	/// rajada: forte nos primeiros, o da fase de controle depois.</summary>
	public static float RecoilVertical(int streak, int earlyShots, float early, float late) =>
		streak <= earlyShots ? early : late;

	// ------------------------------------------------------------ lobo

	/// <summary>
	/// Dano de um golpe do lobo num alvo: a garra da classe vezes o
	/// multiplicador do golpe (combo, salto, bote), mais o bônus contra
	/// humano e o de quem cai de cima.
	/// </summary>
	public static float WolfStrike(float attackDamage, float moveMultiplier, bool targetIsHuman,
		float vsHumanBonus, bool aerial, float airAttackBonus)
	{
		float damage = attackDamage * moveMultiplier;
		if (targetIsHuman) damage *= vsHumanBonus;
		if (aerial) damage *= airAttackBonus;
		return damage;
	}

	/// <summary>Carga do salto, de 0 a 1, depois de segurar
	/// <paramref name="held"/> segundos.</summary>
	public static float ChargePower(double held, float fullChargeTime) =>
		Math.Clamp((float)(held / fullChargeTime), 0f, 1f);

	/// <summary>Até onde o salto vai com esta carga: só ela decide
	/// (decisão do Jhon).</summary>
	public static float PounceRange(float minDistance, float maxDistance, float power) =>
		Lerp(minDistance, maxDistance, Math.Clamp(power, 0f, 1f));

	/// <summary>Multiplicador de dano do salto com esta carga.</summary>
	public static float PounceMultiplier(float fullBonus, float minPower, float power) =>
		fullBonus * Lerp(minPower, 1f, power);

	/// <summary>A carga para o salto cair a uma garra de quem está a
	/// <paramref name="distance"/> metros. É como o bot escolhe.</summary>
	public static float PounceChargeFor(float distance, float clawReach, float minDistance, float maxDistance) =>
		Math.Clamp((distance - clawReach - minDistance) / MathF.Max(maxDistance - minDistance, 0.1f), 0f, 1f);

	// ------------------------------------------------------------ vida

	/// <summary>
	/// O que um golpe faz numa vida. O multiplicador da transformação vale
	/// sempre; a resistência do corpo, só fora da cabeça (headshot
	/// atravessa a resistência inteira — é a resposta do humano contra o
	/// lobo de frente). O escudo absorve antes da vida.
	/// </summary>
	public static DamageOutcome ApplyDamage(float hp, float shield, float amount,
		float damageMultiplier, float bodyResistance, bool headshot)
	{
		float dealt = amount * damageMultiplier;
		if (!headshot) dealt *= bodyResistance;

		float taken = dealt;
		float absorbed = 0f;
		if (shield > 0)
		{
			absorbed = MathF.Min(shield, taken);
			taken -= absorbed;
		}

		return new DamageOutcome(dealt, hp - taken, shield - absorbed);
	}

	private static float Lerp(float from, float to, float t) => from + (to - from) * t;
}

/// <summary>O resultado de um golpe numa vida: quanto valeu (depois da
/// resistência, antes do escudo), e como ficaram vida e escudo.</summary>
public readonly record struct DamageOutcome(float Dealt, float Hp, float Shield)
{
	public bool Lethal => Hp <= 0;
}
