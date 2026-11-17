using System.Linq;

/// <summary>
/// O modo de jogo, escolhido antes de criar a sala (decisão do Jhon,
/// 17/11). Diz duas coisas: contra quem se joga (outros jogadores, ou
/// bots) e em que forma cada time pode ficar (<see cref="FormRule"/>).
///
/// Todos são por equipe: ganha quem chega primeiro aos pontos, ou quem
/// tem mais quando o tempo acaba.
///
/// Nos modos contra bots o mestre põe quantos bots quiser em cada time, e
/// cada jogador fica no time que quiser. Nos outros modos não há bot.
///
/// O número de cada modo vai pela rede ("play" nas regras da sala) e o
/// servidor de salas sabe que de <see cref="PlayMode.BotsLivre"/> em
/// diante é contra bots. Não reordene: acrescente no fim.
/// </summary>
public enum PlayMode
{
	Livre,
	SoHumanos,
	SoLobos,
	HumanosXLobos,
	BotsLivre,
	BotsHumanos,
	BotsLobos,
	CacarLobos,
	CacarHumanos
}

/// <summary>Em que forma um time pode ficar.</summary>
public enum FormRule
{
	/// <summary>Transforma quando quiser (tecla 3), volta com 1, 2 ou 4.</summary>
	Livre,

	/// <summary>Não transforma: o lobo é bloqueado.</summary>
	Humano,

	/// <summary>Nasce lobo e não volta a humano.</summary>
	Lobo
}

public static class PlayModes
{
	public sealed record Info(PlayMode Mode, string Name, string Line, IconKind Icon, bool VsBots);

	public static readonly Info[] All =
	{
		new(PlayMode.Livre, "Livre", "Humano e lobo: cada um transforma quando quiser.", IconKind.Wolf, false),
		new(PlayMode.SoHumanos, "Só humanos", "Ninguém transforma. Tiro puro.", IconKind.Human, false),
		new(PlayMode.SoLobos, "Só lobos", "Todos nascem lobo. Garra e mobilidade.", IconKind.Wolf, false),
		new(PlayMode.HumanosXLobos, "Humanos × Lobos", "Azul humano, laranja lobo. Trocam na metade.", IconKind.Wolf, false),
		new(PlayMode.BotsLivre, "Contra bots", "Com bots nos times, humano e lobo liberados.", IconKind.Bot, true),
		new(PlayMode.BotsHumanos, "Bots, só humanos", "Com bots nos times, ninguém transforma.", IconKind.Bot, true),
		new(PlayMode.BotsLobos, "Bots, só lobos", "Com bots nos times, todos lobos.", IconKind.Bot, true),
		new(PlayMode.CacarLobos, "Caçar lobos", "Com bots: azul humano contra laranja lobo.", IconKind.Human, true),
		new(PlayMode.CacarHumanos, "Caçar humanos", "Com bots: azul lobo contra laranja humano.", IconKind.Wolf, true)
	};

	public static Info Of(PlayMode mode) => All.FirstOrDefault(i => i.Mode == mode) ?? All[0];

	public static bool VsBots(PlayMode mode) => Of(mode).VsBots;

	/// <summary>Os modos contra outros jogadores, e os contra bots.</summary>
	public static Info[] VersusPlayers => All.Where(i => !i.VsBots).ToArray();
	public static Info[] VersusBots => All.Where(i => i.VsBots).ToArray();

	/// <summary>
	/// A forma permitida a um time. No Humanos × Lobos o azul começa humano
	/// e, passada a metade do tempo, os papéis trocam
	/// (<paramref name="secondHalf"/>). No Caçar lobos o azul é humano e o
	/// laranja lobo; no Caçar humanos, o contrário.
	/// </summary>
	public static FormRule RuleFor(PlayMode mode, int team, bool secondHalf)
	{
		bool blue = team != Teams.Orange;
		return mode switch
		{
			PlayMode.SoHumanos or PlayMode.BotsHumanos => FormRule.Humano,
			PlayMode.SoLobos or PlayMode.BotsLobos => FormRule.Lobo,
			PlayMode.HumanosXLobos => blue != secondHalf ? FormRule.Humano : FormRule.Lobo,
			PlayMode.CacarLobos => blue ? FormRule.Humano : FormRule.Lobo,
			PlayMode.CacarHumanos => blue ? FormRule.Lobo : FormRule.Humano,
			_ => FormRule.Livre
		};
	}

	/// <summary>A regra que vale agora para um time, na partida em curso.</summary>
	public static FormRule Now(int team) => RuleFor(RoomConfig.Rules.Play, team, Match.SecondHalf);

	/// <summary>Uma linha curta do que cada time é, para a sala e o carregamento.</summary>
	public static string Forms(PlayMode mode)
	{
		FormRule blue = RuleFor(mode, Teams.Blue, false), orange = RuleFor(mode, Teams.Orange, false);
		static string Of(FormRule r) => r switch { FormRule.Humano => "humanos", FormRule.Lobo => "lobos", _ => "humano e lobo" };
		return blue == orange ? Of(blue) : $"azul {Of(blue)}, laranja {Of(orange)}";
	}
}
