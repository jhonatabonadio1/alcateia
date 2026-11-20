using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Seguranca;

namespace Alcateia.Aplicacao.Seguranca;

/// <summary>Os limites do escudo. Os padrões servem para produção.</summary>
public sealed class SecurityOptions
{
	/// <summary>Pedidos por minuto, por endereço, na API toda.</summary>
	public int RequestsPerMinute = 600;
	/// <summary>Logins errados, do mesmo endereço, que viram bloqueio.</summary>
	public int LoginFailuresToBlock = 10;
	/// <summary>Nomes diferentes tentados do mesmo endereço (uma senha em
	/// várias contas) que viram bloqueio.</summary>
	public int SprayedNamesToBlock = 6;
	/// <summary>Varreduras ou injeções que viram bloqueio.</summary>
	public int StrikesToBlock = 3;
	/// <summary>Recusas (401, 403, 404) por minuto que viram bloqueio: é
	/// quem está adivinhando rota ou identificador.</summary>
	public int DeniedPerMinuteToBlock = 60;
	/// <summary>A janela em que as contagens acima valem.</summary>
	public TimeSpan Window = TimeSpan.FromMinutes(10);
	/// <summary>O primeiro bloqueio automático; cada reincidência quadruplica, até um dia.</summary>
	public TimeSpan FirstBlock = TimeSpan.FromMinutes(15);
	public TimeSpan LongestBlock = TimeSpan.FromHours(24);
	/// <summary>Desligado, o servidor só registra (para observar antes de bloquear).</summary>
	public bool AutoBlock = true;
	/// <summary>Endereço de rede interna (127.x, 10.x, 192.168.x...) não é
	/// bloqueado nem freado sozinho: atrás de um proxy mal configurado,
	/// todo mundo chegaria com o endereço dele e um ataque derrubaria o
	/// jogo para todos. Os testes ligam.</summary>
	public bool PoliceInternal;
	/// <summary>Nunca bloqueados (o escritório, o monitoramento).</summary>
	public List<IpRange> Allow = new();
	/// <summary>Eventos mais velhos que isso são apagados.</summary>
	public int RetentionDays = 90;
}

/// <summary>Os contadores do escudo, para as métricas.</summary>
public sealed class SecurityCounters
{
	public long Requests, Refused, RateLimited, AutoBlocks, Events, EventsDropped;
}

/// <summary>
/// O escudo do servidor (as lições do CT, aplicadas aqui):
///
/// - a <b>blacklist de IP</b>, conferida em todo pedido — em memória, sem
///   tocar o banco, que só é lido ao subir e a cada minuto;
/// - o <b>registro de eventos</b> (login certo e errado, varredura,
///   injeção, freio, bloqueio): vai para uma fila e é gravado em lote por
///   uma tarefa só, e sai também como linha de log estruturada;
/// - a <b>detecção</b>: força bruta, uma senha em várias contas, login
///   certo depois de vários errados, varredura de rotas, injeção,
///   enumeração — e o <b>bloqueio automático</b>, que cresce a cada
///   reincidência.
///
/// Nada aqui fica no caminho da partida: o jogo em si é UDP direto no
/// servidor de partida e não passa por esta API.
/// </summary>
public sealed class SecurityService : IDisposable
{
	private sealed class Watch
	{
		public DateTime Window;
		public int Requests, Denied;
		public bool LimitNoted, RefusalNoted;
		public readonly Queue<DateTime> LoginFailures = new();
		public readonly Dictionary<string, DateTime> Names = new();
		public readonly Queue<DateTime> Strikes = new();
		public DateTime LastSeen;
	}

	private sealed class Blocklist
	{
		public readonly HashSet<IPAddress> Single = new();
		public readonly List<IpRange> Ranges = new();
		public IReadOnlyList<IpBlock> All = Array.Empty<IpBlock>();
	}

	private readonly ISecurityRepository _repository;
	private readonly ISecurityLog _log;
	private readonly IClock _clock;
	private readonly ConcurrentDictionary<string, Watch> _watch = new();
	private readonly ConcurrentDictionary<string, int> _offenses = new();
	private readonly Channel<SecurityEvent> _queue = Channel.CreateBounded<SecurityEvent>(
		new BoundedChannelOptions(20_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
	private readonly CancellationTokenSource _stop = new();
	private volatile Blocklist _blocks = new();
	private Task _writer;
	private Timer _housekeeping;

	public SecurityOptions Options { get; }
	public SecurityCounters Counters { get; } = new();

	public SecurityService(ISecurityRepository repository, ISecurityLog log, IClock clock, SecurityOptions options = null)
	{
		_repository = repository;
		_log = log;
		_clock = clock;
		Options = options ?? new SecurityOptions();
	}

	/// <summary>Carrega a blacklist e liga a gravação dos eventos.</summary>
	public async Task Start()
	{
		await Reload();
		_writer = Task.Run(WriteLoop);
		_housekeeping = new Timer(_ => _ = Housekeep(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
	}

	public void Dispose()
	{
		_housekeeping?.Dispose();
		_queue.Writer.TryComplete();
		try { _writer?.Wait(TimeSpan.FromSeconds(3)); }
		catch (AggregateException) { }
		_stop.Cancel();
	}

	// ------------------------------------------------------------ a blacklist

	/// <summary>As faixas bloqueadas agora.</summary>
	public IReadOnlyList<IpBlock> Blocks => _blocks.All;

	/// <summary>O endereço está bloqueado? É a pergunta de todo pedido:
	/// uma busca em memória.</summary>
	public bool IsBlocked(IPAddress address)
	{
		if (address == null) return false;
		Blocklist list = _blocks;
		if (list.Single.Count == 0 && list.Ranges.Count == 0) return false;
		address = IpRange.Normalize(address);
		if (list.Single.Contains(address)) return true;
		foreach (IpRange range in list.Ranges)
			if (range.Contains(address)) return true;
		return false;
	}

	/// <summary>Relê a blacklist do banco (os bloqueios que venceram saem sozinhos).</summary>
	public async Task Reload()
	{
		IReadOnlyList<IpBlock> all = await _repository.IpBlocks();
		var list = new Blocklist { All = all };
		foreach (IpBlock block in all)
		{
			if (!IpRange.TryParse(block.Cidr, out IpRange range)) continue;
			if (range.Single && IPAddress.TryParse(range.Text, out IPAddress one)) list.Single.Add(one);
			else list.Ranges.Add(range);
		}
		_blocks = list;
	}

	/// <summary>Bloqueia um endereço ou uma faixa. <paramref name="hours"/> zero é para sempre.</summary>
	public async Task<IpBlock> Block(string cidr, string reason, double hours, string source, string adminName)
	{
		IpRange range = IpRange.Parse(cidr);
		if (string.IsNullOrWhiteSpace(reason)) throw new Failure(FailureKind.Invalid, "sem_motivo", "Diga o motivo do bloqueio.");
		DateTime? until = hours > 0 ? _clock.UtcNow.AddHours(hours) : null;
		IpBlock block = await _repository.AddIpBlock(range.Text, reason.Trim(), source, until, adminName);
		await Reload();
		return block;
	}

	public async Task<IpBlock> Unblock(long id)
	{
		IpBlock removed = await _repository.RemoveIpBlock(id);
		if (removed != null)
		{
			_offenses.TryRemove(removed.Cidr, out _);
			_watch.TryRemove(removed.Cidr, out _);
			await Reload();
		}
		return removed;
	}

	// ------------------------------------------------------------ o que o painel lê

	public Task<IReadOnlyList<SecurityEvent>> Events(EventFilter filter) => _repository.Events(filter);
	public Task<SecurityOverview> Overview(int hours) => _repository.Overview(hours);
	public Task<IReadOnlyList<Ban>> Bans(bool activeOnly) => _repository.Bans(null, activeOnly, 200);

	// ------------------------------------------------------------ o registro

	/// <summary>Registra um evento. Não espera nada: põe na fila e volta.</summary>
	public void Record(string kind, int severity, string ip, long? accountId = null, string actor = "", string path = "", string detail = "")
	{
		var e = new SecurityEvent(_clock.UtcNow, kind, severity, ip ?? "", accountId, Cut(actor, 64), Cut(path, 200), Cut(detail, 500));
		Interlocked.Increment(ref Counters.Events);
		_log.Write(e);
		if (!_queue.Writer.TryWrite(e)) Interlocked.Increment(ref Counters.EventsDropped);
	}

	/// <summary>Espera a fila de eventos esvaziar (os testes e o desligamento).</summary>
	public async Task Flush()
	{
		for (int i = 0; i < 200 && (_queue.Reader.Count > 0 || _writing); i++) await Task.Delay(10);
	}

	private volatile bool _writing;

	private async Task WriteLoop()
	{
		var batch = new List<SecurityEvent>(256);
		ChannelReader<SecurityEvent> reader = _queue.Reader;
		while (await reader.WaitToReadAsync())
		{
			_writing = true;
			batch.Clear();
			while (batch.Count < 500 && reader.TryRead(out SecurityEvent e)) batch.Add(e);
			try
			{
				await _repository.AddEvents(batch);
			}
			catch (Exception)
			{
				// O banco caiu: os eventos já saíram no log; estes se perdem
				// na tabela, e o servidor continua de pé.
				Interlocked.Add(ref Counters.EventsDropped, batch.Count);
			}
			_writing = false;
			try { await Task.Delay(250, _stop.Token); }
			catch (OperationCanceledException) { }
		}
	}

	private async Task Housekeep()
	{
		try
		{
			await Reload();
			DateTime now = _clock.UtcNow;
			foreach (var entry in _watch)
				if (now - entry.Value.LastSeen > Options.Window + Options.Window) _watch.TryRemove(entry.Key, out _);
			if (now.Minute == 0 && now.Hour % 6 == 0) await _repository.PruneEvents(now.AddDays(-Options.RetentionDays));
		}
		catch (Exception)
		{
			// Fica para a próxima volta.
		}
	}

	// ------------------------------------------------------------ a detecção

	/// <summary>Um pedido chegou. Falso: passou do limite por minuto.</summary>
	public bool AllowRequest(IPAddress address, string ip, string path)
	{
		Interlocked.Increment(ref Counters.Requests);
		if (!Policed(address)) return true;

		Watch w = Of(ip);
		bool note = false;
		lock (w)
		{
			Roll(w);
			w.Requests++;
			if (w.Requests <= Options.RequestsPerMinute) return true;
			if (!w.LimitNoted) note = w.LimitNoted = true;
		}

		Interlocked.Increment(ref Counters.RateLimited);
		if (note)
		{
			Record(EventKind.RateLimited, Severity.Warning, ip, path: path, detail: $"mais de {Options.RequestsPerMinute} pedidos por minuto");
			Strike(address, ip, "pedidos demais");
		}
		return false;
	}

	/// <summary>Um pedido recusado por bloqueio (anota um por minuto, por endereço).</summary>
	public void Refused(string ip, string path)
	{
		Interlocked.Increment(ref Counters.Refused);
		Watch w = Of(ip);
		lock (w)
		{
			Roll(w);
			if (w.RefusalNoted) return;
			w.RefusalNoted = true;
		}
		Record(EventKind.Blocked, Severity.Info, ip, path: path, detail: "endereço na blacklist");
	}

	/// <summary>Varredura ou injeção: registra e conta para o bloqueio.</summary>
	public void Suspicious(IPAddress address, string ip, string kind, string path)
	{
		Record(kind, Severity.High, ip, path: path);
		Strike(address, ip, kind switch
		{
			EventKind.Injection => "tentativa de injeção",
			EventKind.FleetKey => "chave da frota errada",
			_ => "varredura de rotas"
		});
	}

	/// <summary>Uma recusa (401, 403, 404): muitas em seguida é enumeração.</summary>
	public void Denied(IPAddress address, string ip, string path, int status)
	{
		if (!Policed(address)) return;
		Watch w = Of(ip);
		lock (w)
		{
			Roll(w);
			if (++w.Denied != Options.DeniedPerMinuteToBlock) return;
		}
		Record(EventKind.Enumeration, Severity.High, ip, path: path, detail: $"{Options.DeniedPerMinuteToBlock} recusas em um minuto (última: {status})");
		_ = AutoBlock(address, ip, "enumeração de rotas ou identificadores");
	}

	/// <summary>Login errado (do jogo ou do painel).</summary>
	public void LoginFailed(IPAddress address, string ip, string name, string path, bool admin = false)
	{
		Record(admin ? EventKind.AdminLoginFailed : EventKind.LoginFailed, admin ? Severity.High : Severity.Warning, ip, actor: name, path: path);

		DateTime now = _clock.UtcNow;
		Watch w = Of(ip);
		int failures, names;
		lock (w)
		{
			w.LastSeen = now;
			w.LoginFailures.Enqueue(now);
			while (w.LoginFailures.Count > 0 && now - w.LoginFailures.Peek() > Options.Window) w.LoginFailures.Dequeue();
			w.Names[(name ?? "").ToLowerInvariant()] = now;
			foreach (string old in w.Names.Where(n => now - n.Value > Options.Window).Select(n => n.Key).ToList()) w.Names.Remove(old);
			failures = w.LoginFailures.Count;
			names = w.Names.Count;
		}

		if (names == Options.SprayedNamesToBlock)
		{
			Record(EventKind.Spraying, Severity.High, ip, path: path, detail: $"{names} nomes diferentes tentados em {Options.Window.TotalMinutes:0} min");
			_ = AutoBlock(address, ip, "uma senha em várias contas");
		}
		else if (failures == Options.LoginFailuresToBlock)
		{
			Record(EventKind.BruteForce, Severity.High, ip, actor: name, path: path, detail: $"{failures} logins errados em {Options.Window.TotalMinutes:0} min");
			_ = AutoBlock(address, ip, "força bruta no login");
		}
	}

	/// <summary>Login certo. Depois de vários errados do mesmo endereço, é
	/// o evento mais importante do registro: pode ser a senha que acertaram.</summary>
	public void LoginOk(string ip, long? accountId, string name, string path, bool admin = false)
	{
		int before = 0;
		if (_watch.TryGetValue(ip ?? "", out Watch w))
			lock (w)
			{
				before = w.LoginFailures.Count;
				w.LoginFailures.Clear();
				w.Names.Clear();
			}

		Record(admin ? EventKind.AdminLoginOk : EventKind.LoginOk, Severity.Info, ip, accountId, name, path);
		if (before >= 3)
			Record(EventKind.LoginAfterFailures, admin ? Severity.Critical : Severity.High, ip, accountId, name, path, $"depois de {before} tentativas erradas");
	}

	private void Strike(IPAddress address, string ip, string reason)
	{
		DateTime now = _clock.UtcNow;
		Watch w = Of(ip);
		lock (w)
		{
			w.LastSeen = now;
			w.Strikes.Enqueue(now);
			while (w.Strikes.Count > 0 && now - w.Strikes.Peek() > Options.Window) w.Strikes.Dequeue();
			if (w.Strikes.Count != Options.StrikesToBlock) return;
		}
		_ = AutoBlock(address, ip, reason);
	}

	private async Task AutoBlock(IPAddress address, string ip, string reason)
	{
		if (!Options.AutoBlock || !Policed(address) || IsBlocked(address)) return;

		int offense = _offenses.AddOrUpdate(ip, 1, (_, n) => n + 1);
		double minutes = Math.Min(Options.LongestBlock.TotalMinutes, Options.FirstBlock.TotalMinutes * Math.Pow(4, offense - 1));
		try
		{
			await Block(ip, reason, minutes / 60.0, "auto", "");
			Interlocked.Increment(ref Counters.AutoBlocks);
			Record(EventKind.AutoBlock, Severity.Critical, ip, detail: $"{reason}; bloqueado por {minutes:0} min (reincidência {offense})");
		}
		catch (Exception)
		{
			// Sem banco não há bloqueio persistido; o freio por minuto continua valendo.
		}
	}

	/// <summary>Este endereço entra nas contagens e pode ser bloqueado sozinho?</summary>
	private bool Policed(IPAddress address)
	{
		if (address == null) return false;
		address = IpRange.Normalize(address);
		foreach (IpRange allowed in Options.Allow)
			if (allowed.Contains(address)) return false;
		return Options.PoliceInternal || !Internal(address);
	}

	private static readonly IpRange[] InternalRanges =
	{
		IpRange.Parse("127.0.0.0/8"), IpRange.Parse("10.0.0.0/8"), IpRange.Parse("172.16.0.0/12"), IpRange.Parse("192.168.0.0/16"),
		IpRange.Parse("169.254.0.0/16"), IpRange.Parse("::1"), IpRange.Parse("fc00::/7", true), IpRange.Parse("fe80::/10", true)
	};

	public static bool Internal(IPAddress address) => InternalRanges.Any(r => r.Contains(address));

	private Watch Of(string ip) => _watch.GetOrAdd(ip ?? "", _ => new Watch { Window = _clock.UtcNow, LastSeen = _clock.UtcNow });

	/// <summary>Vira o minuto das contagens por minuto. Com a trava de <paramref name="w"/>.</summary>
	private void Roll(Watch w)
	{
		DateTime now = _clock.UtcNow;
		w.LastSeen = now;
		if (now - w.Window < TimeSpan.FromMinutes(1)) return;
		w.Window = now;
		w.Requests = w.Denied = 0;
		w.LimitNoted = w.RefusalNoted = false;
	}

	private static string Cut(string text, int max) => text == null ? "" : text.Length <= max ? text : text[..max];
}
