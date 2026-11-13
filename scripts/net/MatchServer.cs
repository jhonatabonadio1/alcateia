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
/// <c>ALCATEIA_GAME_PORT</c> (a porta UDP, 7777),
/// <c>ALCATEIA_MATCHES</c> (quantas partidas ao mesmo tempo, 4).
/// </summary>
public partial class MatchServer : Node, HostedMatch.IMatchLink
{
	public const int DefaultPort = 7777, DefaultCapacity = 4;
	private const double HeartbeatEvery = 2, HelloTimeout = 10;

	private readonly string _id = OS.GetEnvironment("ALCATEIA_SERVER_ID") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N")[..12];
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

	// Quem chegou antes de a partida chegar: o saguão avisa os jogadores
	// e o servidor de partida ao mesmo tempo, e o jogador pode ganhar a
	// corrida. Espera aqui até a próxima batida trazer a partida.
	private readonly Dictionary<long, (string Key, long Account, long Expires, string Ticket)> _early = new();

	private double _heartbeat;
	private bool _beating;

	/// <summary>Atalhos de teste, os mesmos da partida: -- --duracao=30 --contagem=2</summary>
	private double? _seconds, _countdown;

	public override void _Ready()
	{
		Engine.MaxFps = 60;

		_fleetKey = OS.GetEnvironment("ALCATEIA_FLEET_KEY");
		_address = OS.GetEnvironment("ALCATEIA_PUBLIC_ADDRESS");
		_port = int.TryParse(OS.GetEnvironment("ALCATEIA_GAME_PORT"), out int port) ? port : DefaultPort;
		_capacity = int.TryParse(OS.GetEnvironment("ALCATEIA_MATCHES"), out int capacity) ? Math.Clamp(capacity, 1, 64) : DefaultCapacity;

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--duracao=") && double.TryParse(arg["--duracao=".Length..], out double d)) _seconds = d;
			if (arg.StartsWith("--contagem=") && double.TryParse(arg["--contagem=".Length..], out double c)) _countdown = c;
		}

		if (string.IsNullOrEmpty(_fleetKey))
		{
			GD.PrintErr("Servidor de partida: falta ALCATEIA_FLEET_KEY.");
			GetTree().Quit(1);
			return;
		}

		_peer = new ENetMultiplayerPeer();
		Error error = _peer.CreateServer(_port, 32 * _capacity);
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

		GD.Print($"Servidor de partida {_id} na porta {_port}/UDP, até {_capacity} partida(s), falando com {Backend.Url}");
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

		foreach (HostedMatch match in _matches.Values.ToList())
		{
			match.Tick(delta);
			if (match.Finished) Close(match);
		}
	}

	// ------------------------------------------------------------ o servidor do Alcateia

	private async Task Heartbeat()
	{
		_beating = true;
		var open = new JsonArray();
		foreach (string key in _matches.Keys.Union(_reporting)) open.Add(key);

		Backend.Reply reply = await Backend.AsGameServer("/v1/fleet/heartbeat", new JsonObject
		{
			["id"] = _id, ["address"] = _address ?? "", ["port"] = _port,
			["players"] = _matches.Values.Sum(m => m.Players), ["capacity"] = _capacity, ["matches"] = open
		}, _fleetKey);
		_beating = false;

		if (!reply.Ok)
		{
			if (reply.Status != 0) GD.PrintErr($"Servidor de partida: o servidor recusou a batida ({reply.Status} {reply.Code}).");
			return;
		}

		foreach (JsonNode node in reply.Body?["assignments"] as JsonArray ?? new JsonArray())
			if (node is JsonObject assignment) Open(assignment);
	}

	private void Open(JsonObject assignment)
	{
		string key = assignment["matchKey"]?.GetValue<string>();
		if (key == null || _matches.ContainsKey(key) || _matches.Count >= _capacity) return;

		HostedMatch match = HostedMatch.FromAssignment(assignment, this, _seconds, _countdown);
		_matches[key] = match;

		foreach (var (peer, hello) in _early.Where(e => e.Value.Key == key).ToList())
		{
			_early.Remove(peer);
			Welcome(peer, hello.Key, hello.Account, hello.Expires, hello.Ticket);
		}
	}

	/// <summary>A partida acabou: manda o resultado (o juiz já fechou o placar).</summary>
	public async void Report(HostedMatch match)
	{
		Referee r = match.Referee;
		JsonObject body = match.Result(_id);
		GD.Print($"Partida {match.Key} acabou: AZUL {r.Score(Referee.Blue)} x {r.Score(Referee.Orange)} LARANJA.");

		// O resultado é o que paga os jogadores: insiste um pouco.
		_reporting.Add(match.Key);
		try
		{
			for (int attempt = 1; attempt <= 4; attempt++)
			{
				Backend.Reply reply = await Backend.AsGameServer("/v1/matches", (JsonObject)body.DeepClone(), _fleetKey);
				if (reply.Ok || reply.Status == 409) return;

				GD.PrintErr($"Servidor de partida: resultado não entregue (tentativa {attempt}: {reply.Status} {reply.Error})");
				await ToSignal(GetTree().CreateTimer(1.5 * attempt), SceneTreeTimer.SignalName.Timeout);
			}
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
		_matches.Remove(match.Key);
		foreach (long peer in _matchOf.Where(e => e.Value == match).Select(e => e.Key).ToList()) _matchOf.Remove(peer);
		_heartbeat = 0;   // avisa já que o lugar vagou
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
		catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException)
		{
			// Pacote torto: ignora. Quem insiste em mandar lixo só perde o próprio jogo.
		}
	}

	private void Welcome(long peer, string matchKey, long account, long expires, string ticket)
	{
		string refusal =
			expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "o ingresso venceu"
			: !SameText(ticket, Sign(_fleetKey, $"{matchKey}|{account}|{expires}")) ? "ingresso inválido"
			: null;

		if (refusal == null && !_matches.ContainsKey(matchKey))
		{
			// A partida ainda não chegou: espera a próxima batida.
			_early[peer] = (matchKey, account, expires, ticket);
			_heartbeat = 0;
			return;
		}

		HostedMatch match = refusal == null ? _matches[matchKey] : null;
		if (match != null && !match.Admit(peer, account)) refusal = "você não está nesta partida";

		if (refusal != null)
		{
			Refuse(peer, account, refusal);
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
		if (_matchOf.Remove(peer, out HostedMatch match)) match.Drop(peer);
	}

	private void Refuse(long peer, long account, string why)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Refused, out MemoryStream stream);
		w.Write(why);
		Send(peer, stream.ToArray(), true);
		GD.Print($"Recusado na porta: {why} (conta {account}).");
	}

	public void Send(long peer, byte[] packet, bool reliable) =>
		_net.SendBytes(packet, (int)peer,
			reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.Unreliable);

	public void Kick(long peer)
	{
		_strangers.Remove(peer);
		_matchOf.Remove(peer);
		_peer.DisconnectPeer((int)peer);
	}

	/// <summary>A mesma assinatura do servidor (Passwords.Sign): HMAC-SHA256 em hexadecimal.</summary>
	public static string Sign(string secret, string text) =>
		Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text)));

	private static bool SameText(string a, string b) =>
		a != null && b != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
