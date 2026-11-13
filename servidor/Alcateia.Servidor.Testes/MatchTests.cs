using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Alcateia.Servidor.Testes.LobbyTests;

namespace Alcateia.Servidor.Testes;

/// <summary>Do "começar" na sala ao prêmio no perfil: a frota recebe a
/// partida, os jogadores recebem o ingresso, o resultado paga.</summary>
public static class MatchTests
{
	private static JsonObject Beat(string id, int port = 7777) =>
		new() { ["id"] = id, ["address"] = "10.0.0.5", ["port"] = port, ["players"] = 0 };

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
			Check.Equal("vila", beat["assignment"]["settings"]["map"].GetValue<string>(), "e as regras da sala");
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
		}),
	};
}
