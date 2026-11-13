using System.Threading.Tasks;

namespace Alcateia.Servidor.Banco;

/// <summary>
/// As tabelas, por versão. Cada passo roda uma vez, em ordem, dentro de
/// uma transação; o banco guarda em <c>schema_version</c> até onde foi.
/// Mudou o banco? Acrescente um passo no fim — nunca edite um que já
/// foi para alguém.
/// </summary>
public static class Schema
{
	private static readonly string[] Steps =
	{
		// 1 — contas, logins, perfil e histórico de partidas.
		@"
		create table accounts (
			id bigserial primary key,
			name text not null,
			name_key text not null unique,
			pass_hash text not null,
			created_at timestamptz not null default now(),
			last_login_at timestamptz
		);

		create table sessions (
			token_hash text primary key,
			account_id bigint not null references accounts(id) on delete cascade,
			created_at timestamptz not null default now(),
			expires_at timestamptz not null
		);
		create index sessions_account on sessions(account_id);

		create table profiles (
			account_id bigint primary key references accounts(id) on delete cascade,
			xp bigint not null default 0,
			presas bigint not null default 0,
			matches int not null default 0,
			wins int not null default 0,
			losses int not null default 0,
			draws int not null default 0,
			kills int not null default 0,
			deaths int not null default 0,
			assists int not null default 0,
			headshots int not null default 0,
			mvps int not null default 0,
			data jsonb not null default '{}'::jsonb,
			updated_at timestamptz not null default now()
		);
		create index profiles_xp on profiles(xp desc);

		create table matches (
			id bigserial primary key,
			match_key text not null unique,
			server_id text not null,
			room_title text not null,
			map text not null,
			mode text not null,
			winner int not null,
			score_blue int not null,
			score_orange int not null,
			started_at timestamptz not null,
			ended_at timestamptz not null default now()
		);

		create table match_players (
			match_id bigint not null references matches(id) on delete cascade,
			account_id bigint not null references accounts(id) on delete cascade,
			team int not null,
			kills int not null,
			deaths int not null,
			assists int not null,
			headshots int not null,
			points int not null,
			mvp boolean not null,
			xp_gain bigint not null,
			presas_gain bigint not null,
			primary key (match_id, account_id)
		);
		create index match_players_account on match_players(account_id, match_id desc);
		"
	};

	public static int Latest => Steps.Length;

	/// <summary>Leva o banco até a última versão. Devolve em qual estava.</summary>
	public static async Task<int> Migrate(PgPool db)
	{
		await db.Use(async c =>
		{
			await c.Script("create table if not exists schema_version (version int primary key, applied_at timestamptz not null default now())");
			return 0;
		});

		PgResult now = await db.Query("select coalesce(max(version), 0) as v from schema_version");
		int from = now.Int(0, "v");

		for (int v = from + 1; v <= Steps.Length; v++)
		{
			int step = v;
			await db.Transaction(async c =>
			{
				await c.Script(Steps[step - 1]);
				await c.Query("insert into schema_version (version) values ($1)", step);
				return 0;
			});
		}

		return from;
	}
}
