using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>
/// O que do jogo mora no servidor: os dados (catálogo, mapas, promoções,
/// desafios, configurações), o inventário, a loja e os desafios.
///
/// | Rota | Quem | O quê |
/// |---|---|---|
/// | GET /v1/gamedata | qualquer um | tudo de uma vez; com <c>If-None-Match</c>, 304 se nada mudou |
/// | GET /v1/inventory | jogador | o que ele tem, a carteira e a patente |
/// | POST /v1/shop/buy | jogador | <c>{ itemId, duration }</c> — o servidor confere preço, patente e saldo |
/// | GET /v1/challenges | jogador | os desafios valendo e o progresso dele |
/// | POST /v1/challenges/{id}/claim | jogador | recebe o prêmio de um desafio completo |
/// </summary>
public static class GameRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapGet("/v1/gamedata", async ctx =>
		{
			GameData data = s.GameData.Current;
			ctx.Response.Headers.ETag = data.ETag;
			ctx.Response.Headers.CacheControl = "no-cache";
			if (ctx.Request.Headers.IfNoneMatch == data.ETag)
			{
				ctx.Response.StatusCode = 304;
				return;
			}
			ctx.Response.ContentType = "application/json; charset=utf-8";
			await ctx.Response.WriteAsync(data.Json);
		});

		app.MapGet("/v1/inventory", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			Profile profile = await s.Accounts.Profile(account.Id);
			var items = new JsonArray();
			foreach (OwnedItem item in await s.Shop.Inventory(account.Id)) items.Add(ToJson(item));
			await Api.Send(ctx, new JsonObject
			{
				["presas"] = profile.Presas, ["sangue"] = profile.Sangue, ["xp"] = profile.Xp, ["rank"] = profile.Rank, ["items"] = items
			});
		});

		app.MapPost("/v1/shop/buy", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			JsonObject body = await Api.Body(ctx);
			PurchaseReceipt receipt = await s.Shop.Buy(account.Id, Api.Text(body, "itemId"), Api.Int(body, "duration", -1));
			await Api.Send(ctx, new JsonObject
			{
				["itemId"] = receipt.ItemId, ["currency"] = receipt.Currency, ["price"] = receipt.Price, ["balance"] = receipt.Balance,
				["expiresAt"] = Api.Date(receipt.ExpiresAt)
			});
		});

		app.MapGet("/v1/challenges", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			var list = new JsonArray();
			foreach (ChallengeState state in await s.Challenges.For(account.Id))
			{
				JsonObject one = GameDataService.ChallengeJson(state.Challenge);
				one["progress"] = state.Progress;
				one["done"] = state.Done;
				one["claimed"] = state.Claimed;
				one["periodKey"] = state.PeriodKey;
				list.Add(one);
			}
			await Api.Send(ctx, new JsonObject { ["challenges"] = list });
		});

		app.MapPost("/v1/challenges/{id}/claim", async ctx =>
		{
			Account account = await s.Accounts.Require(Api.Token(ctx));
			Challenge claimed = await s.Challenges.Claim(account.Id, (string)ctx.Request.RouteValues["id"]);
			Profile profile = await s.Accounts.Profile(account.Id);
			await Api.Send(ctx, new JsonObject
			{
				["id"] = claimed.Id, ["presas"] = claimed.Presas, ["xp"] = claimed.Xp, ["sangue"] = claimed.Sangue, ["itemId"] = claimed.ItemId,
				["balance"] = new JsonObject { ["presas"] = profile.Presas, ["sangue"] = profile.Sangue, ["xp"] = profile.Xp, ["rank"] = profile.Rank }
			});
		});
	}

	public static JsonObject ToJson(OwnedItem item) => new()
	{
		["id"] = item.ItemId, ["acquiredAt"] = Api.Date(item.AcquiredAt), ["expiresAt"] = Api.Date(item.ExpiresAt),
		["source"] = item.Source, ["uses"] = item.Uses
	};
}
