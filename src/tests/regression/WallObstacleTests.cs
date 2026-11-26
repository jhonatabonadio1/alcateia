using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A corrida na parede nos obstáculos e nas pontas que os mapas de verdade
/// têm (Jhon, 26/11, alpha fechado: "wall-run não vira mais em cantos e
/// passa por baixo de obstáculos pequenos"; "na parede, não contorna mais —
/// anda reto e passa por baixo de obstáculos pequenos em vez de subir neles").
///
/// Tudo no alto, longe do cenário da bancada: uma parede de 30 m, e nela o
/// que se quer medir.
/// </summary>
public abstract class WallObstacleBase : RegressionTest
{
	protected static StaticBody3D Box(RegressionBench b, string name, Vector3 at, Vector3 size)
	{
		var body = new StaticBody3D { Name = name, Position = at };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		b.GetTree().CurrentScene.AddChild(body);
		return body;
	}

	/// <summary>O que aconteceu numa corrida de 3 s rente à face z = −0,2,
	/// indo para +X a partir de x = −4.</summary>
	protected record struct Outcome(bool Grabbed, int OffFrames, float Rise, Vector3 End);

	protected static async Task<Outcome> RunAlong(RegressionBench b, List<StaticBody3D> made)
	{
		Player p = b.Player;
		// Toca o chão antes: a regra da mesma parede recomeça.
		await b.Place(new Vector3(-8, 1.2f, 8), -90f, settle: 0.4);
		await b.Frames(2);
		await b.Place(new Vector3(-4, 40, -0.2f - 0.35f), -90f, settle: 0.02);
		p.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		bool grabbed = await b.Until(() => p.IsWallRunning, 0.7);
		float y0 = p.GlobalPosition.Y;
		int off = 0;
		double end = b.Time + 3.0;
		while (b.Time < end && p.GlobalPosition.Y > 30)
		{
			await b.PhysicsFrame();
			if (!p.IsWallRunning) off++;
		}
		RegressionBench.ReleaseAll();
		var run = new Outcome(grabbed, off, p.GlobalPosition.Y - y0, p.GlobalPosition);
		foreach (StaticBody3D m in made) m.QueueFree();
		await b.Frames(3);
		return run;
	}
}

/// <summary>
/// Obstáculo pequeno na parede (caixote, ar-condicionado, peitoril): o lobo
/// sobe por cima e segue na parede. Antes de 26/11 o raio do meio do corpo
/// só via o que estava na altura da barriga: com o obstáculo na altura da
/// cabeça ou das patas o corpo batia, parava e caía (87 de 172 quadros fora
/// da parede, caiu 10 m), ou escorregava por baixo dele.
/// </summary>
public sealed class WallClimbsObstacleTest : WallObstacleBase
{
	public override string Name => "ParedeSobeObstaculo";
	public override string Description => "correndo na parede, o lobo sobe por cima de um obstáculo pequeno (na altura da cabeça, da barriga ou das patas) e segue na parede";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		Player p = b.Player;
		foreach (float height in new[] { 0.5f, 0f, -0.5f })
		{
			// 25 cm de fundo, 50 cm de alto, 40 cm de largura, em x = 2,5.
			var made = new List<StaticBody3D>
			{
				Box(b, "Parede", new Vector3(0, 40, 0), new Vector3(30, 12, 0.4f)),
				Box(b, "Obstaculo", new Vector3(2.5f, 40 + height, -0.325f), new Vector3(0.4f, 0.5f, 0.25f)),
			};
			await b.Frames(2);
			Outcome run = await RunAlong(b, made);
			// Para passar por cima, os pés sobem acima do topo do obstáculo.
			float needed = (height + 0.25f) + p.StandHeight * 0.5f;
			string at = height > 0 ? "na altura da cabeça" : height < 0 ? "na altura das patas" : "na altura da barriga";
			RegressionBench.Note($"obstáculo {at}: {run.OffFrames} quadros fora da parede; subiu {RegressionBench.F(run.Rise)} m (o topo pede {RegressionBench.F(needed)}); chegou a x = {RegressionBench.F(run.End.X)}");
			b.Check(run.Grabbed, $"{at}: grudou");
			b.Check(run.OffFrames == 0, $"{at}: não solta a parede ({run.OffFrames} quadros fora)");
			b.AtLeast(needed - 0.05, run.Rise, $"{at}: sobe por cima do obstáculo (m)");
			b.AtMost(p.WallClimbReach + 0.05, run.Rise, $"{at}: sem subir além do alcance (m)");
			b.AtLeast(6.0, run.End.X, $"{at}: passa do obstáculo e segue (x)");
		}
	}
}

/// <summary>
/// A parede que não acaba onde parece: um vão de porta, um degrau da
/// fachada, um pilar raso. Antes de 26/11 o vão de porta (paredes de 40 cm)
/// virava quina: o lobo contornava a ponta e voltava pelo outro lado da
/// parede (terminava em z = +0,54, atrás dela). Agora atravessa o vão pela
/// linha da parede. O degrau e o pilar ficam de guarda.
/// </summary>
public sealed class WallGapAndStepTest : WallObstacleBase
{
	public override string Name => "ParedeVaoEDegrau";
	public override string Description => "o lobo atravessa um vão de porta pela linha da parede, e segue num degrau da fachada e num pilar raso, sem soltar nem dar a volta";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		var cases = new (string name, System.Func<List<StaticBody3D>> build)[]
		{
			("vão de porta de 1 m", () => new List<StaticBody3D>
			{
				Box(b, "ParedeA", new Vector3(-7.5f, 40, 0), new Vector3(15, 12, 0.4f)),
				Box(b, "ParedeB", new Vector3(8.5f, 40, 0), new Vector3(15, 12, 0.4f)),
			}),
			("degrau da fachada 15 cm para trás", () => new List<StaticBody3D>
			{
				Box(b, "ParedeA", new Vector3(-7.5f, 40, 0), new Vector3(15, 12, 0.4f)),
				Box(b, "ParedeB", new Vector3(7.5f, 40, 0.15f), new Vector3(15, 12, 0.4f)),
			}),
			("pilar raso (15 cm de fundo, 50 cm de largura)", () => new List<StaticBody3D>
			{
				Box(b, "Parede", new Vector3(0, 40, 0), new Vector3(30, 12, 0.4f)),
				Box(b, "Pilar", new Vector3(2.5f, 40, -0.275f), new Vector3(0.5f, 12, 0.15f)),
			}),
		};
		foreach (var (name, build) in cases)
		{
			var made = build();
			await b.Frames(2);
			Outcome run = await RunAlong(b, made);
			RegressionBench.Note($"{name}: {run.OffFrames} quadros fora da parede; terminou em x = {RegressionBench.F(run.End.X)}, z = {RegressionBench.F(run.End.Z)}");
			b.Check(run.Grabbed, $"{name}: grudou");
			b.Check(run.OffFrames == 0, $"{name}: não solta a parede");
			b.AtLeast(6.0, run.End.X, $"{name}: segue em frente (x)");
			b.AtMost(-0.2, run.End.Z, $"{name}: do lado de cá da parede (z)");
		}
	}
}

/// <summary>
/// Nos mapas de verdade: pontos onde, até 26/11, a corrida acabava em menos
/// de 2 m — a parede "acabava" num degrau, num batente, num pilar ou num vão
/// (a regra da quina, de 12/11, foi feita para as paredes lisas da cápsula
/// cinza), ou o corpo batia num obstáculo pequeno e caía. Medidos na
/// varredura de 80 pontos por mapa (média percorrida em 2,5 s, antes →
/// depois): Cidade 4,7 → 6,6 m, Armazém 7,3 → 9,3, Neon 3,1 → 5,3, Estação
/// 3,9 → 6,4, Cidade do Deserto 5,5 → 8,4, Obra 4,2 → 4,2 (lá as paredes
/// são chapas de 20 cm, que de propósito não se contornam).
/// </summary>
public abstract class WallOnMapTest : RegressionTest
{
	protected abstract string MapId { get; }

	/// <summary>Ponto na face (a 1,6 m do chão, onde o raio do teste bateu),
	/// a normal dela, o sentido da corrida e a altura do chão.</summary>
	protected abstract (Vector3 At, Vector3 Normal, Vector3 Along, float Floor)[] Spots { get; }

	public override string Scene => RegressionBench.Match;
	public override string Description => $"no mapa {Maps.Get(MapId).Name}, a corrida na parede passa dos degraus, batentes, pilares e vãos que antes a faziam soltar";

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = MapId, TeamSize = 1 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Members.Add(new RoomMember { Name = "Bot", Team = Teams.Orange, IsBot = true });
		RoomConfig.Active = true;
	}

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 40);
		foreach (CharacterBody3D bot in b.Bots()) { RegressionBench.Freeze(bot); bot.GlobalPosition += Vector3.Down * 300f; }
		await b.BecomeWolf();
		Player p = b.Player;
		foreach (var (at, n, along, floor) in Spots)
		{
			await b.Place(new Vector3(at.X, floor + 1.0f, at.Z) + n * 1.2f, 0f, settle: 0.15);
			p.GlobalPosition = new Vector3(at.X, floor + 1.9f, at.Z) + n * 0.5f;
			p.Velocity = along * 3.5f;
			p.Rotation = new Vector3(0, Mathf.Atan2(-along.X, -along.Z), 0);
			RegressionBench.Press("move_forward");
			bool grabbed = await b.Until(() => p.IsWallRunning, 0.5);
			float path = 0f;
			Vector3 last = p.GlobalPosition;
			double end = b.Time + 2.5;
			while (grabbed && b.Time < end && p.IsWallRunning)
			{
				await b.PhysicsFrame();
				if (p.IsWallRunning) path += (p.GlobalPosition - last).Length();
				last = p.GlobalPosition;
			}
			RegressionBench.ReleaseAll();
			string where = $"({RegressionBench.F(at.X)}, {RegressionBench.F(at.Z)})";
			RegressionBench.Note($"{where}: correu {RegressionBench.F(path)} m na parede");
			b.Check(grabbed, $"{where}: grudou");
			b.AtLeast(6.0, path, $"{where}: corre na parede sem soltar na ponta (m; antes, menos de 2)");
		}
	}
}

public sealed class WallOnCityTest : WallOnMapTest
{
	public override string Name => "ParedeNaCidade";
	protected override string MapId => "cidade";
	protected override (Vector3 At, Vector3 Normal, Vector3 Along, float Floor)[] Spots => new[]
	{
		(new Vector3(22.753f, 1.6f, -17f), Vector3.Left, Vector3.Forward, 0f),               // antes 0,1 m (degrau: "parede fina")
		(new Vector3(11f, 1.46f, -24.783f), Vector3.Forward, Vector3.Right, -0.14f),         // antes 1,4 m (degrau: "parede fina")
		(new Vector3(23f, 1.46f, -24.076f), new Vector3(-0.622f, 0, -0.783f), new Vector3(0.783f, 0, -0.622f), -0.14f), // antes 1,5 m
	};
}

public sealed class WallOnWarehouseTest : WallOnMapTest
{
	public override string Name => "ParedeNoArmazem";
	protected override string MapId => "armazem_estendido";
	protected override (Vector3 At, Vector3 Normal, Vector3 Along, float Floor)[] Spots => new[]
	{
		(new Vector3(-37.5f, 1.6f, 2.8f), Vector3.Right, Vector3.Back, 0f),                  // antes 0,8 m (pilar raso do galpão)
		(new Vector3(-27.7f, 5.2f, -22.25f), Vector3.Back, Vector3.Right, 3.6f),             // antes 0,3 m (no andar de cima)
		(new Vector3(-36.872f, 1.6f, -3.2f), Vector3.Right, Vector3.Back, 0f),               // antes 1,5 m
	};
}

public sealed class WallOnNeonTest : WallOnMapTest
{
	public override string Name => "ParedeNoNeon";
	protected override string MapId => "neon";
	protected override (Vector3 At, Vector3 Normal, Vector3 Along, float Floor)[] Spots => new[]
	{
		(new Vector3(-10f, 1.65f, 12.5f), Vector3.Back, Vector3.Left, 0.05f),                // antes 0,5 m
		(new Vector3(-32f, 4.144f, -10f), Vector3.Right, Vector3.Back, 2.544f),              // antes 0,6 m
		(new Vector3(-40f, 8.272f, 2.475f), Vector3.Forward, Vector3.Left, 6.672f),          // antes 0,6 m (batia e caía)
	};
}
