using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// DESCARTÁVEL: o lobo em poste, placa e parede curta, no alto, longe do cenário.
public sealed class ScratchThinWallTest : RegressionTest
{
	public override string Name => "ScratchPosteSintetico";
	public override string Description => "o lobo em objetos finos sintéticos";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		Player p = b.Player;
		p.WallDebug = OS.GetEnvironment("WALLDEBUG") == "1";
		var cases = new (string name, Shape3D shape, Vector3 offset)[]
		{
			("poste quadrado 28 cm", new BoxShape3D { Size = new Vector3(0.28f, 8, 0.28f) }, Vector3.Zero),
			("poste fino 12 cm", new BoxShape3D { Size = new Vector3(0.12f, 8, 0.12f) }, Vector3.Zero),
			("poste redondo 12 cm", new CylinderShape3D { Radius = 0.06f, Height = 8 }, Vector3.Zero),
			("coluna 50 cm", new BoxShape3D { Size = new Vector3(0.5f, 8, 0.5f) }, Vector3.Zero),
			("placa 1,2 m x 5 cm x 1 m", new BoxShape3D { Size = new Vector3(1.2f, 1.0f, 0.05f) }, Vector3.Zero),
			("parede curta 1,5 m x 20 cm x 3 m", new BoxShape3D { Size = new Vector3(1.5f, 3f, 0.2f) }, Vector3.Zero),
			("mureta 3 m x 25 cm x 1,2 m", new BoxShape3D { Size = new Vector3(3f, 1.2f, 0.25f) }, Vector3.Zero),
		};
		foreach (var (name, shape, offset) in cases)
		{
			var center = new Vector3(0, 40, 0) + offset;
			var body = new StaticBody3D { Name = "Objeto" };
			body.AddChild(new CollisionShape3D { Shape = shape });
			b.Scene.AddChild(body);
			body.GlobalPosition = center;
			await b.Frames(2);

			// Toca o chão (regra da mesma parede recomeça), depois no ar, ao lado (+Z), indo para +X.
			await b.Place(new Vector3(-8, 1.2f, 8), -90f, settle: 0.4);
			float half = shape is BoxShape3D bx ? bx.Size.Z * 0.5f : shape is CylinderShape3D cy ? cy.Radius : 0.2f;
			float startX = shape is BoxShape3D bx2 ? -bx2.Size.X * 0.5f - 0.3f : -0.3f;
			await b.Place(center + new Vector3(startX, 0.0f, half + 0.36f), -90f, settle: 0.02);
			p.Velocity = new Vector3(3.5f, 0, 0);
			RegressionBench.Press("move_forward");
			bool grabbed = await b.Until(() => p.IsWallRunning, 0.6);
			int on = 0, frames = 0, longest = 0, run = 0;
			float swept = 0, last = Angle(p, center), path = 0;
			Vector3 prev = p.GlobalPosition;
			float y0 = p.GlobalPosition.Y, lowest = y0;
			double end = b.Time + 3.0;
			while (b.Time < end && p.GlobalPosition.Y > 30)
			{
				await b.PhysicsFrame();
				frames++;
				if (p.IsWallRunning)
				{
					on++; run++; longest = Mathf.Max(longest, run);
					float now = Angle(p, center);
					swept += Mathf.Abs(Mathf.Wrap(now - last, -Mathf.Pi, Mathf.Pi));
					path += (p.GlobalPosition - prev).Length();
				}
				else run = 0;
				last = Angle(p, center);
				prev = p.GlobalPosition;
				lowest = Mathf.Min(lowest, p.GlobalPosition.Y);
			}
			RegressionBench.ReleaseAll();
			RegressionBench.Note($"{name}: grudou {grabbed}; {on} de {frames} quadros na parede (maior trecho {longest}); {RegressionBench.F(path)} m na parede; volta {RegressionBench.F(Mathf.RadToDeg(swept))}°; desceu {RegressionBench.F(y0 - lowest)} m; terminou em ({RegressionBench.F(p.GlobalPosition.X - center.X)}, {RegressionBench.F(p.GlobalPosition.Z - center.Z)})");
			body.QueueFree();
			await b.Frames(3);
		}
	}

	private static float Angle(Player p, Vector3 c)
	{
		Vector3 d = p.GlobalPosition - c;
		return Mathf.Atan2(d.Z, d.X);
	}
}
