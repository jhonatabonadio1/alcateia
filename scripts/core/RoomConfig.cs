using System.Collections.Generic;
using System.Linq;

public enum GameMode { Equipe, MataMata, Dominacao, Cacada, Sobrevivencia }
public enum WeaponRule { Todas, SemPesadas, SoPistolas, SoFaca }
public enum TransformRule { Livre, Bloqueada, Obrigatoria, UmLado }

/// <summary>
/// As regras de uma sala — o formulário do painel direito da tela de
/// sala, linha por linha. Todo campo aqui vira filtro na lista de salas
/// e linha no painel.
///
/// Os que dependem de outros jogadores de verdade (senha, faixa de
/// patente, espectador, troca de lado) já existem e são salvos, mas só
/// valem quando houver servidor; a interface os marca assim.
/// </summary>
public sealed class RoomSettings
{
	public int Number { get; set; }
	public string Title { get; set; } = "Sala de treino";
	public string Password { get; set; } = "";
	public GameMode Mode { get; set; } = GameMode.Equipe;
	/// <summary>Jogadores por time: 4, 6 ou 8.</summary>
	public int TeamSize { get; set; } = 8;
	public string Map { get; set; } = "hangar";
	public int ScoreToWin { get; set; } = 40;
	public int Minutes { get; set; } = 10;
	public WeaponRule Weapons { get; set; } = WeaponRule.Todas;
	public TransformRule Transform { get; set; } = TransformRule.Livre;
	public int MinRank { get; set; }
	public int MaxRank { get; set; } = Ranks.Count - 1;
	public bool FriendlyFire { get; set; }
	public bool SwapSides { get; set; }
	public bool Spectators { get; set; } = true;
	public BotLevel Difficulty { get; set; } = BotLevel.Medium;

	public RoomSettings Clone() => (RoomSettings)MemberwiseClone();

	// ------------------------------------------------------------------ rótulos

	public static readonly string[] ModeNames = { "Equipe", "Mata-mata", "Dominação", "Caçada", "Sobrevivência" };

	/// <summary>A dificuldade dos bots, na ordem de <see cref="BotLevel"/>.</summary>
	public static readonly string[] DifficultyNames = { "Fáceis", "Médios", "Difíceis" };
	public static readonly string[] WeaponNames = { "Todas", "Sem pesadas", "Só pistolas", "Só faca" };
	public static readonly string[] TransformNames = { "Livre", "Bloqueada", "Obrigatória", "Só um lado" };

	/// <summary>O que já existe de jogo para cada opção. O resto aparece
	/// desabilitado na interface, com "em breve".</summary>
	public static bool ModeAvailable(GameMode m) => m == GameMode.Equipe;
	public static bool WeaponsAvailable(WeaponRule w) => w != WeaponRule.SoFaca;
	public static bool TransformAvailable(TransformRule t) => t is TransformRule.Livre or TransformRule.Bloqueada;

	public static string ModeName(GameMode m) => ModeNames[(int)m];
}

/// <summary>Um lugar ocupado na sala.</summary>
public sealed class RoomMember
{
	public string Name;
	public int Team;
	public bool IsBot;
	public bool Ready;
	public bool IsHost;
	public int Rank;
}

/// <summary>Para onde o fim da partida leva.</summary>
public enum ReturnTarget { None, Room, RoomList }

/// <summary>
/// A sala em que o jogador está, levada para a partida e trazida de
/// volta.
///
/// Estático porque a sala e a partida são cenas diferentes, e o que
/// sobrevive à troca de cena é o que não pertence a nenhuma das duas.
///
/// <see cref="Active"/> falso quer dizer "a partida foi aberta direto
/// pelo editor, sem sala" — e aí ela monta um 4 contra 4 padrão em vez
/// de nascer vazia. É o que permite apertar F6 na cena da partida para
/// testar sem passar pelo menu.
///
/// Fase 2: isto vira o estado da sala no servidor, e cada cliente o
/// recebe ao entrar. Os campos já são os que precisarão ir pela rede.
/// </summary>
public static class RoomConfig
{
	public static bool Active { get; set; }

	public static RoomSettings Settings { get; set; } = new();
	public static List<RoomMember> Members { get; } = new();

	/// <summary>Ao voltar ao menu: abrir a sala de novo, a lista, ou o
	/// padrão.</summary>
	public static ReturnTarget ReturnTo { get; set; }

	public static int PlayerTeam =>
		Members.FirstOrDefault(m => !m.IsBot)?.Team ?? Teams.Blue;

	public static IEnumerable<(string Name, int Team)> Bots =>
		Members.Where(m => m.IsBot).Select(m => (m.Name, m.Team));

	public static int ScoreToWin => Settings.ScoreToWin;

	/// <summary>Duração em segundos.</summary>
	public static float Duration => Settings.Minutes * 60f;

	/// <summary>Regras valendo agora: as da sala, ou o padrão se a
	/// partida abriu sem sala.</summary>
	public static RoomSettings Rules => Active ? Settings : Default;

	private static readonly RoomSettings Default = new();

	/// <summary>A escalação usada quando a partida abre sem sala.</summary>
	public static IEnumerable<(string Name, int Team)> DefaultBots()
	{
		yield return ("Corvo", Teams.Blue);
		yield return ("Brasa", Teams.Blue);
		yield return ("Trovão", Teams.Blue);
		yield return ("Víbora", Teams.Orange);
		yield return ("Lâmina", Teams.Orange);
		yield return ("Sombra", Teams.Orange);
		yield return ("Ferrugem", Teams.Orange);
	}

	/// <summary>Nomes de bot, na ordem em que entram na sala.</summary>
	public static readonly string[] BotNames =
	{
		"Corvo", "Víbora", "Brasa", "Lâmina", "Trovão", "Sombra", "Ferrugem", "Garoa",
		"Cinza", "Faísca", "Granito", "Neblina", "Pólvora", "Raposa", "Tormenta", "Urtiga"
	};
}
