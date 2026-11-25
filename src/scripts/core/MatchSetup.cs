using Godot;
using System.Collections.Generic;

/// <summary>
/// Monta a partida a partir da sala: põe o jogador na base do time dele
/// e cria os bots, cada um num ponto de spawn diferente.
///
/// Antes os bots eram nós fixos na cena. Com sala, quem decide quantos e
/// de que lado é o mestre da sala, então eles passam a nascer aqui.
///
/// Fica na cena depois do nó Bots e do jogador, de propósito: quando o
/// _Ready daqui roda, o mapa (com os spawns) e o jogador já estão
/// prontos.
/// </summary>
public partial class MatchSetup : Node
{
	[Export] public PackedScene BotScene;
	[Export] public NodePath BotsPath = "../Bots";

	public override void _Ready()
	{
		// As formas do sangue são geradas uma vez: aqui, na carga, e não no
		// primeiro tiro que acerta.
		if (Blood.Enabled) Blood.Warm();

		var bots = GetNodeOrNull<Node3D>(BotsPath);
		var player = Groups.LocalPlayer(GetTree()) as Node3D;

		// Cada time distribui os próprios pontos em ordem, e ninguém nasce
		// em cima de ninguém — dois corpos no mesmo ponto se empurram e
		// um deles sai voando da base no primeiro quadro.
		var used = new Dictionary<int, int>();

		if (bots == null || BotScene == null)
		{
			GD.PrintErr("MatchSetup: no bots node or no bot scene.");
			return;
		}

		if (OnlineMatch.Active)
		{
			SetUpOnline(bots, player, used);
			return;
		}

		if (player != null)
		{
			int team = player.GetNodeOrNull<Health>("Health")?.Team ?? Teams.Blue;
			Place(player, team, used);
		}

		IEnumerable<(string Name, int Team)> roster = RoomConfig.Active
			? RoomConfig.Bots
			: RoomConfig.DefaultBots();

		int index = 0;
		foreach (var (name, team) in roster)
		{
			var bot = BotScene.Instantiate<Bot>();

			bot.Name = $"Bot{++index}";
			bot.Team = team;
			bot.Callsign = name;
			bot.PatrolCenter = Vector3.Zero;
			bot.PatrolRadius = 30f;

			// Posição antes de entrar na árvore: o bot guarda o lugar onde
			// nasceu no _Ready, e é para lá que ele volta se cair do mapa.
			Place(bot, team, used);
			bots.AddChild(bot);

			// A regra de armas da sala vale para os bots também.
			RoomRules.EquipBot(bot.GetNode<Weapon>("Weapon"));
		}
	}

	/// <summary>
	/// Partida online. Cada lugar da escalação ganha um ponto na base do
	/// time, na mesma ordem em todas as máquinas (pelo número), e os
	/// outros jogadores viram bonecos que a rede move
	/// (<see cref="NetMatch"/>).
	///
	/// Os bots da sala são bots de verdade só na máquina do dono da sala,
	/// que os move e fala por eles; nas outras, são bonecos como os dos
	/// jogadores.
	/// </summary>
	private void SetUpOnline(Node3D bots, Node3D player, Dictionary<int, int> used)
	{
		var net = new NetMatch { Name = "NetMatch", Setup = this };
		var puppets = new List<Bot>();
		var owned = new List<Bot>();
		_bots = bots;
		_used = used;

		foreach (OnlineMatch.Seat seat in OnlineMatch.Roster)
		{
			if (seat.Id == OnlineMatch.AccountId)
			{
				if (player == null) continue;
				if (player.GetNodeOrNull<Health>("Health") is Health mine) mine.DisplayName = seat.Name;
				Place(player, seat.Team, used);
				continue;
			}

			if (seat.Mine) owned.Add(AddOwnedBot(seat));
			else puppets.Add(AddPuppet(seat));
		}

		AddChild(net);
		foreach (Bot puppet in puppets) net.Register(puppet);
		foreach (Bot bot in owned) net.Own(bot);
	}

	private Node3D _bots;
	private Dictionary<int, int> _used;

	/// <summary>O boneco de outro jogador (ou de um bot de outra máquina), na
	/// base do time. Também no meio da partida, para quem entrou com ela em
	/// curso (<see cref="NetMatch"/>).</summary>
	public Bot AddPuppet(OnlineMatch.Seat seat)
	{
		var puppet = BotScene.Instantiate<Bot>();
		puppet.Name = seat.IsBot ? $"Bot{-seat.Id}" : $"Jogador{seat.Id}";
		puppet.Team = seat.Team;
		puppet.Callsign = seat.Name;
		puppet.Remote = true;
		puppet.RemoteId = seat.Id;
		puppet.RemoteRank = seat.IsBot ? Ranks.None : Ranks.RankFor(seat.Xp);
		puppet.RemotePhoto = seat.IsBot ? null : seat.Photo;
		Place(puppet, seat.Team, _used);
		_bots.AddChild(puppet);
		return puppet;
	}

	/// <summary>Um bot que esta máquina move (ela é a do mestre da sala), na
	/// base do time, ou em <paramref name="at"/>: no lugar do boneco dele,
	/// quando os bots passam para esta máquina no meio da partida.</summary>
	public Bot AddOwnedBot(OnlineMatch.Seat seat, Vector3? at = null)
	{
		var bot = BotScene.Instantiate<Bot>();
		bot.Name = $"Bot{-seat.Id}";
		bot.Team = seat.Team;
		bot.Callsign = seat.Name;
		bot.NetId = seat.Id;
		bot.PatrolCenter = Vector3.Zero;
		bot.PatrolRadius = 30f;
		Place(bot, seat.Team, _used);
		if (at is Vector3 where) bot.Position = where;
		_bots.AddChild(bot);
		RoomRules.EquipBot(bot.GetNode<Weapon>("Weapon"));
		return bot;
	}

	private static void Place(Node3D body, int team, Dictionary<int, int> used)
	{
		List<Vector3> points = SpawnPoints.PointsFor(team);
		if (points.Count == 0) return;

		used.TryGetValue(team, out int next);
		used[team] = next + 1;

		Vector3 at = points[next % points.Count];
		SpawnPoints.SetHome(body, at);

		if (body.IsInsideTree()) body.GlobalPosition = at;
		else body.Position = at;

		// Olhando para o centro do mapa. O azul nasce a oeste e olha para
		// leste; o laranja, o contrário.
		float yaw = team == Teams.Blue ? -Mathf.Pi / 2f : Mathf.Pi / 2f;
		body.Rotation = new Vector3(0, yaw, 0);
	}
}
