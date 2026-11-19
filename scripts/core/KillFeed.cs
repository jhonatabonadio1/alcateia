using Godot;
using System;
using System.Collections.Generic;

public enum KillCause
{
	Gun,
	Headshot,
	Knife,
	Wolf,
	Grenade,
	Fall
}

/// <summary>Com que golpe o lobo matou.</summary>
public enum WolfHit
{
	None,

	/// <summary>A garrada (clique).</summary>
	Claw,

	/// <summary>O especial: o salto carregado (botão direito no chão).</summary>
	Pounce,

	/// <summary>O bote (botão direito no ar): veio de cima.</summary>
	Dive
}

public readonly struct Kill
{
	/// <summary>O que o lobo manda no campo da arma, por golpe. Tudo
	/// vira "lobo" em <see cref="Weapon"/>; o golpe fica em
	/// <see cref="Wolf"/>.</summary>
	public const string WolfClawId = "lobo";
	public const string WolfPounceId = "lobo_salto";
	public const string WolfDiveId = "lobo_bote";

	/// <summary>Separa o id da arma das marcas do golpe ("ar15#c":
	/// <c>c</c> pelas costas, <c>a</c> de cima). As marcas viajam no
	/// mesmo texto da arma, então o abate online não precisou de campo
	/// novo na rede.</summary>
	public const char MarkSeparator = '#';
	public const char BehindMark = 'c';
	public const char AboveMark = 'a';

	public readonly string Killer;
	public readonly string Victim;
	public readonly int KillerTeam;
	public readonly int VictimTeam;
	public readonly KillCause Cause;

	/// <summary>Com o quê: id da arma ("ar15", "faca", "choque") ou
	/// "lobo". É o ícone do feed e a estatística de arma favorita.</summary>
	public readonly string Weapon;

	/// <summary>Quem acertou o morto nos últimos segundos, sem ser quem
	/// matou. Entram no placar como assistência.</summary>
	public readonly string[] Assisters;

	/// <summary>O golpe do lobo que matou (ou None, se não foi lobo).</summary>
	public readonly WolfHit Wolf;

	/// <summary>Quem matou estava atrás da vítima.</summary>
	public readonly bool FromBehind;

	/// <summary>Quem matou estava bem acima da vítima.</summary>
	public readonly bool FromAbove;

	public Kill(string killer, int killerTeam, string victim, int victimTeam, KillCause cause,
		string weapon = "", string[] assisters = null)
	{
		Killer = killer;
		KillerTeam = killerTeam;
		Victim = victim;
		VictimTeam = victimTeam;
		Cause = cause;
		Assisters = assisters ?? System.Array.Empty<string>();

		weapon ??= "";
		string marks = "";
		int cut = weapon.IndexOf(MarkSeparator);
		if (cut >= 0)
		{
			marks = weapon[(cut + 1)..];
			weapon = weapon[..cut];
		}

		FromBehind = marks.Contains(BehindMark);
		FromAbove = marks.Contains(AboveMark);
		Wolf = weapon switch
		{
			WolfPounceId => WolfHit.Pounce,
			WolfDiveId => WolfHit.Dive,
			WolfClawId => WolfHit.Claw,
			_ => cause == KillCause.Wolf ? WolfHit.Claw : WolfHit.None
		};
		Weapon = Wolf != WolfHit.None ? WolfClawId : weapon;
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

	/// <summary>Os últimos abates, com o relógio (ms) de quando saíram: o
	/// HUD que entra no meio (o do espectador) mostra o feed que já estava.</summary>
	public static readonly List<(ulong Ms, Kill Kill)> Recent = new();

	public static void Report(Kill kill)
	{
		Recent.Add((Godot.Time.GetTicksMsec(), kill));
		if (Recent.Count > 12) Recent.RemoveAt(0);
		Reported?.Invoke(kill);
	}

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
			_ => Catalog.Get(kill.Weapon) is CatalogItem item ? Loc.F("com {0}", item.Name) : Describe(kill.Cause)
		};

		string head = kill.Cause == KillCause.Headshot ? ", na cabeça" : "";
		return Loc.F("por {0} {1}{2}", kill.Killer, with, head);
	}
}

/// <summary>
/// Os dois times: a **Vanguarda** (azul) e o **Sindicato** (laranja) —
/// os nomes são do Jhon (17/11). Laranja e não vermelho porque
/// daltônicos distinguem azul de laranja com folga, e vermelho de verde
/// (ou de outro vermelho) não — e a cor do time é a primeira coisa que
/// se lê num encontro.
/// </summary>
public static class Teams
{
	public const int Blue = 1;
	public const int Orange = 2;

	public static Color ColorOf(int team) => team == Blue ? UiTokens.TeamBlue : UiTokens.TeamOrange;

	public static string NameOf(int team) => team == Blue ? "VANGUARDA" : "SINDICATO";

	/// <summary>Nome com só a inicial maiúscula, para texto corrido.</summary>
	public static string TitleOf(int team) => team == Blue ? "Vanguarda" : "Sindicato";

	public static int Other(int team) => team == Blue ? Orange : Blue;
}
