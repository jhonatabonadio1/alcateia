using Godot;
using System.Threading.Tasks;

/// <summary>
/// A mira animada do AR-15: o botão direito leva a arma ao olho
/// (<c>mirar</c> → <c>mira</c>), andando troca para <c>mira_andar</c>, o
/// tiro na mira toca <c>mira_tiro</c>, soltar devolve à cintura
/// (<c>desmirar</c> → <c>parado</c>). A boca do cano acompanha a arma (na
/// mira fica no eixo do olho) e a cruz some enquanto a mira é a da arma.
/// </summary>
public sealed class AimRigTest : RegressionTest
{
	public override string Name => "MiraDaArma";
	public override string Description => "o AR-15 sobe ao olho para mirar, com os clipes de mira, a boca do cano no eixo e a cruz apagada";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		var view = b.Player.GetNode<ViewModel>("Head/Camera3D/ViewModel");
		Camera3D cam = b.GetViewport().GetCamera3D();

		b.Check(ViewModel.HasSights(b.Weapon.HeldId), $"a arma na mão ({b.Weapon.HeldId}) tem os clipes de mira");
		b.Check(view.TryGetMuzzle(out Vector3 hip), "a boca do cano existe na cintura");
		Vector3 hipLocal = cam.ToLocal(hip);
		b.AtLeast(0.03, new Vector2(hipLocal.X, hipLocal.Y).Length(), "na cintura a boca do cano fica fora do eixo do olho (m)");

		Input.ActionPress("special");
		await b.Seconds(0.08);
		b.Check(view.RigAiming && view.RigAnimation == "mirar", $"apertou: toca 'mirar' (tocava '{view.RigAnimation}')");
		await b.Seconds(0.6);
		b.Check(view.RigAnimation == "mira", $"na mira, parado: laço 'mira' (tocava '{view.RigAnimation}')");
		view.TryGetMuzzle(out Vector3 aimed);
		Vector3 aimedLocal = cam.ToLocal(aimed);
		b.AtMost(0.006, Mathf.Abs(aimedLocal.X), "na mira o cano fica centrado no olho, de lado (m)");
		b.Check(aimedLocal.Y < 0f && aimedLocal.Y > -0.06f, $"na mira o cano fica logo abaixo da linha de mira ({aimedLocal.Y:0.000} m)");

		Crosshair cross = null;
		foreach (Node n in b.GetTree().Root.FindChildren("*", "", true, false)) if (n is Crosshair c) { cross = c; break; }
		if (cross != null) b.AtMost(0.05, cross.SelfModulate.A, "a cruz fica apagada na mira da arma");

		await b.Tap("attack");
		await b.Seconds(0.05);
		b.Check(view.RigAnimation == "mira_tiro", $"tiro na mira: 'mira_tiro' (tocava '{view.RigAnimation}')");
		await b.Seconds(0.7);

		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		b.Check(view.RigAnimation == "mira_andar", $"andando na mira: 'mira_andar' (tocava '{view.RigAnimation}')");
		RegressionBench.Release("move_forward");
		await b.Seconds(0.3);

		Input.ActionRelease("special");
		await b.Seconds(0.08);
		b.Check(!view.RigAiming && view.RigAnimation == "desmirar", $"soltou: toca 'desmirar' (tocava '{view.RigAnimation}')");
		await b.Seconds(0.6);
		b.Check(view.RigAnimation == "parado", $"de volta à cintura: 'parado' (tocava '{view.RigAnimation}')");
		if (cross != null) b.AtLeast(0.9, cross.SelfModulate.A, "a cruz volta fora da mira");
	}
}
