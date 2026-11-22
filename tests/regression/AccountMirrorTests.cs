using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// A economia é da conta (decisão 0015): o perfil do jogo só espelha o que
/// o servidor diz. Sem conta é zero e equipamento de série; o arquivo local
/// não guarda Presas nem itens; o conjunto guardado não se desfaz por estar
/// sem conta; a partida sem servidor não rende; as missões vêm do servidor.
/// </summary>
public sealed class AccountMirrorTests : RegressionTest
{
	public override string Name => "ContaNoPerfil";
	public override string Description => "carteira, itens, patente e missões espelham a conta; sem conta é treino; o arquivo não guarda moeda nem item";

	public override Task Run(RegressionBench b)
	{
		string path = ProjectSettings.GlobalizePath(UserFiles.Profile);
		string backup = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
		const string bought = "ar15_ouro";
		try
		{
			Profile.ClearAccount();
			b.Check(!Profile.HasAccount && Profile.Presas == 0 && Profile.Xp == 0 && Profile.Rank == 0, "sem conta: zero Presas, zero XP, Filhote");
			b.Check(!Profile.Owns(bought) && Profile.Owns(Arsenal.DefaultPrimary), "sem conta só há o equipamento de série");

			// O conjunto guardado com uma arma comprada não se desfaz por estar sem conta…
			Profile.Sets[1].Primary = bought;
			Profile.Save();
			Profile.Load();
			b.Check(Profile.Sets[1].Primary == bought, $"o conjunto guarda a arma comprada mesmo sem conta ({Profile.Sets[1].Primary})");
			// …mas na partida entra a de série.
			b.Check(Profile.Usable(Profile.Sets[1]).Primary == Arsenal.DefaultPrimary, "na partida sem conta, entra a arma de série no lugar");
			b.Check(Profile.Sets[1].Primary == bought, "e o conjunto continua como estava");

			// A conta chega.
			long xp = Ranks.XpFor(2);
			var items = new[] { new OwnedItem { Id = bought, AcquiredUtc = DateTime.UtcNow } };
			Profile.ApplyAccount(xp, 4321, 7, items);
			b.Check(Profile.HasAccount && Profile.Presas == 4321 && Profile.Sangue == 7, "a carteira é a da conta");
			b.Check(Profile.Xp == xp && Profile.Rank == 2, $"a patente sai do XP da conta ({Profile.Rank})");
			b.Check(Profile.Owns(bought) && Profile.Usable(Profile.Sets[1]).Primary == bought, "com a conta, a arma comprada é dela e entra na partida");
			b.Check(Profile.Owns(Arsenal.DefaultPrimary), "o que vem com o jogo continua de todo mundo");

			// O arquivo não guarda moeda nem item.
			Profile.Save();
			string file = System.IO.File.ReadAllText(path);
			b.Check(!file.Contains("4321") && !file.Contains("\"Inventory\"") && !file.Contains("\"Presas\""), "o arquivo local não guarda Presas nem inventário");

			// Subir de patente pela conta avisa a promoção.
			var promotions = Profile.ApplyAccount(Ranks.XpFor(4), 4321, 7, items);
			b.Check(promotions.SequenceEqual(new[] { 3, 4 }), $"subiu de 2 para 4: avisa 3 e 4 ({string.Join(",", promotions)})");

			// A conta diz que o item não é mais dela (expirou, foi descartado): sai do conjunto.
			Profile.ApplyAccount(Ranks.XpFor(4), 4321, 7, Array.Empty<OwnedItem>());
			b.Check(!Profile.Owns(bought) && Profile.Sets[1].Primary == Arsenal.DefaultPrimary, $"com a conta carregada, item que não é dela sai do conjunto ({Profile.Sets[1].Primary})");

			// Missões: o espelho do servidor, e a previsão do que a partida completa.
			var reply = (JsonObject)JsonNode.Parse("""
			{
				"challenges": [
					{ "id": "d_abates", "period": "daily", "text": "Faça 10 abates", "metric": "kills", "target": 10, "presas": 300, "xp": 100, "progress": 8, "claimed": false },
					{ "id": "s_vitorias", "period": "weekly", "text": "Vença 5 partidas", "metric": "wins", "target": 5, "presas": 900, "xp": 300, "progress": 5, "claimed": false },
					{ "id": "p_cem", "period": "permanent", "text": "100 abates", "metric": "kills", "target": 100, "presas": 0, "xp": 0, "title": "Centurião", "progress": 100, "claimed": true },
					{ "id": "e_natal", "period": "event", "text": "Jogue 3 partidas", "metric": "matches", "target": 3, "sangue": 10, "progress": 0, "claimed": false, "endsAt": "2999-01-01T00:00:00Z" }
				],
				"titles": [ "Centurião" ],
				"dailyResetsAt": "2999-01-01T03:00:00Z",
				"weeklyResetsAt": "2999-01-05T03:00:00Z"
			}
			""");
			Profile.Missions.Apply(reply);
			b.Check(Profile.Missions.Daily.Count == 1 && Profile.Missions.Weekly.Count == 1 && Profile.Missions.Permanent.Count == 1 && Profile.Missions.Event.Count == 1,
				"as missões são as que o servidor mandou, cada uma no seu período");
			b.Check(Profile.Missions.ClaimableCount == 1, $"só a semanal completa e não resgatada está pronta ({Profile.Missions.ClaimableCount})");
			b.Check(MissionBook.Def("e_natal")?.Sangue == 10 && MissionBook.Def("d_abates")?.Presas == 300, "o prêmio é o do servidor");
			b.Check(Profile.Missions.UntilDailyReset() > TimeSpan.FromDays(300), "a virada do dia é a do servidor, não a do relógio do jogador");
			var predicted = Profile.Missions.Predict(new MatchResult { Kills = 3, Seconds = 300 });
			b.Check(predicted.Select(d => d.Id).OrderBy(i => i).SequenceEqual(new[] { "d_abates" }),
				$"a partida com 3 abates completaria a diária de 10 (estava em 8) ({string.Join(",", predicted.Select(d => d.Id))})");

			// Partida offline é treino: não rende nada, nem com conta.
			long presas = Profile.Presas, xpNow = Profile.Xp;
			RewardSummary offline = Progression.Apply(new MatchResult { Won = true, Kills = 12, Seconds = 600 }, online: false);
			b.Check(!offline.Earns && offline.Lines.Count == 0 && Profile.Presas == presas && Profile.Xp == xpNow, "partida sem servidor não rende XP nem Presas");

			// Online com conta: a tela mostra a previsão, mas quem paga é o servidor.
			RewardSummary online = Progression.Apply(new MatchResult { Won = true, Kills = 12, Seconds = 600 }, online: true);
			b.Check(online.Earns && online.Lines.Count > 0 && online.XpAfter > online.XpBefore, "online com conta, a tela mostra o que a partida rende");
			b.Check(Profile.Presas == presas && Profile.Xp == xpNow, "e a carteira só muda quando o servidor responde");
			b.Check(online.MissionsCompleted.Any(d => d.Id == "d_abates"), "e as missões que ela completa");

			// Sai da conta: volta ao zero.
			Profile.ClearAccount();
			b.Check(!Profile.HasAccount && Profile.Presas == 0 && Profile.Missions.All().Count() == 0 && Profile.Titles.Count == 0, "saiu da conta: carteira, missões e títulos somem");
			RewardSummary noAccount = Progression.Apply(new MatchResult { Won = true, Kills = 5 }, online: true);
			b.Check(!noAccount.Earns, "online sem conta também não rende");
		}
		finally
		{
			Profile.ClearAccount();
			if (backup != null) System.IO.File.WriteAllText(path, backup);
			else if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
			Profile.Load();
		}
		return Task.CompletedTask;
	}
}
