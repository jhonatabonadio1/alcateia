using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Painel;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>Os administradores do painel, os logins deles e a auditoria
/// (tabelas admins, admin_sessions e audit_log).</summary>
public sealed class PgAdminRepository : IAdminRepository
{
	private const string Columns = "id, name, role, totp_enabled, disabled, created_at, last_login_at, last_ip";
	private readonly PgPool _db;

	public PgAdminRepository(PgPool db) => _db = db;

	public async Task<int> Count() => (await _db.Query("select count(*) as n from admins")).Int(0, "n");

	public async Task<long?> TryCreate(string name, string nameKey, string passwordHash, string role)
	{
		try
		{
			PgResult r = await _db.Query("insert into admins (name, name_key, pass_hash, role) values ($1, $2, $3, $4) returning id",
				name, nameKey, passwordHash, role);
			return r.Long(0, "id");
		}
		catch (PgException e) when (e.IsUniqueViolation)
		{
			return null;
		}
	}

	public async Task<AdminCredentials> FindByName(string nameKey) =>
		Credentials(await _db.Query("select * from admins where name_key = $1", nameKey));

	public async Task<AdminCredentials> FindById(long id) =>
		Credentials(await _db.Query("select * from admins where id = $1", id));

	public async Task<AdminUser> FindBySession(string tokenHash)
	{
		PgResult r = await _db.Query(
			$"select a.id, a.name, a.role, a.totp_enabled, a.disabled, a.created_at, a.last_login_at, a.last_ip " +
			"from admin_sessions s join admins a on a.id = s.admin_id where s.token_hash = $1 and s.expires_at > now() and not a.disabled", tokenHash);
		return r.Empty ? null : User(r, 0);
	}

	public async Task AddSession(string tokenHash, long adminId, DateTime expiresAt, string ip, string userAgent)
	{
		await _db.Exec("insert into admin_sessions (token_hash, admin_id, expires_at, ip, user_agent) values ($1, $2, $3, $4, $5)",
			tokenHash, adminId, expiresAt, ip ?? "", Cut(userAgent, 200));
		await _db.Exec("delete from admin_sessions where expires_at < now()");
	}

	public Task RemoveSession(string tokenHash) => _db.Exec("delete from admin_sessions where token_hash = $1", tokenHash);
	public Task RemoveSessions(long adminId) => _db.Exec("delete from admin_sessions where admin_id = $1", adminId);

	public Task LoginFailed(long adminId, int failedLogins, DateTime? lockedUntil) =>
		_db.Exec("update admins set failed_logins = $2, locked_until = $3 where id = $1", adminId, failedLogins, lockedUntil);

	public Task LoginOk(long adminId, string ip) =>
		_db.Exec("update admins set failed_logins = 0, locked_until = null, last_login_at = now(), last_ip = $2 where id = $1", adminId, ip ?? "");

	public Task SetTotp(long adminId, string secretCipher, bool enabled) =>
		_db.Exec("update admins set totp_secret = $2, totp_enabled = $3 where id = $1", adminId, secretCipher, enabled);

	public Task SetPassword(long adminId, string passwordHash) =>
		_db.Exec("update admins set pass_hash = $2, failed_logins = 0, locked_until = null where id = $1", adminId, passwordHash);

	public Task SetRole(long adminId, string role) => _db.Exec("update admins set role = $2 where id = $1", adminId, role);
	public Task SetDisabled(long adminId, bool disabled) => _db.Exec("update admins set disabled = $2 where id = $1", adminId, disabled);

	public async Task<IReadOnlyList<AdminUser>> List()
	{
		PgResult r = await _db.Query($"select {Columns} from admins order by id");
		var list = new List<AdminUser>();
		for (int i = 0; i < r.Rows.Count; i++) list.Add(User(r, i));
		return list;
	}

	public Task Audit(long adminId, string adminName, string action, string target, string detail, string ip) =>
		_db.Exec("insert into audit_log (admin_id, admin_name, action, target, detail, ip) values ($1, $2, $3, $4, $5, $6)",
			adminId == 0 ? null : (object)adminId, adminName, action, target ?? "", Cut(detail, 2000), ip ?? "");

	public async Task<IReadOnlyList<AuditEntry>> AuditLog(int limit, long beforeId, string search)
	{
		var args = new List<object> { beforeId <= 0 ? long.MaxValue : beforeId };
		string where = "id < $1";
		if (!string.IsNullOrWhiteSpace(search))
		{
			args.Add("%" + search.Trim().Replace("%", "").Replace("_", "\\_") + "%");
			where += " and (admin_name ilike $2 or action ilike $2 or target ilike $2 or detail ilike $2)";
		}
		args.Add(Math.Clamp(limit, 1, 500));

		PgResult r = await _db.Query($"select * from audit_log where {where} order by id desc limit ${args.Count}", args.ToArray());
		var list = new List<AuditEntry>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new AuditEntry(r.Long(i, "id"), r.Date(i, "at").Value, r.Text(i, "admin_name"), r.Text(i, "action"),
				r.Text(i, "target"), r.Text(i, "detail"), r.Text(i, "ip")));
		return list;
	}

	private static AdminCredentials Credentials(PgResult r) =>
		r.Empty ? null : new AdminCredentials(User(r, 0), r.Text(0, "pass_hash"), r.Text(0, "totp_secret"), r.Int(0, "failed_logins"), r.Date(0, "locked_until"));

	private static AdminUser User(PgResult r, int i) =>
		new(r.Long(i, "id"), r.Text(i, "name"), r.Text(i, "role"), r.Bool(i, "totp_enabled"), r.Bool(i, "disabled"),
			r.Date(i, "created_at").Value, r.Date(i, "last_login_at"), r.Text(i, "last_ip") ?? "");

	private static string Cut(string text, int max) => text == null ? "" : text.Length <= max ? text : text[..max];
}
