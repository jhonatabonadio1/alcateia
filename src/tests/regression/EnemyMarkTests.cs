using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O inimigo tem um brilho vermelho no ombro esquerdo (15/11, no lugar da
/// borda vermelha no corpo inteiro): preso ao osso do braço, só no time
/// contrário, some quando ele morre e quando vira aliado.
///
/// Com tela (xvfb) e <c>-- --fotos=pasta</c>, fotografa o inimigo de
/// frente, de lado e de costas:
///   xvfb-run -a godot --path . --rendering-driver opengl3 --fixed-fps 60 \
///     res://src/tests/regression/bench.tscn -- --teste=MarcaNoOmbro --fotos=/tmp/fotos
/// </summary>
public sealed class EnemyMarkTest : RegressionTest
{
	public override string Name => "MarcaNoOmbro";
	public override string Description => "o inimigo tem o brilho vermelho no ombro, preso ao braço; aliado e morto, não";

	private readonly string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(1);
		await b.Seconds(0.3);
		var bot = b.Bots()[0];
		RegressionBench.Freeze(bot);
		bot.GlobalPosition = new Vector3(-15, 1, 13);
		bot.Rotation = Vector3.Zero;
		Health health = bot.GetNode<Health>("Health");
		Health mine = b.Player.GetNode<Health>("Health");
		health.Team = mine.Team == Teams.Blue ? Teams.Orange : Teams.Blue;

		// O jogador olha para o bot a 4 m.
		await b.Place(bot.GlobalPosition + new Vector3(0, 0, -4), 180f, settle: 0.3);
		await b.Seconds(0.8);

		var marks = Marks(bot);
		var skeletons = bot.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>()
			.Where(s => Enumerable.Range(0, s.GetBoneCount()).Any(i => s.GetBoneName(i).EndsWith("LeftArm"))).ToList();
		RegressionBench.Note($"{skeletons.Count} esqueleto(s) com braço no inimigo, {marks.Count} brilho(s)");
		b.Check(marks.Count == skeletons.Count && marks.Count > 0, "um brilho em cada esqueleto do inimigo");
		b.Check(marks.All(m => m.GetParent() is Skeleton3D), "o brilho é filho do esqueleto");
		b.Check(marks.All(m => m.BoneName.ToString().EndsWith("LeftArm")), "preso ao braço esquerdo");

		// Segue o osso: a posição do nó é a da junta do ombro.
		float worst = 0;
		foreach (var m in marks)
		{
			var s = (Skeleton3D)m.GetParent();
			Vector3 joint = s.GlobalTransform * s.GetBoneGlobalPose(s.FindBone(m.BoneName)).Origin;
			worst = Mathf.Max(worst, m.GlobalPosition.DistanceTo(joint));
		}
		b.AtMost(0.01, worst, "o brilho está na junta do ombro (m)");

		// A borda do corpo agora é a suave e leve das equipes (TeamOutlines,
		// 18/11): a do inimigo, e nenhuma outra (a forte, que atravessa
		// parede, é só de quem te matou).
		bool other = bot.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
			.Any(m => BodyOverlay.Edge(m) is Material edge && edge != Silhouette.Enemy);
		b.Check(!other, "no corpo, só a borda suave de inimigo");

		await Photo(b, bot, "inimigo_frente", new Vector3(0, 0.2f, -3.2f));
		await Photo(b, bot, "inimigo_lado", new Vector3(3.2f, 0.2f, 0));
		await Photo(b, bot, "inimigo_costas", new Vector3(0, 0.6f, 3.2f));

		// O lobo inimigo, nas fotos: o esqueleto dele vem com escala 0,01 e
		// o brilho tem de sair do mesmo tamanho.
		if (!string.IsNullOrEmpty(_photos) && bot.GetNodeOrNull<FormController>("FormController") is FormController form)
		{
			bot.ProcessMode = Node.ProcessModeEnum.Inherit;
			form.AddWp(5000);
			form.ToggleForm();
			await b.Until(() => form.Current == global::Form.Wolf, 3.0);
			RegressionBench.Freeze(bot);
			bot.GlobalPosition = new Vector3(-15, 1, 13);
			bot.Rotation = Vector3.Zero;
			await b.Seconds(0.6);
			await Photo(b, bot, "lobo_frente", new Vector3(0, 0.4f, -3.6f));
			await Photo(b, bot, "lobo_lado", new Vector3(3.6f, 0.4f, 0));
			await Photo(b, bot, "lobo_costas", new Vector3(0, 0.8f, 3.6f));
		}

		// Morto: apaga no quadro seguinte.
		health.Mirror(0, 100, 0, 0, true);
		await b.Seconds(0.05);
		b.Check(Marks(bot).All(m => !m.Visible), "morto, o brilho apaga");
		health.Mirror(100, 100, 0, 0, false);
		await b.Seconds(0.05);
		b.Check(Marks(bot).Any(m => m.Visible), "de volta, o brilho acende");

		// Aliado: a marca sai.
		health.Team = mine.Team;
		await b.Seconds(0.8);
		b.Check(Marks(bot).Count == 0, "aliado não tem o brilho");
		b.Check(Marks(b.Player).Count == 0, "o próprio jogador não tem o brilho");
	}

	private static System.Collections.Generic.List<BoneAttachment3D> Marks(Node body) =>
		body.FindChildren(EnemyMarks.MarkName, nameof(BoneAttachment3D), true, false).OfType<BoneAttachment3D>()
			.Where(m => !m.IsQueuedForDeletion()).ToList();

	private Camera3D _camera;

	private async Task Photo(RegressionBench b, Node3D bot, string name, Vector3 offset)
	{
		if (string.IsNullOrEmpty(_photos)) return;
		DirAccess.MakeDirRecursiveAbsolute(_photos);
		if (_camera == null) { _camera = new Camera3D { Fov = 45f }; b.AddChild(_camera); }
		Vector3 at = bot.GlobalPosition + Vector3.Up * 0.3f;
		_camera.GlobalPosition = at + offset;
		_camera.LookAt(at + Vector3.Up * 0.25f);
		_camera.MakeCurrent();
		var layers = b.GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(l => l.Visible).ToList();
		foreach (var layer in layers) layer.Visible = false;
		await b.Seconds(0.1);
		await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		b.GetViewport().GetTexture().GetImage().SaveJpg($"{_photos}/{name}.jpg", 0.9f);
		foreach (var layer in layers) layer.Visible = true;
		_camera.ClearCurrent();
	}
}
