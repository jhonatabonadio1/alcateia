using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>As contas vistas pelo painel e a economia de cada uma:
/// carteira (profiles), inventário, extrato e progresso nos desafios.
/// Tudo o que mexe em saldo trava a linha do perfil (<c>for update</c>):
/// duas compras ao mesmo tempo não gastam o mesmo dinheiro.</summary>
public sealed class PgPlayerRepository : IPlayerRepository
{
	private const string Summary =
		"select a.id, a.name, a.created_at, a.last_login_at, a.banned, a.banned_until, a.ban_reason, a.last_ip, p.* " +
		"from accounts a join profiles p on p.account_id = a.id ";

	private const string IsBanned = "(a.banned and (a.banned_until is null or a.banned_until > now()))";

	private readonly PgPool _db;

	public PgPlayerRepository(PgPool db) => _db = db;

	public async Task<IReadOnlyList<PlayerSummary>> Search(string query, int limit, bool bannedOnly)
	{
		query = (query ?? "").Trim();
		var where = new List<string> { "true" };
		var args = new List<object>();
		if (query.Length > 0)
		{
			if (long.TryParse(query, out long id)) { args.Add(id); where.Add($"a.id = ${args.Count}"); }
			else if (query.Contains('.') || query.Contains(':')) { args.Add(query); where.Add($"a.last_ip = ${args.Count}"); }
			else { args.Add(AccountRules.Key(query).Replace("_", "\\_").Replace("%", "") + "%"); where.Add($"a.name_key like ${args.Count}"); }
		}
		if (bannedOnly) where.Add(IsBanned);
		args.Add(Math.Clamp(limit, 1, 200));

		PgResult r = await _db.Query(
			$"{Summary} where {string.Join(" and ", where)} order by a.last_login_at desc nulls last, a.id desc limit ${args.Count}", args.ToArray());
		var list = new List<PlayerSummary>();
		for (int i = 0; i < r.Rows.Count; i++) list.Add(SummaryOf(r, i));
		return list;
	}

	public async Task<PlayerDetail> Detail(long accountId)
	{
		PgResult r = await _db.Query(Summary + "where a.id = $1", accountId);
		if (r.Empty) return null;
		var stats = Profile.StatNames.ToDictionary(column => column, column => r.Int(0, column));
		return new PlayerDetail(SummaryOf(r, 0), r.Text(0, "last_ip") ?? "", r.Text(0, "ban_reason") ?? "", stats);
	}

	private static PlayerSummary SummaryOf(PgResult r, int i)
	{
		DateTime? until = r.Date(i, "banned_until");
		bool banned = r.Bool(i, "banned") && (until == null || until > DateTime.UtcNow);
		return new PlayerSummary(r.Long(i, "id"), r.Text(i, "name"), r.Long(i, "xp"), r.Long(i, "presas"), r.Long(i, "sangue"),
			r.Int(i, "matches"), r.Date(i, "created_at").Value, r.Date(i, "last_login_at"), until, banned);
	}

	// ------------------------------------------------------------ carteira

	public Task<long> Adjust(long accountId, string currency, long amount, string reason, string reference, long? adminId) =>
		_db.Transaction(c => Adjust(c, accountId, currency, amount, reason, reference, adminId));

	private static async Task<long> Adjust(PgConnection c, long accountId, string currency, long amount, string reason, string reference, long? adminId)
	{
		string column = Column(currency);
		PgResult now = await c.Query($"select {column} as balance from profiles where account_id = $1 for update", accountId);
		if (now.Empty) throw new Failure(FailureKind.NotFound, "conta_inexistente", "Essa conta não existe.");

		long balance = now.Long(0, "balance") + amount;
		if (balance < 0) throw new Failure(FailureKind.Conflict, "saldo_insuficiente", "Saldo insuficiente.");

		await c.Query($"update profiles set {column} = $2, updated_at = now() where account_id = $1", accountId, balance);
		await c.Query("insert into ledger (account_id, currency, amount, balance_after, reason, ref, admin_id) values ($1, $2, $3, $4, $5, $6, $7)",
			accountId, currency, amount, balance, reason, reference ?? "", adminId);
		return balance;
	}

	/// <summary>A coluna da moeda — de uma lista fechada, nunca do pedido.</summary>
	private static string Column(string currency) => currency switch
	{
		Currency.Presas => "presas",
		Currency.Sangue => "sangue",
		_ => throw new Failure(FailureKind.Invalid, "moeda_invalida", "Moeda inválida.")
	};

	public async Task<IReadOnlyList<LedgerEntry>> Ledger(long accountId, int limit)
	{
		PgResult r = await _db.Query("select * from ledger where account_id = $1 order by id desc limit $2", accountId, Math.Clamp(limit, 1, 500));
		var list = new List<LedgerEntry>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new LedgerEntry(r.Long(i, "id"), r.Date(i, "at").Value, r.Text(i, "currency"), r.Long(i, "amount"),
				r.Long(i, "balance_after"), r.Text(i, "reason"), r.Text(i, "ref")));
		return list;
	}

	// ------------------------------------------------------------ inventário

	public async Task<IReadOnlyList<OwnedItem>> Inventory(long accountId)
	{
		PgResult r = await _db.Query(
			"select * from inventory where account_id = $1 and (expires_at is null or expires_at > now()) order by acquired_at desc, item_id", accountId);
		var list = new List<OwnedItem>();
		for (int i = 0; i < r.Rows.Count; i++) list.Add(Owned(r, i));
		return list;
	}

	public Task Grant(long accountId, string itemId, DateTime? expiresAt, string source, int uses) =>
		_db.Use(c => Grant(c, accountId, itemId, expiresAt, source, uses));

	private static async Task<int> Grant(PgConnection c, long accountId, string itemId, DateTime? expiresAt, string source, int uses)
	{
		// Consumível soma usos; o resto troca a validade pela nova (quem
		// chama já somou o tempo que restava).
		await c.Query(
			"insert into inventory (account_id, item_id, expires_at, source, uses) values ($1, $2, $3, $4, $5) " +
			"on conflict (account_id, item_id) do update set expires_at = excluded.expires_at, source = excluded.source, " +
			"uses = inventory.uses + excluded.uses, acquired_at = now()",
			accountId, itemId, expiresAt, source, uses);
		return 0;
	}

	public async Task<bool> Consume(long accountId, string itemId)
	{
		PgResult r = await _db.Query("update inventory set uses = uses - 1 where account_id = $1 and item_id = $2 and uses > 0 returning uses", accountId, itemId);
		if (r.Empty) return false;
		if (r.Int(0, "uses") == 0) await _db.Exec("delete from inventory where account_id = $1 and item_id = $2 and uses = 0", accountId, itemId);
		return true;
	}

	public async Task<bool> Revoke(long accountId, string itemId) =>
		(await _db.Query("delete from inventory where account_id = $1 and item_id = $2", accountId, itemId)).Affected > 0;

	public Task<PurchaseReceipt> Purchase(long accountId, string itemId, Func<PurchaseContext, PurchaseDecision> decide, string requestId = null) =>
		_db.Transaction(async c =>
		{
			PgResult wallet = await c.Query("select xp, presas, sangue from profiles where account_id = $1 for update", accountId);
			if (wallet.Empty) throw new Failure(FailureKind.NotFound, "conta_inexistente", "Essa conta não existe.");

			// O mesmo pedido de novo: a carteira já está travada, então o segundo
			// espera o primeiro acabar e encontra o recibo dele aqui.
			if (!string.IsNullOrEmpty(requestId))
			{
				PgResult done = await c.Query("select item_id, currency, price, balance, expires_at from purchase_requests where account_id = $1 and request_id = $2", accountId, requestId);
				if (!done.Empty)
				{
					if (done.Text(0, "item_id") != itemId)
						throw new Failure(FailureKind.Conflict, "pedido_repetido", "Esse pedido já foi usado para outro item.");
					return new PurchaseReceipt(itemId, done.Text(0, "currency"), done.Int(0, "price"), done.Long(0, "balance"), done.Date(0, "expires_at"));
				}
			}

			PgResult has = await c.Query("select * from inventory where account_id = $1 and item_id = $2", accountId, itemId);
			OwnedItem owned = has.Empty ? null : Owned(has, 0);

			PurchaseDecision d = decide(new PurchaseContext(wallet.Long(0, "xp"), wallet.Long(0, "presas"), wallet.Long(0, "sangue"), owned));
			long balance = await Adjust(c, accountId, d.Currency, -d.Price, "compra", itemId, null);
			await Grant(c, accountId, itemId, d.ExpiresAt, "loja", d.Uses);
			if (!string.IsNullOrEmpty(requestId))
				await c.Query("insert into purchase_requests (account_id, request_id, item_id, currency, price, balance, expires_at) values ($1, $2, $3, $4, $5, $6, $7)",
					accountId, requestId, itemId, d.Currency, d.Price, balance, d.ExpiresAt);
			return new PurchaseReceipt(itemId, d.Currency, d.Price, balance, d.ExpiresAt);
		});

	private static OwnedItem Owned(PgResult r, int i) =>
		new(r.Text(i, "item_id"), r.Date(i, "acquired_at").Value, r.Date(i, "expires_at"), r.Text(i, "source")) { Uses = r.Int(i, "uses") };

	// ------------------------------------------------------------ desafios

	public async Task<IReadOnlyList<(string ChallengeId, string PeriodKey, int Progress, bool Claimed)>> Progress(long accountId)
	{
		PgResult r = await _db.Query("select challenge_id, period_key, progress, claimed from challenge_progress where account_id = $1", accountId);
		var list = new List<(string, string, int, bool)>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add((r.Text(i, "challenge_id"), r.Text(i, "period_key"), r.Int(i, "progress"), r.Bool(i, "claimed")));
		return list;
	}

	public Task AddProgress(long accountId, string challengeId, string periodKey, int amount) => _db.Exec(
		"insert into challenge_progress (account_id, challenge_id, period_key, progress) values ($1, $2, $3, $4) " +
		"on conflict (account_id, challenge_id, period_key) do update set progress = challenge_progress.progress + excluded.progress, updated_at = now()",
		accountId, challengeId, periodKey, amount);

	public Task<bool> Claim(long accountId, Challenge ch, string periodKey, DateTime? itemExpiresAt) => _db.Transaction(async c =>
	{
		// Só marca se completou e ainda não recebeu: o prêmio sai uma vez,
		// mesmo com dois pedidos ao mesmo tempo.
		PgResult marked = await c.Query(
			"update challenge_progress set claimed = true, updated_at = now() " +
			"where account_id = $1 and challenge_id = $2 and period_key = $3 and not claimed and progress >= $4",
			accountId, ch.Id, periodKey, ch.Target);
		if (marked.Affected == 0) return false;

		string reference = $"{ch.Id}/{periodKey}";
		if (ch.Presas > 0) await Adjust(c, accountId, Currency.Presas, ch.Presas, "desafio", reference, null);
		if (ch.Sangue > 0) await Adjust(c, accountId, Currency.Sangue, ch.Sangue, "desafio", reference, null);
		if (ch.Xp > 0)
		{
			PgResult after = await c.Query("update profiles set xp = xp + $2, updated_at = now() where account_id = $1 returning xp", accountId, ch.Xp);
			await PayPromotions(c, accountId, after.Long(0, "xp") - ch.Xp, after.Long(0, "xp"), reference);
		}
		if (!string.IsNullOrEmpty(ch.ItemId)) await Grant(c, accountId, ch.ItemId, itemExpiresAt, "desafio", 0);
		return true;
	});

	/// <summary>Subiu de patente: paga as Presas de cada patente alcançada
	/// (com linha no extrato). Dentro da transação de quem deu o XP.</summary>
	internal static async Task PayPromotions(PgConnection c, long accountId, long xpBefore, long xpAfter, string reference)
	{
		long presas = RankRule.PromotionPresas(xpBefore, xpAfter);
		if (presas > 0) await Adjust(c, accountId, Currency.Presas, presas, "promocao", reference, null);
	}

	// ------------------------------------------------------------ banimento e sessões

	public Task SetBan(long accountId, DateTime? until, string reason, bool banned) =>
		_db.Exec("update accounts set banned = $4, banned_until = $2, ban_reason = $3 where id = $1", accountId, until, reason, banned);

	public Task RemoveSessions(long accountId) => _db.Exec("delete from sessions where account_id = $1", accountId);

	public Task NoteIp(long accountId, string ip) => _db.Exec("update accounts set last_ip = $2 where id = $1", accountId, ip ?? "");

	public async Task<IReadOnlyList<(long Id, string Name)>> AccountsByIp(string ip, int limit)
	{
		if (string.IsNullOrEmpty(ip)) return Array.Empty<(long, string)>();
		PgResult r = await _db.Query("select id, name from accounts where last_ip = $1 order by id desc limit $2", ip, Math.Clamp(limit, 1, 100));
		return Enumerable.Range(0, r.Rows.Count).Select(i => (r.Long(i, "id"), r.Text(i, "name"))).ToList();
	}

	// ------------------------------------------------------------ números

	public async Task<ServerNumbers> Numbers()
	{
		PgResult n = await _db.Query(
			"select (select count(*) from accounts) as accounts, " +
			"(select count(*) from accounts where created_at > now() - interval '24 hours') as new_today, " +
			"(select count(*) from accounts where last_login_at > now() - interval '24 hours') as active_day, " +
			"(select count(*) from matches where ended_at > now() - interval '24 hours') as matches_today, " +
			"(select count(*) from matches) as matches, " +
			"(select coalesce(sum(presas), 0) from profiles) as presas, (select coalesce(sum(sangue), 0) from profiles) as sangue, " +
			"(select count(*) from accounts a where " + IsBanned + ") as bans");

		PgResult d = await _db.Query(
			"select day::date as day, " +
			"(select count(*) from accounts a where a.created_at::date = day::date) as accounts, " +
			"(select count(*) from matches m where m.ended_at::date = day::date) as matches " +
			"from generate_series(current_date - 13, current_date, interval '1 day') as day order by day");
		var days = new List<(DateTime, int, int)>();
		for (int i = 0; i < d.Rows.Count; i++)
			days.Add((DateTime.Parse(d.Text(i, "day"), System.Globalization.CultureInfo.InvariantCulture), d.Int(i, "accounts"), d.Int(i, "matches")));

		return new ServerNumbers(n.Long(0, "accounts"), n.Long(0, "new_today"), n.Long(0, "active_day"), n.Long(0, "matches_today"),
			n.Long(0, "matches"), n.Long(0, "presas"), n.Long(0, "sangue"), n.Long(0, "bans"), days);
	}
}
