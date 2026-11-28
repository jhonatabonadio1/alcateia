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
		",

		// 3 — compra com número de pedido (26/11): o mesmo pedido repetido
		// (duplo clique, a rede mandando de novo) devolve o mesmo recibo e
		// não cobra outra vez.
		@"
		create table purchase_requests (
			account_id bigint not null references accounts(id) on delete cascade,
			request_id text not null,
			item_id text not null,
			currency text not null,
			price int not null,
			balance bigint not null,
			expires_at timestamptz,
			at timestamptz not null default now(),
			primary key (account_id, request_id)
		);
		",

		// 4 — "tatuagem" agora é "melhoria de lobo" (Jhon, 27/11: "troque todos
		// esses termos para não confundir"): os ids tat_* viram up_* e o tipo
		// "tattoo" vira "wolf_upgrade" em tudo o que guarda item — o catálogo
		// (com a promoção que aponta para ele), os inventários, os prêmios dos
		// desafios, os pedidos de compra, o extrato e os dados do perfil. O jogo
		// de antes de 27/11 ainda manda tat_* para a partida: o servidor não
		// acha o item na conta e o lobo entra sem a melhoria. O perfil salvo no
		// computador do jogador o próprio jogo converte (Catalog.CurrentId).
		@"
		alter table promotions drop constraint if exists promotions_item_id_fkey;
		update catalog_items set id = 'up_' || substr(id, 5), updated_at = now() where id like 'tat\_%';
		update promotions set item_id = 'up_' || substr(item_id, 5) where item_id like 'tat\_%';
		alter table promotions add constraint promotions_item_id_fkey
			foreign key (item_id) references catalog_items(id) on delete cascade;
		update catalog_items set kind = 'wolf_upgrade' where kind = 'tattoo';
		update catalog_items set type = 'Melhoria de lobo' where type = 'Melhoria · lobo';

		update inventory set item_id = 'up_' || substr(item_id, 5) where item_id like 'tat\_%';
		update challenges set item_id = 'up_' || substr(item_id, 5) where item_id like 'tat\_%';
		update purchase_requests set item_id = 'up_' || substr(item_id, 5) where item_id like 'tat\_%';
		update ledger set ref = 'up_' || substr(ref, 5) where ref like 'tat\_%';
		update profiles set data = replace(data::text, '""tat_', '""up_')::jsonb where data::text like '%""tat\_%';
		",

		// 5 — amigos (Jhon, 27/11: "a parte do social (amigos), inclusive
		// convidar, aceitar pedidos"). Uma linha por par de contas, com a
		// menor na frente: o pedido cruzado bate na mesma chave e vira
		// amizade em vez de segunda linha. `requester` é quem pediu;
		// `accepted` diz se já são amigos. Conta apagada leva as linhas dela.
		@"
		create table friendships (
			low bigint not null references accounts(id) on delete cascade,
			high bigint not null references accounts(id) on delete cascade,
			requester bigint not null,
			accepted boolean not null default false,
			created_at timestamptz not null default now(),
			accepted_at timestamptz,
			primary key (low, high),
			check (low < high),
			check (requester = low or requester = high)
		);
		create index friendships_high on friendships(high);
		",

		// 6 — a tag da conta (Jhon, 27/11: "cada player terá uma tag #0000
		// para adicionar"): quatro algarismos sorteados quando a conta nasce,
		// que vão depois do nome (Nome#0042) e que é preciso saber para pedir
		// amizade. O nome continua único; a tag não precisa ser. As contas
		// que já existiam ganham a delas aqui.
		@"
		alter table accounts add column tag integer;
		update accounts set tag = floor(random() * 10000)::int;
		alter table accounts alter column tag set not null;
		alter table accounts alter column tag set default floor(random() * 10000)::int;
		alter table accounts add constraint accounts_tag_range check (tag between 0 and 9999);
		",

		// 7 — a verificação em duas etapas do jogador (Jhon, 27/11: "dê opção
		// para o player cadastrar 2 fatores"): o segredo do aplicativo
		// autenticador, cifrado com a chave mestra (como o dos administradores),
		// e se já está valendo. Segredo guardado sem estar valendo é quem
		// começou a ligar e ainda não confirmou o primeiro código.
		@"
		alter table accounts add column totp_secret text;
		alter table accounts add column totp_enabled boolean not null default false;
		",

		// 8 — a nota de habilidade do jogo rápido (Jhon, 27/11: "escolhe os
		// players baseado nas suas habilidades, níveis, experiências"). Vazia
		// até a conta jogar a primeira partida pela fila: até lá a largada sai
		// do histórico dela (Dominio/Fila/SkillRules.Seed). skill_matches é
		// quantas partidas de fila formaram a nota.
		@"
		alter table profiles add column skill double precision;
		alter table profiles add column skill_matches integer not null default 0;
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
