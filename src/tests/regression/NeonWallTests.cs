using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O Distrito Neon fecha no muro da cidade (Jhon, 25/11: "tô conseguindo sair
/// pelos muros da cidade com o lobo; o mapa é delimitado por este muro, não
/// pelo que tem no lado de fora"). As paredes Limite… ficam na borda do mapa,
/// de 10 a 20 m além do muro e dos prédios do anel: o lobo subia o muro (o
/// salto carregado sobe até 6,2 m) e andava lá fora. Agora tudo o que fica do
/// lado de fora é a parede invisível <c>BarreiraMuro</c>, do chão ao teto
/// (src/tools/mapas/muro_da_cidade.gd).
///
/// O teste confere que nada de onde se anda ficou tapado (onde se nasce, as
/// rotas dos bots, as escadas), que a volta do mapa e o alto do muro estão
/// fechados até o teto, e põe o lobo para fugir: de pontos encostados no muro
/// em volta da cidade, o salto carregado mirado no alto do muro (ou do telhado
/// de fora) e, em seguida, pular e correr contra ele. Primeiro sem a barreira
/// (o controle: tem de sair, senão o teste não mede nada), depois com ela.
/// </summary>
public sealed class NeonCityWallTest : RegressionTest
{
	public override string Name => "NeonMuroDaCidade";
	public override string Description => "no Neon o lobo não sai pelo muro da cidade (salto carregado, pulo, corrida e salto da parede): o lado de fora é barrado até o teto, e nada de onde se anda ficou tapado";
	public override string Scene => RegressionBench.Match;

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "neon", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Active = true;
	}

	private sealed class Escape
	{
		public Vector3 Start, Target, FacePoint, Out;
	}

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		b.RemoveBots();
		await b.Seconds(0.5);

		var wall = b.Scene.FindChildren("BarreiraMuro", nameof(StaticBody3D), true, false).OfType<StaticBody3D>().FirstOrDefault();
		b.Check(wall != null, "o mapa tem a barreira do muro da cidade");
		if (wall == null) { RoomConfig.Active = false; return; }
		CollisionShape3D[] shapes = wall.GetChildren().OfType<CollisionShape3D>().ToArray();
		// Cada caixa: a planta e a altura de baixo (do chão no lado de fora; acima
		// do carro e do telhado da sala que encostam no muro).
		var rects = shapes.Select(s =>
		{
			Vector3 size = ((BoxShape3D)s.Shape).Size, c = s.GlobalPosition;
			return (Plan: new Rect2(c.X - size.X / 2, c.Z - size.Z / 2, size.X, size.Z), Bottom: c.Y - size.Y / 2);
		}).ToArray();
		b.Check(shapes.Length > 100 && wall.HasMeta("mapa_fora"), $"a barreira cobre o lado de fora em caixas ({shapes.Length}), fora do minimapa");
		// Fora da cidade: dentro da planta de uma caixa e na altura dela (o corpo
		// tem 1,8 m; o ponto é o meio dele).
		bool Outside(Vector3 p) => rects.Any(r => r.Plan.HasPoint(new Vector2(p.X, p.Z)) && p.Y + 0.9f > r.Bottom);
		int capped = rects.Count(r => r.Bottom > 0f);
		RegressionBench.Note($"{shapes.Length} caixas: {shapes.Length - capped} do chão ao teto, {capped} por cima do que é baixo e encosta no muro");

		PhysicsDirectSpaceState3D space = wall.GetWorld3D().DirectSpaceState;

		// 1. Nada de onde se anda ficou tapado: onde se nasce, as rotas dos bots
		// e o alto das escadas.
		var walk = SpawnPoints.PointsFor(Teams.Blue).Concat(SpawnPoints.PointsFor(Teams.Orange)).ToList();
		for (int r = 0; r < PatrolRoutes.RouteCount; r++)
			for (int i = 0; i < PatrolRoutes.Length(r); i++) walk.Add(PatrolRoutes.Point(r, i));
		foreach (StaticBody3D ramp in b.Scene.FindChildren("pr*", nameof(StaticBody3D), true, false).OfType<StaticBody3D>())
			walk.Add(ramp.GlobalPosition);
		int covered = walk.Count(Outside);
		RegressionBench.Note($"lugares de andar conferidos: {walk.Count} (nascer, rotas, escadas); dentro da barreira: {covered}");
		b.Check(walk.Count > 40 && covered == 0, $"nenhum lugar de andar dentro da barreira ({covered} de {walk.Count}: nascer, rotas, escadas)");

		// 2. A volta do mapa e o alto do muro: barrados do chão ao teto.
		Aabb box = MapBounds.Of(b.GetTree());
		int open = 0, points = 0;
		foreach (float y in new[] { 2f, 15f, box.End.Y - 1f })
			for (int i = 0; i <= 60; i++)
			{
				float x = Mathf.Lerp(box.Position.X + 1.3f, box.End.X - 1.3f, i / 60f);
				float z = Mathf.Lerp(box.Position.Z + 1.3f, box.End.Z - 1.3f, i / 60f);
				foreach (Vector3 point in new[] { new Vector3(x, y, box.Position.Z + 1.3f), new Vector3(x, y, box.End.Z - 1.3f),
					         new Vector3(box.Position.X + 1.3f, y, z), new Vector3(box.End.X - 1.3f, y, z) })
				{
					points++;
					var query = new PhysicsPointQueryParameters3D { Position = point, CollideWithBodies = true };
					if (space.IntersectPoint(query, 8).Any(hit => hit["collider"].Obj == wall)) continue;
					if (open++ < 6) RegressionBench.Note($"aberto: ({point.X:0.0}, {point.Y:0}, {point.Z:0.0}){(Outside(point) ? "" : " — chão da cidade")}");
				}
			}
		b.Check(open == 0, $"a volta do mapa é barrada do chão ao teto ({open} de {points} pontos abertos)");

		// Do meio da cidade para fora, em 36 direções, a 8 m e rente ao teto: a
		// primeira parede invisível no caminho é a do muro (antes da Limite…).
		int leaks = 0, rays = 0;
		foreach (float y in new[] { 8f, box.End.Y - 1f })
			for (int i = 0; i < 36; i++)
			{
				rays++;
				Vector3 dir = new Vector3(Mathf.Cos(i * Mathf.Tau / 36), 0, Mathf.Sin(i * Mathf.Tau / 36));
				if (!FirstInvisible(space, new Vector3(0, y, 0), dir * 200f, wall)) leaks++;
			}
		b.Check(leaks == 0, $"de dentro para fora, por cima de tudo, o muro barra antes da borda do mapa ({leaks} de {rays} raios passaram)");

		// 3. O lobo tenta sair.
		await b.BecomeWolf();
		b.Weapon.SpecialFree = true;
		List<Escape> tries = Candidates(space, shapes, wall, Outside, box);
		RegressionBench.Note($"{tries.Count} pontos de fuga em volta da cidade: " + string.Join(" ", tries.Select(t => $"({t.Start.X:0},{t.Start.Z:0})→{t.Target.Y:0.0}m")));
		b.Check(tries.Count >= 6, $"achou pontos encostados no muro, com o alto dele ao alcance do salto ({tries.Count})");

		int escapedWithout = 0, escapedWith = 0, runWithout = 0, runWith = 0;
		foreach (bool barred in new[] { false, true })
		{
			foreach (CollisionShape3D s in shapes) s.Disabled = !barred;
			await b.PhysicsFrame();
			await b.PhysicsFrame();
			foreach (Escape t in tries)
			{
				bool fled = await TryLeap(b, t, Outside);
				var (ran, fledWall) = await TryWallRun(b, t, Outside);
				fled |= fledWall;
				RegressionBench.Note($"{(barred ? "com" : "sem")} barreira, ({t.Start.X:0},{t.Start.Z:0}) → alto a {t.Target.Y:0.0} m: {(fled ? "SAIU" : "ficou")}, {(ran ? "correu no muro" : "não correu")}; parou em ({b.Player.GlobalPosition.X:0.0}, {b.Player.GlobalPosition.Y:0.0}, {b.Player.GlobalPosition.Z:0.0})");
				if (barred) { escapedWith += fled ? 1 : 0; runWith += ran ? 1 : 0; }
				else { escapedWithout += fled ? 1 : 0; runWithout += ran ? 1 : 0; }
			}
		}
		RegressionBench.Note($"sem a barreira: o lobo saiu em {escapedWithout} de {tries.Count} e correu no muro em {runWithout}; com ela: saiu em {escapedWith}, correu em {runWith}");
		b.Check(escapedWithout > 0, $"controle: sem a barreira o lobo sai por cima do muro ({escapedWithout} de {tries.Count})");
		b.Check(escapedWith == 0, $"com a barreira o lobo fica na cidade ({escapedWith} de {tries.Count} saíram)");
		// Correr no muro de dentro continua (a barreira fica atrás da face do
		// térreo; onde o andar de cima avança sobre a rua, a fachada dele é de
		// correr): o que conta é não passar para o outro lado.

		RegressionBench.ReleaseAll();
		RoomConfig.Active = false;
	}

	/// <summary>A primeira parede invisível que o raio encontra é a barreira do muro.</summary>
	private static bool FirstInvisible(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 along, StaticBody3D wall)
	{
		var exclude = new Godot.Collections.Array<Rid>();
		for (int k = 0; k < 64; k++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, from + along);
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return false;
			if (hit["collider"].Obj == wall) return true;
			if (Player.IsInvisibleLimit(hit["collider"].Obj)) return false;
			exclude.Add((Rid)hit["rid"]);
		}
		return false;
	}

	/// <summary>
	/// Pontos de fuga: a face da barreira que dá para a cidade, com chão livre
	/// na frente (onde o lobo fica) e, logo atrás dela, o alto do muro ou de um
	/// telhado de fora a até 6 m (onde o salto carregado o poria). Os mais
	/// espalhados, até 8.
	/// </summary>
	private static List<Escape> Candidates(PhysicsDirectSpaceState3D space, CollisionShape3D[] shapes, StaticBody3D wall, System.Func<Vector3, bool> outside, Aabb bounds)
	{
		var body = new BoxShape3D { Size = new Vector3(0.7f, 1.3f, 0.7f) };
		var found = new List<Escape>();
		var noWall = new Godot.Collections.Array<Rid> { wall.GetRid() };
		foreach (CollisionShape3D s in shapes)
		{
			Vector3 size = ((BoxShape3D)s.Shape).Size, c = s.GlobalPosition;
			foreach (Vector3 m in new[] { Vector3.Right, Vector3.Left, Vector3.Back, Vector3.Forward })
			{
				float half = Mathf.Abs(m.X) > 0 ? size.X / 2 : size.Z / 2;
				float length = Mathf.Abs(m.X) > 0 ? size.Z : size.X;
				if (length < 2f) continue;
				Vector3 face = new Vector3(c.X, 0, c.Z) + m * half;
				Vector3 start = face + m * 2.5f;
				if (outside(start) || outside(face + m * 0.5f)) continue;
				if (start.X < bounds.Position.X + 2f || start.X > bounds.End.X - 2f || start.Z < bounds.Position.Z + 2f || start.Z > bounds.End.Z - 2f) continue;
				var down = space.IntersectRay(PhysicsRayQueryParameters3D.Create(start + Vector3.Up * 1.0f, start + Vector3.Down * 1.0f));
				if (down.Count == 0 || ((Vector3)down["position"]).Y > 0.4f) continue;
				var fit = new PhysicsShapeQueryParameters3D { Shape = body, Transform = new Transform3D(Basis.Identity, start + Vector3.Up * 1.0f) };
				if (space.IntersectShape(fit, 1).Count > 0) continue;
				// O alto do lado de fora, logo atrás da face, sem contar a barreira.
				for (float t = 0.6f; t <= 4.2f; t += 0.6f)
				{
					Vector3 r = face - m * t;
					var top = PhysicsRayQueryParameters3D.Create(new Vector3(r.X, 29f, r.Z), new Vector3(r.X, -1f, r.Z));
					top.Exclude = noWall;
					var hit = space.IntersectRay(top);
					if (hit.Count == 0) continue;
					float y = ((Vector3)hit["position"]).Y;
					if (y < 1.2f || y > 6.0f || ((Vector3)hit["normal"]).Y < 0.7f) continue;
					found.Add(new Escape { Start = start + Vector3.Up * 1.0f, Target = new Vector3(r.X, y, r.Z), FacePoint = face, Out = -m });
					break;
				}
			}
		}
		// os mais espalhados
		var picked = new List<Escape>();
		if (found.Count == 0) return picked;
		picked.Add(found[0]);
		while (picked.Count < 8 && picked.Count < found.Count)
			picked.Add(found.OrderByDescending(f => picked.Min(p => p.Start.DistanceTo(f.Start))).First());
		return picked;
	}

	/// <summary>O salto carregado mirado no alto do lado de fora e, depois,
	/// pular e andar contra o muro. Verdadeiro se o lobo esteve fora da cidade.</summary>
	private static async Task<bool> TryLeap(RegressionBench b, Escape t, System.Func<Vector3, bool> outside)
	{
		Player p = b.Player;
		Vector3 flat = t.Target - t.Start;
		flat.Y = 0;
		await b.Place(t.Start, Mathf.RadToDeg(Mathf.Atan2(-flat.X, -flat.Z)), 0f, settle: 0.4);
		// a mira: a câmera de terceira pessoa olhando o ponto
		for (int i = 0; i < 4; i++)
		{
			Vector3 d = t.Target - p.AimCamera.GlobalPosition;
			p.Rotation = new Vector3(0, Mathf.Atan2(-d.X, -d.Z), 0);
			p.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.Clamp(Mathf.Asin(d.Normalized().Y), -1.3f, 1.3f), 0, 0);
			await b.Frames(2);
		}
		bool fled = false;
		b.Weapon.BotPounce(null, 1f);
		for (int f = 0; f < 150; f++)
		{
			await b.PhysicsFrame();
			fled |= outside(p.GlobalPosition);
		}
		// pular e andar contra o muro
		RegressionBench.Press("move_forward");
		for (int f = 0; f < 120; f++)
		{
			if (f % 20 == 0) RegressionBench.Press("jump");
			else if (f % 20 == 3) RegressionBench.Release("jump");
			await b.PhysicsFrame();
			fled |= outside(p.GlobalPosition);
		}
		RegressionBench.ReleaseAll();
		return fled;
	}

	/// <summary>No ar, rente ao muro, andando ao longo dele: o lobo gruda?</summary>
	private static async Task<(bool Ran, bool Fled)> TryWallRun(RegressionBench b, Escape t, System.Func<Vector3, bool> outside)
	{
		Player p = b.Player;
		Vector3 along = t.Out.Cross(Vector3.Up).Normalized();
		Vector3 at = t.FacePoint - t.Out * 0.5f + Vector3.Up * 2.6f;
		await b.Place(at, Mathf.RadToDeg(Mathf.Atan2(-along.X, -along.Z)), 0f, settle: 0.02);
		p.Velocity = along * 3.5f;
		RegressionBench.Press("move_forward");
		bool grabbed = await b.Until(() => p.IsWallRunning, 0.6);
		if (grabbed) RegressionBench.Note($"  correu no muro em ({p.GlobalPosition.X:0.0}, {p.GlobalPosition.Y:0.0}, {p.GlobalPosition.Z:0.0}): parede {WallName(p)}");
		// O salto da parede, para fora e para cima, e de novo contra o muro.
		bool fled = outside(p.GlobalPosition);
		if (grabbed)
		{
			await b.Tap("jump");
			for (int f = 0; f < 90; f++)
			{
				if (f == 30) { p.Rotation = new Vector3(0, Mathf.Atan2(t.Out.X, t.Out.Z), 0); RegressionBench.Press("jump"); }
				if (f == 34) RegressionBench.Release("jump");
				await b.PhysicsFrame();
				fled |= outside(p.GlobalPosition);
			}
		}
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);
		return (grabbed, fled);
	}

	private static string WallName(Player p)
	{
		var rid = (Rid)typeof(Player).GetField("_wallBody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(p);
		var node = GodotObject.InstanceFromId(PhysicsServer3D.BodyGetObjectInstanceId(rid)) as Node;
		return node == null ? "?" : node.Name.ToString().StartsWith("StaticBody") ? node.GetParent()?.Name.ToString() : node.Name.ToString();
	}
}

/// <summary>
/// Os fios do Neon não têm colisão (Jhon, 25/11: "os fios do mapa neon não
/// podem ter colisão (fios das torres de energia)"): os que cruzam a rua de
/// dentro dos quarteirões, de prédio a prédio e das antenas
/// (<c>SM_Buildings_Part_24</c> e <c>_25</c>), seguravam a bala e o corpo. A
/// antena (<c>SM_Buildings_Part_31</c>) continua sólida.
/// </summary>
public sealed class NeonWiresTest : RegressionTest
{
	public override string Name => "NeonFiosSemColisao";
	public override string Description => "no Neon a bala e o corpo atravessam os fios entre os prédios; a antena continua sólida";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var map = GD.Load<PackedScene>("res://src/scenes/maps/neon.tscn").Instantiate<Node3D>();
		map.Position = new Vector3(3000, 0, 0);
		b.AddChild(map);
		await b.PhysicsFrame();
		await b.PhysicsFrame();
		PhysicsDirectSpaceState3D space = map.GetWorld3D().DirectSpaceState;

		MeshInstance3D[] wires = map.FindChildren("SM_Buildings_Part_2*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
			.Where(m => m.Name.ToString().StartsWith("SM_Buildings_Part_24") || m.Name.ToString().StartsWith("SM_Buildings_Part_25")).ToArray();
		b.Check(wires.Length == 14, $"os fios do mapa ({wires.Length} de 14)");

		var ball = new SphereShape3D { Radius = 0.3f };
		int shots = 0, stopped = 0, touched = 0;
		foreach (MeshInstance3D wire in wires)
		{
			Aabb box = wire.GetAabb();
			// a barriga do fio: a parte de baixo, no meio do comprimento
			Vector3 mid = wire.GlobalTransform * new Vector3(box.GetCenter().X, box.Position.Y + 0.05f, box.GetCenter().Z);
			Vector3 low = LowestNear(wire, mid);
			foreach (Vector3 axis in new[] { Vector3.Up, Vector3.Right, Vector3.Back })
			{
				shots++;
				var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(low - axis * 0.6f, low + axis * 0.6f));
				if (hit.Count > 0) stopped++;
			}
			var query = new PhysicsShapeQueryParameters3D { Shape = ball, Transform = new Transform3D(Basis.Identity, low) };
			if (space.IntersectShape(query, 1).Count > 0) touched++;
		}
		RegressionBench.Note($"fios: {wires.Length}; tiros cruzando o fio que pararam: {stopped} de {shots}; corpo (esfera de 30 cm) encostando no fio: {touched}");
		b.Check(stopped == 0, $"a bala atravessa o fio ({stopped} de {shots} pararam)");
		b.Check(touched == 0, $"o corpo atravessa o fio ({touched} de {wires.Length} encostaram)");

		// A antena (o mastro de onde os fios saem) continua sólida.
		MeshInstance3D[] masts = map.FindChildren("SM_Buildings_Part_31*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
		int mastHits = 0;
		foreach (MeshInstance3D mast in masts)
		{
			Vector3 c = mast.GlobalTransform * mast.GetAabb().GetCenter();
			var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(c - Vector3.Right * 1f, c + Vector3.Right * 1f));
			var hit2 = space.IntersectRay(PhysicsRayQueryParameters3D.Create(c - Vector3.Back * 1f, c + Vector3.Back * 1f));
			if (hit.Count > 0 || hit2.Count > 0) mastHits++;
		}
		RegressionBench.Note($"antenas: {masts.Length}; sólidas: {mastHits}");
		b.Check(masts.Length > 0 && mastHits == masts.Length, $"a antena continua sólida ({mastHits} de {masts.Length})");

		map.QueueFree();
		await b.PhysicsFrame();
	}

	/// <summary>O ponto mais baixo da malha do fio perto de <paramref name="near"/> (a 1 m na horizontal).</summary>
	private static Vector3 LowestNear(MeshInstance3D wire, Vector3 near)
	{
		Vector3 best = near;
		float lowest = float.MaxValue;
		foreach (Vector3 v in wire.Mesh.GetFaces())
		{
			Vector3 w = wire.GlobalTransform * v;
			if (new Vector2(w.X - near.X, w.Z - near.Z).Length() > 1f) continue;
			if (w.Y < lowest) { lowest = w.Y; best = w; }
		}
		return best + Vector3.Up * 0.04f;
	}
}
