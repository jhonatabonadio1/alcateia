using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;
using Alcateia.Dominio.Painel;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Aplicacao.Painel;

/// <summary>
/// O conteúdo do jogo que o painel edita: catálogo (preço, raridade,
/// patente, à venda ou não), mapas (ligado ou não), promoções, desafios e
/// configurações. Cada mudança é conferida pela regra do domínio, vai
/// para a auditoria e renova os dados que o jogo lê.
/// </summary>
public sealed class ContentService
{
	private readonly IGameDataRepository _repository;
	private readonly GameDataService _data;
	private readonly Auditor _audit;

	public ContentService(IGameDataRepository repository, GameDataService data, Auditor audit)
	{
		_repository = repository;
		_data = data;
		_audit = audit;
	}

	public GameData Current(AdminContext who)
	{
		who.Require(Permission.View);
		return _data.Current;
	}

	public Task<IReadOnlyList<Promotion>> Promotions(AdminContext who)
	{
		who.Require(Permission.View);
		return _repository.Promotions(includeEnded: true);
	}

	public async Task SaveItem(AdminContext who, CatalogEntry item)
	{
		who.Require(Permission.Content);
		Shop.Check(item);
		_data.Current.Items.TryGetValue(item.Id, out CatalogEntry before);
		await _repository.SaveItem(item);
		await _data.Reload();
		await _audit.Note(who, before == null ? "catalogo.criou" : "catalogo.alterou", item.Id, Diff(before, item));
	}

	public async Task SaveMap(AdminContext who, GameMap map)
	{
		who.Require(Permission.Content);
		if (string.IsNullOrWhiteSpace(map.Id) || map.Id.Length > 24) throw new Failure(FailureKind.Invalid, "id_invalido", "Identificador de mapa inválido.");
		if (string.IsNullOrWhiteSpace(map.Name) || map.Name.Length > 60) throw new Failure(FailureKind.Invalid, "nome_invalido", "Nome inválido.");

		GameMap before = _data.Current.Maps.FirstOrDefault(m => m.Id == map.Id);
		if (!map.Enabled && _data.Current.Maps.Count(m => m.Enabled && m.Id != map.Id) == 0)
			throw new Failure(FailureKind.Conflict, "ultimo_mapa", "Pelo menos um mapa precisa ficar ligado.");

		await _repository.SaveMap(map);
		await _data.Reload();
		string what = before == null ? "criado" : before.Enabled != map.Enabled ? (map.Enabled ? "ligado" : "desligado") : "alterado";
		await _audit.Note(who, "mapa." + what, map.Id);
	}

	public async Task<Promotion> AddPromotion(AdminContext who, Promotion promotion)
	{
		who.Require(Permission.Content);
		Shop.Check(promotion);
		if (!_data.Current.Items.TryGetValue(promotion.ItemId ?? "", out CatalogEntry item) || !item.ForSale)
			throw new Failure(FailureKind.Invalid, "item_inexistente", "Promoção só em item que está à venda.");

		Promotion made = await _repository.AddPromotion(promotion, who.Admin.Id);
		await _data.Reload();
		await _audit.Note(who, "promocao.criou", item.Id, $"{made.Percent}% de {GameDataService.Iso(made.StartsAt)} a {GameDataService.Iso(made.EndsAt)}");
		return made;
	}

	public async Task RemovePromotion(AdminContext who, long id)
	{
		who.Require(Permission.Content);
		if (!await _repository.RemovePromotion(id)) throw new Failure(FailureKind.NotFound, "promocao_inexistente", "Essa promoção não existe.");
		await _data.Reload();
		await _audit.Note(who, "promocao.removeu", id.ToString());
	}

	public async Task SaveChallenge(AdminContext who, Challenge challenge)
	{
		who.Require(Permission.Content);
		ChallengeRules.Check(challenge);
		if (!string.IsNullOrEmpty(challenge.ItemId) && !_data.Current.Items.ContainsKey(challenge.ItemId))
			throw new Failure(FailureKind.Invalid, "item_inexistente", "O item do prêmio não existe no catálogo.");

		bool isNew = _data.Current.Challenges.All(c => c.Id != challenge.Id);
		await _repository.SaveChallenge(challenge);
		await _data.Reload();
		await _audit.Note(who, isNew ? "desafio.criou" : "desafio.alterou", challenge.Id,
			$"{challenge.Text} — {challenge.Metric} {challenge.Target}; {challenge.Presas} Cash, {challenge.Xp} XP, {challenge.Sangue} IPs");
	}

	public async Task RemoveChallenge(AdminContext who, string id)
	{
		who.Require(Permission.Content);
		if (!await _repository.RemoveChallenge(id)) throw new Failure(FailureKind.NotFound, "desafio_inexistente", "Esse desafio não existe.");
		await _data.Reload();
		await _audit.Note(who, "desafio.removeu", id);
	}

	/// <summary>Muda uma configuração do servidor. Só as chaves conhecidas,
	/// e com o formato conferido.</summary>
	public async Task SaveSetting(AdminContext who, string key, JsonNode value)
	{
		// As notícias são conteúdo (quem cuida da loja e dos mapas publica); o resto é do servidor.
		who.Require(key == SettingKeys.News ? Permission.Content : Permission.Manage);
		if (!SettingKeys.Defaults.ContainsKey(key ?? "")) throw new Failure(FailureKind.NotFound, "configuracao_inexistente", "Não conheço essa configuração.");
		if (value is not JsonObject o) throw new Failure(FailureKind.Invalid, "valor_invalido", "Valor inválido.");

		JsonObject clean = key switch
		{
			SettingKeys.Maintenance => new JsonObject { ["on"] = o["on"]?.GetValue<bool>() ?? false, ["message"] = Cut(o["message"]?.GetValue<string>(), 200) },
			SettingKeys.MinVersion => new JsonObject { ["version"] = Version(o["version"]?.GetValue<string>()) },
			SettingKeys.Notice => new JsonObject { ["text"] = Cut(o["text"]?.GetValue<string>(), 300) },
			SettingKeys.Challenges => new JsonObject { ["daily"] = Count(o["daily"]), ["weekly"] = Count(o["weekly"]) },
			SettingKeys.Showcase => new JsonObject { ["count"] = Count(o["count"]), ["percent"] = Percent(o["percent"]) },
			SettingKeys.News => new JsonObject { ["banners"] = NewsList(o["banners"], MaxBanners, banner: true), ["noticias"] = NewsList(o["noticias"], MaxStories, banner: false) },
			SettingKeys.Rewards => new JsonObject
			{
				["base"] = Cash(o["base"]), ["kill"] = Cash(o["kill"]), ["assist"] = Cash(o["assist"]),
				["win"] = Cash(o["win"]), ["draw"] = Cash(o["draw"]), ["mvp"] = Cash(o["mvp"]), ["starter"] = Cash(o["starter"])
			},
			_ => new JsonObject
			{
				["xp"] = Bonus(o["xp"]), ["presas"] = Bonus(o["presas"]),
				["until"] = string.IsNullOrEmpty(o["until"]?.GetValue<string>()) ? null : GameDataService.Iso(DateTime.Parse(o["until"].GetValue<string>(), null, System.Globalization.DateTimeStyles.AdjustToUniversal))
			}
		};

		await _repository.SaveSetting(key, clean.ToJsonString());
		await _data.Reload();
		await _audit.Note(who, "configuracao.alterou", key, clean.ToJsonString());
	}

	/// <summary>Quantos banners e quantas notícias o painel guarda, no máximo
	/// (o jogo mostra os primeiros publicados de cada lista).</summary>
	public const int MaxBanners = 8, MaxStories = 12;

	private static readonly string[] NewsTargets = { "", "jogar", "loja", "inventario", "missoes" };

	/// <summary>Uma lista de banners ou de notícias vinda do painel: só os
	/// campos conhecidos, cada texto no tamanho que cabe na tela do jogo.
	/// Item sem título não entra.</summary>
	private static JsonArray NewsList(JsonNode node, int max, bool banner)
	{
		var clean = new JsonArray();
		if (node is not JsonArray list) return clean;
		if (list.Count > max) throw new Failure(FailureKind.Invalid, "noticias_demais", $"No máximo {max} por lista.");

		foreach (JsonNode item in list)
		{
			if (item is not JsonObject o) continue;
			string title = Cut(Text(o["title"]).Trim(), 60);
			if (title.Length == 0) throw new Failure(FailureKind.Invalid, "noticia_sem_titulo", "Toda notícia precisa de título.");

			var entry = new JsonObject
			{
				["category"] = Cut(Text(o["category"]).Trim(), 24),
				["title"] = title,
				["body"] = Cut(Text(o["body"]).Trim(), 240),
				["image"] = Cut(Text(o["image"]).Trim(), 40),
				["on"] = o["on"] is JsonValue on && on.TryGetValue(out bool published) ? published : true
			};
			if (banner)
			{
				string go = Text(o["go"]);
				if (!NewsTargets.Contains(go)) throw new Failure(FailureKind.Invalid, "destino_invalido", "O botão leva a: jogar, loja, inventario, missoes ou a lugar nenhum.");
				entry["cta"] = Cut(Text(o["cta"]).Trim(), 32);
				entry["go"] = go;
			}
			else entry["date"] = Cut(Text(o["date"]).Trim(), 16);
			clean.Add(entry);
		}
		return clean;
	}

	/// <summary>O texto de um campo; outro tipo (número, lista) vale vazio.</summary>
	private static string Text(JsonNode node) => node is JsonValue v && v.TryGetValue(out string text) ? text ?? "" : "";

	private static int Percent(JsonNode node)
	{
		int value = node?.GetValue<int>() ?? 0;
		if (value is < 0 or > Shop.MaxPercent) throw new Failure(FailureKind.Invalid, "desconto_invalido", $"Desconto de 0 a {Shop.MaxPercent}%.");
		return value;
	}

	/// <summary>Um valor de Cash do painel: de 0 a 100 000.</summary>
	private static int Cash(JsonNode node)
	{
		int value = node?.GetValue<int>() ?? 0;
		if (value is < 0 or > 100000) throw new Failure(FailureKind.Invalid, "cash_invalido", "O Cash vai de 0 a 100 000.");
		return value;
	}

	private static int Count(JsonNode node)
	{
		int value = node?.GetValue<int>() ?? 0;
		if (value is < 0 or > 50) throw new Failure(FailureKind.Invalid, "quantidade_invalida", "A quantidade vai de 0 a 50.");
		return value;
	}

	private static double Bonus(JsonNode node)
	{
		double value = node?.GetValue<double>() ?? 1;
		if (value < 1 || value > MatchSettlement.MaxBonus) throw new Failure(FailureKind.Invalid, "bonus_invalido", $"O bônus vai de 1 a {MatchSettlement.MaxBonus}.");
		return Math.Round(value, 2);
	}

	private static string Version(string text)
	{
		if (!System.Version.TryParse(text ?? "", out Version v)) throw new Failure(FailureKind.Invalid, "versao_invalida", "Versão no formato 1.2.3.");
		return v.ToString();
	}

	private static string Cut(string text, int max) => text == null ? "" : text.Length <= max ? text : text[..max];

	private static string Diff(CatalogEntry before, CatalogEntry after)
	{
		if (before == null) return $"{after.Name} ({after.Kind})";
		var changes = new List<string>();
		if (before.Name != after.Name) changes.Add($"nome: {before.Name} → {after.Name}");
		if (before.Rarity != after.Rarity) changes.Add($"raridade: {before.Rarity} → {after.Rarity}");
		if (before.MinRank != after.MinRank) changes.Add($"patente: {before.MinRank} → {after.MinRank}");
		if (before.Currency != after.Currency) changes.Add($"moeda: {before.Currency} → {after.Currency}");
		if (!before.Prices.SequenceEqual(after.Prices)) changes.Add($"preços: [{string.Join(", ", before.Prices)}] → [{string.Join(", ", after.Prices)}]");
		if (before.Starter != after.Starter) changes.Add(after.Starter ? "virou item de série" : "deixou de ser item de série");
		if (before.Enabled != after.Enabled) changes.Add(after.Enabled ? "ligado" : "desligado");
		if (before.Hot != after.Hot) changes.Add(after.Hot ? "em destaque" : "fora do destaque");
		return changes.Count == 0 ? "sem mudança" : string.Join("; ", changes);
	}
}
