using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Seguranca;
using static Alcateia.Servidor.Testes.LobbyTests;

namespace Alcateia.Servidor.Testes;

/// <summary>O painel administrativo, pela API dele: entrar, papéis,
/// banir, moeda e item, mapas, promoções, desafios, manutenção,
/// blacklist de IP e auditoria.</summary>
public static class PanelTests
{
	private static string Code((int Status, JsonObject Body) reply) => reply.Body?["error"]?["code"]?.GetValue<string>();

	private static async Task<string[]> Audit(PanelClient admin) =>
		(await admin.Get("/audit")).Body["entries"].AsArray().Select(e => e["action"].GetValue<string>()).ToArray();

	public static readonly (string, Func<Task>)[] All =
	{
		("painel: entra com senha e recebe um cookie que o JavaScript não lê; erro é sempre o mesmo; cinco erros travam a conta", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);

			Check.Equal(401, (await admin.Get("/me")).Status, "sem entrar, nada");
			var wrong = await admin.Post("/login", new JsonObject { ["name"] = World.AdminName, ["password"] = "senha-errada-123" });
			var ghost = await admin.Post("/login", new JsonObject { ["name"] = "nao-existe", ["password"] = "senha-errada-123" });
			Check.Equal("login_invalido", Code(wrong), "senha errada");
			Check.Equal(wrong.Body.ToJsonString(), ghost.Body.ToJsonString(), "conta que não existe responde igualzinho");

			JsonObject me = await admin.Login();
			Check.Equal("dono", me["role"].GetValue<string>(), "o primeiro administrador é o dono");
			Cookie cookie = admin.Cookies.GetAllCookies().First(c => c.Name == "wl_painel");
			Check.True(cookie.HttpOnly, "cookie HttpOnly");
			Check.Equal(200, (await admin.Get("/me")).Status, "com o cookie, entra");
			Check.Equal(200, (await admin.Get("/overview")).Status, "e vê a visão geral");

			Check.Equal(200, (await admin.Post("/logout")).Status, "sair");
			Check.Equal(401, (await admin.Get("/me")).Status, "saiu: o cookie não vale mais");

			for (int i = 0; i < 5; i++) await admin.Post("/login", new JsonObject { ["name"] = World.AdminName, ["password"] = "errada-de-novo-" + i });
			var locked = await admin.Post("/login", new JsonObject { ["name"] = World.AdminName, ["password"] = World.AdminPassword });
			Check.Equal("login_invalido", Code(locked), "travada: nem a senha certa entra, e a resposta não diz por quê");

			await w.Services.Security.Flush();
			var events = await w.Services.Security.Events(new EventFilter(KindPrefix: "painel."));
			Check.True(events.Any(e => e.Kind == EventKind.AdminLocked) && events.Any(e => e.Kind == EventKind.AdminLoginOk), "entrada e travamento no registro de segurança");
		}),

		("painel: pedido que muda algo sem o cabeçalho do painel é recusado (formulário de outro site não passa)", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer();

			admin.SendHeader = false;
			Check.Equal("pedido_recusado", Code(await admin.Post($"/players/{p.Id}/ban", new JsonObject { ["reason"] = "teste de CSRF" })), "sem o cabeçalho");
			Check.Equal(200, (await admin.Get($"/players/{p.Id}")).Status, "ler não precisa dele");
			Check.True(!(await admin.Get($"/players/{p.Id}")).Body["player"]["banned"].GetValue<bool>(), "e o banimento não aconteceu");
		}),

		("painel: a origem vale pelo nome do servidor, não pela porta (o proxy do Fly entrega sem a 8443); outro site é recusado", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			string host = new Uri(w.AdminUrl).Host;

			// Como no Fly: o navegador em https://<nome>:8443, o servidor vendo o Host sem a porta.
			admin.Origin = $"https://{host}:8443";
			await admin.Login();
			Check.Equal(200, (await admin.Post("/logout")).Status, "a mesma máquina por outra porta entra (era \"Pedido recusado\")");

			admin.Origin = "https://site-de-fora.example";
			Check.Equal("pedido_recusado", Code(await admin.Post("/logout")), "outro site é recusado");
		}),

		("painel: verificação em duas etapas — liga com o código do aplicativo e passa a pedi-lo no login", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();

			var start = await admin.Post("/me/totp/start");
			string secret = start.Body["secret"].GetValue<string>();
			Check.True(start.Body["uri"].GetValue<string>().StartsWith("otpauth://totp/"), "o endereço do QR code");
			Check.Equal("codigo_invalido", Code(await admin.Post("/me/totp/confirm", new JsonObject { ["code"] = "000000" })), "código errado não liga");
			Check.Equal(200, (await admin.Post("/me/totp/confirm", new JsonObject { ["code"] = Totp.Code(secret, DateTime.UtcNow) })).Status, "o certo liga");

			string stored = (await w.Db.Query("select totp_secret from admins where name = $1", World.AdminName)).Text(0, "totp_secret");
			Check.True(!stored.Contains(secret), "o segredo fica cifrado no banco");

			using var again = new PanelClient(w);
			Check.Equal("codigo_necessario", Code(await again.Post("/login", new JsonObject { ["name"] = World.AdminName, ["password"] = World.AdminPassword })), "agora pede o código");
			Check.Equal("codigo_invalido", Code(await again.Post("/login", new JsonObject { ["name"] = World.AdminName, ["password"] = World.AdminPassword, ["code"] = "123456" })), "código errado");
			JsonObject me = await again.Login(code: Totp.Code(secret, DateTime.UtcNow));
			Check.True(me["totp"].GetValue<bool>(), "entrou com as duas etapas");
		}),

		("painel: com a segunda etapa obrigatória, quem não ligou só consegue ligá-la", async () =>
		{
			await using World w = await World.Start(c => c.AdminRequireTotp = true);
			using var admin = new PanelClient(w);
			JsonObject me = await admin.Login();
			Check.True(me["mustEnableTotp"].GetValue<bool>(), "o painel avisa");
			Check.Equal("ative_2fa", Code(await admin.Get("/players")), "o resto fica fechado");
			Check.Equal(200, (await admin.Post("/me/totp/start")).Status, "mas ligar a segunda etapa pode");
		}),

		("painel: cada papel faz só o que é dele; o painel nunca fica sem dono", async () =>
		{
			await using World w = await World.Start();
			using var owner = new PanelClient(w);
			await owner.Login();
			await using Player p = await w.NewPlayer();

			using PanelClient moderator = await owner.NewAdmin("moderadora", "moderador");
			using PanelClient viewer = await owner.NewAdmin("leitor1", "leitor");
			Check.Equal("senha_fraca", Code(await owner.Post("/admins", new JsonObject { ["name"] = "fraco", ["password"] = "curta", ["role"] = "admin" })), "senha curta não passa");

			Check.Equal(200, (await viewer.Get("/players")).Status, "o leitor vê");
			Check.Equal("sem_permissao", Code(await viewer.Post($"/players/{p.Id}/ban", new JsonObject { ["reason"] = "não posso" })), "mas não bane");
			Check.Equal("sem_permissao", Code(await moderator.Post($"/players/{p.Id}/currency", new JsonObject { ["currency"] = "presas", ["amount"] = 100, ["reason"] = "não posso" })), "moderador não dá moeda");
			Check.Equal("sem_permissao", Code(await moderator.Put("/maps/cidade", new JsonObject { ["enabled"] = false })), "nem mexe em mapa");
			Check.Equal("sem_permissao", Code(await moderator.Get("/admins")), "nem vê os administradores");
			Check.Equal(200, (await moderator.Post($"/players/{p.Id}/ban", new JsonObject { ["reason"] = "teste de papel", ["hours"] = 1 })).Status, "moderador bane");

			long ownerId = (await owner.Get("/me")).Body["id"].GetValue<long>();
			Check.Equal("voce_mesmo", Code(await owner.Put($"/admins/{ownerId}", new JsonObject { ["role"] = "leitor" })), "o dono não se rebaixa");

			long viewerId = (await owner.Get("/admins")).Body["admins"].AsArray().First(a => a["name"].GetValue<string>() == "leitor1")["id"].GetValue<long>();
			Check.Equal(200, (await owner.Put($"/admins/{viewerId}", new JsonObject { ["disabled"] = true })).Status, "desligar um administrador");
			Check.Equal(401, (await viewer.Get("/players")).Status, "o desligado cai na hora");
			Check.True((await Audit(owner)).Contains("admin.criou") && (await Audit(owner)).Contains("admin.alterou"), "tudo na auditoria");
		}),

		("painel: banir derruba a conexão, invalida o login e fecha a porta até desbanir; fica na auditoria", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer("Trapaceiro");
			await p.Connect();
			await p.Hear("rooms");

			var found = await admin.Get("/players?q=trapa");
			Check.Equal(p.Id, found.Body["players"][0]["id"].GetValue<long>(), "a busca acha pelo começo do nome");
			Check.True(found.Body["players"][0]["online"].GetValue<bool>(), "e mostra que está online");

			Check.Equal("sem_motivo", Code(await admin.Post($"/players/{p.Id}/ban", new JsonObject { ["hours"] = 24 })), "banir pede motivo");
			var ban = await admin.Post($"/players/{p.Id}/ban", new JsonObject { ["reason"] = "aimbot na partida 123", ["hours"] = 24 });
			Check.Equal(200, ban.Status, "banido por um dia");

			for (int i = 0; i < 100 && !p.Closed; i++)
			{
				try { await p.Hear("nada", timeoutMs: 30); }
				catch (Failed) { }
			}
			Check.True(p.Closed, "a conexão do saguão caiu");
			Check.Equal(401, (await p.Call(HttpMethod.Get, "/v1/profile")).Status, "o login dele parou de valer na hora");

			var login = await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "Trapaceiro", ["password"] = "senha-boa-123" });
			Check.Equal(403, login.Status, "e ele não entra de novo");
			Check.Equal("conta_banida", Code(login), "com o aviso de banimento");
			Check.True(login.Body["error"]["message"].GetValue<string>().Contains("aimbot"), "que diz o motivo");

			var ranking = await w.Server().Call(HttpMethod.Get, "/v1/ranking");
			Check.Equal(0, ranking.Body["ranking"].AsArray().Count, "banido sai do ranking");

			var file = await admin.Get($"/players/{p.Id}");
			Check.True(file.Body["player"]["banned"].GetValue<bool>(), "a ficha mostra o banimento");
			Check.Equal(1, file.Body["bans"].AsArray().Count, "e o histórico");
			Check.Equal(1, (await admin.Get("/security/bans")).Body["bans"].AsArray().Count, "na lista de banidos");

			Check.Equal(200, (await admin.Post($"/players/{p.Id}/unban")).Status, "desbanir");
			Check.Equal(200, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "Trapaceiro", ["password"] = "senha-boa-123" })).Status, "volta a entrar");

			await w.Db.Exec("update accounts set banned = true, banned_until = now() - interval '1 minute' where id = $1", p.Id);
			Check.Equal(200, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "Trapaceiro", ["password"] = "senha-boa-123" })).Status, "banimento vencido não segura ninguém");

			string[] audit = await Audit(admin);
			Check.True(audit.Contains("jogador.baniu") && audit.Contains("jogador.desbaniu") && audit.Contains("painel.entrou"), "auditoria: " + string.Join(", ", audit));
		}),

		("painel: dar e tirar Presas e Sangue (com extrato e motivo), dar e tirar item", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer();

			Check.Equal("sem_motivo", Code(await admin.Post($"/players/{p.Id}/currency", new JsonObject { ["currency"] = "presas", ["amount"] = 500 })), "moeda pede motivo");
			var given = await admin.Post($"/players/{p.Id}/currency", new JsonObject { ["currency"] = "presas", ["amount"] = 500, ["reason"] = "compensação da queda de ontem" });
			// A conta nova começa com 0 (AccountRules.StarterPresas, 25/11): só valem as 500 do painel.
			Check.Equal(500L, given.Body["balance"].GetValue<long>(), "0 + 500");
			await admin.Post($"/players/{p.Id}/currency", new JsonObject { ["currency"] = "sangue", ["amount"] = 120, ["reason"] = "pacote comprado" });
			Check.Equal("saldo_insuficiente", Code(await admin.Post($"/players/{p.Id}/currency", new JsonObject { ["currency"] = "presas", ["amount"] = -9000, ["reason"] = "estorno" })), "não tira mais do que tem");
			Check.Equal("moeda_invalida", Code(await admin.Post($"/players/{p.Id}/currency", new JsonObject { ["currency"] = "ouro", ["amount"] = 1, ["reason"] = "moeda que não existe" })), "moeda inventada");

			var (_, profile) = await p.Call(HttpMethod.Get, "/v1/profile");
			Check.Equal(500L, profile["presas"].GetValue<long>(), "o jogador vê as Presas");
			Check.Equal(120L, profile["sangue"].GetValue<long>(), "e o Sangue");

			Check.Equal("item_inexistente", Code(await admin.Post($"/players/{p.Id}/items", new JsonObject { ["itemId"] = "nada", ["days"] = 7 })), "item que não existe");
			Check.Equal(200, (await admin.Post($"/players/{p.Id}/items", new JsonObject { ["itemId"] = "ar15_ouro", ["days"] = 7 })).Status, "sete dias de AR-15 Ouro");
			var (_, inventory) = await p.Call(HttpMethod.Get, "/v1/inventory");
			Check.Equal("ar15_ouro", inventory["items"][0]["id"].GetValue<string>(), "chegou no inventário");
			Check.Equal("painel", inventory["items"][0]["source"].GetValue<string>(), "marcado como dado pelo painel");

			var file = await admin.Get($"/players/{p.Id}");
			Check.Equal(2, file.Body["ledger"].AsArray().Count, "o extrato tem as duas canetadas");
			Check.Equal(200, (await admin.Delete($"/players/{p.Id}/items/ar15_ouro")).Status, "tirar o item");
			Check.Equal(0, (await p.Call(HttpMethod.Get, "/v1/inventory")).Body["items"].AsArray().Count, "saiu do inventário");
			Check.True((await Audit(admin)).Contains("jogador.deu_moeda") && (await Audit(admin)).Contains("jogador.deu_item"), "na auditoria");
		}),

		("painel: mapa desligado some da rotação — sala nova não abre nele, e o jogo fica sabendo", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer();
			await p.Connect();
			await p.Hear("rooms");

			string before = (await w.Http.GetAsync(w.Url + "/v1/gamedata")).Headers.ETag.Tag;
			Check.Equal(200, (await admin.Put("/maps/armazem_estendido", new JsonObject { ["enabled"] = false })).Status, "desligar o Armazém");

			HttpResponseMessage after = await w.Http.GetAsync(w.Url + "/v1/gamedata");
			Check.True(after.Headers.ETag.Tag != before, "os dados do jogo mudaram de etiqueta");
			var data = JsonNode.Parse(await after.Content.ReadAsStringAsync());
			Check.True(!data["maps"].AsArray().First(m => m["id"].GetValue<string>() == "armazem_estendido")["enabled"].GetValue<bool>(), "o jogo vê o mapa desligado");

			await p.Say(Msg("room.create", ("settings", Settings())));
			Check.Equal("mapa_desligado", (await p.Hear("error"))["code"].GetValue<string>(), "sala no mapa desligado não abre");

			JsonObject city = Settings();
			city["map"] = "cidade";
			await p.Say(Msg("room.create", ("settings", city)));
			await p.Hear("room");

			foreach (string map in new[] { "deserto", "estacao", "obra", "neon" })
				Check.Equal(200, (await admin.Put("/maps/" + map, new JsonObject { ["enabled"] = false })).Status, "desligar " + map);
			Check.Equal("ultimo_mapa", Code(await admin.Put("/maps/cidade", new JsonObject { ["enabled"] = false })), "o último mapa ligado não desliga");
			Check.Equal(200, (await admin.Put("/maps/deserto", new JsonObject { ["enabled"] = true })).Status, "religar um mapa desligado");
			Check.Equal("equipe", string.Join(",", (await admin.Get("/content")).Body["releasedModes"].AsArray().Select(m => m.GetValue<string>())),
				"o painel sabe que só o modo Equipe existe, para não anunciar mapa em modo que o jogo não tem");
		}),

		("painel: catálogo e promoção — preço novo e desconto valem na loja na hora; Sangue não compra poder", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer();
			await w.Db.Exec("update profiles set xp = 300, presas = 3000 where account_id = $1", p.Id);

			Check.Equal(200, (await admin.Put("/catalog/ak74", new JsonObject { ["prices"] = new JsonArray(100, 500, 1500, 3300, 8000) })).Status, "baixar o preço da AK-74");
			Check.Equal("sangue_nao_compra_poder", Code(await admin.Put("/catalog/ak74", new JsonObject { ["currency"] = "sangue" })), "arma não pode custar Sangue");
			Check.Equal("raridade_invalida", Code(await admin.Put("/catalog/ak74", new JsonObject { ["rarity"] = 9 })), "raridade fora da escala");

			DateTime now = DateTime.UtcNow;
			var promo = await admin.Post("/promotions", new JsonObject
			{
				["itemId"] = "ak74", ["title"] = "Fim de semana da AK", ["percent"] = 50,
				["startsAt"] = now.AddHours(-1).ToString("o"), ["endsAt"] = now.AddDays(2).ToString("o")
			});
			Check.Equal(200, promo.Status, "promoção de 50%");
			Check.Equal("desconto_invalido", Code(await admin.Post("/promotions", new JsonObject { ["itemId"] = "ak74", ["percent"] = 99, ["startsAt"] = now.ToString("o"), ["endsAt"] = now.AddDays(1).ToString("o") })), "99% não passa");
			Check.Equal("item_inexistente", Code(await admin.Post("/promotions", new JsonObject { ["itemId"] = "ar15", ["percent"] = 10, ["startsAt"] = now.ToString("o"), ["endsAt"] = now.AddDays(1).ToString("o") })), "promoção só em item à venda");

			var bought = await p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = "ak74", ["duration"] = 1 });
			Check.Equal(250, bought.Body["price"].GetValue<int>(), "500 com 50% de desconto");

			var content = await admin.Get("/content");
			Check.Equal(50, content.Body["catalog"].AsArray().First(i => i["id"].GetValue<string>() == "ak74")["discount"].GetValue<int>(), "o painel mostra o desconto valendo");

			Check.Equal(200, (await admin.Delete($"/promotions/{promo.Body["id"].GetValue<long>()}")).Status, "tirar a promoção");
			await w.Db.Exec("delete from inventory where account_id = $1", p.Id);
			bought = await p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = "ak74", ["duration"] = 1 });
			Check.Equal(500, bought.Body["price"].GetValue<int>(), "sem promoção, o preço cheio");

			Check.Equal(200, (await admin.Put("/catalog/ak74", new JsonObject { ["enabled"] = false })).Status, "tirar a AK-74 do jogo");
			Check.Equal("item_inexistente", Code(await p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = "ak74", ["duration"] = 0 })), "desligada, não se vende");
			Check.True((await Audit(admin)).Contains("catalogo.alterou") && (await Audit(admin)).Contains("promocao.criou"), "na auditoria");
		}),

		("painel: vitrine do dia — quatro itens com 20%, os mesmos o dia inteiro, e o desconto vale na compra", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer();
			await w.Db.Exec("update profiles set xp = 900000, presas = 900000 where account_id = $1", p.Id);

			var off = JsonNode.Parse(await (await w.Http.GetAsync(w.Url + "/v1/gamedata")).Content.ReadAsStringAsync());
			Check.Equal(0, off["dailyItems"].AsArray().Count, "desligada, não há vitrine");

			Check.Equal(200, (await admin.Put("/settings/vitrine", new JsonObject { ["count"] = 4, ["percent"] = 20 })).Status, "ligar a vitrine do dia");
			Check.Equal("desconto_invalido", Code(await admin.Put("/settings/vitrine", new JsonObject { ["count"] = 4, ["percent"] = 95 })), "95% não passa");

			var data = JsonNode.Parse(await (await w.Http.GetAsync(w.Url + "/v1/gamedata")).Content.ReadAsStringAsync());
			string[] daily = data["dailyItems"].AsArray().Select(i => i.GetValue<string>()).ToArray();
			Check.Equal(4, daily.Length, "quatro itens do dia");
			Check.Equal(4, data["promotions"].AsArray().Count(x => x["title"].GetValue<string>() == "Item do dia" && x["percent"].GetValue<int>() == 20), "cada um como uma promoção de 20% que vale até o dia virar");

			JsonNode item = data["items"].AsArray().First(i => i["id"].GetValue<string>() == daily[0]);
			Check.True(!item["consumable"].GetValue<bool>() && !item["starter"].GetValue<bool>(), "só item à venda e que não é consumível");
			int duration = Enumerable.Range(0, 5).First(d => item["prices"][d].GetValue<int>() >= 0);
			int full = item["prices"][duration].GetValue<int>();
			var bought = await p.Call(HttpMethod.Post, "/v1/shop/buy", new JsonObject { ["itemId"] = daily[0], ["duration"] = duration });
			Check.Equal((int)Math.Round(full * 0.8), bought.Body["price"].GetValue<int>(), "comprado com os 20% da vitrine");

			// A regra pura: o mesmo dia de Brasília dá a mesma vitrine; outro dia, outra.
			var catalog = w.Services.GameData.Current.Catalog;
			DateTime noon = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
			string Showcase(DateTime at) => string.Join(",", Alcateia.Dominio.Loja.Shop.DailyShowcase(catalog, at).Select(x => x.ItemId));
			Check.Equal(Showcase(noon), Showcase(noon.AddHours(11).AddMinutes(59)), "a mesma até 02:59 UTC (23:59 de Brasília)");
			Check.True(Enumerable.Range(1, 5).Any(d => Showcase(noon.AddDays(d)) != Showcase(noon)), "em outros dias, outra");
			Check.Equal(new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc), Alcateia.Dominio.Loja.Shop.DailyShowcase(catalog, noon)[0].EndsAt, "vale até a meia-noite de Brasília");
			Check.Equal(4, (await admin.Get("/content")).Body["dailyItems"].AsArray().Count, "o painel mostra a vitrine de hoje");
		}),

		("painel: desafio criado no painel anda com o resultado da partida e paga uma vez só; bônus de XP vale; resultado absurdo vira alerta", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();

			DateTime now = DateTime.UtcNow;
			var made = await admin.Put("/challenges/evento_abates", new JsonObject
			{
				["period"] = "event", ["text"] = "Evento: faça 5 abates", ["metric"] = "kills", ["target"] = 5, ["presas"] = 300, ["xp"] = 50,
				["sangue"] = 10, ["itemId"] = "ak74", ["itemDays"] = 3, ["startsAt"] = now.AddHours(-1).ToString("o"), ["endsAt"] = now.AddDays(1).ToString("o")
			});
			Check.Equal(200, made.Status, "criar o desafio de evento");
			Check.Equal("metrica_invalida", Code(await admin.Put("/challenges/ruim", new JsonObject { ["period"] = "daily", ["text"] = "x", ["metric"] = "voar", ["target"] = 1 })), "métrica inventada");
			Check.Equal(200, (await admin.Put("/settings/bonus", new JsonObject { ["xp"] = 2, ["presas"] = 1 })).Status, "XP em dobro");
			Check.Equal(200, (await admin.Put("/settings/desafios", new JsonObject { ["daily"] = 0, ["weekly"] = 0 })).Status, "sem rodízio: todos os desafios valem");
			Check.Equal("quantidade_invalida", Code(await admin.Put("/settings/desafios", new JsonObject { ["daily"] = 999 })), "quantidade absurda não passa");
			Check.Equal("bonus_invalido", Code(await admin.Put("/settings/bonus", new JsonObject { ["xp"] = 50 })), "bônus absurdo não passa");

			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", MatchTests.Beat("gs-1"), w.Config.FleetKey);
			var (host, key) = await MatchTests.Solo(w);
			await using Player h = host;
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", MatchTests.Beat("gs-1"), w.Config.FleetKey);

			Check.Equal("premio_indisponivel", Code(await host.Call(HttpMethod.Post, "/v1/challenges/evento_abates/claim")), "sem jogar não recebe");

			var result = new JsonObject
			{
				["matchKey"] = key, ["serverId"] = "gs-1", ["scoreBlue"] = 40, ["scoreOrange"] = 10,
				["players"] = new JsonArray(new JsonObject { ["accountId"] = host.Id, ["kills"] = 100, ["killsWolf"] = 30, ["deaths"] = 0, ["assists"] = 2, ["headshots"] = 99, ["points"] = 900, ["rejectedHits"] = 40 })
			};
			Check.Equal(200, (await fleet.Call(HttpMethod.Post, "/v1/matches", result, w.Config.FleetKey)).Status, "o resultado");

			Rewards.Gain gain = Rewards.ForMatch(100, 2, won: true, draw: false, mvp: true);
			var (_, profile) = await host.Call(HttpMethod.Get, "/v1/profile");
			Check.Equal(gain.Xp * 2, profile["xp"].GetValue<long>(), "XP em dobro pelo bônus do painel");
			long promoted = Alcateia.Dominio.Contas.RankRule.PromotionPresas(0, gain.Xp * 2);
			Check.True(promoted > 0, "esse XP todo sobe de patente");
			Check.Equal(gain.Presas + promoted, profile["presas"].GetValue<long>(), "Presas sem bônus, mais o prêmio de cada patente alcançada");

			var list = (await host.Call(HttpMethod.Get, "/v1/challenges")).Body["challenges"].AsArray();
			JsonNode Of(string id) => list.First(c => c["id"].GetValue<string>() == id);
			Check.Equal(5, Of("evento_abates")["progress"].GetValue<int>(), "o desafio do evento completou (para no alvo)");
			Check.True(Of("evento_abates")["done"].GetValue<bool>(), "e está pronto para receber");
			Check.Equal(10, Of("d_lobo")["progress"].GetValue<int>(), "abates como lobo contam no desafio de lobo");
			Check.Equal(15, Of("d_humano")["progress"].GetValue<int>(), "e os outros, no de humano");
			Check.Equal(1, Of("d_partidas")["progress"].GetValue<int>(), "uma partida");
			Check.True(!Of("d_assist")["done"].GetValue<bool>(), "duas assistências não completam cinco");

			var claimed = await host.Call(HttpMethod.Post, "/v1/challenges/evento_abates/claim");
			Check.Equal(200, claimed.Status, "receber o prêmio");
			long promotedByClaim = Alcateia.Dominio.Contas.RankRule.PromotionPresas(gain.Xp * 2, gain.Xp * 2 + 50);
			Check.Equal(gain.Presas + promoted + promotedByClaim + 300, claimed.Body["balance"]["presas"].GetValue<long>(), "300 Presas");
			Check.Equal(10L, claimed.Body["balance"]["sangue"].GetValue<long>(), "10 de Sangue");
			Check.Equal(gain.Xp * 2 + 50, claimed.Body["balance"]["xp"].GetValue<long>(), "50 de XP");
			Check.Equal("premio_indisponivel", Code(await host.Call(HttpMethod.Post, "/v1/challenges/evento_abates/claim")), "uma vez só");
			Check.Equal("premio_indisponivel", Code(await host.Call(HttpMethod.Post, "/v1/challenges/d_assist/claim")), "incompleto não paga");
			Check.True((await host.Call(HttpMethod.Get, "/v1/inventory")).Body["items"].AsArray().Any(i => i["id"].GetValue<string>() == "ak74"), "e o item do prêmio");

			await w.Services.Security.Flush();
			var file = await admin.Get($"/players/{host.Id}");
			Check.True(file.Body["events"].AsArray().Any(e => e["kind"].GetValue<string>() == EventKind.Cheat), "100 abates em segundos, quase todos na cabeça: alerta na ficha");
			Check.True(file.Body["events"].AsArray().Any(e => e.ToJsonString().Contains("acertos recusados")),
				"40 acertos recusados pelo servidor de partida: alerta na ficha (decisão 0016)");

			Check.Equal(200, (await admin.Delete("/challenges/evento_abates")).Status, "remover o desafio");
			Check.True((await host.Call(HttpMethod.Get, "/v1/challenges")).Body["challenges"].AsArray().All(c => c["id"].GetValue<string>() != "evento_abates"), "some para o jogador");
		}),

		("painel: manutenção fecha a entrada (e reabre); configuração desconhecida não existe", async () =>
		{
			await using World w = await World.Start();
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer("Antes");

			Check.Equal(200, (await admin.Put("/settings/manutencao", new JsonObject { ["on"] = true, ["message"] = "Voltamos às 18h." })).Status, "ligar a manutenção");
			var login = await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "Antes", ["password"] = "senha-boa-123" });
			Check.Equal(503, login.Status, "ninguém entra");
			Check.Equal("Voltamos às 18h.", login.Body["error"]["message"].GetValue<string>(), "com o recado do painel");
			Check.Equal(503, (await w.Server().Call(HttpMethod.Post, "/v1/accounts", new JsonObject { ["name"] = "Depois", ["password"] = "senha-boa-123" })).Status, "nem cria conta");
			Check.Equal(200, (await admin.Get("/overview")).Status, "o painel continua de pé");

			await admin.Put("/settings/manutencao", new JsonObject { ["on"] = false });
			Check.Equal(200, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = "Antes", ["password"] = "senha-boa-123" })).Status, "reaberto");
			Check.Equal(404, (await admin.Put("/settings/qualquer_coisa", new JsonObject { ["x"] = 1 })).Status, "configuração inventada");
			Check.Equal("versao_invalida", Code(await admin.Put("/settings/versao_minima", new JsonObject { ["version"] = "abc" })), "versão mal escrita");
		}),

		("painel: blacklist de IP pela tela, eventos, visão geral, frota e métricas", async () =>
		{
			await using World w = await World.Start(c => c.MetricsKey = "chave-das-metricas");
			using var admin = new PanelClient(w);
			await admin.Login();
			await using Player p = await w.NewPlayer();

			Check.Equal("ip_invalido", Code(await admin.Post("/security/blocks", new JsonObject { ["cidr"] = "não é ip", ["reason"] = "x" })), "endereço inválido");
			Check.Equal("sem_motivo", Code(await admin.Post("/security/blocks", new JsonObject { ["cidr"] = "198.51.100.0/24" })), "bloqueio pede motivo");
			var block = await admin.Post("/security/blocks", new JsonObject { ["cidr"] = "198.51.100.0/24", ["reason"] = "ataque de ontem", ["hours"] = 48 });
			Check.Equal(200, block.Status, "bloquear a faixa");
			Check.True(w.Services.Security.IsBlocked(IPAddress.Parse("198.51.100.9")), "vale na hora, em memória");
			Check.Equal(1, (await admin.Get("/security/blocks")).Body["blocks"].AsArray().Count, "aparece na lista");
			Check.Equal(200, (await admin.Delete($"/security/blocks/{block.Body["id"].GetValue<long>()}")).Status, "desbloquear");
			Check.True(!w.Services.Security.IsBlocked(IPAddress.Parse("198.51.100.9")), "saiu");

			await w.Server().Call(HttpMethod.Post, "/v1/sessions", new JsonObject { ["name"] = p.Name, ["password"] = "errada-errada" });
			await w.Services.Security.Flush();
			var events = await admin.Get("/security/events?kind=login.");
			Check.True(events.Body["events"].AsArray().Any(e => e["kind"].GetValue<string>() == EventKind.LoginFailed), "o login errado aparece nos eventos");
			Check.True(events.Body["events"].AsArray().All(e => e["kind"].GetValue<string>().StartsWith("login.")), "o filtro por tipo filtra");

			var overview = await admin.Get("/overview");
			Check.Equal(1L, overview.Body["accounts"].GetValue<long>(), "uma conta");
			Check.Equal(14, overview.Body["days"].AsArray().Count, "catorze dias no gráfico");
			Check.True(overview.Body["security"]["byKind"].AsArray().Count > 0, "o resumo de segurança");

			await w.Server().Call(HttpMethod.Post, "/v1/fleet/heartbeat", MatchTests.Beat("gs-1"), w.Config.FleetKey);
			Check.Equal("gs-1", (await admin.Get("/fleet")).Body["servers"][0]["id"].GetValue<string>(), "a frota aparece");

			var noKey = await w.Http.GetAsync(w.AdminUrl + "/metrics");
			Check.Equal(401, (int)noKey.StatusCode, "métricas pedem a chave");
			var request = new HttpRequestMessage(HttpMethod.Get, w.AdminUrl + "/metrics");
			request.Headers.Add("Authorization", "Bearer chave-das-metricas");
			string metrics = await (await w.Http.SendAsync(request)).Content.ReadAsStringAsync();
			Check.True(metrics.Contains("wolfleague_online 0") && metrics.Contains("wolfleague_fleet_servers 1"), "no formato do Prometheus");

			string[] audit = await Audit(admin);
			Check.True(audit.Contains("ip.bloqueou") && audit.Contains("ip.desbloqueou"), "bloqueio e desbloqueio na auditoria");
			Check.Equal(1, (await admin.Get("/audit?q=198.51")).Body["entries"].AsArray().Count(e => e["action"].GetValue<string>() == "ip.bloqueou"), "a busca da auditoria acha pelo alvo");
		}),

		("painel: só entra quem está na lista de endereços do painel", async () =>
		{
			await using World w = await World.Start(c => c.AdminAllow.Add(IpRange.Parse("10.99.0.0/16")));
			using var admin = new PanelClient(w);
			Check.Equal(404, (await admin.Post("/login", new JsonObject { ["name"] = World.AdminName, ["password"] = World.AdminPassword })).Status,
				"de fora da lista o painel nem aparece, mesmo com a senha certa");
			await w.Services.Security.Flush();
			Check.True((await w.Services.Security.Events(new EventFilter(KindPrefix: "painel."))).Any(e => e.Kind == EventKind.AdminDenied), "e a tentativa fica registrada");
		})
	};
}
