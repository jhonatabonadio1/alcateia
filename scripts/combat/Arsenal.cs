using Godot;
using System.Collections.Generic;

/// <summary>Um utilitário (granada) que cabe num conjunto.</summary>
public sealed class UtilityInfo
{
    public string Id;
    public string DisplayName;
    public int WpCost;
    public string Description;

    /// <summary>Granadas que o item dá por vida.</summary>
    public int Count;

    /// <summary>O segundo espaço de utilitário só aceita luz ou fumaça.</summary>
    public bool AllowedInSecondSlot;
}

/// <summary>
/// O catálogo: que armas e utilitários existem e quanto custam.
///
/// Uma fonte só para preço e números. O custo de um conjunto nunca é
/// guardado em lugar nenhum — é sempre somado a partir daqui — então
/// mudar o preço de uma arma chega a todos os conjuntos salvos sem
/// migração nenhuma.
///
/// Só a primeira leva do documento está aqui (docs/ARSENAL.md, "ordem
/// de implementação"): cinco primárias bastam para descobrir se humano
/// contra lobo funciona, e as outras bagunçam o balanceamento se
/// entrarem antes disso.
/// </summary>
public static class Arsenal
{
    /// <summary>
    /// Escala do dano do documento.
    ///
    /// O documento foi calibrado para 100 HP e o humano tem 200. Dobrar
    /// o dano preserva os "tiros para matar" e o TTK de cada arma — o
    /// que é o balanceamento de verdade — sem mexer nos números
    /// escritos, que continuam conferíveis contra o documento.
    /// </summary>
    public const float DamageScale = 2f;

    public static readonly string[] Primaries = { "m16a2", "sg552", "ak47", "ump45", "m3" };
    public static readonly string[] Secondaries = { "m9" };

    public const string DefaultPrimary = "m16a2";
    public const string DefaultSecondary = "m9";

    public static readonly UtilityInfo[] Utilities =
    {
        new UtilityInfo
        {
            Id = "choque",
            DisplayName = "Granada de choque",
            WpCost = 60,
            Count = 1,
            AllowedInSecondSlot = false,
            Description = "Anti-lobo. Pouco dano, muita lentidão: não mata, tira a mobilidade que define a forma."
        }
    };

    private static readonly Dictionary<string, WeaponDefinition> _cache = new();

    public static WeaponDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_cache.TryGetValue(id, out WeaponDefinition cached)) return cached;

        var definition = GD.Load<WeaponDefinition>($"res://resources/weapons/{id}.tres");
        if (definition == null)
            GD.PrintErr($"Arsenal: arma '{id}' não encontrada.");

        _cache[id] = definition;
        return definition;
    }

    public static UtilityInfo GetUtility(string id)
    {
        foreach (UtilityInfo utility in Utilities)
            if (utility.Id == id) return utility;

        return null;
    }

    /// <summary>Custo de qualquer item do catálogo, arma ou utilitário.</summary>
    public static int CostOf(string id)
    {
        UtilityInfo utility = GetUtility(id);
        if (utility != null) return utility.WpCost;

        return Get(id)?.WpCost ?? 0;
    }

    public static string NameOf(string id)
    {
        UtilityInfo utility = GetUtility(id);
        if (utility != null) return utility.DisplayName;

        return Get(id)?.DisplayName ?? id;
    }

    /// <summary>Todos os ids do catálogo, para o inventário.</summary>
    public static IEnumerable<string> AllIds()
    {
        foreach (string id in Primaries) yield return id;
        foreach (string id in Secondaries) yield return id;
        foreach (UtilityInfo utility in Utilities) yield return utility.Id;
    }
}
