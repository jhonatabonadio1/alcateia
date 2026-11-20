using System;
using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Dominio;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Painel: o conteúdo do jogo — catálogo, mapas, promoções,
/// desafios e configurações.</summary>
public static class AdminContentRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.Get(s, "/content", async (ctx, who) =>
		{
			GameData d = s.Content.Current(who);
			DateTime now = DateTime.UtcNow;
			var settings = new JsonObject();
			foreach (var setting in d.Settings) settings[setting.Key] = setting.Value.DeepClone();

			return new JsonObject
			{
				["catalog"] = new JsonArray(d.Catalog.Select(i => (JsonNode)ToJson(i, Shop.Discount(i.Id, d.Promotions, now))).ToArray()),
				["maps"] = new JsonArray(d.Maps.Select(m => (JsonNode)ToJson(m)).ToArray()),
				["promotions"] = new JsonArray((await s.Content.Promotions(who)).Select(p => (JsonNode)ToJson(p, now)).ToArray()),
				["challenges"] = new JsonArray(d.Challenges.Select(c =>
				{
					JsonObject one = GameDataService.ChallengeJson(c);
					one["active"] = c.Active;
					one["sort"] = c.Sort;
					one["running"] = c.RunningAt(now);
					return (JsonNode)one;
				}).ToArray()),
				["settings"] = settings,
				["kinds"] = new JsonArray(ItemKinds.All.Select(k => (JsonNode)k).ToArray()),
				["metrics"] = new JsonArray(Metrics.All.Select(k => (JsonNode)k).ToArray()),
				["periods"] = new JsonArray(Periods.All.Select(k => (JsonNode)k).ToArray()),
				["durations"] = new JsonArray(Shop.DurationDays.Select(k => (JsonNode)k).ToArray())
			};
		});

		app.Put(s, "/catalog/{id}", async (ctx, who, body) =>
		{
			string id = AdminApi.Route(ctx, "id");
			s.GameData.Current.Items.TryGetValue(id, out CatalogEntry old);
			var item = new CatalogEntry(id,
				Text(body, "name", old?.Name), Text(body, "type", old?.Type ?? ""), Text(body, "kind", old?.Kind),
				Api.Int(body, "rarity", old?.Rarity ?? 0), Api.Int(body, "minRank", old?.MinRank ?? 0), Text(body, "currency", old?.Currency ?? Currency.Presas),
				body["prices"] is JsonArray prices ? prices.Select(p => p.GetValue<int>()).ToArray() : old?.Prices ?? Array.Empty<int>(),
				AdminApi.Flag(body, "starter", old?.Starter ?? false), AdminApi.Flag(body, "enabled", old?.Enabled ?? true),
				AdminApi.Flag(body, "hot", old?.Hot ?? false), Api.Int(body, "team", old?.Team ?? 0), Text(body, "image", old?.Image ?? ""),
				Api.Int(body, "sort", old?.Sort ?? 9999)) { Consumable = AdminApi.Flag(body, "consumable", old?.Consumable ?? false) };
			await s.Content.SaveItem(who, item);
			return ToJson(item, 0);
		});

		app.Put(s, "/maps/{id}", async (ctx, who, body) =>
		{
			string id = AdminApi.Route(ctx, "id");
			GameMap old = s.GameData.Current.Maps.FirstOrDefault(m => m.Id == id);
			var map = new GameMap(id, Text(body, "name", old?.Name), Text(body, "size", old?.Size ?? "Médio"), Text(body, "description", old?.Description ?? ""),
				body["modes"] is JsonArray modes ? modes.Select(m => m.GetValue<string>()).ToArray() : old?.Modes ?? Array.Empty<string>(),
				AdminApi.Flag(body, "enabled", old?.Enabled ?? true), Text(body, "image", old?.Image ?? ""), Api.Int(body, "sort", old?.Sort ?? 9999));
			await s.Content.SaveMap(who, map);
			return ToJson(map);
		});

		app.Post(s, "/promotions", async (ctx, who, body) =>
		{
			var promotion = new Promotion(0, Api.Text(body, "itemId"), Api.Text(body, "title"), Api.Int(body, "percent"),
				AdminApi.When(body, "startsAt"), AdminApi.When(body, "endsAt"), true);
			return ToJson(await s.Content.AddPromotion(who, promotion), DateTime.UtcNow);
		});

		app.Delete(s, "/promotions/{id}", async (ctx, who) =>
		{
			await s.Content.RemovePromotion(who, AdminApi.RouteId(ctx));
			return null;
		});

		app.Put(s, "/challenges/{id}", async (ctx, who, body) =>
		{
			string id = AdminApi.Route(ctx, "id");
			Challenge old = s.GameData.Current.Challenges.FirstOrDefault(c => c.Id == id);
			var challenge = new Challenge(id, Text(body, "period", old?.Period), Text(body, "text", old?.Text), Text(body, "metric", old?.Metric),
				Api.Int(body, "target", old?.Target ?? 0), Api.Int(body, "presas", old?.Presas ?? 0), Api.Int(body, "xp", old?.Xp ?? 0),
				Api.Int(body, "sangue", old?.Sangue ?? 0), body.ContainsKey("itemId") ? body["itemId"]?.GetValue<string>() : old?.ItemId,
				Api.Int(body, "itemDays", old?.ItemDays ?? 0), body.ContainsKey("title") ? body["title"]?.GetValue<string>() : old?.Title,
				AdminApi.Flag(body, "active", old?.Active ?? true),
				body.ContainsKey("startsAt") ? AdminApi.WhenOrNull(body, "startsAt") : old?.StartsAt,
				body.ContainsKey("endsAt") ? AdminApi.WhenOrNull(body, "endsAt") : old?.EndsAt, Api.Int(body, "sort", old?.Sort ?? 9999));
			await s.Content.SaveChallenge(who, challenge);
			return null;
		});

		app.Delete(s, "/challenges/{id}", async (ctx, who) =>
		{
			await s.Content.RemoveChallenge(who, AdminApi.Route(ctx, "id"));
			return null;
		});

		app.Put(s, "/settings/{key}", async (ctx, who, body) =>
		{
			await s.Content.SaveSetting(who, AdminApi.Route(ctx, "key"), body);
			return null;
		});
	}

	private static string Text(JsonObject body, string field, string fallback) => body[field]?.GetValue<string>() ?? fallback;

	private static JsonObject ToJson(CatalogEntry i, int discount) => new()
	{
		["id"] = i.Id, ["name"] = i.Name, ["type"] = i.Type, ["kind"] = i.Kind, ["rarity"] = i.Rarity, ["minRank"] = i.MinRank,
		["currency"] = i.Currency, ["prices"] = new JsonArray(i.Prices.Select(p => (JsonNode)p).ToArray()), ["starter"] = i.Starter,
		["enabled"] = i.Enabled, ["hot"] = i.Hot, ["consumable"] = i.Consumable, ["team"] = i.Team, ["image"] = i.Image, ["sort"] = i.Sort,
		["forSale"] = i.ForSale, ["discount"] = discount
	};

	private static JsonObject ToJson(GameMap m) => new()
	{
		["id"] = m.Id, ["name"] = m.Name, ["size"] = m.Size, ["description"] = m.Description,
		["modes"] = new JsonArray(m.Modes.Select(x => (JsonNode)x).ToArray()), ["enabled"] = m.Enabled, ["image"] = m.Image, ["sort"] = m.Sort
	};

	private static JsonObject ToJson(Promotion p, DateTime now) => new()
	{
		["id"] = p.Id, ["itemId"] = p.ItemId, ["title"] = p.Title, ["percent"] = p.Percent, ["startsAt"] = Api.Date(p.StartsAt),
		["endsAt"] = Api.Date(p.EndsAt), ["running"] = p.RunningAt(now), ["ended"] = p.EndsAt <= now
	};
}
