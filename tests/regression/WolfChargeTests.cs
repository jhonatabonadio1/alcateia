using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O rugido e a carga do salto (15/11): apertou R andando, o lobo para e
/// ruge de corpo inteiro; carregando o salto ele anda encolhido, e parado
/// fica na pose do agachar, respirando, e um anel marca onde vai pousar — no chão
/// ou em cima do que o arco alcança.
/// </summary>
public sealed class WolfChargeTest : RegressionTest
{
	public override string Name => "CargaDoSalto";
	public override string Description => "rugindo o lobo para; carregando o salto anda encolhido, e parado mantém a pose e respira; a mira marca onde ele pousa";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.8);

		var visual = b.Player.GetNode<WolfVisual>("WolfVisual");
		var skeleton = visual.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
		int hips = Bone(skeleton, "Hips"), hand = Bone(skeleton, "LeftHand"), chest = Bone(skeleton, "Spine2");
		Vector3 Global(int bone) => skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone).Origin;

		// ---- andando e apertando R: o lobo para e ruge de corpo inteiro.
		Vector3 idle = Global(hand) - Global(chest);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.6);
		b.AtLeast(3.0, RegressionBench.Flat(b.Player.Velocity), "andando antes do rugido (m/s)");
		Input.ActionPress("howl");
		await b.Seconds(0.75);
		Vector3 full = Global(hand) - Global(chest);
		float howlSpeed = RegressionBench.Flat(b.Player.Velocity);
		Vector3 before = b.Player.GlobalPosition;
		await b.Seconds(0.4);
		RegressionBench.Note($"rugido andando: a mão sobe {RegressionBench.F(full.Y - idle.Y)} m, corpo a {RegressionBench.F(howlSpeed)} m/s, andou {RegressionBench.F(RegressionBench.Flat(b.Player.GlobalPosition - before))} m em 0,4 s");
		b.AtLeast(0.25, full.Y - idle.Y, "o rugido toca inteiro: a mão sobe (m)");
		b.AtMost(0.05, howlSpeed, "rugindo, o lobo para (m/s)");
		Input.ActionRelease("howl");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);

		// ---- andando, pula e aperta R no ar: ruge no ar, e o corpo segue
		// o arco do pulo para a frente até cair (pedido do Jhon, 16/11).
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		await b.Tap("jump");
		await b.Until(() => !b.Player.IsOnFloor(), 0.5);
		await b.Seconds(0.05);
		Input.ActionPress("howl");
		await b.Seconds(0.1);
		bool inAir = !b.Player.IsOnFloor();
		float airSpeed = RegressionBench.Flat(b.Player.Velocity);
		RegressionBench.Note($"rugido no ar: progresso {RegressionBench.F(b.Player.HowlProgress)}, corpo a {RegressionBench.F(airSpeed)} m/s");
		b.Check(inAir, "ainda no ar quando apertou R");
		b.Check(b.Player.HowlProgress > 0f, "no ar, o rugido começa");
		b.AtLeast(3.0, airSpeed, "no ar, o corpo segue para a frente (m/s)");
		await b.Until(() => b.Player.IsOnFloor(), 2.0);
		await b.Seconds(0.3);
		b.AtMost(0.05, RegressionBench.Flat(b.Player.Velocity), "pousou rugindo: para (m/s)");
		Input.ActionRelease("howl");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.8);

		// ---- carregando o salto
		Weapon w = b.Weapon;
		// A carga cheia solta sozinha em 1,6 s; aqui ela segura mais, para
		// dar tempo de olhar parado e andando.
		float hold = w.PounceChargeHold;
		w.PounceChargeHold = 5f;
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		RegressionBench.Press("special");
		await b.Seconds(0.55);
		b.Check(w.IsChargingPounce, "está carregando o salto");

		// Parado: mantém a pose do agachar e respira.
		RegressionBench.Release("move_forward");
		await b.Seconds(0.5);
		float low = float.MaxValue, high = float.MinValue, chestLow = float.MaxValue, chestHigh = float.MinValue;
		double end = b.Time + 1.3;
		while (b.Time < end)
		{
			await b.PhysicsFrame();
			float y = Global(hips).Y, c = Global(chest).Y - y;
			low = Mathf.Min(low, y);
			high = Mathf.Max(high, y);
			chestLow = Mathf.Min(chestLow, c);
			chestHigh = Mathf.Max(chestHigh, c);
		}
		RegressionBench.Note($"carga parada: quadril oscila {RegressionBench.F((high - low) * 1000)} mm, peito {RegressionBench.F((chestHigh - chestLow) * 1000)} mm");
		b.AtMost(0.005, high - low, "carregando parado, mantém a pose do agachar: o quadril fica (m)");
		b.AtLeast(0.004, chestHigh - chestLow, "carregando parado, respira: o peito sobe e desce (m)");

		// Andando: anda encolhido, no passo do agachado, e a carga segue.
		Vector3 start = b.Player.GlobalPosition;
		RegressionBench.Press("move_forward", "move_right");
		await b.Seconds(0.5);
		float walk = RegressionBench.Flat(b.Player.Velocity);
		float expected = 3.894f * b.Player.WolfChargeMoveFactor;
		RegressionBench.Release("move_forward", "move_right");
		await b.Seconds(0.15);
		RegressionBench.Note($"carga andando: {RegressionBench.F(walk)} m/s, andou {RegressionBench.F(RegressionBench.Flat(b.Player.GlobalPosition - start))} m");
		b.Near(expected, walk, 0.1, "carregando, anda agachado, devagar (m/s)");
		b.Check(w.IsChargingPounce, "andando, a carga continua");

		// ---- a mira no chão
		var aim = b.GetTree().Root.FindChild("PounceAim", true, false) as PounceAim;
		var ring = aim?.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
		b.Check(ring != null && ring.Visible, "a mira do salto aparece no chão");
		Vector3 mark = ring?.GlobalPosition ?? Vector3.Zero;
		Vector3 predicted = w.PounceLanding ?? Vector3.Zero;
		b.AtMost(0.05, RegressionBench.Flat(mark - predicted), "a mira está no ponto que a arma calcula (m)");

		RegressionBench.Release("special");
		w.PounceChargeHold = hold;
		await b.Until(() => w.IsPounceFlying, 1.5);
		await b.Until(() => !w.IsPounceFlying, 3.0);
		await b.Seconds(0.2);
		float miss = RegressionBench.Flat(b.Player.GlobalPosition - mark);
		RegressionBench.Note($"salto: mira a {RegressionBench.F(RegressionBench.Flat(mark - start))} m, caiu a {RegressionBench.F(miss)} m dela");
		b.AtMost(0.6, miss, "o lobo cai na mira (m)");
		await b.Seconds(1.2);
		b.Check(ring != null && !ring.Visible, "fora da carga, a mira some");

		// ---- em cima de uma superfície: um bloco de 1,5 m no ponto de queda.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 1.0);
		float floorY = b.Player.GlobalPosition.Y + b.Player.FeetOffset;
		var block = new StaticBody3D();
		block.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4f, 1.5f, 4f) } });
		block.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4f, 1.5f, 4f) } });
		b.Scene.AddChild(block);
		block.GlobalPosition = new Vector3(b.Player.GlobalPosition.X + 8.4f, floorY + 0.75f, b.Player.GlobalPosition.Z);
		RegressionBench.Press("special");
		await b.Seconds(1.3);
		Vector3 onTop = ring?.GlobalPosition ?? Vector3.Zero;
		b.Check(ring != null && ring.Visible, "a mira aparece com o bloco no caminho");
		b.Near(1.5, onTop.Y - floorY, 0.1, "a mira fica em cima do bloco (m acima do chão)");
		string photosDir = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--fotos="))?["--fotos=".Length..];
		if (!string.IsNullOrEmpty(photosDir))
		{
			DirAccess.MakeDirRecursiveAbsolute(photosDir);
			var top = new Camera3D { Fov = 45f };
			b.AddChild(top);
			top.GlobalPosition = b.Player.GlobalPosition + new Vector3(-3.5f, 3.4f, 1.5f);
			top.LookAt(b.Player.GlobalPosition + new Vector3(5f, 0, 0));
			top.MakeCurrent();
			var hud = b.GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(l => l.Visible).ToList();
			foreach (var layer in hud) layer.Visible = false;
			await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			b.GetViewport().GetTexture().GetImage().SaveJpg($"{photosDir}/mira_em_cima.jpg", 0.9f);
			foreach (var layer in hud) layer.Visible = true;
			top.QueueFree();
		}
		RegressionBench.Release("special");
		await b.Until(() => w.IsPounceFlying, 1.5);
		await b.Until(() => !w.IsPounceFlying, 3.0);
		await b.Seconds(0.3);
		Vector3 feet = b.Player.GlobalPosition + Vector3.Up * b.Player.FeetOffset;
		RegressionBench.Note($"bloco: mira {RegressionBench.F(onTop.Y - floorY)} m acima do chão; o lobo pousou a {RegressionBench.F(feet.DistanceTo(onTop))} m dela, {RegressionBench.F(feet.Y - floorY)} m acima do chão");
		b.AtMost(0.6, feet.DistanceTo(onTop), "o lobo pousa na mira, em cima do bloco (m)");
		block.QueueFree();
		await b.Seconds(1.5);

		// ---- com tela e -- --fotos=pasta: o rugido andando e a mira no chão.
		string photos = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--fotos="))?["--fotos=".Length..];
		if (string.IsNullOrEmpty(photos)) return;
		DirAccess.MakeDirRecursiveAbsolute(photos);
		var camera = new Camera3D { Fov = 40f };
		b.AddChild(camera);
		async Task Photo(string name, Vector3 offset)
		{
			Vector3 at = b.Player.GlobalPosition + Vector3.Up * 0.1f;
			camera.GlobalPosition = at + offset;
			camera.LookAt(at + (offset.Y > 2f ? new Vector3(3f, -0.8f, 0) : Vector3.Zero));
			camera.MakeCurrent();
			var layers = b.GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(l => l.Visible).ToList();
			foreach (var layer in layers) layer.Visible = false;
			await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			b.GetViewport().GetTexture().GetImage().SaveJpg($"{photos}/{name}.jpg", 0.9f);
			foreach (var layer in layers) layer.Visible = true;
			camera.ClearCurrent();
		}

		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 1.2);
		RegressionBench.Press("special");
		await b.Seconds(0.75);
		await Photo("mira_meia_carga", new Vector3(-3.5f, 3.2f, 0));
		await b.Seconds(0.6);
		await Photo("mira_carga_cheia", new Vector3(-3.5f, 3.2f, 0));
		w.PounceChargeHold = 5f;
		RegressionBench.Press("move_forward");
		for (int i = 0; i < 3; i++)
		{
			await b.Seconds(0.25);
			await Photo($"carga_andando_{i}", new Vector3(0, 0.3f, 3.6f));
		}
		RegressionBench.Release("move_forward");
		await b.Seconds(0.4);
		await Photo("carga_parou", new Vector3(0, 0.3f, 3.6f));
		w.PounceChargeHold = hold;
		RegressionBench.ReleaseAll();
		await b.Seconds(2.5);
	}

	private static int Bone(Skeleton3D skeleton, string end) =>
		Enumerable.Range(0, skeleton.GetBoneCount()).First(i => skeleton.GetBoneName(i).EndsWith(end));
}
