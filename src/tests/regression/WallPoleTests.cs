using Godot;
using System.Threading.Tasks;

/// <summary>
/// O lobo em objeto pequeno: poste, haste de placa, coluna fina, placa e
/// parede curta (Jhon, 26/11: "o lobo não gruda em paredes e objetos muito
/// curtos ou pequenos, como um poste ou placa de rua").
///
/// Até 26/11 o poste de 12 cm (a haste das placas da Cidade) era "a ponta de
/// uma parede fina": o lobo grudava e, no quadro seguinte, saía reto e caía
/// (1 quadro na parede, caiu 10 m). Agora ele dá a volta no poste, como no
/// tonel, com teto de giro (<see cref="Player.WallOrbitMaxTurn"/>, 360°/s).
/// Na placa e na parede curta (comprida num sentido só) ele corre na face
/// até a ponta e sai reto, no mesmo passo: não dá meia-volta em volta de uma
/// chapa de 5 cm. Tudo no alto, longe do cenário da bancada.
/// </summary>
public sealed class WallPoleTest : RegressionTest
{
	public override string Name => "ParedePosteEPlaca";
	public override string Description => "o lobo gruda e dá a volta em poste fino, poste quadrado, poste redondo e coluna; numa placa e numa parede curta corre na face e sai reto na ponta; numa mureta de 25 cm contorna; o mesmo poste não vira elevador";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		Player p = b.Player;

		// Postes e coluna: a volta inteira, sem soltar, no teto de giro.
		foreach (var (name, shape) in new (string, Shape3D)[]
		{
			("poste fino de 12 cm", new BoxShape3D { Size = new Vector3(0.12f, 8, 0.12f) }),
			("poste quadrado de 28 cm", new BoxShape3D { Size = new Vector3(0.28f, 8, 0.28f) }),
			("poste redondo de 12 cm", new CylinderShape3D { Radius = 0.06f, Height = 8 }),
			("coluna de 50 cm", new BoxShape3D { Size = new Vector3(0.5f, 8, 0.5f) }),
		})
		{
			PoleRun run = await RunBeside(b, shape, 3.0);
			RegressionBench.Note($"{name}: {run.On} de {run.Frames} quadros na parede, volta de {RegressionBench.F(run.Swept)}° em 3 s, altura variou {RegressionBench.F(run.Drop)} m");
			b.Check(run.Grabbed, $"{name}: grudou");
			b.Check(run.On == run.Frames, $"{name}: não soltou ({run.Frames - run.On} quadros fora)");
			b.AtLeast(720, run.Swept, $"{name}: deu duas voltas ou mais em 3 s (graus; antes, o de 12 cm: 6°)");
			b.AtMost(p.WallOrbitMaxTurn * 3.05f, run.Swept, $"{name}: sem passar do teto de giro (graus)");
			b.AtMost(0.3, run.Drop, $"{name}: sem cair (m)");
		}

		// Placa e parede curta: corre na face até a ponta e sai reto.
		foreach (var (name, size) in new[]
		{
			("placa de 1,2 m x 5 cm", new Vector3(1.2f, 1.0f, 0.05f)),
			("parede curta de 1,5 m x 20 cm", new Vector3(1.5f, 3f, 0.2f)),
		})
		{
			PoleRun run = await RunBeside(b, new BoxShape3D { Size = size }, 1.5);
			RegressionBench.Note($"{name}: correu {RegressionBench.F(run.Path)} m na face; terminou em x = {RegressionBench.F(run.End.X)}, z = {RegressionBench.F(run.End.Z)}; na saída ia a ({RegressionBench.F(run.LeaveVelocity.X)}, {RegressionBench.F(run.LeaveVelocity.Z)}) m/s");
			b.Check(run.Grabbed, $"{name}: grudou");
			b.AtLeast(size.X - 0.25f, run.Path, $"{name}: correu a face inteira (m)");
			b.AtLeast(size.X * 0.5f + 1f, run.End.X, $"{name}: passou da ponta e seguiu em frente (x)");
			b.AtLeast(0.0, run.End.Z, $"{name}: do lado em que corria, sem dar a volta na ponta (z)");
			b.AtLeast(2.0, run.LeaveVelocity.X, $"{name}: saiu no mesmo passo, para a frente (m/s)");
		}

		// Mureta de 25 cm (a largura mínima para contornar): contorna. Medida
		// pela face, ela passou a sair reto na ponta (26/11) — de guarda.
		PoleRun wall = await RunBeside(b, new BoxShape3D { Size = new Vector3(3f, 1.2f, 0.25f) }, 3.0);
		RegressionBench.Note($"mureta de 3 m x 25 cm: {wall.On} de {wall.Frames} quadros na parede, volta de {RegressionBench.F(wall.Swept)}°");
		b.Check(wall.On == wall.Frames, "mureta de 25 cm: contorna, sem soltar");

		// O mesmo poste não vira elevador: saltou dele, do outro lado ele não
		// regruda (só depois do chão). Outro poste, sim.
		var pole = await Make(b, new BoxShape3D { Size = new Vector3(0.12f, 8, 0.12f) }, new Vector3(0, 40, 0));
		var other = await Make(b, new BoxShape3D { Size = new Vector3(0.12f, 8, 0.12f) }, new Vector3(6, 40, 0));
		await Land(b);
		await b.Place(new Vector3(-0.36f, 40, 0.42f), -90f, settle: 0.02);
		p.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		b.Check(await b.Until(() => p.IsWallRunning, 0.6), "grudou no poste");
		await b.Seconds(0.3);
		RegressionBench.Press("jump");
		b.Check(await b.Until(() => !p.IsWallRunning, 0.3), "saltou do poste");
		RegressionBench.Release("jump");
		await b.Seconds(0.25);
		// Ainda no ar, sem tocar o chão: do outro lado do mesmo poste, passando rente.
		p.GlobalPosition = new Vector3(-0.36f, 41, -0.42f);
		p.Velocity = new Vector3(3.5f, -0.5f, 0);
		bool again = await b.Until(() => p.IsWallRunning, 0.5);
		b.Check(!again, "do outro lado do mesmo poste, sem tocar o chão, não regruda (antes regrudava: subia o poste como elevador)");
		p.GlobalPosition = new Vector3(5.64f, 41, 0.42f);
		p.Velocity = new Vector3(3.5f, -0.5f, 0);
		b.Check(await b.Until(() => p.IsWallRunning, 0.5), "num outro poste, gruda");
		RegressionBench.ReleaseAll();
		pole.QueueFree();
		other.QueueFree();
	}

	private record struct PoleRun(bool Grabbed, int On, int Frames, float Swept, float Path, float Drop, Vector3 End, Vector3 LeaveVelocity);

	private static async Task<StaticBody3D> Make(RegressionBench b, Shape3D shape, Vector3 at)
	{
		var body = new StaticBody3D { Name = "Objeto" };
		body.AddChild(new CollisionShape3D { Shape = shape });
		b.Scene.AddChild(body);
		body.GlobalPosition = at;
		await b.Frames(2);
		return body;
	}

	/// <summary>Toca o chão: a regra da mesma parede recomeça.</summary>
	private static async Task Land(RegressionBench b)
	{
		await b.Place(new Vector3(-8, 1.2f, 8), -90f, settle: 0.5);
	}

	/// <summary>No ar, ao lado do objeto (+Z), um pouco antes dele, indo para
	/// +X a 3,5 m/s segurando W, por <paramref name="seconds"/>.</summary>
	private static async Task<PoleRun> RunBeside(RegressionBench b, Shape3D shape, double seconds)
	{
		Player p = b.Player;
		var center = new Vector3(0, 40, 0);
		var body = await Make(b, shape, center);
		await Land(b);
		float halfZ = shape is BoxShape3D box ? box.Size.Z * 0.5f : ((CylinderShape3D)shape).Radius;
		float startX = shape is BoxShape3D box2 ? -box2.Size.X * 0.5f - 0.3f : -0.3f;
		await b.Place(center + new Vector3(startX, 0, halfZ + 0.36f), -90f, settle: 0.02);
		p.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");
		bool grabbed = await b.Until(() => p.IsWallRunning, 0.6);
		int on = 0, frames = 0;
		float swept = 0, path = 0, last = Angle(p, center), y0 = p.GlobalPosition.Y, lowest = y0;
		Vector3 prev = p.GlobalPosition, leave = Vector3.Zero;
		bool was = p.IsWallRunning;
		double end = b.Time + seconds;
		while (b.Time < end && p.GlobalPosition.Y > 30)
		{
			await b.PhysicsFrame();
			frames++;
			if (p.IsWallRunning)
			{
				on++;
				path += (p.GlobalPosition - prev).Length();
			}
			else if (was) leave = p.Velocity;
			was = p.IsWallRunning;
			float now = Angle(p, center);
			if (p.IsWallRunning) swept += Mathf.Abs(Mathf.Wrap(now - last, -Mathf.Pi, Mathf.Pi));
			last = now;
			prev = p.GlobalPosition;
			lowest = Mathf.Min(lowest, p.GlobalPosition.Y);
		}
		RegressionBench.ReleaseAll();
		var run = new PoleRun(grabbed, on, frames, Mathf.RadToDeg(swept), path, y0 - lowest, p.GlobalPosition - center, leave);
		body.QueueFree();
		await b.Frames(3);
		return run;
	}

	private static float Angle(Player p, Vector3 c)
	{
		Vector3 d = p.GlobalPosition - c;
		return Mathf.Atan2(d.Z, d.X);
	}
}

/// <summary>
/// Os postes da Cidade (as hastes das placas de rua, 12 cm, e os postes de
/// 28 cm): o lobo gruda e dá a volta. Medido em 26/11 nos 44 postes do mapa,
/// antes → depois: 28 → 44 (os 16 de 12 cm não seguravam: o lobo saía reto
/// no quadro em que grudava). Nos outros mapas (varredura dos objetos finos):
/// Estação 4 → 12 de 12 postes; Distrito Neon, postes 0 → 10 de 18 e placas
/// e muretas 7 → 17 de 30 (o que sobra são grades de mourão e painel com
/// colisão de um lado só, onde a parede "acaba" para o raio); Armazém, 32 →
/// 34 de 36 placas.
/// </summary>
public sealed class WallPolesOnCityTest : RegressionTest
{
	public override string Name => "PostesNaCidade";
	public override string Description => "na Cidade, o lobo gruda nos postes (12 e 28 cm) e dá a volta neles";
	public override string Scene => RegressionBench.Match;

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "cidade", TeamSize = 1 };
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
		var space = p.GetWorld3D().DirectSpaceState;

		// Os postes (centro no chão), e de que lado da rua há lugar para passar rente.
		foreach (var (name, pole, side) in new[]
		{
			("haste de placa de 12 cm (pb1)", new Vector3(-32.6565f, 0, -31.25f), 1f),
			("haste de placa de 12 cm (pb8)", new Vector3(-30.1565f, 0, -26.25f), 1f),
			("poste de 28 cm (pb0)", new Vector3(-41.25f, 0, -31.25f), 1f),
		})
		{
			var flat = new Vector3(pole.X - 0.45f, 0, pole.Z + side * 0.5f);
			var down = PhysicsRayQueryParameters3D.Create(flat + Vector3.Up * 4f, flat + Vector3.Down * 4f);
			down.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
			var hit = space.IntersectRay(down);
			float floor = hit.Count > 0 ? ((Vector3)hit["position"]).Y : 0f;

			await b.Place(new Vector3(flat.X, floor + 1.0f, flat.Z), -90f, settle: 0.6);
			p.GlobalPosition = new Vector3(flat.X, floor + 1.45f, flat.Z);
			p.Velocity = new Vector3(3.5f, 0, 0);
			RegressionBench.Press("move_forward");
			bool grabbed = await b.Until(() => p.IsWallRunning, 0.5);
			float swept = 0, last = Angle(p, pole);
			int off = 0;
			double end = b.Time + 2.0;
			while (grabbed && b.Time < end)
			{
				await b.PhysicsFrame();
				if (!p.IsWallRunning) { off++; break; }
				float now = Angle(p, pole);
				swept += Mathf.Abs(Mathf.Wrap(now - last, -Mathf.Pi, Mathf.Pi));
				last = now;
			}
			RegressionBench.ReleaseAll();
			RegressionBench.Note($"{name}: grudou {grabbed}, volta de {RegressionBench.F(Mathf.RadToDeg(swept))}° em 2 s");
			b.Check(grabbed, $"{name}: grudou");
			b.Check(off == 0, $"{name}: não soltou");
			b.AtLeast(600, Mathf.RadToDeg(swept), $"{name}: deu a volta no poste (graus; antes, a haste de 12 cm: quase nada)");
		}
	}

	private static float Angle(Player p, Vector3 c)
	{
		Vector3 d = p.GlobalPosition - c;
		return Mathf.Atan2(d.Z, d.X);
	}
}
