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
        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;

        // Cada time distribui os próprios pontos em ordem, e ninguém nasce
        // em cima de ninguém — dois corpos no mesmo ponto se empurram e
        // um deles sai voando da base no primeiro quadro.
        var used = new Dictionary<int, int>();

        if (player != null)
        {
            int team = player.GetNodeOrNull<Health>("Health")?.Team ?? Teams.Blue;
            Place(player, team, used);
        }

        if (bots == null || BotScene == null)
        {
            GD.PrintErr("MatchSetup: sem nó de bots ou sem cena de bot.");
            return;
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
        }
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
        // leste; o vermelho, o contrário.
        float yaw = team == Teams.Blue ? -Mathf.Pi / 2f : Mathf.Pi / 2f;
        body.Rotation = new Vector3(0, yaw, 0);
    }
}
