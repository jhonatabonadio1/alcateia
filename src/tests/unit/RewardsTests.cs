namespace Alcateia.UnitTests;

/// <summary>O que uma partida rende (scripts/domain/Rewards.cs) — a
/// mesma conta no jogo e no servidor.</summary>
public static class RewardsTests
{
	[Test("participar rende 30 de XP e 150 Cash")]
	public static void Participation()
	{
		Rewards.Gain g = Rewards.ForMatch(0, 0, won: false, draw: false, mvp: false);
		Check.Equal(30L, g.Xp, "XP");
		Check.Equal(150L, g.Presas, "Cash");
	}

	[Test("o Cash do painel vale no lugar do de fábrica")]
	public static void PanelRates()
	{
		var rates = new Rewards.CashRates(Base: 10, Kill: 1, Assist: 2, Win: 3, Draw: 4, Mvp: 5);
		Rewards.Gain g = Rewards.ForMatch(10, 4, won: true, draw: false, mvp: true, cash: rates);
		Check.Equal(10L + 10 + 8 + 3 + 5, g.Presas, "Cash com os valores do painel");
		Check.Equal(30L + 40 + 8 + 24 + 15, g.Xp, "o XP não muda");
	}

	[Test("dez abates, quatro assistências, vitória e MVP: 117 de XP e 406 Cash")]
	public static void FullMatch()
	{
		Rewards.Gain g = Rewards.ForMatch(10, 4, won: true, draw: false, mvp: true);
		Check.Equal(30L + 40 + 8 + 24 + 15, g.Xp, "XP");
		Check.Equal(150L + 100 + 20 + 100 + 50, g.Presas, "Cash");
	}

	[Test("empate rende metade da vitória, e vitória não soma com empate")]
	public static void DrawIsHalf()
	{
		Rewards.Gain draw = Rewards.ForMatch(0, 0, won: false, draw: true, mvp: false);
		Rewards.Gain both = Rewards.ForMatch(0, 0, won: true, draw: true, mvp: false);
		Check.Equal(30L + 12, draw.Xp, "XP do empate");
		Check.Equal(30L + 24, both.Xp, "vitória e empate marcados juntos valem só a vitória");
	}

	[Test("o servidor não paga por abate que não cabe numa partida")]
	public static void KillsAreCapped()
	{
		Rewards.Gain many = Rewards.ForMatch(100000, -5, won: false, draw: false, mvp: false);
		Rewards.Gain cap = Rewards.ForMatch(Rewards.MaxKills, 0, won: false, draw: false, mvp: false);
		Check.Equal(cap.Xp, many.Xp, "XP no teto de abates");
		Check.Equal(cap.Presas, many.Presas, "Presas no teto de abates");
	}
}
