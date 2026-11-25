using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A granada soa a cada batida (Jhon, 25/11: "a granada, ao cair, tem que
/// fazer um barulho de algo caindo no chão e respeitando a física a cada
/// batida"). Solta de uma altura conhecida, a primeira batida tem a
/// velocidade da queda (√(2gh)), cada quique seguinte perde o que o quique
/// tira, e rolando não soa mais.
/// </summary>
public sealed class GrenadeBounceTest : RegressionTest
{
	public override string Name => "GranadaQuica";
	public override string Description => "a granada solta de 2 m soa na primeira batida com a velocidade da queda, cada quique mais fraco que o anterior, e rolando para de soar";

	private const float Height = 2f;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.4);

		var grenade = GD.Load<PackedScene>(Weapon.DefaultGrenadeScene).Instantiate<Grenade>();
		grenade.Thrower = b.Health;
		grenade.Fuse = 60f;                 // sem estourar: só as batidas
		b.Scene.AddChild(grenade);
		// Ao lado do jogador, no chão de onde ele está, de 2 m, quase parada.
		var down = PhysicsRayQueryParameters3D.Create(b.Player.GlobalPosition + new Vector3(2.5f, 0, 0), b.Player.GlobalPosition + new Vector3(2.5f, -5, 0));
		var floor = b.Player.GetWorld3D().DirectSpaceState.IntersectRay(down);
		b.Check(floor.Count > 0, "há chão para a granada");
		if (floor.Count == 0) return;
		Vector3 ground = (Vector3)floor["position"];
		grenade.GlobalPosition = ground + Vector3.Up * Height;
		grenade.LinearVelocity = new Vector3(0.6f, 0, 0);

		await b.Seconds(4.0);
		var hits = grenade.Bounces.ToList();
		float g = (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * grenade.GravityScale;
		float fall = Mathf.Sqrt(2f * g * Height);
		RegressionBench.Note($"{hits.Count} batidas: " + string.Join(", ", hits.Select(h => $"{RegressionBench.F(h.Time)} s a {RegressionBench.F(h.Speed)} m/s")) + $"; queda de {Height} m chega a {RegressionBench.F(fall)} m/s");

		b.AtLeast(2, hits.Count, "soa mais de uma vez (o toque e ao menos um quique)");
		if (hits.Count == 0) return;
		b.Near(fall, hits[0].Speed, 0.15 * fall, "a primeira batida tem a velocidade da queda (m/s)");
		b.Near(Mathf.Sqrt(2f * Height / g), hits[0].Time, 0.05, "e soa quando ela chega ao chão (s)");
		for (int i = 1; i < hits.Count; i++)
			b.Check(hits[i].Speed < hits[i - 1].Speed, $"a batida {i + 1} é mais fraca que a anterior ({RegressionBench.F(hits[i].Speed)} < {RegressionBench.F(hits[i - 1].Speed)} m/s)");
		b.AtMost(1.5, hits[^1].Time, "depois de 1,5 s ela rola ou para: não soa mais (s)");
		grenade.QueueFree();
	}
}
