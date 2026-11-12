using Godot;
using System.Collections.Generic;

/// <summary>
/// Toca sons por nome, a partir de <c>res://assets/audio/</c>.
///
/// O nome é o contrato: <c>Sfx.At("passo", ...)</c> procura
/// <c>passo.wav</c>, <c>passo.ogg</c> ou <c>passo.mp3</c>, e também as
/// variações <c>passo_1</c>, <c>passo_2</c>... Todas as que existirem
/// entram num sorteio sem repetição seguida, com o tom variando um
/// pouco a cada vez — quatro arquivos soam como quarenta, e o passo não
/// vira metralhadora.
///
/// **Arquivo que não existe fica mudo, sem erro.** É o que deixa o jogo
/// ganhar som aos poucos: cada arquivo que entra na pasta passa a tocar,
/// sem mexer em código. A lista de nomes está em
/// <c>assets/audio/LEIA-ME.md</c>.
///
/// Os tocadores ficam na raiz da árvore, não na cena: um tiro que
/// termina de tocar depois de quem atirou sumir (morte, troca de cena)
/// não é cortado no meio.
/// </summary>
public static class Sfx
{
    public const string Folder = "res://assets/audio";

    public const string BusSfx = "SFX";
    public const string BusUi = "Interface";
    public const string BusMusic = "Musica";

    /// <summary>Teto de sons simultâneos. Um tiroteio de dezesseis
    /// pessoas passa disso; o que excede é o som que ninguém
    /// distinguiria mesmo.</summary>
    public const int MaxVoices = 48;

    /// <summary>Variação de tom, como fator: 1,06 = até 6% para cima ou
    /// para baixo.</summary>
    public const float PitchVariation = 1.06f;

    private static readonly string[] Extensions = { ".wav", ".ogg", ".mp3" };
    private static readonly Dictionary<string, AudioStream> _cache = new();
    private static readonly HashSet<string> _missing = new();
    private static int _voices;
    private static bool _busesReady;

    /// <summary>Existe algum arquivo para este nome?</summary>
    public static bool Has(string name) => Get(name) != null;

    /// <summary>O primeiro nome da lista que tiver arquivo, ou null.
    /// Serve para "som desta arma, senão o tiro genérico".</summary>
    public static string First(params string[] names)
    {
        foreach (string name in names)
            if (Has(name)) return name;
        return null;
    }

    /// <summary>
    /// Som num ponto do mundo. <paramref name="radius"/> é até onde ele
    /// chega, em metros — use o mesmo raio do ruído que os bots ouvem,
    /// para o jogador e o bot ouvirem a mesma coisa.
    /// </summary>
    public static void At(string name, Vector3 position, float radius, float volumeDb = 0f)
    {
        AudioStream stream = Get(name);
        if (stream == null || !Reserve(out Window root)) return;

        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            Bus = BusSfx,
            VolumeDb = volumeDb,
            MaxDistance = Mathf.Max(radius, 1f),

            // Com esta escala o som chega à borda do raio uns 16 dB
            // abaixo do volume de perto: audível, mas claramente longe.
            UnitSize = Mathf.Max(radius * 0.15f, 1.5f),
            DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled,
            Position = position
        };

        Start(root, player, () => player.Play(), () => player.Finished += () => Release(player));
    }

    /// <summary>Som sem posição: o que é seu (seu tiro, seu passo, o
    /// marcador de acerto). Soa igual nos dois ouvidos.</summary>
    public static void Local(string name, float volumeDb = 0f) => Flat(name, BusSfx, volumeDb);

    /// <summary>Som de interface: cliques, passar o mouse.</summary>
    public static void Ui(string name, float volumeDb = 0f) => Flat(name, BusUi, volumeDb);

    /// <summary>
    /// Música em loop, filha de <paramref name="owner"/>: para quando o
    /// dono sai de cena. Devolve null se o arquivo não existir.
    /// </summary>
    public static AudioStreamPlayer Music(Node owner, string name, float volumeDb = -8f)
    {
        EnsureBuses();

        AudioStream stream = LoadAny($"{Folder}/{name}");
        if (stream == null) return null;

        // .ogg e .mp3 têm a propriedade "loop"; o .wav marca o laço por
        // amostra. Mais simples ainda: ligar "Loop" na aba Import do
        // Godot, e aí nada disto precisa agir.
        if (stream is AudioStreamWav wav)
        {
            if (wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
            }
        }
        else
        {
            stream.Set("loop", true);
        }

        var player = new AudioStreamPlayer { Stream = stream, Bus = BusMusic, VolumeDb = volumeDb, Autoplay = true };
        owner.AddChild(player);
        return player;
    }

    /// <summary>Clique e passar o mouse num botão. Botão desabilitado
    /// não faz som: é um "não" que o jogador já está vendo.</summary>
    public static void Wire(BaseButton button)
    {
        button.MouseEntered += () =>
        {
            if (!button.Disabled) Ui("ui_passar");
        };
        button.Pressed += () => Ui("ui_clique");
    }

    // ------------------------------------------------------------------

    private static void Flat(string name, string bus, float volumeDb)
    {
        AudioStream stream = Get(name);
        if (stream == null || !Reserve(out Window root)) return;

        var player = new AudioStreamPlayer { Stream = stream, Bus = bus, VolumeDb = volumeDb };
        Start(root, player, () => player.Play(), () => player.Finished += () => Release(player));
    }

    private static void Start(Window root, Node player, System.Action play, System.Action hook)
    {
        _voices++;
        hook();
        root.AddChild(player);
        play();
    }

    private static bool Reserve(out Window root)
    {
        root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null || _voices >= MaxVoices) return false;

        EnsureBuses();
        return true;
    }

    private static void Release(Node player)
    {
        _voices = Mathf.Max(_voices - 1, 0);
        player.QueueFree();
    }

    private static AudioStream Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_cache.TryGetValue(name, out AudioStream cached)) return cached;
        if (_missing.Contains(name)) return null;

        var found = new List<AudioStream>();

        AudioStream single = LoadAny($"{Folder}/{name}");
        if (single != null) found.Add(single);

        for (int i = 1; i <= 32; i++)
        {
            AudioStream variation = LoadAny($"{Folder}/{name}_{i}");
            if (variation == null) break;
            found.Add(variation);
        }

        if (found.Count == 0)
        {
            _missing.Add(name);
            return null;
        }

        var random = new AudioStreamRandomizer
        {
            RandomPitch = PitchVariation,
            RandomVolumeOffsetDb = 1f,
            PlaybackMode = found.Count > 1
                ? AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats
                : AudioStreamRandomizer.PlaybackModeEnum.Random
        };

        foreach (AudioStream stream in found)
            random.AddStream(-1, stream);

        _cache[name] = random;
        return random;
    }

    private static AudioStream LoadAny(string basePath)
    {
        foreach (string extension in Extensions)
        {
            string path = basePath + extension;
            if (ResourceLoader.Exists(path))
                return GD.Load<AudioStream>(path);
        }

        return null;
    }

    /// <summary>
    /// Canais de volume, criados por código como as teclas do
    /// InputSetup: uma fonte só, sem depender do editor. Separados para
    /// o menu de opções poder ter um volume para cada.
    /// </summary>
    public static void EnsureBuses()
    {
        if (_busesReady) return;
        _busesReady = true;

        foreach (string bus in new[] { BusSfx, BusUi, BusMusic })
        {
            if (AudioServer.GetBusIndex(bus) >= 0) continue;

            int index = AudioServer.BusCount;
            AudioServer.AddBus(index);
            AudioServer.SetBusName(index, bus);
            AudioServer.SetBusSend(index, "Master");
        }
    }
}
