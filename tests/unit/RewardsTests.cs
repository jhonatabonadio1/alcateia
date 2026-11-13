namespace Alcateia.UnitTests;

/// <summary>O que uma partida rende (scripts/domain/Rewards.cs) — a
/// mesma conta no jogo e no servidor.</summary>
public static class RewardsTests
{
	[Test("participar rende 150 de XP e 100 Presas")]
	public static void Participation()
	{
		Rewards.Gain g = Rewards.ForMatch(0, 0, won: false, draw: false, mvp: false);
		Check.Equal(150L, g.Xp, "XP");
		Check.Equal(100L, g.Presas, "Presas");
	}

	[Test("dez abates, quatro assistências, vitória e MVP: 590 de XP e 316 Presas")]
	public static void FullMatch()
	{
		Rewards.Gain g = Rewards.ForMatch(10, 4, won: true, draw: false, mvp: true);
		Check.Equal(150L + 200 + 40 + 120 + 80, g.Xp, "XP");
		Check.Equal(100L + 80 + 16 + 80 + 40, g.Presas, "Presas");
	}

	[Test("empate rende metade da vitória, e vitória não soma com empate")]
	public static void DrawIsHalf()
	{
		Rewards.Gain draw = Rewards.ForMatch(0, 0, won: false, draw: true, mvp: false);
		Rewards.Gain both = Rewards.ForMatch(0, 0, won: true, draw: true, mvp: false);
		Check.Equal(150L + 60, draw.Xp, "XP do empate");
		Check.Equal(150L + 120, both.Xp, "vitória e empate marcados juntos valem só a vitória");
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
