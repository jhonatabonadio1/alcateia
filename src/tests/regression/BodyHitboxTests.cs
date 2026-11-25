using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


/// <summary>
/// O que leva tiro é o que se vê (Jhon, 25/11: "ajuste o hitbox dos
/// personagens, deixe mais preciso, exigir mais mira para dar headshot"; "tá
/// muito fácil dar headshot").
///
/// A verdade é a silhueta: a pele deformada pelo esqueleto, na pose do
/// quadro, rasterizada numa grade de 1 cm vista de frente e de lado (raios
/// paralelos). Cada ponto da grade é um tiro: a silhueta diz se ali tem
/// corpo e se é cabeça; o hitbox diz se o tiro acertou e se foi na cabeça.
/// Compara o de antes (a cápsula de movimento e a esfera de 0,55 × a
/// distância entre os ossos da cabeça) com o de agora (<see cref="BodyHitbox"/>).
/// </summary>
public static class SkinSilhouette
{
	public const float Cell = 0.01f;

	/// <summary>A silhueta de um corpo vista na direção <paramref name="dir"/>:
	/// por célula, 0 = vazio, 1 = corpo, 2 = cabeça; e a grade (origem e eixos).</summary>
	public sealed class Grid
	{
		public byte[,] Truth;
		public string[,] Bone;
		public Vector3 Origin, Right, Up, Dir;
		public int W, H;
		public Vector3 Point(int x, int y) => Origin + Right * ((x + 0.5f) * Cell) + Up * ((y + 0.5f) * Cell);
	}

	public static Grid Of(Node3D body, Vector3 dir)
	{
		Skeleton3D skeleton = VisibleSkeleton(body);
		var tris = new List<(Vector3 A, Vector3 B, Vector3 C, bool Head, string Bone)>();
		foreach (MeshInstance3D mesh in skeleton.GetParent().FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (!mesh.IsVisibleInTree() || mesh.Mesh == null || mesh.Skin is not Skin skin || mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) != skeleton) continue;
			var binds = new Transform3D[skin.GetBindCount()];
			var bindHead = new bool[binds.Length];
			var bindName = new string[binds.Length];
			for (int k = 0; k < binds.Length; k++)
			{
				int bone = skin.GetBindBone(k);
				if (bone < 0) bone = skeleton.FindBone(skin.GetBindName(k));
				binds[k] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone) * skin.GetBindPose(k);
				bindHead[k] = IsHead(skeleton, bone);
				bindName[k] = bone >= 0 ? skeleton.GetBoneName(bone) : "?";
			}
			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				var arrays = mesh.Mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				int[] index = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
					? Enumerable.Range(0, verts.Length).ToArray() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
				int per = bones.Length / verts.Length;
				var world = new Vector3[verts.Length];
				var head = new bool[verts.Length];
				var name = new string[verts.Length];
				for (int v = 0; v < verts.Length; v++)
				{
					Vector3 p = Vector3.Zero;
					float strongest = 0f, onHead = 0f;
					for (int j = 0; j < per; j++)
					{
						float w = weights[v * per + j];
						if (w <= 0f) continue;
						int bind = bones[v * per + j];
						p += binds[bind] * verts[v] * w;
						if (bindHead[bind]) onHead += w;
						if (w > strongest) { strongest = w; name[v] = bindName[bind]; }
					}
					// A cabeça é a pele que é quase toda dela (80% do peso), como no
					// BodyHitbox: a emenda com o pescoço e a juba do lobo são corpo.
					head[v] = onHead >= 0.8f;
					world[v] = p;
				}
				for (int i = 0; i + 2 < index.Length; i += 3)
				{
					int a = index[i], b2 = index[i + 1], c = index[i + 2];
					// O rabo do lobo não leva tiro (decisão de 25/11): fica transparente na silhueta.
					if (name[a] != null && name[a].StartsWith("rabo")) continue;
					int heads = (head[a] ? 1 : 0) + (head[b2] ? 1 : 0) + (head[c] ? 1 : 0);
					tris.Add((world[a], world[b2], world[c], heads >= 2, name[a]));
				}
			}
		}

		var grid = new Grid { Dir = dir.Normalized() };
		grid.Up = Vector3.Up;
		grid.Right = grid.Dir.Cross(Vector3.Up).Normalized();
		float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
		foreach (var t in tris)
			foreach (Vector3 p in new[] { t.A, t.B, t.C })
			{
				float x = p.Dot(grid.Right), y = p.Dot(grid.Up);
				x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
			}
		x0 -= 0.2f; y0 -= 0.2f; x1 += 0.2f; y1 += 0.2f;
		grid.W = Mathf.CeilToInt((x1 - x0) / Cell);
		grid.H = Mathf.CeilToInt((y1 - y0) / Cell);
		// A grade fica num plano 6 m antes do corpo, de onde saem os tiros.
		float front = body.GlobalPosition.Dot(grid.Dir) - 6f;
		grid.Origin = grid.Right * x0 + grid.Up * y0 + grid.Dir * front;
		grid.Truth = new byte[grid.W, grid.H];
		grid.Bone = new string[grid.W, grid.H];
		var depth = new float[grid.W, grid.H];
		for (int x = 0; x < grid.W; x++) for (int y = 0; y < grid.H; y++) depth[x, y] = float.MaxValue;

		foreach (var t in tris)
		{
			Vector2 a = new(t.A.Dot(grid.Right) - x0, t.A.Dot(grid.Up) - y0);
			Vector2 b2 = new(t.B.Dot(grid.Right) - x0, t.B.Dot(grid.Up) - y0);
			Vector2 c = new(t.C.Dot(grid.Right) - x0, t.C.Dot(grid.Up) - y0);
			float da = t.A.Dot(grid.Dir), db = t.B.Dot(grid.Dir), dc = t.C.Dot(grid.Dir);
			int gx0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.X, Mathf.Min(b2.X, c.X)) / Cell));
			int gx1 = Mathf.Min(grid.W - 1, Mathf.FloorToInt(Mathf.Max(a.X, Mathf.Max(b2.X, c.X)) / Cell));
			int gy0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.Y, Mathf.Min(b2.Y, c.Y)) / Cell));
			int gy1 = Mathf.Min(grid.H - 1, Mathf.FloorToInt(Mathf.Max(a.Y, Mathf.Max(b2.Y, c.Y)) / Cell));
			float area = (b2 - a).Cross(c - a);
			if (Mathf.Abs(area) < 1e-12f) continue;
			for (int x = gx0; x <= gx1; x++)
				for (int y = gy0; y <= gy1; y++)
				{
					Vector2 p = new((x + 0.5f) * Cell, (y + 0.5f) * Cell);
					float w0 = (b2 - p).Cross(c - p) / area, w1 = (c - p).Cross(a - p) / area, w2 = 1f - w0 - w1;
					if (w0 < 0 || w1 < 0 || w2 < 0) continue;
					float d = w0 * da + w1 * db + w2 * dc;
					if (d >= depth[x, y]) continue;
					depth[x, y] = d;
					grid.Truth[x, y] = (byte)(t.Head ? 2 : 1);
					grid.Bone[x, y] = t.Bone;
				}
		}
		return grid;
	}

	public static Skeleton3D VisibleSkeleton(Node3D body)
	{
		foreach (string root in new[] { "WolfVisual", "Human" })
			if (body.GetNodeOrNull(root) is Node node)
				foreach (Skeleton3D s in node.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>())
					if (s.IsVisibleInTree() && s.FindBone("mixamorig_Head") >= 0) return s;
		return null;
	}

	public static bool IsHead(Skeleton3D skeleton, int bone)
	{
		for (int b = bone; b >= 0; b = skeleton.GetBoneParent(b))
			if (skeleton.GetBoneName(b).EndsWith("Head")) return true;
		return false;
	}

	/// <summary>O hitbox de antes: a cápsula de movimento e a esfera da cabeça
	/// (0,55 × a distância entre os ossos da cabeça e do topo), com a cabeça
	/// valendo até um palmo (0,45 m) dentro da cápsula.</summary>
	public static byte Old(Node3D body, Vector3 from, Vector3 dir)
	{
		var shapeNode = body.GetNode<CollisionShape3D>("CollisionShape3D");
		var cap = (CapsuleShape3D)shapeNode.Shape;
		Transform3D t = shapeNode.GlobalTransform;
		System.Numerics.Vector3 a = N(t * new Vector3(0, -cap.Height * 0.5f + cap.Radius, 0)), b2 = N(t * new Vector3(0, cap.Height * 0.5f - cap.Radius, 0));
		float? body0 = BodyHit.Capsule(N(from), N(dir), a, b2, cap.Radius);

		Skeleton3D s = VisibleSkeleton(body);
		Vector3 head = s.GlobalTransform * s.GetBoneGlobalPose(s.FindBone("mixamorig_Head")).Origin;
		Vector3 top = s.GlobalTransform * s.GetBoneGlobalPose(s.FindBone("mixamorig_HeadTop_End")).Origin;
		var (center, radius) = HeadHit.Sphere(N(head), N(top));
		float? h = HeadHit.Enter(N(from), N(dir), center, radius);
		if (h is float th && HeadHit.Counts(th, body0, body0 != null)) return 2;
		return (byte)(body0 != null ? 1 : 0);
	}

	/// <summary>O hitbox de agora: a peça mais perto que o raio toca.</summary>
	public static byte New(List<BodyHitbox.Part> parts, Vector3 from, Vector3 dir)
	{
		float best = float.MaxValue;
		byte kind = 0;
		foreach (BodyHitbox.Part part in parts)
			if (BodyHit.Enter(N(from), N(dir), part.Shape) is float t && t < best)
			{
				best = t;
				kind = (byte)(part.Head ? 2 : 1);
			}
		return kind;
	}

	/// <summary>Áreas em cm²: [verdade corpo, verdade cabeça, acerto no ar,
	/// corpo que não leva, cabeça sem ser cabeça, cabeça que não conta].</summary>
	public static float[] Compare(Grid g, System.Func<Vector3, byte> hitbox)
	{
		var a = new float[6];
		float cell = Cell * Cell * 1e4f;
		for (int x = 0; x < g.W; x++)
			for (int y = 0; y < g.H; y++)
			{
				byte truth = g.Truth[x, y];
				byte hit = hitbox(g.Point(x, y));
				if (truth > 0) a[0] += cell;
				if (truth == 2) a[1] += cell;
				if (hit > 0 && truth == 0) a[2] += cell;
				if (truth > 0 && hit == 0) a[3] += cell;
				if (hit == 2 && truth != 2) a[4] += cell;
				if (truth == 2 && hit != 2) a[5] += cell;
			}
		return a;
	}

	public static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);

	/// <summary>Um bot do time azul (a Eve): o treino só traz bots laranja.</summary>
	public static async Task<CharacterBody3D> SpawnEve(RegressionBench b)
	{
		var bot = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		bot.Team = Teams.Blue;
		b.Scene.AddChild(bot);
		bot.GlobalPosition = new Vector3(-15, 1, 13);
		await b.Seconds(0.5);
		return bot;
	}
}

public sealed class BodyHitboxTest : RegressionTest
{
	public override string Name => "CorpoPrecisoNoTiro";
	public override string Description => "o tiro acerta o que se vê: a silhueta (pele em grade de 1 cm, de frente e de lado) contra o hitbox de antes e o de agora, na Eve, no Barut e no lobo";

	public override async Task Run(RegressionBench b)
	{
		await b.Seconds(0.3);
		// Um bot de cada time: a Eve (azul) e o Barut (laranja).
		var bots = b.Bots();
		CharacterBody3D eve = bots.FirstOrDefault(x => x.GetNode<Health>("Health").Team == Teams.Blue) ?? await SkinSilhouette.SpawnEve(b);
		CharacterBody3D barut = bots.FirstOrDefault(x => x.GetNode<Health>("Health").Team == Teams.Orange);
		foreach (var bot in bots) if (bot != eve && bot != barut) bot.QueueFree();
		var targets = new List<(string, Node3D)>();
		if (eve != null) targets.Add(("Eve", eve));
		if (barut != null) targets.Add(("Barut", barut));
		int k = 0;
		foreach (var (_, body) in targets)
		{
			RegressionBench.Freeze((CharacterBody3D)body);
			body.GlobalPosition = new Vector3(-15 + 4 * k++, 1, 13);
			body.Rotation = Vector3.Zero;
		}
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 20), 0f, settle: 0.6);
		RegressionBench.Freeze(b.Player);
		targets.Add(("lobo", b.Player));
		await b.Frames(3);

		var parts = new List<BodyHitbox.Part>();
		foreach (var (who, body) in targets)
		{
			b.Check(BodyHitbox.TryGet(body, parts), $"{who}: tem peças de modelo");
			RegressionBench.Note($"{who}: {parts.Count} peças; cabeça {string.Join(" ", parts.Where(p => p.Head).Select(p => $"{p.Shape.Length * 100:0.0} cm de comprimento, corte de {p.Shape.Wide * 200:0.0} × {p.Shape.Deep * 200:0.0} cm"))}");
			var shape = (CapsuleShape3D)body.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
			Skeleton3D s = SkinSilhouette.VisibleSkeleton(body);
			Vector3 hb = s.GlobalTransform * s.GetBoneGlobalPose(s.FindBone("mixamorig_Head")).Origin;
			Vector3 tb = s.GlobalTransform * s.GetBoneGlobalPose(s.FindBone("mixamorig_HeadTop_End")).Origin;
			RegressionBench.Note($"{who}: antes, cápsula de movimento {shape.Radius * 200:0} cm de largura e {shape.Height * 100:0} de altura; esfera da cabeça de raio {HeadHit.Sphere(SkinSilhouette.N(hb), SkinSilhouette.N(tb)).Radius * 100:0.0} cm");

			Vector3 forward = -body.GlobalBasis.Z;
			foreach (var (view, dir) in new[] { ("de frente", -forward), ("de lado", body.GlobalBasis.X) })
			{
				SkinSilhouette.Grid g = SkinSilhouette.Of(body, dir);
				float[] old = SkinSilhouette.Compare(g, p => SkinSilhouette.Old(body, p, g.Dir));
				float[] now = SkinSilhouette.Compare(g, p => SkinSilhouette.New(parts, p, g.Dir));
				RegressionBench.Note($"{who} {view}: silhueta {old[0]:0} cm² (cabeça {old[1]:0}). Antes: acerto no ar {old[2]:0} cm², corpo sem levar {old[3]:0}, cabeça fora da cabeça {old[4]:0}, cabeça sem contar {old[5]:0}. Agora: {now[2]:0}, {now[3]:0}, {now[4]:0}, {now[5]:0}");

				// Medido em 25/11 (Eve, Barut e lobo, de frente e de lado): antes, o
				// ar que levava tiro era 55–90% da silhueta (a cápsula de 60 cm) e a
				// cabeça saía da cabeça visível até 36% da área dela; agora, 11–18% (uma
				// faixa de 1 a 2 cm em volta) e 0–10%. O corpo visível que não leva
				// tiro (bordas, pontas de cabelo e de dedo), 2–8%; a borda da cabeça
				// que não conta como cabeça, 20–30% (a cabeça é mais justa de
				// propósito: exige mira).
				b.AtMost(0.2 * now[0], now[2], $"{who} {view}: ar que leva tiro (cm²)");
				b.AtMost(0.09 * now[0], now[3], $"{who} {view}: corpo visível que não leva tiro (cm²)");
				b.AtMost(0.12 * now[1], now[4], $"{who} {view}: headshot fora da cabeça visível (cm²)");
				b.AtMost(0.35 * now[1], now[5], $"{who} {view}: cabeça visível que não conta como cabeça (cm²)");
				b.AtMost(0.45 * old[2], now[2], $"{who} {view}: ar que leva tiro, contra antes ({old[2]:0} cm²)");
			}
		}

		b.Player.ProcessMode = Node.ProcessModeEnum.Inherit;
		await b.Seconds(0.5);
	}

	/// <summary>Para afinar: por osso, a pele que não leva tiro; por peça, o ar que leva.</summary>
	private static void Diagnose(SkinSilhouette.Grid g, List<BodyHitbox.Part> parts, string who, string view)
	{
		var miss = new Dictionary<string, float>();
		var air = new Dictionary<string, float>();
		for (int x = 0; x < g.W; x++)
			for (int y = 0; y < g.H; y++)
			{
				Vector3 from = g.Point(x, y);
				string hitPart = null;
				float best = float.MaxValue;
				foreach (BodyHitbox.Part part in parts)
					if (BodyHit.Enter(SkinSilhouette.N(from), SkinSilhouette.N(g.Dir), part.Shape) is float t && t < best) { best = t; hitPart = part.Bone; }
				if (g.Truth[x, y] > 0 && hitPart == null) miss[g.Bone[x, y]] = miss.GetValueOrDefault(g.Bone[x, y]) + 1;
				if (g.Truth[x, y] == 0 && hitPart != null) air[hitPart] = air.GetValueOrDefault(hitPart) + 1;
			}
		RegressionBench.Note($"{who} {view}: pele sem tiro por osso: {string.Join(", ", miss.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value:0}"))}");
		RegressionBench.Note($"{who} {view}: ar com tiro por peça: {string.Join(", ", air.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value:0}"))}");
	}
}

/// <summary>
/// Headshot exige mira na cabeça (Jhon, 25/11). O raio da arma (o mesmo
/// <c>Weapon.Trace</c> do tiro, sem a dispersão), de 8 m, na Eve, no Barut e
/// no lobo: no meio da cabeça que se vê, é headshot; 3 cm para fora da
/// silhueta da cabeça, do lado e em cima, não é. Com a esfera de antes (12 cm
/// de raio contra uma cabeça de 8 cm de meia largura), era.
/// </summary>
public sealed class HeadNeedsAimTest : RegressionTest
{
	public override string Name => "CabecaExigeMira";
	public override string Description => "no meio da cabeça que se vê, é headshot; 3 cm fora dela (do lado e em cima), não é — Eve, Barut e lobo";

	private static readonly System.Reflection.MethodInfo TraceMethod =
		typeof(Weapon).GetMethod("Trace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
	private static readonly System.Reflection.MethodInfo TargetsMethod =
		typeof(Weapon).GetMethod("Targets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

	public override async Task Run(RegressionBench b)
	{
		await b.Seconds(0.3);
		var bots = b.Bots();
		CharacterBody3D eve = bots.FirstOrDefault(x => x.GetNode<Health>("Health").Team == Teams.Blue) ?? await SkinSilhouette.SpawnEve(b);
		CharacterBody3D barut = bots.FirstOrDefault(x => x.GetNode<Health>("Health").Team == Teams.Orange);
		foreach (var bot in bots) if (bot != eve && bot != barut) bot.QueueFree();
		b.Check(eve != null && barut != null, "um bot de cada time");
		if (eve == null || barut == null) return;
		RegressionBench.Freeze(eve);
		RegressionBench.Freeze(barut);
		eve.GlobalPosition = new Vector3(-15, 1, 13);
		barut.GlobalPosition = new Vector3(-10, 1, 13);
		eve.Rotation = barut.Rotation = Vector3.Zero;
		await b.BecomeWolf();
		await b.Place(new Vector3(-20, 1, 13), 0f, settle: 0.6);
		RegressionBench.Freeze(b.Player);
		await b.Frames(3);

		// Quem atira é sempre do outro time (o tiro atravessa o aliado).
		Weapon orange = barut.GetNode<Weapon>("Weapon");
		Weapon blue = b.Weapon;
		var all = new Node3D[] { eve, barut, b.Player };
		foreach (var (who, body, gun) in new (string, Node3D, Weapon)[] { ("Eve", eve, orange), ("Barut", barut, blue), ("lobo", b.Player, orange) })
		{
			// Um de cada vez, no mesmo lugar aberto (o caminho do tiro sem caixa nem parede); os outros, longe.
			foreach (Node3D other in all) other.GlobalPosition = other == body ? new Vector3(-15, 1, 13) : new Vector3(-15, -40, 13) + Vector3.Right * System.Array.IndexOf(all, other) * 3f;
			await b.Frames(3);
			// De frente para ele, a 8 m, na altura dos olhos de quem está em pé.
			Vector3 forward = -body.GlobalBasis.Z;
			forward.Y = 0;
			Vector3 from = body.GlobalPosition + forward.Normalized() * 8f + Vector3.Up * 0.6f;
			SkinSilhouette.Grid g = SkinSilhouette.Of(body, (body.GlobalPosition + Vector3.Up * 0.6f - from).Normalized());
			var cells = new List<(int X, int Y)>();
			for (int x = 0; x < g.W; x++) for (int y = 0; y < g.H; y++) if (g.Truth[x, y] == 2) cells.Add((x, y));
			b.Check(cells.Count > 0, $"{who}: a cabeça aparece na silhueta");
			if (cells.Count == 0) continue;
			int cx = (int)cells.Average(c => c.X), cy = (int)cells.Average(c => c.Y);
			// A borda do lado, na altura do meio da cabeça; e o ponto mais alto da
			// cabeça (no lobo, a ponta da orelha).
			int right = cx;
			for (int x = cx; x < g.W; x++) if (g.Truth[x, cy] != 0) right = x;
			var (tx, top) = cells.OrderByDescending(c => c.Y).First();
			// O ponto no plano do corpo, em células a partir da origem da grade.
			float depth = (body.GlobalPosition - g.Origin).Dot(g.Dir);
			Vector3 At(float x, float y) => g.Origin + g.Right * (x * SkinSilhouette.Cell) + g.Up * (y * SkinSilhouette.Cell) + g.Dir * depth;

			foreach (var (point, isHead, what) in new (Vector3, bool, string)[]
			{
				(At(cx + 0.5f, cy + 0.5f), true, "no meio da cabeça"),
				(At(right + 1 + 3f, cy + 0.5f), false, "3 cm do lado da cabeça"),
				(At(tx + 0.5f, top + 1 + 3f), false, "3 cm acima do ponto mais alto da cabeça"),
			})
			{
				Vector3 to = from + (point - from).Normalized() * 30f;
				object targets = TargetsMethod.Invoke(gun, null);
				object[] args = { from, to, targets, true, null, null, null };
				var hit = (Health)TraceMethod.Invoke(gun, args);
				bool headshot = (bool)args[6];
				bool oldHead = SkinSilhouette.Old(body, from, (to - from).Normalized()) == 2;
				RegressionBench.Note($"{who}, {what}: acertou {(hit == null ? "nada" : hit.GetParent().Name)}, headshot {headshot} (com a esfera de antes: {oldHead})");
				if (isHead) b.Check(hit == body.GetNode<Health>("Health") && headshot, $"{who}, {what}: é headshot");
				else b.Check(!headshot, $"{who}, {what}: não é headshot");
			}
		}

		b.Player.ProcessMode = Node.ProcessModeEnum.Inherit;
		await b.Seconds(0.5);
	}
}
