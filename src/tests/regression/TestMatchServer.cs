using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Um servidor de partida de verdade dentro do teste, para o jogo real
/// (<see cref="NetMatch"/>, com o jogador, a arma e a vida de sempre) jogar
/// online pela ligação ENet, sem Docker, sem o servidor do Alcateia e sem
/// ingresso: a mesma <see cref="HostedMatch"/> e o mesmo
/// <see cref="MatchWorld"/> do servidor de partida
/// (<see cref="MatchServer"/>), só que a porta é local e quem entra é
/// admitido sem conferir assinatura.
///
/// Um segundo "jogador" (a conta <see cref="OtherAccount"/>) fica do lado
/// do servidor: os pacotes dele são montados pelo teste e entregues a
/// <see cref="HostedMatch.Receive"/> como se tivessem chegado pela rede, e o
/// que o servidor manda a ele fica em <see cref="ToOther"/>.
/// </summary>
public partial class TestMatchServer : Node, HostedMatch.IMatchLink
{
	public const long MeAccount = 1, OtherAccount = 2, OtherPeer = 2002;

	public HostedMatch Match { get; private set; }
	public MatchWorld World { get; private set; }
	public int Port { get; private set; }

	/// <summary>O que o servidor mandou ao jogo de verdade, na ordem, por tipo de pacote.</summary>
	public readonly List<(double At, byte Type, byte[] Packet)> ToMe = new();

	/// <summary>O que o servidor mandou ao segundo jogador (que não tem jogo).</summary>
	public readonly List<(byte Type, byte[] Packet)> ToOther = new();

	/// <summary>Atraso de cada lado, em segundos (meia ida e volta): os pacotes
	/// do jogo chegam ao servidor e os do servidor ao jogo depois disto.</summary>
	public double Latency { get; set; }

	private readonly List<(double Due, long Peer, byte[] Packet, bool Reliable, bool In)> _delayed = new();
	private double _clock;

	private SceneMultiplayer _net;
	private ENetMultiplayerPeer _peer;
	private uint _tick;
	private long _mePeer;

	/// <summary>Abre a porta e a partida: <paramref name="authoritative"/>
	/// com mundo de física (o servidor move os corpos) ou só de repasse.</summary>
	public void Start(bool authoritative, int scoreToWin = 50, IEnumerable<OnlineMatch.Seat> extra = null)
	{
		Name = "ServidorDeTeste";
		var seats = new List<(long, string, int, long)> { (MeAccount, "Eu", Referee.Blue, 0L), (OtherAccount, "Outro", Referee.Orange, 0L) };
		// Lugares a mais (bots, por exemplo): os mesmos que o teste põe na escalação do jogo.
		if (extra != null) seats.AddRange(extra.Select(e => (e.Id, e.Name, e.Team, e.Owner)));
		var referee = new Referee(seats, scoreToWin, 600, false, countdown: 0);
		if (authoritative)
		{
			World = new MatchWorld("armazem_estendido");
			AddChild(World);
		}
		Match = new HostedMatch("teste", referee, this, World);

		for (Port = 24000; Port < 24200; Port++)
		{
			_peer = new ENetMultiplayerPeer();
			if (_peer.CreateServer(Port, 8) == Error.Ok) break;
		}
		_net = new SceneMultiplayer { ServerRelay = false };
		GetTree().SetMultiplayer(_net, GetPath());
		_net.MultiplayerPeer = _peer;
		_net.PeerPacket += OnPacket;
		_net.PeerDisconnected += id => { if (id == _mePeer) Match.Drop(id); };

		// O segundo jogador já está na partida, sem jogo.
		Match.Admit(OtherPeer, OtherAccount);
	}

	public override void _Process(double delta)
	{
		_clock += delta;
		for (int i = 0; i < _delayed.Count;)
		{
			var d = _delayed[i];
			if (d.Due > _clock) { i++; continue; }
			_delayed.RemoveAt(i);
			if (d.In) Deliver(d.Peer, d.Packet);
			else Transmit(d.Peer, d.Packet, d.Reliable);
		}
		Match?.Tick(delta);
	}

	public override void _PhysicsProcess(double delta) => Match?.PhysicsStep(++_tick, delta);

	public uint Tick => _tick;

	private void OnPacket(long peer, byte[] packet)
	{
		if (packet.Length == 0) return;
		if (Latency > 0) _delayed.Add((_clock + Latency, peer, packet, false, true));
		else Deliver(peer, packet);
	}

	private void Deliver(long peer, byte[] packet)
	{
		using BinaryReader r = NetWire.Open(packet);
		byte type = r.ReadByte();
		if (type == NetWire.Hello)
		{
			r.ReadString();
			long account = r.ReadInt64();
			r.ReadInt64();
			r.ReadString();
			if (Match.Admit(peer, account)) _mePeer = peer;
			return;
		}
		Match.Receive(peer, type, packet, r);
	}

	/// <summary>Um pacote do segundo jogador, como se tivesse chegado pela rede.</summary>
	public void FromOther(byte[] packet)
	{
		using BinaryReader r = NetWire.Open(packet);
		byte type = r.ReadByte();
		Match.Receive(OtherPeer, type, packet, r);
	}

	public void Send(long peer, byte[] packet, bool reliable)
	{
		if (packet.Length == 0) return;
		if (peer == OtherPeer)
		{
			ToOther.Add((packet[0], packet));
			return;
		}
		ToMe.Add((Time.GetTicksMsec() / 1000.0, packet[0], packet));
		if (Latency > 0) _delayed.Add((_clock + Latency, peer, packet, reliable, false));
		else Transmit(peer, packet, reliable);
	}

	private void Transmit(long peer, byte[] packet, bool reliable)
	{
		_net.SendBytes(packet, (int)peer,
			reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.UnreliableOrdered);
	}

	public void Kick(long peer)
	{
		if (peer != OtherPeer) _peer.DisconnectPeer((int)peer);
	}

	public void Report(HostedMatch match) { }

	/// <summary>Quantos pacotes deste tipo o servidor mandou ao jogo.</summary>
	public int CountToMe(byte type) => ToMe.Count(p => p.Type == type);

	public override void _ExitTree()
	{
		_peer?.Close();
		if (_net != null) GetTree().SetMultiplayer(null, GetPath());
	}
}
