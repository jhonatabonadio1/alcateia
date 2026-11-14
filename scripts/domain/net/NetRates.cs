/// <summary>
/// O ritmo da partida autoritativa (decisão 0011).
///
/// O servidor anda a 60 passos por segundo, o mesmo da física do jogo:
/// cada comando do jogador é um passo, nos dois lados. Manda o estado do
/// mundo a cada dois passos (30 por segundo), e o jogo desenha os outros
/// <see cref="InterpolationDelay"/> no passado, entre dois estados que já
/// chegaram — três estados de folga: um pacote perdido não trava ninguém.
/// </summary>
public static class NetRates
{
	public const int TickRate = 60;
	public const double TickSeconds = 1.0 / TickRate;

	/// <summary>De quantos em quantos passos vai um estado do mundo.</summary>
	public const int SnapshotEvery = 2;

	/// <summary>Quanto no passado os outros são desenhados, em segundos.</summary>
	public const double InterpolationDelay = 0.1;

	/// <summary>Sem estado novo, quanto o jogo adivinha à frente pela
	/// velocidade antes de parar e esperar.</summary>
	public const double MaxExtrapolation = 0.1;

	/// <summary>Quanto tempo de história o servidor guarda de cada corpo
	/// para a volta no tempo do tiro (a leva seguinte usa).</summary>
	public const int HistoryTicks = 64;

	/// <summary>Até quanto no passado a volta no tempo vai, em segundos:
	/// com mais ping que isso, quem atira deixa de ser favorecido.</summary>
	public const double MaxRewind = 0.2;
}
