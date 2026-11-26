using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O pé do humano não desliza (Jhon, 26/11: "diminua a velocidade de andar
/// do humano, ele tá muito rápido e deslizando no mapa"). A corrida do X Bot
/// anda 3,02 m/s a 1× (<see cref="CharacterAnimator.RunClip"/>, medida no
/// quadril) e o clipe nunca acelera (regra 9 do CLAUDE.md): o corpo tem de
/// andar o que as pernas andam. Até 26/11 o humano corria a 3,46 m/s (a base
/// 3,2 da cena × 1,08), e o pé de apoio andava junto com o corpo.
///
/// Medido no esqueleto do corpo visto (o que os outros enxergam), como a
/// <c>PassadaDoLobo</c>: o pé mais baixo, enquanto está no chão, não deveria
/// andar. Os números de antes e depois estão no docs/CHANGELOG.md (26/11).
/// </summary>
public sealed class HumanStrideTest : RegressionTest
{
	public override string Name => "PassadaHumana";
	public override string Description => "o humano corre a passada do clipe (3,0 m/s): o pé de apoio não desliza para a frente, para trás nem agachado";

	/// <summary>Com TIME=laranja (à mão), mede o Barut.</summary>
	public override void Prepare()
	{
		if (OS.GetEnvironment("TIME") != "laranja") return;
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Orange });
		RoomConfig.Active = true;
	}

	private Skeleton3D _skeleton;
	private int[] _contacts;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		// Para medir outras velocidades à mão: BONUS=1.08 (o de antes de 26/11).
		if (float.TryParse(OS.GetEnvironment("BONUS"), System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out float bonus))
			b.Player.HumanSpeedBonus = bonus;

		Node human = b.Player.GetNode("Human");
		RegressionBench.Note($"modelo: {string.Join(",", human.GetChildren().Select(n => n.SceneFilePath).Where(f => f != ""))}");
		_skeleton = human.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().First();
		_contacts = Enumerable.Range(0, _skeleton.GetBoneCount())
			.Where(i =>
			{
				string n = _skeleton.GetBoneName(i);
				return n.EndsWith("LeftToeBase") || n.EndsWith("RightToeBase") || n.EndsWith("LeftFoot") || n.EndsWith("RightFoot");
			})
			.ToArray();
		b.Check(_contacts.Length == 4, $"achou os dois pés e as pontas ({_contacts.Length})");

		// O corpo sempre anda para +X; o giro escolhe a tecla. Medido em 26/11
		// (antes, a 3,46 m/s / depois, a 3,0 com a corrida a 0,88×): de frente
		// 0,05 / 0,01 m/s, para trás 0,79 / 0,33, agachado 0,96 / 0,76, de lado
		// 2,70 / 2,25. Os limites ficam um pouco acima do depois. Para trás e
		// agachado o clipe anda menos que o corpo e não acelera (regra 9); de
		// lado só há strafe andando (1,03 m/s), e o corpo vai a 3,0.
		await Phase(b, "de frente", -90f, 0.15, "move_forward");
		await Phase(b, "para trás", 90f, 0.45, "move_back");
		// Agachado desde 26/11 é uma passada montada pela velocidade (KneelWalk): a
		// planta fica parada no chão; o que sobra é o calcanhar levantando na saída
		// do passo (era 0,70 m/s com o clipe).
		await Phase(b, "agachado", -90f, 0.45, "move_forward", "crouch");
		await Phase(b, "de lado", 0f, 2.4, "move_right");

		// Com a faca na mão (26/11: 3,84 m/s). Só medido: a corrida não toca mais
		// rápido que o arquivo (regra 9), e o pé fica atrás do corpo.
		await b.Tap("slot_4");
		await b.Seconds(0.8);
		await Phase(b, "faca de frente", -90f, 99, "move_forward");
	}

	private async Task Phase(RegressionBench b, string what, float yaw, double limit, params string[] keys)
	{
		await b.Place(new Vector3(-15, 1, 13), yaw);
		if (keys.Contains("crouch")) RegressionBench.Press("crouch");
		await b.Seconds(0.4);
		RegressionBench.Press(keys);
		await b.Seconds(0.8);
		var frames = await Record(b, 1.5);
		RegressionBench.ReleaseAll();
		await b.Seconds(0.4);

		string dump = OS.GetEnvironment("DUMP");
		if (!string.IsNullOrEmpty(dump))
		{
			var lines = new List<string>();
			foreach (var f in frames)
				lines.Add(string.Join(";", new[] { f.T.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) }
					.Concat(f.Contacts.Select(c => string.Join(",", new[] { c.X, c.Y - f.Ground, c.Z }.Select(v => v.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)))))));
			System.IO.File.WriteAllLines($"{dump}/{what.Replace(' ', '_')}.csv", lines);
		}
		float body = frames.Average(f => f.Speed);
		float slide = Slide(frames);
		RegressionBench.Note($"{what}: corpo a {RegressionBench.F(body)} m/s, pé de apoio a {RegressionBench.F(slide)} m/s");
		b.AtMost(limit, slide, $"{what}: o pé de apoio desliza (m/s)");
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
			Vector3[] contacts = _contacts.Select(i => (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(i)).Origin).ToArray();
			frames.Add(new Frame(t, contacts, b.Player.GlobalPosition.Y + b.Player.FeetOffset, RegressionBench.Flat(b.Player.Velocity)));
		}
		return frames;
	}

	/// <summary>O quanto o que está apoiado anda, em média, na direção do
	/// corpo (+X), com sinal: o ponto mais baixo do trecho e o que estiver a até
	/// 4 cm dele. Com sinal, o tremor de um quadro para o outro (o pé rolando,
	/// a ponta e o calcanhar) se anula e sobra o deslize de verdade; o valor
	/// devolvido é o tamanho dele.</summary>
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
				sum += (frames[i].Contacts[k].X - frames[i - 1].Contacts[k].X) / dt;
				count++;
			}
		}
		return count > 0 ? Mathf.Abs(sum / count) : float.NaN;
	}
}
