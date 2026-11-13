using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alcateia.Servidor.Banco;

namespace Alcateia.Servidor;

/// <summary>Quem está falando: a conta por trás de um token.</summary>
public sealed record Account(long Id, string Name);

/// <summary>Um pedido que não dá para atender, com o código HTTP e uma
/// frase para mostrar ao jogador.</summary>
public sealed class ApiException : Exception
{
	public int Status { get; }
	public string Code { get; }

	public ApiException(int status, string code, string message) : base(message)
	{
		Status = status;
		Code = code;
	}
}

/// <summary>
/// Contas e logins. O nome é único sem diferenciar maiúsculas (guarda-se
/// como foi escrito e, à parte, em minúsculas); o login devolve um token
/// que vale <see cref="Config.SessionDays"/> dias e pode ser revogado.
/// </summary>
public sealed class Accounts
{
	public const int NameMin = 3, NameMax = 16;
	public const long StarterPresas = 3000;

	private static readonly Regex ValidName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
	private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(30);

	private readonly PgPool _db;
	private readonly Config _config;
	private readonly ConcurrentDictionary<string, (Account Account, DateTime Until)> _cache = new();

	public Accounts(PgPool db, Config config)
	{
		_db = db;
		_config = config;
	}

	public async Task<(Account Account, string Token)> Register(string name, string password)
	{
		name = (name ?? "").Trim();
		if (name.Length < NameMin || name.Length > NameMax || !ValidName.IsMatch(name))
			throw new ApiException(400, "nome_invalido", $"O nome tem de {NameMin} a {NameMax} letras, números ou _.");
		if (password == null || password.Length < Passwords.MinLength || password.Length > Passwords.MaxLength)
			throw new ApiException(400, "senha_fraca", $"A senha precisa de pelo menos {Passwords.MinLength} caracteres.");

		string hash = Passwords.Hash(password);
		try
		{
			long id = await _db.Transaction(async c =>
			{
				PgResult made = await c.Query(
					"insert into accounts (name, name_key, pass_hash, last_login_at) values ($1, $2, $3, now()) returning id",
					name, name.ToLowerInvariant(), hash);
				long account = made.Long(0, "id");
				await c.Query("insert into profiles (account_id, presas) values ($1, $2)", account, StarterPresas);
				return account;
			});

			var created = new Account(id, name);
			return (created, await NewSession(created));
		}
		catch (PgException e) when (e.IsUniqueViolation)
		{
			throw new ApiException(409, "nome_em_uso", "Já existe uma conta com esse nome.");
		}
	}

	public async Task<(Account Account, string Token)> Login(string name, string password)
	{
		PgResult found = await _db.Query("select id, name, pass_hash from accounts where name_key = $1",
			(name ?? "").Trim().ToLowerInvariant());

		// A mesma resposta para "não existe" e "senha errada": quem tenta
		// adivinhar não descobre quais nomes existem.
		if (found.Empty || password == null || !Passwords.Verify(password, found.Text(0, "pass_hash")))
			throw new ApiException(401, "login_invalido", "Nome ou senha incorretos.");

		var account = new Account(found.Long(0, "id"), found.Text(0, "name"));
		await _db.Exec("update accounts set last_login_at = now() where id = $1", account.Id);
		return (account, await NewSession(account));
	}

	/// <summary>A conta dona do token, ou null se ele não vale (mais).</summary>
	public async Task<Account> FromToken(string token)
	{
		if (string.IsNullOrEmpty(token) || token.Length > 128) return null;

		string hash = Passwords.TokenHash(token);
		if (_cache.TryGetValue(hash, out var cached) && cached.Until > DateTime.UtcNow) return cached.Account;

		PgResult found = await _db.Query(
			"select a.id, a.name from sessions s join accounts a on a.id = s.account_id where s.token_hash = $1 and s.expires_at > now()",
			hash);
		if (found.Empty)
		{
			_cache.TryRemove(hash, out _);
			return null;
		}

		var account = new Account(found.Long(0, "id"), found.Text(0, "name"));
		_cache[hash] = (account, DateTime.UtcNow + CacheLife);
		return account;
	}

	public async Task Logout(string token)
	{
		string hash = Passwords.TokenHash(token);
		_cache.TryRemove(hash, out _);
		await _db.Exec("delete from sessions where token_hash = $1", hash);
	}

	private async Task<string> NewSession(Account account)
	{
		string token = Passwords.NewToken();
		await _db.Exec("insert into sessions (token_hash, account_id, expires_at) values ($1, $2, now() + make_interval(days => $3::int))",
			Passwords.TokenHash(token), account.Id, _config.SessionDays);

		// Aproveita para varrer os logins vencidos desta conta.
		await _db.Exec("delete from sessions where account_id = $1 and expires_at < now()", account.Id);
		return token;
	}

	// ------------------------------------------------------------ perfil

	/// <summary>O perfil como vai para o cliente: o que o servidor manda
	/// (XP, Presas, estatísticas) e o que é só do jogador (<c>data</c>).</summary>
	public async Task<JsonObject> Profile(long accountId)
	{
		PgResult p = await _db.Query(
			"select a.name, a.created_at, p.* from profiles p join accounts a on a.id = p.account_id where p.account_id = $1", accountId);
		if (p.Empty) throw new ApiException(404, "sem_perfil", "Conta sem perfil.");

		var stats = new JsonObject();
		foreach (string column in new[] { "matches", "wins", "losses", "draws", "kills", "deaths", "assists", "headshots", "mvps" })
			stats[column] = p.Int(0, column);

		return new JsonObject
		{
			["id"] = accountId,
			["name"] = p.Text(0, "name"),
			["createdAt"] = p.Text(0, "created_at"),
			["xp"] = p.Long(0, "xp"),
			["presas"] = p.Long(0, "presas"),
			["stats"] = stats,
			["data"] = JsonNode.Parse(p.Text(0, "data"))
		};
	}

	/// <summary>Guarda a parte do perfil que é só do jogador (conjuntos,
	/// personagem). XP e Presas não entram por aqui.</summary>
	public async Task SaveData(long accountId, JsonNode data)
	{
		string text = data?.ToJsonString() ?? "{}";
		if (text.Length > 64 * 1024) throw new ApiException(413, "grande_demais", "Perfil grande demais.");
		await _db.Exec("update profiles set data = $2::jsonb, updated_at = now() where account_id = $1", accountId, text);
	}

	public async Task<JsonArray> Ranking(int limit)
	{
		PgResult top = await _db.Query(
			"select a.name, p.xp, p.kills, p.deaths, p.wins, p.matches from profiles p join accounts a on a.id = p.account_id " +
			"order by p.xp desc, a.id limit $1", Math.Clamp(limit, 1, 100));

		var list = new JsonArray();
		for (int i = 0; i < top.Rows.Count; i++)
			list.Add(new JsonObject
			{
				["position"] = i + 1,
				["name"] = top.Text(i, "name"),
				["xp"] = top.Long(i, "xp"),
				["kills"] = top.Int(i, "kills"),
				["deaths"] = top.Int(i, "deaths"),
				["wins"] = top.Int(i, "wins"),
				["matches"] = top.Int(i, "matches")
			});
		return list;
	}
}

/// <summary>
/// Freio de tentativas por endereço: tantas por minuto, e depois 429.
/// Em memória — some ao reiniciar, e está certo: é freio, não registro.
/// </summary>
public sealed class RateLimit
{
	private readonly int _perMinute;
	private readonly ConcurrentDictionary<string, (int Count, DateTime Window)> _hits = new();

	public RateLimit(int perMinute) => _perMinute = perMinute;

	public bool Allow(string key)
	{
		DateTime now = DateTime.UtcNow;
		var entry = _hits.AddOrUpdate(key, _ => (1, now),
			(_, old) => now - old.Window > TimeSpan.FromMinutes(1) ? (1, now) : (old.Count + 1, old.Window));

		if (_hits.Count > 10_000)
			foreach (var (k, v) in _hits)
				if (now - v.Window > TimeSpan.FromMinutes(1)) _hits.TryRemove(k, out _);

		return entry.Count <= _perMinute;
	}
}
