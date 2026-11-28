using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Conversas;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>
/// As mensagens diretas no PostgreSQL (tabela direct_messages, em
/// <see cref="Schema"/>). A conversa é o par de contas, achado pelo índice
/// de (menor, maior, número): o histórico e a poda andam por ele.
/// </summary>
public sealed class PgDirectMessageRepository : IDirectMessageRepository
{
	private readonly PgPool _db;

	public PgDirectMessageRepository(PgPool db) => _db = db;

	private const string Pair =
		"least(sender, recipient) = least($1::bigint, $2::bigint) and greatest(sender, recipient) = greatest($1::bigint, $2::bigint)";

	public Task<DirectMessage> Save(long from, long to, string text) => _db.Transaction(async c =>
	{
		PgResult r = await c.Query(
			"insert into direct_messages (sender, recipient, text) values ($1::bigint, $2::bigint, $3) returning id, sent_at",
			from, to, text);
		long id = r.Long(0, "id");

		// A conversa guarda só as mais novas: o que passou do teto sai agora,
		// na mesma transação (normalmente uma, a mais velha).
		await c.Query(
			$"delete from direct_messages where {Pair} and id <= (" +
			$"select id from direct_messages where {Pair} order by id desc offset $3 limit 1)",
			from, to, ChatRules.Kept);

		return new DirectMessage(id, from, to, text, r.Date(0, "sent_at") ?? DateTime.UtcNow, false);
	});

	public async Task<IReadOnlyList<DirectMessage>> History(long a, long b, long beforeId, int limit)
	{
		PgResult r = await _db.Query(
			$"select id, sender, recipient, text, sent_at, read_at is not null as read from direct_messages " +
			$"where {Pair} and ($3::bigint = 0 or id < $3::bigint) order by id desc limit $4",
			a, b, beforeId, limit);

		var list = new List<DirectMessage>(r.Rows.Count);
		for (int i = r.Rows.Count - 1; i >= 0; i--)
			list.Add(new DirectMessage(r.Long(i, "id"), r.Long(i, "sender"), r.Long(i, "recipient"), r.Text(i, "text"),
				r.Date(i, "sent_at") ?? DateTime.UtcNow, r.Bool(i, "read")));
		return list;
	}

	public async Task<int> MarkRead(long reader, long from) =>
		await _db.Exec(
			"update direct_messages set read_at = now() where recipient = $1::bigint and sender = $2::bigint and read_at is null",
			reader, from);

	public async Task<IReadOnlyList<(long From, int Count)>> Unread(long accountId)
	{
		PgResult r = await _db.Query(
			"select sender, count(*) as n from direct_messages where recipient = $1::bigint and read_at is null group by sender",
			accountId);
		var list = new List<(long, int)>(r.Rows.Count);
		for (int i = 0; i < r.Rows.Count; i++) list.Add((r.Long(i, "sender"), r.Int(i, "n")));
		return list;
	}
}
