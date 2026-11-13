using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Alcateia.Dominio.Partidas;

/// <summary>Um lugar numa partida: um jogador, ou um bot. O bot tem
/// número negativo no lugar da conta e um dono (<see cref="Owner"/>): a
/// conta do jogador cuja máquina o move. Bot não é conta — não ganha XP,
/// não tem patente e não entra no resultado.</summary>
public sealed record MatchSeat(long AccountId, string Name, int Team, long Owner = 0)
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

	public string Title => Settings?["title"]?.GetValue<string>() ?? "";
	public string Map => Settings?["map"]?.GetValue<string>() ?? "";
	public string Mode => Settings?["mode"]?.ToString() ?? "Equipe";
}

/// <summary>O que um jogador fez, segundo o servidor de partida.</summary>
public sealed record ReportedLine(long AccountId, int Kills, int Deaths, int Assists, int Headshots, int Points);

/// <summary>O fim de uma partida, como o servidor de partida conta.</summary>
public sealed record MatchReport(string MatchKey, string ServerId, int ScoreBlue, int ScoreOrange, IReadOnlyList<ReportedLine> Players);

/// <summary>Um jogador no resultado fechado, com o que ganhou.</summary>
public sealed record SettledLine(MatchSeat Seat, int Kills, int Deaths, int Assists, int Headshots, int Points,
	bool Won, bool Draw, bool Mvp, Rewards.Gain Gain);

/// <summary>O resultado fechado: o que vai para o banco e para os jogadores.</summary>
public sealed record SettledMatch(MatchAssignment Assignment, string ServerId, int Winner, int ScoreBlue, int ScoreOrange,
	IReadOnlyList<SettledLine> Lines);

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

	public static SettledMatch Settle(MatchAssignment assignment, MatchReport report)
	{
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
				Points = Math.Clamp(line.Points, 0, 99999)
			}));
		}

		long mvp = counted.Where(c => c.Line.Points > 0).OrderByDescending(c => c.Line.Points).Select(c => c.Seat.AccountId).FirstOrDefault();

		var lines = counted.Select(c =>
		{
			bool won = winner != Draw && c.Seat.Team == winner, draw = winner == Draw, isMvp = c.Seat.AccountId == mvp;
			return new SettledLine(c.Seat, c.Line.Kills, c.Line.Deaths, c.Line.Assists, c.Line.Headshots, c.Line.Points,
				won, draw, isMvp, Rewards.ForMatch(c.Line.Kills, c.Line.Assists, won, draw, isMvp));
		}).ToList();

		return new SettledMatch(assignment, report.ServerId, winner, report.ScoreBlue, report.ScoreOrange, lines);
	}
}
