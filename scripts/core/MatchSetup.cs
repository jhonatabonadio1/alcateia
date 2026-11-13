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
		var bots = GetNodeOrNull<Node3D>(BotsPath);
		var player = Groups.LocalPlayer(GetTree()) as Node3D;

		// Cada time distribui os próprios pontos em ordem, e ninguém nasce
		// em cima de ninguém — dois corpos no mesmo ponto se empurram e
		// um deles sai voando da base no primeiro quadro.
		var used = new Dictionary<int, int>();

		if (bots == null || BotScene == null)
		{
			GD.PrintErr("MatchSetup: sem nó de bots ou sem cena de bot.");
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
		var net = new NetMatch { Name = "NetMatch" };
		var puppets = new List<Bot>();
		var owned = new List<Bot>();

		foreach (OnlineMatch.Seat seat in OnlineMatch.Roster)
		{
			if (seat.Id == OnlineMatch.AccountId)
			{
				if (player == null) continue;
				if (player.GetNodeOrNull<Health>("Health") is Health mine) mine.DisplayName = seat.Name;
				Place(player, seat.Team, used);
				continue;
			}

			if (seat.Mine)
			{
				var bot = BotScene.Instantiate<Bot>();
				bot.Name = $"Bot{-seat.Id}";
				bot.Team = seat.Team;
				bot.Callsign = seat.Name;
				bot.NetId = seat.Id;
				bot.PatrolCenter = Vector3.Zero;
				bot.PatrolRadius = 30f;
				Place(bot, seat.Team, used);
				bots.AddChild(bot);
				RoomRules.EquipBot(bot.GetNode<Weapon>("Weapon"));
				owned.Add(bot);
				continue;
			}

			var puppet = BotScene.Instantiate<Bot>();
			puppet.Name = seat.IsBot ? $"Bot{-seat.Id}" : $"Jogador{seat.Id}";
			puppet.Team = seat.Team;
			puppet.Callsign = seat.Name;
			puppet.Remote = true;
			puppet.RemoteId = seat.Id;
			puppet.RemoteRank = seat.IsBot ? Ranks.None : Ranks.RankFor(seat.Xp);
			Place(puppet, seat.Team, used);
			bots.AddChild(puppet);
			puppets.Add(puppet);
		}

		AddChild(net);
		foreach (Bot puppet in puppets) net.Register(puppet);
		foreach (Bot bot in owned) net.Own(bot);
	}

	private static void Place(Node3D body, int team, Dictionary<int, int> used)
	{
		List<Vector3> points = SpawnPoints.PointsFor(team);
		if (points.Count == 0) return;

		used.TryGetValue(team, out int next);
		used[team] = next + 1;

		Vector3 at = points[next % points.Count];

		if (body.IsInsideTree()) body.GlobalPosition = at;
		else body.Position = at;

		// Olhando para o centro do mapa. O azul nasce a oeste e olha para
		// leste; o laranja, o contrário.
		float yaw = team == Teams.Blue ? -Mathf.Pi / 2f : Mathf.Pi / 2f;
		body.Rotation = new Vector3(0, yaw, 0);
	}
}
