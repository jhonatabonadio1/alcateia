using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Dominio;
using Alcateia.Infra.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Alcateia.Servidor.Http;

/// <summary>O que toda rota usa: ler o corpo, responder em JSON, quem é
/// o jogador, a chave da frota e o formato de erro.</summary>
public static class Api
{
	/// <summary>O código HTTP de cada tipo de recusa do domínio.</summary>
	public static int Status(FailureKind kind) => kind switch
	{
		FailureKind.Invalid => 400,
		FailureKind.Unauthorized => 401,
		FailureKind.Forbidden => 403,
		FailureKind.NotFound => 404,
		FailureKind.Conflict => 409,
		FailureKind.TooLarge => 413,
		FailureKind.TooMany => 429,
		FailureKind.Unavailable => 503,
		_ => 500
	};

	/// <summary>Todo erro sai como JSON: <c>{ "error": { "code", "message" } }</c>.</summary>
	public static void UseJsonErrors(this WebApplication app, ILogger log) => app.Use(async (ctx, next) =>
	{
		try
		{
			await next();
		}
		catch (Failure e) when (!ctx.Response.HasStarted)
		{
			await Fail(ctx, Status(e.Kind), e.Code, e.Message);
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

	public static async Task<JsonObject> Body(HttpContext ctx)
	{
		using var reader = new StreamReader(ctx.Request.Body);
		string text = await reader.ReadToEndAsync();
		return JsonNode.Parse(text) as JsonObject ?? throw new Failure(FailureKind.Invalid, "pedido_invalido", "Pedido inválido.");
	}

	public static string Text(JsonObject body, string field) => body[field]?.GetValue<string>() ?? "";

	public static int Int(JsonObject body, string field, int fallback = 0) => body[field]?.GetValue<int>() ?? fallback;

	/// <summary>O token do jogador (<c>Authorization: Bearer ...</c>).</summary>
	public static string Token(HttpContext ctx)
	{
		string header = ctx.Request.Headers.Authorization;
		return header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
	}

	/// <summary>Só servidor de partida passa: quem tem a chave da frota (<c>X-Fleet-Key</c>).</summary>
	public static void RequireGameServer(HttpContext ctx, Config config)
	{
		if (Passwords.SameSecret(ctx.Request.Headers["X-Fleet-Key"], config.FleetKey)) return;
		OnBadFleetKey?.Invoke(ctx);
		throw new Failure(FailureKind.Unauthorized, "chave_invalida", "Chave da frota inválida.");
	}

	/// <summary>Alguém apresentou a chave da frota errada (vai para o registro de segurança).</summary>
	public static Action<HttpContext> OnBadFleetKey;

	/// <summary>Uma data como vai para o cliente (ISO 8601, UTC).</summary>
	public static JsonNode Date(DateTime? utc) => utc == null ? null : JsonValue.Create(utc.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));

	public static Task Send(HttpContext ctx, JsonNode body)
	{
		ctx.Response.ContentType = "application/json; charset=utf-8";
		return ctx.Response.WriteAsync(body.ToJsonString());
	}

	public static Task Fail(HttpContext ctx, int status, string code, string message)
	{
		ctx.Response.StatusCode = status;
		return Send(ctx, new JsonObject { ["error"] = new JsonObject { ["code"] = code, ["message"] = message } });
	}
}
