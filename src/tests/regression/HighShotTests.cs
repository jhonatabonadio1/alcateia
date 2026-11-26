using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Tiro de cima (bug do Jhon, 20/11: "em alguns pontos altos atirei no
/// oponente e ele não recebeu dano de jeito nenhum"). Em cada mapa de kit,
/// de cima de vários telhados que dão para a rua, mira-se no peito de um
/// bot parado lá embaixo e atira-se: tem de levar dano.
/// </summary>
public abstract class HighShotTest : RegressionTest
{
	protected abstract string MapId { get; }
	public override string Scene => RegressionBench.Match;
	public override string Description => $"no mapa {Maps.Get(MapId).Name}, de cima dos telhados o tiro no oponente lá embaixo dá dano";

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
		// Este teste mede o tiro, não a queda: o jogador é levado de telhado em
		// telhado e às vezes escorrega da beira até a rua. Desde 26/11 a queda
		// fere (decisão do Jhon) e as escorregadas somadas o matavam no meio da
		// volta; aqui a queda não conta.
		FallProfile fall = FallProfile.Load();
		float safe = fall.HumanSafe;
		fall.HumanSafe = 1000f;
		try { await Shots(b); }
		finally { fall.HumanSafe = safe; }
	}

	private async Task Shots(RegressionBench b)
	{
		b.CloseLoadout();
		await b.Until(() => b.Bots().Length >= 1, 6.0);
		var bots = b.Bots();
		CharacterBody3D bot = bots.FirstOrDefault(x => x.GetNode<Health>("Health").Team != b.Health.Team) ?? bots[0];
		bot.GetNode<Health>("Health").Team = b.Health.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
		foreach (var other in bots) if (other != bot) other.QueueFree();
		RegressionBench.Freeze(bot);
		Health target = bot.GetNode<Health>("Health");
		await b.Seconds(target.SpawnProtection + 0.3);

		Weapon w = b.Weapon;
		w.Equip(Arsenal.Get("ar15"), Arsenal.Get("deagle"), 1);
		w.SelectSlot(Slot.Primary);
		var camera = b.Player.GetNode<Camera3D>("Head/Camera3D");
		var space = b.Player.GetWorld3D().DirectSpaceState;

		// Os telhados: a cada 2,5 m, o primeiro chão de cima para baixo acima de 3 m, com rua (chão a menos de 1 m) a
		// 4–14 m na horizontal e o caminho do olho ao peito livre de parede que se vê.
		var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Zero);
		ray.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid(), bot.GetRid() };
		Vector3? Down(Vector3 at, float top)
		{
			ray.From = new Vector3(at.X, top, at.Z); ray.To = new Vector3(at.X, -2f, at.Z);
			var hit = space.IntersectRay(ray);
			if (hit.Count == 0) return null;
			if (hit["collider"].Obj is StaticBody3D s && (((string)s.Name).StartsWith("Barreira") || ((string)s.Name).StartsWith("Limite"))) return null;
			return (Vector3)hit["position"];
		}

		var point = new PhysicsPointQueryParameters3D();
		bool InBarrier(Vector3 at)
		{
			point.Position = at;
			foreach (var hit in space.IntersectPoint(point, 8))
				if (hit["collider"].Obj is StaticBody3D s && (((string)s.Name).StartsWith("Barreira") || ((string)s.Name).StartsWith("Limite"))) return true;
			return false;
		}

		// O que se vê do mapa vira colisão numa camada só dela (a 20), para saber, quando o tiro para, se parou em
		// alguma coisa que se vê ou numa caixa escondida.
		const uint Visible = 1u << 19;
		Node mapRoot = b.Scene.GetNodeOrNull("Map") ?? b.Scene;
		int meshes = 0;
		foreach (MeshInstance3D mi in mapRoot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (!mi.IsVisibleInTree() || mi.Mesh == null) continue;
			var body = new StaticBody3D { CollisionLayer = Visible, CollisionMask = 0 };
			body.AddChild(new CollisionShape3D { Shape = mi.Mesh.CreateTrimeshShape() });
			mi.AddChild(body);
			meshes++;
		}
		await b.Frames(2);
		float VisibleAt(Vector3 from, Vector3 to)
		{
			var q = PhysicsRayQueryParameters3D.Create(from, to, Visible);
			var hit = space.IntersectRay(q);
			return hit.Count == 0 ? float.MaxValue : ((Vector3)hit["position"]).DistanceTo(from);
		}

		Rect2 area = MapArea(b);
		float top = (b.Scene.FindChild("LimiteTeto", true, false) as Node3D)?.GlobalPosition.Y - 0.7f ?? 20.5f;
		var spots = new List<(Vector3 roof, Vector3 street)>();
		var rng = new RandomNumberGenerator { Seed = 7 };
		for (float x = area.Position.X + 2; x < area.End.X - 2 && spots.Count < 400; x += 2.5f)
			for (float z = area.Position.Y + 2; z < area.End.Y - 2; z += 2.5f)
			{
				Vector3? roof = Down(new Vector3(x, 0, z), Mathf.Min(top, 20.5f));
				if (roof is not Vector3 r || r.Y < 3f || r.Y > 24f) continue;
				float ang = rng.Randf() * Mathf.Tau, dist = rng.RandfRange(4f, 14f);
				Vector3? street = Down(r + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * dist, r.Y - 1.5f);
				if (street is not Vector3 s || s.Y > 1f || !area.Grow(-1f).HasPoint(new Vector2(s.X, s.Z))) continue;
				if (InBarrier(r + Vector3.Up * 1.0f) || InBarrier(s + Vector3.Up * 1.0f)) continue;   // onde não se chega
				spots.Add((r, s));
			}
		RegressionBench.Note($"{spots.Count} telhados com rua embaixo");

		int tried = 0, hurt = 0, blocked = 0;
		var misses = new List<string>();
		var hidden = new List<string>();
		foreach (var (roof, street) in spots.OrderBy(_ => rng.Randi()).Take(120))
		{
			await b.Place(roof + Vector3.Up * 1.1f, 0f, settle: 0.35);
			if (b.Player.GlobalPosition.Y < roof.Y - 0.5f) continue;              // não ficou em cima
			// (a origem do corpo é o meio da cápsula: o pé fica meia altura abaixo)
			var capsule = bot.GetChildren().OfType<CollisionShape3D>().First();
			float half = capsule.Shape is CapsuleShape3D cap ? cap.Height / 2f : 0.9f;
			bot.GlobalPosition = street + Vector3.Up * (half - capsule.Position.Y + 0.03f);
			await b.Frames(2);
			Vector3 chest = capsule.GlobalPosition + Vector3.Up * half * 0.35f;

			// O caminho do olho ao peito: o que a física vê (para dizer o que segurou o tiro).
			ray.From = camera.GlobalPosition; ray.To = chest;
			ray.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid() };
			var path = space.IntersectRay(ray);
			string first = path.Count == 0 ? "nada" : (path["collider"].Obj as Node)?.Name ?? "?";
			bool clear = path.Count > 0 && path["collider"].Obj == bot;
			if (!clear && path.Count > 0)
			{
				// Parou antes do bot: numa coisa que se vê ali (parede de verdade — passa) ou em nada que se veja (o defeito).
				float at = ((Vector3)path["position"]).DistanceTo(camera.GlobalPosition);
				if (VisibleAt(camera.GlobalPosition, chest) <= at + 0.25f) { blocked++; continue; }
				hidden.Add($"({roof.X:0.0}, {roof.Y:0.0}, {roof.Z:0.0}) → ({street.X:0.0}, {street.Z:0.0}): caixa invisível '{first}' a {at:0.0} m");
				continue;
			}

			tried++;
			Aim(b.Player, camera, chest);
			await b.Frames(2);
			target.SetHp(target.MaxHp);
			RegressionBench.Press("attack");
			await b.Frames(1);
			RegressionBench.Release("attack");
			await b.Frames(3);
			if (target.Hp < target.MaxHp) hurt++;
			else misses.Add($"({roof.X:0.0}, {roof.Y:0.0}, {roof.Z:0.0}) → ({street.X:0.0}, {street.Z:0.0}): segurou em '{first}'{(path.Count > 0 ? $" a {((Vector3)path["position"]).DistanceTo(camera.GlobalPosition):0.0} m" : "")}");
			RegressionBench.ReleaseAll();
			await b.Seconds(0.12);
		}
		RegressionBench.Note($"{meshes} malhas do mapa; {tried} tiros de cima, {hurt} com dano; {blocked} com parede que se vê no caminho; {hidden.Count} parados em caixa invisível");
		foreach (string m in hidden.Take(12)) RegressionBench.Note("invisível: " + m);
		foreach (string m in misses.Take(8)) RegressionBench.Note("sem dano: " + m);
		b.Check(tried >= 8, $"testou pelo menos 8 telhados ({tried})");
		b.Check(hurt >= tried * 0.9f, $"o tiro de cima no peito dá dano ({hurt} de {tried}; a dispersão da arma pode tirar um ou outro)");
		b.Check(hidden.Count == 0, $"nenhum tiro para em colisão que não se vê ({hidden.Count})");
		RoomConfig.Active = false;
	}

	private static Rect2 MapArea(RegressionBench b)
	{
		var limits = b.Scene.FindChildren("Limite*", "StaticBody3D", true, false).OfType<Node3D>().Select(n => n.GlobalPosition).ToList();
		if (limits.Count < 2) return new Rect2(-60, -45, 120, 90);
		float x0 = limits.Min(p => p.X), x1 = limits.Max(p => p.X), z0 = limits.Min(p => p.Z), z1 = limits.Max(p => p.Z);
		return new Rect2(x0, z0, x1 - x0, z1 - z0);
	}

	private static void Aim(CharacterBody3D shooter, Camera3D camera, Vector3 point)
	{
		Vector3 direction = (point - camera.GlobalPosition).Normalized();
		shooter.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
		shooter.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.Asin(direction.Y), 0, 0);
	}
}

public sealed class CidadeHighShotTest : HighShotTest { public override string Name => "TiroDeCimaCidade"; protected override string MapId => "cidade"; }
public sealed class DesertoHighShotTest : HighShotTest { public override string Name => "TiroDeCimaDeserto"; protected override string MapId => "deserto"; }
public sealed class EstacaoHighShotTest : HighShotTest { public override string Name => "TiroDeCimaEstacao"; protected override string MapId => "estacao"; }
public sealed class ObraHighShotTest : HighShotTest { public override string Name => "TiroDeCimaObra"; protected override string MapId => "obra"; }
