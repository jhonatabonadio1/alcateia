using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Alcateia.Servidor.Testes.LobbyTests;
using static Alcateia.Servidor.Testes.MatchTests;

namespace Alcateia.Servidor.Testes;

/// <summary>Sair e entrar com a partida em curso (Jhon, 24/11: "ao sair da
/// partida, posso entrar nela novamente caso tenha vaga"; "se eu sair da
/// partida, deve ir para o lobby e o mestre passa a ser o próximo jogador";
/// "e o jogo continua, mas se não tiver mais players na sala, o jogo é
/// excluído").</summary>
public static class RejoinTests
{
	public static readonly (string, Func<Task>)[] All =
	{
		("partida em curso: o mestre sai e o próximo assume; dá para voltar e entrar novo se houver vaga; sala vazia descarta a partida", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			JsonObject beat = Beat("gs-1");
			beat["capacity"] = 2;
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);

			await using Player host = await w.NewPlayer(), guest = await w.NewPlayer(), late = await w.NewPlayer(), extra = await w.NewPlayer();
			foreach (Player p in new[] { host, guest, late, extra }) { await p.Connect(); await p.Hear("rooms"); }

			// Times de 1: a sala cabe dois.
			await host.Say(Msg("room.create", ("settings", Settings(teamSize: 1))));
			int id = (await host.Hear("room"))["id"].GetValue<int>();
			await guest.Say(Msg("room.join", ("id", id)));
			await guest.Hear("room");
			await guest.Say(Msg("room.ready", ("ready", true)));
			await host.Hear("room", m => m["members"].AsArray().Count == 2 && m["members"][1]["ready"].GetValue<bool>());
			await host.Say(Msg("room.start"));
			string key = (await host.Hear("match.start"))["matchKey"].GetValue<string>();
			await guest.Hear("match.start");
			var (_, first) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			Check.Equal(key, first["assignment"]["matchKey"].GetValue<string>(), "o servidor de partida recebe a partida");
			beat["matches"] = new JsonArray(key);

			// Cheia (1 contra 1): ninguém entra.
			await late.Say(Msg("room.join", ("id", id)));
			Check.Equal("sala_cheia", (await late.Hear("error"))["code"].GetValue<string>(), "partida cheia, não entra");

			// O mestre sai: o convidado vira o mestre, e a partida continua.
			await host.Say(Msg("room.leave"));
			await host.Hear("left");
			JsonObject inherited = await guest.Hear("room", m => m["members"].AsArray().Count == 1);
			Check.True(inherited["members"][0]["host"].GetValue<bool>(), "quem ficou virou o mestre");
			Check.Equal("playing", inherited["state"].GetValue<string>(), "e a partida continua");

			// Na lista, a sala jogando com vaga aparece aberta.
			late.Forget();
			await late.Say(Msg("rooms.list"));
			JsonObject list = await late.Hear("rooms", m => m["rooms"].AsArray().Any(r => r["id"].GetValue<int>() == id));
			JsonNode listed = list["rooms"].AsArray().First(r => r["id"].GetValue<int>() == id);
			Check.True(listed["open"].GetValue<bool>(), "a sala jogando com vaga aparece aberta");

			// O mestre de antes volta: no mesmo time (o lugar dele na partida), com ingresso para ela.
			guest.Forget();
			await host.Say(Msg("room.join", ("id", id)));
			JsonObject back = await host.Hear("match.start");
			Check.Equal(key, back["matchKey"].GetValue<string>(), "voltou para a mesma partida");
			Check.True(back["late"].GetValue<bool>(), "marcado como entrada no meio");
			JsonObject roomBack = await guest.Hear("room", m => m["members"].AsArray().Count == 2);
			Check.Equal(1, roomBack["members"].AsArray().First(m => m["id"].GetValue<long>() == host.Id)["team"].GetValue<int>(), "no time de antes (azul)");
			Check.True(!roomBack["members"].AsArray().First(m => m["id"].GetValue<long>() == host.Id)["host"].GetValue<bool>(), "e não é mais o mestre");
			var (_, none) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			Check.Equal(0, none["assignments"].AsArray().Count, "quem volta já tem lugar: o servidor de partida não recebe nada");

			// O convidado sai; quem nunca jogou entra no lugar dele, e o servidor de partida abre o lugar.
			await guest.Say(Msg("room.leave"));
			await guest.Hear("left");
			await late.Say(Msg("room.join", ("id", id)));
			JsonObject lateStart = await late.Hear("match.start");
			Check.Equal(key, lateStart["matchKey"].GetValue<string>(), "quem chega entra na partida em curso");
			var (_, update) = await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			JsonObject again = update["assignments"].AsArray().Single()!.AsObject();
			Check.Equal(key, again["matchKey"].GetValue<string>(), "a escalação vai de novo ao servidor de partida");
			Check.True(again["update"].GetValue<bool>(), "como atualização (não abre partida nova)");
			Check.True(again["players"].AsArray().Any(p => p["accountId"].GetValue<long>() == late.Id && p["team"].GetValue<int>() == 2),
				"com o lugar novo, no laranja (o que estava livre)");

			// Todos saem: a sala some e a partida é descartada (o resultado, se chegar, é recusado).
			await host.Say(Msg("room.leave"));
			await host.Hear("left");
			await late.Say(Msg("room.leave"));
			await late.Hear("left");
			extra.Forget();
			await extra.Say(Msg("rooms.list"));
			await extra.Hear("rooms", m => m["rooms"].AsArray().All(r => r["id"].GetValue<int>() != id));
			var result = new JsonObject
			{
				["matchKey"] = key, ["serverId"] = "gs-1", ["scoreBlue"] = 1, ["scoreOrange"] = 0,
				["players"] = new JsonArray(new JsonObject { ["accountId"] = host.Id, ["kills"] = 1, ["deaths"] = 0, ["assists"] = 0, ["headshots"] = 0, ["points"] = 10 })
			};
			var (status, _) = await fleet.Call(HttpMethod.Post, "/v1/matches", result, w.Config.FleetKey);
			Check.Equal(409, status, "a partida da sala vazia foi descartada: o resultado é recusado");
		}),
	};
}
