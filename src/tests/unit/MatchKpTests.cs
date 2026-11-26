using System.Collections.Generic;

namespace Alcateia.UnitTests;

/// <summary>O KP da partida (scripts/domain/MatchKp.cs). Decisão do Jhon,
/// 26/11: morrer não tira KP, a arma paga fica desbloqueada até o fim da
/// partida, e os ganhos sobem 25% (abate 100 → 125, de lobo 70 → 88,
/// assistência 35 → 44).</summary>
public static class MatchKpTests
{
	private static readonly Dictionary<string, int> Prices = new() { ["ak74"] = 380, ["choque"] = 60, ["deagle"] = 0, ["svd"] = 600 };
	private static int Price(string id) => Prices.TryGetValue(id, out int p) ? p : 0;

	[Test("os ganhos subiram 25%")]
	public static void Gains()
	{
		Check.Equal(125, MatchKp.PerKill, "abate");
		Check.Equal(88, MatchKp.PerWolfKill, "abate de lobo");
		Check.Equal(44, MatchKp.PerAssist, "assistência");
		Check.Equal(50, MatchKp.DefaultBonus, "renascer no padrão");
	}

	[Test("o conjunto custa a soma dos itens; o que já foi pago na partida não se paga de novo")]
	public static void PaidOnce()
	{
		var kp = new MatchKp();
		string[] assault = { "ak74", "deagle", "choque" };
		Check.Equal(440, kp.CostOf(assault, Price), "a primeira vez: 380 + 0 + 60");
		kp.Unlock(assault);
		Check.Equal(0, kp.CostOf(assault, Price), "renascer com o mesmo conjunto: de graça");
		Check.Equal(600, kp.CostOf(new[] { "svd", "deagle", "choque" }, Price), "outro conjunto paga só a arma nova");
		Check.Equal(380, new MatchKp().CostOf(new[] { "ak74", "ak74" }, Price), "item repetido conta uma vez");
		kp.Reset();
		Check.Equal(440, kp.CostOf(assault, Price), "partida nova: tudo de novo");
	}

	[Test("o teto do servidor: abates, assistências e o bônus de cada entrada; morrer nunca tira")]
	public static void MostEarned()
	{
		Check.Equal(50, MatchKp.MostEarned(0, 0, 0, 0), "começo: só o bônus do padrão");
		Check.Equal(125 * 7 + 88 * 3 + 44 * 4 + 50 * 9, MatchKp.MostEarned(10, 3, 4, 8), "10 abates (3 de lobo), 4 assistências, 8 mortes");
		Check.True(MatchKp.MostEarned(2, 0, 0, 5) > MatchKp.MostEarned(2, 0, 0, 0), "mais mortes não diminuem (cada renascer pode render o bônus)");
		Check.Equal((int)MatchKp.Max, MatchKp.MostEarned(1000, 0, 0, 0), "nunca passa do teto de 10 000");
		Check.Equal(MatchKp.MostEarned(2, 2, 0, 0), MatchKp.MostEarned(2, 9, 0, 0), "abates de lobo não passam dos abates");
	}

	[Test("uma partida típica rende mais que antes")]
	public static void TypicalMatch()
	{
		// 10 abates de arma, 4 assistências, 8 mortes renascendo no padrão.
		int before = 10 * 100 + 4 * 35 + 8 * 50 - 8 * 100;   // 25/11: a morte tirava 100
		int after = 10 * MatchKp.PerKill + 4 * MatchKp.PerAssist + 8 * MatchKp.DefaultBonus;
		Check.Equal(740, before, "antes");
		Check.Equal(1826, after, "depois");
	}
}
