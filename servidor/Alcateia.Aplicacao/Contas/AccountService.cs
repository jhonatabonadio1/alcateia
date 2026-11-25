using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;

namespace Alcateia.Aplicacao.Contas;

/// <summary>
/// Contas e logins. O login devolve um token que vale tantos dias e pode
/// ser revogado; o servidor guarda só o hash dele. Quem é o dono de um
/// token fica meio minuto em memória: o saguão e cada pedido perguntam
/// isso o tempo todo.
/// </summary>
public sealed class AccountService
{
	private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(30);

	private readonly IAccountRepository _accounts;
	private readonly IPasswordHasher _passwords;
	private readonly ITokens _tokens;
	private readonly IClock _clock;
	private readonly int _sessionDays;
	private readonly ConcurrentDictionary<string, (Account Account, DateTime Until)> _cache = new();

	public AccountService(IAccountRepository accounts, IPasswordHasher passwords, ITokens tokens, IClock clock, int sessionDays)
	{
		_accounts = accounts;
		_passwords = passwords;
		_tokens = tokens;
		_clock = clock;
		_sessionDays = sessionDays;
	}

	/// <summary>O Cash de uma conta nova, lido do painel a cada cadastro (null: o de fábrica).</summary>
	public Func<long> StarterCash { get; set; }

	public async Task<(Account Account, string Token)> Register(string name, string password)
	{
		name = AccountRules.CheckNew(name, password);
		long? id = await _accounts.TryCreate(name, AccountRules.Key(name), _passwords.Hash(password), StarterCash?.Invoke() ?? AccountRules.StarterPresas);
		if (id == null) throw new Failure(FailureKind.Conflict, "nome_em_uso", "Já existe uma conta com esse nome.");

		var created = new Account(id.Value, name);
		return (created, await NewSession(created));
	}

	public async Task<(Account Account, string Token)> Login(string name, string password)
	{
		var found = await _accounts.FindByName(AccountRules.Key(name));

		// A mesma resposta para "não existe" e "senha errada": quem tenta
		// adivinhar não descobre quais nomes existem.
		if (found == null || password == null || !_passwords.Verify(password, found.Value.PasswordHash))
			throw new Failure(FailureKind.Unauthorized, "login_invalido", "Nome ou senha incorretos.");

		Account account = found.Value.Account;
		await RequireNotBanned(account.Id);
		await _accounts.MarkLogin(account.Id);
		return (account, await NewSession(account));
	}

	/// <summary>Troca o nome da conta, gastando um item Troca de nome.
	/// Devolve o nome como ficou.</summary>
	public async Task<string> Rename(long accountId, string name)
	{
		name = AccountRules.CheckName(name);
		await _accounts.Rename(accountId, name, AccountRules.Key(name), AccountRules.RenameItem);
		Forget(accountId);        // os logins em memória ainda dizem o nome antigo
		return name;
	}

	/// <summary>Conta banida não entra: a recusa diz até quando e por quê.</summary>
	public async Task RequireNotBanned(long accountId)
	{
		BanStatus ban = await _accounts.Ban(accountId);
		if (ban == null) return;
		string until = ban.Until == null ? "permanentemente" : $"até {ban.Until.Value:dd/MM/yyyy HH:mm} (UTC)";
		string reason = string.IsNullOrWhiteSpace(ban.Reason) ? "" : $" Motivo: {ban.Reason}";
		throw new Failure(FailureKind.Forbidden, "conta_banida", $"Esta conta está banida {until}.{reason}");
	}

	/// <summary>Esquece os logins em memória de uma conta: quem foi banido
	/// ou teve a sessão derrubada para de valer agora, e não daqui a meio
	/// minuto.</summary>
	public void Forget(long accountId)
	{
		foreach (var entry in _cache)
			if (entry.Value.Account.Id == accountId) _cache.TryRemove(entry.Key, out _);
	}

	/// <summary>A conta dona do token, ou null se ele não vale (mais).</summary>
	public async Task<Account> FromToken(string token)
	{
		if (string.IsNullOrEmpty(token) || token.Length > 128) return null;

		string hash = _tokens.Hash(token);
		if (_cache.TryGetValue(hash, out var cached) && cached.Until > _clock.UtcNow) return cached.Account;

		Account account = await _accounts.FindBySession(hash);
		if (account == null)
		{
			_cache.TryRemove(hash, out _);
			return null;
		}

		_cache[hash] = (account, _clock.UtcNow + CacheLife);
		return account;
	}

	/// <summary>Como <see cref="FromToken"/>, mas sem conta é recusa.</summary>
	public async Task<Account> Require(string token) =>
		await FromToken(token) ?? throw new Failure(FailureKind.Unauthorized, "sem_login", "Entre na conta primeiro.");

	public async Task Logout(string token)
	{
		string hash = _tokens.Hash(token ?? "");
		_cache.TryRemove(hash, out _);
		await _accounts.RemoveSession(hash);
	}

	public async Task<Profile> Profile(long accountId) =>
		await _accounts.Profile(accountId) ?? throw new Failure(FailureKind.NotFound, "sem_perfil", "Conta sem perfil.");

	/// <summary>Guarda a parte do perfil que é só do jogador (conjuntos,
	/// personagem). XP e Presas não entram por aqui.</summary>
	public async Task SaveData(long accountId, string dataJson)
	{
		dataJson ??= "{}";
		if (dataJson.Length > Dominio.Contas.Profile.MaxData) throw new Failure(FailureKind.TooLarge, "grande_demais", "Perfil grande demais.");
		await _accounts.SaveData(accountId, dataJson);
	}

	public Task<IReadOnlyList<RankingEntry>> Ranking(int limit) => _accounts.Ranking(Math.Clamp(limit, 1, 100));

	private async Task<string> NewSession(Account account)
	{
		string token = _tokens.NewToken();
		await _accounts.AddSession(_tokens.Hash(token), account.Id, _sessionDays);
		return token;
	}
}
