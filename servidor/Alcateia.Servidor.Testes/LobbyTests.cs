using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Alcateia.Servidor.Testes;

/// <summary>O saguão por WebSocket: salas, times, chat.</summary>
public static class LobbyTests
{
	public static JsonObject Msg(string type, params (string Key, JsonNode Value)[] fields)
	{
		var m = new JsonObject { ["t"] = type };
		foreach (var (key, value) in fields) m[key] = value;
		return m;
	}

	public static JsonObject Settings(string title = "Sala do teste", int teamSize = 4, string password = "") =>
		new() { ["title"] = title, ["teamSize"] = teamSize, ["map"] = "vila", ["minutes"] = 10, ["scoreToWin"] = 40, ["password"] = password };

	public static readonly (string, Func<Task>)[] All =
	{
		("saguão: sem token não entra; com token recebe olá e a lista", async () =>
		{
			await using World w = await World.Start();
			var anon = new Player(w) { Name = "anon", Token = "token-que-nao-existe" };
			await Check.Throws<System.Net.WebSockets.WebSocketException>(anon.Connect, "conectar sem login");

			await using Player a = await w.NewPlayer();
			await a.Connect();
			JsonObject hello = await a.Hear("hello");
			Check.Equal(a.Name, hello["name"].GetValue<string>(), "olá com o nome da conta");
			Check.Equal(0, (await a.Hear("rooms"))["rooms"].AsArray().Count, "lista vazia");
		}),

		("sala: criar aparece na lista de quem está olhando; entrar equilibra os times; a senha não vaza", async () =>
		{
			await using World w = await World.Start();
			await using Player host = await w.NewPlayer(), guest = await w.NewPlayer(), watcher = await w.NewPlayer();
			foreach (Player p in new[] { host, guest, watcher }) { await p.Connect(); await p.Hear("rooms"); }

			await host.Say(Msg("room.create", ("settings", Settings(password: "segredo"))));
			JsonObject room = await host.Hear("room");
			int id = room["id"].GetValue<int>();
			Check.True(room["members"][0]["host"].GetValue<bool>(), "quem cria é o dono");

			JsonObject list = await watcher.Hear("rooms", m => m["rooms"].AsArray().Count == 1);
			Check.True(list["rooms"][0]["hasPassword"].GetValue<bool>(), "a lista diz que tem senha");
			Check.True(!list.ToJsonString().Contains("segredo"), "a senha não vai na lista");

			await guest.Say(Msg("room.join", ("id", id), ("password", "errada")));
			Check.Equal("senha_errada", (await guest.Hear("error"))["code"].GetValue<string>(), "senha errada");

			await guest.Say(Msg("room.join", ("id", id), ("password", "segredo")));
			JsonObject joined = await guest.Hear("room");
			Check.Equal(2, joined["members"].AsArray().Count, "dois na sala");
			Check.Equal(2, joined["members"][1]["team"].GetValue<int>(), "o segundo vai para o outro time");
			Check.True(!joined.ToJsonString().Contains("segredo"), "a senha não vai no estado da sala");

			var (_, http) = await watcher.Call(HttpMethod.Get, "/v1/rooms");
			Check.Equal(2, http["rooms"][0]["players"].GetValue<int>(), "GET /v1/rooms mostra a mesma coisa");
		}),

		("sala: trocar de time e ficar pronto chega a todos; só o dono muda as regras", async () =>
		{
			await using World w = await World.Start();
			await using Player host = await w.NewPlayer(), guest = await w.NewPlayer();
			foreach (Player p in new[] { host, guest }) { await p.Connect(); await p.Hear("rooms"); }
			await host.Say(Msg("room.create", ("settings", Settings())));
			int id = (await host.Hear("room"))["id"].GetValue<int>();
			await guest.Say(Msg("room.join", ("id", id)));
			await host.Hear("room", m => m["members"].AsArray().Count == 2);

			await guest.Say(Msg("room.team", ("team", 1)));
			JsonObject after = await host.Hear("room", m => m["members"][1]["team"].GetValue<int>() == 1);
			Check.Equal(1, after["members"][1]["team"].GetValue<int>(), "o dono vê a troca de time");

			await guest.Say(Msg("room.ready", ("ready", true)));
			await host.Hear("room", m => m["members"][1]["ready"].GetValue<bool>());

			await guest.Say(Msg("room.settings", ("settings", Settings("Tomei a sala"))));
			Check.Equal("nao_e_dono", (await guest.Hear("error"))["code"].GetValue<string>(), "convidado não muda as regras");

			await host.Say(Msg("room.settings", ("settings", Settings("Nova regra", teamSize: 6))));
			JsonObject changed = await guest.Hear("room", m => m["settings"]["title"].GetValue<string>() == "Nova regra");
			Check.Equal(6, changed["settings"]["teamSize"].GetValue<int>(), "a regra nova chega ao convidado");

			await host.Say(Msg("room.settings", ("settings", Settings(teamSize: 5))));
			Check.Equal("regras_invalidas", (await host.Hear("error"))["code"].GetValue<string>(), "time de 5 não existe");
		}),

		("sala: o dono sai e o próximo assume; o último sai e a sala some", async () =>
		{
			await using World w = await World.Start();
			await using Player host = await w.NewPlayer(), guest = await w.NewPlayer();
			foreach (Player p in new[] { host, guest }) { await p.Connect(); await p.Hear("rooms"); }
			await host.Say(Msg("room.create", ("settings", Settings())));
			int id = (await host.Hear("room"))["id"].GetValue<int>();
			await guest.Say(Msg("room.join", ("id", id)));
			await guest.Hear("room");

			await host.Say(Msg("room.leave"));
			await host.Hear("left");
			JsonObject inherited = await guest.Hear("room", m => m["members"].AsArray().Count == 1);
			Check.True(inherited["members"][0]["host"].GetValue<bool>(), "quem ficou virou o dono");

			await guest.DisposeAsync();      // cai a conexão do último
			host.Forget();
			await host.Say(Msg("rooms.list"));
			JsonObject empty = await host.Hear("rooms", m => m["rooms"].AsArray().Count == 0);
			Check.Equal(0, empty["rooms"].AsArray().Count, "sala vazia some da lista");
		}),

		("chat: na sala fala com a sala, fora dela com a lista; e tem freio", async () =>
		{
			await using World w = await World.Start();
			await using Player a = await w.NewPlayer(), b = await w.NewPlayer(), outside = await w.NewPlayer();
			foreach (Player p in new[] { a, b, outside }) { await p.Connect(); await p.Hear("rooms"); }
			await a.Say(Msg("room.create", ("settings", Settings())));
			int id = (await a.Hear("room"))["id"].GetValue<int>();
			await b.Say(Msg("room.join", ("id", id)));
			await b.Hear("room");

			await a.Say(Msg("chat", ("text", "  bora?  ")));
			JsonObject heard = await b.Hear("chat");
			Check.Equal("bora?", heard["text"].GetValue<string>(), "o texto chega aparado");
			Check.Equal(a.Name, heard["from"].GetValue<string>(), "com o nome de quem falou (o do servidor, não o que o cliente disser)");

			outside.Forget();
			await outside.Say(Msg("ping"));
			await outside.Hear("pong");
			await Check.Throws<Failed>(() => outside.Hear("chat", timeoutMs: 300), "quem está fora da sala não ouve a sala");

			for (int i = 0; i < 6; i++) await a.Say(Msg("chat", ("text", $"spam {i}")));
			Check.Equal("devagar", (await a.Hear("error"))["code"].GetValue<string>(), "seis mensagens seguidas");
		}),

		("conta: entrar de novo derruba a conexão antiga e tira da sala", async () =>
		{
			await using World w = await World.Start();
			await using Player first = await w.NewPlayer("Duplo");
			await first.Connect();
			await first.Hear("rooms");
			await first.Say(Msg("room.create", ("settings", Settings())));
			await first.Hear("room");

			await using var second = new Player(w) { Name = "Duplo", Token = first.Token };
			await second.Connect();
			JsonObject rooms = await second.Hear("rooms");
			Check.Equal(0, rooms["rooms"].AsArray().Count, "a sala da conexão antiga foi embora com ela");
			await Check.Throws<Failed>(() => first.Hear("pong", timeoutMs: 1500), "a conexão antiga foi fechada");
		}),
	};
}
