namespace Alcateia.UnitTests;

/// <summary>As contas do combate (scripts/domain/CombatRules.cs), com os
/// números do jogo: M16, AK, M3 e o lobo (um só desde 26/11).</summary>
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

	// O dano das armas caiu 31% em 25/11 (Jhon: "as armas estão muito
	// apelonas, os bonecos precisam ser mais resistentes"; FirearmDamageScale
	// de 2 para 1,38). Os testes abaixo seguem os números novos.

	[Test("um tiro de AR-15 no corpo tira 37 de 200 (6 matam, eram 4); na cabeça, 93 (3 matam, eram 2)")]
	public static void M16Damage()
	{
		Check.Near(37.26, CombatRules.FirearmDamage(27, 1, false, 2.5f), 1e-3, "corpo");
		Check.Near(93.15, CombatRules.FirearmDamage(27, 1, true, 2.5f), 1e-3, "cabeça");
		Check.True(5 * CombatRules.FirearmDamage(27, 1, false, 2.5f) < 200, "5 no corpo não matam");
		Check.True(6 * CombatRules.FirearmDamage(27, 1, false, 2.5f) >= 200, "6 no corpo matam");
		Check.True(2 * CombatRules.FirearmDamage(27, 1, true, 2.5f) < 200, "2 na cabeça não matam");
		Check.True(3 * CombatRules.FirearmDamage(27, 1, true, 2.5f) >= 200, "3 na cabeça matam");
	}

	[Test("a AK não mata mais com um tiro na cabeça (eram 201,6); dois matam")]
	public static void AkHeadshotKills()
	{
		Check.True(CombatRules.FirearmDamage(42, 1, true, 2.4f) < 200, "um na cabeça não mata");
		Check.True(2 * CombatRules.FirearmDamage(42, 1, true, 2.4f) >= 200, "dois na cabeça matam");
	}

	[Test("só a SVD ainda mata com um tiro na cabeça; no corpo, dois")]
	public static void SniperHeadshotKills()
	{
		Check.True(CombatRules.FirearmDamage(85, 1, true, 2.4f) >= 200, "um na cabeça mata");
		Check.True(CombatRules.FirearmDamage(85, 1, false, 2.4f) < 200, "um no corpo não mata");
		Check.True(2 * CombatRules.FirearmDamage(85, 1, false, 2.4f) >= 200, "dois no corpo matam");
	}

	[Test("a M3 (fora do arsenal) com os 9 chumbos no corpo, de perto, tira 161 de 200")]
	public static void ShotgunPointBlank() =>
		Check.Near(161.46, 9 * CombatRules.FirearmDamage(13, 1, false, 1.5f), 1e-2, "9 chumbos");

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
			bloom = CombatRules.BloomAfterShot(bloom, 0.09f, 1.8f, 0.15f);
		Check.Near(1.65, bloom, 1e-5, "teto do M16 (1,8 − 0,15)");
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

	// O lobo único (decisão do Jhon, 26/11: sem classes), no meio do Bruto
	// (garra 75, bônus aéreo 1,5) e do Ligeiro (55, 2,0): garra 65, aéreo 1,75.
	[Test("a garra do lobo tira 104 de um humano (dois golpes matam 200), e o 3º golpe do combo, 156")]
	public static void WolfClaw()
	{
		Check.Near(104, CombatRules.WolfStrike(65, 1f, true, 1.6f, false, 1.75f), 1e-4, "1º golpe");
		Check.Near(156, CombatRules.WolfStrike(65, 1.5f, true, 1.6f, false, 1.75f), 1e-4, "3º golpe");
		Check.Near(65, CombatRules.WolfStrike(65, 1f, false, 1.6f, false, 1.75f), 1e-4, "contra lobo");
	}

	[Test("cair de cima de alguém vale o bônus aéreo do lobo")]
	public static void AerialBonus()
	{
		Check.Near(182, CombatRules.WolfStrike(65, 1f, true, 1.6f, true, 1.75f), 1e-4, "lobo caindo em humano");
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

	// ------------------------------------------------------------ faca

	[Test("a estocada pelas costas: o golpe vai para onde o alvo olha, até 60°; de frente e de lado, não")]
	public static void StabFromBehind()
	{
		// o alvo olha para −z; quem bate vem de trás (+z) e golpeia para −z
		Check.True(CombatRules.FromBehind(0, -1, 0, -1, 60), "bem atrás");
		Check.True(CombatRules.FromBehind(0, -1, 0.8f, -1, 60), "atrás, de viés (39°)");
		Check.True(!CombatRules.FromBehind(0, -1, 1, -0.3f, 60), "de lado (73°)");
		Check.True(!CombatRules.FromBehind(0, -1, 0, 1, 60), "de frente");
		Check.True(!CombatRules.FromBehind(0, -1, 0, 0, 60), "um em cima do outro");
	}

	[Test("a estocada tira 120; pelas costas, 240 (mata um humano de vida cheia); costas e cabeça não somam")]
	public static void StabDamage()
	{
		Check.Near(120, CombatRules.KnifeStab(120, false, 2f, false, 1.5f), 1e-4, "de frente");
		Check.Near(240, CombatRules.KnifeStab(120, true, 2f, false, 1.5f), 1e-4, "pelas costas");
		Check.True(CombatRules.KnifeStab(120, true, 2f, false, 1.5f) >= 200, "pelas costas mata de uma vez");
		Check.Near(180, CombatRules.KnifeStab(120, false, 2f, true, 1.5f), 1e-4, "na cabeça, de frente");
		Check.Near(240, CombatRules.KnifeStab(120, true, 2f, true, 1.5f), 1e-4, "na cabeça, pelas costas");
	}
}
