using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Alcateia.Dominio.Partidas;

/// <summary>Um lugar numa partida: um jogador, ou um bot. O bot tem
/// número negativo no lugar da conta e um dono (<see cref="Owner"/>): a
/// conta do jogador cuja máquina o move. Bot não é conta — não ganha XP,
/// não tem patente e não entra no resultado.</summary>
public sealed record MatchSeat(long AccountId, string Name, int Team, long Owner = 0, IReadOnlyCollection<string> Weapons = null)
{
	public bool IsBot => AccountId < 0;
}

/// <summary>Uma partida a jogar: a sala de onde veio, as regras e a
/// escalação. É o que o servidor de partida recebe.</summary>
public sealed class MatchAssignment
{
	public string MatchKey;
	public int RoomId;
	public JsonObject Settings;
	public List<MatchSeat> Players = new();
	public DateTime StartedAt = DateTime.UtcNow;

	/// <summary>Esta entrega é da partida que o servidor de partida já tem,
	/// com lugares a mais (<see cref="Alcateia.Dominio.Frota.Fleet.AddSeat"/>): ele só abre os
	/// lugares, nunca uma partida nova com ela (que pode ter acabado no meio
	/// do caminho).</summary>
	public bool Update;

	public string Title => Settings?["title"]?.GetValue<string>() ?? "";
	public string Map => Settings?["map"]?.GetValue<string>() ?? "";
	public string Mode => Settings?["mode"]?.ToString() ?? "Equipe";
}

/// <summary>O que um jogador fez, segundo o servidor de partida.</summary>
public sealed record ReportedLine(long AccountId, int Kills, int Deaths, int Assists, int Headshots, int Points)
{
	/// <summary>Dos abates, quantos como lobo (o resto é como humano). Servidor
	/// de partida antigo não manda: fica zero e os desafios "como lobo" não andam.</summary>
	public int KillsWolf { get; init; }

	/// <summary>Acertos que o servidor de partida recusou (parede no caminho,
	/// fora da mira, longe demais, rápido demais: decisão 0016). Só o
	/// servidor de partida autoritativo confere; o de repasse manda zero.</summary>
	public int RejectedHits { get; init; }

	/// <summary>Acertos de arma de fogo e, deles, os que vieram logo depois de
	/// um tranco de mira (o AimAudit do servidor de partida, 27/11). Servidor
	/// de partida antigo não manda: zero, e nada é denunciado.</summary>
	public int AimHits { get; init; }
	public int AimSnaps { get; init; }

	/// <summary>Vezes em que o jogo contou uma forma que as regras não deixam (o FormAudit).</summary>
	public int RefusedForms { get; init; }

	/// <summary>Segundos em chamas (o rage, <see cref="Rage"/>): vira XP.
	/// Servidor de partida antigo não manda: zero.</summary>
	public int FireSeconds { get; init; }
}

/// <summary>O fim de uma partida, como o servidor de partida conta.</summary>
public sealed record MatchReport(string MatchKey, string ServerId, int ScoreBlue, int ScoreOrange, IReadOnlyList<ReportedLine> Players);

/// <summary>Um jogador no resultado fechado, com o que ganhou.</summary>
public sealed record SettledLine(MatchSeat Seat, int Kills, int Deaths, int Assists, int Headshots, int Points,
	bool Won, bool Draw, bool Mvp, Rewards.Gain Gain)
{
	public int KillsWolf { get; init; }
	public int KillsHuman => Kills - KillsWolf;
}

/// <summary>O resultado fechado: o que vai para o banco e para os jogadores.</summary>
public sealed record SettledMatch(MatchAssignment Assignment, string ServerId, int Winner, int ScoreBlue, int ScoreOrange,
	IReadOnlyList<SettledLine> Lines)
{
	/// <summary>Minutos jogados, para os desafios de tempo.</summary>
	public int Minutes(DateTime now) => (int)Math.Clamp((now - Assignment.StartedAt).TotalMinutes, 0, 120);
}

/// <summary>Uma partida no histórico de um jogador.</summary>
public sealed record MatchHistoryEntry(string Map, int Winner, int ScoreBlue, int ScoreOrange, string EndedAt, int Team,
	int Kills, int Deaths, int Assists, bool Mvp, long Xp, long Presas);

/// <summary>
/// Fecha o resultado de uma partida. Quem conta é o servidor de partida
/// (o cliente não diz quanto ganhou), mas só vale quem estava escalado,
/// uma vez cada, com números que cabem; bot fica de fora. O prêmio é a
/// mesma regra do jogo (<see cref="Rewards"/>).
/// </summary>
public static class MatchSettlement
{
	public const int Draw = 0, Blue = 1, Orange = 2;

	/// <summary>O maior bônus de evento que o painel pode ligar.</summary>
	public const double MaxBonus = 5;

	/// <summary><paramref name="xpBonus"/> e <paramref name="presasBonus"/>: o
	/// multiplicador de evento ("XP em dobro"), que o painel liga e desliga.</summary>
	public static SettledMatch Settle(MatchAssignment assignment, MatchReport report, double xpBonus = 1, double presasBonus = 1,
		Rewards.CashRates? cash = null)
	{
		xpBonus = Math.Clamp(xpBonus, 1, MaxBonus);
		presasBonus = Math.Clamp(presasBonus, 1, MaxBonus);
		int winner = report.ScoreBlue == report.ScoreOrange ? Draw : report.ScoreBlue > report.ScoreOrange ? Blue : Orange;
		var seats = assignment.Players.ToDictionary(p => p.AccountId);

		var counted = new List<(MatchSeat Seat, ReportedLine Line)>();
		foreach (ReportedLine line in report.Players)
		{
			if (!seats.TryGetValue(line.AccountId, out MatchSeat seat) || seat.IsBot) continue;
			if (counted.Any(c => c.Seat.AccountId == line.AccountId)) continue;
			counted.Add((seat, line with
			{
				Kills = Math.Clamp(line.Kills, 0, Rewards.MaxKills),
				Deaths = Math.Clamp(line.Deaths, 0, 999),
				Assists = Math.Clamp(line.Assists, 0, Rewards.MaxAssists),
				Headshots = Math.Clamp(line.Headshots, 0, Rewards.MaxKills),
				Points = Math.Clamp(line.Points, 0, 99999),
				KillsWolf = Math.Clamp(line.KillsWolf, 0, Math.Clamp(line.Kills, 0, Rewards.MaxKills))
			}));
		}

		long mvp = counted.Where(c => c.Line.Points > 0).OrderByDescending(c => c.Line.Points).Select(c => c.Seat.AccountId).FirstOrDefault();

		var lines = counted.Select(c =>
		{
			bool won = winner != Draw && c.Seat.Team == winner, draw = winner == Draw, isMvp = c.Seat.AccountId == mvp;
			Rewards.Gain gain = Rewards.ForMatch(c.Line.Kills, c.Line.Assists, won, draw, isMvp, c.Line.FireSeconds, cash);
			gain = new Rewards.Gain((long)Math.Round(gain.Xp * xpBonus), (long)Math.Round(gain.Presas * presasBonus));
			return new SettledLine(c.Seat, c.Line.Kills, c.Line.Deaths, c.Line.Assists, c.Line.Headshots, c.Line.Points,
				won, draw, isMvp, gain) { KillsWolf = c.Line.KillsWolf };
		}).ToList();

		return new SettledMatch(assignment, report.ServerId, winner, report.ScoreBlue, report.ScoreOrange, lines);
	}
}
