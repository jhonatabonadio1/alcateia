using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Alcateia.Pacote;

namespace Alcateia.Cliente.Core;

public enum GameState
{
	/// <summary>Perguntando ao servidor qual é a última versão.</summary>
	Checking,

	/// <summary>Não instalado e sem como baixar agora (<see cref="GameService.Problem"/> diz por quê).</summary>
	Unavailable,

	NotInstalled,
	UpdateAvailable,
	Ready,

	/// <summary>Instalando, atualizando ou reparando.</summary>
	Working,

	/// <summary>O jogador pausou: o que já baixou fica guardado.</summary>
	Paused,

	Playing
}

/// <summary>O que o trabalho em curso é, para a tela dizer.</summary>
public enum WorkKind { Install, Update, Repair, Uninstall }

/// <summary>
/// O jogo visto do cliente: está instalado? em que versão? há uma mais nova?
/// — e as ações: instalar, atualizar, verificar e reparar, desinstalar,
/// jogar. Quem baixa e monta os arquivos é o <see cref="Installer"/>; aqui
/// fica o estado, para a tela mostrar.
///
/// **De onde baixa.** O servidor diz (<c>GET /v1/client</c>, campo
/// <c>downloads</c>); lá, o manifesto do jogo tem nome fixo
/// (<c>wolfleague-windows.manifest.json</c>) e é sempre o da última versão.
/// A variável <c>WOLFLEAGUE_DOWNLOADS</c> passa por cima (um endereço ou uma
/// pasta do disco: é como se testa um pacote antes de publicar).
///
/// **Sem internet**, o jogo instalado continua abrindo: só não se confere se
/// há versão nova.
///
/// Os eventos saem no fio da interface.
/// </summary>
public sealed class GameService
{
	private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

	private readonly Settings _settings;
	private readonly Api _api;
	private SynchronizationContext _ui;
	private CancellationTokenSource _cancel;
	private Process _game;
	private bool _deepPaused;

	public GameService(Settings settings, Api api)
	{
		_settings = settings;
		_api = api;
	}

	public GameState State { get; private set; } = GameState.Checking;

	/// <summary>O que deu errado na última tentativa (ou por que não dá para baixar), para a tela.</summary>
	public string Problem { get; private set; } = "";

	/// <summary>O manifesto da última versão (null: não deu para perguntar) e o do que está instalado (null: nada).</summary>
	public Manifest Latest { get; private set; }
	public Manifest Installed { get; private set; }

	/// <summary>Quanto a instalação ou a atualização vai baixar, no máximo.</summary>
	public UpdateEstimate Estimate { get; private set; }

	public WorkKind Work { get; private set; }
	public InstallProgress Progress { get; private set; }

	/// <summary>Bytes por segundo vindos da rede, alisado.</summary>
	public double Speed { get; private set; }

	/// <summary>Na pasta escolhida há um jogo que o cliente não instalou (o
	/// instalador de antes): instalar ali aproveita o que já está.</summary>
	public bool FoundOldInstall { get; private set; }

	/// <summary>De onde os downloads vêm (para a tela de configurações mostrar).</summary>
	public string Downloads { get; private set; } = "";

	public event Action Changed;

	public string GameDir => _settings.GameDir;

	private string ManifestLocation => Combine(Downloads, Packer.ManifestName(AppInfo.Product, AppInfo.GamePlatform));

	private static bool IsHttp(string location) =>
		location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

	private static string Combine(string root, string name) =>
		IsHttp(root) ? (root.EndsWith('/') ? root : root + "/") + name : Path.Combine(root, name);

	// ------------------------------------------------------------------ conferir

	/// <summary>Pergunta qual é a última versão e acerta o estado. Não mexe
	/// num trabalho em curso nem no jogo aberto.</summary>
	public async Task Refresh()
	{
		_ui ??= SynchronizationContext.Current;
		if (State is GameState.Working or GameState.Playing or GameState.Paused) return;
		if (Latest == null) Set(GameState.Checking);

		Manifest installed = await Task.Run(() => Installer.Installed(_settings.GameDir));
		(Manifest latest, string problem) = await FetchLatest();
		if (State is GameState.Working or GameState.Playing or GameState.Paused) return;

		Installed = installed;
		if (latest != null) Latest = latest;
		Problem = problem;
		Decide();
	}

	private async Task<(Manifest, string)> FetchLatest()
	{
		string downloads = Environment.GetEnvironmentVariable("WOLFLEAGUE_DOWNLOADS");
		if (string.IsNullOrWhiteSpace(downloads))
		{
			Reply info = await _api.ClientInfo();
			if (!info.Ok)
				return (null, info.Status == 404
					? "Este servidor ainda não diz de onde baixar o jogo."
					: "Sem resposta do servidor: não deu para conferir se há versão nova.");
			downloads = info.Body?["downloads"]?.GetValue<string>() ?? "";
			if (downloads.Length == 0) return (null, "Os downloads ainda não foram configurados no servidor.");
		}
		Downloads = downloads.Trim();

		try
		{
			string json;
			if (IsHttp(ManifestLocation))
			{
				// O manifesto tem nome fixo e muda a cada versão: nada de cópia guardada no caminho.
				var request = new HttpRequestMessage(HttpMethod.Get, ManifestLocation);
				request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
				using HttpResponseMessage response = await Http.SendAsync(request, timeout.Token);
				if ((int)response.StatusCode == 404) return (null, "Ainda não há versão publicada para este sistema.");
				response.EnsureSuccessStatusCode();
				json = await response.Content.ReadAsStringAsync(timeout.Token);
			}
			else json = await File.ReadAllTextAsync(ManifestLocation);
			return (Manifest.Parse(json), "");
		}
		catch (ManifestException e)
		{
			return (null, e.Message);
		}
		catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or UnauthorizedAccessException)
		{
			return (null, "Não deu para falar com o servidor de downloads. Confira a sua internet.");
		}
	}

	/// <summary>O estado, a partir do que está instalado e do que há de novo.</summary>
	private void Decide()
	{
		Estimate = null;
		FoundOldInstall = false;
		if (Latest != null)
		{
			var installer = new Installer(_settings.GameDir, null);
			Estimate = installer.Estimate(Latest);
			FoundOldInstall = Installed == null && Latest.Exe != "" && File.Exists(Manifest.OnDisk(_settings.GameDir, Latest.Exe));
		}

		if (Installed == null) Set(Latest == null ? GameState.Unavailable : GameState.NotInstalled);
		else if (Latest != null && (Latest.Version != Installed.Version || Estimate.Files > 0)) Set(GameState.UpdateAvailable);
		else Set(GameState.Ready);
	}

	// ------------------------------------------------------------------ instalar, atualizar, reparar

	public Task Install() => Run(Installed == null ? WorkKind.Install : WorkKind.Update, deep: false);

	/// <summary>Lê todos os arquivos e conserta o que estiver diferente.</summary>
	public Task Repair() => Run(WorkKind.Repair, deep: true);

	/// <summary>Para o trabalho em curso. O que já baixou fica; <see cref="Resume"/> continua.</summary>
	public void Pause()
	{
		if (State != GameState.Working || Work == WorkKind.Uninstall) return;
		_deepPaused = Work == WorkKind.Repair;
		_cancel?.Cancel();
	}

	public Task Resume() => State == GameState.Paused ? Run(Work, _deepPaused) : Task.CompletedTask;

	/// <summary>Desiste do que estava pausado: volta ao estado de antes (o que baixou segue no disco).</summary>
	public void Dismiss()
	{
		if (State != GameState.Paused) return;
		Decide();
	}

	private async Task Run(WorkKind kind, bool deep)
	{
		if (State is GameState.Working or GameState.Playing or GameState.Checking) return;
		_ui ??= SynchronizationContext.Current;
		Manifest target = Latest ?? (deep ? Installed : null);
		if (target == null) return;

		Work = kind;
		Problem = "";
		Speed = 0;
		Progress = new InstallProgress(InstallPhase.Checking, 0, 1, 0, 0, "");
		_cancel = new CancellationTokenSource();
		CancellationToken cancel = _cancel.Token;
		Set(GameState.Working);

		var meter = new SpeedMeter();
		var progress = new Relay(p => Post(() =>
		{
			if (State != GameState.Working) return;
			Progress = p;
			Speed = meter.Sample(p);
			Changed?.Invoke();
		}));

		try
		{
			IBlobSource source = BlobSources.For(target.Base, Http, ManifestLocation);
			var installer = new Installer(_settings.GameDir, source);
			await Task.Run(() => installer.Apply(target, deep, progress, cancel), CancellationToken.None);
			Installed = target;
		}
		catch (OperationCanceledException)
		{
			Set(GameState.Paused);
			return;
		}
		catch (InstallException e)
		{
			Problem = e.Message;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or ManifestException)
		{
			Problem = "Não deu para terminar: " + e.Message;
		}
		Installed = await Task.Run(() => Installer.Installed(_settings.GameDir));
		string problem = Problem;
		Decide();
		// O Decide limpa o estado, não o recado do que acabou de falhar.
		if (problem != "")
		{
			Problem = problem;
			Changed?.Invoke();
		}
	}

	// ------------------------------------------------------------------ desinstalar

	public async Task Uninstall()
	{
		if (State is GameState.Working or GameState.Playing or GameState.Checking) return;
		Work = WorkKind.Uninstall;
		Problem = "";
		Set(GameState.Working);
		string dir = _settings.GameDir;
		await Task.Run(() => Installer.Uninstall(dir));
		Installed = null;
		Decide();
	}

	/// <summary>Troca a pasta de instalação (só sem jogo instalado nela e sem trabalho em curso).</summary>
	public bool ChangeDir(string dir)
	{
		if (State is GameState.Working or GameState.Playing or GameState.Paused || string.IsNullOrWhiteSpace(dir)) return false;
		_settings.GameDir = dir;
		_settings.Save();
		Installed = Installer.Installed(dir);
		Decide();
		return true;
	}

	// ------------------------------------------------------------------ jogar

	/// <summary>
	/// Abre o jogo já com a conta do cliente: o login vai em variáveis de
	/// ambiente do processo do jogo (<c>ALCATEIA_SESSAO</c>, <c>ALCATEIA_CONTA</c>),
	/// que só ele enxerga — na linha de comando qualquer programa do
	/// computador leria. O jogo entra com ela uma vez (Backend.TakeLauncherAccess).
	/// </summary>
	public bool Play(Account account)
	{
		if (State is not (GameState.Ready or GameState.UpdateAvailable) || Installed == null || Installed.Exe == "") return false;
		_ui ??= SynchronizationContext.Current;
		try
		{
			string exe = Manifest.OnDisk(_settings.GameDir, Installed.Exe);
			var start = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false };
			if (account != null)
			{
				start.Environment["ALCATEIA_SESSAO"] = account.Token;
				start.Environment["ALCATEIA_CONTA"] = account.Name;
			}
			if (AppInfo.ServerOverride != null) start.Environment["ALCATEIA_API"] = AppInfo.ServerOverride;

			_game = Process.Start(start);
			if (_game == null) throw new InvalidOperationException("o sistema não abriu o programa");
			_game.EnableRaisingEvents = true;
			_game.Exited += (_, _) => Post(() =>
			{
				_game = null;
				if (State == GameState.Playing) _ = AfterGame();
			});
			Problem = "";
			Set(GameState.Playing);
			return true;
		}
		catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or ManifestException)
		{
			Problem = "Não consegui abrir o jogo: " + e.Message + " Use Verificar e reparar.";
			Changed?.Invoke();
			return false;
		}
	}

	private async Task AfterGame()
	{
		State = GameState.Ready;
		await Refresh();
	}

	// ------------------------------------------------------------------

	/// <summary>Só para fotografar as telas (cliente/Alcateia.Cliente.Fotos): um estado de mentira, sem servidor.</summary>
	internal void Preview(GameState state, Manifest latest, Manifest installed, WorkKind work = WorkKind.Install,
		InstallProgress progress = default, double speed = 0, string problem = "")
	{
		Latest = latest;
		Installed = installed;
		Work = work;
		Progress = progress;
		Speed = speed;
		Problem = problem;
		Estimate = latest == null ? null : new UpdateEstimate(3, 180L << 20, 96L << 20);
		Downloads = "https://downloads.exemplo/";
		Set(state);
	}

	private void Set(GameState state)
	{
		State = state;
		Changed?.Invoke();
	}

	private void Post(Action action)
	{
		if (_ui != null) _ui.Post(_ => action(), null);
		else action();
	}

	private sealed class Relay : IProgress<InstallProgress>
	{
		private readonly Action<InstallProgress> _on;
		public Relay(Action<InstallProgress> on) => _on = on;
		public void Report(InstallProgress value) => _on(value);
	}

	/// <summary>A velocidade do download, alisada: sem isso o número pula a cada pedaço.</summary>
	private sealed class SpeedMeter
	{
		private readonly Stopwatch _clock = Stopwatch.StartNew();
		private long _lastBytes;
		private double _lastTime, _speed;

		public double Sample(InstallProgress p)
		{
			if (p.Phase != InstallPhase.Downloading) return 0;
			double now = _clock.Elapsed.TotalSeconds, dt = now - _lastTime;
			if (dt < 0.5) return _speed;
			double instant = Math.Max(0, p.Downloaded - _lastBytes) / dt;
			_speed = _speed <= 0 ? instant : _speed * 0.7 + instant * 0.3;
			_lastBytes = p.Downloaded;
			_lastTime = now;
			return _speed;
		}
	}
}
