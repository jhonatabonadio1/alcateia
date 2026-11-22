using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Alcateia.Servidor.Testes.LobbyTests;
using static Alcateia.Servidor.Testes.MatchTests;

namespace Alcateia.Servidor.Testes;

/// <summary>Reiniciar a partida (Jhon, 21/11: "somente o mestre da sala pode
/// reiniciar partida"): só o mestre, para a sala inteira, e a partida
/// descartada não paga ninguém.</summary>
public static class RestartTests
{
	private static JsonObject Result(string key, params Player[] players) => new()
	{
		["matchKey"] = key, ["serverId"] = "gs-1", ["scoreBlue"] = 40, ["scoreOrange"] = 10,
		["players"] = new JsonArray(players.Select(p => (JsonNode)new JsonObject
		{
			["accountId"] = p.Id, ["kills"] = 10, ["deaths"] = 2, ["assists"] = 1, ["headshots"] = 2, ["points"] = 1000
		}).ToArray())
	};

	public static readonly (string, Func<Task>)[] All =
	{
		("reiniciar: só o mestre; a sala inteira vai para uma partida nova; a descartada não paga e a nova paga", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			// Um servidor de partida com lugar para as duas (a descartada ainda está aberta nele).
			JsonObject beat = Beat("gs-1");
			beat["capacity"] = 4;
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);

			await using Player host = await w.NewPlayer(), guest = await w.NewPlayer();
			foreach (Player p in new[] { host, guest }) { await p.Connect(); await p.Hear("rooms"); }
			await host.Say(Msg("room.create", ("settings", Settings())));
			int id = (await host.Hear("room"))["id"].GetValue<int>();
			await guest.Say(Msg("room.join", ("id", id)));
			await guest.Hear("room");
			await guest.Say(Msg("room.ready", ("ready", true)));
			await host.Hear("room", m => m["members"].AsArray().Count == 2 && m["members"][1]["ready"].GetValue<bool>());
			await host.Say(Msg("room.start"));
			string first = (await host.Hear("match.start"))["matchKey"].GetValue<string>();
			Check.Equal(first, (await guest.Hear("match.start"))["matchKey"].GetValue<string>(), "os dois na primeira partida");
			Check.Equal(first, (await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey)).Body["assignment"]["matchKey"].GetValue<string>(), "o servidor de partida recebe a primeira");

			// Quem não é mestre não reinicia.
			await guest.Say(Msg("room.restart"));
			Check.Equal("nao_e_dono", (await guest.Hear("error"))["code"].GetValue<string>(), "o convidado não reinicia");

			// O mestre reinicia no meio: os dois recebem a nova, marcada como reinício.
			await host.Say(Msg("room.restart"));
			JsonObject h = await host.Hear("match.start"), g = await guest.Hear("match.start");
			string second = h["matchKey"].GetValue<string>();
			Check.True(second != first, "partida nova, chave nova");
			Check.Equal(second, g["matchKey"].GetValue<string>(), "o convidado vai para a mesma");
			Check.True(h["restart"].GetValue<bool>() && g["restart"].GetValue<bool>(), "marcada como reinício");
			Check.True(h["ticket"].GetValue<string>() != g["ticket"].GetValue<string>(), "cada um com o seu ingresso");
			JsonObject room = await host.Hear("room", m => m["state"]?.GetValue<string>() == "playing");
			Check.Equal("playing", room["state"].GetValue<string>(), "a sala segue em partida");

			var (_, delivered) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			Check.Equal(second, delivered["assignment"]["matchKey"].GetValue<string>(), "o servidor de partida recebe a nova");
			Check.Equal(2, delivered["assignment"]["players"].AsArray().Count, "com os dois, sem ninguém ter marcado pronto de novo");

			// O resultado da descartada chega depois: recusado, ninguém ganha nada.
			var (oldStatus, _) = await fleet.Call(HttpMethod.Post, "/v1/matches", Result(first, host, guest), w.Config.FleetKey);
			Check.Equal(409, oldStatus, "o resultado da partida descartada é recusado");
			var (_, history) = await host.Call(HttpMethod.Get, "/v1/matches");
			Check.Equal(0, history["matches"].AsArray().Count, "e não entra no histórico do mestre");

			// O da nova paga, como sempre.
			var (newStatus, _) = await fleet.Call(HttpMethod.Post, "/v1/matches", Result(second, host, guest), w.Config.FleetKey);
			Check.Equal(200, newStatus, "o resultado da nova é aceito");
			JsonObject end = await guest.Hear("match.end");
			Check.True(end["you"]["xp"].GetValue<long>() > 0, "e paga");

			// No resultado (a sala esperando, ninguém pronto), o "jogar de novo" do mestre leva os dois.
			await host.Hear("room", m => m["state"]?.GetValue<string>() == "waiting");
			await host.Say(Msg("room.restart"));
			JsonObject again = await guest.Hear("match.start");
			Check.True(again["matchKey"].GetValue<string>() != second, "jogar de novo: outra partida, para todos");
			Check.True(!again["restart"].GetValue<bool>(), "sem partida em curso não é reinício");
		}),
	};
}
