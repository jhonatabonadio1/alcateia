using Godot;
using System.Collections.Generic;

/// <summary>
/// Os bots também falam no rádio (pedido do Jhon, 19/11: "bots devem dar
/// sinais de rádio também, mas sem exagero para não floodar"). Pelo mesmo
/// <see cref="Radio"/> do jogador: a voz do time, o ícone em cima de quem
/// falou e no minimapa — e só o time de quem falou ouve.
///
/// Quando falam:
/// - acharam um inimigo depois de um tempo sem ver ninguém: "Enemy Spotted!";
/// - com pouca vida, apanhando: "Need Backup!";
/// - você abateu alguém e um bot do seu time viu: "Nice Shot!";
/// - um bot abateu alguém: às vezes "Sector Clear!";
/// - você pediu ("Follow Me!", "Cover Me!", "Hold Position!", "Need
///   Backup!", "Go, Go, Go!", "Fall Back!"): um bot do seu time responde
///   "Roger That!".
///
/// Sem flood: cada fala tem chance, cada bot espera <see cref="BotRest"/>
/// segundos entre falas e o time inteiro de bots, <see cref="TeamGap"/>.
/// Responder a você tem intervalo próprio, mais curto.
/// </summary>
public partial class BotRadio : Node
{
	public const int Spotted = 0, Roger = 1, Backup = 7, Clear = 8, NiceShot = 9;

	/// <summary>Segundos entre duas falas de bots do mesmo time.</summary>
	[Export] public double TeamGap = 9.0;

	/// <summary>Segundos entre duas falas do mesmo bot.</summary>
	[Export] public double BotRest = 30.0;

	/// <summary>Segundos entre duas respostas ("Roger That!") a você.</summary>
	[Export] public double ReplyGap = 4.0;

	/// <summary>A que distância (m) um bot aliado "viu" o seu abate.</summary>
	[Export] public float WitnessRange = 30f;

	public static BotRadio Instance { get; private set; }

	private double _clock;
	private double _lastReply = -100;
	private readonly Dictionary<int, double> _teamLast = new();
	private readonly Dictionary<ulong, double> _botLast = new();
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<(double At, Bot Bot, int Call)> _pending = new();

	public override void _Ready()
	{
		Instance = this;
		_rng.Seed = 0xB07AD10;
		KillFeed.Reported += OnKill;
		Radio.Heard += OnHeard;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
		KillFeed.Reported -= OnKill;
		Radio.Heard -= OnHeard;
	}

	public override void _Process(double delta)
	{
		_clock += delta;
		for (int i = _pending.Count - 1; i >= 0; i--)
		{
			if (_pending[i].At > _clock) continue;
			var (_, bot, call) = _pending[i];
			_pending.RemoveAt(i);
			if (IsInstanceValid(bot)) Say(bot, call);
		}
	}

	// ------------------------------------------------------------ o que os bots chamam

	/// <summary>O bot achou um inimigo depois de um tempo sem ninguém.</summary>
	public void OnSpotted(Bot bot) => Try(bot, Spotted, 0.35f);

	/// <summary>O bot apanhou e está com pouca vida.</summary>
	public void OnHurt(Bot bot) => Try(bot, Backup, 0.4f);

	// ------------------------------------------------------------ o que acontece em volta

	private void OnKill(Kill kill)
	{
		if (!Match.IsLive) return;
		Node3D me = Groups.LocalPlayer(GetTree()) as Node3D;
		string myName = me?.GetNodeOrNull<Health>("Health")?.PublicName;

		// Você abateu: um bot do seu time, perto, elogia.
		if (myName != null && kill.Killer == myName && kill.KillerTeam != kill.VictimTeam)
		{
			Bot witness = Nearest(me.GlobalPosition, kill.KillerTeam, WitnessRange);
			if (witness != null) Try(witness, NiceShot, 0.3f, delay: 0.7);
			return;
		}

		// Um bot abateu: às vezes avisa que o setor está limpo.
		if (Find(kill.Killer) is Bot killer && kill.KillerTeam != kill.VictimTeam)
			Try(killer, Clear, 0.15f, delay: 0.9);
	}

	private void OnHeard(string who, int team, int call)
	{
		// Só os pedidos têm resposta.
		if (call is Roger or NiceShot or Clear or Spotted) return;
		if (_clock - _lastReply < ReplyGap) return;
		Node3D me = Groups.LocalPlayer(GetTree()) as Node3D;
		if (me?.GetNodeOrNull<Health>("Health") is not Health health || health.PublicName != who) return;

		Bot mate = Nearest(me.GlobalPosition, team, 9999f);
		if (mate == null || _rng.Randf() > 0.7f) return;
		_lastReply = _clock;
		_pending.Add((_clock + _rng.RandfRange(0.6f, 1.2f), mate, Roger));
	}

	// ------------------------------------------------------------ falar

	private void Try(Bot bot, int call, float chance, double delay = 0)
	{
		if (!CanTalk(bot) || _rng.Randf() > chance) return;
		int team = bot.GetNode<Health>("Health").Team;
		// Reserva a vez já: a fala atrasada não abre espaço para outra.
		_teamLast[team] = _clock + delay;
		_botLast[bot.GetInstanceId()] = _clock + delay;
		if (delay <= 0) Say(bot, call);
		else _pending.Add((_clock + delay, bot, call));
	}

	private bool CanTalk(Bot bot)
	{
		if (bot == null || !IsInstanceValid(bot) || bot.Remote || !Match.IsLive) return false;
		if (bot.GetNodeOrNull<Health>("Health") is not Health health || health.IsDead) return false;
		if (_teamLast.TryGetValue(health.Team, out double team) && _clock - team < TeamGap) return false;
		if (_botLast.TryGetValue(bot.GetInstanceId(), out double own) && _clock - own < BotRest) return false;
		return true;
	}

	private static void Say(Bot bot, int call)
	{
		if (bot.GetNodeOrNull<Health>("Health") is Health health && !health.IsDead)
			Radio.Receive(bot, call);
	}

	private Bot Nearest(Vector3 at, int team, float range)
	{
		Bot best = null;
		float bestDistance = range * range;
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Bots))
		{
			if (node is not Bot bot || bot.Remote || bot.GetNodeOrNull<Health>("Health") is not Health health) continue;
			if (health.IsDead || health.Team != team) continue;
			float distance = bot.GlobalPosition.DistanceSquaredTo(at);
			if (distance < bestDistance) { bestDistance = distance; best = bot; }
		}
		return best;
	}

	private Bot Find(string name)
	{
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Bots))
			if (node is Bot bot && bot.GetNodeOrNull<Health>("Health")?.PublicName == name) return bot;
		return null;
	}
}
