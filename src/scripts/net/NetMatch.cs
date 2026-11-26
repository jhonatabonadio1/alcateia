using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

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
/// **Partida autoritativa** (decisão 0011; o servidor diz no Welcome):
/// o meu corpo deixa de ser meu. Este jogo manda, a cada passo, o que
/// apertei (<see cref="InputCommand"/>, com os últimos quatro de folga) e
/// continua movendo o corpo na hora — a predição; quando o estado do
/// mundo chega, compara onde o servidor pôs o corpo com onde eu achava
/// que estava naquele comando e corrige a diferença
/// (<see cref="Prediction"/>), com o desenho escorregando até o lugar
/// (<see cref="VisualSmoothing.Absorb"/>). Os outros jogadores são
/// desenhados 100 ms no passado, entre dois estados que já chegaram
/// (<see cref="InterpolationBuffer"/>). A vida, o tiro e os bots ainda são
/// como no modo de repasse (a leva seguinte leva para o servidor).
///
/// Nasce dentro da cena da partida, criado pelo <see cref="MatchSetup"/>,
/// e some com ela.
/// </summary>
public partial class NetMatch : Node
{
	public static NetMatch Current { get; private set; }

	/// <summary>Quem montou a partida: cria os bonecos e os bots que chegam no meio dela.</summary>
	public MatchSetup Setup { get; init; }

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
	private bool _hitInProgress, _left, _superseded;

	// ---- partida autoritativa
	/// <summary>O servidor move os corpos (decisão 0011).</summary>
	public bool Authoritative { get; private set; }

	private Player _me;
	private readonly Dictionary<byte, long> _byIndex = new();
	private readonly Dictionary<long, InterpolationBuffer> _poses = new();
	private readonly InputCommand[] _recent = new InputCommand[InputCommand.Redundancy];
	private readonly Prediction _prediction = new();
	private readonly ServerClock _clock = new();
	private readonly byte[] _inputBuffer = new byte[2 + InputCommand.Redundancy * InputCommand.MaxSize];
	private uint _seq;
	private byte _epoch;
	private Vector3 _spawn;
	private bool _spawnConfirmed;
	private double _statusTimer;

	/// <summary>Quantas vezes o servidor corrigiu o meu corpo, e o tamanho
	/// da última diferença (metros): o que o teste e o painel de rede leem.</summary>
	public int Corrections => _prediction.Corrections;

	/// <summary>A soma e a maior correção do corpo, em metros (para medir).</summary>
	public float CorrectionSum => _prediction.ErrorSum;
	public float CorrectionMax => _prediction.ErrorMax;
	public float LastError => _prediction.LastError;

	/// <summary>Ida e volta até o servidor de partida (ms), do ENet; -1 sem ligação.</summary>
	public int PingMs => Server()?.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime) is double rtt ? (int)rtt : -1;

	/// <summary>Pacotes perdidos no caminho (0 a 1), do ENet; -1 sem ligação.</summary>
	public float PacketLoss => Server()?.GetStatistic(ENetPacketPeer.PeerStatistic.PacketLoss) is double loss
		? (float)(loss / 65536.0) : -1f;

	private ENetPacketPeer Server() =>
		_peer != null && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected ? _peer.GetPeer(1) : null;

	/// <summary>
	/// O relógio daqui para o relógio do servidor e o desenho dos outros: os
	/// passos de física já dados (26/11). Era o relógio de parede do sistema
	/// (Time.GetTicksUsec), lido em qualquer ponto do quadro: o instante em
	/// que um estado "chegou" e o instante do desenho saíam de relógios
	/// diferentes do passo em que o boneco anda, e o boneco tremia (na medida
	/// do teste FluidezAutoritativo, tranco em 352 de 360 quadros com a rede
	/// limpa). A física anda no tempo real (o Godot dá os passos que faltam),
	/// então isto é o tempo real, só que contado no passo.
	/// </summary>
	private static double LocalSeconds => Engine.GetPhysicsFrames() / (double)Engine.PhysicsTicksPerSecond;

	public override void _EnterTree() => Current = this;

	public override void _Ready()
	{
		_player = Groups.LocalPlayer(GetTree()) as Node3D;
		_body = _player as CharacterBody3D;
		_me = _player as Player;

		// Depois do passo do jogador: o comando que sai é o do passo que
		// ele acabou de dar, com a posição em que ficou.
		ProcessPhysicsPriority = 100;
		_head = _player.GetNode<Node3D>("Head");
		_health = _player.GetNode<Health>("Health");
		_weapon = _player.GetNode<Weapon>("Weapon");
		_form = _player.GetNode<FormController>("FormController");

		_health.Died += OnLocalDied;
		_health.Respawned += OnLocalRespawned;
		// O golpe do lobo vai quando começa (SendMove), não no impacto: o
		// boneco toca o golpe certo, na hora certa. A arma, quando atira.
		_weapon.Fired += () => { if (!_form.IsWolf) SendAttack(); };
		_weapon.Thrown += () => SendAttack(Slot.Grenade);
		_weapon.Stabbed += () => SendAttack(Slot.Knife, NetWire.KnifeStabMove);
		_weapon.WolfMoveStarted += move => SendMoveSoon(null, (WolfMove)move);

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
		var botForm = bot.GetNode<FormController>("FormController");
		weapon.Fired += () => { if (!botForm.IsWolf) SendBotAttack(bot, weapon.CurrentSlot); };
		weapon.Thrown += () => SendBotAttack(bot, Slot.Grenade);
		weapon.Stabbed += () => SendBotAttack(bot, Slot.Knife, NetWire.KnifeStabMove);
		weapon.WolfMoveStarted += move => SendMoveSoon(bot, (WolfMove)move);

		// Morte sem autor (queda, a própria granada): o servidor fica sabendo,
		// com o que matou (o feed diz "de granada").
		var botHealth = bot.GetNode<Health>("Health");
		botHealth.Died += _ =>
		{
			if (!_hitInProgress && Joined) SendDied(id, 0, botHealth.LastSelfCause, botHealth.LastSelfWeapon, false, new List<long>());
		};
	}

	/// <summary>Como o servidor conhece este corpo: a minha conta, a do
	/// dono do boneco, ou o número do bot. Zero se não é da partida.</summary>
	private long IdOf(Node body) =>
		body == null ? 0
		: body == _player ? OnlineMatch.AccountId
		: body is Bot bot ? bot.OnlineId
		: 0;

	/// <summary>O lugar de um corpo na escalação do servidor (o número que
	/// vai no estado do mundo), para o alvo travado do especial.</summary>
	private byte SeatOf(Node3D body)
	{
		long id = IdOf(body);
		if (id != 0)
			foreach (var (index, account) in _byIndex)
				if (account == id) return index;
		return NetCommands.NoSeat;
	}

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
		Player.NetQuantize = false;
		if (_net != null)
		{
			_peer.Close();
			_net.MultiplayerPeer = new OfflineMultiplayerPeer();
			GetTree().SetMultiplayer(null, GetPath());
		}
		// Trocada por uma nova (o mestre reiniciou): a online continua.
		if (!_superseded) OnlineMatch.Finish();
	}

	/// <summary>
	/// O saguão mandou outra partida (o mestre reiniciou): esta para de falar
	/// com o servidor dela agora — o resultado dela não vale nada — e não
	/// volta para a sala nem encerra a partida online quando a cena sair: a
	/// tela de carregamento já está levando para a nova.
	/// </summary>
	public void Supersede()
	{
		if (_superseded) return;
		_superseded = true;
		_left = true;
		GD.Print("Online match: the room host restarted; moving to the new one.");
		_peer?.Close();
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

		if (!Authoritative) UpdateRelayedPuppets();
		if (Authoritative)
		{
			SendCommand();
			UpdatePuppets();
			_statusTimer -= delta;
			if (_statusTimer <= 0)
			{
				_statusTimer = 0.1;
				SendStatus();
			}
		}

		_sendTimer -= delta;
		if (_sendTimer > 0) return;
		_sendTimer = 1.0 / NetWire.StateRate;

		foreach (var (id, bot) in _owned)
		{
			BinaryWriter bw = NetWire.Begin(NetWire.BotState, out MemoryStream botStream);
			bw.Write(id);
			NetState botState = bot.NetSnapshot();
			botState.HasTime = true;
			botState.Time = LocalSeconds;
			botState.Write(bw);
			Send(botStream, reliable: false);
		}

		var state = new NetState
		{
			HasTime = true,
			Time = LocalSeconds,
			Position = _player.GlobalPosition,
			Velocity = _body.Velocity,
			Yaw = _player.Rotation.Y,
			Pitch = _head.Rotation.X,
			Form = (byte)_form.Current,
			Slot = (byte)_weapon.HumanSlot,
			Grounded = _body.IsOnFloor(),
			WallRunning = MyWall(),
			Sprinting = _me is { IsSprinting: true },
			Crouching = _me is { IsCrouching: true },
			Howling = _me != null && _me.HowlProgress > 0f,
			Quiet = _me != null && (_me.IsCrouching || _me.IsWalking),
			GrenadeReady = _weapon.IsGrenadeReady,
			Aiming = _weapon.IsAiming,
			Dead = _health.IsDead,
			Hp = _health.Hp,
			MaxHp = _health.MaxHp,
			Shield = _health.Shield,
			MaxShield = _health.MaxShield,
			Primary = _weapon.PrimaryDef?.Id ?? "",
			Secondary = _weapon.SecondaryDef?.Id ?? "",
			HasGear = true,
			Ammo = (short)_weapon.AmmoInMag,
			Reloading = _weapon.IsReloading,
			Grenades = (byte)Mathf.Clamp(_weapon.Grenades, 0, 255),
			Wp = _form.Wp,
			Charge = _weapon.SpecialCharge
		};

		if (Authoritative) return;

		BinaryWriter w = NetWire.Begin(NetWire.State, out MemoryStream stream);
		state.Write(w);
		Send(stream, reliable: false);
	}

	// ------------------------------------------------------------ partida autoritativa

	/// <summary>O comando do passo que o jogador acabou de dar, com os
	/// últimos de folga. Guarda onde o corpo ficou, para comparar quando o
	/// servidor responder.</summary>
	private void SendCommand()
	{
		if (_me == null) return;

		_seq++;
		InputCommand c = NetCommands.From(_me.Intent, _seq, _me.Rotation.Y, _me.HeadPitch);
		c.Form = (byte)_form.Current;
		c.HeldSlot = (byte)_weapon.HumanSlot;
		c.Epoch = _epoch;
		c.Dead = _health.IsDead;
		c.Frozen = _me.Frozen;
		c.OnWall = MyWall();
		if (!_spawnConfirmed)
		{
			c.HasSpawn = true;
			c.Spawn = _spawn.ToNumerics();
		}
		NetCommands.Special(ref c, _weapon, SeatOf);

		_recent[_seq % InputCommand.Redundancy] = c;
		_prediction.Record(_seq, _me.GlobalPosition.ToNumerics(), _me.Velocity.ToNumerics());
		_wallAt[_seq % (uint)_wallAt.Length] = (_seq, _me.IsWallRunning);

		_net.SendBytes(NetCommands.InputPacket(_recent, _seq, _inputBuffer), 1, MultiplayerPeer.TransferModeEnum.Unreliable);
	}

	/// <summary>Vida, escudo e armas: os outros desenham, o servidor copia
	/// (a vida ainda é do jogo nesta leva).</summary>
	private void SendStatus()
	{
		BinaryWriter w = NetWire.Begin(NetWire.Status, out MemoryStream stream);
		w.Write(_health.Hp);
		w.Write(_health.MaxHp);
		w.Write(_health.Shield);
		w.Write(_health.MaxShield);
		w.Write(_health.IsDead);
		w.Write(_weapon.PrimaryDef?.Id ?? "");
		w.Write(_weapon.SecondaryDef?.Id ?? "");
		// O que o HUD mostra (19/11): quem te assiste no espectador vê o
		// mesmo. No fim, para quem lê só o começo continuar lendo.
		w.Write((short)_weapon.AmmoInMag);
		w.Write(_weapon.IsReloading);
		w.Write((byte)Mathf.Clamp(_weapon.Grenades, 0, 255));
		w.Write(_form.Wp);
		w.Write(_weapon.SpecialCharge);
		// A guarda do especial do lobo (decisão 0016): o servidor aplica o
		// dano e precisa saber que ela está ligada.
		w.Write(_weapon.SpecialGuarded);
		Send(stream, reliable: false);
	}

	/// <summary>A classe de lobo e as melhorias: o servidor precisa delas
	/// para mover o lobo como este jogo move.</summary>
	private void SendLoadout()
	{
		BinaryWriter w = NetWire.Begin(NetWire.Loadout, out MemoryStream stream);
		w.Write(Profile.Breed ?? "");
		List<string> tattoos = Profile.OwnedTattoos();
		w.Write((byte)tattoos.Count);
		foreach (string id in tattoos) w.Write(id);
		Send(stream);
	}

	/// <summary>Renasci: vida nova. O servidor põe o corpo dele onde eu
	/// nasci quando o primeiro comando desta vida chegar.</summary>
	private void OnLocalRespawned()
	{
		if (!Authoritative) return;
		NewLife(_player.GlobalPosition);
	}

	/// <summary>Põe o corpo noutro lugar de uma vez (o robô de teste). Na
	/// autoritativa vale como renascer ali (nesta leva o servidor confia).</summary>
	public void Teleport(Vector3 at)
	{
		_player.GlobalPosition = at;
		if (Authoritative) NewLife(at);
	}

	private void NewLife(Vector3 at)
	{
		_epoch++;
		_spawn = at;
		_spawnConfirmed = false;
		_prediction.Clear();
	}

	private void ReadWelcome(BinaryReader r)
	{
		ReadClock(r);
		if (r.BaseStream.Position >= r.BaseStream.Length || r.ReadByte() != NetWire.ModeAuthoritative) return;

		r.ReadByte();   // passos por segundo (60, NetRates.TickRate)
		for (int n = r.ReadByte(); n > 0; n--) _byIndex[r.ReadByte()] = r.ReadInt64();

		Authoritative = true;
		Player.NetQuantize = true;
		_spawn = _player.GlobalPosition;
		_spawnConfirmed = false;
		SendLoadout();
		GD.Print("Authoritative online match: the server moves the bodies.");
	}

	/// <summary>O estado do mundo: corrige o meu corpo e guarda a pose dos outros.</summary>
	private void ReadSnapshot(byte[] packet)
	{
		var r = new PacketReader(packet, 1);
		int count = SnapshotWire.ReadHeader(ref r, out uint tick, out uint ack);
		_clock.Observe(tick, LocalSeconds);

		for (int i = 0; i < count; i++)
		{
			EntityState e = EntityState.Read(ref r);
			if (!_byIndex.TryGetValue(e.Index, out long id)) continue;

			if (id == OnlineMatch.AccountId)
			{
				Reconcile(e, ack);
				continue;
			}

			if (!_poses.TryGetValue(id, out InterpolationBuffer poses)) _poses[id] = poses = new InterpolationBuffer();
			poses.Add(new NetPose
			{
				Time = tick * NetRates.TickSeconds,
				Position = e.Position,
				Velocity = e.Velocity,
				Yaw = Quant.Yaw(e.Yaw),
				Pitch = Quant.Pitch(e.Pitch),
				Flags = e.Flags,
				Form = e.Form,
				Slot = e.Slot,
				Epoch = e.Epoch
			});
		}
	}

	/// <summary>O servidor disse onde o meu corpo ficou depois do comando
	/// <paramref name="ack"/>. Diferente do previsto: o corpo de agora anda
	/// a diferença, e o desenho escorrega até lá.</summary>
	private void Reconcile(EntityState e, uint ack)
	{
		if (e.Epoch != _epoch || !e.Has(EntityFlags.Present)) return;
		_spawnConfirmed = true;
		if (_health.IsDead || ack == 0) return;

		SyncWall(e.Has(EntityFlags.WallRunning), ack);

		// Um estado que chega depois de um mais novo (o mesmo comando ou um
		// mais velho) já está corrigido: corrigir de novo puxaria o corpo pela
		// mesma diferença duas vezes, e o seguinte o devolveria (tremor, 26/11).
		if (ack <= _reconciledAck && _reconciledEpoch == _epoch) return;
		_reconciledAck = ack;
		_reconciledEpoch = _epoch;

		System.Numerics.Vector3 fix = _prediction.Reconcile(ack, _seq, e.Position);
		if (fix == System.Numerics.Vector3.Zero) return;

		Vector3 moved = fix.ToGodot();
		_player.GlobalPosition += moved;
		if (moved.Length() < Prediction.SnapDistance)
			_player.GetNodeOrNull<VisualSmoothing>("VisualSmoothing")?.Absorb(moved);
	}

	/// <summary>O último comando cujo resultado o servidor já contou (e em que vida).</summary>
	private uint _reconciledAck;
	private byte _reconciledEpoch;

	/// <summary>Se este jogo estava na parede em cada passo mandado (para
	/// comparar com o que o servidor diz do mesmo passo).</summary>
	private readonly (uint Seq, bool Wall)[] _wallAt = new (uint, bool)[128];

	/// <summary>O passo em que o jogo voltou para a parede por ordem do
	/// servidor: os estados de antes dele já não dizem nada.</summary>
	private uint _wallSyncSeq;

	/// <summary>
	/// O servidor diz que, no passo <paramref name="ack"/>, o corpo estava na
	/// parede e este jogo, no mesmo passo, não: a posição sozinha não
	/// resolve (o jogo cai, o servidor corre, e a correção arrasta o lobo
	/// de pé pela parede). O jogo volta para ela
	/// (<see cref="Player.ResumeWallFromServer"/>).
	/// </summary>
	private void SyncWall(bool serverWall, uint ack)
	{
		if (_me == null || !serverWall || ack <= _wallSyncSeq) return;
		var (seq, clientWall) = _wallAt[ack % (uint)_wallAt.Length];
		if (seq != ack || clientWall) return;
		if (_me.ResumeWallFromServer()) _wallSyncSeq = _seq;
	}

	/// <summary>Estou na parede? E o log da troca (online, para achar onde
	/// a parede se perde entre as máquinas).</summary>
	private bool MyWall()
	{
		bool on = _player is Player { IsWallRunning: true };
		if (on != _myWallWas)
		{
			_myWallWas = on;
			GD.Print($"wall net: me {(on ? "on" : "off")} the wall");
		}
		return on;
	}

	private bool _myWallWas;

	/// <summary>
	/// Partida de repasse: o caminho de cada boneco cujo dono conta o instante
	/// de cada estado (<see cref="NetState.Time"/>, 26/11). O boneco é desenhado
	/// entre dois estados, um pouco no passado, no relógio de quem mandou —
	/// como na partida autoritativa —, em vez de perseguir a última notícia e
	/// se adiantar pela velocidade: com a rede irregular, a notícia que chegava
	/// mais tarde puxava o boneco para trás.
	/// </summary>
	private sealed class Relayed
	{
		public readonly InterpolationBuffer Poses = new();
		public readonly ServerClock Clock = new();
		public byte Epoch;
		public bool WasDead, HasLast;
		public Vector3 Last;
	}

	private readonly Dictionary<long, Relayed> _relayed = new();

	/// <summary>Andar mais que isto entre dois estados é teleporte (renasceu):
	/// o boneco pula em vez de deslizar pelo mapa.</summary>
	private const float RelayTeleport = 4f;

	private void Track(long id, in NetState s)
	{
		if (!_relayed.TryGetValue(id, out Relayed track)) _relayed[id] = track = new Relayed();
		if (s.Dead) track.WasDead = true;
		else if (track.WasDead || (track.HasLast && track.Last.DistanceTo(s.Position) > RelayTeleport))
		{
			track.Epoch++;
			track.WasDead = false;
		}
		track.Last = s.Position;
		track.HasLast = true;
		track.Clock.Observe(s.Time, LocalSeconds);
		track.Poses.Add(new NetPose
		{
			Time = s.Time,
			Position = s.Position.ToNumerics(),
			Velocity = s.Velocity.ToNumerics(),
			Yaw = s.Yaw,
			Pitch = s.Pitch,
			Flags = (s.Grounded ? EntityFlags.Grounded : 0) | (s.Dead ? EntityFlags.Dead : 0),
			Epoch = track.Epoch
		});
	}

	private void UpdateRelayedPuppets()
	{
		foreach (var (id, track) in _relayed)
		{
			if (!_puppets.TryGetValue(id, out Bot puppet) || !track.Clock.Known) continue;
			if (!track.Poses.Sample(track.Clock.RenderTime(LocalSeconds), out NetPose p)) continue;
			puppet.RemoteInterpolated(p.Position.ToGodot(), p.Velocity.ToGodot(), p.Yaw, p.Pitch, p.Has(EntityFlags.Grounded));
		}
	}

	/// <summary>Os outros, um pouco no passado, entre dois estados que já chegaram.</summary>
	private void UpdatePuppets()
	{
		if (!_clock.Known) return;
		double renderTime = _clock.RenderTime(LocalSeconds);

		foreach (var (id, poses) in _poses)
		{
			if (!_puppets.TryGetValue(id, out Bot puppet) || !poses.Sample(renderTime, out NetPose p)) continue;
			if (!p.Has(EntityFlags.Present)) continue;

			Form form = Enum.IsDefined(typeof(Form), (int)p.Form) ? (Form)p.Form : Form.Human;
			puppet.RemoteSnapshot(p.Position.ToGodot(), p.Velocity.ToGodot(), p.Yaw, p.Pitch,
				p.Has(EntityFlags.Grounded), form, (Slot)p.Slot);
			// O servidor já mandava (EntityFlags.WallRunning); o boneco não usava.
			puppet.RemoteWallRun = p.Has(EntityFlags.WallRunning);
			// Correr de quatro e agachar também já vinham; rugir, desde 21/11.
			bool crouching = p.Has(EntityFlags.Crouching);
			puppet.RemoteMotion(p.Has(EntityFlags.Sprinting), crouching, p.Has(EntityFlags.Howling), crouching, false,
				p.Has(EntityFlags.Aiming));
		}
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

	private void SendAttack(Slot? slot = null, byte move = 0)
	{
		if (!Joined) return;

		BinaryWriter w = NetWire.Begin(NetWire.Attack, out MemoryStream stream);
		w.Write((byte)(slot ?? _weapon.CurrentSlot));
		w.Write(_player.Rotation.Y);
		w.Write(_head.Rotation.X);
		if (move != 0) w.Write(move);
		Send(stream);
	}

	/// <summary>
	/// O golpe do lobo começou (o meu ou o de um bot meu): vai no fim do
	/// quadro, quando a arma já calculou o voo do salto (o sinal sai antes,
	/// no meio da conta). O uivo vai no estado, não aqui.
	/// </summary>
	private void SendMoveSoon(Bot bot, WolfMove move)
	{
		if (move == WolfMove.Howl) return;
		Callable.From(() => SendMove(bot, move)).CallDeferred();
	}

	/// <summary>Um <c>Attack</c> de lobo com o golpe no fim (NetWire): o
	/// boneco toca exatamente este golpe — garrada de um braço ou do outro,
	/// o salto do especial ou o bote.</summary>
	private void SendMove(Bot bot, WolfMove move)
	{
		if (!Joined || _left) return;
		if (bot != null && !IsInstanceValid(bot)) return;

		Weapon weapon = bot != null ? bot.GetNode<Weapon>("Weapon") : _weapon;
		float a = 0f, b = 0f;
		if (move == WolfMove.Pounce)
		{
			a = weapon.PounceSkip;
			b = weapon.PounceFlightRate;
		}
		else if (move == WolfMove.Leap) a = weapon.DiveEta;

		BinaryWriter w = NetWire.Begin(bot != null ? NetWire.BotAttack : NetWire.Attack, out MemoryStream stream);
		if (bot != null) w.Write(bot.NetId);
		w.Write((byte)Slot.Wolf);
		w.Write(bot != null ? bot.Rotation.Y : _player.Rotation.Y);
		w.Write(bot != null ? bot.HeadPitch : _head.Rotation.X);
		w.Write((byte)((int)move + 1));
		w.Write(a);
		w.Write(b);
		Send(stream);
	}

	/// <summary>O call de rádio do jogador desta máquina, para a partida
	/// repassar (<see cref="Radio"/>).</summary>
	public void SendRadio(int call)
	{
		if (!Joined) return;

		BinaryWriter w = NetWire.Begin(NetWire.Radio, out MemoryStream stream);
		w.Write((byte)call);
		Send(stream);
	}

	private void SendBotAttack(Bot bot, Slot slot, byte move = 0)
	{
		if (!Joined) return;

		BinaryWriter w = NetWire.Begin(NetWire.BotAttack, out MemoryStream stream);
		w.Write(bot.NetId);
		w.Write((byte)slot);
		w.Write(bot.Rotation.Y);
		w.Write(bot.HeadPitch);
		if (move != 0) w.Write(move);
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
		// Para o servidor conferir (decisão 0016): o comando em que o tiro
		// saiu (o deste passo, que sai logo depois) e o instante em que os
		// outros estavam na minha tela. O servidor de repasse ignora.
		w.Write(_seq + 1);
		w.Write(_clock.Known ? _clock.RenderTime(LocalSeconds) * NetRates.TickRate : double.NaN);
		Send(stream);
	}

	/// <summary>Só o robô do teste de ponta a ponta: um acerto inventado
	/// (dano, cabeça e origem à escolha), para conferir que o servidor
	/// autoritativo recusa (decisão 0016).</summary>
	public void SendForgedHit(long target, float amount, Vector3 from) =>
		SendHit(target, new RemoteHit(amount, from, true, KillCause.Headshot, _weapon.Held?.Id ?? "", null));

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
	/// granada): o servidor fica sabendo, e não dá o abate a ninguém. Vai o
	/// que matou: até 25/11 ia sempre "queda", e o servidor nem anunciava
	/// (o feed não mostrava a morte).</summary>
	private void OnLocalDied(int killerId)
	{
		if (!_hitInProgress && Joined)
			SendDied(OnlineMatch.AccountId, 0, _health.LastSelfCause, _health.LastSelfWeapon, false, new List<long>());
	}

	// Sem garantia, mas em ordem: o estado velho que chega depois de um novo
	// é jogado fora (21/11). O comando da partida autoritativa (Input) não
	// passa por aqui: ele leva os últimos de folga e o servidor ordena.
	private void Send(MemoryStream stream, bool reliable = true) =>
		_net.SendBytes(stream.ToArray(), 1,
			reliable ? MultiplayerPeer.TransferModeEnum.Reliable : MultiplayerPeer.TransferModeEnum.UnreliableOrdered);

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
					ReadWelcome(r);
					break;

				case NetWire.Snapshot:
					if (Authoritative) ReadSnapshot(packet);
					break;

				case NetWire.Status:
					if (_puppets.TryGetValue(r.ReadInt64(), out Bot statusOf))
					{
						statusOf.RemoteStatus(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadBoolean(), r.ReadString(), r.ReadString());
						if (r.BaseStream.Length - r.BaseStream.Position >= 2 + 1 + 1 + 4 + 4)
							statusOf.RemoteGear(r.ReadInt16(), r.ReadBoolean(), r.ReadByte(), r.ReadSingle(), r.ReadSingle());
					}
					break;

				case NetWire.Refused:
					Abandon("O servidor da partida recusou a entrada: " + r.ReadString());
					break;

				case NetWire.State:
					long stateOf = r.ReadInt64();
					if (_puppets.TryGetValue(stateOf, out Bot body))
					{
						NetState st = NetState.Read(r);
						if (st.HasTime) Track(stateOf, st);
						Apply(body, st);
					}
					break;

				case NetWire.Attack:
					if (_puppets.TryGetValue(r.ReadInt64(), out Bot attacker))
					{
						var attackSlot = (Slot)r.ReadByte();
						float attackYaw = r.ReadSingle(), attackPitch = r.ReadSingle();
						// O golpe do lobo, no fim (desde 21/11); sem ele, o ataque de antes.
						int move = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : 0;
						float moveA = 0f, moveB = 0f;
						if (move > 0 && r.BaseStream.Length - r.BaseStream.Position >= 8)
						{
							moveA = r.ReadSingle();
							moveB = r.ReadSingle();
						}
						attacker.RemoteAttack(attackSlot, attackYaw, attackPitch, move, moveA, moveB);
					}
					break;

				case NetWire.Radio:
					if (_puppets.TryGetValue(r.ReadInt64(), out Bot caller)) Radio.Receive(caller, r.ReadByte());
					break;

				case NetWire.Hit:
					TakeHit(OnlineMatch.AccountId, r.ReadInt64(), r.ReadSingle(), r.ReadBoolean(), (KillCause)r.ReadByte(), r.ReadString(), r.ReadVector());
					break;

				case NetWire.Damage:
					TakeServerDamage(r);
					break;

				case NetWire.Vitals:
					float vitalHp = r.ReadSingle(), vitalShield = r.ReadSingle();
					if (!r.ReadBoolean()) _health.CorrectDown(vitalHp, vitalShield);
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

				case NetWire.Fire:
					ReadFire(r);
					break;

				case NetWire.Seat:
					ReadSeat(r);
					break;

				case NetWire.BotOwner:
					ReadBotOwner(r);
					break;

				case NetWire.Gone:
					long goneId = r.ReadInt64();
					if (_puppets.TryGetValue(goneId, out Bot gone)) gone.RemotePresent(false);
					_poses.Remove(goneId);
					_relayed.Remove(goneId);
					break;
			}
		}
		catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException or IndexOutOfRangeException)
		{
			GD.PushWarning($"NetMatch: malformed packet from the server ({e.Message})");
		}
	}

	/// <summary>
	/// Alguém entrou com a partida em curso (Jhon, 24/11: "posso entrar nela
	/// novamente caso tenha vaga"), ou voltou: ganha um boneco aqui, se ainda
	/// não tem, e entra no placar. A patente e a foto vêm da sala do saguão.
	/// </summary>
	private void ReadSeat(BinaryReader r)
	{
		long id = r.ReadInt64();
		string name = r.ReadString();
		int team = r.ReadByte();
		long owner = r.ReadInt64();
		byte index = r.ReadByte();
		if (Authoritative && id > 0) _byIndex[index] = id;
		if (id == OnlineMatch.AccountId || _owned.ContainsKey(id) || Setup == null) return;

		// Quem voltou escolheu o outro time na sala (25/11): o boneco de antes
		// sai de cena, e entra um novo, na cor e no lado de agora.
		if (_puppets.TryGetValue(id, out Bot old))
		{
			if (!IsInstanceValid(old) || old.Team == team) return;
			_puppets.Remove(id);
			_poses.Remove(id);
			_relayed.Remove(id);
			Retire(old);
		}

		JsonNode member = (LobbyLink.InRoom ? LobbyLink.Instance.Room["members"] as JsonArray : null)?
			.FirstOrDefault(m => m?["id"]?.GetValue<long>() == id);
		var seat = new OnlineMatch.Seat(id, name, team, member?["xp"]?.GetValue<long>() ?? 0, owner, member?["photo"]?.GetValue<string>());
		OnlineMatch.Roster.RemoveAll(s => s.Id == id);
		OnlineMatch.Roster.Add(seat);

		Bot puppet = Setup.AddPuppet(seat);
		Register(puppet);
		Match.Current?.Enlist();
		GD.Print($"Online match: {name} joined the match in progress.");
	}

	/// <summary>
	/// Os bots de quem saiu passaram para outro jogador (o próximo mestre).
	/// Se for esta máquina, o boneco de cada um vira bot de verdade, no mesmo
	/// lugar, e ela passa a movê-lo e a falar por ele.
	/// </summary>
	private void ReadBotOwner(BinaryReader r)
	{
		long owner = r.ReadInt64();
		for (int n = r.ReadByte(); n > 0; n--)
		{
			long id = r.ReadInt64();
			int at = OnlineMatch.Roster.FindIndex(s => s.Id == id);
			if (at >= 0) OnlineMatch.Roster[at] = OnlineMatch.Roster[at] with { Owner = owner };
			if (owner != OnlineMatch.AccountId || Setup == null || !_puppets.Remove(id, out Bot puppet)) continue;
			_relayed.Remove(id);

			_poses.Remove(id);
			OnlineMatch.Seat seat = at >= 0 ? OnlineMatch.Roster[at] : new OnlineMatch.Seat(id, puppet.Callsign, puppet.Team, 0, owner);
			Vector3? where = IsInstanceValid(puppet) && !puppet.GetNode<Health>("Health").IsDead ? puppet.GlobalPosition : null;
			if (IsInstanceValid(puppet)) Retire(puppet);
			Own(Setup.AddOwnedBot(seat, where));
		}
		Match.Current?.Enlist();
	}

	/// <summary>O boneco de um bot que passou a ser movido aqui sai de cena sem
	/// ser apagado: a câmera de espectador, a mira e o minimapa podem estar
	/// segurando ele neste quadro. Fica escondido, parado, sem colisão e fora
	/// dos grupos (o placar e quem procura corpos acham o bot novo).</summary>
	private static void Retire(Bot puppet)
	{
		puppet.RemotePresent(false);
		puppet.ProcessMode = ProcessModeEnum.Disabled;
		foreach (StringName group in puppet.GetGroups()) puppet.RemoveFromGroup(group);
		puppet.Name = $"{puppet.Name}_aposentado";
	}

	private static void Apply(Bot body, NetState s)
	{
		Form form = Enum.IsDefined(typeof(Form), (int)s.Form) ? (Form)s.Form : Form.Human;
		body.RemoteUpdate(s.Position, s.Velocity, s.Yaw, s.Pitch, s.Grounded, form, (Slot)s.Slot,
			s.Primary, s.Secondary, s.Hp, s.MaxHp, s.Shield, s.MaxShield, s.Dead);
		body.RemoteWallRun = s.WallRunning && !s.Dead;
		body.RemoteMotion(s.Sprinting, s.Crouching, s.Howling && !s.Dead, s.Quiet, s.GrenadeReady && !s.Dead, s.Aiming && !s.Dead);
		if (s.HasGear && !s.Dead) body.RemoteGear(s.Ammo, s.Reloading, s.Grenades, s.Wp, s.Charge);
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

	/// <summary>
	/// Partida autoritativa (decisão 0016): o servidor aplicou um acerto em
	/// mim e diz como fiquei. A morte, se veio, é dele: não conto de volta
	/// (o abate chega logo depois, para todos).
	/// </summary>
	private void TakeServerDamage(BinaryReader r)
	{
		long attackerId = r.ReadInt64();
		float dealt = r.ReadSingle();
		r.ReadBoolean();
		r.ReadByte();
		r.ReadString();
		Vector3 from = r.ReadVector();
		float hp = r.ReadSingle(), shield = r.ReadSingle();

		_puppets.TryGetValue(attackerId, out Bot attacker);
		_hitInProgress = true;
		_health.ApplyServerHit(dealt, hp, shield, from, attacker);
		_hitInProgress = false;
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
		// o meu, e o dos bots que esta máquina move. Quem se matou (a própria
		// granada, a queda: o servidor anuncia com ele nos dois lados) não ganha.
		if (killer != victim)
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

	/// <summary>O rage de cada um, como o servidor de partida conta.</summary>
	private void ReadFire(BinaryReader r)
	{
		int count = r.ReadByte();
		for (int i = 0; i < count; i++)
		{
			long id = r.ReadInt64();
			float heat = r.ReadSingle();
			bool onFire = r.ReadBoolean();
			int seconds = r.ReadUInt16();
			(string name, int team) = Who(id);
			if (team != 0) Match.Current?.NetFire(name, team, heat, onFire, seconds);
		}
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

		GD.Print("Online match ended: " + why);
		OnlineMatch.Notice = why;
		Callable.From(() => Match.Current?.Leave(ReturnTarget.Room)).CallDeferred();
	}
}
