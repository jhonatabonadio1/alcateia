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
/// pouco acima do medido na aprovação (de frente 0,73 m/s, de lado 0,85,
/// para trás 0,72, engatinhando 0,55, correndo de quatro 1,16).
/// </summary>
public sealed class WolfStrideTest : RegressionTest
{
	public override string Name => "PassadaDoLobo";
	public override string Description => "o pé do lobo não patina andando para qualquer lado, agachado, de quatro, nem ao parar";

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
		await Phase(b, "de frente", -90f, 0.95, "move_forward");
		await Phase(b, "de lado", 0f, 1.1, "move_right");
		await Phase(b, "para trás", 90f, 0.95, "move_back");
		await Phase(b, "na diagonal", -45f, 1.1, "move_forward", "move_right");
		await Phase(b, "engatinhando", -90f, 0.9, "move_forward", "crouch");
		await Phase(b, "de quatro", -90f, 1.3, "move_forward", "sprint");
	}

	private async Task Phase(RegressionBench b, string what, float yaw, double slideLimit, params string[] keys)
	{
		bool crouch = keys.Contains("crouch");
		bool sprint = keys.Contains("sprint");
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
		var run = await Record(b, 1.5);

		RegressionBench.Release(keys.Where(k => k != "crouch").ToArray());
		var stop = await Record(b, 0.9);
		RegressionBench.ReleaseAll();

		float slide = Slide(run);
		RegressionBench.Note($"{what}: patinação {RegressionBench.F(slide)} m/s");
		b.AtMost(slideLimit, slide, $"{what}: patinação do apoio (m/s)");

		// Parando: o corpo para em menos de um décimo de segundo, e depois
		// disso os pés só se juntam (sem correr no lugar). Engatinhando, as
		// mãos sobem do chão para a pose agachada.
		int stopped = stop.FindIndex(f => f.Speed < 0.05f);
		b.Check(stopped >= 0 && stopped <= 6, $"{what}: o corpo parou em até 0,1 s");
		float travel = Travel(stop.Skip(System.Math.Max(stopped, 0)).ToList());
		b.AtMost(crouch || sprint ? 3.2 : 1.1, travel, $"{what}: o quanto os pés andam depois de o corpo parar (m)");
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
