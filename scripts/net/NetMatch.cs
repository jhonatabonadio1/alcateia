using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// A partida online, do lado de quem joga: a ligação com o servidor de
/// partida.
///
/// Cada jogo manda o próprio estado e o que fez; recebe o dos outros e o
/// encena nos bonecos (<see cref="Bot.Remote"/>). A divisão de quem
/// decide o quê:
///
/// - **o meu corpo** (posição, forma, vida) é meu: os outros só copiam;
/// - **o acerto** é de quem atirou: a arma dele acha o alvo e manda o
///   dano para o dono do corpo, que o aplica com as regras de sempre
///   (escudo, cabeça, proteção de renascimento);
/// - **o abate, o placar e o relógio** são do servidor
///   (<see cref="Referee"/>): a partida daqui só mostra o que ele diz.
///
/// Nasce dentro da cena da partida, criado pelo <see cref="MatchSetup"/>,
/// e some com ela.
/// </summary>
public partial class NetMatch : Node
{
	public static NetMatch Current { get; private set; }

	/// <summary>Entrou e foi aceito pelo servidor.</summary>
	public bool Joined { get; private set; }

	private SceneMultiplayer _net;
	private ENetMultiplayerPeer _peer;
	private Node3D _player;
	private Node3D _head;
	private CharacterBody3D _body;
	private Health _health;
	private Weapon _weapon;
	private FormController _form;
	private readonly Dictionary<long, Bot> _puppets = new();
	private double _sendTimer, _joinTimer;
	private bool _hitInProgress, _left;

	public override void _EnterTree() => Current = this;

	public override void _Ready()
	{
		_player = Groups.LocalPlayer(GetTree()) as Node3D;
		_body = _player as CharacterBody3D;
		_head = _player.GetNode<Node3D>("Head");
		_health = _player.GetNode<Health>("Health");
		_weapon = _player.GetNode<Weapon>("Weapon");
		_form = _player.GetNode<FormController>("FormController");

		_health.Died += OnLocalDied;
		_weapon.Fired += () => SendAttack();
		_weapon.Thrown += () => SendAttack(Slot.Grenade);

		_peer = new ENetMultiplayerPeer();
		Error error = _peer.CreateClient(OnlineMatch.Address, OnlineMatch.Port);
		if (error != Error.Ok)
		{
			Abandon($"Não consegui abrir a ligação com a partida ({error}).");
			return;
		}

		// Uma ligação só deste nó: o resto da cena continua offline, e o
		// jogador continua dono do próprio corpo.
		_net = new SceneMultiplayer();
		GetTree().SetMultiplayer(_net, GetPath());
		_net.MultiplayerPeer = _peer;
		_net.ConnectedToServer += SendHello;
		_net.ConnectionFailed += () => Abandon("Não consegui chegar ao servidor da partida.");
		_net.ServerDisconnected += () => Abandon("A ligação com a partida caiu.");
		_net.PeerPacket += OnPacket;
	}

	/// <summary>O boneco de um jogador escalado, registrado por quem monta a partida.</summary>
	public void Register(Bot puppet)
	{
		_puppets[puppet.RemoteId] = puppet;
		long id = puppet.RemoteId;
		puppet.GetNode<Health>("Health").Remote = hit => SendHit(id, hit);
	}

	public override void _ExitTree()
	{
		if (Current == this) Current = null;
		if (_net != null)
		{
			_peer.Close();
			_net.MultiplayerPeer = new OfflineMultiplayerPeer();
			GetTree().SetMultiplayer(null, GetPath());
		}
		OnlineMatch.Finish();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_net == null || _left) return;

		if (!Joined)
		{
			// O servidor pode estar subindo; mas não se espera para sempre.
			_joinTimer += delta;
			if (_joinTimer > 15) Abandon("O servidor da partida não respondeu.");
			return;
		}

		_sendTimer -= delta;
		if (_sendTimer > 0) return;
		_sendTimer = 1.0 / NetWire.StateRate;

		var state = new NetState
		{
			Position = _player.GlobalPosition,
			Velocity = _body.Velocity,
			Yaw = _player.Rotation.Y,
			Pitch = _head.Rotation.X,
			Form = (byte)_form.Current,
			Slot = (byte)_weapon.HumanSlot,
			Grounded = _body.IsOnFloor(),
			Dead = _health.IsDead,
			Hp = _health.Hp,
			MaxHp = _health.MaxHp,
			Shield = _health.Shield,
			MaxShield = _health.MaxShield,
			Primary = _weapon.PrimaryDef?.Id ?? "",
			Secondary = _weapon.SecondaryDef?.Id ?? ""
		};

		BinaryWriter w = NetWire.Begin(NetWire.State, out MemoryStream stream);
		state.Write(w);
		Send(stream, reliable: false);
	}

	// ------------------------------------------------------------ o que eu conto

	private void SendHello()
	{
		BinaryWriter w = NetWire.Begin(NetWire.Hello, out MemoryStream stream);
		w.Write(OnlineMatch.MatchKey);
		w.Write(OnlineMatch.AccountId);
		w.Write(OnlineMatch.Expires);
		w.Write(OnlineMatch.Ticket);
		Send(stream);
	}

	private void SendAttack(Slot? slot = null)
	{
		if (!Joined) return;

		BinaryWriter w = NetWire.Begin(NetWire.Attack, out MemoryStream stream);
		w.Write((byte)(slot ?? _weapon.CurrentSlot));
		w.Write(_player.Rotation.Y);
		w.Write(_head.Rotation.X);
		Send(stream);
	}

	private void SendHit(long target, RemoteHit hit)
	{
		if (!Joined) return;

		BinaryWriter w = NetWire.Begin(NetWire.Hit, out MemoryStream stream);
		w.Write(target);
		w.Write(hit.Amount);
		w.Write(hit.Headshot);
		w.Write((byte)hit.Cause);
		w.Write(hit.Weapon);
		w.Write(hit.From);
		Send(stream);
	}

	private void SendDied(long killer, KillCause cause, string weapon, bool headshot, List<long> assisters)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Died, out MemoryStream stream);
		w.Write(killer);
		w.Write((byte)cause);
		w.Write(weapon ?? "");
		w.Write(headshot);
		w.Write((byte)assisters.Count);
		foreach (long id in assisters) w.Write(id);
		Send(stream);
	}

	/// <summary>Morri sem ser por acerto de ninguém (queda, a própria
	/// granada): o servidor fica sabendo, e não dá o abate a ninguém.</summary>
	private void OnLocalDied(int killerId)
	{
		if (!_hitInProgress && Joined) SendDied(0, KillCause.Fall, "", false, new List<long>());
	}

	private void Send(MemoryStream stream, bool reliable = true) =>
		_net.SendBytes(stream.ToArray(), 1,
			reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.Unreliable);

	// ------------------------------------------------------------ o que me contam

	private void OnPacket(long from, byte[] packet)
	{
		if (packet.Length == 0 || _left) return;

		try
		{
			using BinaryReader r = NetWire.Open(packet);
			switch (r.ReadByte())
			{
				case NetWire.Welcome:
					Joined = true;
					ReadClock(r);
					break;

				case NetWire.Refused:
					Abandon("O servidor da partida recusou a entrada: " + r.ReadString());
					break;

				case NetWire.State:
					if (_puppets.TryGetValue(r.ReadInt64(), out Bot body)) Apply(body, NetState.Read(r));
					break;

				case NetWire.Attack:
					if (_puppets.TryGetValue(r.ReadInt64(), out Bot attacker))
						attacker.RemoteAttack((Slot)r.ReadByte(), r.ReadSingle(), r.ReadSingle());
					break;

				case NetWire.Hit:
					TakeHit(r.ReadInt64(), r.ReadSingle(), r.ReadBoolean(), (KillCause)r.ReadByte(), r.ReadString(), r.ReadVector());
					break;

				case NetWire.Kill:
					ReadKill(r);
					break;

				case NetWire.Clock:
					ReadClock(r);
					break;

				case NetWire.Gone:
					if (_puppets.TryGetValue(r.ReadInt64(), out Bot gone)) gone.RemotePresent(false);
					break;
			}
		}
		catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException)
		{
			GD.PushWarning($"NetMatch: pacote torto do servidor ({e.Message})");
		}
	}

	private static void Apply(Bot body, NetState s)
	{
		Form form = Enum.IsDefined(typeof(Form), (int)s.Form) ? (Form)s.Form : Form.Human;
		body.RemoteUpdate(s.Position, s.Velocity, s.Yaw, s.Pitch, s.Grounded, form, (Slot)s.Slot,
			s.Primary, s.Secondary, s.Hp, s.MaxHp, s.Shield, s.MaxShield, s.Dead);
	}

	/// <summary>Alguém me acertou: o dano é aplicado aqui, com as regras
	/// do meu corpo. Se eu cair, conto ao servidor quem foi.</summary>
	private void TakeHit(long attackerId, float amount, bool headshot, KillCause cause, string weapon, Vector3 from)
	{
		_puppets.TryGetValue(attackerId, out Bot attacker);
		if (_health.IsDead) return;

		if (attacker != null) _health.NoteAttacker(attacker);

		_hitInProgress = true;
		_health.TakeDamage(amount, 0, from, headshot);
		_hitInProgress = false;

		if (!_health.IsDead) return;

		List<long> assisters = _health.TakeAssists(attacker).OfType<Bot>().Select(b => b.RemoteId).ToList();
		SendDied(attackerId, cause, weapon, headshot, assisters);
	}

	private void ReadKill(BinaryReader r)
	{
		long killer = r.ReadInt64(), victim = r.ReadInt64();
		var cause = (KillCause)r.ReadByte();
		string weapon = r.ReadString();
		var assisters = new long[r.ReadByte()];
		for (int i = 0; i < assisters.Length; i++) assisters[i] = r.ReadInt64();
		int blue = r.ReadInt32(), orange = r.ReadInt32();

		(string killerName, int killerTeam) = Who(killer);
		(string victimName, int victimTeam) = Who(victim);
		KillFeed.Report(new Kill(killerName, killerTeam, victimName, victimTeam, cause, weapon,
			assisters.Select(id => Who(id).Name).ToArray()));

		// O WP do abate e da assistência é de quem fez, na máquina dele.
		if (killer == OnlineMatch.AccountId) _form.OnKill(cause == KillCause.Wolf);
		else if (assisters.Contains(OnlineMatch.AccountId)) _form.OnAssist();

		Match.Current?.NetScore(blue, orange);
	}

	private void ReadClock(BinaryReader r)
	{
		var phase = (RefPhase)r.ReadByte();
		float countdown = r.ReadSingle(), time = r.ReadSingle();
		int blue = r.ReadInt32(), orange = r.ReadInt32();
		Match.Current?.NetSync(phase, countdown, time, blue, orange);
	}

	/// <summary>O nome e o time de uma conta, como aparecem nesta máquina.</summary>
	private (string Name, int Team) Who(long id)
	{
		if (id == OnlineMatch.AccountId) return (_health.PublicName, _health.Team);
		OnlineMatch.Seat seat = OnlineMatch.Roster.FirstOrDefault(s => s.Id == id);
		return seat == null ? ("?", 0) : (seat.Name, seat.Team);
	}

	/// <summary>Sem servidor não há partida: volta para a sala com o recado.</summary>
	private void Abandon(string why)
	{
		if (_left) return;
		_left = true;

		// Partida já encerrada: o servidor fechou a porta, como combinado.
		if (Match.Current is { Phase: MatchPhase.Ended }) return;

		GD.Print("Partida online: " + why);
		OnlineMatch.Notice = why;
		Callable.From(() => Match.Current?.Leave(ReturnTarget.Room)).CallDeferred();
	}
}
