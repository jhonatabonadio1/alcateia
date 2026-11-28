using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;
using Alcateia.Pacote;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Alcateia.Cliente.ViewModels;

/// <summary>Qual aba da página do jogo está aberta.</summary>
public enum GameTab { Overview, News }

/// <summary>
/// A página de um jogo da biblioteca: a arte e o logo dele, as abas (visão
/// geral, novidades), um botão grande que é sempre a próxima coisa a fazer
/// (instalar, atualizar, jogar, continuar), a linha que diz como o jogo
/// está, a barra do download e o menu de opções do jogo (verificar e
/// reparar, abrir a pasta, mudar a pasta, desinstalar).
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
		ShowOverview = new Command(() => Tab = GameTab.Overview);
		ShowNews = new Command(() => Tab = GameTab.News);
	}

	/// <summary>O jogo da biblioteca e o serviço que cuida dele.</summary>
	public GameEntry Entry => _game.Entry;
	public GameService Service => _game;

	// ------------------------------------------------------------------ a arte

	private Bitmap _art, _logo, _icon;
	public Bitmap Art => _art ??= Load(Entry.Art);
	public Bitmap Logo => _logo ??= Load(Entry.Logo);
	public Bitmap Icon => _icon ??= Load(Entry.Icon);
	public string Stage => Entry.Stage;
	public bool HasStage => !string.IsNullOrEmpty(Entry.Stage);

	private static Bitmap Load(string uri)
	{
		try
		{
			using Stream stream = AssetLoader.Open(new Uri(uri));
			return new Bitmap(stream);
		}
		catch (Exception e) when (e is IOException or FileNotFoundException or InvalidOperationException)
		{
			return null;
		}
	}

	// ------------------------------------------------------------------ as abas

	private GameTab _tab = GameTab.Overview;
	public GameTab Tab
	{
		get => _tab;
		set
		{
			if (!Set(ref _tab, value)) return;
			Raise(nameof(IsOverview));
			Raise(nameof(IsNews));
		}
	}

	public bool IsOverview => _tab == GameTab.Overview;
	public bool IsNews => _tab == GameTab.News;
	public Command ShowOverview { get; }
	public Command ShowNews { get; }

	internal void Refresh() => OnChanged();

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

	public string PrimaryText => Tr.T(State switch
	{
		GameState.Checking => "game.btn_checking",
		GameState.Unavailable => "game.btn_unavailable",
		GameState.NotInstalled => "game.btn_install",
		GameState.UpdateAvailable => "game.btn_update",
		GameState.Ready => "game.btn_play",
		GameState.Paused => "game.btn_resume",
		GameState.Playing => "game.btn_playing",
		_ => _game.Work switch
		{
			WorkKind.Install => "game.btn_installing",
			WorkKind.Update => "game.btn_updating",
			WorkKind.Repair => "game.btn_verifying",
			_ => "game.btn_uninstalling"
		}
	});

	/// <summary>Uma palavra de como o jogo está, para o menu da esquerda.</summary>
	public string ShortStatus => State switch
	{
		GameState.Checking => "",
		GameState.Unavailable => Tr.T("lib.unavailable"),
		GameState.NotInstalled => Tr.T("lib.not_installed"),
		GameState.UpdateAvailable => Tr.T("lib.update"),
		GameState.Ready => Tr.T("lib.ready"),
		GameState.Paused => Tr.T("lib.paused"),
		GameState.Playing => Tr.T("lib.playing"),
		_ => _game.Work == WorkKind.Uninstall ? Tr.T("lib.uninstalling") : Tr.F("lib.working", (int)ProgressValue)
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

	public string Title => Entry.Name.ToUpperInvariant();

	/// <summary>Como o jogo está, numa linha.</summary>
	public string StatusLine
	{
		get
		{
			Manifest latest = _game.Latest, installed = _game.Installed;
			switch (State)
			{
				case GameState.Checking: return Tr.T("game.st_checking");
				case GameState.Unavailable: return _game.Problem;
				case GameState.NotInstalled:
					return _game.FoundOldInstall
						? Tr.F("game.st_found_old", latest.Version)
						: Tr.F("game.st_not_installed", latest.Version, Sizes.Text(latest.DownloadSize), Sizes.Text(latest.InstallSize));
				case GameState.UpdateAvailable:
					string size = _game.Estimate == null ? "" : Tr.F("game.st_upto", Sizes.Text(_game.Estimate.DownloadBytes));
					return latest.Version == installed.Version
						? Tr.F("game.st_repair_needed", installed.Version, size)
						: Tr.F("game.st_update", latest.Version, installed.Version, size);
				case GameState.Ready:
					return latest == null ? Tr.F("game.st_offline", installed.Version) : Tr.F("game.st_ready", installed.Version);
				case GameState.Paused: return Tr.T("game.st_paused");
				case GameState.Playing: return Tr.T("game.st_playing");
				default: return WorkLine;
			}
		}
	}

	private string WorkLine
	{
		get
		{
			if (_game.Work == WorkKind.Uninstall) return Tr.T("game.wk_uninstall");
			InstallProgress p = _game.Progress;
			switch (p.Phase)
			{
				case InstallPhase.Checking: return Tr.T("game.wk_checking");
				case InstallPhase.Finishing: return Tr.T("game.wk_finishing");
				case InstallPhase.Downloading:
					if (p.DownloadTotal <= 0) return Tr.F("game.wk_building", Sizes.Text(p.Done), Sizes.Text(p.Total));
					string speed = _game.Speed > 0 ? $" · {Sizes.Text((long)_game.Speed)}/s" : "";
					string left = _game.Speed > 1024 ? " · " + Remaining((p.DownloadTotal - p.Downloaded) / _game.Speed) : "";
					return Tr.F("game.wk_download", Sizes.Text(p.Downloaded), Sizes.Text(p.DownloadTotal)) + speed + left;
				default: return "";
			}
		}
	}

	private static string Remaining(double seconds)
	{
		if (seconds < 60) return Tr.T("game.left_lt1");
		if (seconds < 3600) return Tr.F("game.left_min", (int)Math.Ceiling(seconds / 60));
		return Tr.F("game.left_h", (int)(seconds / 3600), (int)(seconds % 3600 / 60));
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
	public string InstalledVersion => _game.Installed == null ? Tr.T("lib.not_installed") : Tr.F("game.version", _game.Installed.Version);
	public string InstalledSize => _game.Installed == null ? "" : Tr.F("game.on_disk", Sizes.Text(_game.Installed.InstallSize));
	public string InstallDirLine => Tr.F("game.folder_line", _game.GameDir);

	private void OpenDir()
	{
		try
		{
			Directory.CreateDirectory(_game.GameDir);
			Process.Start(new ProcessStartInfo(_game.GameDir) { UseShellExecute = true });
		}
		catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
		{
			_main.Toast(Tr.T("toast.open_folder_failed"));
		}
	}

	private async Task PickDir()
	{
		if (_main.PickFolder == null) return;
		string dir = await _main.PickFolder(Tr.F("game.pick_folder", Entry.Name));
		if (string.IsNullOrEmpty(dir)) return;
		// Dentro da pasta escolhida, uma do jogo: desinstalar nunca mexe no que já havia nela.
		if (Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)) != Entry.Folder) dir = Path.Combine(dir, Entry.Folder);
		if (!_game.ChangeDir(dir)) _main.Toast(Tr.T("toast.cant_change_folder"));
	}

	private void AskUninstall() => _main.Ask(Tr.F("game.uninstall_title", Entry.Name),
		Tr.T("game.uninstall_body"), Tr.T("game.uninstall"), () => _ = _game.Uninstall(), danger: true);

	// ------------------------------------------------------------------ novidades

	private Manifest NotesFrom => _game.Latest ?? _game.Installed;
	public string NotesTitle => NotesFrom == null ? Tr.T("game.tab_news") : Tr.F("game.notes_title", NotesFrom.Version);
	public string Notes => string.IsNullOrWhiteSpace(NotesFrom?.Notes) ? Tr.T("game.notes_none") : NotesFrom.Notes;
}
