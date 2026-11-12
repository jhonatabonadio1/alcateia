using Godot;
using System.Collections.Generic;

/// <summary>
/// O som do mundo: passos, pousos, tiros, uivos, explosões, e os avisos
/// da partida.
///
/// **Não inventa evento.** Escuta o <see cref="NoiseBus"/> — o mesmo
/// barramento que os bots ouvem — e toca no ponto e no raio do ruído.
/// É a regra do CLAUDE.md: o que o jogador ouve e o que o bot ouve são a
/// mesma coisa, senão o jogador não consegue prever a reação de quem
/// ouviu. Agachado e andando com Shift não emitem ruído, então também
/// não fazem som.
///
/// Os sons do próprio jogador tocam sem posição (iguais nos dois
/// ouvidos) e um pouco mais baixos: o passo de quem você é não pode
/// cobrir o passo de quem está atrás de você.
///
/// Fica uma vez em cada cena de jogo (main e treino).
/// </summary>
public partial class WorldAudio : Node
{
    /// <summary>Volume dos seus próprios passos e pousos, em dB. Um
    /// pouco abaixo dos outros: o seu passo não pode cobrir o de quem
    /// está atrás de você. Abaixo de uns −6 ele some no meio do jogo.</summary>
    [Export] public float OwnStepVolume = -3f;

    /// <summary>Escreve no Output cada ruído recebido e o som que ele
    /// tocou — ou "SEM ARQUIVO". Para descobrir por que algo está mudo.</summary>
    [Export] public bool Debug;

    /// <summary>Distância a partir da qual um tiro usa a versão
    /// distante (<c>tiro_distante</c>), se ela existir.</summary>
    [Export] public float DistantShot = 35f;

    /// <summary>Até onde se ouve o tiro de outro, em metros (decisão do
    /// Jhon: o tiro do inimigo vem dele, da distância dele, de qualquer
    /// ponto do mapa). É o único som que passa do raio do ruído: o bot
    /// continua ouvindo só até o raio da arma, e o volume cai com a
    /// distância a partir dele — longe, abafado, mas presente.</summary>
    [Export] public float ShotReach = 120f;

    /// <summary>O uivo emite ruído a cada meio segundo para os bots;
    /// o som toca uma vez por uivo.</summary>
    [Export] public float HowlRetrigger = 1.5f;

    /// <summary>
    /// Som do uivo (R, de lobo). É o mesmo da transformação, decisão do
    /// Jhon: o rugido de virar lobo é o grito da fera, e o uivo de cura
    /// usa a mesma voz. Para um uivo próprio, ponha o arquivo em
    /// <c>assets/audio/</c> e troque este nome.
    /// </summary>
    [Export] public string HowlSound = "lobo_transformar";

    private Node _local;
    private Health _localHealth;
    private readonly Dictionary<ulong, double> _lastHowl = new();
    private double _clock;

    private MatchPhase _phase = MatchPhase.Countdown;
    private int _countdownSecond = -1;

    public override void _Ready()
    {
        NoiseBus.Subscribe(OnNoise);
        KillFeed.Reported += OnKill;
    }

    public override void _ExitTree()
    {
        NoiseBus.Unsubscribe(OnNoise);
        KillFeed.Reported -= OnKill;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        FindLocal();
        UpdateMatch();
    }

    private void FindLocal()
    {
        if (_local != null && IsInstanceValid(_local)) return;

        _local = GetTree().GetFirstNodeInGroup("player");
        _localHealth = _local?.GetNodeOrNull<Health>("Health");
    }

    private void OnNoise(Noise noise)
    {
        Node source = noise.Source;
        bool mine = source != null && source == _local;
        bool wolf = source?.GetNodeOrNull<FormController>("FormController")?.IsWolf ?? false;

        switch (noise.Kind)
        {
            case NoiseKind.Step:
                Play(wolf ? "lobo_passo" : "passo", noise, mine, mine ? OwnStepVolume : 0f);
                break;

            case NoiseKind.Land:
                Play(wolf ? "lobo_pouso" : "pouso", noise, mine, mine ? OwnStepVolume : 0f);
                break;

            case NoiseKind.Howl:
                if (source == null) break;
                ulong id = source.GetInstanceId();
                if (_lastHowl.TryGetValue(id, out double last) && _clock - last < HowlRetrigger) break;
                _lastHowl[id] = _clock;
                Play(HowlSound, noise, mine);
                break;

            case NoiseKind.Shot:
                Play(ShotName(source, noise.Position, mine), noise, mine, reach: ShotReach);
                break;

            case NoiseKind.Explosion:
                Play("granada_explosao", noise, mine: false);
                break;
        }
    }

    /// <summary>Cada arma tem a voz dela: <c>tiro_ak47</c>,
    /// <c>tiro_m3</c>... Sem arquivo da arma, cai no <c>tiro</c>.
    /// Longe, prefere <c>tiro_distante</c>: tiro distante é abafado,
    /// não só mais baixo.</summary>
    private string ShotName(Node source, Vector3 position, bool mine)
    {
        string id = source?.GetNodeOrNull<Weapon>("Weapon")?.Held?.Id ?? "";

        if (!mine && Listener(out Vector3 ear) && ear.DistanceTo(position) > DistantShot)
        {
            string far = Sfx.First($"tiro_distante_{id}", "tiro_distante");
            if (far != null) return far;
        }

        return Sfx.First($"tiro_{id}", "tiro");
    }

    private void Play(string name, Noise noise, bool mine, float volumeDb = 0f, float reach = 0f)
    {
        if (Debug)
            GD.Print($"[som] {noise.Kind} de {noise.Source?.Name} ({(mine ? "seu" : $"raio {noise.Radius:0} m")}) → "
                + (name != null && Sfx.Has(name) ? name : $"SEM ARQUIVO ({name})"));

        if (name == null) return;

        if (mine) Sfx.Local(name, volumeDb);
        else Sfx.At(name, noise.Position, noise.Radius, volumeDb, reach);
    }

    private bool Listener(out Vector3 position)
    {
        Camera3D camera = GetViewport()?.GetCamera3D();
        position = camera?.GlobalPosition ?? Vector3.Zero;
        return camera != null;
    }

    /// <summary>Confirmação de abate: o som que diz "foi seu" sem
    /// precisar olhar o feed.</summary>
    private void OnKill(Kill kill)
    {
        if (_localHealth != null && kill.Killer == _localHealth.PublicName)
            Sfx.Local("abate");
    }

    private void UpdateMatch()
    {
        Match match = Match.Current;
        if (match == null) return;

        if (match.Phase == MatchPhase.Countdown)
        {
            int second = Mathf.CeilToInt((float)match.CountdownLeft);
            if (second != _countdownSecond && second is >= 1 and <= 3)
                Sfx.Local("contagem");
            _countdownSecond = second;
        }

        if (match.Phase == _phase) return;
        _phase = match.Phase;

        if (_phase == MatchPhase.Live)
            Sfx.Local("partida_inicio");
        else if (_phase == MatchPhase.Ended)
            Sfx.Local(match.Winner == 0 ? "empate"
                : match.Winner == (_localHealth?.Team ?? 0) ? "vitoria" : "derrota");
    }
}
