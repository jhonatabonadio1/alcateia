using Godot;
using System.Threading.Tasks;

/// <summary>
/// Parede e barreira protegem da granada (Jhon, 26/11: "área de efeito da
/// granada atravessa paredes, não deve acontecer mais"; "granada forte demais:
/// deixar mais balanceada"). A explosão confere se enxerga o corpo
/// (<see cref="Grenade.Exposed"/>, os mesmos pontos que o servidor confere,
/// <see cref="HitRules.TargetPoints"/>, com uma bola de 6 cm: fresta fina não
/// deixa passar), e o centro tira 300 (era 420): mata um humano de vida cheia
/// só até 3 m. Até 26/11 a explosão não conferia nada: tirava o dano cheio
/// atrás de parede, de barreira e de fresta.
/// </summary>
public sealed class GrenadeWallTest : RegressionTest
{
	public override string Name => "GranadaParede";
	public override string Description => "a granada não fere atrás de parede, de barreira nem por uma fresta de 4 cm; por cima de um muro baixo, fere; 300 no centro, mata só até 3 m";

	private static readonly Vector3 Spot = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var bot = b.Bots()[0];
		RegressionBench.Freeze(bot);
		Health target = bot.GetNode<Health>("Health");
		await b.Place(new Vector3(-15, 1, 30), 0f);
		await b.Seconds(target.SpawnProtection + 0.3);

		var probe = GD.Load<PackedScene>(Weapon.DefaultGrenadeScene).Instantiate<Grenade>();
		RegressionBench.Note($"granada: {probe.MaxDamage} no centro, raio {probe.Radius} m");
		b.Near(300, probe.MaxDamage, 0.5, "o centro tira 300 (era 420)");
		probe.Free();

		// No aberto, a 2,5 m: 300 × (1 − 2,5/9) = 217, mata quem tem 200.
		float open = await Blow(b, bot, target, 2.5f, null);
		b.AtLeast(200f, open, "no aberto, a 2,5 m, mata (dano)");

		// A 4 m no aberto: fere e não mata (era morte certa até 4,7 m).
		float four = await Blow(b, bot, target, 4f, null);
		b.Check(four > 50f && four < 200f, $"no aberto, a 4 m, fere e não mata ({four:0})");

		// Atrás de uma parede de 3 m: nada.
		float wall = await Blow(b, bot, target, 2.5f, new Vector3(0.3f, 3f, 4f));
		b.Near(0, wall, 0.01, "atrás da parede, não fere");

		// Atrás de uma barreira invisível (o corpo com o nome que os mapas usam): nada.
		float barrier = await Blow(b, bot, target, 2.5f, new Vector3(0.3f, 4f, 4f), "Barreira");
		b.Near(0, barrier, 0.01, "atrás da barreira, não fere");

		// Uma parede com uma fresta de 4 cm no meio, alinhada com o corpo: o raio
		// fino passava por ela; a bola de 6 cm não passa.
		float slit = await Blow(b, bot, target, 2.5f, new Vector3(0.3f, 3f, 4f), "Parede", 0.04f);
		b.Near(0, slit, 0.01, "por uma fresta de 4 cm na parede, não fere");

		// Um muro baixo (0,8 m) no meio: o peito e a cabeça aparecem, fere.
		float low = await Blow(b, bot, target, 2.5f, new Vector3(0.3f, 0.8f, 4f));
		b.AtLeast(150f, low, "por cima de um muro baixo, fere");
	}

	/// <summary>A granada estoura a <paramref name="distance"/> m do alvo, com
	/// um bloco do tamanho <paramref name="wall"/> no meio do caminho (ou nada).
	/// Devolve quanto o alvo perdeu.</summary>
	private static async Task<float> Blow(RegressionBench b, CharacterBody3D bot, Health target, float distance, Vector3? wall, string name = "Parede", float gap = 0f)
	{
		// Morreu na anterior: renasce e espera a proteção de quem nasceu passar.
		if (target.IsDead)
		{
			target.Mirror(target.MaxHp, target.MaxHp, 0, target.MaxShield, false);
			await b.Seconds(target.SpawnProtection + 0.2);
		}
		target.SetPool(target.MaxHp, target.MaxShield, refill: true);
		bot.GlobalPosition = Spot;
		await b.Frames(2);
		float floor = bot.GlobalPosition.Y - 0.875f;

		StaticBody3D block = null;
		if (wall is Vector3 size)
		{
			block = new StaticBody3D { Name = name };
			if (gap <= 0f)
				block.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
			else
			{
				// Duas metades com a fresta no meio (no eixo z, o do corpo).
				float half = (size.Z - gap) * 0.5f;
				foreach (float sign in new[] { -1f, 1f })
					block.AddChild(new CollisionShape3D
					{
						Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y, half) },
						Position = new Vector3(0, 0, sign * (gap * 0.5f + half * 0.5f))
					});
			}
			b.Scene.AddChild(block);
			block.GlobalPosition = new Vector3(Spot.X + distance * 0.5f, floor + size.Y * 0.5f, Spot.Z);
		}
		await b.PhysicsFrame();

		var grenade = GD.Load<PackedScene>(Weapon.DefaultGrenadeScene).Instantiate<Grenade>();
		grenade.Thrower = b.Health;
		grenade.MaxFlight = 0.05f;
		grenade.GravityScale = 0f;
		b.Scene.AddChild(grenade);
		grenade.GlobalPosition = new Vector3(Spot.X + distance, floor + 0.1f, Spot.Z);
		float before = target.Hp + target.Shield;
		await b.Until(() => !GodotObject.IsInstanceValid(grenade), 3.0);
		await b.Frames(2);
		float lost = target.IsDead ? before : before - (target.Hp + target.Shield);
		RegressionBench.Note($"a {distance} m{(wall is Vector3 w ? $", {name.ToLower()} de {w.Y} m no meio" : ", no aberto")}: perdeu {lost:0}");
		block?.QueueFree();
		await b.PhysicsFrame();
		return lost;
	}
}
