using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// DESCARTÁVEL: o lobo em cada poste e placa de um mapa (antes/depois).
public sealed class ScratchThinSweepTest : RegressionTest
{
	public override string Name => "ScratchVarreduraFinos";
	public override string Description => "o lobo em cada objeto fino do mapa";
	public override string Scene => RegressionBench.Match;

	public override void Prepare()
	{
		string map = OS.GetEnvironment("MAPA");
		RoomConfig.Settings = new RoomSettings { Map = string.IsNullOrEmpty(map) ? "cidade" : map, TeamSize = 1 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		RoomConfig.Members.Add(new RoomMember { Name = "Bot", Team = Teams.Orange, IsBot = true });
		RoomConfig.Active = true;
	}

	private record struct Prop(string Name, Vector3 Center, Vector3 Size);

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 40);
		foreach (CharacterBody3D bot in b.Bots()) { RegressionBench.Freeze(bot); bot.GlobalPosition += Vector3.Down * 300f; }
		await b.BecomeWolf();
		Player p = b.Player;
		p.WallDebug = OS.GetEnvironment("WALLDEBUG") == "1";
		var space = p.GetWorld3D().DirectSpaceState;
		var capsule = (CapsuleShape3D)p.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
		float half = capsule.Height * 0.5f;

		var props = new List<Prop>();
		var slot = b.Scene.GetNode<Node3D>("Map");
		foreach (CollisionShape3D cs in slot.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>())
		{
			if (cs.Shape == null || cs.Disabled) continue;
			string owner = cs.GetParent()?.Name ?? "";
			if (owner.StartsWith("Barreira") || owner.StartsWith("Limite")) continue;
			Aabb box = WorldBox(cs);
			if (box.Size == Vector3.Zero || box.Size.Y < 0.6f) continue;
			float a = Mathf.Min(box.Size.X, box.Size.Z), c = Mathf.Max(box.Size.X, box.Size.Z);
			if (a > 0.35f || c > 3.5f) continue;
			string only = OS.GetEnvironment("FILTRO");
			if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(owner)) continue;
			string near = OS.GetEnvironment("CENTROS");
			if (!string.IsNullOrEmpty(near))
			{
				Vector3 cc = box.GetCenter();
				bool match = near.Split(';').Any(xz => { var v = xz.Split(','); return new Vector2(cc.X, cc.Z).DistanceTo(new Vector2(float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture))) < 0.3f; });
				if (!match) continue;
			}
			props.Add(new Prop(owner, box.GetCenter(), box.Size));
		}
		if (OS.GetEnvironment("DUMP") == "1")
			foreach (Prop pr in props)
				foreach (CollisionShape3D cs in slot.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>())
				{
					Aabb nb = WorldBox(cs);
					if (nb.Size == Vector3.Zero) continue;
					if (new Vector2(nb.GetCenter().X, nb.GetCenter().Z).DistanceTo(new Vector2(pr.Center.X, pr.Center.Z)) > 2.0f) continue;
					if (nb.Size.X > 6 || nb.Size.Z > 6) continue;
					GD.Print($"VIZINHO de {pr.Center:0.00}: {cs.GetParent()?.Name}/{cs.Name} {cs.Shape.GetType().Name} centro {nb.GetCenter():0.000} tam {nb.Size:0.000} costas {(cs.Shape as ConcavePolygonShape3D)?.BackfaceCollision}");
				}
		int limit = int.TryParse(OS.GetEnvironment("LIMITE"), out int l) ? l : 60;
		if (props.Count > limit)
		{
			int step = Mathf.CeilToInt(props.Count / (float)limit);
			props = props.Where((_, i) => i % step == 0).ToList();
		}

		int tried = 0, grabbedN = 0, okN = 0, lowN = 0, blockedN = 0;
		int poles = 0, polesOk = 0, plates = 0, platesOk = 0;
		foreach (Prop prop in props)
		{
			bool pole = Mathf.Max(prop.Size.X, prop.Size.Z) <= 0.6f;
			// Eixo comprido (a) e fino (t) no chão.
			Vector3 a = prop.Size.X >= prop.Size.Z ? Vector3.Right : Vector3.Back;
			Vector3 t = a == Vector3.Right ? Vector3.Back : Vector3.Right;
			float halfA = (a == Vector3.Right ? prop.Size.X : prop.Size.Z) * 0.5f;
			float halfT = (a == Vector3.Right ? prop.Size.Z : prop.Size.X) * 0.5f;
			float top = prop.Center.Y + prop.Size.Y * 0.5f, bottom = prop.Center.Y - prop.Size.Y * 0.5f;

			Vector3? start = null;
			float floor = 0;
			foreach (float sideSign in new[] { 1f, -1f })
			{
				Vector3 flat = new Vector3(prop.Center.X, 0, prop.Center.Z) - a * (halfA + 0.3f) + t * sideSign * (halfT + 0.37f);
				var down = PhysicsRayQueryParameters3D.Create(new Vector3(flat.X, top + 0.5f, flat.Z), new Vector3(flat.X, top - 30f, flat.Z));
				down.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
				var hit = space.IntersectRay(down);
				if (hit.Count == 0) continue;
				floor = ((Vector3)hit["position"]).Y;
				// Centro do corpo: os pés a 0,55 m do chão, e a faixa da sonda (±0,45) tocando o objeto.
				float y = Mathf.Max(floor + half + 0.55f, bottom + 0.2f);
				if (y - 0.4f > top) { continue; }
				var at = new Vector3(flat.X, y, flat.Z);
				var q = new PhysicsShapeQueryParameters3D { Shape = capsule, Transform = new Transform3D(Basis.Identity, at) };
				q.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
				if (space.IntersectShape(q, 1).Count > 0) continue;
				start = at;
				break;
			}
			if (start == null)
			{
				if (top < floor + half + 0.15f) lowN++; else blockedN++;
				continue;
			}

			tried++;
			if (pole) poles++; else plates++;
			await b.Place(new Vector3(start.Value.X, floor + half + 0.1f, start.Value.Z), 0f, settle: 0.12);
			await b.Until(() => p.IsOnFloor(), 1.0);
			await b.Frames(2);
			p.GlobalPosition = start.Value;
			p.Velocity = a * 3.5f;
			p.Rotation = new Vector3(0, Mathf.Atan2(-a.X, -a.Z), 0);
			RegressionBench.Press("move_forward");
			GD.Print($"INICIO {prop.Name} em {start.Value:0.00}, chão {floor:0.00}");
			bool grabbed = await b.Until(() => p.IsWallRunning, 0.45);
			float swept = 0, path = 0, last = Angle(p, prop.Center);
			Vector3 prev = p.GlobalPosition;
			double end = b.Time + 2.0;
			while (grabbed && b.Time < end && p.IsWallRunning)
			{
				await b.PhysicsFrame();
				if (!p.IsWallRunning) break;
				float now = Angle(p, prop.Center);
				swept += Mathf.Abs(Mathf.Wrap(now - last, -Mathf.Pi, Mathf.Pi));
				last = now;
				path += (p.GlobalPosition - prev).Length();
				prev = p.GlobalPosition;
			}
			RegressionBench.ReleaseAll();
			bool ok = grabbed && (pole ? Mathf.RadToDeg(swept) >= 300f : path >= Mathf.Min(0.6f * halfA * 2f, 1.0f));
			if (grabbed) grabbedN++;
			if (ok) { okN++; if (pole) polesOk++; else platesOk++; }
			GD.Print($"PROP {(pole ? "POSTE" : "PLACA")} {prop.Name} centro {prop.Center:0.0} tam {prop.Size:0.00}: grudou {grabbed}, {RegressionBench.F(path)} m, volta {RegressionBench.F(Mathf.RadToDeg(swept))}°, ok {ok}");
		}
		RegressionBench.Note($"{RoomConfig.Settings.Map}: {props.Count} objetos finos; testados {tried} (baixos demais {lowN}, sem lugar livre {blockedN}); grudou em {grabbedN}; correu/deu a volta em {okN} — postes {polesOk} de {poles}, placas e paredes curtas {platesOk} de {plates}");
	}

	private static float Angle(Player p, Vector3 c)
	{
		Vector3 d = p.GlobalPosition - c;
		return Mathf.Atan2(d.Z, d.X);
	}

	private static Aabb WorldBox(CollisionShape3D cs)
	{
		Transform3D t = cs.GlobalTransform;
		Vector3[] pts;
		switch (cs.Shape)
		{
			case BoxShape3D box:
				Vector3 h = box.Size * 0.5f;
				pts = new[] { new Vector3(-h.X, -h.Y, -h.Z), new Vector3(h.X, h.Y, h.Z), new Vector3(-h.X, h.Y, -h.Z), new Vector3(h.X, -h.Y, h.Z),
					new Vector3(-h.X, -h.Y, h.Z), new Vector3(h.X, h.Y, -h.Z), new Vector3(-h.X, h.Y, h.Z), new Vector3(h.X, -h.Y, -h.Z) };
				break;
			case CylinderShape3D cyl:
				pts = new[] { new Vector3(-cyl.Radius, -cyl.Height / 2, -cyl.Radius), new Vector3(cyl.Radius, cyl.Height / 2, cyl.Radius) };
				break;
			case ConvexPolygonShape3D conv:
				pts = conv.Points;
				break;
			case ConcavePolygonShape3D conc:
				pts = conc.GetFaces();
				break;
			default:
				return new Aabb();
		}
		if (pts.Length == 0) return new Aabb();
		var aabb = new Aabb(t * pts[0], Vector3.Zero);
		foreach (Vector3 q in pts) aabb = aabb.Expand(t * q);
		return aabb;
	}
}
