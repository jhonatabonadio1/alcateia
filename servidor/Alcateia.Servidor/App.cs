using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Servidor.Banco;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Alcateia.Servidor;

/// <summary>
/// O servidor montado: o banco, os serviços e as rotas.
///
/// | Rota | Quem | O quê |
/// |---|---|---|
/// | GET /health | qualquer um | está de pé? o banco responde? |
/// | POST /v1/accounts | qualquer um | cria conta, já entra |
/// | POST /v1/sessions | qualquer um | entra (login) |
/// | DELETE /v1/sessions | jogador | sai |
/// | GET /v1/profile | jogador | o perfil |
/// | PUT /v1/profile/data | jogador | guarda conjuntos, personagem... |
/// | GET /v1/ranking | qualquer um | os primeiros por XP |
/// | GET /v1/rooms | qualquer um | as salas abertas |
/// | GET /v1/matches | jogador | as últimas partidas dele |
/// | GET /v1/lobby (WebSocket) | jogador | salas e chat ao vivo |
/// | POST /v1/fleet/heartbeat | servidor de partida | "estou vivo"; recebe a partida |
/// | POST /v1/matches | servidor de partida | o resultado de uma partida |
///
/// Jogador se identifica com <c>Authorization: Bearer token</c> (no
/// WebSocket, <c>?token=</c>); servidor de partida, com
/// <c>X-Fleet-Key</c>.
/// </summary>
public static class App
{
	public const string Version = "0.1.0";

	public static async Task<WebApplication> Build(Config config, string[] args = null)
	{
		var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
		builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");
		builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 128 * 1024);
		builder.Logging.ClearProviders();
		builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
		builder.Logging.SetMinimumLevel(LogLevel.Warning);
		builder.Logging.AddFilter("Alcateia", LogLevel.Information);

		var db = new PgPool(config.Database);
		var accounts = new Accounts(db, config);
		var fleet = new Fleet(config);
		var lobby = new Lobby(fleet, accounts, config);
		var matches = new Matches(db, fleet, lobby);
		var logins = new RateLimit(config.LoginsPerMinute);

		builder.Services.AddSingleton(db);
		WebApplication app = builder.Build();
		ILogger log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Alcateia.Servidor");

		int from = await WaitForDatabase(db, log);
		log.LogInformation("banco na versão {To} (estava na {From})", Schema.Latest, from);
		if (config.FleetKey == Config.DevFleetKey)
			log.LogWarning("ALCATEIA_FLEET_KEY está com o valor de desenvolvimento: troque antes de abrir para a internet");

		app.Lifetime.ApplicationStopped.Register(() => db.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3)));
		app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

		// Todo erro sai como JSON: { "error": { "code", "message" } }.
		app.Use(async (ctx, next) =>
		{
			try
			{
				await next();
			}
			catch (ApiException e) when (!ctx.Response.HasStarted)
			{
				await Fail(ctx, e.Status, e.Code, e.Message);
			}
			catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException && !ctx.Response.HasStarted)
			{
				await Fail(ctx, 400, "pedido_invalido", "Pedido inválido.");
			}
			catch (Exception e) when (!ctx.Response.HasStarted)
			{
				log.LogError(e, "erro em {Path}", ctx.Request.Path);
				await Fail(ctx, 500, "erro_interno", "Erro no servidor.");
			}
		});

		app.MapGet("/health", async ctx =>
		{
			bool dbOk = true;
			try { await db.Query("select 1"); }
			catch (Exception) { dbOk = false; }

			ctx.Response.StatusCode = dbOk ? 200 : 503;
			await Send(ctx, new JsonObject
			{
				["status"] = dbOk ? "ok" : "sem_banco", ["version"] = Version, ["schema"] = Schema.Latest,
				["online"] = lobby.Online, ["fleet"] = fleet.Status()
			});
		});

		app.MapPost("/v1/accounts", async ctx =>
		{
			Throttle(ctx, logins);
			JsonObject body = await Body(ctx);
			var (account, token) = await accounts.Register(Text(body, "name"), Text(body, "password"));
			ctx.Response.StatusCode = 201;
			await Send(ctx, new JsonObject { ["token"] = token, ["profile"] = await accounts.Profile(account.Id) });
		});

		app.MapPost("/v1/sessions", async ctx =>
		{
			Throttle(ctx, logins);
			JsonObject body = await Body(ctx);
			var (account, token) = await accounts.Login(Text(body, "name"), Text(body, "password"));
			await Send(ctx, new JsonObject { ["token"] = token, ["profile"] = await accounts.Profile(account.Id) });
		});

		app.MapDelete("/v1/sessions", async ctx =>
		{
			await Player(ctx, accounts);
			await accounts.Logout(Token(ctx));
			ctx.Response.StatusCode = 204;
		});

		app.MapGet("/v1/profile", async ctx => await Send(ctx, await accounts.Profile((await Player(ctx, accounts)).Id)));

		app.MapPut("/v1/profile/data", async ctx =>
		{
			Account account = await Player(ctx, accounts);
			await accounts.SaveData(account.Id, (await Body(ctx))["data"]);
			ctx.Response.StatusCode = 204;
		});

		app.MapGet("/v1/ranking", async ctx =>
		{
			int.TryParse(ctx.Request.Query["limit"], out int limit);
			await Send(ctx, new JsonObject { ["ranking"] = await accounts.Ranking(limit == 0 ? 50 : limit) });
		});

		app.MapGet("/v1/rooms", async ctx => await Send(ctx, new JsonObject { ["rooms"] = lobby.Rooms() }));

		app.MapGet("/v1/matches", async ctx =>
			await Send(ctx, new JsonObject { ["matches"] = await matches.Recent((await Player(ctx, accounts)).Id, 20) }));

		app.Map("/v1/lobby", async ctx =>
		{
			if (!ctx.WebSockets.IsWebSocketRequest) throw new ApiException(400, "so_websocket", "Esta rota é WebSocket.");
			Account account = await accounts.FromToken(ctx.Request.Query["token"]) ?? await accounts.FromToken(Token(ctx))
				?? throw new ApiException(401, "sem_login", "Entre na conta primeiro.");

			using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
			await lobby.Run(account, socket);
		});

		app.MapPost("/v1/fleet/heartbeat", async ctx =>
		{
			GameServer(ctx, config);
			JsonObject body = await Body(ctx);
			string address = Text(body, "address");
			if (string.IsNullOrEmpty(address)) address = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

			Assignment assignment = fleet.Heartbeat(Text(body, "id"), address, body["port"]?.GetValue<int>() ?? 0, body["players"]?.GetValue<int>() ?? 0);
			var reply = new JsonObject { ["ok"] = true };
			if (assignment != null)
			{
				var players = new JsonArray();
				foreach (MatchSeat p in assignment.Players)
					players.Add(new JsonObject { ["accountId"] = p.AccountId, ["name"] = p.Name, ["team"] = p.Team });
				reply["assignment"] = new JsonObject
				{
					["matchKey"] = assignment.MatchKey, ["settings"] = assignment.Settings.DeepClone(), ["players"] = players
				};
			}
			await Send(ctx, reply);
		});

		app.MapPost("/v1/matches", async ctx =>
		{
			GameServer(ctx, config);
			await Send(ctx, await matches.Report(await Body(ctx)));
		});

		return app;
	}

	/// <summary>Espera o banco subir (no Docker ele pode demorar uns
	/// segundos a mais que o servidor) e aplica as migrações.</summary>
	private static async Task<int> WaitForDatabase(PgPool db, ILogger log)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				return await Schema.Migrate(db);
			}
			catch (Exception e) when (attempt < 30 && e is System.Net.Sockets.SocketException or IOException or PgException { Code: "57P03" })
			{
				log.LogInformation("esperando o banco ({Attempt}/30): {Message}", attempt, e.Message);
				await Task.Delay(1000);
			}
		}
	}

	private static void Throttle(HttpContext ctx, RateLimit limit)
	{
		if (!limit.Allow(ctx.Connection.RemoteIpAddress?.ToString() ?? "?"))
			throw new ApiException(429, "devagar", "Muitas tentativas. Espere um minuto.");
	}

	private static string Token(HttpContext ctx)
	{
		string header = ctx.Request.Headers.Authorization;
		return header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
	}

	private static async Task<Account> Player(HttpContext ctx, Accounts accounts) =>
		await accounts.FromToken(Token(ctx)) ?? throw new ApiException(401, "sem_login", "Entre na conta primeiro.");

	private static void GameServer(HttpContext ctx, Config config)
	{
		if (!Passwords.SameSecret(ctx.Request.Headers["X-Fleet-Key"], config.FleetKey))
			throw new ApiException(401, "chave_invalida", "Chave da frota inválida.");
	}

	private static async Task<JsonObject> Body(HttpContext ctx)
	{
		using var reader = new StreamReader(ctx.Request.Body);
		string text = await reader.ReadToEndAsync();
		return JsonNode.Parse(text) as JsonObject ?? throw new ApiException(400, "pedido_invalido", "Pedido inválido.");
	}

	private static string Text(JsonObject body, string field) => body[field]?.GetValue<string>() ?? "";

	private static Task Send(HttpContext ctx, JsonNode body)
	{
		ctx.Response.ContentType = "application/json; charset=utf-8";
		return ctx.Response.WriteAsync(body.ToJsonString());
	}

	private static Task Fail(HttpContext ctx, int status, string code, string message)
	{
		ctx.Response.StatusCode = status;
		return Send(ctx, new JsonObject { ["error"] = new JsonObject { ["code"] = code, ["message"] = message } });
	}
}
