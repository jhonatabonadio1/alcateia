using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Alcateia.Pacote.Ferramenta;

/// <summary>
/// O pacote pela linha de comando.
///
///   pacote empacotar --origem PASTA --saida PASTA --versao 0.5.0 --plataforma windows
///                    [--produto wolfleague] [--exe WolfLeague.exe] [--base URL/] [--notas arquivo.md]
///   pacote instalar  --manifesto ARQUIVO_OU_URL --destino PASTA [--reparar]
///   pacote desinstalar --destino PASTA
///   pacote info      --manifesto ARQUIVO_OU_URL
///
/// "empacotar" é o que o pipeline roda depois de exportar o jogo
/// (.github/workflows/lancamento.yml); "instalar" faz o que o cliente faz,
/// sem a interface — serve para conferir um pacote antes de publicar.
/// </summary>
public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		if (args.Length == 0) return Usage();
		Dictionary<string, string> o = Options(args.Skip(1).ToArray());
		try
		{
			switch (args[0])
			{
				case "empacotar": return Pack(o);
				case "instalar": return await Install(o);
				case "desinstalar":
					Console.WriteLine(Installer.Uninstall(Need(o, "destino")) ? "desinstalado" : "não havia instalação nessa pasta");
					return 0;
				case "info": return await Info(o);
				default: return Usage();
			}
		}
		catch (Exception e) when (e is InstallException or ManifestException or InvalidOperationException or IOException or HttpRequestException or ArgumentException)
		{
			Console.Error.WriteLine("erro: " + e.Message);
			return 1;
		}
	}

	private static int Usage()
	{
		Console.Error.WriteLine("uso: pacote empacotar|instalar|desinstalar|info --opção valor ... (veja cliente/LEIA-ME.md)");
		return 2;
	}

	private static Dictionary<string, string> Options(string[] args)
	{
		var options = new Dictionary<string, string>();
		for (int i = 0; i < args.Length; i++)
		{
			if (!args[i].StartsWith("--")) throw new ArgumentException($"não entendi '{args[i]}'");
			string key = args[i][2..];
			bool flag = i + 1 >= args.Length || args[i + 1].StartsWith("--");
			options[key] = flag ? "sim" : args[++i];
		}
		return options;
	}

	private static string Need(Dictionary<string, string> o, string key) =>
		o.TryGetValue(key, out string value) ? value : throw new ArgumentException($"falta --{key}");

	private static int Pack(Dictionary<string, string> o)
	{
		var options = new PackOptions
		{
			Product = o.GetValueOrDefault("produto", "wolfleague"),
			Platform = Need(o, "plataforma"),
			Version = Need(o, "versao"),
			Exe = o.GetValueOrDefault("exe", ""),
			Base = o.GetValueOrDefault("base", ""),
			Notes = o.TryGetValue("notas", out string notes) ? File.ReadAllText(notes).Trim() : ""
		};
		if (options.Base != "" && !options.Base.EndsWith('/')) options.Base += "/";
		string outDir = Need(o, "saida");

		var clock = Stopwatch.StartNew();
		Manifest manifest = Packer.Pack(Need(o, "origem"), outDir, options, o.ContainsKey("detalhes") ? Console.WriteLine : null);
		string name = Packer.ManifestName(options.Product, options.Platform);
		File.WriteAllText(Path.Combine(outDir, name), manifest.ToJson());

		Console.WriteLine($"{manifest.Files.Count} arquivos, {Sizes.Text(manifest.InstallSize)} instalados -> " +
			$"{manifest.Chunks.Count} pedaços, {Sizes.Text(manifest.DownloadSize)} em {manifest.Blobs.Count} pacote(s), {clock.Elapsed.TotalSeconds:0.0} s");
		foreach (BlobInfo blob in manifest.Blobs) Console.WriteLine($"  {blob.Name}  {Sizes.Text(blob.Size)}");
		Console.WriteLine($"  {name}");
		return 0;
	}

	private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

	private static async Task<(Manifest Manifest, string Location)> Load(Dictionary<string, string> o)
	{
		string location = Need(o, "manifesto");
		string json = location.StartsWith("http", StringComparison.OrdinalIgnoreCase)
			? await Http.GetStringAsync(location)
			: await File.ReadAllTextAsync(location);
		return (Manifest.Parse(json), location);
	}

	private static async Task<int> Install(Dictionary<string, string> o)
	{
		(Manifest manifest, string location) = await Load(o);
		string root = Need(o, "destino");
		// --pacotes: de onde baixar, no lugar da base do manifesto (conferir no disco um pacote ainda não publicado).
		IBlobSource source = BlobSources.For(o.GetValueOrDefault("pacotes", manifest.Base), Http, location);
		var installer = new Installer(root, source);

		UpdateEstimate estimate = installer.Estimate(manifest);
		Console.WriteLine($"{manifest.Product} {manifest.Version} ({manifest.Platform}): {estimate.Files} arquivo(s) a montar, até {Sizes.Text(estimate.DownloadBytes)} de download");

		long downloaded = 0, total = 0;
		InstallPhase phase = InstallPhase.Checking;
		var progress = new SyncProgress(p =>
		{
			if (p.Phase != phase && p.Phase == InstallPhase.Downloading) Console.WriteLine($"  faltam {Sizes.Text(p.DownloadTotal)} de download, {Sizes.Text(p.Total)} a escrever");
			phase = p.Phase;
			if (p.Phase == InstallPhase.Downloading) (downloaded, total) = (p.Downloaded, p.DownloadTotal);
		});

		var clock = Stopwatch.StartNew();
		await installer.Apply(manifest, o.ContainsKey("reparar"), progress, CancellationToken.None);
		Console.WriteLine($"pronto em {clock.Elapsed.TotalSeconds:0.0} s: baixou {Sizes.Text(downloaded)} ({downloaded:N0} bytes) de {Sizes.Text(total)}");
		return 0;
	}

	private static async Task<int> Info(Dictionary<string, string> o)
	{
		(Manifest m, _) = await Load(o);
		Console.WriteLine($"{m.Product} {m.Version} ({m.Platform}), formato {m.Format}");
		Console.WriteLine($"  executável: {m.Exe}");
		Console.WriteLine($"  base: {m.Base}");
		Console.WriteLine($"  {m.Files.Count} arquivos, {Sizes.Text(m.InstallSize)} instalados; {m.Chunks.Count} pedaços, {Sizes.Text(m.DownloadSize)} de download em {m.Blobs.Count} pacote(s)");
		foreach (FileEntry f in m.Files.OrderByDescending(f => f.Size).Take(8)) Console.WriteLine($"  {Sizes.Text(f.Size),8}  {f.Path}");
		return 0;
	}

	/// <summary>Progresso entregue na hora, no fio de quem avisa (o
	/// <see cref="Progress{T}"/> do .NET entrega depois, fora de ordem).</summary>
	private sealed class SyncProgress : IProgress<InstallProgress>
	{
		private readonly Action<InstallProgress> _on;
		public SyncProgress(Action<InstallProgress> on) => _on = on;
		public void Report(InstallProgress value) => _on(value);
	}
}
