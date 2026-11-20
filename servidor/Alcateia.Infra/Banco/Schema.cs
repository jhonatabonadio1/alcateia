using System.Threading.Tasks;

namespace Alcateia.Infra.Banco;

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
		",

		// 2 — o servidor passa a ser dono de tudo (20/11): moeda paga,
		// inventário e extrato; catálogo, mapas, promoções e desafios que o
		// painel edita; banimentos, blacklist de IP, eventos de segurança;
		// os administradores do painel e a auditoria do que fizeram.
		@"
		alter table accounts add column banned boolean not null default false;
		alter table accounts add column banned_until timestamptz;
		alter table accounts add column ban_reason text;
		alter table accounts add column last_ip text;
		alter table profiles add column sangue bigint not null default 0;
		alter table match_players add column kills_wolf int not null default 0;
		alter table match_players add column kills_human int not null default 0;

		create table inventory (
			account_id bigint not null references accounts(id) on delete cascade,
			item_id text not null,
			acquired_at timestamptz not null default now(),
			expires_at timestamptz,
			source text not null,
			uses int not null default 0,
			primary key (account_id, item_id)
		);
		create table ledger (
			id bigserial primary key,
			account_id bigint not null references accounts(id) on delete cascade,
			at timestamptz not null default now(),
			currency text not null,
			amount bigint not null,
			balance_after bigint not null,
			reason text not null,
			ref text not null default '',
			admin_id bigint
		);
		create index ledger_account on ledger(account_id, id desc);

		create table catalog_items (
			id text primary key,
			name text not null,
			type text not null,
			kind text not null,
			rarity int not null default 0,
			min_rank int not null default 0,
			currency text not null default 'presas',
			prices jsonb not null default '[]'::jsonb,
			starter boolean not null default false,
			enabled boolean not null default true,
			hot boolean not null default false,
			consumable boolean not null default false,
			team int not null default 0,
			image text not null default '',
			sort int not null default 0,
			updated_at timestamptz not null default now()
		);
		create table game_maps (
			id text primary key,
			name text not null,
			size text not null default '',
			description text not null default '',
			modes jsonb not null default '[]'::jsonb,
			enabled boolean not null default true,
			image text not null default '',
			sort int not null default 0,
			updated_at timestamptz not null default now()
		);
		create table promotions (
			id bigserial primary key,
			item_id text not null references catalog_items(id) on delete cascade,
			title text not null default '',
			percent int not null,
			starts_at timestamptz not null,
			ends_at timestamptz not null,
			active boolean not null default true,
			created_by bigint,
			created_at timestamptz not null default now()
		);
		create index promotions_window on promotions(ends_at);
		create table challenges (
			id text primary key,
			period text not null,
			text text not null,
			metric text not null,
			target int not null,
			presas int not null default 0,
			xp int not null default 0,
			sangue int not null default 0,
			item_id text,
			item_days int not null default 0,
			title text,
			active boolean not null default true,
			starts_at timestamptz,
			ends_at timestamptz,
			sort int not null default 0,
			updated_at timestamptz not null default now()
		);
		create table challenge_progress (
			account_id bigint not null references accounts(id) on delete cascade,
			challenge_id text not null,
			period_key text not null,
			progress int not null default 0,
			claimed boolean not null default false,
			updated_at timestamptz not null default now(),
			primary key (account_id, challenge_id, period_key)
		);
		create table settings (
			key text primary key,
			value jsonb not null,
			updated_at timestamptz not null default now()
		);

		create table bans (
			id bigserial primary key,
			account_id bigint not null references accounts(id) on delete cascade,
			reason text not null,
			created_at timestamptz not null default now(),
			until timestamptz,
			admin_id bigint,
			admin_name text not null default '',
			lifted_at timestamptz,
			lifted_by text
		);
		create index bans_account on bans(account_id, id desc);
		create table ip_blocks (
			id bigserial primary key,
			cidr text not null unique,
			reason text not null,
			source text not null,
			created_at timestamptz not null default now(),
			until timestamptz,
			admin_name text not null default ''
		);
		create table security_events (
			id bigserial primary key,
			at timestamptz not null default now(),
			kind text not null,
			severity int not null,
			ip text not null default '',
			account_id bigint,
			actor text not null default '',
			path text not null default '',
			detail text not null default ''
		);
		create index security_events_at on security_events(at desc);
		create index security_events_ip on security_events(ip, at desc);
		create index security_events_kind on security_events(kind, at desc);

		create table admins (
			id bigserial primary key,
			name text not null,
			name_key text not null unique,
			pass_hash text not null,
			role text not null,
			totp_secret text,
			totp_enabled boolean not null default false,
			disabled boolean not null default false,
			failed_logins int not null default 0,
			locked_until timestamptz,
			created_at timestamptz not null default now(),
			last_login_at timestamptz,
			last_ip text
		);
		create table admin_sessions (
			token_hash text primary key,
			admin_id bigint not null references admins(id) on delete cascade,
			created_at timestamptz not null default now(),
			expires_at timestamptz not null,
			ip text not null default '',
			user_agent text not null default ''
		);
		create table audit_log (
			id bigserial primary key,
			at timestamptz not null default now(),
			admin_id bigint,
			admin_name text not null,
			action text not null,
			target text not null default '',
			detail text not null default '',
			ip text not null default ''
		);
		create index audit_log_at on audit_log(at desc);
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
