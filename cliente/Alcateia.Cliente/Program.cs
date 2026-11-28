using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;
using Alcateia.Pacote;
using Avalonia;

namespace Alcateia.Cliente;

/// <summary>
/// A entrada do cliente.
///
///   WolfLeagueLauncher                  abre a janela
///   WolfLeagueLauncher --minimizado     abre na bandeja (é como sobe com o Windows)
///   WolfLeagueLauncher --desinstalar-jogo   apaga os jogos instalados e sai, sem janela
///                                       (o desinstalador do cliente chama antes de se apagar)
///
/// Um cliente por vez: aberto de novo, o segundo só pede ao primeiro que
/// mostre a janela e sai (duas cópias baixariam por cima uma da outra).
/// </summary>
public static class Program
{
	private const string PipeName = "wolfleague-launcher";

	/// <summary>Abriu com <c>--minimizado</c>: a janela nasce escondida.</summary>
	public static bool StartHidden { get; private set; }

	/// <summary>Outro cliente foi aberto: mostre a janela.</summary>
	public static event Action ShowRequested;

	[STAThread]
	public static int Main(string[] args)
	{
		if (args.Contains("--desinstalar-jogo"))
		{
			// Todos os jogos da biblioteca, cada um na sua pasta.
			Settings settings = Settings.Load();
			foreach (GameEntry game in Games.All) Installer.Uninstall(settings.GameDir(game));
			return 0;
		}

		// Quem acabou de se atualizar e reabriu espera o anterior soltar a trava.
		FileStream single = TakeLock(args.Contains("--reiniciado") ? 20 : 1);
		if (single == null)
		{
			AskToShow();
			return 0;
		}

		StartHidden = args.Contains("--minimizado");
		using (single)
		{
			_ = Task.Run(Listen);
			return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
		}
	}

	public static AppBuilder BuildAvaloniaApp() =>
		AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

	private static FileStream TakeLock(int attempts)
	{
		string path = Path.Combine(AppInfo.DataDir, "aberto.lock");
		for (int i = 0; i < attempts; i++)
		{
			try
			{
				return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				if (i + 1 < attempts) Thread.Sleep(250);
			}
		}
		return null;
	}

	private static void AskToShow()
	{
		try
		{
			using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
			pipe.Connect(1500);
			pipe.WriteByte(1);
		}
		catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException)
		{
		}
	}

	private static async Task Listen()
	{
		while (true)
		{
			try
			{
				using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				await pipe.WaitForConnectionAsync();
				if (pipe.ReadByte() == 1) ShowRequested?.Invoke();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or ObjectDisposedException)
			{
				await Task.Delay(2000);
			}
		}
	}
}
