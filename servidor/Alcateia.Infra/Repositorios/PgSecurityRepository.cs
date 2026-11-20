using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Seguranca;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>Blacklist de IP, eventos de segurança e banimentos no
/// PostgreSQL (tabelas ip_blocks, security_events e bans).</summary>
public sealed class PgSecurityRepository : ISecurityRepository
{
	private const int EventsPerInsert = 100;
	private readonly PgPool _db;

	public PgSecurityRepository(PgPool db) => _db = db;

	public async Task<IReadOnlyList<IpBlock>> IpBlocks()
	{
		PgResult r = await _db.Query("select * from ip_blocks where until is null or until > now() order by id desc");
		var list = new List<IpBlock>();
		for (int i = 0; i < r.Rows.Count; i++) list.Add(Block(r, i));
		return list;
	}

	public async Task<IpBlock> AddIpBlock(string cidr, string reason, string source, DateTime? until, string adminName)
	{
		PgResult r = await _db.Query(
			"insert into ip_blocks (cidr, reason, source, until, admin_name) values ($1, $2, $3, $4, $5) " +
			"on conflict (cidr) do update set reason = excluded.reason, source = excluded.source, until = excluded.until, " +
			"admin_name = excluded.admin_name, created_at = now() returning *",
			cidr, reason, source, until, adminName ?? "");
		return Block(r, 0);
	}

	public async Task<IpBlock> RemoveIpBlock(long id)
	{
		PgResult r = await _db.Query("delete from ip_blocks where id = $1 returning *", id);
		return r.Empty ? null : Block(r, 0);
	}

	public async Task AddEvents(IReadOnlyList<SecurityEvent> events)
	{
		for (int from = 0; from < events.Count; from += EventsPerInsert)
		{
			int count = Math.Min(EventsPerInsert, events.Count - from);
			var sql = new StringBuilder("insert into security_events (at, kind, severity, ip, account_id, actor, path, detail) values ");
			var args = new object[count * 8];
			for (int i = 0; i < count; i++)
			{
				SecurityEvent e = events[from + i];
				int p = i * 8;
				if (i > 0) sql.Append(',');
				sql.Append($"(${p + 1}, ${p + 2}, ${p + 3}, ${p + 4}, ${p + 5}, ${p + 6}, ${p + 7}, ${p + 8})");
				args[p] = e.At; args[p + 1] = e.Kind; args[p + 2] = e.Severity; args[p + 3] = e.Ip ?? "";
				args[p + 4] = e.AccountId; args[p + 5] = e.Actor ?? ""; args[p + 6] = e.Path ?? ""; args[p + 7] = e.Detail ?? "";
			}
			await _db.Query(sql.ToString(), args);
		}
	}

	public async Task<IReadOnlyList<SecurityEvent>> Events(EventFilter f)
	{
		var where = new List<string> { "severity >= $1" };
		var args = new List<object> { f.MinSeverity };
		if (!string.IsNullOrEmpty(f.KindPrefix)) { args.Add(f.KindPrefix + "%"); where.Add($"kind like ${args.Count}"); }
		if (!string.IsNullOrEmpty(f.Ip)) { args.Add(f.Ip); where.Add($"ip = ${args.Count}"); }
		if (f.BeforeId > 0) { args.Add(f.BeforeId); where.Add($"id < ${args.Count}"); }
		if (f.AccountId != null) { args.Add(f.AccountId.Value); where.Add($"account_id = ${args.Count}"); }
		args.Add(Math.Clamp(f.Limit, 1, 500));

		PgResult r = await _db.Query(
			$"select * from security_events where {string.Join(" and ", where)} order by id desc limit ${args.Count}", args.ToArray());
		var list = new List<SecurityEvent>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new SecurityEvent(r.Date(i, "at").Value, r.Text(i, "kind"), r.Int(i, "severity"), r.Text(i, "ip"),
				r.Null(i, "account_id") ? null : r.Long(i, "account_id"), r.Text(i, "actor"), r.Text(i, "path"), r.Text(i, "detail"))
			{ Id = r.Long(i, "id") });
		return list;
	}

	public async Task<SecurityOverview> Overview(int hours)
	{
		hours = Math.Clamp(hours, 1, 24 * 30);
		PgResult h = await _db.Query(
			"select date_trunc('hour', at) as hour, " +
			"count(*) filter (where severity = 0) as info, count(*) filter (where severity = 1) as warning, " +
			"count(*) filter (where severity = 2) as high, count(*) filter (where severity >= 3) as critical " +
			"from security_events where at > now() - make_interval(hours => $1::int) group by 1 order by 1", hours);
		var byHour = new List<EventBucket>();
		for (int i = 0; i < h.Rows.Count; i++)
			byHour.Add(new EventBucket(h.Date(i, "hour").Value, h.Int(i, "info"), h.Int(i, "warning"), h.Int(i, "high"), h.Int(i, "critical")));

		PgResult k = await _db.Query(
			"select kind, count(*) as n from security_events where at > now() - make_interval(hours => $1::int) group by kind order by n desc limit 30", hours);
		var byKind = new List<(string, int)>();
		for (int i = 0; i < k.Rows.Count; i++) byKind.Add((k.Text(i, "kind"), k.Int(i, "n")));

		PgResult t = await _db.Query(
			"select ip, count(*) as n, max(severity) as worst from security_events " +
			"where at > now() - make_interval(hours => $1::int) and ip <> '' and severity >= 1 group by ip order by n desc limit 15", hours);
		var top = new List<(string, int, int)>();
		for (int i = 0; i < t.Rows.Count; i++) top.Add((t.Text(i, "ip"), t.Int(i, "n"), t.Int(i, "worst")));

		return new SecurityOverview(byHour, byKind, top);
	}

	public async Task<Ban> AddBan(long accountId, string reason, DateTime? until, long adminId, string adminName)
	{
		PgResult r = await _db.Query(
			"with made as (insert into bans (account_id, reason, until, admin_id, admin_name) values ($1, $2, $3, $4, $5) returning *) " +
			"select made.*, a.name as account_name from made join accounts a on a.id = made.account_id",
			accountId, reason, until, adminId, adminName);
		return BanOf(r, 0);
	}

	public async Task<bool> LiftBan(long accountId, string adminName)
	{
		PgResult r = await _db.Query(
			"update bans set lifted_at = now(), lifted_by = $2 where account_id = $1 and lifted_at is null and (until is null or until > now())",
			accountId, adminName);
		return r.Affected > 0;
	}

	public async Task<IReadOnlyList<Ban>> Bans(long? accountId, bool activeOnly, int limit)
	{
		var where = new List<string> { "true" };
		var args = new List<object>();
		if (accountId != null) { args.Add(accountId.Value); where.Add($"b.account_id = ${args.Count}"); }
		if (activeOnly) where.Add("b.lifted_at is null and (b.until is null or b.until > now())");
		args.Add(Math.Clamp(limit, 1, 500));

		PgResult r = await _db.Query(
			$"select b.*, a.name as account_name from bans b join accounts a on a.id = b.account_id where {string.Join(" and ", where)} " +
			$"order by b.id desc limit ${args.Count}", args.ToArray());
		var list = new List<Ban>();
		for (int i = 0; i < r.Rows.Count; i++) list.Add(BanOf(r, i));
		return list;
	}

	public async Task<int> PruneEvents(DateTime olderThan) =>
		(await _db.Query("delete from security_events where at < $1", olderThan)).Affected;

	private static IpBlock Block(PgResult r, int i) =>
		new(r.Long(i, "id"), r.Text(i, "cidr"), r.Text(i, "reason"), r.Text(i, "source"), r.Date(i, "created_at").Value, r.Date(i, "until"), r.Text(i, "admin_name"));

	private static Ban BanOf(PgResult r, int i) =>
		new(r.Long(i, "id"), r.Long(i, "account_id"), r.Text(i, "account_name"), r.Text(i, "reason"), r.Date(i, "created_at").Value,
			r.Date(i, "until"), r.Text(i, "admin_name"), r.Date(i, "lifted_at"), r.Text(i, "lifted_by"));
}
