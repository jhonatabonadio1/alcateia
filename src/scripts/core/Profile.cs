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

	public float WinRate => Matches == 0 ? 0f : Wins / (float)Matches;
	public float KillDeath => Deaths == 0 ? Kills : Kills / (float)Deaths;

	public string FavoriteWeapon => KillsByWeapon.Count == 0 ? null
		: KillsByWeapon.OrderByDescending(p => p.Value).First().Key;
}

/// <summary>
/// O perfil do jogador, salvo em user://profile.json, fora do projeto.
///
/// Guarda o que sobrevive entre partidas: nickname, patente e XP, as
/// duas moedas, o inventário com a duração de cada item, os quatro
/// conjuntos, a classe de lobo e as melhorias, as estatísticas vitalícias
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
	public const int MaxWolfUpgrades = 2;
	public const int NameMin = 3;
	public const int NameMax = 16;

	/// <summary>Item expirado fica no inventário por este tempo antes de
	/// sumir: dá chance de renovar e evita a raiva de "perdi meu item".</summary>
	public static readonly TimeSpan ExpiredGrace = TimeSpan.FromDays(30);

	private static string FilePath => UserFiles.Resolve(UserFiles.Profile, "user://perfil.json");
	private const int Version = 4;

	/// <summary>O arquivo lido era de antes da versão 3: o conjunto padrão
	/// ganha a granada, uma vez (<see cref="Repair"/>).</summary>
	private static bool _grenadeToDefault;

	/// <summary>Disparado quando algo visível no header muda (moedas,
	/// XP, patente, nome).</summary>
	public static event Action Changed;

	// ------------------------------------------------------------------ dados

	public static string PlayerName { get; private set; } = "";
	public static bool HasName => !string.IsNullOrEmpty(PlayerName);
	public static DateTime CreatedUtc { get; private set; }

	// ---- o que é da conta (docs/decisoes/0015) ----
	//
	// XP, Presas, Sangue, itens, missões e títulos moram no servidor. Aqui
	// fica só o espelho do que a conta tem agora, enquanto se está nela:
	// não vai para o arquivo, e sem conta é tudo zero (só os itens de série).

	/// <summary>Há uma conta carregada: os valores abaixo são os dela.</summary>
	public static bool HasAccount { get; private set; }

	/// <summary>A conta chegou, mudou ou saiu (as telas que dependem dela se refazem).</summary>
	public static event Action AccountChanged;

	public static long Xp { get; private set; }

	/// <summary>O XP que dá a patente mostrada (o da conta).</summary>
	public static long RankXp => Xp;
	public static int Rank => Ranks.RankFor(Xp);

	public static List<RankEvent> RankHistory { get; private set; } = new();

	public static long Presas { get; private set; }
	public static long Sangue { get; private set; }

	public static List<OwnedItem> Inventory { get; private set; } = new();
	public static List<LoadoutSet> Sets { get; private set; } = new();
	public static int LastUsed { get; set; }

	public static List<string> WolfUpgrades { get; private set; } = new();

	/// <summary>O personagem de cada time (id do catálogo), ou nulo para o
	/// de sempre (Exo Gray no azul, Ely no laranja).</summary>
	public static string BlueCharacter { get; private set; }
	public static string OrangeCharacter { get; private set; }

	public static LifetimeStats Stats { get; private set; } = new();
	public static MissionBook Missions { get; private set; } = new();

	public static List<string> Titles { get; private set; } = new();
	public static string Title { get; set; } = "";

	/// <summary>A última sala criada, para o formulário já vir preenchido.</summary>
	public static RoomSettings LastRoom { get; set; }

	/// <summary>O arquivo do jogador: as escolhas dele (conjuntos, lobo,
	/// personagem, título em uso, última sala) e as estatísticas locais.
	/// Nada que valha alguma coisa: isso é da conta. Arquivo antigo, com
	/// Presas e itens, é lido sem erro e esses campos são ignorados.</summary>
	private sealed class Data
	{
		public int Version { get; set; }
		public string PlayerName { get; set; }
		public DateTime CreatedUtc { get; set; }
		public List<RankEvent> RankHistory { get; set; }
		public List<LoadoutSet> Sets { get; set; }
		public int LastUsed { get; set; }
		public List<string> WolfUpgrades { get; set; }
		/// <summary>As melhorias de lobo no arquivo de antes de 27/11, quando se
		/// chamavam tatuagens (ids <c>tat_*</c>). Só se lê; o arquivo novo grava
		/// <see cref="WolfUpgrades"/>.</summary>
		[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
		public List<string> Tattoos { get; set; }
		public string BlueCharacter { get; set; }
		public string OrangeCharacter { get; set; }
		public LifetimeStats Stats { get; set; }
		public string Title { get; set; }
		public RoomSettings LastRoom { get; set; }
	}

	// ------------------------------------------------------------------ disco

	static Profile() => Load();

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
			GD.PrintErr($"Profile: could not read ({error.Message}); starting a new one.");
		}

		data ??= Fresh();
		_grenadeToDefault = data.Version < 3;

		PlayerName = data.PlayerName ?? "";
		CreatedUtc = data.CreatedUtc == default ? DateTime.UtcNow : data.CreatedUtc;
		RankHistory = data.RankHistory ?? new List<RankEvent>();
		Sets = data.Sets ?? new List<LoadoutSet>();
		LastUsed = data.LastUsed;
		WolfUpgrades = (data.WolfUpgrades ?? data.Tattoos ?? new List<string>()).Select(Catalog.CurrentId).ToList();
		BlueCharacter = data.BlueCharacter;
		OrangeCharacter = data.OrangeCharacter;
		Stats = data.Stats ?? new LifetimeStats();
		Title = data.Title ?? "";
		LastRoom = data.LastRoom;

		Repair();
	}

	/// <summary>Lê o arquivo. O da versão 1 só tinha os conjuntos: o resto
	/// começa novo e a tela de entrada pede um nome.</summary>
	private static Data Parse(string json)
	{
		JsonNode root = JsonNode.Parse(json);
		int version = root?["Version"]?.GetValue<int>() ?? 1;

		if (version >= 2)
			return JsonSerializer.Deserialize<Data>(json);

		var old = root?.AsObject();
		Data data = Fresh();
		data.Version = 1;
		if (old?["Sets"] is JsonArray sets)
			data.Sets = JsonSerializer.Deserialize<List<LoadoutSet>>(sets.ToJsonString());
		data.LastUsed = old?["LastUsed"]?.GetValue<int>() ?? 0;
		return data;
	}

	private static Data Fresh() => new()
	{
		Version = Version,
		PlayerName = "",
		CreatedUtc = DateTime.UtcNow,
		RankHistory = new List<RankEvent> { new() { Rank = 0, Utc = DateTime.UtcNow } }
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
				RankHistory = RankHistory,
				Sets = Sets,
				LastUsed = LastUsed,
				WolfUpgrades = WolfUpgrades,
				BlueCharacter = BlueCharacter,
				OrangeCharacter = OrangeCharacter,
				Stats = Stats,
				Title = Title,
				LastRoom = LastRoom
			};

			string path = ProjectSettings.GlobalizePath(FilePath);
			System.IO.File.WriteAllText(path,
				JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception error)
		{
			GD.PrintErr($"Profile: could not save ({error.Message}).");
		}
	}

	public static void NotifyChanged() => Changed?.Invoke();

	// ------------------------------------------------------------------ conta

	/// <summary>
	/// O servidor disse o que a conta tem agora: carteira, XP e itens.
	/// Devolve as patentes alcançadas desde a última vez que ele disse
	/// (para a tela de promoção) — na primeira, nenhuma.
	/// </summary>
	/// <summary>Acima de tantos níveis de uma vez, é salto (o painel), não progresso: veja
	/// <see cref="ApplyAccount"/>. Uma partida ou um prêmio de missão rendem bem menos.</summary>
	private const int BigJump = 10;

	public static List<int> ApplyAccount(long xp, long presas, long sangue, IEnumerable<OwnedItem> items)
	{
		var promotions = new List<int>();
		int before = Rank;
		bool first = !HasAccount;

		HasAccount = true;
		Xp = Math.Max(0, xp);
		Presas = Math.Max(0, presas);
		Sangue = Math.Max(0, sangue);
		Inventory = items?.ToList() ?? new List<OwnedItem>();

		if (!first && Rank - before > BigJump)
		{
			// Um salto grande — o painel pôs a conta num nível (Jhon, 27/11) — comemora só o
			// último nível marcante dele, não cada degrau: subir do 1 ao 500 eram dezenas de
			// telas seguidas. O histórico guarda o nível final.
			int shown = Rank;
			for (int rank = Rank; rank > before; rank--)
				if (Ranks.IsMilestone(rank)) { shown = rank; break; }
			promotions.Add(shown);
			RankHistory.Add(new RankEvent { Rank = Rank, Utc = DateTime.UtcNow });
		}
		else if (!first)
			for (int rank = before + 1; rank <= Rank; rank++)
			{
				promotions.Add(rank);
				RankHistory.Add(new RankEvent { Rank = rank, Utc = DateTime.UtcNow });
			}

		Repair();
		if (promotions.Count > 0) Save();
		Changed?.Invoke();
		AccountChanged?.Invoke();
		return promotions;
	}

	/// <summary>Saiu da conta: sem carteira, sem itens, sem missões. As
	/// escolhas (conjuntos, lobo) ficam como estão, para a volta.</summary>
	public static void ClearAccount()
	{
		if (!HasAccount && Xp == 0 && Presas == 0 && Inventory.Count == 0) return;
		HasAccount = false;
		Xp = Presas = Sangue = 0;
		Inventory = new List<OwnedItem>();
		Missions = new MissionBook();
		Titles = new List<string>();
		Repair();
		Changed?.Invoke();
		AccountChanged?.Invoke();
	}

	/// <summary>Os títulos que a conta ganhou (os dos desafios resgatados).</summary>
	public static void ApplyTitles(IEnumerable<string> titles)
	{
		Titles = titles?.ToList() ?? new List<string>();
		if (!string.IsNullOrEmpty(Title) && HasAccount && !Titles.Contains(Title)) Title = "";
	}

	/// <summary>Com conta, o nome do jogador é o da conta.</summary>
	public static void UseAccountName(string name)
	{
		if (string.IsNullOrEmpty(name) || name == PlayerName) return;
		PlayerName = name;
		Save();
		Changed?.Invoke();
	}

	// ------------------------------------------------------------------ nome

	/// <summary>Valida um nickname: 3 a 16 caracteres, letras, números,
	/// "_" e "-". Devolve o erro, ou null se serve.</summary>
	public static string ValidateName(string name)
	{
		name = (name ?? "").Trim();
		if (name.Length < NameMin) return Loc.F("Mínimo de {0} caracteres", NameMin);
		if (name.Length > NameMax) return Loc.F("Máximo de {0} caracteres", NameMax);
		foreach (char c in name)
			if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
				return "Use só letras, números, _ e -";
		if (name.Equals("bot", StringComparison.OrdinalIgnoreCase)) return "Nome reservado";
		return null;
	}

	/// <summary>O nome com que se joga sem conta, escolhido na primeira
	/// abertura. Com conta vale o nome dela; trocar esse é com o item
	/// Troca de nome, pelo servidor (<see cref="AccountSync.Rename"/>).</summary>
	public static bool SetName(string name, out string error)
	{
		error = ValidateName(name);
		if (error != null) return false;
		if (HasName && HasAccount) { error = "Com conta, o nome se troca com o item Troca de nome"; return false; }

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

	/// <summary>Está em uso em algum conjunto, ou equipado no lobo?</summary>
	public static bool IsEquipped(string id) =>
		Sets.Any(s => s.Primary == id || s.Secondary == id || s.Melee == id || s.Utilities.Contains(id))
		|| WolfUpgrades.Contains(id) || id == Arsenal.KnifeId
		|| BlueCharacter == id || OrangeCharacter == id;

	// ------------------------------------------------------------------ moedas

	/// <summary>O saldo da conta. Comprar, ganhar e gastar é com o
	/// servidor (<see cref="AccountSync"/>): aqui só se lê.</summary>
	public static long Balance(Currency currency) => currency == Currency.Presas ? Presas : Sangue;

	// ------------------------------------------------------------------ lobo

	public static bool EquipWolfUpgrade(string id, out string error)
	{
		error = null;
		if (Catalog.Get(id)?.Kind != ItemKind.WolfUpgrade) error = "Não é uma melhoria de lobo";
		else if (!Owns(id)) error = "Item fora do inventário ou expirado";
		else if (WolfUpgrades.Contains(id)) error = "Já equipada";
		else if (WolfUpgrades.Count >= MaxWolfUpgrades) error = Loc.F("Máximo de {0} melhorias de lobo", MaxWolfUpgrades);

		if (error != null) return false;
		WolfUpgrades.Add(id);
		Save();
		return true;
	}

	public static void UnequipWolfUpgrade(string id)
	{
		if (WolfUpgrades.Remove(id)) Save();
	}

	/// <summary>A classe de lobo do jogador com as melhorias aplicadas.
	/// Cópia: o recurso original é compartilhado com os bots e não pode
	/// mudar.</summary>
	/// <summary>O lobo de todos (um só desde 26/11: as classes Bruto e
	/// Ligeiro saíram, decisão do Jhon).</summary>
	public const string WolfPath = "res://src/resources/breeds/lobo.tres";

	public static WolfBreed BuildBreed() => BuildBreed(OwnedWolfUpgrades());

	/// <summary>As melhorias equipadas que o jogador tem de fato.</summary>
	public static System.Collections.Generic.List<string> OwnedWolfUpgrades() => WolfUpgrades.Where(Owns).ToList();

	/// <summary>O lobo com as melhorias somadas. Uma cópia: o recurso
	/// original é o mesmo de todos. Também é como o servidor de partida
	/// monta o lobo de cada jogador (o jogo conta quais melhorias).</summary>
	public static WolfBreed BuildBreed(System.Collections.Generic.IEnumerable<string> upgrades)
	{
		var source = GD.Load<WolfBreed>(WolfPath);
		if (source == null) return null;

		var breed = (WolfBreed)source.Duplicate();
		foreach (string id in upgrades)
		{
			WolfUpgradeEffect fx = Catalog.Get(id)?.WolfUpgrade;
			if (fx == null) continue;

			breed.SpeedMultiplier *= fx.Speed;
			breed.JumpMultiplier *= fx.Jump;
			breed.Shield *= fx.Shield;
			breed.AttackDamage *= fx.ClawDamage;
			breed.ShieldDecay *= fx.ShieldDecay;
		}

		return breed;
	}

	// ------------------------------------------------------------------ personagem

	/// <summary>O id do personagem escolhido para o time, ou nulo.</summary>
	public static string CharacterIdFor(int team) =>
		team == Teams.Blue ? BlueCharacter : team == Teams.Orange ? OrangeCharacter : null;

	/// <summary>O personagem que o jogador usa no time, ou nulo para o de
	/// sempre. Só o que ele possui e que vale para aquele time.</summary>
	public static CharacterSkin CharacterFor(int team)
	{
		CatalogItem item = Catalog.Get(CharacterIdFor(team));
		return FitsTeam(item, team) && Owns(item.Id) ? item.Character : null;
	}

	/// <summary>Usar o personagem no time dele. Personagem dos dois times
	/// entra nos dois.</summary>
	public static bool SetCharacter(string id, out string error)
	{
		error = null;
		CatalogItem item = Catalog.Get(id);
		if (item?.Kind != ItemKind.Character) error = "Não é um personagem";
		else if (!Owns(id)) error = "Personagem fora do inventário ou expirado";
		if (error != null) return false;

		if (FitsTeam(item, Teams.Blue)) BlueCharacter = id;
		if (FitsTeam(item, Teams.Orange)) OrangeCharacter = id;
		Save();
		return true;
	}

	/// <summary>Volta o time ao personagem de sempre.</summary>
	public static void ClearCharacter(int team)
	{
		if (team == Teams.Blue) BlueCharacter = null;
		else if (team == Teams.Orange) OrangeCharacter = null;
		Save();
	}

	/// <summary>Esta arma pode estar neste espaço de um conjunto: existe,
	/// é do espaço certo, o jogador a tem e, no padrão, ela é liberada.</summary>
	private static bool Fits(string id, bool secondary, bool isDefault, bool ownership)
	{
		if (string.IsNullOrEmpty(id) || Arsenal.Get(id) == null || Catalog.Get(id) == null) return false;
		if (Arsenal.IsSecondary(id) != secondary) return false;
		if (ownership && !Owns(id)) return false;
		return !isDefault || LoadoutSet.AllowedInDefault(id);
	}

	private static bool FitsTeam(CatalogItem item, int team) =>
		item?.Kind == ItemKind.Character && item.Character != null && (item.Team == 0 || item.Team == team);

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
			// A granada já vem no padrão (19/11: "granada deve entrar no kit
			// padrão por padrão").
			Sets.Add(new LoadoutSet { Name = "Padrão", IsDefault = true, Utilities = new List<string> { Arsenal.GrenadeId } });
			Sets.Add(new LoadoutSet { Name = "Conjunto 2" });
			Sets.Add(new LoadoutSet { Name = "Conjunto 3" });
			Sets.Add(new LoadoutSet { Name = "Conjunto 4" });
		}

		while (Sets.Count < SetCount)
			Sets.Add(new LoadoutSet { Name = $"Conjunto {Sets.Count + 1}" });
		if (Sets.Count > SetCount)
			Sets.RemoveRange(SetCount, Sets.Count - SetCount);

		// O que o jogador escolheu fica (19/11: "as mudanças no inventário
		// não persistem quando fecho e abro o jogo" — até aqui o padrão
		// voltava sempre para AR-15 e M9 sem granada, mesmo com a AR-15
		// Ultimate e a granada liberadas para ele). Só sai o que não existe
		// mais, não é dele ou não cabe no espaço.
		// O que depende de possuir (ou da patente) só se confere com a conta
		// carregada: sem ela não se sabe o que é dele, e limpar aqui apagaria
		// os conjuntos de quem só ainda não entrou. Na hora de usar vale
		// Usable(), que troca pelo padrão o que não é dele.
		for (int i = 0; i < Sets.Count; i++) Sanitize(Sets[i], i == 0, ownership: HasAccount);

		LoadoutSet first = Sets[0];
		first.IsDefault = true;

		// Perfil de antes da versão 3: o padrão ganha a granada, uma vez. Quem
		// tirar depois, fica sem — o arquivo já sai na versão nova.
		if (_grenadeToDefault)
		{
			_grenadeToDefault = false;
			if (first.GrenadeCount() == 0 && first.Utilities.Count < LoadoutSet.MaxUtilities) first.Utilities.Add(Arsenal.GrenadeId);
		}
		if (string.IsNullOrEmpty(first.Name)) first.Name = "Padrão";

		for (int i = 1; i < Sets.Count; i++)
			Sets[i].IsDefault = false;

		LastUsed = Mathf.Clamp(LastUsed, 0, SetCount - 1);

		WolfUpgrades = WolfUpgrades.Where(id => Catalog.Get(id)?.Kind == ItemKind.WolfUpgrade).Distinct().Take(MaxWolfUpgrades).ToList();

		// Personagem que saiu da pasta, ou que não é daquele time, volta ao de sempre.
		if (!FitsTeam(Catalog.Get(BlueCharacter), Teams.Blue)) BlueCharacter = null;
		if (!FitsTeam(Catalog.Get(OrangeCharacter), Teams.Orange)) OrangeCharacter = null;

		if (RankHistory.Count == 0)
			RankHistory.Add(new RankEvent { Rank = 0, Utc = CreatedUtc });

	}

	/// <summary>Tira do conjunto o que não existe mais, não cabe no espaço
	/// ou — com <paramref name="ownership"/> — não é do jogador.</summary>
	private static void Sanitize(LoadoutSet set, bool isDefault, bool ownership)
	{
		set.Utilities ??= new List<string>();
		if (!Fits(set.Primary, secondary: false, isDefault, ownership)) set.Primary = Arsenal.DefaultPrimary;
		if (!Fits(set.Secondary, secondary: true, isDefault, ownership)) set.Secondary = Arsenal.DefaultSecondary;
		// A faca do conjunto: uma que exista, que ele tenha e, no padrão, comum.
		if (!Arsenal.IsKnife(set.Melee) || (ownership && !Owns(set.Melee)) || (isDefault && !LoadoutSet.AllowedInDefault(set.Melee))) set.Melee = Arsenal.KnifeId;
		set.Utilities.RemoveAll(id => Arsenal.GetUtility(id) == null || Catalog.Get(id) == null
			|| (ownership && !Owns(id)) || (isDefault && !LoadoutSet.AllowedInDefault(id)));
	}

	/// <summary>O conjunto como dá para usar agora: uma cópia em que o que
	/// não é do jogador (sem conta, tudo o que não é de série) vira o
	/// padrão. O conjunto guardado não muda.</summary>
	public static LoadoutSet Usable(LoadoutSet set)
	{
		LoadoutSet usable = set.Clone();
		Sanitize(usable, set.IsDefault, ownership: true);
		return usable;
	}
}
