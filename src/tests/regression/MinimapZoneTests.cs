using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Numa partida, o minimapa marca a área do inimigo (onde o time dele
/// nasce), virada para cima.
/// </summary>
public sealed class MinimapZoneTest : RegressionTest
{
	public override string Name => "ZonaInimigaNoMapa";
	public override string Description => "o minimapa pinta de vermelho a área onde o inimigo nasce, em cima";
	public override string Scene => RegressionBench.Match;

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "armazem_estendido", TeamSize = 2 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		foreach (var (name, team) in new[] { ("Lua", Teams.Blue), ("Sombra", Teams.Orange), ("Presa", Teams.Orange) })
			RoomConfig.Members.Add(new RoomMember { Name = name, Team = team, IsBot = true });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		b.CloseLoadout();
		await b.Seconds(1.0);
		Minimap map = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Minimap>().FirstOrDefault();
		b.Check(map != null, "há minimapa");
		if (map == null) return;
		b.Check(map.HasEnemyZone, "a área do inimigo está marcada");
		b.Check(map.EnemyZoneOnTop, "e fica na metade de cima do minimapa");
	}
}
