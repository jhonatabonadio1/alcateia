using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;
using Alcateia.Pacote;

namespace Alcateia.Cliente.ViewModels;

/// <summary>
/// A página do jogo: um botão grande que é sempre a próxima coisa a fazer
/// (instalar, atualizar, jogar, continuar), a linha que diz como o jogo
/// está, a barra do download e o menu de manutenção (verificar e reparar,
/// abrir a pasta, mudar a pasta, desinstalar).
///
/// Tudo aqui é texto tirado do <see cref="GameService"/>: quem decide é ele.
/// </summary>
public sealed class GameViewModel : Observable
{
	private readonly MainViewModel _main;
	private readonly GameService _game;

	public GameViewModel(MainViewModel main, GameService game)
	{
		_main = main;
		_game = game;
		_game.Changed += OnChanged;

		Primary = new Command(RunPrimary, () => PrimaryEnabled);
		Pause = new Command(_game.Pause);
		Cancel = new Command(_game.Dismiss);
		Repair = new Command(() => _ = _game.Repair(), () => CanMaintain);
		OpenFolder = new Command(OpenDir);
		ChangeFolder = new Command(() => _ = PickDir(), () => CanChangeFolder);
		Uninstall = new Command(AskUninstall, () => CanMaintain);
		Recheck = new Command(() => _ = _game.Refresh());
	}

	private void OnChanged()
	{
		RaiseAll();
		Primary.Refresh();
		Repair.Refresh();
		ChangeFolder.Refresh();
		Uninstall.Refresh();
	}

	public Command Primary { get; }
	public Command Pause { get; }
	public Command Cancel { get; }
	public Command Repair { get; }
	public Command OpenFolder { get; }
	public Command ChangeFolder { get; }
	public Command Uninstall { get; }
	public Command Recheck { get; }

	private GameState State => _game.State;

	// ------------------------------------------------------------------ o botão

	public string PrimaryText => State switch
	{
		GameState.Checking => "CONFERINDO…",
		GameState.Unavailable => "INDISPONÍVEL",
		GameState.NotInstalled => "INSTALAR",
		GameState.UpdateAvailable => "ATUALIZAR",
		GameState.Ready => "JOGAR",
		GameState.Paused => "CONTINUAR",
		GameState.Playing => "JOGANDO",
		_ => _game.Work switch
		{
			WorkKind.Install => "INSTALANDO…",
			WorkKind.Update => "ATUALIZANDO…",
			WorkKind.Repair => "VERIFICANDO…",
			_ => "DESINSTALANDO…"
		}
	};

	public bool PrimaryEnabled => State is GameState.NotInstalled or GameState.UpdateAvailable or GameState.Ready or GameState.Paused;

	private void RunPrimary()
	{
		switch (State)
		{
			case GameState.NotInstalled:
			case GameState.UpdateAvailable:
				_ = _game.Install();
				break;
			case GameState.Paused:
				_ = _game.Resume();
				break;
			case GameState.Ready:
				if (_game.Play(_main.Account)) _main.GameOpened();
				break;
		}
	}

	// ------------------------------------------------------------------ as linhas

	public string Title => "WOLFLEAGUE";

	/// <summary>Como o jogo está, numa linha.</summary>
	public string StatusLine
	{
		get
		{
			Manifest latest = _game.Latest, installed = _game.Installed;
			switch (State)
			{
				case GameState.Checking: return "Conferindo a versão…";
				case GameState.Unavailable: return _game.Problem;
				case GameState.NotInstalled:
					return _game.FoundOldInstall
						? $"Versão {latest.Version} · já há um jogo nesta pasta: só a diferença será baixada"
						: $"Versão {latest.Version} · {Sizes.Text(latest.DownloadSize)} de download · {Sizes.Text(latest.InstallSize)} em disco";
				case GameState.UpdateAvailable:
					string size = _game.Estimate == null ? "" : $" · até {Sizes.Text(_game.Estimate.DownloadBytes)} de download";
					return latest.Version == installed.Version
						? $"Versão {installed.Version} · há arquivos a consertar{size}"
						: $"Versão {latest.Version} disponível (você tem a {installed.Version}){size}";
				case GameState.Ready:
					return latest == null ? $"Versão {installed.Version} · sem conexão para conferir se há uma nova" : $"Versão {installed.Version} · atualizado";
				case GameState.Paused: return "Pausado · o que já baixou está guardado";
				case GameState.Playing: return "O jogo está aberto";
				default: return WorkLine;
			}
		}
	}

	private string WorkLine
	{
		get
		{
			if (_game.Work == WorkKind.Uninstall) return "Apagando os arquivos do jogo…";
			InstallProgress p = _game.Progress;
			switch (p.Phase)
			{
				case InstallPhase.Checking: return "Conferindo os arquivos que já estão no disco…";
				case InstallPhase.Finishing: return "Colocando os arquivos no lugar…";
				case InstallPhase.Downloading:
					if (p.DownloadTotal <= 0) return $"Montando os arquivos · {Sizes.Text(p.Done)} de {Sizes.Text(p.Total)}";
					string speed = _game.Speed > 0 ? $" · {Sizes.Text((long)_game.Speed)}/s" : "";
					string left = _game.Speed > 1024 ? " · " + Remaining((p.DownloadTotal - p.Downloaded) / _game.Speed) : "";
					return $"{Sizes.Text(p.Downloaded)} de {Sizes.Text(p.DownloadTotal)}{speed}{left}";
				default: return "";
			}
		}
	}

	private static string Remaining(double seconds)
	{
		if (seconds < 60) return "falta menos de 1 min";
		if (seconds < 3600) return $"faltam {(int)Math.Ceiling(seconds / 60)} min";
		return $"faltam {(int)(seconds / 3600)} h {(int)(seconds % 3600 / 60)} min";
	}

	/// <summary>O que deu errado na última tentativa (a linha vermelha).</summary>
	public string Problem => State == GameState.Unavailable ? "" : _game.Problem;
	public bool HasProblem => Problem.Length > 0;

	// ------------------------------------------------------------------ a barra

	public bool ProgressVisible => State is GameState.Working or GameState.Paused;

	public double ProgressValue
	{
		get
		{
			InstallProgress p = _game.Progress;
			return p.Total <= 0 ? 0 : Math.Clamp(p.Done * 100.0 / p.Total, 0, 100);
		}
	}

	public string ProgressPercent => ProgressVisible && _game.Work != WorkKind.Uninstall ? $"{ProgressValue:0}%" : "";
	public bool CanPause => State == GameState.Working && _game.Work != WorkKind.Uninstall;
	public bool CanCancel => State == GameState.Paused;

	// ------------------------------------------------------------------ manutenção

	public bool CanMaintain => _game.Installed != null && State is GameState.Ready or GameState.UpdateAvailable;
	public bool CanChangeFolder => _game.Installed == null && State is GameState.NotInstalled or GameState.Unavailable;
	public string InstallDir => _game.GameDir;
	public string InstalledVersion => _game.Installed == null ? "Não instalado" : "Versão " + _game.Installed.Version;
	public string InstalledSize => _game.Installed == null ? "" : Sizes.Text(_game.Installed.InstallSize) + " em disco";

	private void OpenDir()
	{
		try
		{
			Directory.CreateDirectory(_game.GameDir);
			Process.Start(new ProcessStartInfo(_game.GameDir) { UseShellExecute = true });
		}
		catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
		{
			_main.Toast("Não consegui abrir a pasta.");
		}
	}

	private async Task PickDir()
	{
		if (_main.PickFolder == null) return;
		string dir = await _main.PickFolder("Onde instalar o WolfLeague");
		if (string.IsNullOrEmpty(dir)) return;
		// Dentro da pasta escolhida, uma do jogo: desinstalar nunca mexe no que já havia nela.
		if (Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)) != AppInfo.Name) dir = Path.Combine(dir, AppInfo.Name);
		if (!_game.ChangeDir(dir)) _main.Toast("Agora não dá para trocar a pasta.");
	}

	private void AskUninstall() => _main.Ask("Desinstalar o WolfLeague?",
		"Os arquivos do jogo são apagados desta pasta. A sua conta, os seus ajustes e o que você comprou não são tocados: ficam no servidor e na pasta de dados do jogo.",
		"Desinstalar", () => _ = _game.Uninstall(), danger: true);

	// ------------------------------------------------------------------ novidades

	private Manifest NotesFrom => _game.Latest ?? _game.Installed;
	public string NotesTitle => NotesFrom == null ? "Novidades" : "O que há na versão " + NotesFrom.Version;
	public string Notes => string.IsNullOrWhiteSpace(NotesFrom?.Notes) ? "As notas desta versão ainda não chegaram." : NotesFrom.Notes;
}
