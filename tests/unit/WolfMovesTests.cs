namespace Alcateia.UnitTests;

/// <summary>O tempo dos golpes do lobo, que a arma e o corpo leem do
/// mesmo lugar.</summary>
public static class WolfMovesTests
{
	// As garradas tocam a 1,3× desde 18/11 (pedido do Jhon: "mais fluido,
	// sem demorar"); o teste ficou com o tempo do clipe a 1×.
	[Test("a garra dura 0,56 s e o dano sai 0,15 s depois do clique (o Attack_Primary a 1,3×)")]
	public static void ClawTiming()
	{
		Check.Near(0.733 / 1.3, WolfMoves.Duration(WolfMove.Claw), 0.001, "duração da garra");
		Check.Near(0.20 / 1.3, WolfMoves.ImpactDelay(WolfMove.Claw), 0.001, "instante do dano");
	}

	[Test("o soco tem o mesmo ritmo da garra")]
	public static void PunchTiming()
	{
		Check.Near(WolfMoves.Duration(WolfMove.Claw), WolfMoves.Duration(WolfMove.Punch), 0.001, "duração");
		Check.Near(WolfMoves.ImpactDelay(WolfMove.Claw), WolfMoves.ImpactDelay(WolfMove.Punch), 0.001, "instante do dano");
	}

	[Test("o salto dura 2,2 s, voa de 0,47 s a 1,27 s e bate em 1,33 s (o Special_Attack a 1×)")]
	public static void PounceTiming()
	{
		Check.Near(2.2, WolfMoves.Duration(WolfMove.Pounce), 0.001, "duração do salto");
		Check.Near(1.33, WolfMoves.ImpactDelay(WolfMove.Pounce), 0.001, "instante do dano");

		var (delay, duration, distance) = WolfMoves.StepOf(WolfMove.Pounce);
		Check.Near(0.47, delay, 0.001, "início do voo");
		Check.Near(0.80, duration, 0.001, "duração do voo do clipe");
		Check.Near(0, distance, 0.001, "o clipe é no lugar: quem anda é o corpo, pela carga");
	}

	[Test("o rugido dura 1,53 s (o Howl a 1×)")]
	public static void HowlTiming() =>
		Check.Near(1.533, WolfMoves.Duration(WolfMove.Howl), 0.001, "duração do rugido");

	[Test("a aterrissagem do bote prende o lobo 0,87 s")]
	public static void DiveLanding() =>
		Check.Near(0.87, WolfMoves.LandDuration, 0.001, "aterrissagem");

	[Test("a queda do bote acelera o clipe para o impacto chegar no contato, entre 0,43× e 4×")]
	public static void DiveSpeedFitsContact()
	{
		float clip = WolfMoves.Clips["dive"].Length;
		Check.Near(clip / (0.5f * 1.1f), WolfMoves.DiveSpeed(0.5f), 0.001, "contato em 0,5 s");
		Check.Near(0.43, WolfMoves.DiveSpeed(5f), 0.001, "mergulho longo usa o piso");
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
