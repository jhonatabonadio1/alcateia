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
	public const float DamageScale = CombatRules.FirearmDamageScale;

	public static readonly string[] Primaries = { "ar15", "ar15_ultimate", "ar15_ouro", "ar15_diamante", "ar15_8bit", "ar15_lava", "m4a4", "ak74", "scar", "svd",
		// As versões épicas e lendárias de 19/11 (tools/armas/peles.py): a mesma arma, outra pintura, um pouco mais forte.
		"m4a4_neon", "m4a4_dragao", "ak74_tigre", "ak74_imperial", "scar_artico", "scar_galaxia", "svd_carbono", "svd_tempestade" };
	public static readonly string[] Secondaries = { "deagle", "deagle_cobra", "deagle_fenix" };

	/// <summary>A arma vai para o espaço da secundária? É o que decide em
	/// que espaço entra uma arma pega do chão.</summary>
	public static bool IsSecondary(string id) => System.Array.IndexOf(Secondaries, id) >= 0;

	/// <summary>Ids da faca e da granada: o que o feed, o HUD e a arma na
	/// mão usam para desenhar. As armas de fogo usam o id do .tres.</summary>
	public const string KnifeId = "faca";

	/// <summary>As facas: a de todo mundo e as versões dela (19/11). A faca
	/// não tem .tres — os números dela estão na arma (Weapon.Knife) e cada
	/// versão os multiplica (<see cref="KnifeStats"/>).</summary>
	public static readonly string[] Knives = { KnifeId, "faca_damasco", "faca_obsidiana" };

	public static bool IsKnife(string id) => id != null && System.Array.IndexOf(Knives, id) >= 0;

	/// <summary>Quanto esta faca muda o dano, o tempo entre golpes e o
	/// alcance da faca comum (1 = igual).</summary>
	public static (float Damage, float Cooldown, float Range) KnifeStats(string id) => id switch
	{
		"faca_damasco" => (1.06f, 0.95f, 1f),
		"faca_obsidiana" => (1.12f, 0.9f, 1.05f),
		_ => (1f, 1f, 1f)
	};
	public const string GrenadeId = "choque";

	/// <summary>As granadas: a de todo mundo e as versões dela (20/11) — a
	/// mesma granada com outra pintura (tools/armas/peles.py). O dano, o
	/// raio e o nome no abate são os da comum (<see cref="GrenadeId"/>).</summary>
	public static readonly string[] Grenades = { GrenadeId, "choque_carbono", "choque_neon", "choque_magma", "choque_ouro" };

	public static bool IsGrenade(string id) => id != null && System.Array.IndexOf(Grenades, id) >= 0;

	/// <summary>O fuzil de quem não escolheu outro: o AR-15 desde 15/11
	/// (era a M16A2, que continua no arsenal).</summary>
	/// A M4A4 desde 19/11 (pedido do Jhon: "a arma padrão agora é a M4A4").
	public const string DefaultPrimary = "m4a4";
	public const string DefaultSecondary = "deagle";

	public static readonly UtilityInfo[] Utilities =
	{
		new UtilityInfo
		{
			Id = GrenadeId,
			DisplayName = "Granada",
			WpCost = 60,
			Count = 1,
			AllowedInSecondSlot = false,
			Description = "Explosiva. Mata de perto e fere até a borda do raio; cai onde a mira aponta."
		},
		// As versões (20/11): a mesma granada, com outra pintura. Custam e fazem o mesmo.
		new UtilityInfo { Id = "choque_carbono", DisplayName = "Granada Carbono", WpCost = 60, Count = 1, AllowedInSecondSlot = false,
			Description = "A granada explosiva, em fibra de carbono com faixa vermelha." },
		new UtilityInfo { Id = "choque_neon", DisplayName = "Granada Neon", WpCost = 60, Count = 1, AllowedInSecondSlot = false,
			Description = "A granada explosiva, em placas pretas com as juntas acesas em ciano." },
		new UtilityInfo { Id = "choque_magma", DisplayName = "Granada Magma", WpCost = 60, Count = 1, AllowedInSecondSlot = false,
			Description = "A granada explosiva, em basalto com lava acesa nas rachaduras." },
		new UtilityInfo { Id = "choque_ouro", DisplayName = "Granada Ouro", WpCost = 60, Count = 1, AllowedInSecondSlot = false,
			Description = "A granada explosiva, em ouro gravado." }
	};

	private static readonly Dictionary<string, WeaponDefinition> _cache = new();

	public static WeaponDefinition Get(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		if (_cache.TryGetValue(id, out WeaponDefinition cached)) return cached;

		// Nem todo id é arma de fogo: a faca, a granada e os itens da
		// loja passam por aqui e não têm definição. Ausência é resposta
		// normal (nulo), não erro.
		string path = $"res://resources/weapons/{id}.tres";
		WeaponDefinition definition = ResourceLoader.Exists(path) ? GD.Load<WeaponDefinition>(path) : null;

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
