using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// O perfil do jogador: inventário e os quatro conjuntos salvos.
///
/// Mora em user://perfil.json, fora do projeto, e sobrevive entre uma
/// partida e outra. O WP não mora aqui: ele nasce em zero a cada partida
/// e morre com ela (docs/CONJUNTOS.md, "regra econômica central").
///
/// O inventário hoje tem tudo o que existe no arsenal. Quando houver
/// loja, é aqui que a compra entra — o editor de conjuntos já só aceita
/// item do inventário, então nada mais muda.
/// </summary>
public static class Profile
{
    public const int SetCount = 4;
    private const string FilePath = "user://perfil.json";

    public static List<LoadoutSet> Sets { get; private set; }
    public static List<string> Inventory { get; private set; }

    /// <summary>Último conjunto escolhido — o que entra se o tempo da
    /// tela de respawn acabar.</summary>
    public static int LastUsed { get; set; }

    public static string PlayerName { get; set; } = "VOCÊ";

    private sealed class Data
    {
        public List<LoadoutSet> Sets { get; set; }
        public List<string> Inventory { get; set; }
        public int LastUsed { get; set; }
        public string PlayerName { get; set; }
    }

    static Profile() => Load();

    public static bool Owns(string id) => Inventory.Contains(id);

    public static void Load()
    {
        Data data = null;

        try
        {
            string path = ProjectSettings.GlobalizePath(FilePath);
            if (System.IO.File.Exists(path))
                data = JsonSerializer.Deserialize<Data>(System.IO.File.ReadAllText(path));
        }
        catch (System.Exception error)
        {
            // Perfil corrompido não pode impedir o jogo de abrir. Volta
            // para o padrão e avisa.
            GD.PrintErr($"Perfil: não consegui ler ({error.Message}); usando o padrão.");
        }

        Inventory = data?.Inventory ?? new List<string>();
        Sets = data?.Sets ?? new List<LoadoutSet>();
        LastUsed = data?.LastUsed ?? 0;
        PlayerName = string.IsNullOrEmpty(data?.PlayerName) ? "VOCÊ" : data.PlayerName;

        Repair();
    }

    public static void Save()
    {
        try
        {
            var data = new Data
            {
                Sets = Sets,
                Inventory = Inventory,
                LastUsed = LastUsed,
                PlayerName = PlayerName
            };

            string path = ProjectSettings.GlobalizePath(FilePath);
            System.IO.File.WriteAllText(path,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (System.Exception error)
        {
            GD.PrintErr($"Perfil: não consegui salvar ({error.Message}).");
        }
    }

    /// <summary>
    /// Garante o que o resto do jogo assume: inventário com tudo que
    /// existe, exatamente quatro conjuntos, e o primeiro sendo o padrão
    /// gratuito. Roda também em perfil velho, de antes de uma arma nova.
    /// </summary>
    private static void Repair()
    {
        foreach (string id in Arsenal.AllIds())
            if (!Inventory.Contains(id)) Inventory.Add(id);

        if (Sets.Count == 0)
        {
            Sets.Add(new LoadoutSet { Name = "Padrão", IsDefault = true });
            Sets.Add(new LoadoutSet { Name = "Assalto", Primary = "ak47" });
            Sets.Add(new LoadoutSet { Name = "Caçador", Primary = "m3", Utilities = new List<string> { "choque" } });
            Sets.Add(new LoadoutSet { Name = "Móvel", Primary = "ump45", Utilities = new List<string> { "choque" } });
        }

        while (Sets.Count < SetCount)
            Sets.Add(new LoadoutSet { Name = $"Conjunto {Sets.Count + 1}" });

        if (Sets.Count > SetCount)
            Sets.RemoveRange(SetCount, Sets.Count - SetCount);

        // O padrão é sempre o primeiro, sempre gratuito, sempre válido.
        // É a rede de segurança de quem está sem WP; não pode quebrar.
        LoadoutSet first = Sets[0];
        first.IsDefault = true;
        first.Primary = Arsenal.DefaultPrimary;
        first.Secondary = Arsenal.DefaultSecondary;
        first.Utilities.Clear();

        for (int i = 1; i < Sets.Count; i++)
            Sets[i].IsDefault = false;

        LastUsed = Mathf.Clamp(LastUsed, 0, SetCount - 1);
    }
}
