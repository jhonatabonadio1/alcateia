namespace Alcateia.UnitTests;

/// <summary>As contas do combate (scripts/domain/CombatRules.cs), com os
/// números do jogo: M16, AK, M3, Bruto e Ligeiro.</summary>
public static class CombatRulesTests
{
	// ------------------------------------------------------------ queda de dano

	[Test("o dano é inteiro até o começo da queda e o mínimo depois do fim (M16: 35 a 60 m, 70%)")]
	public static void FalloffEnds()
	{
		Check.Near(1.0, CombatRules.Falloff(20, 35, 60, 0.7f), 1e-6, "a 20 m");
		Check.Near(1.0, CombatRules.Falloff(35, 35, 60, 0.7f), 1e-6, "no começo da queda");
		Check.Near(0.7, CombatRules.Falloff(60, 35, 60, 0.7f), 1e-6, "no fim da queda");
		Check.Near(0.7, CombatRules.Falloff(140, 35, 60, 0.7f), 1e-6, "muito longe");
	}

	[Test("entre o começo e o fim da queda, o dano cai em linha reta")]
	public static void FalloffIsLinear() =>
		Check.Near(0.85, CombatRules.Falloff(47.5f, 35, 60, 0.7f), 1e-5, "no meio da queda");

	// ------------------------------------------------------------ dano de arma

	[Test("um tiro de M16 no corpo tira 60 de 200; na cabeça, 150")]
	public static void M16Damage()
	{
		Check.Near(60, CombatRules.FirearmDamage(30, 1, false, 2.5f), 1e-4, "corpo");
		Check.Near(150, CombatRules.FirearmDamage(30, 1, true, 2.5f), 1e-4, "cabeça");
	}

	[Test("a AK mata com um tiro na cabeça de perto (o documento do arsenal)")]
	public static void AkHeadshotKills() =>
		Check.True(CombatRules.FirearmDamage(42, 1, true, 2.4f) >= 200, "dano na cabeça ≥ 200");

	[Test("a M3 com os 9 chumbos no corpo, de perto, mata um humano")]
	public static void ShotgunPointBlank() =>
		Check.True(9 * CombatRules.FirearmDamage(13, 1, false, 1.5f) >= 200, "9 chumbos ≥ 200");

	// ------------------------------------------------------------ dispersão e recuo

	[Test("a dispersão é a base mais o acumulado, vezes andar e agachar")]
	public static void SpreadMultipliers()
	{
		Check.Near(0.35, CombatRules.Spread(0.35f, 0, false, 2.2f, false, 0.7f), 1e-6, "parado");
		Check.Near(0.77, CombatRules.Spread(0.35f, 0, true, 2.2f, false, 0.7f), 1e-6, "andando");
		Check.Near(0.245, CombatRules.Spread(0.35f, 0, false, 2.2f, true, 0.7f), 1e-6, "agachado");
		Check.Near(1.35, CombatRules.Spread(0.35f, 1, false, 2.2f, false, 0.7f), 1e-6, "com acumulado");
	}

	[Test("o acumulado da rajada para no teto da arma")]
	public static void BloomCaps()
	{
		float bloom = 0;
		for (int shot = 0; shot < 40; shot++)
			bloom = CombatRules.BloomAfterShot(bloom, 0.22f, 4f, 0.35f);
		Check.Near(3.65, bloom, 1e-5, "teto do M16 (4 − 0,35)");
	}

	[Test("parado de atirar, o acumulado volta até zero e não passa")]
	public static void BloomRecovers()
	{
		Check.Near(1.1, CombatRules.BloomRecovered(2f, 9f, 0.1f), 1e-5, "em 0,1 s");
		Check.Near(0, CombatRules.BloomRecovered(0.5f, 9f, 1f), 1e-6, "não fica negativo");
	}

	[Test("o recuo é forte nos primeiros tiros e o de controle depois (M16: 4 tiros)")]
	public static void RecoilPhases()
	{
		Check.Near(1.0, CombatRules.RecoilVertical(1, 4, 1f, 0.55f), 1e-6, "1º tiro");
		Check.Near(1.0, CombatRules.RecoilVertical(4, 4, 1f, 0.55f), 1e-6, "4º tiro");
		Check.Near(0.55, CombatRules.RecoilVertical(5, 4, 1f, 0.55f), 1e-6, "5º tiro");
	}

	// ------------------------------------------------------------ golpes do lobo

	[Test("a garra do Bruto tira 120 de um humano, e o 3º golpe do combo, 180")]
	public static void BrutoClaw()
	{
		Check.Near(120, CombatRules.WolfStrike(75, 1f, true, 1.6f, false, 1.5f), 1e-4, "1º golpe");
		Check.Near(180, CombatRules.WolfStrike(75, 1.5f, true, 1.6f, false, 1.5f), 1e-4, "3º golpe");
		Check.Near(75, CombatRules.WolfStrike(75, 1f, false, 1.6f, false, 1.5f), 1e-4, "contra lobo");
	}

	[Test("cair de cima de alguém vale o bônus aéreo da classe")]
	public static void AerialBonus()
	{
		Check.Near(180, CombatRules.WolfStrike(75, 1f, true, 1.6f, true, 1.5f), 1e-4, "Bruto");
		Check.Near(176, CombatRules.WolfStrike(55, 1f, true, 1.6f, true, 2.0f), 1e-4, "Ligeiro");
	}

	[Test("o salto cheio vale 2,1 garras; sem carga, 40% disso")]
	public static void PounceDamage()
	{
		Check.Near(2.1, CombatRules.PounceMultiplier(2.1f, 0.4f, 1f), 1e-5, "cheio");
		Check.Near(0.84, CombatRules.PounceMultiplier(2.1f, 0.4f, 0f), 1e-5, "sem carga");
		Check.Near(252, CombatRules.WolfStrike(75, CombatRules.PounceMultiplier(2.1f, 0.4f, 1f), true, 1.6f, false, 1.5f),
			1e-3, "Bruto cheio contra humano");
	}

	[Test("a distância do salto é só a da barra: 1,5 m sem carga, 8,4 m cheio, nunca além")]
	public static void PounceRange()
	{
		Check.Near(1.5, CombatRules.PounceRange(1.5f, 8.4f, 0f), 1e-5, "sem carga");
		Check.Near(4.95, CombatRules.PounceRange(1.5f, 8.4f, 0.5f), 1e-5, "metade");
		Check.Near(8.4, CombatRules.PounceRange(1.5f, 8.4f, 1f), 1e-5, "cheio");
		Check.Near(8.4, CombatRules.PounceRange(1.5f, 8.4f, 1.4f), 1e-5, "acima do cheio");
	}

	[Test("a barra enche em 1 s e não passa de cheia")]
	public static void ChargePower()
	{
		Check.Near(0.5, CombatRules.ChargePower(0.5, 1f), 1e-6, "meio segundo");
		Check.Near(1, CombatRules.ChargePower(2.0, 1f), 1e-6, "dois segundos");
	}

	[Test("o bot carrega o bastante para cair a uma garra do alvo")]
	public static void BotChoosesCharge()
	{
		float power = CombatRules.PounceChargeFor(7f, 1.5f, 1.5f, 8.4f);
		Check.Near(7f - 1.5f, CombatRules.PounceRange(1.5f, 8.4f, power), 1e-4, "cai a uma garra de 7 m");
		Check.Near(0, CombatRules.PounceChargeFor(2f, 1.5f, 1.5f, 8.4f), 1e-6, "perto demais: sem carga");
		Check.Near(1, CombatRules.PounceChargeFor(30f, 1.5f, 1.5f, 8.4f), 1e-6, "longe demais: cheio");
	}

	// ------------------------------------------------------------ vida e escudo

	[Test("no humano o dano vai direto na vida")]
	public static void HumanTakesDamage()
	{
		var hit = CombatRules.ApplyDamage(200, 0, 60, 1f, 1f, false);
		Check.Near(60, hit.Dealt, 1e-5, "valeu");
		Check.Near(140, hit.Hp, 1e-5, "vida");
		Check.False(hit.Lethal, "não matou");
	}

	[Test("no lobo o corpo resiste e o escudo absorve antes da vida")]
	public static void WolfBodyAndShield()
	{
		var hit = CombatRules.ApplyDamage(200, 200, 60, 1f, 0.6f, false);
		Check.Near(36, hit.Dealt, 1e-4, "60 × 0,6");
		Check.Near(164, hit.Shield, 1e-4, "escudo");
		Check.Near(200, hit.Hp, 1e-4, "vida intacta");
	}

	[Test("tiro na cabeça atravessa a resistência do lobo")]
	public static void HeadshotIgnoresResistance()
	{
		var hit = CombatRules.ApplyDamage(200, 100, 150, 1f, 0.6f, true);
		Check.Near(150, hit.Dealt, 1e-4, "valeu inteiro");
		Check.Near(0, hit.Shield, 1e-4, "escudo zerado");
		Check.Near(150, hit.Hp, 1e-4, "o resto foi na vida");
	}

	[Test("transformando, o dano vale 1,5×")]
	public static void ShiftingIsVulnerable() =>
		Check.Near(90, CombatRules.ApplyDamage(200, 0, 60, 1.5f, 1f, false).Dealt, 1e-4, "60 × 1,5");

	[Test("o golpe que zera a vida é letal")]
	public static void LethalHit() =>
		Check.True(CombatRules.ApplyDamage(100, 0, 120, 1f, 1f, false).Lethal, "120 em 100 de vida");
}
