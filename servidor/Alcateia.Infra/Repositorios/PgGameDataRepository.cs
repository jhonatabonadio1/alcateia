using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;
using Alcateia.Infra.Banco;

namespace Alcateia.Infra.Repositorios;

/// <summary>O que o painel edita e o jogo lê: catálogo, mapas, promoções,
/// desafios e configurações (tabelas catalog_items, game_maps, promotions,
/// challenges e settings).</summary>
public sealed class PgGameDataRepository : IGameDataRepository
{
	private readonly PgPool _db;

	public PgGameDataRepository(PgPool db) => _db = db;

	// ------------------------------------------------------------ catálogo

	public async Task<IReadOnlyList<CatalogEntry>> Catalog()
	{
		PgResult r = await _db.Query("select * from catalog_items order by sort, id");
		var list = new List<CatalogEntry>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new CatalogEntry(r.Text(i, "id"), r.Text(i, "name"), r.Text(i, "type"), r.Text(i, "kind"), r.Int(i, "rarity"),
				r.Int(i, "min_rank"), r.Text(i, "currency"), JsonSerializer.Deserialize<int[]>(r.Text(i, "prices")), r.Bool(i, "starter"),
				r.Bool(i, "enabled"), r.Bool(i, "hot"), r.Int(i, "team"), r.Text(i, "image"), r.Int(i, "sort")) { Consumable = r.Bool(i, "consumable") });
		return list;
	}

	private const string ItemInsert =
		"insert into catalog_items (id, name, type, kind, rarity, min_rank, currency, prices, starter, enabled, hot, team, image, sort, consumable) " +
		"values ($1, $2, $3, $4, $5, $6, $7, $8::jsonb, $9, $10, $11, $12, $13, $14, $15) ";

	private static object[] ItemArgs(CatalogEntry e) => new object[]
	{
		e.Id, e.Name, e.Type ?? "", e.Kind, e.Rarity, e.MinRank, e.Currency, JsonSerializer.Serialize(e.Prices), e.Starter,
		e.Enabled, e.Hot, e.Team, e.Image ?? "", e.Sort, e.Consumable
	};

	public Task SaveItem(CatalogEntry e) => _db.Exec(ItemInsert +
		"on conflict (id) do update set name = excluded.name, type = excluded.type, kind = excluded.kind, rarity = excluded.rarity, " +
		"min_rank = excluded.min_rank, currency = excluded.currency, prices = excluded.prices, starter = excluded.starter, " +
		"enabled = excluded.enabled, hot = excluded.hot, team = excluded.team, image = excluded.image, sort = excluded.sort, consumable = excluded.consumable, updated_at = now()",
		ItemArgs(e));

	public async Task<int> SeedItems(IEnumerable<CatalogEntry> items)
	{
		int made = 0;
		foreach (CatalogEntry e in items)
			// O que o painel edita fica como está; a arte é do jogo e o acompanha.
			made += (await _db.Query(ItemInsert +
				"on conflict (id) do update set image = excluded.image where excluded.image <> '' and catalog_items.image <> excluded.image", ItemArgs(e))).Affected;
		return made;
	}

	// ------------------------------------------------------------ mapas

	public async Task<IReadOnlyList<GameMap>> Maps()
	{
		PgResult r = await _db.Query("select * from game_maps order by sort, id");
		var list = new List<GameMap>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new GameMap(r.Text(i, "id"), r.Text(i, "name"), r.Text(i, "size"), r.Text(i, "description"),
				JsonSerializer.Deserialize<string[]>(r.Text(i, "modes")), r.Bool(i, "enabled"), r.Text(i, "image"), r.Int(i, "sort")));
		return list;
	}

	private const string MapInsert =
		"insert into game_maps (id, name, size, description, modes, enabled, image, sort) values ($1, $2, $3, $4, $5::jsonb, $6, $7, $8) ";

	private static object[] MapArgs(GameMap m) => new object[]
		{ m.Id, m.Name, m.Size ?? "", m.Description ?? "", JsonSerializer.Serialize(m.Modes), m.Enabled, m.Image ?? "", m.Sort };

	public Task SaveMap(GameMap m) => _db.Exec(MapInsert +
		"on conflict (id) do update set name = excluded.name, size = excluded.size, description = excluded.description, " +
		"modes = excluded.modes, enabled = excluded.enabled, image = excluded.image, sort = excluded.sort, updated_at = now()", MapArgs(m));

	public async Task<int> SeedMaps(IEnumerable<GameMap> maps)
	{
		int made = 0;
		foreach (GameMap m in maps)
			// Nome, descrição e "ligado" são do painel. A foto e os modos para
			// que o mapa foi feito são do jogo: acompanham a semente.
			made += (await _db.Query(MapInsert +
				"on conflict (id) do update set modes = excluded.modes, " +
				"image = case when excluded.image <> '' then excluded.image else game_maps.image end " +
				"where game_maps.modes <> excluded.modes or (excluded.image <> '' and game_maps.image <> excluded.image)", MapArgs(m))).Affected;
		return made;
	}

	// ------------------------------------------------------------ promoções

	public async Task<IReadOnlyList<Promotion>> Promotions(bool includeEnded)
	{
		PgResult r = await _db.Query(
			"select * from promotions " + (includeEnded ? "" : "where active and ends_at > now() ") + "order by ends_at desc, id desc limit 500");
		var list = new List<Promotion>();
		for (int i = 0; i < r.Rows.Count; i++) list.Add(PromotionOf(r, i));
		return list;
	}

	public async Task<Promotion> AddPromotion(Promotion p, long adminId)
	{
		PgResult r = await _db.Query(
			"insert into promotions (item_id, title, percent, starts_at, ends_at, active, created_by) values ($1, $2, $3, $4, $5, $6, $7) returning *",
			p.ItemId, p.Title ?? "", p.Percent, p.StartsAt, p.EndsAt, p.Active, adminId);
		return PromotionOf(r, 0);
	}

	public async Task<bool> RemovePromotion(long id) => (await _db.Query("delete from promotions where id = $1", id)).Affected > 0;

	private static Promotion PromotionOf(PgResult r, int i) =>
		new(r.Long(i, "id"), r.Text(i, "item_id"), r.Text(i, "title"), r.Int(i, "percent"), r.Date(i, "starts_at").Value,
			r.Date(i, "ends_at").Value, r.Bool(i, "active"));

	// ------------------------------------------------------------ desafios

	public async Task<IReadOnlyList<Challenge>> Challenges()
	{
		PgResult r = await _db.Query("select * from challenges order by sort, id");
		var list = new List<Challenge>();
		for (int i = 0; i < r.Rows.Count; i++)
			list.Add(new Challenge(r.Text(i, "id"), r.Text(i, "period"), r.Text(i, "text"), r.Text(i, "metric"), r.Int(i, "target"),
				r.Int(i, "presas"), r.Int(i, "xp"), r.Int(i, "sangue"), r.Text(i, "item_id"), r.Int(i, "item_days"), r.Text(i, "title"),
				r.Bool(i, "active"), r.Date(i, "starts_at"), r.Date(i, "ends_at"), r.Int(i, "sort")));
		return list;
	}

	private const string ChallengeInsert =
		"insert into challenges (id, period, text, metric, target, presas, xp, sangue, item_id, item_days, title, active, starts_at, ends_at, sort) " +
		"values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15) ";

	private static object[] ChallengeArgs(Challenge c) => new object[]
	{
		c.Id, c.Period, c.Text, c.Metric, c.Target, c.Presas, c.Xp, c.Sangue, string.IsNullOrEmpty(c.ItemId) ? null : c.ItemId, c.ItemDays,
		string.IsNullOrEmpty(c.Title) ? null : c.Title, c.Active, c.StartsAt, c.EndsAt, c.Sort
	};

	public Task SaveChallenge(Challenge c) => _db.Exec(ChallengeInsert +
		"on conflict (id) do update set period = excluded.period, text = excluded.text, metric = excluded.metric, target = excluded.target, " +
		"presas = excluded.presas, xp = excluded.xp, sangue = excluded.sangue, item_id = excluded.item_id, item_days = excluded.item_days, " +
		"title = excluded.title, active = excluded.active, starts_at = excluded.starts_at, ends_at = excluded.ends_at, sort = excluded.sort, " +
		"updated_at = now()", ChallengeArgs(c));

	public async Task<bool> RemoveChallenge(string id) => (await _db.Query("delete from challenges where id = $1", id)).Affected > 0;

	public async Task<int> SeedChallenges(IEnumerable<Challenge> challenges)
	{
		int made = 0;
		foreach (Challenge c in challenges)
			made += (await _db.Query(ChallengeInsert + "on conflict (id) do nothing", ChallengeArgs(c))).Affected;
		return made;
	}

	// ------------------------------------------------------------ configurações

	public async Task<IReadOnlyDictionary<string, string>> Settings()
	{
		PgResult r = await _db.Query("select key, value from settings");
		return Enumerable.Range(0, r.Rows.Count).ToDictionary(i => r.Text(i, "key"), i => r.Text(i, "value"));
	}

	public Task SaveSetting(string key, string json) => _db.Exec(
		"insert into settings (key, value) values ($1, $2::jsonb) on conflict (key) do update set value = excluded.value, updated_at = now()", key, json);
}
