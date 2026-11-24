using System.Collections.Generic;

/// <summary>
/// Um conjunto de equipamento: primária, secundária e até dois
/// utilitários. É a unidade de compra — paga-se o conjunto, não a arma
/// (docs/CONJUNTOS.md).
///
/// Guarda só ids. O custo nunca é salvo: é sempre somado a partir do
/// <see cref="Arsenal"/>, então um ajuste de preço chega a todos os
/// conjuntos de todo mundo sem migração.
///
/// Classe simples e não Resource porque vive num arquivo de perfil em
/// JSON, fora do projeto, e não precisa de Inspector.
/// </summary>
public sealed class LoadoutSet
{
	public const int MaxNameLength = 16;
	public const int MaxUtilities = 2;

	public string Name { get; set; } = "Novo conjunto";
	public bool IsDefault { get; set; }
	public string Primary { get; set; } = Arsenal.DefaultPrimary;
	public string Secondary { get; set; } = Arsenal.DefaultSecondary;
	/// <summary>A faca do conjunto (19/11: as versões da faca). A comum é de todo mundo.</summary>
	public string Melee { get; set; } = Arsenal.KnifeId;
	public List<string> Utilities { get; set; } = new();

	/// <summary>Soma simples dos itens — sem desconto nem bônus de
	/// combinação. O jogador tem que conseguir somar de cabeça olhando a
	/// tela. O padrão custa zero sempre.</summary>
	public int TotalCost()
	{
		if (IsDefault) return 0;

		int cost = Arsenal.CostOf(Primary) + Arsenal.CostOf(Secondary);
		foreach (string id in Utilities)
			cost += Arsenal.CostOf(id);

		return cost;
	}

	public bool IsAffordable(float wp) => TotalCost() <= wp;

	public int GrenadeCount()
	{
		int count = 0;
		foreach (string id in Utilities)
			count += Arsenal.GetUtility(id)?.Count ?? 0;

		return count;
	}

	/// <summary>
	/// As regras do editor. Note o que NÃO está aqui: teto de custo.
	/// Um conjunto de 2000 WP que raramente se consegue pagar é uma
	/// meta, não um erro — o documento avisa que essa é a primeira regra
	/// que alguém tenta "consertar".
	/// </summary>
	public bool Validate(out string error)
	{
		error = null;

		if (string.IsNullOrEmpty(Primary) || Arsenal.Get(Primary) == null)
			error = "Primária obrigatória";
		else if (string.IsNullOrEmpty(Secondary) || Arsenal.Get(Secondary) == null)
			error = "Secundária obrigatória";
		else if (Utilities.Count > MaxUtilities)
			error = Loc.F("Máximo {0} utilitários", MaxUtilities);
		else if (HasDuplicates())
			error = "Item repetido no mesmo conjunto";
		else if (Utilities.Count == MaxUtilities && Arsenal.GetUtility(Utilities[1]) is UtilityInfo second
				 && !second.AllowedInSecondSlot)
			error = "O segundo utilitário só aceita granada de luz ou de fumaça";
		else if (IsDefault && !DefaultAllows())
			error = "Este item não pode ir no conjunto padrão";
		else if (!OwnsAll())
			error = "Item fora do inventário";

		return error == null;
	}

	/// <summary>
	/// O que pode ir no conjunto padrão. Arma: só as comuns (pedido do Jhon,
	/// 19/11: "a AR-15 não pode ser definida como padrão, somente arma comum
	/// pode") — e de graça, como todo o padrão (<see cref="TotalCost"/>).
	/// Utilitário: o que é gratuito ou o que o catálogo libera para ele
	/// (<see cref="CatalogItem.DefaultOk"/>, a granada de choque).
	/// </summary>
	public static bool AllowedInDefault(string id)
	{
		if (string.IsNullOrEmpty(id)) return true;
		if (Arsenal.Get(id) != null || Arsenal.IsKnife(id)) return Catalog.Get(id)?.Rarity == 0;
		return Arsenal.CostOf(id) <= 0 || Catalog.Get(id)?.DefaultOk == true;
	}

	private bool DefaultAllows()
	{
		if (!AllowedInDefault(Primary) || !AllowedInDefault(Secondary) || !AllowedInDefault(Melee)) return false;
		foreach (string id in Utilities)
			if (!AllowedInDefault(id)) return false;
		return true;
	}

	private int RawCost()
	{
		int cost = Arsenal.CostOf(Primary) + Arsenal.CostOf(Secondary);
		foreach (string id in Utilities) cost += Arsenal.CostOf(id);
		return cost;
	}

	private bool HasDuplicates()
	{
		var seen = new HashSet<string>();
		foreach (string id in Utilities)
			if (!seen.Add(id)) return true;

		return false;
	}

	private bool OwnsAll()
	{
		if (!Profile.Owns(Primary) || !Profile.Owns(Secondary) || !Profile.Owns(Melee ?? Arsenal.KnifeId)) return false;

		foreach (string id in Utilities)
			if (!Profile.Owns(id)) return false;

		return true;
	}

	/// <summary>Itens do conjunto em texto, um por linha.</summary>
	public string Describe()
	{
		var lines = new List<string> { Arsenal.NameOf(Primary), Arsenal.NameOf(Secondary) };
		foreach (string id in Utilities) lines.Add(Arsenal.NameOf(id));
		return string.Join("\n", lines);
	}

	public LoadoutSet Clone() => new LoadoutSet
	{
		Name = Name,
		IsDefault = IsDefault,
		Primary = Primary,
		Secondary = Secondary,
		Melee = Melee,
		Utilities = new List<string>(Utilities)
	};
}
