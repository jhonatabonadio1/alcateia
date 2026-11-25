using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;

namespace Alcateia.Aplicacao.Jogo;

/// <summary>As configurações que o painel edita, com o padrão de cada uma.</summary>
public static class SettingKeys
{
	/// <summary><c>{ "on": false, "message": "" }</c> — em manutenção ninguém entra.</summary>
	public const string Maintenance = "manutencao";
	/// <summary><c>{ "version": "0.0.0" }</c> — cliente mais velho que isso tem de atualizar.</summary>
	public const string MinVersion = "versao_minima";
	/// <summary><c>{ "text": "" }</c> — o aviso que aparece no saguão.</summary>
	public const string Notice = "aviso";
	/// <summary><c>{ "xp": 1, "presas": 1, "until": null }</c> — evento de XP ou Presas em dobro.</summary>
	public const string Bonus = "bonus";
	/// <summary><c>{ "daily": 3, "weekly": 5 }</c> — quantos desafios diários e
	/// semanais valem por vez (o resto entra no rodízio); 0 é "todos".</summary>
	public const string Challenges = "desafios";
	/// <summary><c>{ "count": 4, "percent": 20 }</c> — a vitrine do dia: quantos
	/// itens e com que desconto; count 0 desliga.</summary>
	public const string Showcase = "vitrine";
	/// <summary><c>{ "base": 150, "kill": 10, "assist": 5, "win": 100, "draw": 50, "mvp": 50, "starter": 0 }</c> —
	/// o Cash que cada coisa rende numa partida e o com que a conta nova começa.</summary>
	public const string Rewards = "recompensas";

	public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
	{
		[Maintenance] = "{\"on\":false,\"message\":\"\"}",
		[MinVersion] = "{\"version\":\"0.0.0\"}",
		[Notice] = "{\"text\":\"\"}",
		[Bonus] = "{\"xp\":1,\"presas\":1,\"until\":null}",
		[Challenges] = "{\"daily\":3,\"weekly\":5}",
		[Showcase] = "{\"count\":4,\"percent\":20}",
		[Rewards] = "{\"base\":150,\"kill\":10,\"assist\":5,\"win\":100,\"draw\":50,\"mvp\":50,\"starter\":0}"
	};
}

/// <summary>Uma fotografia dos dados do jogo: o que estava no banco na
/// última vez que alguém mexeu.</summary>
public sealed class GameData
{
	public IReadOnlyList<CatalogEntry> Catalog = Array.Empty<CatalogEntry>();
	public IReadOnlyDictionary<string, CatalogEntry> Items = new Dictionary<string, CatalogEntry>();
	public IReadOnlyList<GameMap> Maps = Array.Empty<GameMap>();
	public IReadOnlyList<Promotion> Promotions = Array.Empty<Promotion>();
	public IReadOnlyList<Challenge> Challenges = Array.Empty<Challenge>();
	public IReadOnlyDictionary<string, JsonNode> Settings = new Dictionary<string, JsonNode>();

	/// <summary>O que vai para o jogo, já em JSON, e a etiqueta dele.</summary>
	public string Json = "{}", ETag = "\"0\"";

	/// <summary>O dia do jogo para o qual o JSON foi montado (a vitrine do dia vai nele).</summary>
	public string DayKey = "";

	/// <summary>As promoções valendo: as do painel e a vitrine do dia.</summary>
	public List<Promotion> PromotionsAt(DateTime now)
	{
		int count = Shop.DailyCount, percent = Shop.DailyPercent;
		if (Settings.TryGetValue(SettingKeys.Showcase, out JsonNode v))
		{
			count = v["count"]?.GetValue<int>() ?? count;
			percent = v["percent"]?.GetValue<int>() ?? percent;
		}
		var all = new List<Promotion>(Promotions);
		all.AddRange(Shop.DailyShowcase(Catalog, now, count, percent));
		return all;
	}

	/// <summary>Uma cópia com os mesmos dados (as listas não mudam depois de montadas).</summary>
	public GameData Copy() => new()
	{
		Catalog = Catalog, Items = Items, Maps = Maps, Promotions = Promotions, Challenges = Challenges, Settings = Settings
	};

	public bool Maintenance => Settings.TryGetValue(SettingKeys.Maintenance, out JsonNode m) && (m["on"]?.GetValue<bool>() ?? false);
	public string MaintenanceMessage => Settings.TryGetValue(SettingKeys.Maintenance, out JsonNode m) ? m["message"]?.GetValue<string>() ?? "" : "";

	/// <summary>Quantos desafios diários e semanais valem por vez.</summary>
	public (int Daily, int Weekly) ChallengeCounts
	{
		get
		{
			if (!Settings.TryGetValue(SettingKeys.Challenges, out JsonNode c)) return (ChallengeRules.DailyCount, ChallengeRules.WeeklyCount);
			return (c["daily"]?.GetValue<int>() ?? ChallengeRules.DailyCount, c["weekly"]?.GetValue<int>() ?? ChallengeRules.WeeklyCount);
		}
	}

	/// <summary>Os desafios valendo agora (com o rodízio dos diários e semanais).</summary>
	public List<Challenge> RunningChallenges(DateTime now)
	{
		var (daily, weekly) = ChallengeCounts;
		return ChallengeRules.Running(Challenges, now, daily, weekly);
	}

	/// <summary>O Cash por partida valendo (o do painel, ou o de fábrica).</summary>
	public global::Rewards.CashRates Cash()
	{
		var d = global::Rewards.CashRates.Default;
		if (!Settings.TryGetValue(SettingKeys.Rewards, out JsonNode r)) return d;
		return new(Int(r["base"], d.Base), Int(r["kill"], d.Kill), Int(r["assist"], d.Assist), Int(r["win"], d.Win), Int(r["draw"], d.Draw), Int(r["mvp"], d.Mvp));
	}

	/// <summary>O Cash de uma conta nova.</summary>
	public long StarterCash => Settings.TryGetValue(SettingKeys.Rewards, out JsonNode r) ? Int(r["starter"], 0) : 0;

	private static int Int(JsonNode node, int fallback)
	{
		try { return node == null ? fallback : Math.Clamp(node.GetValue<int>(), 0, 100000); }
		catch (Exception e) when (e is InvalidOperationException or FormatException) { return fallback; }
	}

	/// <summary>O multiplicador de evento valendo agora.</summary>
	public (double Xp, double Presas) Bonus(DateTime now)
	{
		if (!Settings.TryGetValue(SettingKeys.Bonus, out JsonNode b)) return (1, 1);
		string until = b["until"]?.GetValue<string>();
		if (!string.IsNullOrEmpty(until) && DateTime.TryParse(until, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime end) && end <= now)
			return (1, 1);
		return (Number(b["xp"]), Number(b["presas"]));
	}

	private static double Number(JsonNode node)
	{
		try { return node == null ? 1 : Math.Max(1, node.GetValue<double>()); }
		catch (InvalidOperationException) { return 1; }
	}
}

/// <summary>
/// Os dados do jogo que moram no servidor: catálogo, mapas, promoções,
/// desafios e configurações. O jogo pede tudo de uma vez
/// (<c>GET /v1/gamedata</c>) e, se nada mudou, recebe um 304 sem corpo.
///
/// Fica tudo em memória: ninguém vai ao banco para saber o preço de uma
/// arma ou se um mapa está ligado. Só relê quando o painel muda algo
/// (<see cref="Reload"/>) e, por garantia, de tempos em tempos.
/// </summary>
public sealed class GameDataService
{
	private readonly IGameDataRepository _repository;
	private volatile GameData _current = new();
	private readonly object _rebuild = new();
	private readonly Func<DateTime> _now;
	private IReadOnlyList<string> _releasedModes = new[] { "equipe" };

	public GameDataService(IGameDataRepository repository, Func<DateTime> now = null)
	{
		_repository = repository;
		_now = now ?? (() => DateTime.UtcNow);
	}

	/// <summary>Os dados de agora. Virou o dia do jogo? A vitrine do dia
	/// muda: o JSON é montado de novo (em memória, sem ir ao banco).</summary>
	public GameData Current
	{
		get
		{
			GameData data = _current;
			if (data.DayKey == GameClock.DayKey(_now())) return data;
			lock (_rebuild)
			{
				if (_current.DayKey != GameClock.DayKey(_now())) _current = Stamp(_current.Copy());
				return _current;
			}
		}
	}

	/// <summary>Os modos que o jogo deixa escolher hoje (vem com o jogo, na
	/// semente; o painel não muda). Um mapa pode ter sido feito para um modo
	/// que ainda não existe: o painel mostra isso em vez de anunciar o modo.</summary>
	public IReadOnlyList<string> ReleasedModes => _releasedModes;

	/// <summary>Põe no banco o que veio com o jogo e ainda não está lá, e
	/// acerta o que é do jogo (a arte, os modos de cada mapa).</summary>
	public async Task Seed(IEnumerable<CatalogEntry> items, IEnumerable<GameMap> maps, IEnumerable<Challenge> challenges,
		IReadOnlyList<string> releasedModes = null)
	{
		if (releasedModes != null && releasedModes.Count > 0) _releasedModes = releasedModes;
		await _repository.SeedItems(items);
		await _repository.SeedMaps(maps);
		await _repository.SeedChallenges(challenges);
		await Reload();
	}

	public async Task Reload()
	{
		var data = new GameData
		{
			Catalog = await _repository.Catalog(),
			Maps = await _repository.Maps(),
			Promotions = await _repository.Promotions(includeEnded: false),
			Challenges = await _repository.Challenges()
		};
		data.Items = data.Catalog.ToDictionary(i => i.Id);

		IReadOnlyDictionary<string, string> stored = await _repository.Settings();
		data.Settings = SettingKeys.Defaults.ToDictionary(d => d.Key, d => JsonNode.Parse(stored.TryGetValue(d.Key, out string json) ? json : d.Value));

		_current = Stamp(data);
	}

	/// <summary>Monta o JSON do jogo para o dia de hoje e a etiqueta dele.</summary>
	private GameData Stamp(GameData data)
	{
		DateTime now = _now();
		data.DayKey = GameClock.DayKey(now);
		data.Json = ToJson(data, now).ToJsonString();
		data.ETag = "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.Json)))[..16] + "\"";
		return data;
	}

	/// <summary>O mapa pode ser jogado? Mapa que o servidor não conhece
	/// passa (o jogo pode ter saído com um mapa novo antes da semente);
	/// o que o painel desligou, não.</summary>
	public bool MapAllowed(string id)
	{
		GameMap map = _current.Maps.FirstOrDefault(m => m.Id == id);
		return map == null || map.Enabled;
	}

	private static JsonObject ToJson(GameData d, DateTime now)
	{
		var items = new JsonArray();
		foreach (CatalogEntry i in d.Catalog.Where(i => i.Enabled))
			items.Add(new JsonObject
			{
				["id"] = i.Id, ["name"] = i.Name, ["kind"] = i.Kind, ["rarity"] = i.Rarity, ["minRank"] = i.MinRank,
				["currency"] = i.Currency, ["prices"] = new JsonArray(i.Prices.Select(p => (JsonNode)p).ToArray()),
				["starter"] = i.Starter, ["hot"] = i.Hot, ["consumable"] = i.Consumable, ["team"] = i.Team
			});

		var maps = new JsonArray();
		foreach (GameMap m in d.Maps)
			maps.Add(new JsonObject
			{
				["id"] = m.Id, ["name"] = m.Name, ["enabled"] = m.Enabled, ["modes"] = new JsonArray(m.Modes.Select(x => (JsonNode)x).ToArray())
			});

		var promotions = new JsonArray();
		List<Promotion> valid = d.PromotionsAt(now);
		foreach (Promotion p in valid.Where(p => p.Active))
			promotions.Add(new JsonObject
			{
				["id"] = p.Id, ["itemId"] = p.ItemId, ["title"] = p.Title, ["percent"] = p.Percent,
				["startsAt"] = Iso(p.StartsAt), ["endsAt"] = Iso(p.EndsAt)
			});

		var challenges = new JsonArray();
		foreach (Challenge c in d.Challenges.Where(c => c.Active))
			challenges.Add(ChallengeJson(c));

		var settings = new JsonObject();
		foreach (var s in d.Settings) settings[s.Key] = s.Value.DeepClone();

		return new JsonObject
		{
			["items"] = items, ["maps"] = maps, ["promotions"] = promotions, ["challenges"] = challenges, ["settings"] = settings,
			// A vitrine do dia (também está nas promoções, com o desconto e até quando).
			["dailyItems"] = new JsonArray(valid.Where(p => p.Id == 0).Select(p => (JsonNode)p.ItemId).ToArray())
		};
	}

	public static JsonObject ChallengeJson(Challenge c) => new()
	{
		["id"] = c.Id, ["period"] = c.Period, ["text"] = c.Text, ["metric"] = c.Metric, ["target"] = c.Target,
		["presas"] = c.Presas, ["xp"] = c.Xp, ["sangue"] = c.Sangue, ["itemId"] = c.ItemId, ["itemDays"] = c.ItemDays, ["title"] = c.Title,
		["startsAt"] = c.StartsAt == null ? null : Iso(c.StartsAt.Value), ["endsAt"] = c.EndsAt == null ? null : Iso(c.EndsAt.Value)
	};

	public static string Iso(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
}
