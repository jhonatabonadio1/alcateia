namespace Alcateia.UnitTests;

/// <summary>O tempo dos golpes do lobo, que a arma e o corpo leem do
/// mesmo lugar.</summary>
public static class WolfMovesTests
{
	[Test("a garra dura 0,39 s e o dano sai 0,14 s depois do clique")]
	public static void ClawTiming()
	{
		Check.Near(0.386, WolfMoves.Duration(WolfMove.Claw), 0.001, "duração da garra");
		Check.Near(0.136, WolfMoves.ImpactDelay(WolfMove.Claw), 0.001, "instante do dano");
	}

	[Test("o soco tem o mesmo ritmo da garra")]
	public static void PunchTiming()
	{
		Check.Near(WolfMoves.Duration(WolfMove.Claw), WolfMoves.Duration(WolfMove.Punch), 0.001, "duração");
		Check.Near(0.136, WolfMoves.ImpactDelay(WolfMove.Punch), 0.001, "instante do dano");
	}

	[Test("o salto dura 2,14 s, voa de 0,32 s em diante e bate em 1,14 s")]
	public static void PounceTiming()
	{
		Check.Near(2.143, WolfMoves.Duration(WolfMove.Pounce), 0.001, "duração do salto");
		Check.Near(1.143, WolfMoves.ImpactDelay(WolfMove.Pounce), 0.001, "instante do dano");

		var (delay, duration, distance) = WolfMoves.StepOf(WolfMove.Pounce);
		Check.Near(0.321, delay, 0.001, "início do voo");
		Check.Near(0.786, duration, 0.001, "duração do voo do clipe");
		Check.Near(1.881, distance, 0.001, "passo do clipe na escala do jogo");
	}

	[Test("o rugido dura 2,0 s")]
	public static void HowlTiming() =>
		Check.Near(2.0, WolfMoves.Duration(WolfMove.Howl), 0.001, "duração do rugido");

	[Test("a aterrissagem do bote prende o lobo 0,78 s")]
	public static void DiveLanding() =>
		Check.Near(0.78, WolfMoves.LandDuration, 0.001, "aterrissagem");

	[Test("a queda do bote acelera o clipe para o impacto chegar no contato, entre 0,45× e 4×")]
	public static void DiveSpeedFitsContact()
	{
		float clip = WolfMoves.Clips["dive"].Length;
		Check.Near(clip / (0.5f * 1.1f), WolfMoves.DiveSpeed(0.5f), 0.001, "contato em 0,5 s");
		Check.Near(0.45, WolfMoves.DiveSpeed(5f), 0.001, "mergulho longo usa o piso");
		Check.Near(4.0, WolfMoves.DiveSpeed(0.01f), 0.001, "contato imediato usa o teto");
	}

	[Test("todo clipe termina depois de começar e o golpe cai dentro dele")]
	public static void ClipsAreConsistent()
	{
		foreach (var (name, clip) in WolfMoves.Clips)
		{
			Check.True(clip.End > clip.Start, $"{name}: fim depois do começo");
			Check.Between(clip.Start, clip.End, clip.Impact, $"{name}: instante do golpe");
		}
	}
}
