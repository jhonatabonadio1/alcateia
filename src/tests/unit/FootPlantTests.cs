using System.Numerics;

namespace Alcateia.UnitTests;

/// <summary>O pé do lobo no chão: quanto mover a sola e o IK de dois ossos.</summary>
public static class FootPlantTests
{
	[Test("dentro do chão a sola sobe tudo; encostada fica; perto do chão desce suave; longe não mexe")]
	public static void LiftByGap()
	{
		Check.Near(0.13, FootPlant.Lift(-0.13f, 0.05f), 1e-6, "13 cm dentro do chão: sobe 13");
		Check.Near(0, FootPlant.Lift(0f, 0.05f), 1e-6, "encostada: nada");
		Check.Near(-0.01, FootPlant.Lift(0.01f, 0.05f), 0.0012, "1 cm acima, na faixa: desce quase 1");
		Check.True(FootPlant.Lift(0.04f, 0.05f) > -0.01f && FootPlant.Lift(0.04f, 0.05f) < 0f, "4 cm acima, no fim da faixa: desce pouco");
		Check.Near(0, FootPlant.Lift(0.05f, 0.05f), 1e-6, "no fim da faixa: nada");
		Check.Near(0, FootPlant.Lift(0.3f, 0.05f), 1e-6, "pé no ar dando o passo: nada");
		Check.True(FootPlant.Lift(0.02f, 0.05f) <= 0f && FootPlant.Lift(0.02f, 0.05f) >= -0.02f, "nunca desce além do chão");
	}

	[Test("o IK leva a ponta ao alvo, mantém os comprimentos e o joelho dobra para o mesmo lado")]
	public static void SolveReachesTarget()
	{
		var root = new Vector3(0, 0.9f, 0);
		var mid = new Vector3(0, 0.5f, 0.08f);  // joelho um pouco à frente
		var end = new Vector3(0, 0.1f, 0);
		float a = Vector3.Distance(root, mid), b = Vector3.Distance(mid, end);

		var target = end + new Vector3(0, 0.12f, 0);  // subir o pé 12 cm
		var (m2, e2) = FootPlant.Solve(root, mid, end, target);
		Check.Near(0, Vector3.Distance(e2, target), 1e-4, "a ponta chega ao alvo");
		Check.Near(a, Vector3.Distance(root, m2), 1e-4, "coxa com o mesmo comprimento");
		Check.Near(b, Vector3.Distance(m2, e2), 1e-4, "canela com o mesmo comprimento");
		Check.True(m2.Z > mid.Z, "dobrando mais, o joelho vai mais para a frente, do mesmo lado");
	}

	[Test("alvo longe demais: estica até o limite, na direção do alvo")]
	public static void SolveClampsReach()
	{
		var root = new Vector3(0, 1f, 0);
		var mid = new Vector3(0, 0.6f, 0.05f);
		var end = new Vector3(0, 0.2f, 0);
		float reach = Vector3.Distance(root, mid) + Vector3.Distance(mid, end);
		var (_, e2) = FootPlant.Solve(root, mid, end, new Vector3(0, -2f, 0));
		Check.True(Vector3.Distance(root, e2) <= reach + 1e-3f, "não passa do comprimento da perna");
		Check.True(Vector3.Distance(root, e2) > reach - 1e-2f, "estica até quase reta");
		Check.Near(0, e2.X, 1e-4, "na linha do alvo");
	}

	[Test("perna reta: dobra para algum lado sem quebrar a conta")]
	public static void SolveStraightLeg()
	{
		var root = new Vector3(0, 1f, 0);
		var mid = new Vector3(0, 0.55f, 0);
		var end = new Vector3(0, 0.1f, 0);
		var (m2, e2) = FootPlant.Solve(root, mid, end, new Vector3(0, 0.3f, 0));
		Check.Near(0.3, e2.Y, 1e-4, "a ponta sobe ao alvo");
		Check.True(!float.IsNaN(m2.X) && !float.IsNaN(m2.Y) && !float.IsNaN(m2.Z), "sem NaN");
		Check.Near(0.45, Vector3.Distance(root, m2), 1e-4, "coxa inteira");
	}
}
