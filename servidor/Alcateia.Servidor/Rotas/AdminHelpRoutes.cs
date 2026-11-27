using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Dominio.Painel;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>
/// A central de ajuda do painel: os documentos do projeto (o guia, o
/// <c>docs/</c> e as decisões), lidos do disco a cada pedido — a ajuda é
/// sempre o que está no repositório, sem cópia que envelhece.
///
/// Vem por uma rota com sessão, e não junto com os arquivos do painel,
/// porque os arquivos do painel abrem sem login e a documentação conta
/// como o escudo e a trapaça funcionam (<c>docs/SEGURANCA.md</c>).
/// </summary>
public static class AdminHelpRoutes
{
	/// <summary>O que entra, relativo à raiz: a pasta e o padrão dos arquivos.
	/// Só arquivos achados aqui são entregues — nenhum caminho vem do pedido.</summary>
	private static readonly (string Folder, string Pattern)[] Sources =
	{
		("", "CLAUDE.md"), ("", "CONTRIBUTING.md"), ("docs", "*.md"), ("docs/decisoes", "*.md"), ("servidor", "LEIA-ME.md")
	};

	/// <summary>Um documento maior que isto é engano (um arquivo gerado), não ajuda.</summary>
	private const long MaxBytes = 2 * 1024 * 1024;

	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.Get(s, "/help", async (ctx, who) =>
		{
			who.Require(Permission.View);
			string root = s.Config.AdminDocs;
			var docs = new JsonArray();
			foreach (string file in Files(root))
			{
				var info = new FileInfo(file);
				if (info.Length > MaxBytes) continue;
				docs.Add(new JsonObject
				{
					["path"] = Path.GetRelativePath(root, file).Replace('\\', '/'),
					["updatedAt"] = Api.Date(info.LastWriteTimeUtc),
					["text"] = await File.ReadAllTextAsync(file)
				});
			}
			return new JsonObject { ["docs"] = docs };
		});
	}

	private static IEnumerable<string> Files(string root)
	{
		if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) yield break;
		foreach (var (folder, pattern) in Sources)
		{
			string dir = Path.Combine(root, folder);
			if (!Directory.Exists(dir)) continue;
			foreach (string file in Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.Ordinal))
				yield return file;
		}
	}

	/// <summary>
	/// Onde estão os documentos. Na imagem do Docker, a pasta <c>ajuda</c> ao
	/// lado do programa (o Dockerfile copia para lá). Rodando do repositório
	/// (<c>dotnet run</c>), a raiz dele: a primeira pasta, subindo, que tem
	/// <c>docs/DESIGN.md</c>.
	/// </summary>
	public static string DefaultRoot()
	{
		string beside = Path.Combine(AppContext.BaseDirectory, "ajuda");
		if (Directory.Exists(beside)) return beside;
		foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
		{
			for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
				if (File.Exists(Path.Combine(dir.FullName, "docs", "DESIGN.md")))
					return dir.FullName;
		}
		return beside;
	}
}
