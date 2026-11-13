using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Alcateia.Servidor.Testes;

/// <summary>Contas, login e perfil, pela API como o jogo a usa.</summary>
public static class AccountTests
{
	private static JsonObject Login(string name, string password) => new() { ["name"] = name, ["password"] = password };

	public static readonly (string, Func<Task>)[] All =
	{
		("saúde: /health diz que está de pé e que o banco responde", async () =>
		{
			await using World w = await World.Start();
			var (status, body) = await w.Server().Call(HttpMethod.Get, "/health");
			Check.Equal(200, status, "status");
			Check.Equal("ok", body["status"].GetValue<string>(), "estado");
			Check.Equal(Banco.Schema.Latest, body["schema"].GetValue<int>(), "versão do banco");
		}),

		("conta: criar já entra, com o perfil inicial (3000 Presas, XP 0)", async () =>
		{
			await using World w = await World.Start();
			Player p = await w.NewPlayer("Jhon_01");
			var (status, profile) = await p.Call(HttpMethod.Get, "/v1/profile");
			Check.Equal(200, status, "ler o perfil com o token do cadastro");
			Check.Equal("Jhon_01", profile["name"].GetValue<string>(), "nome");
			Check.Equal(3000L, profile["presas"].GetValue<long>(), "Presas iniciais");
			Check.Equal(0L, profile["xp"].GetValue<long>(), "XP inicial");
		}),

		("conta: nome repetido (mesmo com outra caixa), nome inválido e senha fraca são recusados", async () =>
		{
			await using World w = await World.Start();
			await w.NewPlayer("Matilha");
			Player anon = w.Server();

			var (repeated, body) = await anon.Call(HttpMethod.Post, "/v1/accounts", Login("MATILHA", "senha-boa-123"));
			Check.Equal(409, repeated, "nome repetido");
			Check.Equal("nome_em_uso", body["error"]["code"].GetValue<string>(), "código do erro");

			Check.Equal(400, (await anon.Call(HttpMethod.Post, "/v1/accounts", Login("ab", "senha-boa-123"))).Status, "nome curto");
			Check.Equal(400, (await anon.Call(HttpMethod.Post, "/v1/accounts", Login("nome com espaço", "senha-boa-123"))).Status, "nome com espaço");
			Check.Equal(400, (await anon.Call(HttpMethod.Post, "/v1/accounts", Login("Valido", "curta"))).Status, "senha curta");
			Check.Equal(400, (await anon.Call(HttpMethod.Post, "/v1/accounts", new JsonObject { ["name"] = 5 })).Status, "corpo torto");
		}),

		("login: senha certa entra, errada não; sair invalida o token; a senha não fica no banco", async () =>
		{
			await using World w = await World.Start();
			Player p = await w.NewPlayer("Bruto");

			var (ok, body) = await w.Server().Call(HttpMethod.Post, "/v1/sessions", Login("bruto", "senha-boa-123"));
			Check.Equal(200, ok, "login com o nome em minúsculas");
			string second = body["token"].GetValue<string>();
			Check.True(second != p.Token, "cada login tem o seu token");

			Check.Equal(401, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", Login("Bruto", "senha-errada"))).Status, "senha errada");
			Check.Equal(401, (await w.Server().Call(HttpMethod.Post, "/v1/sessions", Login("Ninguem", "senha-boa-123"))).Status, "conta que não existe");
			Check.Equal(401, (await w.Server().Call(HttpMethod.Get, "/v1/profile")).Status, "sem token");

			Check.Equal(204, (await p.Call(HttpMethod.Delete, "/v1/sessions")).Status, "sair");
			Check.Equal(401, (await p.Call(HttpMethod.Get, "/v1/profile")).Status, "o token antigo não vale mais");

			Banco.PgResult stored = await w.Db.Query("select pass_hash from accounts where name_key = 'bruto'");
			Check.True(!stored.Text(0, "pass_hash").Contains("senha-boa-123") && stored.Text(0, "pass_hash").StartsWith("pbkdf2$"), "só o hash da senha no banco");
			Banco.PgResult tokens = await w.Db.Query("select count(*) as n from sessions where token_hash = $1", second);
			Check.Equal(0, tokens.Int(0, "n"), "o token não fica guardado em claro");
		}),

		("login: tentativas demais do mesmo endereço levam 429", async () =>
		{
			await using World w = await World.Start(c => c.LoginsPerMinute = 5);
			int last = 0;
			for (int i = 0; i < 7; i++)
				last = (await w.Server().Call(HttpMethod.Post, "/v1/sessions", Login("Alguem", "senha-errada"))).Status;
			Check.Equal(429, last, "a sétima tentativa");
		}),

		("perfil: os dados do jogador vão e voltam; XP e Presas não se mudam por aqui", async () =>
		{
			await using World w = await World.Start();
			Player p = await w.NewPlayer();
			var data = new JsonObject { ["breed"] = "raca_bruto", ["sets"] = new JsonArray("m16a2", "ak47"), ["xp"] = 999999 };
			Check.Equal(204, (await p.Call(HttpMethod.Put, "/v1/profile/data", new JsonObject { ["data"] = data })).Status, "guardar");

			var (_, profile) = await p.Call(HttpMethod.Get, "/v1/profile");
			Check.Equal("raca_bruto", profile["data"]["breed"].GetValue<string>(), "voltou o que foi guardado");
			Check.Equal(0L, profile["xp"].GetValue<long>(), "o XP de verdade não mudou");

			var big = new JsonObject { ["data"] = new JsonObject { ["lixo"] = new string('x', 70_000) } };
			Check.Equal(413, (await p.Call(HttpMethod.Put, "/v1/profile/data", big)).Status, "perfil grande demais");
		}),
	};
}
