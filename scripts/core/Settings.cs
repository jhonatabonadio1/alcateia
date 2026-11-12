using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Opções do jogador: volumes por canal e sensibilidade do mouse. Salvas
/// em user://opcoes.json, separadas do perfil — opção é da máquina, não
/// da conta.
/// </summary>
public static class Settings
{
    private const string FilePath = "user://opcoes.json";

    public sealed class Data
    {
        public float Master { get; set; } = 0.9f;
        public float Sfx { get; set; } = 1f;
        public float Interface { get; set; } = 0.8f;
        public float Music { get; set; } = 0.6f;
        /// <summary>Multiplicador sobre a sensibilidade base do Player.</summary>
        public float Sensitivity { get; set; } = 1f;
    }

    public static Data Values { get; private set; } = new();

    public static event Action Changed;

    static Settings() => Load();

    public static void Load()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(FilePath);
            if (System.IO.File.Exists(path))
                Values = JsonSerializer.Deserialize<Data>(System.IO.File.ReadAllText(path)) ?? new Data();
        }
        catch (Exception error)
        {
            GD.PrintErr($"Opções: não consegui ler ({error.Message}); usando o padrão.");
            Values = new Data();
        }
    }

    public static void Save()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(FilePath);
            System.IO.File.WriteAllText(path, JsonSerializer.Serialize(Values, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            GD.PrintErr($"Opções: não consegui salvar ({error.Message}).");
        }

        Changed?.Invoke();
    }

    /// <summary>Aplica os volumes nos canais de áudio. Os canais são
    /// criados pelo Sfx; chamar antes disso é seguro (o Sfx cria).</summary>
    public static void ApplyAudio()
    {
        Sfx.EnsureBuses();
        SetBus("Master", Values.Master);
        SetBus(Sfx.BusSfx, Values.Sfx);
        SetBus(Sfx.BusUi, Values.Interface);
        SetBus(Sfx.BusMusic, Values.Music);
    }

    private static void SetBus(string name, float linear)
    {
        int index = AudioServer.GetBusIndex(name);
        if (index < 0) return;
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
        AudioServer.SetBusMute(index, linear <= 0.001f);
    }
}
