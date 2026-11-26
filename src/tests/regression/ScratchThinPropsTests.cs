using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// DESCARTÁVEL: levantamento dos postes, placas e paredes curtas dos mapas
// (para a corrida na parede em objeto pequeno, 26/11).
public sealed class ScratchThinPropsTest : RegressionTest
{
	public override string Name => "ScratchFinos";
	public override string Description => "levantamento de objetos finos";
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

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 40);
		var slot = b.Scene.GetNodeOrNull<Node3D>("Map");
		var types = new Dictionary<string, int>();
		int thin = 0, poles = 0, plates = 0;
		var samples = new List<string>();
		foreach (CollisionShape3D cs in slot.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>())
		{
			if (cs.Shape == null || cs.Disabled) continue;
			string t = cs.Shape.GetType().Name;
			types[t] = types.GetValueOrDefault(t) + 1;
			Aabb box = WorldBox(cs);
			if (box.Size == Vector3.Zero) continue;
			string owner = cs.GetParent()?.Name ?? "";
			if (owner.StartsWith("Barreira") || owner.StartsWith("Limite")) continue;
			float a = Mathf.Min(box.Size.X, box.Size.Z), c = Mathf.Max(box.Size.X, box.Size.Z);
			if (box.Size.Y < 0.6f) continue;
			if (a > 0.35f) continue;
			if (c > 3.5f) continue;
			thin++;
			bool pole = c <= 0.4f;
			if (pole) poles++; else plates++;
			string body = cs.GetParent()?.Name ?? "?";
			if (samples.Count < 4000)
				samples.Add($"{(pole ? "POSTE" : "PLACA")} {body}/{cs.Name} {t} centro {box.GetCenter():0.00} tam {box.Size:0.00}");
		}
		RegressionBench.Note("tipos: " + string.Join(", ", types.Select(kv => $"{kv.Key} {kv.Value}")));
		RegressionBench.Note($"finos: {thin} (postes {poles}, placas/paredes curtas {plates})");
		foreach (string s in samples) GD.Print("FINO " + s);
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
			case CapsuleShape3D cap:
				pts = new[] { new Vector3(-cap.Radius, -cap.Height / 2, -cap.Radius), new Vector3(cap.Radius, cap.Height / 2, cap.Radius) };
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
		foreach (Vector3 p in pts) aabb = aabb.Expand(t * p);
		return aabb;
	}
}
