using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Aplicacao.Seguranca;
using Alcateia.Dominio;
using Alcateia.Dominio.Painel;
using Alcateia.Dominio.Seguranca;

namespace Alcateia.Aplicacao.Painel;

/// <summary>Quem está fazendo a ação no painel, e de onde: vai para a auditoria.</summary>
public sealed record AdminContext(AdminUser Admin, string Ip)
{
	/// <summary>Recusa se o papel dele não permite.</summary>
	public void Require(Permission permission)
	{
		if (!Roles.Can(Admin.Role, permission))
			throw new Failure(FailureKind.Forbidden, "sem_permissao", "Seu papel no painel não permite essa ação.");
	}
}

/// <summary>Escreve a auditoria: toda ação sensível do painel fica
/// registrada com quem, o quê, em quem e de onde.</summary>
public sealed class Auditor
{
	private readonly IAdminRepository _admins;

	public Auditor(IAdminRepository admins) => _admins = admins;

	public Task Note(AdminContext who, string action, string target, string detail = "") =>
		_admins.Audit(who.Admin.Id, who.Admin.Name, action, target, detail, who.Ip);

	public Task<IReadOnlyList<AuditEntry>> Log(int limit, long beforeId, string search) => _admins.AuditLog(limit, beforeId, search);
}

/// <summary>
/// As contas do painel. São separadas das de jogador e mais fechadas:
/// senha de 12 ou mais, travamento depois de cinco erros, segunda etapa
/// (TOTP) e sessão de doze horas que se pode derrubar. O erro de login é
/// sempre o mesmo, exista a conta ou não.
/// </summary>
public sealed class AdminAuthService
{
	private readonly IAdminRepository _admins;
	private readonly IPasswordHasher _passwords;
	private readonly ITokens _tokens;
	private readonly ICipher _cipher;
	private readonly IClock _clock;
	private readonly SecurityService _security;
	private readonly Auditor _audit;
	private readonly string _dummyHash;

	/// <summary>Sem a segunda etapa ligada, o administrador só consegue ligá-la.</summary>
	public bool RequireTotp { get; set; }

	public AdminAuthService(IAdminRepository admins, IPasswordHasher passwords, ITokens tokens, ICipher cipher, IClock clock,
		SecurityService security, Auditor audit)
	{
		_admins = admins;
		_passwords = passwords;
		_tokens = tokens;
		_cipher = cipher;
		_clock = clock;
		_security = security;
		_audit = audit;
		// Para gastar o mesmo tempo quando a conta não existe.
		_dummyHash = passwords.Hash(tokens.NewToken());
	}

	/// <summary>Cria o primeiro administrador (o dono), se ainda não há nenhum.</summary>
	public async Task<bool> Bootstrap(string name, string password)
	{
		if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password) || await _admins.Count() > 0) return false;
		name = AdminRules.CheckNew(name, password, Roles.Owner);
		return await _admins.TryCreate(name, name.ToLowerInvariant(), _passwords.Hash(password), Roles.Owner) != null;
	}

	/// <summary>Cria ou conserta um administrador pela linha de comando do
	/// servidor: senha nova, segunda etapa desligada, destravado. É a
	/// saída para quem se trancou fora.</summary>
	public async Task Rescue(string name, string password, string role)
	{
		name = AdminRules.CheckNew(name, password, role);
		AdminCredentials existing = await _admins.FindByName(name.ToLowerInvariant());
		if (existing == null)
		{
			await _admins.TryCreate(name, name.ToLowerInvariant(), _passwords.Hash(password), role);
			return;
		}
		long id = existing.User.Id;
		await _admins.SetPassword(id, _passwords.Hash(password));
		await _admins.SetTotp(id, null, false);
		await _admins.SetRole(id, role);
		await _admins.SetDisabled(id, false);
		await _admins.RemoveSessions(id);
	}

	public async Task<(AdminUser Admin, string Token)> Login(string name, string password, string code, IPAddress address, string ip, string userAgent)
	{
		const string path = "/admin/v1/login";
		name = (name ?? "").Trim();
		AdminCredentials found = name.Length is > 0 and <= 32 ? await _admins.FindByName(name.ToLowerInvariant()) : null;
		DateTime now = _clock.UtcNow;

		bool locked = found?.LockedUntil > now;
		bool passwordOk = _passwords.Verify(password ?? "", found?.PasswordHash ?? _dummyHash) && found != null;

		if (found == null || found.User.Disabled || locked || !passwordOk)
		{
			if (found != null && !locked && !found.User.Disabled) await CountFailure(found, ip);
			_security.LoginFailed(address, ip, name, path, admin: true);
			throw Invalid();
		}

		if (found.User.TotpEnabled)
		{
			if (string.IsNullOrWhiteSpace(code))
				throw new Failure(FailureKind.Unauthorized, "codigo_necessario", "Digite o código do aplicativo autenticador.");
			if (!Totp.Verify(_cipher.Decrypt(found.TotpSecretCipher), code, now))
			{
				await CountFailure(found, ip);
				_security.LoginFailed(address, ip, name, path, admin: true);
				throw new Failure(FailureKind.Unauthorized, "codigo_invalido", "Código incorreto ou vencido.");
			}
		}

		string token = _tokens.NewToken();
		await _admins.AddSession(_tokens.Hash(token), found.User.Id, now + AdminRules.SessionLife, ip, userAgent);
		await _admins.LoginOk(found.User.Id, ip);
		_security.LoginOk(ip, null, found.User.Name, path, admin: true);
		await _admins.Audit(found.User.Id, found.User.Name, "painel.entrou", "", "", ip);
		return (found.User, token);
	}

	private async Task CountFailure(AdminCredentials found, string ip)
	{
		int failures = found.FailedLogins + 1;
		bool lockNow = failures >= AdminRules.MaxFailedLogins;
		await _admins.LoginFailed(found.User.Id, lockNow ? 0 : failures, lockNow ? _clock.UtcNow + AdminRules.Lockout : null);
		if (lockNow)
			_security.Record(EventKind.AdminLocked, Severity.Critical, ip, actor: found.User.Name,
				detail: $"{failures} erros seguidos; travada por {AdminRules.Lockout.TotalMinutes:0} min");
	}

	private static Failure Invalid() => new(FailureKind.Unauthorized, "login_invalido", "Nome ou senha incorretos.");

	/// <summary>O administrador dono do token, ou null. Sem memória: uma
	/// sessão derrubada para de valer no pedido seguinte.</summary>
	public async Task<AdminUser> FromToken(string token)
	{
		if (string.IsNullOrEmpty(token) || token.Length > 128) return null;
		return await _admins.FindBySession(_tokens.Hash(token));
	}

	public Task Logout(string token) => _admins.RemoveSession(_tokens.Hash(token ?? ""));

	public async Task ChangePassword(AdminContext who, string current, string next)
	{
		AdminCredentials me = await _admins.FindById(who.Admin.Id);
		if (me == null || !_passwords.Verify(current ?? "", me.PasswordHash))
			throw new Failure(FailureKind.Forbidden, "senha_atual_errada", "A senha atual não confere.");
		AdminRules.CheckPassword(next);
		await _admins.SetPassword(me.User.Id, _passwords.Hash(next));
		await _admins.RemoveSessions(me.User.Id);
		await _audit.Note(who, "painel.trocou_senha", me.User.Name);
	}

	// ------------------------------------------------------------ segunda etapa

	/// <summary>Começa a ligar a segunda etapa: devolve o segredo e o
	/// endereço que vira QR code. Só vale depois de <see cref="ConfirmTotp"/>.</summary>
	public async Task<(string Secret, string Uri)> StartTotp(AdminContext who, string issuer)
	{
		if (who.Admin.TotpEnabled) throw new Failure(FailureKind.Conflict, "ja_ligada", "A segunda etapa já está ligada.");
		string secret = Totp.NewSecret();
		await _admins.SetTotp(who.Admin.Id, _cipher.Encrypt(secret), false);
		return (secret, Totp.Uri(issuer, who.Admin.Name, secret));
	}

	public async Task ConfirmTotp(AdminContext who, string code)
	{
		AdminCredentials me = await _admins.FindById(who.Admin.Id);
		if (me?.TotpSecretCipher == null) throw new Failure(FailureKind.Conflict, "sem_segredo", "Comece de novo a ligar a segunda etapa.");
		if (!Totp.Verify(_cipher.Decrypt(me.TotpSecretCipher), code, _clock.UtcNow))
			throw new Failure(FailureKind.Invalid, "codigo_invalido", "Código incorreto ou vencido.");
		await _admins.SetTotp(me.User.Id, me.TotpSecretCipher, true);
		await _audit.Note(who, "painel.ligou_2fa", me.User.Name);
	}

	public async Task DisableTotp(AdminContext who, string password)
	{
		AdminCredentials me = await _admins.FindById(who.Admin.Id);
		if (me == null || !_passwords.Verify(password ?? "", me.PasswordHash))
			throw new Failure(FailureKind.Forbidden, "senha_atual_errada", "A senha não confere.");
		await _admins.SetTotp(me.User.Id, null, false);
		await _audit.Note(who, "painel.desligou_2fa", me.User.Name);
	}

	// ------------------------------------------------------------ os administradores

	public Task<IReadOnlyList<AdminUser>> List(AdminContext who)
	{
		who.Require(Permission.Manage);
		return _admins.List();
	}

	public async Task<long> Create(AdminContext who, string name, string password, string role)
	{
		who.Require(Permission.Manage);
		name = AdminRules.CheckNew(name, password, role);
		long? id = await _admins.TryCreate(name, name.ToLowerInvariant(), _passwords.Hash(password), role);
		if (id == null) throw new Failure(FailureKind.Conflict, "nome_em_uso", "Já existe um administrador com esse nome.");
		await _audit.Note(who, "admin.criou", name, $"papel: {role}");
		return id.Value;
	}

	/// <summary>Muda papel, liga/desliga ou troca a senha de outro
	/// administrador. Nunca deixa o painel sem dono.</summary>
	public async Task Update(AdminContext who, long id, string role, bool? disabled, string newPassword)
	{
		who.Require(Permission.Manage);
		AdminCredentials target = await _admins.FindById(id) ?? throw new Failure(FailureKind.NotFound, "admin_inexistente", "Esse administrador não existe.");

		bool losesOwner = target.User.Role == Roles.Owner && !target.User.Disabled && ((role != null && role != Roles.Owner) || disabled == true);
		if (losesOwner)
		{
			if (id == who.Admin.Id) throw new Failure(FailureKind.Conflict, "voce_mesmo", "Você não pode se rebaixar nem se desligar.");
			int owners = (await _admins.List()).Count(a => a.Role == Roles.Owner && !a.Disabled);
			if (owners <= 1) throw new Failure(FailureKind.Conflict, "ultimo_dono", "O painel não pode ficar sem dono.");
		}

		var changes = new List<string>();
		if (role != null && role != target.User.Role)
		{
			if (!Roles.Valid(role)) throw new Failure(FailureKind.Invalid, "papel_invalido", "Papel inválido.");
			await _admins.SetRole(id, role);
			changes.Add($"papel: {target.User.Role} → {role}");
		}
		if (disabled != null && disabled != target.User.Disabled)
		{
			await _admins.SetDisabled(id, disabled.Value);
			changes.Add(disabled.Value ? "desligado" : "religado");
		}
		if (!string.IsNullOrEmpty(newPassword))
		{
			AdminRules.CheckPassword(newPassword);
			await _admins.SetPassword(id, _passwords.Hash(newPassword));
			await _admins.SetTotp(id, null, false);
			changes.Add("senha trocada e 2FA desligada");
		}
		if (changes.Count == 0) return;

		await _admins.RemoveSessions(id);
		await _audit.Note(who, "admin.alterou", target.User.Name, string.Join("; ", changes));
	}
}
