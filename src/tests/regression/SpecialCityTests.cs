using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O especial no mapa de verdade (Jhon, 25/11, com vídeo na Cidade: o lobo
/// travou num humano a 14 m do outro lado da rua, subiu alto, bateu num poste
/// no meio do caminho, caiu a uns 4 m dele e não acertou).
///
/// Faz o especial como o jogador (Q arma, a trava pega o alvo no centro da
/// tela, o clique salta) em dois tipos de caso:
///   - os do vídeo: um poste de luz no meio do caminho, alvo a 10 e a 13,5 m;
///   - pares sorteados (com semente) pelas ruas da Cidade, de 5 a 13 m, com a
///     rua aberta entre o lobo e o alvo e céu aberto sobre os dois.
/// Mede, em cada salto, se caiu colado no alvo e acertou, quanto subiu acima
/// do caminho, se bateu em algo no ar e se recuou.
/// </summary>
public sealed class SpecialCityTest : RegressionTest
{
	public override string Name => "EspecialNaCidade";
	public override string Description => "na Cidade, o especial travado cai colado no alvo do outro lado da rua (postes, carros e barreiras no meio), sem bater em nada no ar, sem recuar e sem subir à toa";
	public override string Scene => RegressionBench.Match;

	/// <summary>Quantos saltos sorteados.</summary>
	private const int Leaps = 30;


	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = "cidade", TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue });
		foreach (var (name, team) in RoomConfig.DefaultBots())
			RoomConfig.Members.Add(new RoomMember { Name = name, Team = team, IsBot = true });
		RoomConfig.Active = true;
	}

	private readonly record struct Result(string Where, bool Locked, float Gap, float Dealt, float Rise, int Bumps, int Back, float Side, string Hit);

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		await b.Until(() => b.Bots().Length >= 1, 6.0);
		var bots = b.Bots();
		CharacterBody3D target = bots.FirstOrDefault(x => x.GetNode<Health>("Health").Team != b.Health.Team) ?? bots[0];
		target.GetNode<Health>("Health").Team = Teams.Other(b.Health.Team);
		foreach (var other in bots) if (other != target) other.QueueFree();
		RegressionBench.Freeze(target);
		await b.BecomeWolf();
		b.Weapon.SpecialFree = false;

		// 1. Os do vídeo: um poste de luz no meio do caminho (15 cm fora da
		// linha), na calçada, com a rua aberta dos dois lados dele.
		var poles = PoleCases(b, target);
		b.Check(poles.Count == 2, $"achou um poste de luz com rua aberta dos dois lados ({poles.Count} casos)");
		foreach (var (from, to, distance) in poles)
		{
			Result r = await Leap(b, target, from, to);
			string what = $"poste no caminho, alvo a {RegressionBench.F(distance)} m";
			RegressionBench.Note($"{what}: {Describe(r)}");
			b.Check(r.Locked, $"{what}: trava no alvo");
			b.AtMost(1.4, r.Gap, $"{what}: cai colado no alvo (m)");
			b.AtLeast(50, r.Dealt, $"{what}: acerta");
			b.Near(0, r.Bumps, 0.01, $"{what}: não bate no poste nem em nada no ar");
			b.Near(0, r.Back, 0.01, $"{what}: não recua no ar");
		}

		// O alvo travado a 13,8 m que se afasta para 14,6 m antes do clique (a
		// tela dizia "saltar 14 m"): a trava segura e o salto chega nele.
		if (poles.Count > 0)
		{
			var (from, to, _) = poles[1 < poles.Count ? 1 : 0];
			Vector3 away = (to - from) with { Y = 0 };
			Vector3 near = from + away.Normalized() * 13.8f, far = from + away.Normalized() * 14.6f;
			Result r = await Leap(b, target, from, near with { Y = to.Y }, far with { Y = to.Y });
			RegressionBench.Note($"travado a 13,8 m, o alvo vai a 14,6 m antes do clique: {Describe(r)}");
			b.Check(r.Locked, "o alvo que passa de 14 m depois de travado continua travado");
			b.AtMost(1.4, r.Gap, "e o salto cai colado nele (m)");
			b.AtLeast(50, r.Dealt, "e acerta");
		}

		// 2. Sorteados pelas ruas: metade com a rua livre, metade com algo
		// baixo no meio (carro, barreira, caixote, banco) que o salto passa por cima.
		var pairs = Pairs(b, target, Leaps, over: false);
		var overs = Pairs(b, target, Leaps / 2, over: true);
		RegressionBench.Note($"{pairs.Count} saltos sorteados com a rua livre e {overs.Count} com algo baixo no meio");
		b.AtLeast(Leaps / 3, overs.Count, "achou casos com algo baixo no meio");
		var results = new List<Result>();
		string only = OS.GetEnvironment("ESPECIAL_DE");
		foreach (var (from, to) in pairs.Concat(overs))
		{
			if (!string.IsNullOrEmpty(only) && !$"({from.X:0.0}, {from.Z:0.0})".Contains(only)) continue;
			results.Add(await Leap(b, target, from, to));
		}

		var locked = results.Where(r => r.Locked).ToList();
		int hits = locked.Count(r => r.Dealt > 50f);
		int close = locked.Count(r => r.Gap <= 1.4f);
		int bumped = locked.Count(r => r.Bumps > 0);
		int back = locked.Count(r => r.Back > 0);
		float worstRise = locked.Count > 0 ? locked.Max(r => r.Rise) : 0f;
		float worstGap = locked.Count > 0 ? locked.Max(r => r.Gap) : 0f;
		float worstSide = locked.Count > 0 ? locked.Max(r => r.Side) : 0f;
		foreach (var r in locked.Where(r => r.Gap > 1.4f || r.Dealt <= 50f || r.Bumps > 0 || r.Back > 0 || r.Rise > 2f || r.Side > 0.2f))
			RegressionBench.Note($"  {r.Where}: {Describe(r)}");
		RegressionBench.Note($"travou em {locked.Count} de {results.Count}; acertou {hits}; caiu colado {close}; bateu em algo no ar {bumped}; recuou {back}; o mais longe {RegressionBench.F(worstGap)} m; o mais alto {RegressionBench.F(worstRise)} m acima do caminho; o maior desvio de lado {RegressionBench.F(worstSide)} m");

		b.AtLeast(results.Count * 0.9, locked.Count, "a trava pega o alvo à vista");
		b.Near(locked.Count, hits, 0.01, "todo especial travado acerta");
		b.Near(locked.Count, close, 0.01, "e cai colado no alvo (até 1,4 m do centro dele)");
		b.Near(0, bumped, 0.01, "sem bater em nada no ar");
		b.Near(0, back, 0.01, "sem recuar no ar");
		b.AtMost(0.2, worstSide, "sem desviar de lado no caminho (m)");
		float freeRise = results.Take(pairs.Count).Where(r => r.Locked).Select(r => r.Rise).DefaultIfEmpty(0f).Max();
		RegressionBench.Note($"subida: com a rua livre, no máximo {RegressionBench.F(freeRise)} m; passando por cima de algo, no máximo {RegressionBench.F(worstRise)} m");
		b.AtMost(1.6, freeRise, "sem subir à toa: com a rua livre, o mais alto acima do caminho (m)");
		b.AtMost(2.6, worstRise, "por cima de um carro ou barreira, só o que precisa (m)");
	}

	private static string Describe(Result r) =>
		$"{(r.Locked ? "travado" : "SEM TRAVA")}, caiu a {RegressionBench.F(r.Gap)} m, dano {RegressionBench.F(r.Dealt)}, subiu {RegressionBench.F(r.Rise)} m, {r.Bumps} batidas no ar, {r.Back} quadros para trás, {RegressionBench.F(r.Side)} m de lado{(r.Hit != "" ? "; " + r.Hit : "")}";

	/// <summary>Os casos do vídeo: um poste de luz (os de 4,6 m e 12 cm do
	/// mapa) a 7 m do lobo, bem no caminho, e o alvo a 10 e a 13,5 m — com a
	/// rua aberta entre os dois, tirando o próprio poste.</summary>
	private static List<(Vector3 From, Vector3 To, float Distance)> PoleCases(RegressionBench b, CharacterBody3D target)
	{
		var space = b.Player.GetWorld3D().DirectSpaceState;
		foreach (StaticBody3D pole in b.Scene.FindChildren("pb*", "StaticBody3D", true, false).OfType<StaticBody3D>())
		{
			if (pole.GetNodeOrNull<CollisionShape3D>("Colisao")?.Shape is not BoxShape3D box || box.Size.Y < 4f || box.Size.X > 0.2f) continue;
			Vector3 at = pole.GetNode<CollisionShape3D>("Colisao").GlobalPosition;
			var skip = new Godot.Collections.Array<Rid> { b.Player.GetRid(), target.GetRid(), pole.GetRid() };
			foreach (Vector3 dir in new[] { Vector3.Forward, Vector3.Back, Vector3.Left, Vector3.Right })
			{
				var cases = new List<(Vector3, Vector3, float)>();
				Vector3 side = dir.Cross(Vector3.Up) * 0.15f;
				Vector3 from2 = at - dir * 7f;
				foreach (float distance in new[] { 10f, 13.5f })
				{
					Vector3 to2 = at + dir * (distance - 7f) + side;
					if (Floor(space, skip, from2) is not float y0 || Floor(space, skip, to2) is not float y1) break;
					Vector3 from = new(from2.X, y0 + 0.9f, from2.Z), to = new(to2.X, y1 + 0.9f, to2.Z);
					if (!Free(space, skip, from) || !Free(space, skip, to) || !Open(space, skip, from, to)) break;
					cases.Add((from, to, distance));
				}
				if (cases.Count == 2) return cases;
			}
		}
		return new List<(Vector3, Vector3, float)>();
	}

	private static bool Clear(PhysicsDirectSpaceState3D space, Godot.Collections.Array<Rid> skip, Vector3 a, Vector3 c)
	{
		var q = PhysicsRayQueryParameters3D.Create(a, c);
		q.Exclude = skip;
		return space.IntersectRay(q).Count == 0;
	}

	/// <summary>O chão da rua ou da calçada em <paramref name="at"/> (nulo se não é chão baixo).</summary>
	private static float? Floor(PhysicsDirectSpaceState3D space, Godot.Collections.Array<Rid> skip, Vector3 at)
	{
		var q = PhysicsRayQueryParameters3D.Create(new Vector3(at.X, 2.5f, at.Z), new Vector3(at.X, -3f, at.Z));
		q.Exclude = skip;
		var hit = space.IntersectRay(q);
		if (hit.Count == 0) return null;
		float y = ((Vector3)hit["position"]).Y;
		return y is > -0.6f and < 0.6f ? y : null;
	}

	/// <summary>Cabe um corpo ali, com céu aberto em cima.</summary>
	private static bool Free(PhysicsDirectSpaceState3D space, Godot.Collections.Array<Rid> skip, Vector3 at)
	{
		var capsule = new PhysicsShapeQueryParameters3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.75f }, Exclude = skip, Transform = new Transform3D(Basis.Identity, at) };
		return space.IntersectShape(capsule, 1).Count == 0 && Clear(space, skip, at, at + Vector3.Up * 5f);
	}

	/// <summary>De onde o lobo salta há meio metro livre em volta do corpo
	/// (um corpo de 85 cm de raio cabe ali). Na Cidade de 27/11 (a cena de
	/// demonstração do kit) há casas com beiral e toldo baixos: o sorteio caía
	/// a 10 cm de uma parede, debaixo do beiral, e o salto raspava nele ao
	/// sair — o que mede o lugar sorteado, não o especial.</summary>
	private static bool Roomy(PhysicsDirectSpaceState3D space, Godot.Collections.Array<Rid> skip, Vector3 at)
	{
		var wide = new PhysicsShapeQueryParameters3D { Shape = new CylinderShape3D { Radius = 0.85f, Height = 2.6f }, Exclude = skip, Transform = new Transform3D(Basis.Identity, at + Vector3.Up * 0.55f) };
		return space.IntersectShape(wide, 1).Count == 0;
	}

	/// <summary>A rua aberta entre os dois: dos pés à cabeça, no meio e nos
	/// lados do corpo (e não por uma janela ou o balcão de uma banca).</summary>
	private static bool Open(PhysicsDirectSpaceState3D space, Godot.Collections.Array<Rid> skip, Vector3 a, Vector3 c)
	{
		Vector3 side = (c - a).Cross(Vector3.Up).Normalized() * 0.35f;
		foreach (float up in new[] { -0.7f, -0.35f, 0f, 0.4f, 0.8f, 1.3f })
			foreach (Vector3 off in new[] { Vector3.Zero, side, -side })
				if (!Clear(space, skip, a + off + Vector3.Up * up, c + off + Vector3.Up * up)) return false;

		// E o corpo inteiro passa em linha reta, um palmo acima do chão.
		var body = new PhysicsShapeQueryParameters3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.75f }, Exclude = skip };
		int steps = Mathf.CeilToInt(a.DistanceTo(c) / 0.25f);
		for (int k = 0; k <= steps; k++)
		{
			body.Transform = new Transform3D(Basis.Identity, a.Lerp(c, k / (float)steps) + Vector3.Up * 0.25f);
			if (space.IntersectShape(body, 1).Count > 0) return false;
		}
		return true;
	}

	/// <summary>Pares de lobo e alvo no chão das ruas, de 5 a 13 m: a rua
	/// aberta entre os dois (nos pés, no peito e na cabeça, no meio e nos
	/// lados do corpo) e céu aberto sobre os dois.</summary>
	/// <param name="over">Com algo baixo no meio (até 2 m): o corpo não passa
	/// em linha reta rente ao chão, mas passa a 2,2 m.</param>
	private static List<(Vector3 From, Vector3 To)> Pairs(RegressionBench b, CharacterBody3D target, int count, bool over)
	{
		var space = b.Player.GetWorld3D().DirectSpaceState;
		var skip = new Godot.Collections.Array<Rid> { b.Player.GetRid(), target.GetRid() };
		var random = new System.Random(over ? 20261126 : 20261125);
		var pairs = new List<(Vector3, Vector3)>();
		for (int tries = 0; tries < 8000 && pairs.Count < count; tries++)
		{
			float x = (float)(random.NextDouble() * 100 - 50), z = (float)(random.NextDouble() * 70 - 35);
			if (Floor(space, skip, new Vector3(x, 0, z)) is not float y0) continue;
			Vector3 from = new(x, y0 + 0.9f, z);
			if (!Free(space, skip, from) || !Roomy(space, skip, from)) continue;

			float angle = (float)(random.NextDouble() * Mathf.Tau), distance = 5f + (float)random.NextDouble() * 8f;
			float tx = x + Mathf.Cos(angle) * distance, tz = z + Mathf.Sin(angle) * distance;
			if (Floor(space, skip, new Vector3(tx, 0, tz)) is not float y1) continue;
			Vector3 to = new(tx, y1 + 0.9f, tz);
			if (!Free(space, skip, to)) continue;
			if (over ? Open(space, skip, from, to) || !Open(space, skip, from + Vector3.Up * 2.2f, to + Vector3.Up * 2.2f) : !Open(space, skip, from, to)) continue;
			pairs.Add((from, to));
		}

		return pairs;
	}

	/// <param name="moveTo">Para onde o alvo vai depois de travado e antes do clique.</param>
	private static async Task<Result> Leap(RegressionBench b, CharacterBody3D target, Vector3 from, Vector3 to, Vector3? moveTo = null)
	{
		Player p = b.Player;
		Weapon w = b.Weapon;
		string where = $"({from.X:0.0}, {from.Z:0.0}) → ({to.X:0.0}, {to.Z:0.0})";
		target.GlobalPosition = to;
		target.Velocity = Vector3.Zero;
		Vector3 d = to - from;
		await b.Place(from, Mathf.RadToDeg(Mathf.Atan2(-d.X, -d.Z)), -6f, settle: 0.4);

		var health = target.GetNode<Health>("Health");
		health.SetPool(5000f, 0f, refill: true);
		await b.Until(() => !health.IsProtected, 4.0);
		w.FillSpecial();
		await b.Tap("pounce");
		await b.Frames(3);
		if (moveTo is Vector3 later)
		{
			target.GlobalPosition = later;
			await b.Frames(3);
		}
		bool locked = p.LockTarget == target;
		float before = health.Hp;
		await b.Tap("attack");
		await b.Until(() => w.IsPounceFlying, 1.0);

		// A linha da saída até onde o voo vai cair (o lado do alvo que ele escolheu).
		Vector3 start = p.GlobalPosition, last = start;
		Vector3 end = w.PounceFlightEnd;
		Vector3 line = (end - start) with { Y = 0 };
		line = line.LengthSquared() > 0.0001f ? line.Normalized() : Vector3.Forward;
		Vector3 side = line.Cross(Vector3.Up);
		float peak = start.Y, worstSide = 0f;
		int bumps = 0, back = 0, frames = 0;
		string hit = "";
		while (w.IsPounceFlying && frames < 120)
		{
			await b.PhysicsFrame();
			frames++;
			Vector3 pos = p.GlobalPosition;
			peak = Mathf.Max(peak, pos.Y);
			Vector3 moved = (pos - last) with { Y = 0 };
			if (moved.Dot(line) < -0.01f) back++;
			worstSide = Mathf.Max(worstSide, Mathf.Abs((pos - start).Dot(side)));

			// Bater de lado em algo (não o chão, nem o alvo) ainda no ar —
			// acima da altura da queda; o toque da queda no meio-fio não conta,
			// nem o raspão de baixo na quina de cima de uma barreira (a normal
			// para cima: o corpo passa, desviado um ou dois centímetros).
			if (pos.Y - end.Y > 0.25f)
				for (int k = 0; k < p.GetSlideCollisionCount(); k++)
				{
					var c = p.GetSlideCollision(k);
					if (c.GetCollider() == target || c.GetNormal().Y >= 0.5f) continue;
					bumps++;
					if (hit == "" && c.GetCollider() is Node3D n)
						hit = $"bateu em {n.Name} no quadro {frames}, {pos.Y - start.Y:0.00} m acima da saída";
					break;
				}
			last = pos;
		}

		float gap = RegressionBench.Flat(target.GlobalPosition - p.GlobalPosition);
		await b.Until(() => !w.IsRooted, 2.5);
		float rise = peak - Mathf.Max(start.Y, p.GlobalPosition.Y);
		return new Result(where, locked, gap, before - health.Hp, rise, bumps, back, worstSide, hit);
	}
}
