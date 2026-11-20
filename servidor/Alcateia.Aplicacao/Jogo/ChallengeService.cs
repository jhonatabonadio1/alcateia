using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Aplicacao.Jogo;

/// <summary>
/// Os desafios no servidor: o progresso anda com o resultado que o
/// servidor de partida mandou (nunca com o que o cliente diz) e o prêmio
/// sai uma vez só por período.
/// </summary>
public sealed class ChallengeService
{
	private readonly IPlayerRepository _players;
	private readonly GameDataService _data;
	private readonly IClock _clock;

	public ChallengeService(IPlayerRepository players, GameDataService data, IClock clock)
	{
		_players = players;
		_data = data;
		_clock = clock;
	}

	/// <summary>Os desafios valendo agora, com o progresso da conta em cada um.</summary>
	public async Task<IReadOnlyList<ChallengeState>> For(long accountId)
	{
		DateTime now = _clock.UtcNow;
		var progress = (await _players.Progress(accountId)).ToDictionary(p => (p.ChallengeId, p.PeriodKey));
		var list = new List<ChallengeState>();
		foreach (Challenge c in _data.Current.Challenges.Where(c => c.RunningAt(now)))
		{
			string key = ChallengeRules.PeriodKey(c, now);
			progress.TryGetValue((c.Id, key), out var p);
			list.Add(new ChallengeState(c, key, Math.Min(p.Progress, c.Target), p.Claimed));
		}
		return list;
	}

	/// <summary>Soma o resultado de uma partida nos desafios de cada jogador.</summary>
	public async Task Apply(SettledMatch match)
	{
		DateTime now = _clock.UtcNow;
		int minutes = match.Minutes(now);
		var running = _data.Current.Challenges.Where(c => c.RunningAt(now)).ToList();

		foreach (SettledLine line in match.Lines)
			foreach (Challenge c in running)
			{
				int gain = ChallengeRules.Gain(c.Metric, line, minutes, line.KillsWolf, line.KillsHuman);
				if (gain > 0) await _players.AddProgress(line.Seat.AccountId, c.Id, ChallengeRules.PeriodKey(c, now), gain);
			}
	}

	/// <summary>Recebe o prêmio de um desafio completo.</summary>
	public async Task<Challenge> Claim(long accountId, string challengeId)
	{
		DateTime now = _clock.UtcNow;
		Challenge c = _data.Current.Challenges.FirstOrDefault(x => x.Id == challengeId && x.RunningAt(now))
			?? throw new Failure(FailureKind.NotFound, "desafio_inexistente", "Esse desafio não está valendo.");

		DateTime? itemUntil = c.ItemDays > 0 ? now.AddDays(c.ItemDays) : null;
		if (!await _players.Claim(accountId, c, ChallengeRules.PeriodKey(c, now), itemUntil))
			throw new Failure(FailureKind.Conflict, "premio_indisponivel", "Desafio incompleto ou prêmio já recebido.");
		return c;
	}
}
