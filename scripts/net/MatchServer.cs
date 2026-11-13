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
/// (<c>godot --headless -- --servidor</c>), uma partida por vez.
///
/// O que ele faz, em ordem:
///
/// 1. bate ponto no servidor do Alcateia a cada dois segundos
///    (<c>POST /v1/fleet/heartbeat</c>) dizendo onde está;
/// 2. quando a resposta traz uma partida, abre o juiz
///    (<see cref="Referee"/>) com a escalação e as regras da sala;
/// 3. recebe os jogadores na porta UDP — cada um mostra o ingresso que o
///    saguão assinou; sem ingresso válido, fora;
/// 4. repassa o estado e os ataques de cada um aos outros, leva os
///    acertos até a vítima e anuncia os abates que o juiz aceita;
/// 5. no fim, manda o resultado (<c>POST /v1/matches</c>), solta todo
///    mundo e volta a esperar a próxima.
///
/// Não carrega mapa nem simula corpos: quem move cada corpo é a máquina
/// do dono (veja <see cref="NetMatch"/> e docs/decisoes/0008). Por isso é
/// leve — um processo aguenta a partida com folga.
///
/// Variáveis de ambiente: <c>ALCATEIA_API</c> (o servidor),
/// <c>ALCATEIA_FLEET_KEY</c> (o segredo da frota),
/// <c>ALCATEIA_PUBLIC_ADDRESS</c> (o endereço que os jogadores usam),
/// <c>ALCATEIA_GAME_PORT</c> (a porta UDP, 7777).
/// </summary>
public partial class MatchServer : Node
{
	public const int DefaultPort = 7777;
	private const double HeartbeatEvery = 2, ClockEvery = 0.5, HelloTimeout = 10, LingerAfterEnd = 8;

	private readonly string _id = OS.GetEnvironment("ALCATEIA_SERVER_ID") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N")[..12];
	private string _fleetKey, _address;
	private int _port;

	private SceneMultiplayer _net;
	private ENetMultiplayerPeer _peer;

	private Referee _referee;
	private string _matchKey;
	private readonly Dictionary<long, long> _accountOf = new();   // ligação → conta
	private readonly Dictionary<long, long> _peerOf = new();      // conta → ligação
	private readonly Dictionary<long, double> _strangers = new(); // ligação → há quanto tempo calada

	// Quem chegou antes de a partida chegar: o saguão avisa os jogadores
	// e o servidor de partida ao mesmo tempo, e o jogador pode ganhar a
	// corrida. Espera aqui até a próxima batida trazer a partida.
	private readonly Dictionary<long, (string Key, long Account, long Expires, string Ticket)> _early = new();

	private readonly List<long> _left = new();
	private double _heartbeat, _clock, _linger;
	private bool _beating, _reported;

	public override void _Ready()
	{
		Engine.MaxFps = 60;

		_fleetKey = OS.GetEnvironment("ALCATEIA_FLEET_KEY");
		_address = OS.GetEnvironment("ALCATEIA_PUBLIC_ADDRESS");
		_port = int.TryParse(OS.GetEnvironment("ALCATEIA_GAME_PORT"), out int port) ? port : DefaultPort;

		if (string.IsNullOrEmpty(_fleetKey))
		{
			GD.PrintErr("Servidor de partida: falta ALCATEIA_FLEET_KEY.");
			GetTree().Quit(1);
			return;
		}

		_peer = new ENetMultiplayerPeer();
		Error error = _peer.CreateServer(_port, 32);
		if (error != Error.Ok)
		{
			GD.PrintErr($"Servidor de partida: não consegui abrir a porta {_port}/UDP ({error}).");
			GetTree().Quit(1);
			return;
		}

		_net = new SceneMultiplayer { ServerRelay = false };
		GetTree().SetMultiplayer(_net, GetPath());
		_net.MultiplayerPeer = _peer;
		_net.PeerConnected += id => _strangers[id] = 0;
		_net.PeerDisconnected += OnGone;
		_net.PeerPacket += OnPacket;

		GD.Print($"Servidor de partida {_id} na porta {_port}/UDP, falando com {Backend.Url}");
	}

	public override void _Process(double delta)
	{
		if (_net == null) return;

		_heartbeat -= delta;
		if (_heartbeat <= 0 && !_beating)
		{
			_heartbeat = HeartbeatEvery;
			_ = Heartbeat();
		}

		// Quem conectou e não se apresentou não fica pendurado na porta.
		foreach (long peer in _strangers.Keys.ToList())
		{
			_strangers[peer] += delta;
			if (_strangers[peer] <= HelloTimeout) continue;

			if (_early.Remove(peer)) Refuse(peer, 0, "essa partida não está aqui");
			Kick(peer);
		}

		if (_left.Count > 0) AnnounceGone();
		if (_referee == null) return;

		RefPhase before = _referee.Phase;
		_referee.Tick(delta);

		_clock -= delta;
		if (_referee.Phase != before || _clock <= 0)
		{
			_clock = ClockEvery;
			Broadcast(ClockPacket(NetWire.Clock), reliable: _referee.Phase != before);
		}

		if (_referee.Phase != RefPhase.Ended) return;

		if (!_reported)
		{
			_reported = true;
			_linger = LingerAfterEnd;
			_ = Report();
		}

		_linger -= delta;
		if (_linger <= 0) Close();
	}

	// ------------------------------------------------------------ o servidor do Alcateia

	private async Task Heartbeat()
	{
		_beating = true;
		Backend.Reply reply = await Backend.AsGameServer("/v1/fleet/heartbeat", new JsonObject
		{
			["id"] = _id, ["address"] = _address ?? "", ["port"] = _port, ["players"] = _accountOf.Count
		}, _fleetKey);
		_beating = false;

		if (!reply.Ok)
		{
			if (reply.Status != 0) GD.PrintErr($"Servidor de partida: o servidor recusou a batida ({reply.Status} {reply.Code}).");
			return;
		}

		if (reply.Body?["assignment"] is JsonObject assignment && _referee == null) Open(assignment);
	}

	private void Open(JsonObject assignment)
	{
		JsonObject settings = assignment["settings"] as JsonObject ?? new JsonObject();
		var seats = new List<(long, string, int)>();
		foreach (JsonNode p in assignment["players"] as JsonArray ?? new JsonArray())
			seats.Add((p["accountId"].GetValue<long>(), p["name"].GetValue<string>(), p["team"].GetValue<int>()));

		double seconds = (settings["minutes"]?.GetValue<int>() ?? 10) * 60.0, countdown = 10;

		// Atalhos de teste, os mesmos da partida: -- --duracao=30 --contagem=2
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--duracao=") && double.TryParse(arg["--duracao=".Length..], out double d)) seconds = d;
			if (arg.StartsWith("--contagem=") && double.TryParse(arg["--contagem=".Length..], out double c)) countdown = c;
		}

		_matchKey = assignment["matchKey"].GetValue<string>();
		_referee = new Referee(seats,
			settings["scoreToWin"]?.GetValue<int>() ?? 40, seconds,
			settings["friendlyFire"]?.GetValue<bool>() ?? false, countdown);


		_reported = false;
		_clock = 0;

		foreach (var (peer, hello) in _early.ToList())
			Welcome(peer, hello.Key, hello.Account, hello.Expires, hello.Ticket);
		_early.Clear();

		GD.Print($"Partida {_matchKey}: {seats.Count} jogador(es), mapa {settings["map"]}, até {_referee.ScoreToWin} pontos.");
	}

	private async Task Report()
	{
		Referee r = _referee;
		var players = new JsonArray();
		foreach (Referee.Seat s in r.Seats.Where(s => s.EverConnected))
			players.Add(new JsonObject
			{
				["accountId"] = s.Id, ["kills"] = s.Kills, ["deaths"] = s.Deaths, ["assists"] = s.Assists,
				["headshots"] = s.Headshots, ["points"] = s.Points
			});

		var body = new JsonObject
		{
			["matchKey"] = _matchKey, ["serverId"] = _id,
			["scoreBlue"] = r.Score(Referee.Blue), ["scoreOrange"] = r.Score(Referee.Orange), ["players"] = players
		};

		GD.Print($"Partida {_matchKey} acabou: AZUL {r.Score(Referee.Blue)} x {r.Score(Referee.Orange)} LARANJA.");

		// O resultado é o que paga os jogadores: insiste um pouco.
		for (int attempt = 1; attempt <= 4; attempt++)
		{
			Backend.Reply reply = await Backend.AsGameServer("/v1/matches", (JsonObject)body.DeepClone(), _fleetKey);
			if (reply.Ok || reply.Status == 409) return;

			GD.PrintErr($"Servidor de partida: resultado não entregue (tentativa {attempt}: {reply.Status} {reply.Error})");
			await ToSignal(GetTree().CreateTimer(1.5 * attempt), SceneTreeTimer.SignalName.Timeout);
		}
	}

	/// <summary>Solta todo mundo e fica livre para a próxima partida.</summary>
	private void Close()
	{
		foreach (long peer in _accountOf.Keys.ToList()) Kick(peer);
		_accountOf.Clear();
		_peerOf.Clear();
		_referee = null;
		_matchKey = null;
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

			// Daqui para baixo, só quem já entrou.
			if (_referee == null || !_accountOf.TryGetValue(peer, out long account)) return;

			switch (type)
			{
				case NetWire.State:
					Broadcast(NetWire.Relay(packet, account), reliable: false, except: peer);
					break;

				case NetWire.Attack:
					Broadcast(NetWire.Relay(packet, account), except: peer);
					break;

				case NetWire.Hit:
					long target = r.ReadInt64();
					float amount = r.ReadSingle();
					if (!_referee.Hit(account, target, amount) || !_peerOf.TryGetValue(target, out long victim)) break;

					// Para a vítima vai o mesmo acerto, com o autor no lugar do alvo.
					byte[] forward = (byte[])packet.Clone();
					BitConverter.TryWriteBytes(new Span<byte>(forward, 1, 8), account);
					Send(victim, forward);
					break;

				case NetWire.Died:
					long killer = r.ReadInt64();
					byte cause = r.ReadByte();
					string weapon = r.ReadString();
					bool headshot = r.ReadBoolean();
					var assisters = new long[r.ReadByte()];
					for (int i = 0; i < assisters.Length; i++) assisters[i] = r.ReadInt64();

					if (_referee.Death(account, killer, headshot, assisters) is RefKill kill) Announce(kill, cause, weapon);
					break;
			}
		}
		catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException)
		{
			// Pacote torto: ignora. Quem insiste em mandar lixo só perde o próprio jogo.
		}
	}

	private void Welcome(long peer, string matchKey, long account, long expires, string ticket)
	{
		if (_referee == null)
		{
			_early[peer] = (matchKey, account, expires, ticket);
			_heartbeat = 0;
			return;
		}

		string refusal =
			matchKey != _matchKey ? "essa partida não está aqui"
			: expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "o ingresso venceu"
			: !SameText(ticket, Sign(_fleetKey, $"{matchKey}|{account}|{expires}")) ? "ingresso inválido"
			: !_referee.Join(account) ? "você não está nesta partida"
			: null;

		if (refusal != null)
		{
			Refuse(peer, account, refusal);
			return;
		}

		// Uma ligação por conta: a nova vale, a velha cai.
		if (_peerOf.TryGetValue(account, out long old) && old != peer)
		{
			_accountOf.Remove(old);
			Kick(old);
		}

		_strangers.Remove(peer);
		_accountOf[peer] = account;
		_peerOf[account] = peer;
		Send(peer, ClockPacket(NetWire.Welcome));
		GD.Print($"Entrou: {_referee.Find(account).Name} ({_referee.ConnectedCount} de {_referee.Seats.Count}).");
	}

	private void Refuse(long peer, long account, string why)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Refused, out MemoryStream stream);
		w.Write(why);
		Send(peer, stream.ToArray());
		GD.Print($"Recusado na porta: {why} (conta {account}).");
	}

	private void OnGone(long peer)
	{
		_strangers.Remove(peer);
		_early.Remove(peer);
		if (!_accountOf.Remove(peer, out long account)) return;

		if (_peerOf.TryGetValue(account, out long current) && current == peer) _peerOf.Remove(account);
		_referee?.Leave(account);

		// O aviso sai no quadro seguinte: quando dois saem juntos, o
		// segundo ainda consta aqui mas já não recebe nada.
		_left.Add(account);
	}

	private void AnnounceGone()
	{
		foreach (long account in _left)
		{
			BinaryWriter w = NetWire.Begin(NetWire.Gone, out MemoryStream stream);
			w.Write(account);
			Broadcast(stream.ToArray());
		}
		_left.Clear();
	}

	private void Announce(RefKill kill, byte cause, string weapon)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Kill, out MemoryStream stream);
		w.Write(kill.Killer);
		w.Write(kill.Victim);
		w.Write(cause);
		w.Write(weapon);
		w.Write((byte)kill.Assisters.Length);
		foreach (long id in kill.Assisters) w.Write(id);
		w.Write(_referee.Score(Referee.Blue));
		w.Write(_referee.Score(Referee.Orange));
		Broadcast(stream.ToArray());
	}

	private byte[] ClockPacket(byte type)
	{
		BinaryWriter w = NetWire.Begin(type, out MemoryStream stream);
		w.Write((byte)_referee.Phase);
		w.Write((float)_referee.CountdownLeft);
		w.Write((float)_referee.TimeLeft);
		w.Write(_referee.Score(Referee.Blue));
		w.Write(_referee.Score(Referee.Orange));
		return stream.ToArray();
	}

	private void Broadcast(byte[] packet, bool reliable = true, long except = 0)
	{
		foreach (long peer in _accountOf.Keys)
			if (peer != except) Send(peer, packet, reliable);
	}

	private void Send(long peer, byte[] packet, bool reliable = true) =>
		_net.SendBytes(packet, (int)peer,
			reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.Unreliable);

	private void Kick(long peer)
	{
		_strangers.Remove(peer);
		_peer.DisconnectPeer((int)peer);
	}

	/// <summary>A mesma assinatura do servidor (Passwords.Sign): HMAC-SHA256 em hexadecimal.</summary>
	public static string Sign(string secret, string text) =>
		Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text)));

	private static bool SameText(string a, string b) =>
		a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
