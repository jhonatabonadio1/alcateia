using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>Contas, logins e perfis no PostgreSQL (tabelas accounts,
/// sessions e profiles, em <see cref="Schema"/>).</summary>
public sealed class PgAccountRepository : IAccountRepository
{
	private readonly PgPool _db;

	public PgAccountRepository(PgPool db) => _db = db;

	public async Task<Account> TryCreate(string name, string nameKey, string passwordHash, long starterPresas)
	{
		try
		{
			return await _db.Transaction(async c =>
			{
				// A tag é sorteada pelo banco (o padrão da coluna, em Schema).
				PgResult made = await c.Query(
					"insert into accounts (name, name_key, pass_hash, last_login_at) values ($1, $2, $3, now()) returning id, tag",
					name, nameKey, passwordHash);
				long account = made.Long(0, "id");
				await c.Query("insert into profiles (account_id, presas) values ($1, $2)", account, starterPresas);
				return new Account(account, name, made.Int(0, "tag"));
			});
		}
		catch (PgException e) when (e.IsUniqueViolation)
		{
			return null;
		}
	}

	public async Task<(Account Account, string PasswordHash)?> FindByName(string nameKey)
	{
		PgResult found = await _db.Query("select id, name, tag, pass_hash from accounts where name_key = $1", nameKey);
		if (found.Empty) return null;
		return (new Account(found.Long(0, "id"), found.Text(0, "name"), found.Int(0, "tag")), found.Text(0, "pass_hash"));
	}

	public Task MarkLogin(long accountId) => _db.Exec("update accounts set last_login_at = now() where id = $1", accountId);

	public async Task<Account> FindBySession(string tokenHash)
	{
		PgResult found = await _db.Query(
			"select a.id, a.name, a.tag from sessions s join accounts a on a.id = s.account_id where s.token_hash = $1 and s.expires_at > now()",
			tokenHash);
		return found.Empty ? null : new Account(found.Long(0, "id"), found.Text(0, "name"), found.Int(0, "tag"));
	}

	public async Task AddSession(string tokenHash, long accountId, int days)
	{
		await _db.Exec("insert into sessions (token_hash, account_id, expires_at) values ($1, $2, now() + make_interval(days => $3::int))",
			tokenHash, accountId, days);

		// Aproveita para varrer os logins vencidos desta conta.
		await _db.Exec("delete from sessions where account_id = $1 and expires_at < now()", accountId);
	}

	public Task RemoveSession(string tokenHash) => _db.Exec("delete from sessions where token_hash = $1", tokenHash);

	public async Task<Profile> Profile(long accountId)
	{
		PgResult p = await _db.Query(
			"select a.name, a.tag, a.totp_enabled, a.created_at, p.* from profiles p join accounts a on a.id = p.account_id where p.account_id = $1", accountId);
		if (p.Empty) return null;

		var stats = Dominio.Contas.Profile.StatNames.ToDictionary(column => column, column => p.Int(0, column));
		return new Profile(accountId, p.Text(0, "name"), p.Text(0, "created_at"), p.Long(0, "xp"), p.Long(0, "presas"), stats, p.Text(0, "data"))
		{
			Sangue = p.Long(0, "sangue"),
			Tag = p.Int(0, "tag"),
			TwoFactor = p.Bool(0, "totp_enabled"),
			Skill = p.Null(0, "skill") ? null : double.Parse(p.Text(0, "skill"), System.Globalization.CultureInfo.InvariantCulture),
			SkillMatches = p.Int(0, "skill_matches")
		};
	}

	public Task SaveData(long accountId, string dataJson) =>
		_db.Exec("update profiles set data = $2::jsonb, updated_at = now() where account_id = $1", accountId, dataJson);

	public async Task Rename(long accountId, string name, string nameKey, string itemId)
	{
		try
		{
			await _db.Transaction(async c =>
			{
				PgResult used = await c.Query(
					"update inventory set uses = uses - 1 where account_id = $1 and item_id = $2 and uses > 0 returning uses", accountId, itemId);
				if (used.Empty) throw new Failure(FailureKind.Conflict, "sem_troca_de_nome", "Você precisa do item Troca de nome, da loja.");
				if (used.Int(0, "uses") == 0) await c.Query("delete from inventory where account_id = $1 and item_id = $2", accountId, itemId);

				await c.Query("update accounts set name = $2, name_key = $3 where id = $1", accountId, name, nameKey);
				return 0;
			});
		}
		catch (PgException e) when (e.IsUniqueViolation)
		{
			// A transação desfez o gasto do item.
			throw new Failure(FailureKind.Conflict, "nome_em_uso", "Já existe uma conta com esse nome.");
		}
	}

	public async Task<BanStatus> Ban(long accountId)
	{
		PgResult r = await _db.Query(
			"select banned_until, ban_reason from accounts where id = $1 and banned and (banned_until is null or banned_until > now())", accountId);
		return r.Empty ? null : new BanStatus(r.Date(0, "banned_until"), r.Text(0, "ban_reason") ?? "");
	}

	public Task SaveSkill(long accountId, double rating, int matches) =>
		_db.Exec("update profiles set skill = $2::double precision, skill_matches = $3 where account_id = $1",
			accountId, rating.ToString("R", System.Globalization.CultureInfo.InvariantCulture), matches);

	public async Task<(string SecretCipher, bool Enabled)> Totp(long accountId)
	{
		PgResult r = await _db.Query("select totp_secret, totp_enabled from accounts where id = $1", accountId);
		return r.Empty ? (null, false) : (r.Text(0, "totp_secret"), r.Bool(0, "totp_enabled"));
	}

	public Task SetTotp(long accountId, string secretCipher, bool enabled) =>
		_db.Exec("update accounts set totp_secret = $2, totp_enabled = $3 where id = $1", accountId, secretCipher, enabled);

	public async Task<string> PasswordHash(long accountId)
	{
		PgResult r = await _db.Query("select pass_hash from accounts where id = $1", accountId);
		return r.Empty ? null : r.Text(0, "pass_hash");
	}

	public async Task<IReadOnlyList<RankingEntry>> Ranking(int limit)
	{
		PgResult top = await _db.Query(
			"select a.name, p.xp, p.kills, p.deaths, p.wins, p.matches from profiles p join accounts a on a.id = p.account_id " +
			"where not (a.banned and (a.banned_until is null or a.banned_until > now())) order by p.xp desc, a.id limit $1", limit);

		var list = new List<RankingEntry>();
		for (int i = 0; i < top.Rows.Count; i++)
			list.Add(new RankingEntry(i + 1, top.Text(i, "name"), top.Long(i, "xp"), top.Int(i, "kills"),
				top.Int(i, "deaths"), top.Int(i, "wins"), top.Int(i, "matches")));
		return list;
	}
}
