using Godot;
using System.Collections.Generic;

public enum MatchPhase
{
    /// <summary>Todos na base, ninguém se mexe. Hora de escolher o lote.</summary>
    Countdown,

    /// <summary>Valendo.</summary>
    Live,

    /// <summary>Alguém fez os pontos ou o tempo acabou.</summary>
    Ended
}

/// <summary>Linha do placar de um combatente.</summary>
public sealed class CombatantStats
{
    public string Name;
    public int Team;
    public int Kills;
    public int Deaths;
    public bool IsLocal;
}

/// <summary>
/// A partida: mata-mata em equipe, Azul contra Vermelho.
///
/// Ganha o time que fizer <see cref="ScoreToWin"/> abates primeiro. Se o
/// tempo acabar antes, ganha quem tiver mais; empate é empate.
///
/// Existe porque sem ela o jogo era um campo de testes: morrer não
/// custava nada e matar rendia só WP. A pergunta que decide o projeto —
/// "transformar é gostoso?" — não se responde sem aposta. Virar lobo
/// com o placar 38 a 38 é uma decisão; virar lobo sem placar é um botão.
///
/// A partida não sabe quem atirou em quem. Ela escuta o
/// <see cref="KillFeed"/>, o mesmo barramento que alimenta o feed na
/// tela, e conta. Quem mata não precisa saber que existe placar.
///
/// Fora de uma partida — a cena de treino, a referência do Arrival —
/// não há instância, e <see cref="IsLive"/> responde verdadeiro: tudo
/// funciona como antes, sem contagem nem fim.
/// </summary>
public partial class Match : Node
{
    [Signal] public delegate void PhaseChangedEventHandler(int phase);
    [Signal] public delegate void ScoreChangedEventHandler(int blue, int red);

    [Export] public int ScoreToWin = 40;

    /// <summary>Contagem antes de valer, em segundos. É também o tempo
    /// da tela de lote, de propósito: escolhe-se o equipamento enquanto
    /// se espera, e ninguém sai da base com vantagem.</summary>
    [Export] public float CountdownTime = 10f;

    /// <summary>Duração da partida, em segundos.</summary>
    [Export] public float Duration = 600f;

    [Export] public bool Debug = true;

    public static Match Current { get; private set; }

    /// <summary>Pode-se andar e atirar? Verdadeiro também quando não
    /// há partida nenhuma na cena.</summary>
    public static bool IsLive => Current == null || Current.Phase == MatchPhase.Live;

    public MatchPhase Phase { get; private set; } = MatchPhase.Countdown;
    public double CountdownLeft { get; private set; }
    public double TimeLeft { get; private set; }

    /// <summary>Time vencedor. 0 é empate.</summary>
    public int Winner { get; private set; }

    /// <summary>Terminou por pontos, e não por tempo.</summary>
    public bool EndedByScore { get; private set; }

    public IEnumerable<CombatantStats> Combatants => _stats.Values;

    private readonly int[] _score = new int[3];
    private readonly Dictionary<string, CombatantStats> _stats = new();
    private bool _rostered;

    public int ScoreOf(int team) => team == Teams.Blue || team == Teams.Red ? _score[team] : 0;

    public override void _EnterTree() => Current = this;

    public override void _Ready()
    {
        if (RoomConfig.Active)
        {
            ScoreToWin = RoomConfig.ScoreToWin;
            Duration = RoomConfig.Duration;
        }

        CountdownLeft = CountdownTime;
        TimeLeft = Duration;

        KillFeed.Reported += OnKill;
    }

    public override void _ExitTree()
    {
        KillFeed.Reported -= OnKill;
        if (Current == this) Current = null;
    }

    public override void _Process(double delta)
    {
        // A lista sai no primeiro quadro, não no _Ready: os bots só
        // recebem nome e time no _EnterTree deles, e a ordem de entrada
        // na árvore não é algo em que valha confiar.
        // Durante a contagem a lista é refeita a cada quadro: os bots
        // nascem no _Ready do MatchSetup, e confiar que isso acontece antes
        // do primeiro quadro daqui seria apostar na ordem da cena.
        if (!_rostered || Phase == MatchPhase.Countdown)
        {
            Roster();
            _rostered = true;
        }

        switch (Phase)
        {
            case MatchPhase.Countdown:
                CountdownLeft -= delta;
                if (CountdownLeft <= 0)
                {
                    CountdownLeft = 0;
                    SetPhase(MatchPhase.Live);
                }
                break;

            case MatchPhase.Live:
                TimeLeft -= delta;
                if (TimeLeft <= 0)
                {
                    TimeLeft = 0;
                    End(byScore: false);
                }
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Phase != MatchPhase.Ended || !@event.IsActionPressed("restart")) return;

        // O contador de menus é estático e sobrevive à troca de cena. Se
        // algum menu estava aberto no fim, a cena nova nasceria achando
        // que já há menus por cima — e a tela de lote nunca contaria o
        // tempo, porque ela só conta quando está sozinha.
        UiState.Reset();
        GetTree().ReloadCurrentScene();
    }

    // ------------------------------------------------------------------

    /// <summary>Todo mundo que está na cena entra no placar já no
    /// começo, com zero. Placar que só mostra quem já matou esconde
    /// quem ainda não fez nada, que é metade da informação.</summary>
    private void Roster()
    {
        foreach (string group in new[] { "player", "bot" })
        {
            foreach (Node node in GetTree().GetNodesInGroup(group))
            {
                var health = node.GetNodeOrNull<Health>("Health");
                if (health == null) continue;

                CombatantStats entry = Entry(health.PublicName, health.Team);
                entry.IsLocal = group == "player";
            }
        }
    }

    private CombatantStats Entry(string name, int team)
    {
        if (!_stats.TryGetValue(name, out CombatantStats entry))
        {
            entry = new CombatantStats { Name = name, Team = team };
            _stats[name] = entry;
        }

        return entry;
    }

    private void OnKill(Kill kill)
    {
        // Abate na contagem ou depois do fim não vale nada.
        if (Phase != MatchPhase.Live) return;

        Entry(kill.Victim, kill.VictimTeam).Deaths++;

        // Só abate de inimigo pontua. Queda, granada nos próprios pés e
        // qualquer coisa sem autor contam como morte e nada mais.
        bool scores = (kill.KillerTeam == Teams.Blue || kill.KillerTeam == Teams.Red)
            && kill.KillerTeam != kill.VictimTeam;

        if (!scores) return;

        Entry(kill.Killer, kill.KillerTeam).Kills++;
        _score[kill.KillerTeam]++;

        EmitSignal(SignalName.ScoreChanged, _score[Teams.Blue], _score[Teams.Red]);

        if (_score[kill.KillerTeam] >= ScoreToWin)
            End(byScore: true);
    }

    private void End(bool byScore)
    {
        EndedByScore = byScore;

        int blue = _score[Teams.Blue];
        int red = _score[Teams.Red];
        Winner = blue > red ? Teams.Blue : red > blue ? Teams.Red : 0;

        SetPhase(MatchPhase.Ended);
    }

    private void SetPhase(MatchPhase phase)
    {
        Phase = phase;
        EmitSignal(SignalName.PhaseChanged, (int)phase);

        if (Debug)
            GD.Print($"Partida: {phase}  (AZUL {_score[Teams.Blue]} x {_score[Teams.Red]} VERMELHO)");
    }
}
