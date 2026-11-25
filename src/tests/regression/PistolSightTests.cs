using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A bala vai onde a mira da arma aponta (Jhon, 25/11: "a mira da pistola
/// escopada não está atirando a bala onde eu miro, tá mais acima"). A pistola
/// mirava com a massa 2,1° abaixo do centro da tela (a linha de mira descia 11
/// mm, pedido de 19/11), e o tiro sai do centro: a bala ia 2,1° acima da massa,
/// uns 55 cm a 15 m. Ele pediu a pistola como o fuzil, a massa e a alça no
/// centro: a arma sobe 5 mm mirando (<see cref="WeaponDefinition.AimLift"/>) e o
/// tiro continua saindo do centro, agora onde a massa aponta. Este teste
/// guarda o lado do tiro: o centro, da cintura e mirando, na pistola e no fuzil
/// (quem pôr um desvio no tiro de novo reprova aqui).
/// </summary>
public sealed class PistolSightTest : RegressionTest
{
	public override string Name => "TiroPelaMiraDaPistola";
	public override string Description => "a bala sai do centro da tela, da cintura e mirando, na pistola (Desert Eagle e Cobra) e no fuzil: a massa da pistola fica no centro";

	private static readonly Vector3 Spot = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Weapon w = b.Weapon;
		Camera3D cam = b.Player.GetNode<Camera3D>("Head/Camera3D");
		Vector3 hitAt = Vector3.Zero;
		bool got = false;
		w.WorldHit += (p, n, c) => { hitAt = p; got = true; };

		// Uma direção com parede a uns 15 m.
		float bestYaw = 0, bestGap = 1e9f;
		for (int k = 0; k < 24; k++)
		{
			await b.Place(Spot, k * 15f, 0f, settle: 0.1);
			Vector3 e = cam.GlobalPosition, f = -cam.GlobalBasis.Z;
			var q = PhysicsRayQueryParameters3D.Create(e, e + f * 60f);
			q.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid() };
			var h = b.Player.GetWorld3D().DirectSpaceState.IntersectRay(q);
			if (h.Count == 0) continue;
			float gap = Mathf.Abs(e.DistanceTo((Vector3)h["position"]) - 15f);
			if (gap < bestGap) { bestGap = gap; bestYaw = k * 15f; }
		}

		foreach (var (primary, secondary, slot, zoom, what, expected) in new[]
		{
			("ar15", "deagle", Slot.Secondary, false, "pistola da cintura", 0f),
			("ar15", "deagle", Slot.Secondary, true, "pistola mirando", 0f),
			("ar15", "deagle_cobra", Slot.Secondary, true, "pistola (pele Cobra) mirando", 0f),
			("ar15", "deagle", Slot.Primary, true, "fuzil mirando", 0f),
		})
		{
			var angles = new List<float>();
			float distance = 0;
			for (int shot = 0; shot < 6; shot++)
			{
				await b.Place(Spot, bestYaw, 0f, settle: 0.3);
				w.Equip(Arsenal.Get(primary), Arsenal.Get(secondary), 2);
				w.SelectSlot(slot);
				await b.Seconds(0.9);
				if (zoom) { RegressionBench.Press("special"); await b.Seconds(0.8); }
				Vector3 eye = cam.GlobalPosition, fwd = -cam.GlobalBasis.Z;
				got = false;
				// Dois quadros apertado: o fuzil é automático e só lê o botão segurado.
				RegressionBench.Press("attack");
				await b.Frames(2);
				RegressionBench.Release("attack");
				await b.Frames(3);
				if (got)
				{
					angles.Add(Mathf.RadToDeg(fwd.SignedAngleTo((hitAt - eye).Normalized(), cam.GlobalBasis.X)));
					distance = eye.DistanceTo(hitAt);
				}
				RegressionBench.ReleaseAll();
				await b.Seconds(0.4);
			}

			float mean = 0;
			foreach (float a in angles) mean += a;
			mean = angles.Count > 0 ? mean / angles.Count : 0;
			// O sinal: positivo ao girar para cima pelo eixo X da câmera; a bala abaixo do centro dá negativo.
			float below = -mean;
			RegressionBench.Note($"{what}: {angles.Count} tiros a {RegressionBench.F(distance)} m, a bala sai {RegressionBench.F(below)}° abaixo do centro (esperado {RegressionBench.F(expected)}°)");
			b.AtLeast(4, angles.Count, $"{what}: os tiros bateram na parede");
			b.Near(expected, below, 0.35, $"{what}: o ângulo da bala abaixo do centro (°)");
			await b.Seconds(0.2);
		}
	}
}
