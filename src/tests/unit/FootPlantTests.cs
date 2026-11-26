using System.Numerics;

namespace Alcateia.UnitTests;

/// <summary>O pé do lobo no chão: quanto mover a sola e o IK de dois ossos.</summary>
public static class FootPlantTests
{
	[Test("dentro do chão a sola sobe tudo; a pata de apoio desce até o chão; a que dá o passo e a longe não mexem")]
	public static void LiftByGap()
	{
		Check.Near(0.13, FootPlant.Lift(-0.13f, 0f, 0.15f), 1e-6, "13 cm dentro do chão: sobe 13, apoiando ou não");
		Check.Near(0, FootPlant.Lift(0f, 1f, 0.15f), 1e-6, "encostada: nada");
		Check.Near(-0.048, FootPlant.Lift(0.048f, 1f, 0.15f), 1e-6, "apoiando 4,8 cm acima (o agachado): desce tudo");
		Check.Near(0, FootPlant.Lift(0.048f, 0f, 0.15f), 1e-6, "dando o passo 4,8 cm acima: nada");
		Check.Near(-0.03, FootPlant.Lift(0.06f, 0.5f, 0.15f), 1e-6, "meio apoio: desce metade");
		Check.Near(0, FootPlant.Lift(0.15f, 1f, 0.15f), 1e-6, "no fim da faixa: nada");
		Check.True(FootPlant.Lift(0.13f, 1f, 0.15f) > -0.13f && FootPlant.Lift(0.13f, 1f, 0.15f) < 0f, "perto do fim da faixa: desce menos (suave)");
		Check.Near(0, FootPlant.Lift(0.4f, 1f, 0.15f), 1e-6, "longe do chão (pulo): nada");
	}

	[Test("apoio pela velocidade vertical da sola: parada apoia, subindo ou descendo não")]
	public static void StanceBySpeed()
	{
		Check.Near(1, FootPlant.Stance(0f, 0.3f), 1e-6, "parada");
		Check.Near(1, FootPlant.Stance(0.1f, 0.3f), 1e-6, "quase parada");
		Check.Near(0, FootPlant.Stance(0.5f, 0.3f), 1e-6, "subindo no passo");
		Check.Near(0, FootPlant.Stance(-0.5f, 0.3f), 1e-6, "descendo no passo");
		float mid = FootPlant.Stance(0.225f, 0.3f);
		Check.True(mid > 0f && mid < 1f, "entre as duas, suave");
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
