using Godot;
using System;

public enum KillCause
{
	Gun,
	Headshot,
	Knife,
	Wolf,
	Grenade,
	Fall
}

public readonly struct Kill
{
	public readonly string Killer;
	public readonly string Victim;
	public readonly int KillerTeam;
	public readonly int VictimTeam;
	public readonly KillCause Cause;

	/// <summary>Com o quê: id da arma ("ak47", "faca", "choque") ou
	/// "lobo". É o ícone do feed e a estatística de arma favorita.</summary>
	public readonly string Weapon;

	/// <summary>Quem acertou o morto nos últimos segundos, sem ser quem
	/// matou. Entram no placar como assistência.</summary>
	public readonly string[] Assisters;

	public Kill(string killer, int killerTeam, string victim, int victimTeam, KillCause cause,
		string weapon = "", string[] assisters = null)
	{
		Killer = killer;
		KillerTeam = killerTeam;
		Victim = victim;
		VictimTeam = victimTeam;
		Cause = cause;
		Weapon = weapon ?? "";
		Assisters = assisters ?? System.Array.Empty<string>();
	}

	public bool ByWolf => Cause == KillCause.Wolf;
}

/// <summary>
/// Barramento de abates. Quem mata reporta, o HUD escuta.
///
/// Mesmo padrão do NoiseBus, e pela mesma razão: quem atira não
/// precisa conhecer a interface, e a interface não precisa vasculhar
/// o mundo atrás de mortes.
///
/// O feed não é enfeite. É como o jogador entende o que aconteceu
/// fora do seu campo de visão — e, principalmente, como ele descobre
/// de que morreu. Morrer sem saber a causa é a forma mais rápida de
/// um jogo parecer injusto.
/// </summary>
public static class KillFeed
{
	public static event Action<Kill> Reported;

	public static void Report(Kill kill) => Reported?.Invoke(kill);

	public static string Describe(KillCause cause) => cause switch
	{
		KillCause.Headshot => "na cabeça",
		KillCause.Knife => "na faca",
		KillCause.Wolf => "de lobo",
		KillCause.Grenade => "de granada",
		KillCause.Fall => "caiu",
		_ => "abateu"
	};

	/// <summary>A frase de "como você morreu", para a tela de morte:
	/// "por Ferrugem com AK-47", "por Lâmina de lobo", "Morte acidental".
	/// Morrer sem saber de quê é o que faz um jogo parecer injusto.</summary>
	public static string HowDied(Kill kill)
	{
		if (kill.Killer == kill.Victim || kill.KillerTeam == 0)
			return kill.Cause == KillCause.Fall ? "Queda" : "Morte acidental";

		string with = kill.Weapon switch
		{
			"lobo" => "de lobo",
			Arsenal.KnifeId => "na faca",
			Arsenal.GrenadeId => "com granada",
			_ => Catalog.Get(kill.Weapon) is CatalogItem item ? $"com {item.Name}" : Describe(kill.Cause)
		};

		string head = kill.Cause == KillCause.Headshot ? ", na cabeça" : "";
		return $"por {kill.Killer} {with}{head}";
	}
}

/// <summary>
/// Os dois times: azul e laranja. Laranja e não vermelho porque
/// daltônicos distinguem azul de laranja com folga, e vermelho de verde
/// (ou de outro vermelho) não — e a cor do time é a primeira coisa que
/// se lê num encontro.
/// </summary>
public static class Teams
{
	public const int Blue = 1;
	public const int Orange = 2;

	public static Color ColorOf(int team) => team == Blue ? UiTokens.TeamBlue : UiTokens.TeamOrange;

	public static string NameOf(int team) => team == Blue ? "AZUL" : "LARANJA";

	/// <summary>Nome com só a inicial maiúscula, para texto corrido.</summary>
	public static string TitleOf(int team) => team == Blue ? "Azul" : "Laranja";

	public static int Other(int team) => team == Blue ? Orange : Blue;
}
