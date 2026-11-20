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
	private static readonly Dictionary<string, int[]> Prices = new();
	private static readonly HashSet<string> ItemsKnown = new();
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

	/// <summary>O desconto valendo agora para o item, em por cento (0: nenhum).</summary>
	public static int Discount(string itemId)
	{
		DateTime now = DateTime.UtcNow;
		int best = 0;
		foreach (Promo p in Promos)
			if (p.ItemId == itemId && p.StartsAt <= now && now < p.EndsAt) best = Math.Max(best, p.Percent);
		return best;
	}

	/// <summary>O preço do servidor para essa duração, já com o desconto;
	/// −1 se não se vende. Sem dados do servidor, o do catálogo do jogo.</summary>
	public static int Price(CatalogItem item, Duration duration)
	{
		int[] prices = Prices.TryGetValue(item.Id, out int[] server) ? server : item.Prices;
		int index = (int)duration;
		if (prices == null || index >= prices.Length || prices[index] < 0) return -1;
		int percent = Discount(item.Id);
		return percent <= 0 ? prices[index] : Math.Max(1, (int)Math.Round(prices[index] * (100 - percent) / 100.0));
	}

	/// <summary>Lê a resposta de <c>/v1/gamedata</c>.</summary>
	public static void Apply(JsonObject data)
	{
		MapsOff.Clear();
		foreach (JsonNode m in data["maps"] as JsonArray ?? new JsonArray())
			if (!(m["enabled"]?.GetValue<bool>() ?? true)) MapsOff.Add(m["id"]?.GetValue<string>() ?? "");

		Prices.Clear();
		ItemsKnown.Clear();
		foreach (JsonNode i in data["items"] as JsonArray ?? new JsonArray())
		{
			string id = i["id"]?.GetValue<string>() ?? "";
			ItemsKnown.Add(id);
			if (i["prices"] is JsonArray prices && prices.Count > 0) Prices[id] = prices.Select(p => p.GetValue<int>()).ToArray();
		}

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
		Prices.Clear();
		ItemsKnown.Clear();
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
