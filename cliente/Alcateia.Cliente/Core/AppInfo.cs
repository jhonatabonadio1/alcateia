using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Alcateia.Cliente.Core;

/// <summary>
/// O que o cliente sabe de si: a versão, com que servidor fala, em que
/// sistema está e onde guarda as coisas.
///
/// O servidor vem de quando o cliente foi montado (<c>-p:ServidorUrl=</c>, no
/// pipeline); a variável de ambiente <c>ALCATEIA_API</c> passa por cima — é a
/// mesma que o jogo obedece, e o cliente a repassa ao jogo que abre.
/// </summary>
public static class AppInfo
{
	public const string Name = "WolfLeague";

	/// <summary>O produto que este cliente instala (o nome nos manifestos).</summary>
	public const string Product = "wolfleague";

	public static readonly string Version =
		typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0-dev";

	/// <summary>O servidor escolhido na mão (variável de ambiente), ou null.</summary>
	public static readonly string ServerOverride = Clean(Environment.GetEnvironmentVariable("ALCATEIA_API"));

	public static readonly string ServerUrl = ServerOverride
		?? Clean(typeof(AppInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "ServidorUrl")?.Value)
		?? "http://127.0.0.1:8080";

	private static string Clean(string url) => string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');

	public static bool IsWindows => OperatingSystem.IsWindows();
	public static bool IsMac => OperatingSystem.IsMacOS();

	/// <summary>A plataforma do jogo, como vai no nome do manifesto dele. O
	/// jogo de Mac é um só para Intel e Apple Silicon.</summary>
	public static string GamePlatform => IsWindows ? "windows" : IsMac ? "mac" : "linux";

	/// <summary>A plataforma do próprio cliente: no Mac há um por processador.</summary>
	public static string LauncherPlatform => IsWindows ? "windows"
		: IsMac ? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "mac-arm64" : "mac-x64") : "linux";

	/// <summary>Onde o cliente guarda os ajustes e as contas salvas. Não é a
	/// pasta do jogo (o Godot usa "WolfLeague" na mesma raiz).</summary>
	public static string DataDir
	{
		get
		{
			string dir = Environment.GetEnvironmentVariable("WOLFLEAGUE_LAUNCHER_DADOS");
			if (string.IsNullOrEmpty(dir))
				dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WolfLeagueLauncher");
			Directory.CreateDirectory(dir);
			return dir;
		}
	}

	/// <summary>
	/// Onde o jogo vai, se o jogador não escolher outra pasta. No Windows é a
	/// mesma do instalador de antes (%LOCALAPPDATA%\Programs\WolfLeague):
	/// quem já tinha o jogo instalado por ele não baixa tudo de novo — o
	/// cliente aproveita o que está lá e baixa só a diferença.
	/// </summary>
	public static string DefaultGameDir => IsWindows
		? OldInstallDir() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WolfLeague")
		: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WolfLeagueLauncher", "Jogo");

	/// <summary>Onde o instalador de antes pôs o jogo, se o jogador escolheu
	/// outra pasta (ele a guarda em HKCU\Software\WolfLeague\InstallPath).</summary>
	private static string OldInstallDir()
	{
		if (!OperatingSystem.IsWindows()) return null;
		try
		{
			using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\WolfLeague");
			return key?.GetValue("InstallPath") is string dir && Directory.Exists(dir) ? dir : null;
		}
		catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
		{
			return null;
		}
	}

	/// <summary>A pasta onde o próprio cliente está instalado: no Windows, a
	/// do executável; no Mac, o pacote .app (null se não está num).</summary>
	public static string InstallRoot
	{
		get
		{
			string dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
			if (!IsMac) return dir;
			// …/WolfLeague Launcher.app/Contents/MacOS
			string contents = Path.GetDirectoryName(dir);
			string bundle = contents == null ? null : Path.GetDirectoryName(contents);
			return bundle != null && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(contents) == "Contents" ? bundle : null;
		}
	}
}
