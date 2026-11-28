using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Alcateia.Pacote;

namespace Alcateia.Cliente.Core;

/// <summary>
/// O cliente se atualiza sozinho, com o mesmo pacote do jogo: o manifesto
/// dele (<c>launcher-windows.manifest.json</c>, <c>launcher-mac-arm64…</c>)
/// fica no mesmo lugar dos do jogo. Havendo versão mais nova, baixa em
/// segundo plano por cima da própria instalação e avisa: a nova vale ao
/// reiniciar (<see cref="Restart"/>) ou na próxima vez que abrir.
///
/// No Windows um programa aberto não se sobrescreve, mas troca de nome: o
/// executável antigo sai da frente (<c>.antigo-…</c>), o novo entra no lugar,
/// e a sobra é apagada na próxima atualização (<see cref="Installer"/>).
///
/// Não se atualiza: versão de desenvolvimento (0.0.0-dev), cliente rodando
/// fora de uma instalação (no Mac, fora de um .app) ou em pasta sem
/// permissão de escrita — aí o jogador baixa o instalador novo.
/// </summary>
public sealed class SelfUpdate
{
	public const string Product = "launcher";

	private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
	private bool _busy;

	/// <summary>A versão nova já está no disco: falta reiniciar. (Vazio: não há.)</summary>
	public string Ready { get; private set; } = "";

	public event Action Changed;

	public async Task Check(string downloads)
	{
		if (_busy || Ready != "" || string.IsNullOrEmpty(downloads) || AppInfo.Version.Contains("dev")) return;
		string root = AppInfo.InstallRoot;
		if (root == null || !Writable(root)) return;

		_busy = true;
		SynchronizationContext ui = SynchronizationContext.Current;
		try
		{
			string name = Packer.ManifestName(Product, AppInfo.LauncherPlatform);
			bool http = downloads.StartsWith("http", StringComparison.OrdinalIgnoreCase);
			string location = http ? (downloads.EndsWith('/') ? downloads : downloads + "/") + name : Path.Combine(downloads, name);

			string json;
			if (http)
			{
				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
				using HttpResponseMessage response = await Http.GetAsync(location, timeout.Token);
				if (!response.IsSuccessStatusCode) return;
				json = await response.Content.ReadAsStringAsync(timeout.Token);
			}
			else
			{
				if (!File.Exists(location)) return;
				json = await File.ReadAllTextAsync(location);
			}

			Manifest latest = Manifest.Parse(json);
			if (!Versions.IsNewer(latest.Version, AppInfo.Version)) return;

			// O estado fica na pasta de dados, não ao lado do programa: no Mac um
			// arquivo a mais dentro do .app quebra o lacre da assinatura dele.
			var installer = new Installer(root, BlobSources.For(latest.Base, Http, location), Path.Combine(AppInfo.DataDir, "atualizacao"));
			await Task.Run(() => installer.Apply(latest, false, null, CancellationToken.None));
			Ready = latest.Version;
			if (ui != null) ui.Post(_ => Changed?.Invoke(), null);
			else Changed?.Invoke();
		}
		catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or UnauthorizedAccessException
			or ManifestException or InstallException)
		{
			// Não deu agora: tenta de novo na próxima conferência. O cliente segue na versão que está.
		}
		finally
		{
			_busy = false;
		}
	}

	/// <summary>Abre o cliente de novo (já a versão nova) e fecha este.</summary>
	public static bool Restart()
	{
		try
		{
			string exe = Environment.ProcessPath;
			if (string.IsNullOrEmpty(exe)) return false;
			Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, Arguments = "--reiniciado" });
			return true;
		}
		catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
		{
			return false;
		}
	}

	private static bool Writable(string dir)
	{
		try
		{
			string probe = Path.Combine(dir, ".escrita-" + Guid.NewGuid().ToString("N")[..6]);
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}
}
