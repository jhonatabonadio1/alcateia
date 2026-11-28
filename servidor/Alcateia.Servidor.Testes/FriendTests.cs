using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Dominio.Contas;
using static Alcateia.Servidor.Testes.LobbyTests;

namespace Alcateia.Servidor.Testes;

/// <summary>Os amigos pelo WebSocket do saguão: pedir, aceitar, recusar,
/// cancelar, remover, a presença empurrada e o convite para a sala.</summary>
public static class FriendTests
{
	private static string Code(JsonObject error) => error["code"].GetValue<string>();

	private static int Count(JsonObject friends, string list) => friends[list].AsArray().Count;

	private static JsonNode Friend(JsonObject friends, long id) =>
		friends["friends"].AsArray().FirstOrDefault(f => f["id"].GetValue<long>() == id);

	private static string Status(JsonObject friends, long id) => Friend(friends, id)?["status"]?.GetValue<string>();

	/// <summary>Entra no saguão e joga fora o que chega na entrada (olá,
	/// salas e a lista de amigos).</summary>
	private static async Task Enter(params Player[] players)
	{
		foreach (Player p in players)
		{
			await p.Connect();
			await p.Hear("rooms");
			await p.Hear("friends");
		}
	}

	/// <summary><paramref name="a"/> pede, <paramref name="b"/> aceita, e os
	/// dois esperam a lista com o outro dentro.</summary>
	private static async Task Befriend(Player a, Player b)
	{
		await a.Say(Msg("friend.add", ("name", b.Handle)));
		await b.Hear("friend.request");
		await b.Say(Msg("friend.accept", ("id", a.Id)));
		await a.Hear("friends", m => Friend(m, b.Id) != null);
		await b.Hear("friends", m => Friend(m, a.Id) != null);
		a.Forget();
		b.Forget();
	}

	public static readonly (string, Func<Task>)[] All =
	{
		// 27/11: o pedido passou a ser pelo nome com a tag (Nome#0042; Jhon: "cada
		// player terá uma tag #0000 para adicionar"). Só o nome não basta mais.
		("amigos: a lista vem ao entrar; o pedido pelo nome com a tag (sem diferenciar maiúsculas) chega na hora; aceitar vira amizade dos dois lados", async () =>
		{
			await using World w = await World.Start();
			await using Player a = await w.NewPlayer(), b = await w.NewPlayer();
			await a.Connect();
			JsonObject first = await a.Hear("friends");
			Check.Equal(0, Count(first, "friends") + Count(first, "requests") + Count(first, "sent"), "conta nova entra com a lista vazia, sem pedir");
			await b.Connect();
			await b.Hear("friends");

			Check.Equal(4, a.Tag.Length, "a conta nasce com uma tag de quatro algarismos, que vem no perfil");
			Check.Equal(a.Tag, (await a.Hear("hello"))["tag"].GetValue<string>(), "e no olá do saguão");

			await a.Say(Msg("friend.add", ("name", "nao_existe_1#0001")));
			Check.Equal("conta_inexistente", Code(await a.Hear("error")), "nome que não existe");
			string onlyName = b.Name;
			await a.Say(Msg("friend.add", ("name", onlyName)));
			Check.Equal("tag_faltando", Code(await a.Hear("error")), "só o nome, sem a tag, não pede");
			string wrong = ((int.Parse(b.Tag) + 1) % 10000).ToString("D4");
			await a.Say(Msg("friend.add", ("name", $"{b.Name}#{wrong}")));
			Check.Equal("conta_inexistente", Code(await a.Hear("error")), "tag errada responde igual a nome que não existe");
			await a.Say(Msg("friend.add", ("name", a.Handle.ToUpperInvariant())));
			Check.Equal("amigo_voce", Code(await a.Hear("error")), "não se adiciona a si mesmo");

			await a.Say(Msg("friend.add", ("name", "  " + b.Handle.ToUpperInvariant() + " ")));
			JsonObject request = await b.Hear("friend.request");
			Check.Equal(a.Id, request["from"]["id"].GetValue<long>(), "o pedido diz de quem é");
			Check.Equal(a.Name, request["from"]["name"].GetValue<string>(), "com o nome");
			Check.Equal(a.Tag, request["from"]["tag"].GetValue<string>(), "a tag");
			Check.Equal(PhotoRule.Default, request["from"]["photo"].GetValue<string>(), "e a foto");
			JsonObject inbox = await b.Hear("friends", m => Count(m, "requests") == 1);
			Check.Equal(a.Id, inbox["requests"][0]["id"].GetValue<long>(), "a lista de quem recebeu mostra o pedido");
			JsonObject sent = await a.Hear("friends", m => Count(m, "sent") == 1);
			Check.Equal(b.Id, sent["sent"][0]["id"].GetValue<long>(), "a de quem pediu mostra o enviado");
			Check.Equal(0, Count(sent, "friends"), "pedido não é amizade ainda");

			await a.Say(Msg("friend.add", ("name", b.Handle)));
			Check.Equal("pedido_ja_enviado", Code(await a.Hear("error")), "pedir de novo não duplica");
			await a.Say(Msg("friend.accept", ("id", b.Id)));
			Check.Equal("pedido_inexistente", Code(await a.Hear("error")), "quem pediu não aceita o próprio pedido");

			await b.Say(Msg("friend.accept", ("id", a.Id)));
			JsonObject accepted = await a.Hear("friend.accepted");
			Check.Equal(b.Id, accepted["by"]["id"].GetValue<long>(), "quem pediu é avisado de quem aceitou");
			JsonObject mine = await a.Hear("friends", m => Count(m, "friends") == 1);
			Check.Equal("online", Status(mine, b.Id), "o amigo novo já aparece com a presença");
			Check.Equal(0, Count(mine, "sent"), "e o pedido enviado some");
			JsonObject theirs = await b.Hear("friends", m => Count(m, "friends") == 1);
			Check.Equal(0, Count(theirs, "requests"), "do outro lado o pedido virou amigo");

			await b.Say(Msg("friend.add", ("name", a.Handle)));
			Check.Equal("ja_amigos", Code(await b.Hear("error")), "amigo não se pede de novo");

			a.Forget();
			await a.Say(Msg("friends.list"));
			Check.Equal(1, Count(await a.Hear("friends"), "friends"), "friends.list devolve a lista");
		}),

		("amigos: recusar apaga o pedido; quem pediu cancela o próprio; remover desfaz dos dois lados; a lista fica no banco", async () =>
		{
			await using World w = await World.Start();
			await using Player a = await w.NewPlayer(), b = await w.NewPlayer();
			await Enter(a, b);

			// Recusar.
			await a.Say(Msg("friend.add", ("name", b.Handle)));
			await b.Hear("friend.request");
			await b.Hear("friends", m => Count(m, "requests") == 1);
			await a.Hear("friends", m => Count(m, "sent") == 1);
			await b.Say(Msg("friend.decline", ("id", a.Id)));
			await b.Hear("friends", m => Count(m, "requests") == 0);
			JsonObject after = await a.Hear("friends", m => Count(m, "sent") == 0);
			Check.Equal(0, Count(after, "friends"), "recusado: o pedido some de quem pediu, sem virar amizade");
			await b.Say(Msg("friend.decline", ("id", a.Id)));
			Check.Equal("pedido_inexistente", Code(await b.Hear("error")), "recusar duas vezes");

			// Cancelar o que mandou.
			await a.Say(Msg("friend.add", ("name", b.Handle)));
			await b.Hear("friends", m => Count(m, "requests") == 1);
			await a.Say(Msg("friend.decline", ("id", b.Id)));
			await a.Hear("friends", m => Count(m, "sent") == 0);
			await b.Hear("friends", m => Count(m, "requests") == 0);
			await b.Say(Msg("friend.accept", ("id", a.Id)));
			Check.Equal("pedido_inexistente", Code(await b.Hear("error")), "pedido cancelado não se aceita");

			// Amigos, e a lista sobrevive a sair e entrar de novo.
			a.Forget(); b.Forget();
			await Befriend(a, b);
			await b.DisposeAsync();
			await a.Hear("friends", m => Status(m, b.Id) == "offline");
			await using Player again = new Player(w) { Name = b.Name, Token = b.Token, Id = b.Id };
			await again.Connect();
			JsonObject saved = await again.Hear("friends");
			Check.Equal(a.Id, saved["friends"][0]["id"].GetValue<long>(), "ao entrar de novo, o amigo está na lista (veio do banco)");

			// Remover.
			await again.Say(Msg("friend.remove", ("id", a.Id)));
			await again.Hear("friends", m => Count(m, "friends") == 0);
			await a.Hear("friends", m => Count(m, "friends") == 0);
			await a.Say(Msg("friend.remove", ("id", b.Id)));
			Check.Equal("nao_e_amigo", Code(await a.Hear("error")), "remover quem já não é amigo");
		}),

		("amigos: pedidos cruzados viram amizade na hora, sem ninguém aceitar", async () =>
		{
			await using World w = await World.Start();
			await using Player a = await w.NewPlayer(), b = await w.NewPlayer();
			await Enter(a, b);

			await a.Say(Msg("friend.add", ("name", b.Handle)));
			await b.Hear("friend.request");
			await b.Say(Msg("friend.add", ("name", a.Handle)));

			Check.Equal(b.Id, (await a.Hear("friend.accepted"))["by"]["id"].GetValue<long>(), "o segundo pedido vale como aceite");
			JsonObject mine = await a.Hear("friends", m => Count(m, "friends") == 1);
			Check.Equal(0, Count(mine, "sent") + Count(mine, "requests"), "sem pedido sobrando de um lado");
			JsonObject theirs = await b.Hear("friends", m => Count(m, "friends") == 1);
			Check.Equal(0, Count(theirs, "sent") + Count(theirs, "requests"), "nem do outro");
			Check.Equal(1L, (await w.Db.Query("select count(*) as n from friendships")).Long(0, "n"), "uma linha só no banco para o par");
		}),

		("amigos: a presença é empurrada (entrou, sala, partida, caiu), só para amigos; os ligados vêm primeiro", async () =>
		{
			await using World w = await World.Start();
			Player fleet = w.Server();
			await fleet.Call(HttpMethod.Post, "/v1/fleet/heartbeat", MatchTests.Beat("gs-1"), w.Config.FleetKey);

			// "Zeca" vem depois de "Ana" no alfabeto, mas é quem vai estar ligado.
			await using Player me = await w.NewPlayer(), zeca = await w.NewPlayer("Zeca_amigo"), ana = await w.NewPlayer("Ana_amiga"), stranger = await w.NewPlayer();
			await Enter(me, zeca, ana, stranger);
			await Befriend(me, zeca);
			await Befriend(me, ana);

			await ana.DisposeAsync();
			JsonObject list = await me.Hear("friends", m => Status(m, ana.Id) == "offline");
			Check.Equal(zeca.Id, list["friends"][0]["id"].GetValue<long>(), "o amigo ligado vem antes do desligado, mesmo depois no alfabeto");
			Check.Equal("online", Status(list, zeca.Id), "ligado, fora de sala");

			await zeca.Say(Msg("room.create", ("settings", Settings(play: VsBots))));
			int room = (await zeca.Hear("room"))["id"].GetValue<int>();
			JsonObject inRoom = await me.Hear("friends", m => Status(m, zeca.Id) == "room");
			Check.Equal(room, Friend(inRoom, zeca.Id)["room"].GetValue<int>(), "numa sala, com o número dela");

			await zeca.Say(Msg("room.bot.add", ("team", 2)));
			await zeca.Say(Msg("room.start"));
			await zeca.Hear("match.start");
			JsonObject playing = await me.Hear("friends", m => Status(m, zeca.Id) == "playing");
			Check.Equal(room, Friend(playing, zeca.Id)["room"].GetValue<int>(), "em partida, ainda com a sala");

			await zeca.Say(Msg("room.leave"));
			JsonObject left = await me.Hear("friends", m => Status(m, zeca.Id) == "online");
			Check.True(Friend(left, zeca.Id)["room"] == null, "saiu da sala: sem número");

			await zeca.DisposeAsync();
			await me.Hear("friends", m => Status(m, zeca.Id) == "offline");

			// Quem não é amigo não recebeu nada disso.
			stranger.Forget();
			await stranger.Say(Msg("ping"));
			await stranger.Hear("pong");
			await Check.Throws<Failed>(() => stranger.Hear("friends", timeoutMs: 300), "quem não é amigo não recebe presença");
		}),

		("convite: só amigo ligado, de dentro de uma sala; dispensa a senha; recusar avisa; vale enquanto quem convidou está na sala", async () =>
		{
			await using World w = await World.Start();
			await using Player host = await w.NewPlayer(), friend = await w.NewPlayer(), stranger = await w.NewPlayer(), away = await w.NewPlayer();
			await Enter(host, friend, stranger, away);
			await Befriend(host, friend);
			await Befriend(host, away);
			await away.DisposeAsync();
			await host.Hear("friends", m => Status(m, away.Id) == "offline");

			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			Check.Equal("fora_de_sala", Code(await host.Hear("error")), "sem sala não há para onde convidar");

			await host.Say(Msg("room.create", ("settings", Settings("Sala fechada", teamSize: 2, password: "segredo"))));
			int room = (await host.Hear("room"))["id"].GetValue<int>();

			await host.Say(Msg("friend.invite", ("id", stranger.Id)));
			Check.Equal("nao_e_amigo", Code(await host.Hear("error")), "quem não é amigo não se convida");
			await host.Say(Msg("friend.invite", ("id", away.Id)));
			Check.Equal("amigo_offline", Code(await host.Hear("error")), "amigo desligado não recebe convite");
			await stranger.Say(Msg("invite.accept", ("from", host.Id)));
			Check.Equal("convite_vencido", Code(await stranger.Hear("error")), "sem convite não se entra por ele");

			// Recusar: quem convidou fica sabendo.
			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			JsonObject invite = await friend.Hear("friend.invited");
			Check.Equal(host.Id, invite["from"]["id"].GetValue<long>(), "o convite diz de quem é");
			Check.Equal(room, invite["room"]["id"].GetValue<int>(), "para qual sala");
			Check.Equal("Sala fechada", invite["room"]["title"].GetValue<string>(), "com o título");
			Check.Equal("armazem_estendido", invite["room"]["map"].GetValue<string>(), "o mapa");
			Check.Equal(1, invite["room"]["players"].GetValue<int>(), "quantos há");
			Check.Equal(4, invite["room"]["capacity"].GetValue<int>(), "e quantos cabem");
			Check.True(!invite.ToJsonString().Contains("segredo"), "a senha não vai no convite");
			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			Check.Equal("devagar", Code(await host.Hear("error")), "convidar o mesmo amigo de novo logo em seguida");
			await friend.Say(Msg("invite.decline", ("from", host.Id)));
			Check.Equal(friend.Id, (await host.Hear("invite.declined"))["by"]["id"].GetValue<long>(), "recusar avisa quem convidou");
			await friend.Say(Msg("invite.accept", ("from", host.Id)));
			Check.Equal("convite_vencido", Code(await friend.Hear("error")), "convite recusado não vale mais");

			// Aceitar: entra sem a senha.
			await friend.Say(Msg("room.join", ("id", room)));
			Check.Equal("senha_errada", Code(await friend.Hear("error")), "sem convite a senha vale");
			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			await friend.Hear("friend.invited");
			await friend.Say(Msg("invite.accept", ("from", host.Id)));
			JsonObject joined = await friend.Hear("room");
			Check.Equal(2, joined["members"].AsArray().Count, "com o convite, entrou na sala com senha");
			await host.Hear("friends", m => Status(m, friend.Id) == "room");
			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			Check.Equal("ja_na_sala", Code(await host.Hear("error")), "quem já está na sala não se convida");

			// Quem convidou saiu da sala: o convite caduca.
			await friend.Say(Msg("room.leave"));
			await friend.Hear("left");
			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			await friend.Hear("friend.invited");
			await host.Say(Msg("room.leave"));
			await host.Hear("left");
			await friend.Say(Msg("invite.accept", ("from", host.Id)));
			Check.Equal("convite_vencido", Code(await friend.Hear("error")), "quem convidou saiu da sala: o convite não vale");
		}),

		("convite: respeita a lotação da sala", async () =>
		{
			await using World w = await World.Start();
			await using Player host = await w.NewPlayer(), other = await w.NewPlayer(), friend = await w.NewPlayer();
			await Enter(host, other, friend);
			await Befriend(host, friend);

			await host.Say(Msg("room.create", ("settings", Settings(teamSize: 1, password: "segredo"))));
			int room = (await host.Hear("room"))["id"].GetValue<int>();
			await other.Say(Msg("room.join", ("id", room), ("password", "segredo")));
			await other.Hear("room");

			await host.Say(Msg("friend.invite", ("id", friend.Id)));
			JsonObject invite = await friend.Hear("friend.invited");
			Check.Equal(2, invite["room"]["players"].GetValue<int>(), "o convite mostra a sala cheia");
			await friend.Say(Msg("invite.accept", ("from", host.Id)));
			Check.Equal("sala_cheia", Code(await friend.Hear("error")), "convite não abre lugar em sala cheia");

			// Abriu vaga: o mesmo convite ainda serve.
			await other.Say(Msg("room.leave"));
			await other.Hear("left");
			await friend.Say(Msg("invite.accept", ("from", host.Id)));
			Check.Equal(2, (await friend.Hear("room"))["members"].AsArray().Count, "com vaga, o convite entra");
		}),

		("amigos: os tetos (100 amigos, 50 pedidos de cada lado), o freio de pedidos e a conta apagada", async () =>
		{
			await using World w = await World.Start();
			await using Player a = await w.NewPlayer(), b = await w.NewPlayer(), c = await w.NewPlayer();
			await Enter(a, b, c);

			// Contas de enchimento direto no banco (criar pela API custaria uma senha cada).
			await w.Db.Query(
				"insert into accounts (name, name_key, pass_hash) select 'Massa' || g, 'massa' || g, 'x' from generate_series(1, 150) g");
			string filler = "(select id from accounts where name_key like 'massa%' order by id offset {0} limit {1})";

			// 50 pedidos enviados por A: o 51º não sai.
			await w.Db.Query($"insert into friendships (low, high, requester) select least($1::bigint, id), greatest($1::bigint, id), $1::bigint from {string.Format(filler, 0, 50)} x", a.Id);
			await a.Say(Msg("friend.add", ("name", b.Handle)));
			Check.Equal("pedidos_demais", Code(await a.Hear("error")), "50 pedidos esperando: não manda mais");

			// 50 pedidos recebidos por B: ninguém consegue mandar o 51º.
			await w.Db.Query($"insert into friendships (low, high, requester) select least($1::bigint, id), greatest($1::bigint, id), id from {string.Format(filler, 0, 50)} x", b.Id);
			await c.Say(Msg("friend.add", ("name", b.Handle)));
			Check.Equal("pedidos_demais_dele", Code(await c.Hear("error")), "caixa de pedidos cheia do outro lado");

			// 100 amigos de C: não pede nem aceita mais.
			await w.Db.Query($"insert into friendships (low, high, requester, accepted) select least($1::bigint, id), greatest($1::bigint, id), id, true from {string.Format(filler, 50, 100)} x", c.Id);
			string massa = (await w.Db.Query("select tag from accounts where name_key = 'massa1'")).Int(0, "tag").ToString("D4");
			await c.Say(Msg("friend.add", ("name", "Massa1#" + massa)));
			Check.Equal("amigos_cheio", Code(await c.Hear("error")), "lista cheia: não pede");
			await w.Db.Query("insert into friendships (low, high, requester) values (least($1::bigint, $2::bigint), greatest($1::bigint, $2::bigint), $2::bigint)", c.Id, a.Id);
			await c.Say(Msg("friend.accept", ("id", a.Id)));
			Check.Equal("amigos_cheio", Code(await c.Hear("error")), "lista cheia: não aceita");

			// Conta apagada leva as amizades dela.
			Check.Equal(100L, (await w.Db.Query("select count(*) as n from friendships where accepted")).Long(0, "n"), "os 100 amigos de C estão no banco");
			await w.Db.Query("delete from accounts where name_key like 'massa%'");
			Check.Equal(1L, (await w.Db.Query("select count(*) as n from friendships")).Long(0, "n"), "apagar as contas apaga as linhas delas (cascata); fica só o pedido entre A e C");

			// O freio: pedidos demais num minuto.
			string last = "";
			for (int i = 0; i < Alcateia.Aplicacao.Saguao.LobbyService.FriendAddsPerMinute + 1 && last != "devagar"; i++)
			{
				await b.Say(Msg("friend.add", ("name", "ninguem_" + i + "#0000")));
				last = Code(await b.Hear("error"));
			}
			Check.Equal("devagar", last, "pedido de amizade demais em um minuto");
		})
	};
}
