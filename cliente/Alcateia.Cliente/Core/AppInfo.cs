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
	/// <summary>O nome do programa (Jhon, 27/11: "ele é iniciado da initech games, o initech
	/// launcher"). Os jogos que ele instala estão em <see cref="Games"/>.</summary>
	public const string Name = "Initech Launcher";
	public const string Studio = "Initech Games";

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

	private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

	/// <summary>A pasta de dados de antes de 27/11, quando o cliente era só do WolfLeague.</summary>
	private static string OldDataDir => Path.Combine(Roaming, "WolfLeagueLauncher");

	private static string _dataDir;

	/// <summary>A pasta de dados de teste (<c>WOLFLEAGUE_LAUNCHER_DADOS</c>, a das fotos
	/// das telas): com ela, os jogos também ficam lá dentro, longe das pastas de verdade.</summary>
	private static string TestDataDir => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WOLFLEAGUE_LAUNCHER_DADOS"))
		? null : Environment.GetEnvironmentVariable("WOLFLEAGUE_LAUNCHER_DADOS");

	/// <summary>
	/// Onde o cliente guarda os ajustes e as contas salvas (e, fora do
	/// Windows, os jogos, em <c>game/</c>). Não é a pasta de dados de um jogo
	/// (o Godot usa "WolfLeague" na mesma raiz). A de antes de 27/11
	/// (WolfLeagueLauncher) é renomeada na primeira vez: as contas lembradas e o
	/// jogo baixado vão junto. Sem conseguir (o Windows não renomeia pasta com
	/// arquivo aberto), segue usando a de antes.
	/// </summary>
	public static string DataDir
	{
		get
		{
			if (_dataDir != null) return _dataDir;
			string dir = Environment.GetEnvironmentVariable("WOLFLEAGUE_LAUNCHER_DADOS");
			if (string.IsNullOrEmpty(dir))
			{
				dir = Path.Combine(Roaming, "InitechLauncher");
				if (Directory.Exists(OldDataDir) && !File.Exists(Path.Combine(dir, "settings.json")))
				{
					try
					{
						if (!Directory.Exists(dir)) Directory.Move(OldDataDir, dir);
						else
						{
							// A nova já existe sem ajustes (criada por outra coisa): traz o
							// que há na velha para dentro dela, o que ainda não estiver lá.
							foreach (string entry in Directory.EnumerateFileSystemEntries(OldDataDir))
							{
								string to = Path.Combine(dir, Path.GetFileName(entry));
								if (File.Exists(to) || Directory.Exists(to)) continue;
								if (Directory.Exists(entry)) Directory.Move(entry, to);
								else File.Move(entry, to);
							}
							if (!Directory.EnumerateFileSystemEntries(OldDataDir).GetEnumerator().MoveNext()) Directory.Delete(OldDataDir);
						}
					}
					catch (Exception e) when (e is IOException or UnauthorizedAccessException) { dir = OldDataDir; }
				}
			}
			Directory.CreateDirectory(dir);
			return _dataDir = dir;
		}
	}

	/// <summary>
	/// A pasta onde os jogos ficam, cada um na sua (<see cref="GameEntry.Folder"/>):
	/// <c>game/WolfLeague</c> (Jhon, 27/11: "deve salvar o jogo em game"). No
	/// Windows ao lado do launcher, fora do %APPDATA% (que acompanha o perfil
	/// do Windows e não é lugar de 1 GB); no Mac e no Linux na pasta de dados.
	/// </summary>
	public static string GamesRoot => TestDataDir != null ? Path.Combine(TestDataDir, "game")
		: IsWindows
		? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Name, "game")
		: Path.Combine(Roaming, "InitechLauncher", "game");

	/// <summary>Onde um jogo vai, se o jogador não escolher outra pasta. Quem
	/// tinha o WolfLeague pelo instalador de antes (windows.nsi) não baixa tudo
	/// de novo: o launcher usa a pasta dele e baixa só a diferença.</summary>
	public static string DefaultGameDir(GameEntry game) =>
		(game.Id == Games.WolfLeague.Id ? OldInstallDir() : null) ?? Path.Combine(GamesRoot, game.Folder);

	/// <summary>As pastas padrão de antes, fora do Windows, de onde o jogo
	/// baixado é trazido para a de agora (<see cref="Settings"/>): ".../Jogo"
	/// do cliente 0.4, na pasta de dados velha ou já na renomeada. No Windows a
	/// de antes era a do instalador antigo e fica onde está.</summary>
	public static string[] OldDefaultGameDirs(GameEntry game) => IsWindows || game.Id != Games.WolfLeague.Id
		? Array.Empty<string>()
		: new[] { Path.Combine(OldDataDir, "Jogo"), Path.Combine(Roaming, "InitechLauncher", "Jogo") };

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
			// …/Initech Launcher.app/Contents/MacOS
			string contents = Path.GetDirectoryName(dir);
			string bundle = contents == null ? null : Path.GetDirectoryName(contents);
			return bundle != null && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(contents) == "Contents" ? bundle : null;
		}
	}
}
