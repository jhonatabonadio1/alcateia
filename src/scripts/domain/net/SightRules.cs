using System;

/// <summary>
/// O que um jogador pode saber de um inimigo (27/11, contra wallhack): o
/// servidor só conta onde o inimigo está a quem pode **ver** ou **ouvir**.
/// Um jogo adulterado desenha através da parede o que recebe; o que não
/// recebe, não tem como desenhar. As contas daqui (até onde se ouve, por
/// quanto tempo o que se viu continua valendo) são C# puro, com teste; a
/// linha de visão, que precisa do mapa, fica no servidor de partida
/// (<c>HostedMatch.Sight</c>).
///
/// Tudo com folga para o lado de mostrar: o inimigo que aparece na tela um
/// instante atrasado, ao virar a esquina, estraga o jogo de todo mundo; o
/// que o trapaceiro ganha com meio segundo a mais de informação é pouco.
/// </summary>
public static class SightRules
{
	/// <summary>De quantos em quantos passos o servidor refaz a conta (10 por segundo).</summary>
	public const int EveryTicks = 6;

	/// <summary>Por quantos passos o que se viu ou ouviu continua sendo contado (0,75 s).</summary>
	public const int HoldTicks = 45;

	/// <summary>Por quantos passos depois de atacar (tiro, golpe, granada) o
	/// inimigo é contado a todos: o som dele se ouve do mapa inteiro (1,5 s).</summary>
	public const int LoudTicks = 90;

	/// <summary>Quanto à frente, em segundos, a conta olha (onde cada um vai
	/// estar pela velocidade): quem vai aparecer já está na tela ao aparecer —
	/// o desenho dos outros anda 0,1 s no passado e a conta, a cada 0,1 s.</summary>
	public const float LookAhead = 0.3f;

	/// <summary>Perto assim, sempre (m): parado e calado atrás da esquina.</summary>
	public const float QuietRadius = 4f;

	/// <summary>Até onde se ouve o passo (m; os de <c>ruido.tres</c>, com folga).</summary>
	public const float StepRadius = 14f, RunRadius = 23f, HowlRadius = 46f;

	/// <summary>Até que distância este corpo se ouve, pelo que está fazendo.</summary>
	public static float HearingRadius(float speed, bool crouching, bool running, bool howling)
	{
		if (howling) return HowlRadius;
		if (speed < 0.5f || crouching) return QuietRadius;
		return running ? RunRadius : StepRadius;
	}

	/// <summary>O que foi visto ou ouvido no passo <paramref name="seenAt"/> ainda vale em <paramref name="now"/>.</summary>
	public static bool Fresh(uint seenAt, uint now, int ticks = HoldTicks) => seenAt != 0 && now - seenAt <= (uint)ticks;
}
