using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Um item possuído. <see cref="ExpiresUtc"/> nulo = permanente.</summary>
public sealed class OwnedItem
{
    public string Id { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public DateTime AcquiredUtc { get; set; }
    /// <summary>Usos restantes de um consumível.</summary>
    public int Uses { get; set; }

    public bool IsPermanent => ExpiresUtc == null;
    public bool IsExpired => ExpiresUtc != null && ExpiresUtc <= DateTime.UtcNow;
    public TimeSpan Remaining => ExpiresUtc == null ? TimeSpan.MaxValue : ExpiresUtc.Value - DateTime.UtcNow;
}

public sealed class RankEvent
{
    public int Rank { get; set; }
    public DateTime Utc { get; set; }
}

/// <summary>Estatísticas vitalícias.</summary>
public sealed class LifetimeStats
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Headshots { get; set; }
    public int KillsHuman { get; set; }
    public int KillsWolf { get; set; }
    public int Mvps { get; set; }
    public double SecondsPlayed { get; set; }
    public double SecondsHuman { get; set; }
    public double SecondsWolf { get; set; }
    public Dictionary<string, int> KillsByWeapon { get; set; } = new();
    public Dictionary<string, int> KillsByBreed { get; set; } = new();

    public float WinRate => Matches == 0 ? 0f : Wins / (float)Matches;
    public float KillDeath => Deaths == 0 ? Kills : Kills / (float)Deaths;

    public string FavoriteWeapon => KillsByWeapon.Count == 0 ? null
        : KillsByWeapon.OrderByDescending(p => p.Value).First().Key;

    public string FavoriteBreed => KillsByBreed.Count == 0 ? null
        : KillsByBreed.OrderByDescending(p => p.Value).First().Key;
}

/// <summary>
/// O perfil do jogador, salvo em user://perfil.json, fora do projeto.
///
/// Guarda o que sobrevive entre partidas: nickname, patente e XP, as
/// duas moedas, o inventário com a duração de cada item, os quatro
/// conjuntos, a classe de lobo e as tatuagens, as estatísticas vitalícias
/// e as missões.
///
/// O WP não mora aqui: ele nasce em zero a cada partida e morre com ela
/// (docs/CONJUNTOS.md, "regra econômica central").
///
/// Fase 2: tudo isto passa a viver no servidor. A forma dos dados já é a
/// que iria pela rede; o que muda é de onde vem.
/// </summary>
public static class Profile
{
    public const int SetCount = 4;
    public const int MaxTattoos = 2;
    public const int StarterPresas = 3000;
    public const int NameMin = 3;
    public const int NameMax = 16;

    /// <summary>Item expirado fica no inventário por este tempo antes de
    /// sumir: dá chance de renovar e evita a raiva de "perdi meu item".</summary>
    public static readonly TimeSpan ExpiredGrace = TimeSpan.FromDays(30);

    private const string FilePath = "user://perfil.json";
    private const int Version = 2;

    /// <summary>Disparado quando algo visível no header muda (moedas,
    /// XP, patente, nome).</summary>
    public static event Action Changed;

    // ------------------------------------------------------------------ dados

    public static string PlayerName { get; private set; } = "";
    public static bool HasName => !string.IsNullOrEmpty(PlayerName);
    public static DateTime CreatedUtc { get; private set; }

    public static long Xp { get; private set; }
    public static int Rank => Ranks.RankFor(Xp);
    public static List<RankEvent> RankHistory { get; private set; } = new();

    public static long Presas { get; private set; }
    public static long Sangue { get; private set; }

    public static List<OwnedItem> Inventory { get; private set; } = new();
    public static List<LoadoutSet> Sets { get; private set; } = new();
    public static int LastUsed { get; set; }

    public static string Breed { get; private set; } = "raca_bruto";
    public static List<string> Tattoos { get; private set; } = new();

    public static LifetimeStats Stats { get; private set; } = new();
    public static MissionBook Missions { get; private set; } = new();

    public static List<string> Titles { get; private set; } = new();
    public static string Title { get; set; } = "";

    /// <summary>A última sala criada, para o formulário já vir preenchido.</summary>
    public static RoomSettings LastRoom { get; set; }

    private sealed class Data
    {
        public int Version { get; set; }
        public string PlayerName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public long Xp { get; set; }
        public List<RankEvent> RankHistory { get; set; }
        public long Presas { get; set; }
        public long Sangue { get; set; }
        public List<OwnedItem> Inventory { get; set; }
        public List<LoadoutSet> Sets { get; set; }
        public int LastUsed { get; set; }
        public string Breed { get; set; }
        public List<string> Tattoos { get; set; }
        public LifetimeStats Stats { get; set; }
        public MissionBook Missions { get; set; }
        public List<string> Titles { get; set; }
        public string Title { get; set; }
        public RoomSettings LastRoom { get; set; }
    }

    static Profile() => Load();

    // ------------------------------------------------------------------ disco

    public static void Load()
    {
        Data data = null;

        try
        {
            string path = ProjectSettings.GlobalizePath(FilePath);
            if (System.IO.File.Exists(path))
                data = Parse(System.IO.File.ReadAllText(path));
        }
        catch (Exception error)
        {
            // Perfil corrompido não pode impedir o jogo de abrir.
            GD.PrintErr($"Perfil: não consegui ler ({error.Message}); começando um novo.");
        }

        data ??= Fresh();

        PlayerName = data.PlayerName ?? "";
        CreatedUtc = data.CreatedUtc == default ? DateTime.UtcNow : data.CreatedUtc;
        Xp = Math.Max(0, data.Xp);
        RankHistory = data.RankHistory ?? new List<RankEvent>();
        Presas = Math.Max(0, data.Presas);
        Sangue = Math.Max(0, data.Sangue);
        Inventory = data.Inventory ?? new List<OwnedItem>();
        Sets = data.Sets ?? new List<LoadoutSet>();
        LastUsed = data.LastUsed;
        Breed = string.IsNullOrEmpty(data.Breed) ? "raca_bruto" : data.Breed;
        Tattoos = data.Tattoos ?? new List<string>();
        Stats = data.Stats ?? new LifetimeStats();
        Missions = data.Missions ?? new MissionBook();
        Titles = data.Titles ?? new List<string>();
        Title = data.Title ?? "";
        LastRoom = data.LastRoom;

        Repair();
    }

    /// <summary>Lê as duas versões do arquivo. A 1 tinha o inventário como
    /// lista de ids e nenhum dado de progressão: vira versão 2 com tudo o
    /// que o jogador tinha mantido como permanente, as Presas iniciais e
    /// sem nickname — a tela de entrada pede um.</summary>
    private static Data Parse(string json)
    {
        JsonNode root = JsonNode.Parse(json);
        int version = root?["Version"]?.GetValue<int>() ?? 1;

        if (version >= 2)
            return JsonSerializer.Deserialize<Data>(json);

        var old = root?.AsObject();
        Data data = Fresh();

        if (old?["Inventory"] is JsonArray ids)
        {
            foreach (JsonNode id in ids)
            {
                string value = id?.GetValue<string>();
                if (!string.IsNullOrEmpty(value) && data.Inventory.All(i => i.Id != value))
                    data.Inventory.Add(new OwnedItem { Id = value, AcquiredUtc = DateTime.UtcNow });
            }
        }

        if (old?["Sets"] is JsonArray sets)
            data.Sets = JsonSerializer.Deserialize<List<LoadoutSet>>(sets.ToJsonString());

        data.LastUsed = old?["LastUsed"]?.GetValue<int>() ?? 0;

        GD.Print("Perfil: arquivo da versão 1 convertido para a 2 (itens mantidos como permanentes).");
        return data;
    }

    private static Data Fresh() => new()
    {
        Version = Version,
        PlayerName = "",
        CreatedUtc = DateTime.UtcNow,
        Presas = StarterPresas,
        RankHistory = new List<RankEvent> { new() { Rank = 0, Utc = DateTime.UtcNow } },
        Inventory = new List<OwnedItem>()
    };

    public static void Save()
    {
        try
        {
            var data = new Data
            {
                Version = Version,
                PlayerName = PlayerName,
                CreatedUtc = CreatedUtc,
                Xp = Xp,
                RankHistory = RankHistory,
                Presas = Presas,
                Sangue = Sangue,
                Inventory = Inventory,
                Sets = Sets,
                LastUsed = LastUsed,
                Breed = Breed,
                Tattoos = Tattoos,
                Stats = Stats,
                Missions = Missions,
                Titles = Titles,
                Title = Title,
                LastRoom = LastRoom
            };

            string path = ProjectSettings.GlobalizePath(FilePath);
            System.IO.File.WriteAllText(path,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            GD.PrintErr($"Perfil: não consegui salvar ({error.Message}).");
        }
    }

    /// <summary>Apaga o perfil e começa do zero (tela de opções).</summary>
    public static void Reset()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(FilePath);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        catch (Exception error)
        {
            GD.PrintErr($"Perfil: não consegui apagar ({error.Message}).");
        }

        Load();
        Save();
        Changed?.Invoke();
    }

    public static void NotifyChanged() => Changed?.Invoke();

    // ------------------------------------------------------------------ nome

    /// <summary>Valida um nickname: 3 a 16 caracteres, letras, números,
    /// "_" e "-". Devolve o erro, ou null se serve.</summary>
    public static string ValidateName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < NameMin) return $"Mínimo de {NameMin} caracteres";
        if (name.Length > NameMax) return $"Máximo de {NameMax} caracteres";
        foreach (char c in name)
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                return "Use só letras, números, _ e -";
        if (name.Equals("bot", StringComparison.OrdinalIgnoreCase)) return "Nome reservado";
        return null;
    }

    /// <summary>Primeiro nome: grátis. Trocar depois consome o item
    /// "Troca de nome".</summary>
    public static bool SetName(string name, out string error)
    {
        error = ValidateName(name);
        if (error != null) return false;

        if (HasName)
        {
            if (!Owns("troca_nome")) { error = "Precisa do item Troca de nome"; return false; }
            Consume("troca_nome");
        }

        PlayerName = name.Trim();
        Save();
        Changed?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------ inventário

    public static OwnedItem Find(string id) => Inventory.FirstOrDefault(i => i.Id == id);

    /// <summary>Possui e não expirou. Item inicial conta sempre.</summary>
    public static bool Owns(string id)
    {
        if (Catalog.Get(id)?.Starter == true) return true;
        OwnedItem item = Find(id);
        if (item == null || item.IsExpired) return false;

        bool consumable = Catalog.Get(id)?.Consumable == true;
        return !consumable || item.Uses > 0;
    }

    public static bool IsExpired(string id) => Find(id)?.IsExpired == true;

    /// <summary>Acrescenta tempo a um item: soma ao que resta se ainda
    /// não expirou, conta de agora se expirou, e permanente vence tudo.</summary>
    public static void Grant(string id, Duration duration)
    {
        CatalogItem info = Catalog.Get(id);
        OwnedItem item = Find(id);

        if (item == null)
        {
            item = new OwnedItem { Id = id, AcquiredUtc = DateTime.UtcNow, ExpiresUtc = DateTime.UtcNow };
            Inventory.Add(item);
        }

        if (info?.Consumable == true)
        {
            item.ExpiresUtc = null;
            item.Uses++;
            return;
        }

        // Já permanente continua permanente; comprar tempo não rebaixa.
        if (duration == Duration.Permanent || item.ExpiresUtc == null)
        {
            item.ExpiresUtc = null;
            return;
        }

        DateTime from = item.ExpiresUtc != null && item.ExpiresUtc > DateTime.UtcNow ? item.ExpiresUtc.Value : DateTime.UtcNow;
        item.ExpiresUtc = from.AddDays(Catalog.Days(duration));
    }

    public static void Consume(string id)
    {
        OwnedItem item = Find(id);
        if (item == null) return;
        item.Uses = Math.Max(0, item.Uses - 1);
        if (item.Uses == 0) Inventory.Remove(item);
    }

    /// <summary>Tira do inventário (descartar). Itens iniciais não saem.</summary>
    public static void Discard(string id)
    {
        if (Catalog.Get(id)?.Starter == true) return;
        Inventory.RemoveAll(i => i.Id == id);
        Tattoos.Remove(id);
        if (Breed == id) Breed = "raca_bruto";
        Repair();
        Save();
        Changed?.Invoke();
    }

    /// <summary>Está em uso em algum conjunto, ou equipado no lobo?</summary>
    public static bool IsEquipped(string id) =>
        Sets.Any(s => s.Primary == id || s.Secondary == id || s.Utilities.Contains(id))
        || Tattoos.Contains(id) || Breed == id || id == "faca";

    // ------------------------------------------------------------------ moedas

    public static long Balance(Currency currency) => currency == Currency.Presas ? Presas : Sangue;

    public static bool Spend(Currency currency, long amount)
    {
        if (amount < 0 || Balance(currency) < amount) return false;
        if (currency == Currency.Presas) Presas -= amount; else Sangue -= amount;
        Save();
        Changed?.Invoke();
        return true;
    }

    public static void Earn(Currency currency, long amount, bool save = true)
    {
        if (amount <= 0) return;
        if (currency == Currency.Presas) Presas += amount; else Sangue += amount;
        if (save) Save();
        Changed?.Invoke();
    }

    /// <summary>Compra: debita e entrega. Falha sem mexer em nada se
    /// faltar saldo, patente ou o item não estiver à venda na duração.</summary>
    public static bool Buy(string id, Duration duration, out string error)
    {
        error = null;
        CatalogItem item = Catalog.Get(id);
        int price = Catalog.Price(item, duration);

        if (item == null || price < 0) error = "Item não está à venda nessa duração";
        else if (Rank < item.MinRank) error = $"Exige a patente {Ranks.NameOf(item.MinRank)}";
        else if (Presas < price) error = "Presas insuficientes";

        if (error != null) return false;

        Presas -= price;
        Grant(id, duration);
        Save();
        Changed?.Invoke();
        return true;
    }

    // ------------------------------------------------------------------ progressão

    /// <summary>Soma XP. Devolve as patentes alcançadas (para a tela de
    /// promoção); cada promoção rende Presas e entra no histórico.</summary>
    public static List<int> AddXp(long amount)
    {
        var promotions = new List<int>();
        if (amount <= 0) return promotions;

        int before = Rank;
        Xp += amount;
        int after = Rank;

        for (int rank = before + 1; rank <= after; rank++)
        {
            promotions.Add(rank);
            Presas += Ranks.PromotionReward(rank);
            RankHistory.Add(new RankEvent { Rank = rank, Utc = DateTime.UtcNow });
        }

        Changed?.Invoke();
        return promotions;
    }

    // ------------------------------------------------------------------ lobo

    public static void SetBreed(string id)
    {
        CatalogItem item = Catalog.Get(id);
        if (item?.Kind != ItemKind.Breed || Rank < item.MinRank) return;
        Breed = id;
        Save();
    }

    public static bool EquipTattoo(string id, out string error)
    {
        error = null;
        if (Catalog.Get(id)?.Kind != ItemKind.Tattoo) error = "Não é uma tatuagem";
        else if (!Owns(id)) error = "Item fora do inventário ou expirado";
        else if (Tattoos.Contains(id)) error = "Já equipada";
        else if (Tattoos.Count >= MaxTattoos) error = $"Máximo de {MaxTattoos} tatuagens";

        if (error != null) return false;
        Tattoos.Add(id);
        Save();
        return true;
    }

    public static void UnequipTattoo(string id)
    {
        if (Tattoos.Remove(id)) Save();
    }

    /// <summary>A classe de lobo do jogador com as tatuagens aplicadas.
    /// Cópia: o recurso original é compartilhado com os bots e não pode
    /// mudar.</summary>
    public static WolfBreed BuildBreed()
    {
        CatalogItem item = Catalog.Get(Breed) ?? Catalog.Get("raca_bruto");
        var source = GD.Load<WolfBreed>(item.BreedPath);
        if (source == null) return null;

        var breed = (WolfBreed)source.Duplicate();
        foreach (string id in Tattoos)
        {
            if (!Owns(id)) continue;
            TattooEffect fx = Catalog.Get(id)?.Tattoo;
            if (fx == null) continue;

            breed.SpeedMultiplier *= fx.Speed;
            breed.JumpMultiplier *= fx.Jump;
            breed.Shield *= fx.Shield;
            breed.AttackDamage *= fx.ClawDamage;
            breed.ShieldDecay *= fx.ShieldDecay;
        }

        return breed;
    }

    // ------------------------------------------------------------------ consistência

    /// <summary>
    /// Garante o que o resto do jogo assume: itens iniciais presentes,
    /// expirados antigos removidos, quatro conjuntos com o primeiro
    /// sendo o padrão gratuito, e nada equipado que não se possua.
    /// </summary>
    private static void Repair()
    {
        foreach (CatalogItem starter in Catalog.All.Where(i => i.Starter))
            if (Find(starter.Id) == null)
                Inventory.Add(new OwnedItem { Id = starter.Id, AcquiredUtc = CreatedUtc });

        // Ids que não existem mais no catálogo saem.
        Inventory.RemoveAll(i => Catalog.Get(i.Id) == null);

        // Expirado há mais de 30 dias sai de vez.
        Inventory.RemoveAll(i => i.ExpiresUtc != null && i.ExpiresUtc.Value + ExpiredGrace < DateTime.UtcNow);

        if (Sets.Count == 0)
        {
            Sets.Add(new LoadoutSet { Name = "Padrão", IsDefault = true });
            Sets.Add(new LoadoutSet { Name = "Conjunto 2" });
            Sets.Add(new LoadoutSet { Name = "Conjunto 3" });
            Sets.Add(new LoadoutSet { Name = "Conjunto 4" });
        }

        while (Sets.Count < SetCount)
            Sets.Add(new LoadoutSet { Name = $"Conjunto {Sets.Count + 1}" });
        if (Sets.Count > SetCount)
            Sets.RemoveRange(SetCount, Sets.Count - SetCount);

        LoadoutSet first = Sets[0];
        first.IsDefault = true;
        first.Primary = Arsenal.DefaultPrimary;
        first.Secondary = Arsenal.DefaultSecondary;
        first.Utilities.Clear();
        if (string.IsNullOrEmpty(first.Name)) first.Name = "Padrão";

        for (int i = 1; i < Sets.Count; i++)
            Sets[i].IsDefault = false;

        LastUsed = Mathf.Clamp(LastUsed, 0, SetCount - 1);

        if (Catalog.Get(Breed)?.Kind != ItemKind.Breed || Rank < Catalog.Get(Breed).MinRank)
            Breed = "raca_bruto";

        Tattoos = Tattoos.Where(id => Catalog.Get(id)?.Kind == ItemKind.Tattoo).Distinct().Take(MaxTattoos).ToList();

        if (RankHistory.Count == 0)
            RankHistory.Add(new RankEvent { Rank = 0, Utc = CreatedUtc });

        Missions.Refresh(DateTime.Now);
    }
}
