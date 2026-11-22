using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Seguranca;
using Alcateia.Infra.Seguranca;
using static Alcateia.Servidor.Testes.LobbyTests;

namespace Alcateia.Servidor.Testes;

/// <summary>Do "começar" na sala ao prêmio no perfil: a frota recebe a
/// partida, os jogadores recebem o ingresso, o resultado paga.</summary>
public static class MatchTests
{
	public static JsonObject Beat(string id, int port = 7777) =>
		new() { ["id"] = id, ["address"] = "10.0.0.5", ["port"] = port, ["players"] = 0 };

	/// <summary>A batida de um servidor de partida de agora: quantas partidas
	/// aguenta e quais tem abertas.</summary>
	private static JsonObject BeatWith(string id, int capacity, params string[] open)
	{
		JsonObject beat = Beat(id);
		beat["capacity"] = capacity;
		var list = new JsonArray();
		foreach (string key in open) list.Add(key);
		beat["matches"] = list;
		return beat;
	}

	/// <summary>Uma sala com um jogador só, que começa a partida. Devolve a chave.</summary>
	public static async Task<(Player Host, string Key)> Solo(World w)
	{
		Player host = await w.NewPlayer();
		await host.Connect();
		await host.Hear("rooms");
		await host.Say(Msg("room.create", ("settings", Settings())));
		await host.Hear("room");
		await host.Say(Msg("room.start"));
		JsonObject start = await host.Hear("match.start");
		return (host, start["matchKey"].GetValue<string>());
	}

	private static async Task<(Player Host, Player Guest, int Room)> Room(World w)
	{
		Player host = await w.NewPlayer(), guest = await w.NewPlayer();
		foreach (Player p in new[] { host, guest }) { await p.Connect(); await p.Hear("rooms"); }
		await host.Say(Msg("room.create", ("settings", Settings())));
		int id = (await host.Hear("room"))["id"].GetValue<int>();
		await guest.Say(Msg("room.join", ("id", id)));
		await guest.Hear("room");
		return (host, guest, id);
	}

	public static readonly (string, Func<Task>)[] All =
	{
		("partida: sem todos prontos não começa; sem servidor de partida livre, avisa", async () =>
		{
			await using World w = await World.Start();
			var (host, guest, _) = await Room(w);
			await using Player h = host, g = guest;

			await guest.Say(Msg("room.start"));
			Check.Equal("nao_e_dono", (await guest.Hear("error"))["code"].GetValue<string>(), "convidado não começa");

			await host.Say(Msg("room.start"));
			Check.Equal("nem_todos_prontos", (await host.Hear("error"))["code"].GetValue<string>(), "convidado ainda não está pronto");

			await guest.Say(Msg("room.ready", ("ready", true)));
			await host.Hear("room", m => m["members"][1]["ready"].GetValue<bool>());
			await host.Say(Msg("room.start"));
			Check.Equal("sem_servidor", (await host.Hear("error"))["code"].GetValue<string>(), "frota vazia");
		}),

		("frota: batida sem a chave é recusada", async () =>
		{
			await using World w = await World.Start();
			Check.Equal(401, (await w.Server().Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"))).Status, "sem chave");
			Check.Equal(401, (await w.Server().Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), "chave-errada")).Status, "chave errada");
			Check.Equal(200, (await w.Server().Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey)).Status, "chave certa");
		}),

		("frota sob demanda: o servidor ocioso só desliga se não faz falta", async () =>
		{
			await using World w = await World.Start(c => c.FleetReserve = 1);
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-2", 7778), w.Config.FleetKey);

			Check.Equal(401, (await fleet.Call(HttpMethod.Post, "/v1/fleet/leave", new JsonObject { ["id"] = "gs-1" })).Status, "sem chave");
			var (_, first) = await fleet.Call(HttpMethod.Post, "/v1/fleet/leave", new JsonObject { ["id"] = "gs-1" }, w.Config.FleetKey);
			Check.True(first["leave"].GetValue<bool>(), "sobra o gs-2 de reserva: o gs-1 pode desligar");
			await w.Services.Security.Flush();
			var stopped = await w.Services.Security.Events(new EventFilter(KindPrefix: EventKind.MatchServerStopped));
			Check.Equal(1, stopped.Count, "o painel fica sabendo que o servidor desligou");
			Check.Equal("gs-1", stopped[0].Actor, "qual servidor");
			var (_, last) = await fleet.Call(HttpMethod.Post, "/v1/fleet/leave", new JsonObject { ["id"] = "gs-2" }, w.Config.FleetKey);
			Check.True(!last["leave"].GetValue<bool>(), "o último lugar livre fica de pé");

			// O que saiu não recebe partida: a próxima sala cai no gs-2.
			await using Player host = await w.NewPlayer();
			await host.Connect();
			await host.Hear("rooms");
			await host.Say(Msg("room.create", ("settings", Settings(play: LobbyTests.VsBots))));
			await host.Hear("room");
			await host.Say(Msg("room.bot.add", ("team", 2)));
			await host.Hear("room", m => m["members"].AsArray().Count == 2);
			await host.Say(Msg("room.start"));
			Check.Equal(7778, (await host.Hear("match.start"))["port"].GetValue<int>(), "a partida vai para o que ficou");

			var (_, busy) = await fleet.Call(HttpMethod.Post, "/v1/fleet/leave", new JsonObject { ["id"] = "gs-2" }, w.Config.FleetKey);
			Check.True(!busy["leave"].GetValue<bool>(), "com partida, não desliga");
		}),

		("partida com bots: a escalação leva os bots com o dono; o resultado não paga nem grava bot", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);

			await using Player host = await w.NewPlayer();
			await host.Connect();
			await host.Hear("rooms");
			await host.Say(Msg("room.create", ("settings", Settings(play: LobbyTests.VsBots))));
			await host.Hear("room");
			await host.Say(Msg("room.bot.add", ("team", 2)));
			await host.Say(Msg("room.bot.add", ("team", 2)));
			await host.Hear("room", m => m["members"].AsArray().Count == 3);

			// Sozinho com bots, o dono começa: bot está sempre pronto.
			await host.Say(Msg("room.start"));
			string key = (await host.Hear("match.start"))["matchKey"].GetValue<string>();

			var (_, beat) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);
			JsonArray players = beat["assignment"]["players"].AsArray();
			Check.Equal(3, players.Count, "o dono e os dois bots");
			JsonNode bot = players.First(p => p["accountId"].GetValue<long>() < 0);
			Check.Equal(host.Id, bot["owner"].GetValue<long>(), "o bot vai com o dono da sala como dono");
			Check.Equal(0L, players.First(p => p["accountId"].GetValue<long>() > 0)["owner"].GetValue<long>(), "gente não tem dono");

			// O servidor de partida manda (por engano ou má-fé) uma linha de bot.
			var (status, _) = await fleet.Call(HttpMethod.Post, "/v1/matches", new JsonObject
			{
				["matchKey"] = key, ["serverId"] = "gs-1", ["scoreBlue"] = 3, ["scoreOrange"] = 1,
				["players"] = new JsonArray(
					new JsonObject { ["accountId"] = host.Id, ["kills"] = 3, ["deaths"] = 1, ["assists"] = 0, ["headshots"] = 1, ["points"] = 30 },
					new JsonObject { ["accountId"] = bot["accountId"].GetValue<long>(), ["kills"] = 1, ["deaths"] = 3, ["assists"] = 0, ["headshots"] = 0, ["points"] = 10 })
			}, w.Config.FleetKey);
			Check.Equal(200, status, "resultado aceito");

			JsonObject end = await host.Hear("match.end");
			Check.True(end["you"]["xp"].GetValue<long>() > 0, "o dono ganhou XP");
			var (_, matches) = await host.Call(HttpMethod.Get, "/v1/matches");
			Check.Equal(1, matches["matches"].AsArray().Count, "a partida está na conta do dono");
			Check.True(!matches.ToJsonString().Contains(bot["name"].GetValue<string>()), "e o bot não aparece nela");
		}),

		("partida inteira: começa, cada um recebe o ingresso, o servidor de partida recebe a escalação, o resultado paga", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);

			var (host, guest, _) = await Room(w);
			await using Player h = host, g = guest;
			await guest.Say(Msg("room.ready", ("ready", true)));
			await host.Hear("room", m => m["members"][1]["ready"].GetValue<bool>());
			await host.Say(Msg("room.start"));

			JsonObject startHost = await host.Hear("match.start"), startGuest = await guest.Hear("match.start");
			string key = startHost["matchKey"].GetValue<string>();
			Check.Equal("10.0.0.5", startHost["address"].GetValue<string>(), "endereço do servidor de partida");
			Check.Equal(7777, startHost["port"].GetValue<int>(), "porta");
			Check.True(startHost["ticket"].GetValue<string>() != startGuest["ticket"].GetValue<string>(), "cada jogador tem o seu ingresso");

			// O servidor de partida confere o ingresso sozinho, com a chave da frota.
			string expected = Passwords.Sign(w.Config.FleetKey, $"{key}|{guest.Id}|{startGuest["expires"].GetValue<long>()}");
			Check.Equal(expected, startGuest["ticket"].GetValue<string>(), "o ingresso é a assinatura de partida|conta|validade");

			var (_, beat) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);
			Check.Equal(key, beat["assignment"]["matchKey"].GetValue<string>(), "a batida seguinte traz a partida");
			Check.Equal(2, beat["assignment"]["players"].AsArray().Count, "com os dois jogadores");
			Check.Equal("armazem_estendido", beat["assignment"]["settings"]["map"].GetValue<string>(), "e as regras da sala");
			var (_, again) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);
			Check.True(again["assignment"] == null, "a partida é entregue uma vez só");

			// O azul (o dono) ganha de 40 a 31; o convidado faz mais pontos e leva o MVP.
			var result = new JsonObject
			{
				["matchKey"] = key, ["serverId"] = "gs-1", ["scoreBlue"] = 40, ["scoreOrange"] = 31,
				["players"] = new JsonArray(
					new JsonObject { ["accountId"] = host.Id, ["kills"] = 10, ["deaths"] = 4, ["assists"] = 2, ["headshots"] = 3, ["points"] = 1200 },
					new JsonObject { ["accountId"] = guest.Id, ["kills"] = 14, ["deaths"] = 6, ["assists"] = 1, ["headshots"] = 5, ["points"] = 1500 },
					new JsonObject { ["accountId"] = 999999, ["kills"] = 99, ["points"] = 99999 })
			};
			guest.Forget();
			Check.Equal(401, (await fleet.Call(HttpMethod.Post, "/v1/matches", result)).Status, "resultado sem a chave da frota");
			var (status, paid) = await fleet.Call(HttpMethod.Post, "/v1/matches", result, w.Config.FleetKey);
			Check.Equal(200, status, "registrar o resultado");
			Check.True(paid["players"]["999999"] == null, "quem não estava escalado não recebe");

			Rewards.Gain hostGain = Rewards.ForMatch(10, 2, won: true, draw: false, mvp: false);
			Rewards.Gain guestGain = Rewards.ForMatch(14, 1, won: false, draw: false, mvp: true);

			var (_, hostProfile) = await host.Call(HttpMethod.Get, "/v1/profile");
			Check.Equal(hostGain.Xp, hostProfile["xp"].GetValue<long>(), "XP do vencedor, pela regra do jogo");
			Check.Equal(3000 + hostGain.Presas, hostProfile["presas"].GetValue<long>(), "Presas do vencedor");
			Check.Equal(1, hostProfile["stats"]["wins"].GetValue<int>(), "uma vitória");
			Check.Equal(10, hostProfile["stats"]["kills"].GetValue<int>(), "abates somados");

			var (_, guestProfile) = await guest.Call(HttpMethod.Get, "/v1/profile");
			Check.Equal(guestGain.Xp, guestProfile["xp"].GetValue<long>(), "XP de quem perdeu com MVP");
			Check.Equal(1, guestProfile["stats"]["losses"].GetValue<int>(), "uma derrota");
			Check.Equal(1, guestProfile["stats"]["mvps"].GetValue<int>(), "um MVP");

			JsonObject end = await guest.Hear("match.end");
			Check.Equal(guestGain.Xp, end["you"]["xp"].GetValue<long>(), "o jogador é avisado do que ganhou");
			JsonObject back = await guest.Hear("room", m => m["state"].GetValue<string>() == "waiting");
			Check.True(!back["members"][1]["ready"].GetValue<bool>(), "a sala volta a esperar, ninguém pronto");

			Check.Equal(409, (await fleet.Call(HttpMethod.Post, "/v1/matches", result, w.Config.FleetKey)).Status, "o mesmo resultado de novo é recusado");

			var (_, ranking) = await host.Call(HttpMethod.Get, "/v1/ranking");
			Check.Equal(guest.Name, ranking["ranking"][0]["name"].GetValue<string>(), "o ranking é por XP");
			var (_, history) = await host.Call(HttpMethod.Get, "/v1/matches");
			Check.Equal(1, history["matches"].AsArray().Count, "a partida entra no histórico do jogador");
		}),

		("frota: um servidor de partida com dois lugares joga duas salas ao mesmo tempo; a terceira espera", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", BeatWith("gs-1", 2), w.Config.FleetKey);

			var (a, keyA) = await Solo(w);
			var (b, keyB) = await Solo(w);
			await using Player pa = a, pb = b;
			Check.True(keyA != keyB, "cada sala tem a sua partida");

			var (_, beat) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", BeatWith("gs-1", 2), w.Config.FleetKey);
			var keys = beat["assignments"].AsArray().Select(x => x["matchKey"].GetValue<string>()).ToList();
			Check.True(keys.Count == 2 && keys.Contains(keyA) && keys.Contains(keyB), "a batida traz as duas partidas");

			Player c = await w.NewPlayer();
			await using Player pc = c;
			await c.Connect();
			await c.Hear("rooms");
			await c.Say(Msg("room.create", ("settings", Settings())));
			await c.Hear("room");
			await c.Say(Msg("room.start"));
			Check.Equal("sem_servidor", (await c.Hear("error"))["code"].GetValue<string>(), "com os dois lugares ocupados, a terceira sala espera");
		}),

		("frota: partida que o servidor de partida fecha sem resultado libera o lugar e a sala volta a esperar", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", BeatWith("gs-1", 1), w.Config.FleetKey);

			var (host, key) = await Solo(w);
			await using Player h = host;
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", BeatWith("gs-1", 1), w.Config.FleetKey);   // entrega
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", BeatWith("gs-1", 1, key), w.Config.FleetKey); // aberta

			// Todo mundo saiu antes do fim e ninguém voltou: o servidor de partida fecha e a partida some da lista.
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", BeatWith("gs-1", 1), w.Config.FleetKey);
			Check.Equal("partida_encerrada", (await host.Hear("error"))["code"].GetValue<string>(), "o jogador é avisado");
			await host.Hear("room", m => m["state"].GetValue<string>() == "waiting");

			// O lugar vagou: dá para jogar de novo, sem reiniciar nada.
			host.Forget();
			await host.Say(Msg("room.start"));
			JsonObject again = await host.Hear("match.start");
			Check.True(again["matchKey"].GetValue<string>() != key, "a sala começa outra partida no mesmo servidor");
		}),

		("frota: servidor de partida que para de bater devolve a sala para a espera", async () =>
		{
			await using World w = await World.Start(c => c.FleetTimeout = TimeSpan.FromMilliseconds(300));
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", Beat("gs-1"), w.Config.FleetKey);

			Player host = await w.NewPlayer();
			await using Player h = host;
			await host.Connect();
			await host.Hear("rooms");
			await host.Say(Msg("room.create", ("settings", Settings())));
			await host.Hear("room");
			await host.Say(Msg("room.start"));
			await host.Hear("match.start");

			await Task.Delay(500);
			await fleet.Call(HttpMethod.Get, "/health");      // qualquer olhada na frota varre os caídos
			Check.Equal("servidor_caiu", (await host.Hear("error"))["code"].GetValue<string>(), "o jogador é avisado");
			Check.Equal("waiting", (await host.Hear("room", m => m["state"].GetValue<string>() == "waiting"))["state"].GetValue<string>(), "a sala volta a esperar");

			await w.Services.Security.Flush();
			var gone = await w.Services.Security.Events(new EventFilter(KindPrefix: EventKind.MatchServerGone));
			Check.Equal(1, gone.Count, "o painel fica sabendo que o servidor de partida sumiu");
			Check.Equal("gs-1", gone[0].Actor, "qual servidor");
			Check.Equal(Severity.High, gone[0].Severity, "e é grave");
		}),

		("frota: o servidor de partida conta os incidentes na batida e o painel os vê (partida.*)", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();

			JsonObject beat = Beat("gs-1");
			beat["events"] = new JsonArray
			{
				new JsonObject { ["kind"] = "partida.recusado", ["severity"] = 1, ["match"] = "abc", ["account"] = 7, ["detail"] = "ingresso inválido" },
				new JsonObject { ["kind"] = "login.ok", ["severity"] = 0, ["detail"] = "forjado: não é um tipo de partida" },
				new JsonObject { ["kind"] = "partida.vazia", ["severity"] = 99, ["detail"] = "gravidade fora da faixa" },
				new JsonObject { ["kind"] = 5, ["severity"] = "alta" },
				"torto"
			};
			Check.Equal(200, (await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey)).Status, "evento torto não recusa a batida");

			await w.Services.Security.Flush();
			var events = await w.Services.Security.Events(new EventFilter(KindPrefix: EventKind.MatchPrefix));
			Check.Equal(2, events.Count, "só os tipos de partida entram");

			SecurityEvent refused = events.First(e => e.Kind == EventKind.MatchRefused);
			Check.Equal(7L, refused.AccountId ?? 0, "a conta");
			Check.Equal("gs-1", refused.Actor, "o servidor de partida");
			Check.Equal("abc", refused.Path, "a partida");
			Check.Equal("ingresso inválido", refused.Detail, "o motivo");
			Check.Equal(Severity.Critical, events.First(e => e.Kind == EventKind.MatchEmpty).Severity, "a gravidade é limitada à faixa");

			var logins = await w.Services.Security.Events(new EventFilter(KindPrefix: "login."));
			Check.True(logins.All(e => e.Detail != "forjado: não é um tipo de partida"), "um tipo que não é de partida não passa pela batida");

			// A mesma batida pode trazer no máximo 20: o resto fica para o servidor de partida contar de novo.
			beat = Beat("gs-1");
			var many = new JsonArray();
			for (int i = 0; i < 30; i++) many.Add(new JsonObject { ["kind"] = "partida.recusado", ["detail"] = $"n{i}" });
			beat["events"] = many;
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			await w.Services.Security.Flush();
			var after = await w.Services.Security.Events(new EventFilter(KindPrefix: EventKind.MatchRefused, Limit: 200));
			Check.Equal(1 + 20, after.Count, "uma batida leva até 20 incidentes");
		}),
	};
}
