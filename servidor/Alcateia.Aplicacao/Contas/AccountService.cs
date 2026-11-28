using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Seguranca;

namespace Alcateia.Aplicacao.Contas;

/// <summary>
/// Contas e logins. O login devolve um token que vale tantos dias e pode
/// ser revogado; o servidor guarda só o hash dele. Quem é o dono de um
/// token fica meio minuto em memória: o saguão e cada pedido perguntam
/// isso o tempo todo.
///
/// **Verificação em duas etapas** (Jhon, 27/11: "dê opção para o player
/// cadastrar 2 fatores"): quem quiser liga um aplicativo autenticador
/// (Google Authenticator, Authy, 1Password — TOTP, <see cref="Totp"/>) e daí
/// em diante o login pede, além da senha, o código de 6 dígitos dele. É
/// opcional e por conta. O segredo fica cifrado com a chave mestra
/// (<see cref="Cipher"/>), como o dos administradores do painel. Os logins
/// que já existiam continuam valendo: a segunda etapa é de quem entra com
/// senha. Quem perde o celular pede ao suporte (o painel desliga:
/// <see cref="ResetTwoFactor"/>).
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

	/// <summary>Cifra o segredo da segunda etapa. Sem ela (um serviço montado
	/// sem chave mestra), a segunda etapa não se liga.</summary>
	public ICipher Cipher { get; set; }

	/// <summary>O nome que aparece no aplicativo autenticador.</summary>
	public const string TotpIssuer = "WolfLeague";

	public async Task<(Account Account, string Token)> Register(string name, string password)
	{
		name = AccountRules.CheckNew(name, password);
		Account created = await _accounts.TryCreate(name, AccountRules.Key(name), _passwords.Hash(password), StarterCash?.Invoke() ?? AccountRules.StarterPresas)
			?? throw new Failure(FailureKind.Conflict, "nome_em_uso", "Já existe uma conta com esse nome.");
		return (created, await NewSession(created));
	}

	/// <summary><paramref name="code"/>: o código do aplicativo autenticador,
	/// para a conta que ligou a segunda etapa. Sem ele, essa conta recebe
	/// <c>codigo_necessario</c> (a senha estava certa: a tela pede o código e
	/// manda de novo); errado, <c>codigo_invalido</c>.</summary>
	public async Task<(Account Account, string Token)> Login(string name, string password, string code = null)
	{
		var found = await _accounts.FindByName(AccountRules.Key(name));

		// A mesma resposta para "não existe" e "senha errada": quem tenta
		// adivinhar não descobre quais nomes existem.
		if (found == null || password == null || !_passwords.Verify(password, found.Value.PasswordHash))
			throw new Failure(FailureKind.Unauthorized, "login_invalido", "Nome ou senha incorretos.");

		Account account = found.Value.Account;
		await RequireNotBanned(account.Id);
		await RequireCode(account.Id, code);
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

	// ------------------------------------------------------------ segunda etapa

	private async Task RequireCode(long accountId, string code)
	{
		(string secret, bool enabled) = await _accounts.Totp(accountId);
		if (!enabled || secret == null || Cipher == null) return;
		if (string.IsNullOrWhiteSpace(code))
			throw new Failure(FailureKind.Unauthorized, "codigo_necessario", "Esta conta tem verificação em duas etapas: digite o código de 6 dígitos do aplicativo autenticador.");
		if (!Totp.Verify(Cipher.Decrypt(secret), code, _clock.UtcNow))
			throw new Failure(FailureKind.Unauthorized, "codigo_invalido", "Código incorreto. Confira o aplicativo autenticador e tente de novo.");
	}

	/// <summary>Começa a ligar a segunda etapa: sorteia o segredo e devolve-o
	/// com o endereço que vira QR code. Só passa a valer com <see cref="ConfirmTwoFactor"/>.</summary>
	public async Task<(string Secret, string Uri)> StartTwoFactor(Account account)
	{
		if (Cipher == null) throw new Failure(FailureKind.Unavailable, "duas_etapas_indisponivel", "A verificação em duas etapas está fora do ar.");
		if ((await _accounts.Totp(account.Id)).Enabled)
			throw new Failure(FailureKind.Conflict, "ja_ligada", "A verificação em duas etapas já está ligada nesta conta.");
		string secret = Totp.NewSecret();
		await _accounts.SetTotp(account.Id, Cipher.Encrypt(secret), false);
		return (secret, Totp.Uri(TotpIssuer, account.Name, secret));
	}

	/// <summary>Confere o primeiro código do aplicativo e liga a segunda etapa.</summary>
	public async Task ConfirmTwoFactor(long accountId, string code)
	{
		(string secret, bool enabled) = await _accounts.Totp(accountId);
		if (enabled) throw new Failure(FailureKind.Conflict, "ja_ligada", "A verificação em duas etapas já está ligada nesta conta.");
		if (secret == null || Cipher == null) throw new Failure(FailureKind.Conflict, "sem_segredo", "Comece de novo a ligar a verificação em duas etapas.");
		if (!Totp.Verify(Cipher.Decrypt(secret), code, _clock.UtcNow))
			throw new Failure(FailureKind.Invalid, "codigo_invalido", "Código incorreto. Confira o aplicativo autenticador e tente de novo.");
		await _accounts.SetTotp(accountId, secret, true);
	}

	/// <summary>Desliga a segunda etapa. Pede a senha e um código: um login
	/// esquecido aberto num computador não basta para tirar a proteção.</summary>
	public async Task DisableTwoFactor(long accountId, string password, string code)
	{
		(string secret, bool enabled) = await _accounts.Totp(accountId);
		if (!enabled) throw new Failure(FailureKind.Conflict, "nao_ligada", "A verificação em duas etapas não está ligada nesta conta.");
		string hash = await _accounts.PasswordHash(accountId);
		if (hash == null || password == null || !_passwords.Verify(password, hash))
			throw new Failure(FailureKind.Unauthorized, "senha_errada", "Senha incorreta.");
		if (Cipher == null || !Totp.Verify(Cipher.Decrypt(secret), code, _clock.UtcNow))
			throw new Failure(FailureKind.Invalid, "codigo_invalido", "Código incorreto. Confira o aplicativo autenticador e tente de novo.");
		await _accounts.SetTotp(accountId, null, false);
	}

	/// <summary>Desliga a segunda etapa sem senha nem código: é o suporte
	/// atendendo quem perdeu o celular (o painel chama).</summary>
	public Task ResetTwoFactor(long accountId) => _accounts.SetTotp(accountId, null, false);

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

	/// <summary>Guarda a nota de habilidade do jogo rápido (quem calcula é o
	/// saguão, no fim de cada partida de fila).</summary>
	public Task SaveSkill(long accountId, double rating, int matches) => _accounts.SaveSkill(accountId, rating, matches);

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
