using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Contas;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>
/// As amizades no PostgreSQL (tabela friendships, em <see cref="Schema"/>):
/// uma linha por par de contas, a de número menor em <c>low</c>.
///
/// Pedir e aceitar conferem os tetos e gravam dentro de uma transação que
/// trava as duas contas (<c>pg_advisory_xact_lock</c>, sempre a menor
/// primeiro, para duas transações cruzadas não se travarem uma à outra):
/// dois pedidos ao mesmo tempo não passam do teto nem se atropelam.
/// </summary>
public sealed class PgFriendRepository : IFriendRepository
{
	private readonly PgPool _db;

	public PgFriendRepository(PgPool db) => _db = db;

	public async Task<IReadOnlyList<FriendLink>> Links(long accountId)
	{
		PgResult r = await _db.Query(
			"select a.id, a.name, a.tag, coalesce(p.xp, 0) as xp, f.accepted, f.requester " +
			"from friendships f " +
			"join accounts a on a.id = case when f.low = $1::bigint then f.high else f.low end " +
			"left join profiles p on p.account_id = a.id " +
			"where f.low = $1::bigint or f.high = $1::bigint", accountId);

		var list = new List<FriendLink>(r.Rows.Count);
		for (int i = 0; i < r.Rows.Count; i++)
		{
			FriendKind kind = r.Bool(i, "accepted") ? FriendKind.Friend
				: r.Long(i, "requester") == accountId ? FriendKind.Outgoing : FriendKind.Incoming;
			list.Add(new FriendLink(r.Long(i, "id"), r.Text(i, "name"), r.Long(i, "xp"), kind, r.Int(i, "tag")));
		}
		return list;
	}

	public async Task<FriendTarget> Find(string nameKey)
	{
		PgResult r = await _db.Query(
			"select a.id, a.name, a.tag, coalesce(p.xp, 0) as xp from accounts a left join profiles p on p.account_id = a.id where a.name_key = $1", nameKey);
		return r.Empty ? null : new FriendTarget(r.Long(0, "id"), r.Text(0, "name"), r.Long(0, "xp"), r.Int(0, "tag"));
	}

	public Task<FriendRequestOutcome> Request(long from, long to) => _db.Transaction(async c =>
	{
		(long low, long high) = Pair(from, to);
		await Lock(c, low, high);

		PgResult row = await c.Query("select requester, accepted from friendships where low = $1::bigint and high = $2::bigint", low, high);
		if (!row.Empty)
		{
			if (row.Bool(0, "accepted")) throw FriendRules.AlreadyFriends();
			if (row.Long(0, "requester") == from) throw FriendRules.AlreadyRequested();

			// O outro já tinha pedido: pedido cruzado vira amizade.
			await RequireRoom(c, from, to);
			await c.Query("update friendships set accepted = true, accepted_at = now() where low = $1::bigint and high = $2::bigint", low, high);
			return FriendRequestOutcome.Accepted;
		}

		Counts mine = await Count(c, from);
		if (mine.Friends >= FriendRules.MaxFriends) throw FriendRules.ListFull();
		if (mine.Sent >= FriendRules.MaxPending) throw FriendRules.TooManySent();
		if ((await Count(c, to)).Inbox >= FriendRules.MaxPending) throw FriendRules.TheirInboxFull();

		await c.Query("insert into friendships (low, high, requester) values ($1::bigint, $2::bigint, $3::bigint)", low, high, from);
		return FriendRequestOutcome.Requested;
	});

	public Task<bool> Accept(long me, long from) => _db.Transaction(async c =>
	{
		(long low, long high) = Pair(me, from);
		await Lock(c, low, high);

		PgResult row = await c.Query(
			"select 1 as x from friendships where low = $1::bigint and high = $2::bigint and not accepted and requester = $3::bigint", low, high, from);
		if (row.Empty) return false;

		await RequireRoom(c, me, from);
		await c.Query("update friendships set accepted = true, accepted_at = now() where low = $1::bigint and high = $2::bigint", low, high);
		return true;
	});

	public async Task<bool> RemovePending(long me, long other)
	{
		(long low, long high) = Pair(me, other);
		PgResult gone = await _db.Query(
			"delete from friendships where low = $1::bigint and high = $2::bigint and not accepted returning low", low, high);
		return !gone.Empty;
	}

	public async Task<bool> RemoveFriend(long me, long other)
	{
		(long low, long high) = Pair(me, other);
		PgResult gone = await _db.Query(
			"delete from friendships where low = $1::bigint and high = $2::bigint and accepted returning low", low, high);
		return !gone.Empty;
	}

	// ------------------------------------------------------------------

	private static (long Low, long High) Pair(long a, long b) => (Math.Min(a, b), Math.Max(a, b));

	/// <summary>Trava as duas contas até o fim da transação, a menor primeiro.</summary>
	private static async Task Lock(PgConnection c, long low, long high)
	{
		await c.Query("select pg_advisory_xact_lock($1::bigint)", low);
		await c.Query("select pg_advisory_xact_lock($1::bigint)", high);
	}

	private readonly record struct Counts(int Friends, int Sent, int Inbox);

	private static async Task<Counts> Count(PgConnection c, long account)
	{
		PgResult r = await c.Query(
			"select count(*) filter (where accepted) as friends, " +
			"count(*) filter (where not accepted and requester = $1::bigint) as sent, " +
			"count(*) filter (where not accepted and requester <> $1::bigint) as inbox " +
			"from friendships where low = $1::bigint or high = $1::bigint", account);
		return new Counts(r.Int(0, "friends"), r.Int(0, "sent"), r.Int(0, "inbox"));
	}

	/// <summary>Os dois têm lugar para mais um amigo? (<paramref name="me"/> é quem está aceitando.)</summary>
	private static async Task RequireRoom(PgConnection c, long me, long other)
	{
		if ((await Count(c, me)).Friends >= FriendRules.MaxFriends) throw FriendRules.ListFull();
		if ((await Count(c, other)).Friends >= FriendRules.MaxFriends) throw FriendRules.TheirListFull();
	}
}
