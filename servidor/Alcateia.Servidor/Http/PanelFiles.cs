using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Alcateia.Servidor.Http;

/// <summary>
/// Entrega o painel (o React já compilado) na porta do painel. Qualquer
/// caminho que não seja arquivo cai no <c>index.html</c>: as telas são
/// rotas do próprio painel. A política de conteúdo só deixa carregar o
/// que vem deste servidor — script injetado de fora não roda.
/// </summary>
public static class PanelFiles
{
	private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
	{
		[".html"] = "text/html; charset=utf-8", [".js"] = "text/javascript; charset=utf-8", [".css"] = "text/css; charset=utf-8",
		[".json"] = "application/json; charset=utf-8", [".svg"] = "image/svg+xml", [".png"] = "image/png", [".jpg"] = "image/jpeg",
		[".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".ico"] = "image/x-icon", [".woff2"] = "font/woff2", [".txt"] = "text/plain; charset=utf-8"
	};

	private const string Policy =
		"default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; " +
		"font-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

	public static void UsePanelFiles(this WebApplication app, Config config) => app.Use(async (ctx, next) =>
	{
		string path = ctx.Request.Path.Value ?? "/";
		bool api = path.StartsWith("/admin/", StringComparison.Ordinal) || path == "/metrics" || path == "/health";
		if (!Shield.OnAdminPort(ctx) || api || !HttpMethods.IsGet(ctx.Request.Method))
		{
			await next();
			return;
		}
		await Serve(ctx, config.AdminUi, path);
	});

	private static async Task Serve(HttpContext ctx, string root, string path)
	{
		root = Path.GetFullPath(root);
		string file = Path.GetFullPath(Path.Combine(root, path.TrimStart('/')));
		bool inside = file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
		bool asset = inside && File.Exists(file);

		// Arquivo com extensão que não existe é 404; o resto é tela do painel.
		if (!asset)
		{
			if (Path.HasExtension(path))
			{
				await Api.Fail(ctx, 404, "nao_encontrado", "Não encontrado.");
				return;
			}
			file = Path.Combine(root, "index.html");
			if (!File.Exists(file))
			{
				ctx.Response.ContentType = "text/plain; charset=utf-8";
				await ctx.Response.WriteAsync("O painel ainda não foi compilado. Rode: cd servidor/painel && npm install && npm run build");
				return;
			}
		}

		string extension = Path.GetExtension(file);
		ctx.Response.ContentType = Types.TryGetValue(extension, out string type) ? type : "application/octet-stream";
		ctx.Response.Headers["Content-Security-Policy"] = Policy;
		// Os arquivos do Vite têm o hash no nome: podem ficar no navegador para sempre.
		ctx.Response.Headers["Cache-Control"] = path.StartsWith("/assets/", StringComparison.Ordinal) ? "public, max-age=31536000, immutable"
			: extension == ".html" ? "no-cache" : "public, max-age=3600";
		await ctx.Response.SendFileAsync(file);
	}
}
