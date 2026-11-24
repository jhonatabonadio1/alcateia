using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// O que do jogo mora no servidor e o painel administrativo muda sem
/// versão nova: que mapas estão em rotação, o preço e o desconto de cada
/// item, o aviso do saguão, a manutenção, o bônus de XP.
///
/// O jogo pede tudo de uma vez (<c>GET /v1/gamedata</c>) ao entrar na
/// conta e de tempos em tempos; se nada mudou, o servidor responde 304 e
/// nada é lido de novo. Sem servidor (ou jogando offline) vale o que vem
/// com o jogo: todo mapa ligado, preço do <see cref="Catalog"/>.
/// </summary>
public static class ServerData
{
	public sealed record Promo(string ItemId, int Percent, DateTime StartsAt, DateTime EndsAt);

	private static string _etag;
	private static DateTime _checkedAt = DateTime.MinValue;
	private static readonly HashSet<string> MapsOff = new();
	private static readonly HashSet<string> ItemsKnown = new();
	private static readonly HashSet<string> Daily = new();
	private static readonly List<Promo> Promos = new();

	/// <summary>Já chegou alguma resposta do servidor.</summary>
	public static bool Loaded { get; private set; }

	/// <summary>O aviso que o painel escreveu para o saguão (vazio: nenhum).</summary>
	public static string Notice { get; private set; } = "";

	public static bool Maintenance { get; private set; }
	public static string MinVersion { get; private set; } = "0.0.0";

	/// <summary>O multiplicador de evento (1 = normal).</summary>
	public static double XpBonus { get; private set; } = 1;
	public static double PresasBonus { get; private set; } = 1;

	/// <summary>Mudou algo que a tela mostra.</summary>
	public static event Action Changed;

	/// <summary>Pede os dados de novo, se já faz um tempo que pediu.</summary>
	public static async Task Refresh(double everySeconds = 60)
	{
		if ((DateTime.UtcNow - _checkedAt).TotalSeconds < everySeconds) return;
		_checkedAt = DateTime.UtcNow;

		var (status, etag, body) = await Backend.GameData(_etag);
		if (status != 200 || body == null) return;       // 304: nada mudou; 0: sem servidor
		_etag = etag;
		Apply(body);
		Changed?.Invoke();
	}

	/// <summary>O mapa está em rotação? Mapa que o servidor não conhece está.</summary>
	public static bool MapOn(string id) => !MapsOff.Contains(id ?? "");

	/// <summary>O primeiro mapa do jogo que está em rotação.</summary>
	public static string FirstMapOn() => (Maps.All.FirstOrDefault(m => MapOn(m.Id)) ?? Maps.All[0]).Id;

	/// <summary>O item existe e está ligado no servidor (ou o servidor ainda não disse nada).</summary>
	public static bool ItemOn(string id) => !Loaded || ItemsKnown.Contains(id ?? "");

	/// <summary>Os itens da vitrine de hoje (com desconto até o dia virar).</summary>
	public static IReadOnlyCollection<string> DailyItems => Daily;

	/// <summary>Até quando vale a vitrine de hoje (nulo: não há vitrine).</summary>
	public static DateTime? DailyEndsUtc
	{
		get
		{
			DateTime? ends = null;
			foreach (Promo p in Promos)
				if (Daily.Contains(p.ItemId) && p.EndsAt > DateTime.UtcNow && (ends == null || p.EndsAt < ends)) ends = p.EndsAt;
			return ends;
		}
	}

	/// <summary>O desconto valendo agora para o item, em por cento (0: nenhum).</summary>
	public static int Discount(string itemId)
	{
		DateTime now = DateTime.UtcNow;
		int best = 0;
		foreach (Promo p in Promos)
			if (p.ItemId == itemId && p.StartsAt <= now && now < p.EndsAt) best = Math.Max(best, p.Percent);
		return best;
	}

	/// <summary>O preço que o servidor cobra hoje por essa duração, já com
	/// o desconto; −1 se não se vende. Sem dados do servidor, o do jogo.</summary>
	public static int Price(CatalogItem item, Duration duration) => Catalog.Price(item, duration);

	/// <summary>Lê a resposta de <c>/v1/gamedata</c>.</summary>
	public static void Apply(JsonObject data)
	{
		MapsOff.Clear();
		foreach (JsonNode m in data["maps"] as JsonArray ?? new JsonArray())
			if (!(m["enabled"]?.GetValue<bool>() ?? true)) MapsOff.Add(m["id"]?.GetValue<string>() ?? "");

		// A economia de cada item passa a ser a do servidor (o painel muda
		// preço, raridade, patente e destaque sem versão nova do jogo).
		Catalog.RestoreBuiltIn();
		ItemsKnown.Clear();
		foreach (JsonNode i in data["items"] as JsonArray ?? new JsonArray())
		{
			string id = i["id"]?.GetValue<string>() ?? "";
			ItemsKnown.Add(id);
			Catalog.ApplyServer(id,
				(i["prices"] as JsonArray)?.Select(p => p.GetValue<int>()).ToArray(),
				i["rarity"]?.GetValue<int>() ?? Catalog.Get(id)?.Rarity ?? 0,
				i["minRank"]?.GetValue<int>() ?? Catalog.Get(id)?.MinRank ?? 0,
				i["hot"]?.GetValue<bool>() ?? Catalog.Get(id)?.Hot ?? false,
				i["starter"]?.GetValue<bool>() ?? Catalog.Get(id)?.Starter ?? false);
		}

		Daily.Clear();
		foreach (JsonNode d in data["dailyItems"] as JsonArray ?? new JsonArray()) Daily.Add(d.GetValue<string>());

		Promos.Clear();
		foreach (JsonNode p in data["promotions"] as JsonArray ?? new JsonArray())
			if (DateTime.TryParse(p["startsAt"]?.GetValue<string>(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime from)
				&& DateTime.TryParse(p["endsAt"]?.GetValue<string>(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime to))
				Promos.Add(new Promo(p["itemId"]?.GetValue<string>() ?? "", p["percent"]?.GetValue<int>() ?? 0, from, to));

		JsonNode settings = data["settings"];
		Notice = settings?["aviso"]?["text"]?.GetValue<string>() ?? "";
		Maintenance = settings?["manutencao"]?["on"]?.GetValue<bool>() ?? false;
		MinVersion = settings?["versao_minima"]?["version"]?.GetValue<string>() ?? "0.0.0";
		XpBonus = Number(settings?["bonus"]?["xp"]);
		PresasBonus = Number(settings?["bonus"]?["presas"]);
		Loaded = true;
	}

	/// <summary>Esquece tudo (os testes, e ao trocar de servidor).</summary>
	public static void Reset()
	{
		_etag = null;
		_checkedAt = DateTime.MinValue;
		MapsOff.Clear();
		Catalog.RestoreBuiltIn();
		ItemsKnown.Clear();
		Daily.Clear();
		Promos.Clear();
		Notice = "";
		Maintenance = false;
		MinVersion = "0.0.0";
		XpBonus = PresasBonus = 1;
		Loaded = false;
	}

	private static double Number(JsonNode node)
	{
		try { return node == null ? 1 : Math.Max(1, node.GetValue<double>()); }
		catch (InvalidOperationException) { return 1; }
	}
}
