/// <summary>
/// O que uma partida rende: XP para a patente e Presas para a loja.
///
/// É regra pura, e fica aqui porque dois programas a usam: o jogo, para
/// mostrar a conta na tela de resultado, e o servidor, que é quem dá o
/// prêmio de verdade numa partida online — o cliente não é confiável
/// para dizer quanto ganhou.
///
/// A conta (no ritmo de uma partida de 10 minutos): uns 150 de XP por
/// participar, mais 20 por abate e 10 por assistência, mais vitória e
/// MVP. Dá entre 300 e 600 por partida.
/// </summary>
public static class Rewards
{
	public const int XpBase = 150, XpKill = 20, XpAssist = 10, XpWin = 120, XpDraw = 60, XpMvp = 80;
	public const int PresasBase = 100, PresasKill = 8, PresasAssist = 4, PresasWin = 80, PresasDraw = 40, PresasMvp = 40;

	/// <summary>Teto do que conta numa partida: uma partida de dez
	/// minutos não tem duzentos abates, e o servidor não paga por eles.</summary>
	public const int MaxKills = 120, MaxAssists = 120;

	public readonly record struct Gain(long Xp, long Presas);

	/// <summary>O total de uma partida.</summary>
	public static Gain ForMatch(int kills, int assists, bool won, bool draw, bool mvp)
	{
		kills = System.Math.Clamp(kills, 0, MaxKills);
		assists = System.Math.Clamp(assists, 0, MaxAssists);

		long xp = XpBase + XpKill * kills + XpAssist * assists;
		long presas = PresasBase + PresasKill * kills + PresasAssist * assists;

		if (won) { xp += XpWin; presas += PresasWin; }
		else if (draw) { xp += XpDraw; presas += PresasDraw; }
		if (mvp) { xp += XpMvp; presas += PresasMvp; }

		return new Gain(xp, presas);
	}
}
