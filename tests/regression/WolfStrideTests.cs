using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O passo do lobo acompanha o corpo (v0.26.10): o pé de apoio quase não
/// patina em nenhuma direção, e parando ele não corre no lugar.
///
/// Medido no esqueleto do WolfVisual: o pé (ou a mão, engatinhando) mais
/// baixo, enquanto está no chão, não deveria andar. Os limites ficam um
/// pouco acima do medido com os andares do Jhon (15/11, o lobo a 4,15
/// m/s, com o vaivém do quadril dos clipes): de frente 0,16 m/s, na
/// diagonal 0,14, para trás 0,19, engatinhando 0,25, de lado 0,50 a 0,67
/// (o Walk_Left toca a 5,5×, um ciclo a cada 0,11 s) e de quatro 0,56 (a
/// 5,4 m/s, o clipe a 1,37×).
///
/// Em 15/11 à noite os andares em pé e o agachado foram refeitos na velocidade
/// do jogo (tocam perto de 1×, com o pé de apoio a velocidade constante
/// no arquivo). Medido: de frente 0,13 m/s, de lado 0,14, para trás 0,22,
/// na diagonal 0,14, engatinhando 0,23, de quatro 0,56 (o Run_Quad não
/// mudou). Os limites de patinação continuam os de antes.
/// </summary>
public sealed class WolfStrideTest : RegressionTest
{
	public override string Name => "PassadaDoLobo";
	public override string Description => "o pé do lobo não patina andando para qualquer lado, agachado, de quatro, nem ao parar";

	/// <summary>Com tela (xvfb) e <c>-- --fotos=pasta</c>, fotografa o lobo de
	/// lado em cada fase, oito quadros ao longo de um ciclo do passo: a
	/// patinação é só um número, e a animação também se confere a olho.
	///   xvfb-run -a godot --path . --rendering-driver opengl3 --fixed-fps 60 \
	///     res://tests/regression/bench.tscn -- --teste=PassadaDoLobo --fotos=/tmp/fotos</summary>
	private readonly string _photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];
	private Camera3D _side;
	private int _phase;

	private Skeleton3D _skeleton;
	private int[] _contacts;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		_skeleton = b.Player.GetNode("WolfVisual").FindChildren("*", "Skeleton3D", true, false)[0] as Skeleton3D;
		_contacts = Enumerable.Range(0, _skeleton.GetBoneCount())
			.Where(i =>
			{
				string n = _skeleton.GetBoneName(i);
				return n.EndsWith("LeftToeBase") || n.EndsWith("RightToeBase") || n.EndsWith("LeftHand") || n.EndsWith("RightHand");
			})
			.ToArray();
		b.Check(_contacts.Length == 4, "achou os dois pés e as duas mãos");

		// O corpo sempre anda para +X; o giro escolhe a tecla.
		await Phase(b, "de frente", -90f, 0.4, "move_forward");
		await Phase(b, "de lado", 0f, 0.9, "move_right");
		await Phase(b, "para trás", 90f, 0.4, "move_back");
		await Phase(b, "na diagonal", -45f, 0.4, "move_forward", "move_right");
		await Phase(b, "engatinhando", -90f, 0.45, "move_forward", "crouch");
		await Phase(b, "de quatro", -90f, 0.9, "move_forward", "sprint");
		await Phase(b, "engatinhando de lado", 0f, 0.45, "move_right", "crouch");
		// As três fases "carregando o salto" saíram em 18/11: com o especial
		// no Q, num toque, o lobo não anda mais carregando.
	}

	private async Task Phase(RegressionBench b, string what, float yaw, double slideLimit, params string[] keys)
	{
		bool crouch = keys.Contains("crouch");
		bool sprint = keys.Contains("sprint");
		bool charge = keys.Contains("special");
		// Carregando, a carga cheia solta sozinha em 1,6 s: aqui segura.
		float hold = b.Weapon.PounceChargeHold;
		if (charge) b.Weapon.PounceChargeHold = 9f;

		// De quatro, no ritmo em que o Jhon animou (1×), seja qual for a
		// escala que a cena traz.
		var visual = b.Player.GetNode<WolfVisual>("WolfVisual");
		float sceneScale = visual.SprintAnimScale;
		if (sprint) visual.SprintAnimScale = 1f;
		keys = keys.Where(k => k != "sprint").ToArray();
		await b.Place(new Vector3(-15, 1, 13), yaw);
		if (crouch) RegressionBench.Press("crouch");
		await b.Seconds(0.4);

		// De quatro: o primeiro toque no W, e o segundo segura.
		if (sprint)
		{
			await b.Tap("move_forward");
			await b.Seconds(0.08);
		}

		RegressionBench.Press(keys);
		await b.Seconds(0.5);
		await Photos(b, what);
		var run = await Record(b, 1.5);

		RegressionBench.Release(keys.Where(k => k != "crouch" && k != "special").ToArray());
		var stop = await Record(b, 0.9);
		RegressionBench.ReleaseAll();
		visual.SprintAnimScale = sceneScale;
		if (charge) { b.Weapon.PounceChargeHold = hold; await b.Seconds(2.5); }

		float slide = Slide(run);
		RegressionBench.Note($"{what}: patinação {RegressionBench.F(slide)} m/s");
		b.AtMost(slideLimit, slide, $"{what}: patinação do apoio (m/s)");

		// Parando: o corpo para em menos de um décimo de segundo, e depois
		// disso os pés só se juntam (sem correr no lugar). Engatinhando, as
		// mãos sobem do chão para a pose agachada.
		int stopped = stop.FindIndex(f => f.Speed < 0.05f);
		// De quatro ele vem a 6,2 m/s (15/11, 15% mais rápido) e a freada
		// é a mesma: leva um pouco mais.
		int limit = sprint ? 9 : 6;
		RegressionBench.Note($"{what}: parou em {stopped} quadros");
		b.Check(stopped >= 0 && stopped <= limit, $"{what}: o corpo parou em até {(sprint ? "0,15" : "0,1")} s");
		float travel = Travel(stop.Skip(System.Math.Max(stopped, 0)).ToList());
		RegressionBench.Note($"{what}: pés e mãos depois de parar {RegressionBench.F(travel)} m");
		// Em pé, os andares do Jhon param com 1,1 a 1,7 m de mãos e pés
		// somados: o passo congela onde o quadril estava no vaivém, e as
		// pernas se juntam embaixo dele.
		// Desde 15/11 à noite os andares em pé são um trote na velocidade do
		// jogo (pernas bem mais abertas que no andar antigo): parando, mãos
		// e pés somam 1,7 a 1,8 m de frente e 2,2 a 2,7 m de lado, conforme
		// o ponto do ciclo em que o corpo para.
		b.AtMost(crouch || sprint || charge ? 3.2 : 2.9, travel, $"{what}: o quanto os pés andam depois de o corpo parar (m)");
	}

	private async Task Photos(RegressionBench b, string what)
	{
		_phase++;
		if (string.IsNullOrEmpty(_photos)) return;
		DirAccess.MakeDirRecursiveAbsolute(_photos);
		if (_side == null)
		{
			_side = new Camera3D { Fov = 40f };
			b.AddChild(_side);
		}
		for (int i = 0; i < 8; i++)
		{
			await b.Seconds(5.0 / 60.0);
			// O corpo anda sempre para +X: a câmera fica do lado, em +Z.
			Vector3 at = b.Player.GlobalPosition + Vector3.Up * 0.1f;
			_side.GlobalPosition = at + new Vector3(0f, 0.4f, 3.4f);
			_side.LookAt(at);
			_side.MakeCurrent();
			// Sem o HUD nem os menus por cima: só o lobo.
			var layers = b.GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(l => l.Visible).ToList();
			foreach (var layer in layers) layer.Visible = false;
			await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			b.GetViewport().GetTexture().GetImage().SaveJpg($"{_photos}/{_phase}_{i}.jpg", 0.9f);
			foreach (var layer in layers) layer.Visible = true;
		}
		_side.ClearCurrent();
	}

	private record struct Frame(float T, Vector3[] Contacts, float Ground, float Speed);

	private async Task<List<Frame>> Record(RegressionBench b, double seconds)
	{
		var frames = new List<Frame>();
		double end = b.Time + seconds;
		var tree = b.GetTree();
		float t = 0;
		while (b.Time < end)
		{
			await b.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			t += (float)b.GetProcessDeltaTime();
			Vector3[] contacts = _contacts
				.Select(i => (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(i)).Origin)
				.ToArray();
			frames.Add(new Frame(t, contacts, b.Player.GlobalPosition.Y + b.Player.FeetOffset,
				RegressionBench.Flat(b.Player.Velocity)));
		}
		return frames;
	}

	/// <summary>Velocidade média do que está apoiado no chão: o ponto mais
	/// baixo e o que estiver a até 4 cm dele.</summary>
	private static float Slide(List<Frame> frames)
	{
		float lowest = frames.Min(f => f.Contacts.Min(c => c.Y - f.Ground));
		float sum = 0;
		int count = 0;
		for (int i = 1; i < frames.Count; i++)
		{
			float dt = frames[i].T - frames[i - 1].T;
			if (dt <= 0) continue;
			for (int k = 0; k < frames[i].Contacts.Length; k++)
			{
				float h0 = frames[i - 1].Contacts[k].Y - frames[i - 1].Ground;
				float h1 = frames[i].Contacts[k].Y - frames[i].Ground;
				if (h0 > lowest + 0.04f || h1 > lowest + 0.04f) continue;
				sum += RegressionBench.Flat(frames[i].Contacts[k] - frames[i - 1].Contacts[k]) / dt;
				count++;
			}
		}
		return count > 0 ? sum / count : float.NaN;
	}

	private static float Travel(List<Frame> frames)
	{
		float sum = 0;
		for (int i = 1; i < frames.Count; i++)
			for (int k = 0; k < frames[i].Contacts.Length; k++)
				sum += RegressionBench.Flat(frames[i].Contacts[k] - frames[i - 1].Contacts[k]);
		return sum;
	}
}
