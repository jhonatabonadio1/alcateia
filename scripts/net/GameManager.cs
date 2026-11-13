using Godot;
using System.Collections.Generic;

/// <summary>
/// Rede e spawn de jogadores.
/// O host é a autoridade: ele decide quem entra e onde cada um nasce.
/// Degrau 1 — ainda sem tiro, sem dano, sem lag compensation.
/// </summary>
public partial class GameManager : Node
{
	public const int Port = 7777;
	public const int MaxPlayers = 8;

	[Export] public PackedScene PlayerScene;

	private Node3D _spawnRoot;
	private readonly List<Vector3> _spawnPoints = new()
	{
		new Vector3(0, 2, 0),
		new Vector3(6, 2, 0),
		new Vector3(-6, 2, 0),
		new Vector3(0, 2, 6),
		new Vector3(0, 2, -6),
		new Vector3(6, 2, 6),
		new Vector3(-6, 2, -6),
		new Vector3(6, 2, -6),
	};

	public override void _Ready()
	{
		_spawnRoot = GetNode<Node3D>("../Players");

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
	}

	public void HostGame()
	{
		var peer = new ENetMultiplayerPeer();
		Error err = peer.CreateServer(Port, MaxPlayers);

		if (err != Error.Ok)
		{
			GD.PrintErr($"Falha ao abrir servidor na porta {Port}: {err}");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"Servidor aberto na porta {Port}");

		// O host também joga.
		SpawnPlayer(1);
	}

	public void JoinGame(string address = "127.0.0.1")
	{
		var peer = new ENetMultiplayerPeer();
		Error err = peer.CreateClient(address, Port);

		if (err != Error.Ok)
		{
			GD.PrintErr($"Falha ao conectar em {address}:{Port}: {err}");
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"Conectando em {address}:{Port}...");
	}

	private void OnPeerConnected(long id)
	{
		// Só o servidor instancia jogadores. Os clientes recebem
		// pelo MultiplayerSpawner configurado na cena.
		if (!Multiplayer.IsServer()) return;

		GD.Print($"Peer {id} conectou");
		SpawnPlayer((int)id);
	}

	private void OnPeerDisconnected(long id)
	{
		if (!Multiplayer.IsServer()) return;

		GD.Print($"Peer {id} desconectou");

		Node player = _spawnRoot.GetNodeOrNull(id.ToString());
		player?.QueueFree();
	}

	private void SpawnPlayer(int peerId)
	{
		var player = PlayerScene.Instantiate<Player>();

		// O nome precisa ser o peer id: é assim que os clientes
		// identificam o nó replicado e aplicam a autoridade.
		player.Name = peerId.ToString();

		_spawnRoot.AddChild(player, true);

		// Autoridade definida depois do AddChild, senão o
		// MultiplayerSynchronizer nasce apontando para o servidor.
		player.SetMultiplayerAuthority(peerId);

		int index = _spawnRoot.GetChildCount() - 1;
		player.GlobalPosition = _spawnPoints[index % _spawnPoints.Count];
	}
}
