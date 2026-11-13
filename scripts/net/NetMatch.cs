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
/// **Bots.** Os bots da sala são bots de verdade só na máquina do dono
/// da sala (<see cref="Own"/>): ela os move, manda o estado e os ataques
/// deles, aplica o dano que levam e conta quando morrem — fala por eles
/// como fala por si. Nas outras máquinas são bonecos, iguais aos dos
/// jogadores. Acerto entre dois corpos da mesma máquina (o jogador e um
/// bot dele, ou dois bots) é aplicado ali mesmo, e o servidor só é
/// avisado da autoria (<see cref="BeginLocalHit"/>) para o abate valer.
/// Se o dono sai, os bots saem com ele.
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
	private readonly Dictionary<long, Bot> _owned = new();   // os bots que esta máquina move
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

	/// <summary>Um bot que esta máquina move e pelo qual ela fala.</summary>
	public void Own(Bot bot)
	{
		long id = bot.NetId;
		_owned[id] = bot;

		var weapon = bot.GetNode<Weapon>("Weapon");
		weapon.Fired += () => SendBotAttack(bot, weapon.CurrentSlot);
		weapon.Thrown += () => SendBotAttack(bot, Slot.Grenade);

		// Morte sem autor (queda, a própria granada): o servidor fica sabendo.
		bot.GetNode<Health>("Health").Died += _ =>
		{
			if (!_hitInProgress && Joined) SendDied(id, 0, KillCause.Fall, "", false, new List<long>());
		};
	}

	/// <summary>Como o servidor conhece este corpo: a minha conta, a do
	/// dono do boneco, ou o número do bot. Zero se não é da partida.</summary>
	private long IdOf(Node body) =>
		body == null ? 0
		: body == _player ? OnlineMatch.AccountId
		: body is Bot bot ? bot.OnlineId
		: 0;

	/// <summary>O corpo desta máquina com este número: o jogador ou um bot dela.</summary>
	private Node LocalBody(long id) =>
		id == OnlineMatch.AccountId ? _player : _owned.TryGetValue(id, out Bot bot) ? bot : null;

	// ------------------------------------------------------------ acerto entre dois corpos daqui

	/// <summary>
	/// Antes de aplicar, aqui mesmo, o dano de um corpo desta máquina em
	/// outro (o jogador num bot dele, um bot no jogador, bot em bot):
	/// avisa o servidor da autoria — sem isso o juiz não aceita o abate —
	/// e marca que a morte que vier tem autor.
	/// </summary>
	public void BeginLocalHit(Node attacker, Health target, float amount)
	{
		long from = IdOf(attacker), to = IdOf(target?.GetParent());
		if (!Joined || from == 0 || to == 0 || from == to) return;

		_hitInProgress = true;
		BinaryWriter w = NetWire.Begin(NetWire.Touch, out MemoryStream stream);
		w.Write(from);
		w.Write(to);
		w.Write(amount);
		Send(stream);
	}

	public void EndLocalHit() => _hitInProgress = false;

	/// <summary>Um corpo desta máquina matou outro: a vítima conta ao
	/// servidor, que devolve o abate para todos (inclusive para cá).</summary>
	public void LocalKill(Health victim, Node killer, KillCause cause, string weapon, bool headshot)
	{
		long who = IdOf(victim.GetParent()), by = IdOf(killer);
		if (!Joined || who == 0) return;

		List<long> assisters = victim.TakeAssists(killer).Select(IdOf).Where(id => id != 0).ToList();
		SendDied(who, by, cause, weapon, headshot, assisters);
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

		foreach (var (id, bot) in _owned)
		{
			BinaryWriter bw = NetWire.Begin(NetWire.BotState, out MemoryStream botStream);
			bw.Write(id);
			bot.NetSnapshot().Write(bw);
			Send(botStream, reliable: false);
		}

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

	private void SendBotAttack(Bot bot, Slot slot)
	{
		if (!Joined) return;

		BinaryWriter w = NetWire.Begin(NetWire.BotAttack, out MemoryStream stream);
		w.Write(bot.NetId);
		w.Write((byte)slot);
		w.Write(bot.Rotation.Y);
		w.Write(bot.HeadPitch);
		Send(stream);
	}

	private void SendHit(long target, RemoteHit hit)
	{
		if (!Joined) return;

		// Quem acertou: eu, ou um bot que esta máquina move.
		long shooter = hit.Attacker is Bot { Remote: false } bot ? bot.NetId : 0;
		BinaryWriter w = NetWire.Begin(shooter != 0 ? NetWire.BotHit : NetWire.Hit, out MemoryStream stream);
		if (shooter != 0) w.Write(shooter);
		w.Write(target);
		w.Write(hit.Amount);
		w.Write(hit.Headshot);
		w.Write((byte)hit.Cause);
		w.Write(hit.Weapon);
		w.Write(hit.From);
		Send(stream);
	}

	/// <summary>Conta que <paramref name="victim"/> (eu ou um bot meu) morreu.</summary>
	private void SendDied(long victim, long killer, KillCause cause, string weapon, bool headshot, List<long> assisters)
	{
		bool me = victim == OnlineMatch.AccountId;
		BinaryWriter w = NetWire.Begin(me ? NetWire.Died : NetWire.BotDied, out MemoryStream stream);
		if (!me) w.Write(victim);
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
		if (!_hitInProgress && Joined) SendDied(OnlineMatch.AccountId, 0, KillCause.Fall, "", false, new List<long>());
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
					TakeHit(OnlineMatch.AccountId, r.ReadInt64(), r.ReadSingle(), r.ReadBoolean(), (KillCause)r.ReadByte(), r.ReadString(), r.ReadVector());
					break;

				case NetWire.HitBot:
					long shooter = r.ReadInt64();
					TakeHit(r.ReadInt64(), shooter, r.ReadSingle(), r.ReadBoolean(), (KillCause)r.ReadByte(), r.ReadString(), r.ReadVector());
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

	/// <summary>Alguém de outra máquina acertou um corpo daqui (eu, ou um
	/// bot meu): o dano é aplicado aqui, com as regras do corpo. Se ele
	/// cair, conto ao servidor quem foi.</summary>
	private void TakeHit(long victimId, long attackerId, float amount, bool headshot, KillCause cause, string weapon, Vector3 from)
	{
		Health health = LocalBody(victimId)?.GetNodeOrNull<Health>("Health");
		if (health == null || health.IsDead) return;

		_puppets.TryGetValue(attackerId, out Bot attacker);
		if (attacker != null) health.NoteAttacker(attacker);

		_hitInProgress = true;
		health.TakeDamage(amount, 0, from, headshot);
		_hitInProgress = false;

		if (!health.IsDead) return;

		List<long> assisters = health.TakeAssists(attacker).Select(IdOf).Where(id => id != 0).ToList();
		SendDied(victimId, attackerId, cause, weapon, headshot, assisters);
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

		// O WP do abate e da assistência é de quem fez, na máquina dele:
		// o meu, e o dos bots que esta máquina move.
		LocalBody(killer)?.GetNodeOrNull<FormController>("FormController")?.OnKill(cause == KillCause.Wolf);
		foreach (long id in assisters)
			if (id != killer) LocalBody(id)?.GetNodeOrNull<FormController>("FormController")?.OnAssist();

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
