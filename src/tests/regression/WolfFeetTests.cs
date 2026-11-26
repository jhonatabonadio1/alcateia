using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// As patas do lobo tocam o chão (alpha fechado, 26/11: "os pés do lobo
/// atravessam o chão, precisa corrigir para ele tocar o chão perfeitamente").
///
/// Mede a pele como ela é desenhada (<see cref="WolfFeetProbe"/>: a pose
/// depois das patas no chão e do rabo) contra o chão de verdade embaixo de
/// cada pata, quadro a quadro: parado, andando nas quatro direções, de quatro,
/// agachado, engatinhando, girando, pousando de um pulo, numa rampa (subindo,
/// de lado, andando para cima e para baixo) e num degrau.
///
/// Antes de 26/11 a ponta das garras entrava de 6 a 18 cm no chão em toda pose
/// (o pé esquerdo parado a −17,8 cm, o direito a −9,4; de quatro, as mãos a
/// −17): o ajuste dos clipes ao corpo copiava a direção do pé do esqueleto dos
/// clipes, que aponta mais para baixo. Agora nenhuma pata entra mais de 2 cm
/// no chão, e cada pata de apoio encosta nele (até 2 cm acima).
/// </summary>
public sealed class WolfFeetTest : RegressionTest
{
	public override string Name => "PesDoLoboNoChao";
	public override string Description => "as patas do lobo encostam no chão sem entrar nele: parado, andando, de quatro, agachado, girando, pousando, em rampa e em degrau";

	/// <summary>O quanto uma pata pode entrar no chão, e o quanto a de apoio
	/// pode ficar acima dele, em metros.</summary>
	private const float Sink = 0.02f, Float = 0.02f;

	private static readonly Vector3 Spot = new(-15, 1, 13);

	private sealed class Range
	{
		public float Min = float.MaxValue, Max = float.MinValue;
		public void Add(float v) { Min = Mathf.Min(Min, v); Max = Mathf.Max(Max, v); }
	}

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		await b.Seconds(0.5);
		var visual = b.Player.GetNode<WolfVisual>("WolfVisual");
		b.Check(visual.FootPlant != null, "o lobo tem as patas no chão (WolfFootPlant)");
		var probe = new WolfFeetProbe(b.Player, visual.Model);

		var feet = new[] { WolfFeetProbe.Limb.LeftFoot, WolfFeetProbe.Limb.RightFoot };
		var hands = new[] { WolfFeetProbe.Limb.LeftHand, WolfFeetProbe.Limb.RightHand };

		// Mede por um tempo: o menor e o maior vão de cada pata (negativo: dentro do chão).
		async Task<Dictionary<WolfFeetProbe.Limb, Range>> Sample(double seconds, System.Action each = null)
		{
			var ranges = new Dictionary<WolfFeetProbe.Limb, Range>();
			for (int i = 0; i < (int)(seconds * 60); i++)
			{
				each?.Invoke();
				await b.Frames(1);
				if (!b.Player.IsOnFloor() || b.Player.IsWallRunning) continue;
				foreach (var (limb, c) in probe.Measure())
				{
					if (!c.HasFloor) continue;
					if (!ranges.TryGetValue(limb, out Range r)) ranges[limb] = r = new Range();
					r.Add(c.Gap);
				}
			}
			return ranges;
		}

		void Judge(string pose, Dictionary<WolfFeetProbe.Limb, Range> ranges, IEnumerable<WolfFeetProbe.Limb> planted)
		{
			RegressionBench.Note($"{pose}: " + string.Join("  ", ranges.OrderBy(r => r.Key).Select(r => $"{r.Key} {r.Value.Min * 100:0.0}..{r.Value.Max * 100:0.0} cm")));
			float deepest = ranges.Count > 0 ? ranges.Values.Min(r => r.Min) : 0f;
			b.Check(deepest >= -Sink, $"{pose}: nenhuma pata entra no chão (a mais funda a {deepest * 100:0.0} cm)");
			foreach (var limb in planted)
			{
				bool touches = ranges.TryGetValue(limb, out Range r) && r.Min <= Float;
				b.Check(touches, $"{pose}: {limb} encosta no chão (o mais perto a {(ranges.TryGetValue(limb, out Range q) ? q.Min * 100 : float.NaN):0.0} cm)");
			}
		}

		async Task Reset(Vector3 at, float yaw = 0f)
		{
			RegressionBench.ReleaseAll();
			await b.Place(at, yaw, settle: 0.7);
		}

		// ---- chão plano
		await Reset(Spot);
		Judge("parado", await Sample(1.5), feet);

		foreach (var (action, name) in new[] { ("move_forward", "andando para a frente"), ("move_back", "andando para trás"), ("move_left", "andando para a esquerda"), ("move_right", "andando para a direita") })
		{
			await Reset(new Vector3(-8, 1, 13), action == "move_left" ? -90f : action == "move_right" ? 90f : 0f);
			RegressionBench.Press(action);
			await b.Seconds(0.4);
			Judge(name, await Sample(1.2), feet);
		}

		await Reset(new Vector3(-8, 1, 18));
		await b.Tap("move_forward");
		await b.Seconds(0.08);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		Judge("de quatro", await Sample(0.9), feet.Concat(hands));

		await Reset(Spot);
		RegressionBench.Press("crouch");
		await b.Seconds(0.8);
		Judge("agachado", await Sample(1.0), feet);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.4);
		Judge("engatinhando", await Sample(1.2), feet);

		await Reset(Spot);
		Judge("girando parado", await Sample(1.2, () => b.Player.RotateY(Mathf.DegToRad(4f))), feet);

		await Reset(Spot);
		await b.Tap("jump");
		await b.Until(() => !b.Player.IsOnFloor(), 0.5);
		await b.Until(() => b.Player.IsOnFloor(), 3.0);
		Judge("pousando do pulo", await Sample(1.0), feet);

		// ---- rampa de 15° (sobe para −z) e degrau de 18 cm, montados aqui
		var ramp = Solid(b, new Vector3(3, 0.2f, 6), new Vector3(10, 0.68f, 14), new Vector3(Mathf.DegToRad(15f), 0, 0));
		var step = Solid(b, new Vector3(3, 0.18f, 3), new Vector3(16, 0.09f, 14), Vector3.Zero);
		await b.PhysicsFrame();

		await Reset(new Vector3(10, 1.8f, 14), 0f);
		Judge("parado na rampa, de frente para a subida", await Sample(1.0), feet);
		await Reset(new Vector3(10, 1.8f, 14), 90f);
		Judge("parado na rampa, de lado", await Sample(1.0), feet);
		await Reset(new Vector3(10, 1.2f, 16.6f), 0f);
		RegressionBench.Press("move_forward");
		Judge("subindo a rampa", await Sample(1.0), feet);
		await Reset(new Vector3(10, 2.2f, 11.6f), 180f);
		RegressionBench.Press("move_forward");
		Judge("descendo a rampa", await Sample(1.0), feet);

		await Reset(new Vector3(16, 1, 15.35f), 0f);
		Judge("parado na beira do degrau", await Sample(1.0), feet);
		await Reset(new Vector3(16, 1, 18.5f), 0f);
		RegressionBench.Press("move_forward");
		Judge("subindo o degrau", await Sample(1.4), feet);

		RegressionBench.ReleaseAll();
		ramp.QueueFree();
		step.QueueFree();
	}

	private static StaticBody3D Solid(RegressionBench b, Vector3 size, Vector3 at, Vector3 rotation)
	{
		var body = new StaticBody3D { Position = at, Rotation = rotation };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		b.Scene.AddChild(body);
		return body;
	}
}
