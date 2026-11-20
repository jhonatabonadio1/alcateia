using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
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

	public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
	{
		[Maintenance] = "{\"on\":false,\"message\":\"\"}",
		[MinVersion] = "{\"version\":\"0.0.0\"}",
		[Notice] = "{\"text\":\"\"}",
		[Bonus] = "{\"xp\":1,\"presas\":1,\"until\":null}"
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

	public bool Maintenance => Settings.TryGetValue(SettingKeys.Maintenance, out JsonNode m) && (m["on"]?.GetValue<bool>() ?? false);
	public string MaintenanceMessage => Settings.TryGetValue(SettingKeys.Maintenance, out JsonNode m) ? m["message"]?.GetValue<string>() ?? "" : "";

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
	private IReadOnlyList<string> _releasedModes = new[] { "equipe" };

	public GameDataService(IGameDataRepository repository) => _repository = repository;

	public GameData Current => _current;

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

		data.Json = ToJson(data).ToJsonString();
		data.ETag = "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.Json)))[..16] + "\"";
		_current = data;
	}

	/// <summary>O mapa pode ser jogado? Mapa que o servidor não conhece
	/// passa (o jogo pode ter saído com um mapa novo antes da semente);
	/// o que o painel desligou, não.</summary>
	public bool MapAllowed(string id)
	{
		GameMap map = _current.Maps.FirstOrDefault(m => m.Id == id);
		return map == null || map.Enabled;
	}

	private static JsonObject ToJson(GameData d)
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
		foreach (Promotion p in d.Promotions.Where(p => p.Active))
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
			["items"] = items, ["maps"] = maps, ["promotions"] = promotions, ["challenges"] = challenges, ["settings"] = settings
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
