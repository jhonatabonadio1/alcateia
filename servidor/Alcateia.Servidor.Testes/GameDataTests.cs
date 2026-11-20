using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Alcateia.Servidor.Testes;

/// <summary>O que do jogo mora no servidor: os dados (com a semente que
/// vem do jogo), o inventário e a loja.</summary>
public static class GameDataTests
{
	private static Task<(int Status, JsonObject Body)> Buy(Player p, string item, int duration) =>
		p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = item, ["duration"] = duration });

	private static string Code((int Status, JsonObject Body) reply) => reply.Body?["error"]?["code"]?.GetValue<string>();

	private static double DaysFromNow(JsonNode date) => (DateTime.Parse(date.GetValue<string>()).ToUniversalTime() - DateTime.UtcNow).TotalDays;

	public static readonly (string, Func<Task>)[] All =
	{
		("dados do jogo: o servidor sobe com o catálogo, os mapas e os desafios do jogo; sem mudança, responde 304", async () =>
		{
			await using World w = await World.Start();
			HttpResponseMessage first = await w.Http.GetAsync(w.Url + "/v1/gamedata");
			var data = (JsonObject)JsonNode.Parse(await first.Content.ReadAsStringAsync());

			JsonNode ak = data["items"].AsArray().First(i => i["id"].GetValue<string>() == "ak74");
			Check.Equal(1, ak["minRank"].GetValue<int>(), "a AK-74 pede patente 1");
			Check.Equal(15500, ak["prices"][4].GetValue<int>(), "e custa 15 500 para sempre");
			Check.True(data["items"].AsArray().Any(i => i["id"].GetValue<string>() == "pers_eve"), "os personagens estão no catálogo");
			Check.True(data["maps"].AsArray().First(m => m["id"].GetValue<string>() == "armazem_estendido")["enabled"].GetValue<bool>(), "o Armazém está ligado");
			Check.True(!data["maps"].AsArray().First(m => m["id"].GetValue<string>() == "polar")["enabled"].GetValue<bool>(), "os mapas guardados vêm desligados");
			Check.True(data["challenges"].AsArray().Count >= 20, "as missões do jogo viram desafios");
			Check.True(!data["settings"]["manutencao"]["on"].GetValue<bool>(), "sem manutenção");

			// O que é do jogo acompanha o jogo: um mapa que passou a ser só de
			// um modo que ainda não existe muda no banco na subida seguinte,
			// sem perder o que o painel editou (aqui, o nome e o "desligado").
			await w.Db.Exec("update game_maps set name = 'Obra do Jhon', enabled = false where id = 'obra'");
			var seed = Alcateia.Infra.Semente.SeedData.Load();
			var changed = seed.Maps.Select(m => m.Id == "obra" ? m with { Modes = new[] { "mata_mata" }, Name = "Outro nome" } : m).ToList();
			await w.Services.GameData.Seed(seed.Catalog, changed, seed.Challenges, seed.ReleasedModes);
			var obra = w.Services.GameData.Current.Maps.First(m => m.Id == "obra");
			Check.Equal("mata_mata", string.Join(",", obra.Modes), "os modos do mapa acompanham o jogo");
			Check.Equal("Obra do Jhon", obra.Name, "o nome que o painel deu fica");
			Check.True(!obra.Enabled, "e o desligado também");
			Check.Equal("equipe", string.Join(",", w.Services.GameData.ReleasedModes), "só o modo Equipe está liberado");

			first = await w.Http.GetAsync(w.Url + "/v1/gamedata");
			string etag = first.Headers.ETag.Tag;
			var again = new HttpRequestMessage(HttpMethod.Get, w.Url + "/v1/gamedata");
			again.Headers.TryAddWithoutValidation("If-None-Match", etag);
			HttpResponseMessage second = await w.Http.SendAsync(again);
			Check.Equal(304, (int)second.StatusCode, "nada mudou: 304, sem corpo");
			Check.Equal(0, (await second.Content.ReadAsByteArrayAsync()).Length, "sem corpo mesmo");
		}),

		("loja: o servidor confere patente, preço e saldo; tempo soma; permanente não se compra de novo; consumível soma usos", async () =>
		{
			await using World w = await World.Start();
			await using Player p = await w.NewPlayer();

			Check.Equal("patente_insuficiente", Code(await Buy(p, "ak74", 0)), "patente 0 não compra a AK-74");
			Check.Equal("item_de_serie", Code(await Buy(p, "ar15", 4)), "item que vem com a conta não se compra");
			Check.Equal("item_inexistente", Code(await Buy(p, "arma_que_nao_existe", 0)), "item inventado");
			Check.Equal(401, (await w.Server().Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = "ak74" })).Status, "sem login não compra");

			await w.Db.Exec("update profiles set xp = 300 where account_id = $1", p.Id);
			Check.Equal("duracao_indisponivel", Code(await Buy(p, "ak74", 9)), "duração inventada");
			Check.Equal("patente_insuficiente", Code(await Buy(p, "scar", 0)), "a SCAR pede patente 3");

			var day = await Buy(p, "ak74", 0);
			Check.Equal(200, day.Status, "um dia de AK-74");
			Check.Equal(200, day.Body["price"].GetValue<int>(), "pelo preço do servidor");
			Check.Equal(2800L, day.Body["balance"].GetValue<long>(), "descontado das 3 000 iniciais");
			Check.True(Math.Abs(DaysFromNow(day.Body["expiresAt"]) - 1) < 0.01, "vale um dia");

			var week = await Buy(p, "ak74", 1);
			Check.Equal(1800L, week.Body["balance"].GetValue<long>(), "mais sete dias por 1 000");
			Check.True(Math.Abs(DaysFromNow(week.Body["expiresAt"]) - 8) < 0.01, "o tempo soma ao que restava");

			Check.Equal("saldo_insuficiente", Code(await Buy(p, "ak74", 4)), "sem saldo para o permanente");
			var (_, wallet) = await p.Call(HttpMethod.Get, "/v1/inventory");
			Check.Equal(1800L, wallet["presas"].GetValue<long>(), "compra recusada não cobra");

			await w.Db.Exec("update profiles set presas = 20000 where account_id = $1", p.Id);
			var forever = await Buy(p, "ak74", 4);
			Check.True(forever.Body["expiresAt"] == null, "permanente não vence");
			Check.Equal(4500L, forever.Body["balance"].GetValue<long>(), "20 000 − 15 500");
			Check.Equal("ja_tem", Code(await Buy(p, "ak74", 1)), "quem tem para sempre não compra de novo");

			await Buy(p, "troca_nome", 4);
			await Buy(p, "troca_nome", 4);
			var (_, inventory) = await p.Call(HttpMethod.Get, "/v1/inventory");
			Check.Equal(1500L, inventory["presas"].GetValue<long>(), "duas trocas de nome a 1 500");
			Check.Equal(1, inventory["rank"].GetValue<int>(), "a patente vem junto");
			JsonNode name = inventory["items"].AsArray().First(i => i["id"].GetValue<string>() == "troca_nome");
			Check.Equal(2, name["uses"].GetValue<int>(), "consumível soma usos");
			Check.Equal(2, inventory["items"].AsArray().Count, "a AK-74 e a troca de nome");

			long[] ledger = (await w.Db.Query("select amount from ledger where account_id = $1 order by id", p.Id)).Rows.Select(r => long.Parse(r[0])).ToArray();
			Check.Equal("-200,-1000,-15500,-1500,-1500", string.Join(",", ledger), "cada compra no extrato");
		}),

		("desafios: três diários e cinco semanais por vez, os mesmos para todos; a virada é à meia-noite de Brasília", async () =>
		{
			await using World w = await World.Start();
			await using Player a = await w.NewPlayer("Ana");
			await using Player b = await w.NewPlayer("Beto");

			var (_, mine) = await a.Call(HttpMethod.Get, "/v1/challenges");
			var (_, theirs) = await b.Call(HttpMethod.Get, "/v1/challenges");
			string[] Ids(JsonObject body, string period) => body["challenges"].AsArray().Where(c => c["period"].GetValue<string>() == period).Select(c => c["id"].GetValue<string>()).ToArray();
			Check.Equal(3, Ids(mine, "daily").Length, "três diários");
			Check.Equal(5, Ids(mine, "weekly").Length, "cinco semanais");
			Check.True(Ids(mine, "permanent").Length >= 5, "os permanentes valem todos");
			Check.Equal(string.Join(",", Ids(mine, "daily")), string.Join(",", Ids(theirs, "daily")), "os diários de hoje são os mesmos para todo mundo");

			// A regra pura: mesma data, mesmo sorteio; outro dia, outro sorteio; a meia-noite é a de Brasília.
			var all = w.Services.GameData.Current.Challenges;
			DateTime noon = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
			string Daily(DateTime at) => string.Join(",", Alcateia.Dominio.Desafios.ChallengeRules.Running(all, at).Where(c => c.Period == "daily").Select(c => c.Id));
			Check.Equal(Daily(noon), Daily(noon.AddHours(5)), "no mesmo dia, os mesmos");
			Check.True(Enumerable.Range(1, 6).Any(d => Daily(noon.AddDays(d)) != Daily(noon)), "em outros dias, outros");
			Check.Equal(Daily(new DateTime(2026, 11, 21, 2, 59, 0, DateTimeKind.Utc)), Daily(noon), "02:59 UTC ainda é dia 20 em Brasília");
			Check.Equal(new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc), Alcateia.Dominio.Desafios.ChallengeRules.NextDailyReset(noon), "os diários viram às 03:00 UTC");
			Check.Equal(new DateTime(2026, 11, 23, 3, 0, 0, DateTimeKind.Utc), Alcateia.Dominio.Desafios.ChallengeRules.NextWeeklyReset(noon), "os semanais, na segunda-feira");
			Check.True(mine["dailyResetsAt"] != null && mine["weeklyResetsAt"] != null, "o jogo recebe as duas viradas");
			Check.Equal(0, mine["titles"].AsArray().Count, "conta nova não tem título");
		}),

		("conta: subir de patente paga Presas; desafio permanente dá título; descartar item; trocar de nome gasta o item", async () =>
		{
			await using World w = await World.Start();
			await using Player p = await w.NewPlayer("Velho_Nome");
			await using Player other = await w.NewPlayer("Ocupado");

			// Um desafio permanente já completo: receber o prêmio dá XP (sobe de patente) e o título.
			await w.Db.Exec("update profiles set xp = 225 where account_id = $1", p.Id);
			await w.Db.Exec("insert into challenge_progress (account_id, challenge_id, period_key, progress) values ($1, 'p_partidas', 'sempre', 100)", p.Id);
			var def = w.Services.GameData.Current.Challenges.First(c => c.Id == "p_partidas");
			var claimed = await p.Call(HttpMethod.Post, "/v1/challenges/p_partidas/claim");
			Check.Equal(200, claimed.Status, "receber o prêmio do desafio permanente");
			Check.Equal(1, claimed.Body["balance"]["rank"].GetValue<int>(), "o XP do prêmio levou à patente 1");
			Check.Equal(3000L + def.Presas + 150, claimed.Body["balance"]["presas"].GetValue<long>(), "as Presas do desafio e mais 150 pela promoção");
			var (_, challenges) = await p.Call(HttpMethod.Get, "/v1/challenges");
			Check.Equal(def.Title, challenges["titles"][0].GetValue<string>(), "e o título do desafio");
			string[] reasons = (await w.Db.Query("select reason from ledger where account_id = $1 order by id", p.Id)).Rows.Select(r => r[0]).ToArray();
			Check.True(reasons.Contains("promocao") && reasons.Contains("desafio"), "promoção e desafio no extrato: " + string.Join(",", reasons));

			// Descartar.
			await p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = "ak74", ["duration"] = 0 });
			Check.Equal(200, (await p.Call(HttpMethod.Delete, "/v1/inventory/ak74")).Status, "jogar a AK-74 fora");
			Check.Equal(404, (await p.Call(HttpMethod.Delete, "/v1/inventory/ak74")).Status, "o que não se tem não se descarta");
			Check.Equal(0, (await p.Call(HttpMethod.Get, "/v1/inventory")).Body["items"].AsArray().Count, "saiu do inventário");

			// Trocar de nome.
			Check.Equal("sem_troca_de_nome", Code(await p.Call(HttpMethod.Post, "/v1/account/name", new JsonObject { ["name"] = "Novo_Nome" })), "sem o item não troca");
			await p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = "troca_nome", ["duration"] = 4 });
			Check.Equal("nome_em_uso", Code(await p.Call(HttpMethod.Post, "/v1/account/name", new JsonObject { ["name"] = "ocupado" })), "nome de outra conta não");
			Check.Equal("nome_invalido", Code(await p.Call(HttpMethod.Post, "/v1/account/name", new JsonObject { ["name"] = "a b" })), "nome inválido não");
			Check.Equal(1, (await p.Call(HttpMethod.Get, "/v1/inventory")).Body["items"][0]["uses"].GetValue<int>(), "troca recusada não gasta o item");
			var renamed = await p.Call(HttpMethod.Post, "/v1/account/name", new JsonObject { ["name"] = "Novo_Nome" });
			Check.Equal("Novo_Nome", renamed.Body["name"].GetValue<string>(), "nome trocado");
			Check.Equal(0, (await p.Call(HttpMethod.Get, "/v1/inventory")).Body["items"].AsArray().Count, "o item foi gasto");
			Check.Equal("Novo_Nome", (await p.Call(HttpMethod.Get, "/v1/profile")).Body["name"].GetValue<string>(), "o perfil já diz o nome novo");
			Check.Equal(200, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "novo_nome", ["password"] = "senha-boa-123" })).Status, "entra com o nome novo");
			Check.Equal(401, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "Velho_Nome", ["password"] = "senha-boa-123" })).Status, "o antigo não existe mais");
		}),

		("loja: várias compras ao mesmo tempo não gastam o mesmo dinheiro", async () =>
		{
			await using World w = await World.Start();
			await using Player p = await w.NewPlayer();
			await w.Db.Exec("update profiles set xp = 300, presas = 200 where account_id = $1", p.Id);

			var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Buy(p, "ak74", 0)));
			Check.Equal(1, replies.Count(r => r.Status == 200), "com dinheiro para uma, só uma compra passa");
			Check.Equal(7, replies.Count(r => Code(r) == "saldo_insuficiente"), "as outras são recusadas por saldo");
			var (_, wallet) = await p.Call(HttpMethod.Get, "/v1/inventory");
			Check.Equal(0L, wallet["presas"].GetValue<long>(), "o saldo zerou, e não ficou negativo");
		})
	};
}
