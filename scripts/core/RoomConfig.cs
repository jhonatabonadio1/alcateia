using System.Collections.Generic;

/// <summary>
/// O que a sala decidiu, levado para a partida.
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

    public static int PlayerTeam { get; set; } = Teams.Blue;

    public static List<(string Name, int Team)> Bots { get; } = new();

    public static int ScoreToWin { get; set; } = 40;

    /// <summary>Duração em segundos.</summary>
    public static float Duration { get; set; } = 600f;

    /// <summary>A escalação usada quando a partida abre sem sala.</summary>
    public static IEnumerable<(string Name, int Team)> DefaultBots()
    {
        yield return ("Corvo", Teams.Blue);
        yield return ("Brasa", Teams.Blue);
        yield return ("Trovão", Teams.Blue);
        yield return ("Víbora", Teams.Red);
        yield return ("Lâmina", Teams.Red);
        yield return ("Sombra", Teams.Red);
        yield return ("Ferrugem", Teams.Red);
    }
}
