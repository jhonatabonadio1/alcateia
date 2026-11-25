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
/// excluído"). Desde 25/11 quem chega entra na sala, escolhe o lado e entra
/// com room.enter; um time nunca fica com dois a mais; e partida quase
/// acabando não recebe ninguém.</summary>
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

			// O mestre de antes volta: entra na sala, de fora da partida, no time
			// em que jogou; e entra nela com room.enter, com ingresso para ela.
			guest.Forget();
			host.Forget();
			await host.Say(Msg("room.join", ("id", id)));
			JsonObject inRoom = await host.Hear("room");
			JsonNode meBack = inRoom["members"].AsArray().First(m => m["id"].GetValue<long>() == host.Id);
			Check.True(!meBack["inMatch"].GetValue<bool>(), "quem chega fica na sala, de fora da partida");
			Check.Equal(1, meBack["team"].GetValue<int>(), "sugerido no time em que jogou (azul)");
			Check.True(inRoom["enter"].AsArray().Select(t => t.GetValue<int>()).SequenceEqual(new[] { 1 }), "só dá para entrar pelo azul (o laranja está cheio)");
			await host.Say(Msg("room.enter"));
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
			await late.Hear("room");
			await late.Say(Msg("room.team", ("team", 1)));
			Check.Equal("time_cheio", (await late.Hear("error"))["code"].GetValue<string>(), "o azul está cheio: não troca para ele");
			await late.Say(Msg("room.enter"));
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

		("partida em curso: os bots contam na lista, um time não fica com dois a mais, e quase acabando ninguém entra", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			JsonObject beat = Beat("gs-1");
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);

			await using Player host = await w.NewPlayer(), late = await w.NewPlayer(), extra = await w.NewPlayer(), seer = await w.NewPlayer();
			foreach (Player p in new[] { host, late, extra, seer }) { await p.Connect(); await p.Hear("rooms"); }

			// 3 contra 3 contra bots: o mestre no azul, um bot no laranja.
			await host.Say(Msg("room.create", ("settings", Settings(teamSize: 3, play: LobbyTests.VsBots))));
			int id = (await host.Hear("room"))["id"].GetValue<int>();
			await host.Say(Msg("room.bot.add", ("team", 2)));
			await host.Hear("room", m => m["members"].AsArray().Count == 2);
			await host.Say(Msg("room.start"));
			string key = (await host.Hear("match.start"))["matchKey"].GetValue<string>();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			beat["matches"] = new JsonArray(key);

			// A lista conta o bot nos lugares (Jhon: "partida com bot deve contabilizar aqui").
			seer.Forget();
			await seer.Say(Msg("rooms.list"));
			JsonNode row = (await seer.Hear("rooms", m => m["rooms"].AsArray().Any(r => r["id"].GetValue<int>() == id)))
				["rooms"].AsArray().First(r => r["id"].GetValue<int>() == id);
			Check.Equal(2, row["players"].GetValue<int>() + row["bots"].GetValue<int>(), "1 jogador + 1 bot = 2 lugares");

			// 1 contra 1: entra no azul (empate vai para o azul) e fica 2 contra 1.
			await late.Say(Msg("room.join", ("id", id)));
			await late.Hear("room");
			await late.Say(Msg("room.enter"));
			await late.Hear("match.start");

			// 2 contra 1: o azul ficaria com dois a mais; o que chega vai para o laranja.
			await extra.Say(Msg("room.join", ("id", id)));
			JsonObject room = await extra.Hear("room");
			Check.Equal(2, room["members"].AsArray().First(m => m["id"].GetValue<long>() == extra.Id)["team"].GetValue<int>(), "sugerido no laranja");
			Check.True(room["enter"].AsArray().Select(t => t.GetValue<int>()).SequenceEqual(new[] { 2 }), "só o laranja está aberto");
			await extra.Say(Msg("room.team", ("team", 1)));
			Check.Equal("time_desequilibrado", (await extra.Hear("error"))["code"].GetValue<string>(), "3 contra 1 não pode");

			// Quase acabando (o servidor de partida conta na batida): não entra, e a lista mostra.
			beat["progress"] = new JsonArray(new JsonObject { ["key"] = key, ["live"] = true, ["timeLeft"] = 110.0, ["duration"] = 600.0, ["blue"] = 3, ["orange"] = 1, ["toWin"] = 40 });
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			extra.Forget();
			await extra.Say(Msg("room.enter"));
			Check.Equal("partida_acabando", (await extra.Hear("error"))["code"].GetValue<string>(), "partida de 10 minutos faltando menos de 2, ninguém entra");
			JsonObject closed = await extra.Hear("room", m => m["closing"]?.GetValue<bool>() == true, 3000);
			Check.Equal(0, closed["enter"].AsArray().Count, "e a sala diz que não dá para entrar por nenhum lado");
			seer.Forget();
			await seer.Say(Msg("rooms.list"));
			row = (await seer.Hear("rooms", m => m["rooms"].AsArray().Any(r => r["id"].GetValue<int>() == id)))
				["rooms"].AsArray().First(r => r["id"].GetValue<int>() == id);
			Check.True(row["closing"].GetValue<bool>() && !row["open"].GetValue<bool>(), "na lista, acabando e fechada");
			await seer.Say(Msg("room.join", ("id", id)));
			Check.Equal("partida_acabando", (await seer.Hear("error"))["code"].GetValue<string>(), "nem entra na sala");

			// Placar quase decidido também fecha; com tempo e placar folgados, abre de novo.
			beat["progress"] = new JsonArray(new JsonObject { ["key"] = key, ["live"] = true, ["timeLeft"] = 400.0, ["blue"] = 32, ["orange"] = 1, ["toWin"] = 40 });
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			await extra.Say(Msg("room.enter"));
			Check.Equal("partida_acabando", (await extra.Hear("error"))["code"].GetValue<string>(), "32 de 40 pontos: decidida");
			// A janela do fim acompanha a partida: numa de 3 minutos são os últimos
			// 36 s (20%), não 2 minutos; faltando 1 minuto, ainda entra.
			beat["progress"] = new JsonArray(new JsonObject { ["key"] = key, ["live"] = true, ["timeLeft"] = 60.0, ["duration"] = 180.0, ["blue"] = 5, ["orange"] = 1, ["toWin"] = 40 });
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", beat, w.Config.FleetKey);
			await extra.Say(Msg("room.enter"));
			await extra.Hear("match.start");
		}),
	};
}
