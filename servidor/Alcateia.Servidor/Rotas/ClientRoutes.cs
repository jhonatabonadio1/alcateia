using System.Text.Json.Nodes;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>
/// O que o cliente de instalação precisa saber antes de qualquer conta: de
/// onde baixar o jogo. Os arquivos não passam por este servidor (ele pagaria
/// a banda de cada download): ficam num lugar público só de arquivos, e
/// aqui se diz qual (<see cref="Config.Downloads"/>).
///
/// Lá, o manifesto de cada produto e plataforma tem nome fixo
/// (<c>wolfleague-windows.manifest.json</c>, <c>launcher-mac-arm64.manifest.json</c>…)
/// e é sempre o da última versão; o formato está em cliente/LEIA-ME.md.
/// </summary>
public static class ClientRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapGet("/v1/client", async ctx => await Api.Send(ctx, new JsonObject
		{
			["downloads"] = s.Config.Downloads,
			["server"] = App.Version
		}));
	}
}
