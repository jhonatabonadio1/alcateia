using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// O servidor de partida: o próprio jogo rodando sem tela
/// (<c>godot --headless -- --servidor</c>). Aguenta várias partidas ao
/// mesmo tempo (<c>ALCATEIA_MATCHES</c>, 4 por padrão), cada uma com os
/// seus jogadores (<see cref="HostedMatch"/>), todas na mesma porta UDP.
///
/// O que ele faz, em ordem:
///
/// 1. bate ponto no servidor do Alcateia a cada dois segundos
///    (<c>POST /v1/fleet/heartbeat</c>) dizendo onde está, quantas
///    partidas aguenta e quais tem abertas;
/// 2. quando a resposta traz partidas novas, abre um juiz
///    (<see cref="Referee"/>) para cada uma, com a escalação e as regras
///    da sala;
/// 3. recebe os jogadores na porta — cada um mostra o ingresso que o
///    saguão assinou, que diz em que partida ele entra; sem ingresso
///    válido, fora;
/// 4. repassa o estado e os ataques de cada um aos outros da mesma
///    partida, leva os acertos até a vítima e anuncia os abates que o
///    juiz aceita;
/// 5. no fim de cada partida (placar, tempo, ou todo mundo saiu), manda o
///    resultado (<c>POST /v1/matches</c>), solta os jogadores dela e
///    libera o lugar.
///
/// A partida que fecha sem resultado (ninguém entrou) some da lista da
/// próxima batida, e o servidor do Alcateia libera o lugar e a sala.
///
/// Não carrega mapa nem simula corpos: quem move cada corpo é a máquina
/// do dono (veja <see cref="NetMatch"/> e docs/decisoes/0008). Por isso é
/// leve — um processo aguenta várias partidas com folga. Os bots da sala
/// são movidos pela máquina do dono da sala, que fala por eles; aqui só
/// se confere que o bot é mesmo de quem fala.
///
/// Variáveis de ambiente: <c>ALCATEIA_API</c> (o servidor),
/// <c>ALCATEIA_FLEET_KEY</c> (o segredo da frota),
/// <c>ALCATEIA_PUBLIC_ADDRESS</c> (o endereço que os jogadores usam),
/// <c>ALCATEIA_GAME_PORT</c> (a porta UDP, 7777; ou uma faixa, 7777-7876,
/// e ele fica com a primeira livre — é como vários sobem na mesma máquina),
/// <c>ALCATEIA_MATCHES</c> (quantas partidas ao mesmo tempo, 4),
/// <c>ALCATEIA_NETCODE</c> (<c>repasse</c>, o padrão: cada jogo move o
/// próprio corpo; ou <c>autoritativo</c>: o servidor move os corpos a 60
/// passos por segundo, decisão 0011).
///
/// Na autoritativa, o servidor mede quanto cada passo leva (todas as
/// partidas juntas) e escreve a média e o pior a cada 30 s: o passo tem de
/// caber com folga nos 16,7 ms, senão o servidor atrasa todo mundo.
/// </summary>
public partial class MatchServer : Node, HostedMatch.IMatchLink
{
	public const int DefaultPort = 7777, DefaultCapacity = 4;
	private const double HeartbeatEvery = 2, HelloTimeout = 10;
	/// <summary>O máximo que um quadro conta para os prazos e o relógio das partidas.</summary>
	private const double MaxTickSeconds = 0.25;

	// O que o painel mostra em Segurança → Partidas: os mesmos tipos de
	// servidor/Alcateia.Dominio/Seguranca/SecurityEvent.cs, e a gravidade.
	private const string KindConnectionLost = "partida.conexao_caiu", KindRefused = "partida.recusado",
		KindNoHello = "partida.sem_apresentacao", KindEmpty = "partida.vazia", KindAbandoned = "partida.abandonada",
		KindResultFailed = "partida.resultado_falhou", KindServerError = "partida.erro_servidor", KindSlowStep = "partida.passo_lento";
	private const int Info = 0, Warning = 1, High = 2;

	// O passo da física a 60/s tem 16,7 ms: acima disto o servidor atrasa todo mundo.
	private const double SlowAverageMs = 10, SlowWorstMs = 40;

	// No Fly.io, o id da máquina: o painel mostra o mesmo nome que o "fly machine list".
	private readonly string _id = OS.GetEnvironment("ALCATEIA_SERVER_ID") is { Length: > 0 } id ? id
		: OS.GetEnvironment("FLY_MACHINE_ID") is { Length: > 0 } machine ? machine : Guid.NewGuid().ToString("N")[..12];

	// Servidor sob demanda (docs/FLY.md): ocioso por _idleExit segundos, pede
	// para sair da frota e encerra — a máquina para. Zero: nunca.
	private double _idleExit, _idle;
	private bool _leaving;
	private string _fleetKey, _address;
	private int _port, _capacity;

	private SceneMultiplayer _net;
	private ENetMultiplayerPeer _peer;

	private readonly Dictionary<string, HostedMatch> _matches = new();   // chave → partida
	private readonly Dictionary<long, HostedMatch> _matchOf = new();     // ligação → partida
	private readonly Dictionary<long, double> _strangers = new();        // ligação → há quanto tempo calada

	// Resultado ainda a caminho: a partida continua na lista da batida até
	// ele chegar, senão o servidor do Alcateia a daria por fechada sem
	// resultado e recusaria o resultado depois.
	private readonly HashSet<string> _reporting = new();

	// O que deu errado aqui, para o painel: vai junto da próxima batida. A
	// fila é pequena de propósito: com a API fora do ar por muito tempo, os
	// mais velhos se perdem em vez de encher a memória.
	private sealed record IncidentNote(string Kind, int Severity, string Match, long Account, string Detail);
	private readonly List<IncidentNote> _incidents = new();
	private const int MaxIncidents = 40, IncidentsPerBeat = 20;

	// O mesmo incidente (tipo, partida, conta) só vira aviso a cada tanto:
	// quem insiste em bater na porta não enche o painel.
	private readonly Dictionary<string, (double At, int Skipped)> _noted = new();
	private const double SameIncidentEvery = 15;

	// Quem chegou antes de a partida chegar: o saguão avisa os jogadores
	// e o servidor de partida ao mesmo tempo, e o jogador pode ganhar a
	// corrida. Espera aqui até a próxima batida trazer a partida.
	// Também quem entrou na sala com a partida em curso e chegou antes do lugar
	// dele (Since: desde quando espera).
	private readonly Dictionary<long, (string Key, long Account, long Expires, string Ticket, double Since)> _early = new();

	private double _heartbeat;
	private bool _beating;

	private bool _authoritative;
	private uint _tick;
	private readonly System.Diagnostics.Stopwatch _stepWatch = new();
	private double _stepTotalMs, _stepWorstMs, _statsTimer;
	private int _stepCount;

	/// <summary>Passos de física (todas as partidas): média e pior, em ms, na
	/// última janela de 30 s.</summary>
	public (double Average, double Worst, int Steps) StepStats { get; private set; }

	/// <summary>Atalhos de teste, os mesmos da partida: -- --duracao=30 --contagem=2</summary>
	private double? _seconds, _countdown;

	/// <summary>O endereço (IPv4) de <paramref name="name"/>, que pode ser
	/// um nome (o "fly-global-services" do Fly está no /etc/hosts); null
	/// para todos os endereços.</summary>
	private static string Bind(string name)
	{
		name = name?.Trim();
		if (string.IsNullOrEmpty(name) || name == "*") return null;
		if (System.Net.IPAddress.TryParse(name, out _)) return name;
		string ip = IP.ResolveHostname(name, IP.Type.Ipv4);
		if (string.IsNullOrEmpty(ip)) GD.PrintErr($"Match server: address of {name} not found (ALCATEIA_BIND); listening on all.");
		return string.IsNullOrEmpty(ip) ? null : ip;
	}

	public override void _Ready()
	{
		Engine.MaxFps = 60;

		_fleetKey = OS.GetEnvironment("ALCATEIA_FLEET_KEY");
		_address = OS.GetEnvironment("ALCATEIA_PUBLIC_ADDRESS");
		var (first, last) = PortRange(OS.GetEnvironment("ALCATEIA_GAME_PORT"));
		_capacity = int.TryParse(OS.GetEnvironment("ALCATEIA_MATCHES"), out int capacity) ? Math.Clamp(capacity, 1, 64) : DefaultCapacity;
		_authoritative = OS.GetEnvironment("ALCATEIA_NETCODE").Trim().ToLowerInvariant() is "autoritativo" or "authoritative";
		_idleExit = double.TryParse(OS.GetEnvironment("ALCATEIA_IDLE_EXIT"), out double idle) ? Math.Max(idle, 0) : 0;
		// Só o teste de ponta a ponta liga: o robô se põe de frente para o
		// alvo "renascendo" vivo. Em produção, renascer vivo é recusado.
		ServerBody.AllowTeleport = OS.GetEnvironment("ALCATEIA_TESTE_TELEPORTE") == "1";

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--duracao=") && double.TryParse(arg["--duracao=".Length..], out double d)) _seconds = d;
			if (arg.StartsWith("--contagem=") && double.TryParse(arg["--contagem=".Length..], out double c)) _countdown = c;
		}

		if (string.IsNullOrEmpty(_fleetKey))
		{
			GD.PrintErr("Match server: ALCATEIA_FLEET_KEY is missing.");
			GetTree().Quit(1);
			return;
		}

		// Antes de abrir a porta e bater: a primeira partida não pode pagar
		// o carregamento (MatchWorld.Warmup). Enquanto aquece, a frota nem
		// sabe que este servidor existe.
		long warm = MatchWorld.Warmup(this);
		GD.Print($"Match server: warmed up in {warm} ms ({Maps.All.Count} maps and the player loaded).");

		// A primeira porta livre da faixa: vários servidores de partida na
		// mesma máquina (docker compose --scale) não brigam pela mesma.
		// O endereço em que a porta abre: todos (o padrão) ou o que
		// ALCATEIA_BIND diz. No Fly.io tem de ser "fly-global-services" —
		// com o endereço curinga o Linux responde do endereço errado e o
		// jogador nunca recebe a resposta (docs/FLY.md).
		string bind = Bind(OS.GetEnvironment("ALCATEIA_BIND"));
		Error error = Error.Failed;
		for (_port = first; _port <= last; _port++)
		{
			_peer = new ENetMultiplayerPeer();
			if (bind != null) _peer.SetBindIP(bind);
			error = _peer.CreateServer(_port, 32 * _capacity);
			if (error == Error.Ok) break;
		}
		if (error != Error.Ok)
		{
			GD.PrintErr($"Match server: could not open any port from {first} to {last}/UDP ({error}).");
			GetTree().Quit(1);
			return;
		}

		_net = new SceneMultiplayer { ServerRelay = false };
		GetTree().SetMultiplayer(_net, GetPath());
		_net.MultiplayerPeer = _peer;
		_net.PeerConnected += id => _strangers[id] = 0;
		_net.PeerDisconnected += OnGone;
		_net.PeerPacket += OnPacket;

		GD.Print($"Match server {_id} on port {_port}/UDP, up to {_capacity} match(es)" +
			(_authoritative ? ", authoritative (60 steps/s)" : ", relay") + $", talking to {Backend.Url}");
	}

	public override void _Process(double delta)
	{
		if (_net == null) return;

		// Um quadro lento (o servidor travou carregando algo) não pode virar
		// "ficou calado 10 s" nem "ninguém entrou em 20 s": para os prazos,
		// o quadro conta no máximo MaxTickSeconds.
		delta = Math.Min(delta, MaxTickSeconds);

		_heartbeat -= delta;
		if (_heartbeat <= 0 && !_beating && !_leaving)
		{
			_heartbeat = HeartbeatEvery;
			_ = Heartbeat();
		}

		if (_idleExit > 0)
		{
			bool quiet = _matches.Count == 0 && _reporting.Count == 0 && _strangers.Count == 0 && _early.Count == 0;
			_idle = quiet ? _idle + delta : 0;
			if (_idle >= _idleExit && !_beating && !_leaving) _ = Leave();
		}

		// Quem conectou e não se apresentou não fica pendurado na porta.
		foreach (long peer in _strangers.Keys.ToList())
		{
			_strangers[peer] += delta;
			if (_strangers[peer] <= HelloTimeout) continue;

			if (_early.Remove(peer, out var early)) Refuse(peer, early.Account, "essa partida não está aqui", early.Key);
			else Incident(KindNoHello, Warning, "", 0, $"ligou na porta e não mostrou o ingresso em {HelloTimeout:0} s");
			Kick(peer);
		}

		foreach (HostedMatch match in _matches.Values.ToList())
		{
			try
			{
				match.Tick(delta);
				if (match.Finished) Close(match);
			}
			catch (Exception e)
			{
				Fault("relógio da partida", match.Key, e);
			}
		}

		_statsTimer += delta;
		if (_statsTimer >= 30 && _stepCount > 0)
		{
			StepStats = (_stepTotalMs / _stepCount, _stepWorstMs, _stepCount);
			GD.Print($"Server step: average {StepStats.Average:0.00} ms, worst {StepStats.Worst:0.00} ms " +
				$"({_matches.Count} match(es), {_matches.Values.Sum(m => m.Bodies)} body(ies))");
			if (StepStats.Average > SlowAverageMs || StepStats.Worst > SlowWorstMs)
				Incident(KindSlowStep, Warning, "", 0, $"passo médio {StepStats.Average:0.0} ms, pior {StepStats.Worst:0.0} ms " +
					$"({_matches.Count} partida(s), {_matches.Values.Sum(m => m.Bodies)} corpo(s)); o orçamento é 16,7 ms");
			_statsTimer = 0;
			_stepTotalMs = _stepWorstMs = 0;
			_stepCount = 0;
		}
	}

	/// <summary>O passo de física de todas as partidas autoritativas: cada
	/// corpo consome o comando do passo; depois a física do Godot move.</summary>
	public override void _PhysicsProcess(double delta)
	{
		if (_net == null || !_authoritative || _matches.Count == 0) return;

		_tick++;
		_stepWatch.Restart();
		foreach (HostedMatch match in _matches.Values)
		{
			try { match.PhysicsStep(_tick, delta); }
			catch (Exception e) { Fault("passo da física", match.Key, e); }
		}
		double ms = _stepWatch.Elapsed.TotalMilliseconds;
		_stepTotalMs += ms;
		_stepWorstMs = Math.Max(_stepWorstMs, ms);
		_stepCount++;
	}

	// ------------------------------------------------------------ o servidor do Alcateia

	private async Task Heartbeat()
	{
		_beating = true;
		var open = new JsonArray();
		foreach (string key in _matches.Keys.Union(_reporting)) open.Add(key);

		// Os incidentes vão junto. Se a batida não chegar, voltam para a fila.
		List<IncidentNote> sent = _incidents.Take(IncidentsPerBeat).ToList();
		_incidents.RemoveRange(0, sent.Count);
		var events = new JsonArray();
		foreach (IncidentNote n in sent)
			events.Add(new JsonObject { ["kind"] = n.Kind, ["severity"] = n.Severity, ["match"] = n.Match, ["account"] = n.Account, ["detail"] = n.Detail });

		Backend.Reply reply = await Backend.AsGameServer("/v1/fleet/heartbeat", new JsonObject
		{
			["id"] = _id, ["address"] = _address ?? "", ["port"] = _port,
			["players"] = _matches.Values.Sum(m => m.Players), ["capacity"] = _capacity, ["matches"] = open,
			["events"] = events
		}, _fleetKey);
		_beating = false;

		if (!reply.Ok)
		{
			_incidents.InsertRange(0, sent);
			if (_incidents.Count > MaxIncidents) _incidents.RemoveRange(0, _incidents.Count - MaxIncidents);
			if (reply.Status != 0) GD.PrintErr($"Match server: the API refused the heartbeat ({reply.Status} {reply.Code}).");
			return;
		}

		foreach (JsonNode node in reply.Body?["assignments"] as JsonArray ?? new JsonArray())
			if (node is JsonObject assignment) Open(assignment);

		// Quem espera um lugar numa partida que já está aqui: tenta de novo (o
		// lugar pode não vir; depois de SeatWait, é recusado).
		foreach (string key in _early.Values.Select(e => e.Key).Distinct().Where(_matches.ContainsKey).ToList())
			Readmit(key);
	}

	/// <summary>Ocioso: pergunta ao servidor se pode desligar. Ele diz não
	/// se a frota ficaria sem lugar livre (ALCATEIA_FLEET_RESERVE); diz sim e
	/// já tira este da lista, para nenhuma partida cair aqui no caminho.</summary>
	private async Task Leave()
	{
		_leaving = true;
		Backend.Reply reply = await Backend.AsGameServer("/v1/fleet/leave", new JsonObject { ["id"] = _id }, _fleetKey);
		if (reply.Ok && reply.Body?["leave"]?.GetValue<bool>() == true)
		{
			GD.Print($"Match server {_id}: idle for {_idle:0} s and not needed by the fleet. Shutting down.");
			GetTree().Quit(0);
			return;
		}
		// Ainda faço falta (ou não deu para perguntar): pergunto de novo daqui a um minuto.
		_idle = Math.Max(_idleExit - 60, 0);
		_leaving = false;
	}

	private void Open(JsonObject assignment)
	{
		string key = assignment["matchKey"]?.GetValue<string>();
		if (key == null) return;

		// A partida que já está aqui, com lugares a mais (alguém entrou na sala
		// com ela jogando): abre os lugares e deixa entrar quem esperava por eles.
		if (_matches.TryGetValue(key, out HostedMatch running))
		{
			running.AddSeats(assignment);
			Readmit(key);
			return;
		}
		// Uma atualização de partida que não está mais aqui não abre partida nova.
		if (assignment["update"]?.GetValue<bool>() == true || _matches.Count >= _capacity) return;

		HostedMatch match = HostedMatch.FromAssignment(assignment, this, _seconds, _countdown, _authoritative);
		if (match.World != null) AddChild(match.World);
		_matches[key] = match;

		Readmit(key);
	}

	/// <summary>Quem chegou antes da partida (ou do lugar dele nela) tenta de novo.</summary>
	private void Readmit(string key)
	{
		foreach (var (peer, hello) in _early.Where(e => e.Value.Key == key).ToList())
		{
			_early.Remove(peer);
			Welcome(peer, hello.Key, hello.Account, hello.Expires, hello.Ticket, hello.Since);
		}
	}

	/// <summary>Quanto quem tem ingresso espera o lugar dele chegar numa partida em
	/// curso (o saguão manda o lugar na próxima batida, a cada 2 s).</summary>
	private const double SeatWait = 15;

	/// <summary>A partida acabou: manda o resultado (o juiz já fechou o placar).</summary>
	public async void Report(HostedMatch match)
	{
		Referee r = match.Referee;
		JsonObject body = match.Result(_id);
		GD.Print($"Match {match.Key} ended: VANGUARD {r.Score(Referee.Blue)} x {r.Score(Referee.Orange)} SYNDICATE.");
		NoteEnded(match);
		if (match.Authoritative)
			GD.Print($"Hits in {match.Key}: {match.HitsAccepted} accepted; refused: " +
				(match.HitsRefused.Count == 0 ? "none" : string.Join(", ", match.HitsRefused.Select(e => $"{HitCheck.Describe(e.Key)} {e.Value}"))) +
				$"; suspicious per player: {string.Join(", ", r.Seats.Where(s => !s.IsBot).Select(s => $"{s.Name} {s.RejectedHits}"))}.");

		// O resultado é o que paga os jogadores: insiste um pouco.
		_reporting.Add(match.Key);
		try
		{
			string lastError = "";
			for (int attempt = 1; attempt <= 4; attempt++)
			{
				Backend.Reply reply = await Backend.AsGameServer("/v1/matches", (JsonObject)body.DeepClone(), _fleetKey);
				if (reply.Ok || reply.Status == 409) return;

				lastError = $"{reply.Status} {reply.Error}";
				GD.PrintErr($"Match server: result not delivered (attempt {attempt}: {lastError})");
				await ToSignal(GetTree().CreateTimer(1.5 * attempt), SceneTreeTimer.SignalName.Timeout);
			}
			Incident(KindResultFailed, High, match.Key, 0, $"o resultado não chegou à API depois de 4 tentativas ({lastError})");
		}
		finally
		{
			_reporting.Remove(match.Key);
		}
	}

	/// <summary>Solta os jogadores da partida e libera o lugar dela.</summary>
	private void Close(HostedMatch match)
	{
		match.Close();
		match.World?.QueueFree();
		_matches.Remove(match.Key);
		foreach (long peer in _matchOf.Where(e => e.Value == match).Select(e => e.Key).ToList()) _matchOf.Remove(peer);
		_heartbeat = 0;   // avisa já que o lugar vagou
	}

	// ------------------------------------------------------------ incidentes para o painel

	/// <summary>Conta um incidente para o painel (Segurança → Partidas): sai na
	/// próxima batida. O mesmo incidente (tipo, partida, conta) só vale a cada
	/// <see cref="SameIncidentEvery"/> s; os de permeio são contados no
	/// próximo. Devolve se valeu.</summary>
	private bool Incident(string kind, int severity, string matchKey = "", long account = 0, string detail = "")
	{
		double now = Time.GetTicksMsec() / 1000.0;
		string id = $"{kind}|{matchKey}|{account}";
		int skipped = 0;
		if (_noted.TryGetValue(id, out var before))
		{
			if (now - before.At < SameIncidentEvery)
			{
				_noted[id] = (before.At, before.Skipped + 1);
				return false;
			}
			skipped = before.Skipped;
		}
		if (_noted.Count > 500) _noted.Clear();   // quem varre a porta com mil nomes não enche a memória
		_noted[id] = (now, 0);

		if (skipped > 0) detail += $" (+{skipped} iguais nos {SameIncidentEvery:0} s anteriores)";
		_incidents.Add(new IncidentNote(kind, severity, matchKey ?? "", account, detail ?? ""));
		if (_incidents.Count > MaxIncidents) _incidents.RemoveRange(0, _incidents.Count - MaxIncidents);
		return true;
	}

	/// <summary>Uma exceção no meio da partida: vai para o painel e para o log,
	/// e as outras partidas seguem.</summary>
	private void Fault(string at, string matchKey, Exception e)
	{
		if (Incident(KindServerError, High, matchKey, 0, $"{at}: {e.GetType().Name}: {e.Message}"))
			GD.PrintErr($"Match server: error in {at} ({matchKey}): {e}");
	}

	/// <summary>Um jogador saiu com a partida em curso. O servidor não sabe se
	/// foi ele que fechou o jogo ou a ligação que caiu: os dois chegam aqui iguais.</summary>
	private void NoteGone(HostedMatch match, long account)
	{
		RefPhase phase = match.Referee.Phase;
		if (phase == RefPhase.Ended) return;   // saiu depois do fim: o normal

		string phaseText = phase switch
		{
			RefPhase.Waiting => "esperando a partida começar",
			RefPhase.Countdown => "na contagem para começar",
			_ => $"em jogo, faltando {match.Referee.TimeLeft:0} s"
		};
		Incident(KindConnectionLost, phase == RefPhase.Live ? Warning : Info, match.Key, account,
			$"saiu ou a ligação caiu {phaseText}; sobram {Math.Max(match.Referee.ConnectedCount - 1, 0)} jogador(es) ligado(s)");
	}

	/// <summary>A partida acabou: se foi por falta de gente, é incidente.</summary>
	private void NoteEnded(HostedMatch match)
	{
		Referee r = match.Referee;
		List<Referee.Seat> humans = r.Seats.Where(s => !s.IsBot).ToList();
		long first = humans.Count > 0 ? humans[0].Id : 0;
		string score = $"{r.Score(Referee.Blue)} x {r.Score(Referee.Orange)}";

		if (!humans.Any(s => s.EverConnected))
		{
			Incident(KindEmpty, Warning, match.Key, first,
				$"ninguém entrou em {r.EmptyTimeout:0} s e a partida fechou vazia ({score}); escalados: {string.Join(",", humans.Select(s => s.Id))}");
			return;
		}

		bool byTime = r.TimeLeft <= 0;
		bool byScore = r.Score(Referee.Blue) >= r.ScoreToWin || r.Score(Referee.Orange) >= r.ScoreToWin;
		if (!byTime && !byScore)
			Incident(KindAbandoned, Warning, match.Key, first,
				$"acabou porque ficou {r.EmptyTimeout:0} s sem ninguém ({score}, faltavam {r.TimeLeft:0} s)");
	}

	// ------------------------------------------------------------ os jogadores

	private void OnPacket(long peer, byte[] packet)
	{
		if (packet.Length == 0) return;

		try
		{
			using BinaryReader r = NetWire.Open(packet);
			byte type = r.ReadByte();

			if (type == NetWire.Hello)
			{
				Welcome(peer, r.ReadString(), r.ReadInt64(), r.ReadInt64(), r.ReadString());
				return;
			}

			// Daqui para baixo, só quem já entrou — e só na partida dele.
			if (_matchOf.TryGetValue(peer, out HostedMatch match)) match.Receive(peer, type, packet, r);
		}
		catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException or IndexOutOfRangeException)
		{
			// Pacote torto: ignora. Quem insiste em mandar lixo só perde o próprio jogo.
		}
	}

	private void Welcome(long peer, string matchKey, long account, long expires, string ticket, double since = -1)
	{
		string refusal =
			expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "o ingresso venceu"
			: !SameText(ticket, Sign(_fleetKey, $"{matchKey}|{account}|{expires}")) ? "ingresso inválido"
			: null;
		double now = Time.GetTicksMsec() / 1000.0;
		if (since < 0) since = now;

		// A partida ainda não chegou, ou chegou sem o lugar de quem entrou com
		// ela em curso: espera a próxima batida (o lugar, só um tanto).
		bool noMatch = !_matches.TryGetValue(matchKey, out HostedMatch waiting);
		bool noSeat = !noMatch && waiting.Referee.Find(account) == null && waiting.Referee.Phase != RefPhase.Ended && now - since < SeatWait;
		if (refusal == null && (noMatch || noSeat))
		{
			_early[peer] = (matchKey, account, expires, ticket, since);
			_heartbeat = 0;
			return;
		}

		string note = "";
		HostedMatch match = refusal == null ? _matches[matchKey] : null;
		if (match != null && !match.Admit(peer, account))
		{
			refusal = "você não está nesta partida";
			note = match.Referee.Phase == RefPhase.Ended ? "a partida já tinha acabado" : "a conta não está na escalação";
		}

		if (refusal != null)
		{
			Refuse(peer, account, refusal, matchKey, note);
			return;
		}

		// Trocou de partida na mesma ligação: sai da outra.
		if (_matchOf.TryGetValue(peer, out HostedMatch before) && before != match) before.Drop(peer);
		_strangers.Remove(peer);
		_matchOf[peer] = match;
	}

	private void OnGone(long peer)
	{
		_strangers.Remove(peer);
		_early.Remove(peer);
		if (_matchOf.Remove(peer, out HostedMatch match))
		{
			NoteGone(match, match.AccountOf(peer));
			match.Drop(peer);
		}
	}

	private void Refuse(long peer, long account, string why, string matchKey = "", string note = "")
	{
		BinaryWriter w = NetWire.Begin(NetWire.Refused, out MemoryStream stream);
		w.Write(why);
		Send(peer, stream.ToArray(), true);
		GD.Print($"Refused at the door: {why} (account {account}).");
		Incident(KindRefused, Warning, matchKey, account, note == "" ? why : $"{why}: {note}");
	}

	// Sem garantia, mas em ordem: um estado velho que chega depois de um novo
	// é jogado fora pela ligação, em vez de voltar o boneco (21/11).
	public void Send(long peer, byte[] packet, bool reliable) =>
		_net.SendBytes(packet, (int)peer,
			reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.UnreliableOrdered);

	public void Kick(long peer)
	{
		_strangers.Remove(peer);
		_matchOf.Remove(peer);
		_peer.DisconnectPeer((int)peer);
	}

	/// <summary>"7777" ou "7777-7876" (vazio: a porta padrão).</summary>
	public static (int First, int Last) PortRange(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return (DefaultPort, DefaultPort);
		string[] parts = text.Split('-', 2, StringSplitOptions.TrimEntries);
		int first = int.TryParse(parts[0], out int a) ? a : DefaultPort;
		int last = parts.Length > 1 && int.TryParse(parts[1], out int b) ? Math.Max(b, first) : first;
		return (first, last);
	}

	/// <summary>A mesma assinatura do servidor (Passwords.Sign): HMAC-SHA256 em hexadecimal.</summary>
	public static string Sign(string secret, string text) =>
		Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text)));

	private static bool SameText(string a, string b) =>
		a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
