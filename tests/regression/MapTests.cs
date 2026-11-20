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

	/// <summary>O mapa é fechado por um muro (ou por prédios) em que o lobo
	/// poderia subir, de salto em salto na parede: tem de haver barreira
	/// invisível por cima dele, até o teto (pedido do Jhon, 20/11).</summary>
	protected virtual bool ClosedRing => false;
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

		// Em cima do muro de fora não cabe ninguém: 1,3 m para dentro das
		// paredes de limite, a 1 m do teto, todo ponto da volta do mapa cai
		// dentro de uma barreira.
		if (ClosedRing)
		{
			Aabb box = MapBounds.Of(b.GetTree());
			int open = 0, points = 0;
			float y = box.End.Y - 1f, inset = 1.3f;
			for (int i = 0; i <= 60; i++)
			{
				float x = Mathf.Lerp(box.Position.X + inset, box.End.X - inset, i / 60f);
				float z = Mathf.Lerp(box.Position.Z + inset, box.End.Z - inset, i / 60f);
				foreach (Vector3 point in new[] { new Vector3(x, y, box.Position.Z + inset), new Vector3(x, y, box.End.Z - inset),
					         new Vector3(box.Position.X + inset, y, z), new Vector3(box.End.X - inset, y, z) })
				{
					points++;
					var query = new PhysicsPointQueryParameters3D { Position = point, CollideWithBodies = true, CollideWithAreas = false };
					if (space.IntersectPoint(query, 1).Count == 0) open++;
				}
			}
			b.Check(open == 0, $"o alto do muro de fora é barrado até o teto: ninguém sobe nem sai ({open} de {points} pontos abertos)");
		}

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

		// O mais longe que cada bot chegou do ponto de onde saiu, medido a cada
		// segundo: no mapa grande a briga é no meio, e quem morre lá renasce
		// na base — a posição do fim sozinha diria que ele nunca saiu (20/11).
		float[] far = new float[bots.Length];
		void Measure()
		{
			for (int i = 0; i < bots.Length; i++)
				if (GodotObject.IsInstanceValid(bots[i]))
					far[i] = Mathf.Max(far[i], RegressionBench.Flat(bots[i].GlobalPosition - start[i]));
		}
		for (int second = 0; second < 40; second++)
		{
			await b.Seconds(1);
			Measure();
		}

		// No mapa grande os times levam quase 40 s para se achar, e a
		// malha de navegação fica pronta num quadro que depende da
		// máquina: quem ainda não trocou tiro ganha mais um minuto.
		double waited = 40;
		if (shots < 5 && await b.Until(() => shots >= 5, 60)) waited = 100;
		NoiseBus.Unsubscribe(Count);

		Measure();
		int moved = far.Count(distance => distance > 6f);
		RegressionBench.Note($"{Maps.Get(MapId).Name}: {moved} de {bots.Length} bots saíram da base, {shots} tiros em {(waited > 40 ? "até 100" : "40")} s");
		b.AtLeast(5, moved, "bots que andaram mais de 6 m (a navegação funciona)");
		b.AtLeast(5, shots, "tiros trocados (em até 100 s)");

		RoomConfig.Active = false;
	}
}

public sealed class ArmazemEstendidoMapTest : MapPlaysTest
{
	public override string Name => "MapaArmazemEstendido";
	protected override string MapId => "armazem_estendido";
}

public sealed class CidadeMapTest : MapPlaysTest
{
	public override string Name => "MapaCidade";
	protected override string MapId => "cidade";
	protected override bool ClosedRing => true;
}

public sealed class DesertoMapTest : MapPlaysTest
{
	public override string Name => "MapaDeserto";
	protected override string MapId => "deserto";
	protected override bool ClosedRing => true;
}

public sealed class EstacaoMapTest : MapPlaysTest
{
	public override string Name => "MapaEstacao";
	protected override string MapId => "estacao";
	protected override bool ClosedRing => true;
}

public sealed class ObraMapTest : MapPlaysTest
{
	public override string Name => "MapaObra";
	protected override string MapId => "obra";
	protected override bool ClosedRing => true;
}

/// <summary>
/// Nas escadas dos mapas de kit o corpo sobe deslizando numa chapa de
/// colisão por cima dos degraus (nós <c>pr…</c>), na velocidade de andar e
/// sem parar em cada espelho — "a corrida em escadas não está boa, ele não
/// sobe na mesma velocidade que anda, ele trava, não só o lobo, mas humano
/// também" (Jhon, 20/11).
/// </summary>
public abstract class StairsRunTest : RegressionTest
{
	protected abstract string MapId { get; }
	public override string Scene => RegressionBench.Match;
	public override string Description => $"no mapa {Maps.Get(MapId).Name}, humano e lobo sobem a escada na velocidade de andar, sem travar";

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = MapId, TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		b.RemoveBots();
		await b.Seconds(0.5);

		var ramps = b.Scene.FindChildren("pr*", "StaticBody3D", true, false).OfType<StaticBody3D>()
			.Where(body => System.Text.RegularExpressions.Regex.IsMatch(body.Name, "^pr\\d+$")).ToList();
		b.Check(ramps.Count > 0, $"o mapa tem chapa de subida nas escadas ({ramps.Count})");
		if (ramps.Count == 0) { RoomConfig.Active = false; return; }

		StaticBody3D ramp = ramps[0];
		Vector3 size = ((BoxShape3D)ramp.GetChild<CollisionShape3D>(0).Shape).Size;
		Basis basis = ramp.GlobalTransform.Basis;
		Vector3 up = basis.X.Normalized(), top = ramp.GlobalPosition + basis.Y.Normalized() * size.Y * 0.5f;
		// (um quarto para o lado: a escada da Estação tem o corrimão no meio)
		top += basis.Z.Normalized() * size.Z * 0.25f;
		Vector3 foot = top - up * size.X * 0.5f, head = top + up * size.X * 0.5f;
		Vector3 flat = new Vector3(up.X, 0, up.Z).Normalized();
		float yaw = Mathf.RadToDeg(Mathf.Atan2(-flat.X, -flat.Z));

		for (int wolf = 0; wolf < 2; wolf++)
		{
			string who = wolf == 0 ? "humano" : "lobo";
			if (wolf == 1) await b.BecomeWolf();
			Player p = b.Player;
			await b.Place(foot - flat * 1.0f + Vector3.Up * 1.1f, yaw, settle: 0.6);
			float floorY = p.GlobalPosition.Y;

			RegressionBench.Press("move_forward");
			// Até estar na chapa, já embalado.
			bool onRamp = await b.Until(() => p.GlobalPosition.Y > floorY + 0.5f, 3.0);
			b.Check(onRamp, $"{who}: entra na escada andando (pé da escada em {foot.X:0.0}, {foot.Y:0.0}, {foot.Z:0.0}; o corpo em {p.GlobalPosition.X:0.0}, {p.GlobalPosition.Y:0.0}, {p.GlobalPosition.Z:0.0})");

			float slowest = float.MaxValue, total = 0f, wanted = 0f;
			int frames = 0;
			Vector3 last = p.GlobalPosition;
			while (p.GlobalPosition.Y < floorY + (head.Y - foot.Y) - 0.6f && frames < 600)
			{
				await b.PhysicsFrame();
				float step = (p.GlobalPosition - last).Length();
				last = p.GlobalPosition;
				slowest = Mathf.Min(slowest, step);
				total += step;
				wanted += new Vector2(p.Velocity.X, p.Velocity.Z).Length() / 60f;
				frames++;
			}
			bool arrived = await b.Until(() => p.GlobalPosition.Y > floorY + (head.Y - foot.Y) - 0.15f, 3.0);
			RegressionBench.ReleaseAll();

			float mean = frames > 0 ? total / frames : 0f;
			RegressionBench.Note($"{Maps.Get(MapId).Name}, {who}: {mean * 60f:0.00} m/s na escada (andando no chão: {(frames > 0 ? wanted / frames * 60f : 0f):0.00} m/s), o quadro mais lento a {slowest * 60f:0.00} m/s, {frames} quadros");
			b.Check(arrived, $"{who}: chega ao alto da escada");
			b.Check(frames > 10 && total >= wanted * 0.95f, $"{who}: sobe na velocidade de andar ({total:0.00} m para {wanted:0.00} m)");
			b.Check(frames > 10 && slowest >= mean * 0.7f, $"{who}: sem travar em degrau (o quadro mais lento a {(mean > 0 ? slowest / mean * 100f : 0f):0}% da média)");
		}

		RoomConfig.Active = false;
	}
}

public sealed class ObraStairsTest : StairsRunTest
{
	public override string Name => "EscadaObra";
	protected override string MapId => "obra";
}

public sealed class EstacaoStairsTest : StairsRunTest
{
	public override string Name => "EscadaEstacao";
	protected override string MapId => "estacao";
}

public sealed class DesertoStairsTest : StairsRunTest
{
	public override string Name => "EscadaDeserto";
	protected override string MapId => "deserto";
}
