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

		// Onde se nasce há lugar para o corpo e chão embaixo; e entre os
		// pontos de nascimento não há buraco no chão por onde cair do mapa.
		PhysicsDirectSpaceState3D space = b.Scene.GetNode<Node3D>("Map").GetWorld3D().DirectSpaceState;
		var all = SpawnPoints.PointsFor(Teams.Blue).Concat(SpawnPoints.PointsFor(Teams.Orange)).ToList();
		var body = new CapsuleShape3D { Radius = 0.35f, Height = 1.7f };
		int cramped = 0, floorless = 0;
		foreach (Vector3 point in all)
		{
			var query = new PhysicsShapeQueryParameters3D
			{
				Shape = body,
				Transform = new Transform3D(Basis.Identity, point + Vector3.Up * 0.25f),
				CollisionMask = 1
			};
			// Só o cenário conta: quem nasceu ali está em pé no ponto.
			if (space.IntersectShape(query, 16).Any(hit => hit["collider"].AsGodotObject() is StaticBody3D)) cramped++;
			if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(point, point + Vector3.Down * 3f, 1)).Count == 0) floorless++;
		}
		b.Check(cramped == 0, $"nenhum ponto de nascimento dentro de parede ou objeto ({cramped} de {all.Count})");
		b.Check(floorless == 0, $"todo ponto de nascimento tem chão embaixo ({floorless} de {all.Count})");

		float minX = all.Min(p => p.X), maxX = all.Max(p => p.X), minZ = all.Min(p => p.Z), maxZ = all.Max(p => p.Z);
		int holes = 0, cells = 0;
		string where = "";
		for (float x = minX; x <= maxX; x += 1f)
		{
			for (float z = minZ; z <= maxZ; z += 1f)
			{
				cells++;
				var from = new Vector3(x + 0.13f, 60f, z + 0.17f);
				if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 80f, 1)).Count > 0) continue;
				holes++;
				if (holes <= 3) where += $" ({x:0},{z:0})";
			}
		}
		b.Check(holes == 0, $"sem buraco no chão ({holes} de {cells} pontos{where})");

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

		// No mapa grande os times levam quase 40 s para se achar, e a
		// malha de navegação fica pronta num quadro que depende da
		// máquina: quem ainda não trocou tiro ganha mais um minuto.
		double waited = 40;
		if (shots < 5 && await b.Until(() => shots >= 5, 60)) waited = 100;
		NoiseBus.Unsubscribe(Count);

		int moved = 0;
		for (int i = 0; i < bots.Length; i++)
			if (GodotObject.IsInstanceValid(bots[i]) && RegressionBench.Flat(bots[i].GlobalPosition - start[i]) > 6f) moved++;
		RegressionBench.Note($"{Maps.Get(MapId).Name}: {moved} de {bots.Length} bots saíram da base, {shots} tiros em {(waited > 40 ? "até 100" : "40")} s");
		b.AtLeast(5, moved, "bots que andaram mais de 6 m (a navegação funciona)");
		b.AtLeast(5, shots, "tiros trocados (em até 100 s)");

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
