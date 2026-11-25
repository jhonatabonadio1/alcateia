using Godot;
using System.Threading.Tasks;

/// <summary>
/// O rabo do lobo com física (Jhon, 25/11: "o rabo deve ter movimentação e
/// física durante o game, podendo mexer conforme o personagem anda ou corre,
/// bem próximo da física e respeitando leis da gravidade"). O modelo traz a
/// cadeia rabo1..rabo5 e o <see cref="WolfVisual"/> a simula
/// (WolfVisual.Tail.cs): a gravidade puxa a ponta, a arrancada deixa o rabo
/// para trás, a freada o traz para a frente, e parado ele assenta.
/// </summary>
public sealed class WolfTailTest : RegressionTest
{
	public override string Name => "RaboDoLobo";
	public override string Description => "o rabo do lobo tem ossos e física: pende com a gravidade, fica para trás na arrancada, levanta correndo de quatro, volta e assenta ao parar, e não entra no corpo";

	private static readonly Vector3 Start = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();
		Player p = b.Player;
		var visual = p.GetNode<WolfVisual>("WolfVisual");
		Skeleton3D skeleton = AnimationKit.FindSkeleton(visual.Model);
		int root = skeleton.FindBone(WolfVisual.TailRoot), tip = skeleton.FindBone(WolfVisual.TailEnd);
		b.Check(root >= 0 && tip >= 0, $"o lobo tem os ossos do rabo ({WolfVisual.TailRoot} a {WolfVisual.TailEnd})");
		b.Check(visual.Tail != null, "e o rabo é simulado");
		if (root < 0 || tip < 0 || visual.Tail == null) return;

		// A ponta do rabo em relação à raiz, no espaço do corpo: x para a
		// direita, y para cima, z para trás (o corpo olha para −Z). Lida no
		// sinal do esqueleto: o que o simulador põe nos ossos só vale até o
		// fim da atualização (depois o Godot devolve a pose da animação).
		Vector3 sample = Vector3.Zero;
		void Read()
		{
			Transform3D at = skeleton.GlobalTransform;
			Vector3 world = at * skeleton.GetBoneGlobalPose(tip).Origin - at * skeleton.GetBoneGlobalPose(root).Origin;
			sample = p.GlobalBasis.Inverse() * world;
		}
		skeleton.SkeletonUpdated += Read;
		Vector3 Tail() => sample;

		// 1. Parado, sem a física: o rabo como o modelo o desenha.
		await b.Place(Start, -90f, settle: 0.6);
		visual.Tail.Active = false;
		await b.Seconds(0.3);
		Vector3 drawn = Tail();
		float length = drawn.Length();
		visual.Tail.Active = true;
		visual.Tail.Reset();
		await b.Seconds(2.0);
		Vector3 rest = Tail();
		RegressionBench.Note($"desenho {V(drawn)}; assentado {V(rest)}; comprimento {RegressionBench.F(length)} m");
		b.AtMost(drawn.Y - 0.02, rest.Y, "a gravidade puxa a ponta para baixo do desenho (m)");
		b.AtLeast(0.05, rest.Z, "parado o rabo fica atrás do corpo (m)");
		b.AtMost(-0.15, rest.Y, "e pende (a ponta abaixo da raiz, m)");
		b.Near(length, rest.Length(), 0.12 * length, "sem esticar nem encolher (m)");

		// Assentou mesmo: parado, a ponta não fica tremendo.
		float jitter = 0f;
		Vector3 last = Tail();
		for (int i = 0; i < 30; i++)
		{
			await b.Frames(1);
			Vector3 now = Tail();
			jitter = Mathf.Max(jitter, (now - last).Length());
			last = now;
		}
		// O parado respira (o quadril mexe): a ponta acompanha, devagar.
		b.AtMost(0.01, jitter, "parado, a ponta anda menos de 1 cm por quadro");

		// 2. A arrancada de quatro: o rabo fica para trás e levanta; e nunca
		// entra no corpo (a ponta à frente da raiz).
		await b.Tap("move_forward");
		await b.Seconds(0.08);
		RegressionBench.Press("move_forward");
		float back = rest.Z, high = rest.Y, front = rest.Z;
		for (int i = 0; i < 75; i++)
		{
			await b.Frames(1);
			Vector3 now = Tail();
			back = Mathf.Max(back, now.Z);
			high = Mathf.Max(high, now.Y);
			front = Mathf.Min(front, now.Z);
		}
		Vector3 running = Tail();
		RegressionBench.Note($"correndo de quatro a {RegressionBench.F(RegressionBench.Flat(p.Velocity))} m/s: {V(running)}; mais atrás {RegressionBench.F(back)} m, mais alto {RegressionBench.F(high)} m");
		b.AtLeast(rest.Z + 0.08, back, "na arrancada o rabo fica para trás (m)");
		b.AtLeast(rest.Y + 0.10, high, "e correndo de quatro ele levanta (m)");

		// 3. A freada: o corpo para e o rabo vem para a frente, depois volta
		// ao lugar e assenta.
		RegressionBench.ReleaseAll();
		float swing = running.Z;
		for (int i = 0; i < 45; i++)
		{
			await b.Frames(1);
			Vector3 now = Tail();
			swing = Mathf.Min(swing, now.Z);
			front = Mathf.Min(front, now.Z);
		}
		await b.Seconds(2.0);
		Vector3 settled = Tail();
		RegressionBench.Note($"freando o rabo veio até {RegressionBench.F(swing)} m; assentou em {V(settled)}; mais à frente no teste todo {RegressionBench.F(front)} m");
		b.AtMost(running.Z - 0.08, swing, "na freada o rabo vem para a frente (m)");
		b.Near(rest.Z, settled.Z, 0.06, "parado de novo, volta para onde estava (trás, m)");
		b.Near(rest.Y, settled.Y, 0.06, "e na mesma altura (m)");
		// Na freada a ponta chega a passar uns 10 cm da raiz (que fica na
		// traseira do quadril), entre as coxas; as cápsulas das pernas seguram o resto.
		b.AtLeast(-0.2, front, "em nenhum quadro a ponta atravessa o corpo para a frente (m)");
		b.Check(float.IsFinite(settled.X) && float.IsFinite(settled.Y) && float.IsFinite(settled.Z), "a simulação não estourou");
		skeleton.SkeletonUpdated -= Read;
	}

	private static string V(Vector3 v) => $"({RegressionBench.F(v.X)} {RegressionBench.F(v.Y)} {RegressionBench.F(v.Z)})";
}
