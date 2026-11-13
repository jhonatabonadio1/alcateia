using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Cada mapa da lista carrega na partida, tem onde os dois times nascerem,
/// rotas de ronda, malha de navegação com chão de verdade, e os bots saem
/// da base e trocam tiro nele. O testar.sh reprova qualquer ERROR.
/// </summary>
public abstract class MapPlaysTest : RegressionTest
{
	protected abstract string MapId { get; }
	public override string Scene => RegressionBench.Match;
	public override string Description => $"o mapa {Maps.Get(MapId).Name} carrega, os times nascem e os bots lutam nele";

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = MapId, TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		foreach (var (name, team) in RoomConfig.DefaultBots())
			RoomConfig.Members.Add(new RoomMember { Name = name, Team = team, IsBot = true });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		var slot = b.Scene.GetNodeOrNull<MapSlot>("Map");
		b.Check(slot != null && slot.Info.Id == MapId, $"carregou o mapa pedido ({slot?.Info.Id})");
		b.Check(SpawnPoints.PointsFor(Teams.Blue).Count >= 6 && SpawnPoints.PointsFor(Teams.Orange).Count >= 6,
			"pelo menos seis pontos de nascimento por time");
		b.Check(PatrolRoutes.RouteCount >= 5, $"rotas de ronda ({PatrolRoutes.RouteCount})");

		// Ninguém nasce dentro de parede nem cai do mapa.
		await b.Seconds(1.5);
		var bots = b.Bots();
		b.Check(bots.Length == 7, $"sete bots na partida ({bots.Length})");
		b.Check(bots.All(bot => bot.GlobalPosition.Y > -1.5f) && b.Player.GlobalPosition.Y > -1.5f, "todos pisando no mapa");

		Vector3[] start = bots.Select(bot => bot.GlobalPosition).ToArray();
		int shots = 0;
		void Count(Noise noise)
		{
			if (noise.Kind == NoiseKind.Shot) shots++;
		}
		NoiseBus.Subscribe(Count);
		await b.Seconds(40);
		NoiseBus.Unsubscribe(Count);

		int moved = 0;
		for (int i = 0; i < bots.Length; i++)
			if (GodotObject.IsInstanceValid(bots[i]) && RegressionBench.Flat(bots[i].GlobalPosition - start[i]) > 6f) moved++;
		RegressionBench.Note($"{Maps.Get(MapId).Name}: {moved} de {bots.Length} bots saíram da base, {shots} tiros em 40 s");
		b.AtLeast(5, moved, "bots que andaram mais de 6 m (a navegação funciona)");
		b.AtLeast(5, shots, "tiros trocados em 40 s");

		RoomConfig.Active = false;
	}
}

public sealed class MetroMapTest : MapPlaysTest
{
	public override string Name => "MapaMetro";
	protected override string MapId => "metro";
}

public sealed class VilaMapTest : MapPlaysTest
{
	public override string Name => "MapaVila";
	protected override string MapId => "vila";
}

public sealed class PolarMapTest : MapPlaysTest
{
	public override string Name => "MapaPolar";
	protected override string MapId => "polar";
}

public sealed class HangarMapTest : MapPlaysTest
{
	public override string Name => "MapaHangar";
	protected override string MapId => "hangar";
}
