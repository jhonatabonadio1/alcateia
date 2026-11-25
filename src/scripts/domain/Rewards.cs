/// <summary>
/// O que uma partida rende: XP para a patente e Presas para a loja.
///
/// É regra pura, e fica aqui porque dois programas a usam: o jogo, para
/// mostrar a conta na tela de resultado, e o servidor, que é quem dá o
/// prêmio de verdade numa partida online — o cliente não é confiável
/// para dizer quanto ganhou.
///
/// A conta (no ritmo de uma partida de 10 minutos): 30 de XP por
/// participar, mais 4 por abate e 2 por assistência, mais vitória (24) e
/// MVP (15). Dá entre 60 e 150 por partida, uns 100 numa partida comum.
/// Era 150/20/10/120/80 (300 a 600): subia-se de patente rápido demais
/// (pedido do Jhon, 17/11). A curva está em Ranks.XpFor. As Presas não
/// mudaram.
/// </summary>
public static class Rewards
{
	public const int XpBase = 30, XpKill = 4, XpAssist = 2, XpWin = 24, XpDraw = 12, XpMvp = 15;
	/// <summary>Os valores de fábrica do Cash por partida (Presas no código). Pensados
	/// para o jogador comprar cedo e se divertir (Jhon, 25/11): uma partida comum
	/// (~180 a 300) paga um aluguel de 1 dia da loja (120 a 280), e umas 4 partidas
	/// pagam uma semana. O painel muda tudo isto (<see cref="CashRates"/>).</summary>
	public const int PresasBase = 150, PresasKill = 10, PresasAssist = 5, PresasWin = 100, PresasDraw = 50, PresasMvp = 50;

	/// <summary>Quanto Cash cada coisa rende numa partida. O servidor lê do painel
	/// ("recompensas"); o jogo lê o mesmo, para a previsão da tela de resultado.</summary>
	public readonly record struct CashRates(int Base, int Kill, int Assist, int Win, int Draw, int Mvp)
	{
		public static readonly CashRates Default = new(PresasBase, PresasKill, PresasAssist, PresasWin, PresasDraw, PresasMvp);
	}

	/// <summary>O Cash com que uma conta nova começa: nenhum (Jhon, 25/11).</summary>
	public const long StarterCash = 0;

	/// <summary>Teto do que conta numa partida: uma partida de dez
	/// minutos não tem duzentos abates, e o servidor não paga por eles.</summary>
	public const int MaxKills = 120, MaxAssists = 120;

	/// <summary>Rage (<see cref="Rage"/>): 1 de XP a cada tantos segundos em
	/// chamas, até o teto. Só XP: o rage não dá Presas.</summary>
	public const int FireSecondsPerXp = 6, MaxFireXp = 40;

	public readonly record struct Gain(long Xp, long Presas);

	/// <summary>O total de uma partida.</summary>
	public static Gain ForMatch(int kills, int assists, bool won, bool draw, bool mvp, int fireSeconds = 0, CashRates? cash = null)
	{
		CashRates c = cash ?? CashRates.Default;
		kills = System.Math.Clamp(kills, 0, MaxKills);
		assists = System.Math.Clamp(assists, 0, MaxAssists);

		long xp = XpBase + XpKill * kills + XpAssist * assists;
		long presas = c.Base + (long)c.Kill * kills + (long)c.Assist * assists;

		if (won) { xp += XpWin; presas += c.Win; }
		else if (draw) { xp += XpDraw; presas += c.Draw; }
		if (mvp) { xp += XpMvp; presas += c.Mvp; }
		// Só o tempo em chamas que esses abates e assistências permitem.
		xp += FireXp(System.Math.Min(fireSeconds, Rage.MaxFireSeconds(kills, assists)));

		return new Gain(xp, presas);
	}

	/// <summary>O XP do tempo em chamas.</summary>
	public static int FireXp(int fireSeconds) => System.Math.Min(MaxFireXp, System.Math.Clamp(fireSeconds, 0, 3600) / FireSecondsPerXp);
}
