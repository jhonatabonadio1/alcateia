using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum ItemKind
{
	Weapon,
	Utility,
	Tattoo,
	Breed,
	Service
}

/// <summary>As durações da loja. O preço por dia cai com a duração — o
/// desconto por volume precisa ser visível.</summary>
public enum Duration
{
	Day1,
	Day7,
	Day30,
	Day90,
	Permanent
}

/// <summary>Efeito de uma tatuagem: multiplicadores sobre a classe de
/// lobo. Pequenos de propósito — ajuste fino, não poder.</summary>
public sealed class TattooEffect
{
	public float Speed = 1f;
	public float Jump = 1f;
	public float Shield = 1f;
	public float ClawDamage = 1f;
	public float ShieldDecay = 1f;
}

public sealed class CatalogItem
{
	public string Id;
	public string Name;
	/// <summary>Tipo por extenso no cartão: "Fuzil de assalto".</summary>
	public string Type;
	public string Description;
	public ItemKind Kind;
	/// <summary>0 comum … 4 lendário.</summary>
	public int Rarity;
	/// <summary>Patente mínima para comprar ou usar.</summary>
	public int MinRank;
	/// <summary>Preço em Presas por duração, na ordem de <see cref="Duration"/>.
	/// −1 = não vendido nessa duração. Nulo = não está à venda.</summary>
	public int[] Prices;
	/// <summary>Vem com todo perfil, permanente e grátis.</summary>
	public bool Starter;
	/// <summary>Vai para a aba DESTAQUE da loja.</summary>
	public bool Hot;
	/// <summary>Consumível: some ao usar (troca de nome).</summary>
	public bool Consumable;
	public TattooEffect Tattoo;
	/// <summary>Classe de lobo: o recurso com as estatísticas.</summary>
	public string BreedPath;

	public bool ForSale => Prices != null && Prices.Any(p => p >= 0);

	public WeaponDefinition Weapon => Kind == ItemKind.Weapon ? Arsenal.Get(Id) : null;

	/// <summary>Menor preço à venda, para "a partir de" no cartão.</summary>
	public int FromPrice => ForSale ? Prices.Where(p => p >= 0).Min() : 0;
}

/// <summary>
/// Tudo o que existe para possuir: armas, granada, tatuagens, classes de
/// lobo e serviços. É a fonte única de nome, raridade, patente mínima e
/// preço — loja, inventário e editor de conjuntos leem daqui.
///
/// Regra do guia que vale ouro: tudo que afeta o combate se compra com
/// Presas, a moeda ganha jogando. Sangue (a moeda paga) nunca compra
/// poder. Por isso nenhum item daqui tem preço em Sangue.
///
/// Os números de combate das armas continuam em resources/weapons/*.tres;
/// aqui fica só o que é de economia e de vitrine.
/// </summary>
public static class Catalog
{
	public static readonly string[] DurationLabels = { "1 dia", "7 dias", "30 dias", "90 dias", "Permanente" };
	public static readonly string[] DurationShort = { "1d", "7d", "30d", "90d", "Perm" };
	private static readonly int[] DurationDays = { 1, 7, 30, 90, 0 };

	public static int Days(Duration d) => DurationDays[(int)d];

	private static readonly List<CatalogItem> _items = new()
	{
		// ---------------------------------------------------- armas
		new() { Id = "m16a2", Name = "M16A2", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "O fuzil padrão. Preciso no tiro a tiro, previsível na rajada. Não é o melhor em nada e não é ruim em nada." },
		new() { Id = "m9", Name = "M9", Type = "Pistola", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "A secundária de todo conjunto. Troca rápida para quando o pente acaba no meio da luta." },
		new() { Id = "faca", Name = "Faca", Type = "Corpo a corpo", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "Silenciosa: não faz ruído para os bots. A arma de quem quer o abate sem entregar a posição." },
		new() { Id = "ump45", Name = "UMP-45", Type = "Submetralhadora", Kind = ItemKind.Weapon,
			Rarity = 1, MinRank = 1, Prices = new[] { 250, 1200, 3600, 8000, 18000 },
			Description = "SMG de dano, a arma anti-lobo. Rápida na mão e forte de perto — a que você puxa quando ouve o uivo." },
		new() { Id = "ak47", Name = "AK-47", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 2, MinRank = 2, Prices = new[] { 300, 1500, 4500, 10000, 24000 }, Hot = true,
			Description = "Impacto bruto: mata com um tiro na cabeça. Paga com recuo pesado e um estampido que se ouve de longe." },
		new() { Id = "m3", Name = "M3", Type = "Escopeta", Kind = ItemKind.Weapon,
			Rarity = 2, MinRank = 3, Prices = new[] { 280, 1350, 4000, 9000, 21000 },
			Description = "Escopeta de bombeada, recarga cartucho a cartucho. Decide corredor; não existe em campo aberto." },
		new() { Id = "sg552", Name = "SG-552", Type = "Fuzil de controle", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 4, Prices = new[] { 320, 1600, 4800, 10800, 26000 }, Hot = true,
			Description = "O fuzil mais estável do arsenal: recuo baixo e linear. Pune quem atira andando." },

		// ---------------------------------------------------- utilitário
		new() { Id = "choque", Name = "Granada de choque", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 1, Prices = new[] { 60, 300, 900, 2000, 4500 },
			Description = "Anti-lobo: atordoa e desacelera quem pega o estouro. Não existe para matar; existe para o lobo não chegar." },

		// ---------------------------------------------------- tatuagens
		new() { Id = "tat_pata", Name = "Pata Leve", Type = "Tatuagem · lobo", Kind = ItemKind.Tattoo,
			Rarity = 1, MinRank = 1, Prices = new[] { 120, 600, 1800, 4000, 9500 },
			Tattoo = new TattooEffect { Speed = 1.05f },
			Description = "+5% de velocidade na forma de lobo." },
		new() { Id = "tat_salto", Name = "Salto Longo", Type = "Tatuagem · lobo", Kind = ItemKind.Tattoo,
			Rarity = 1, MinRank = 2, Prices = new[] { 120, 600, 1800, 4000, 9500 },
			Tattoo = new TattooEffect { Jump = 1.06f },
			Description = "+6% de força no pulo do lobo." },
		new() { Id = "tat_couro", Name = "Couro Grosso", Type = "Tatuagem · lobo", Kind = ItemKind.Tattoo,
			Rarity = 2, MinRank = 3, Prices = new[] { 150, 750, 2200, 5000, 12000 }, Hot = true,
			Tattoo = new TattooEffect { Shield = 1.10f },
			Description = "+10% de escudo máximo do lobo." },
		new() { Id = "tat_garra", Name = "Garra Afiada", Type = "Tatuagem · lobo", Kind = ItemKind.Tattoo,
			Rarity = 2, MinRank = 4, Prices = new[] { 150, 750, 2200, 5000, 12000 },
			Tattoo = new TattooEffect { ClawDamage = 1.06f },
			Description = "+6% de dano da garra." },
		new() { Id = "tat_pelo", Name = "Pelo Denso", Type = "Tatuagem · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 6, Prices = new[] { 180, 900, 2700, 6000, 14000 },
			Tattoo = new TattooEffect { ShieldDecay = 0.8f },
			Description = "O escudo do lobo se desfaz 20% mais devagar." },

		// ---------------------------------------------------- classes de lobo
		new() { Id = "raca_bruto", Name = "Bruto", Type = "Classe de lobo", Kind = ItemKind.Breed,
			Rarity = 1, Starter = true, BreedPath = "res://resources/breeds/bruto.tres",
			Description = "Linha de frente: escudo alto, garra pesada, resiste a tiro no corpo. Mais lento no chão." },
		new() { Id = "raca_ligeiro", Name = "Ligeiro", Type = "Classe de lobo", Kind = ItemKind.Breed,
			Rarity = 2, MinRank = 3, BreedPath = "res://resources/breeds/ligeiro.tres",
			Description = "Velocidade e flanco: o lobo mais rápido e o pulo mais longo, com pouco escudo. Destrava em Batedor I." },

		// ---------------------------------------------------- serviços
		new() { Id = "troca_nome", Name = "Troca de nome", Type = "Serviço", Kind = ItemKind.Service,
			Rarity = 1, Consumable = true, Prices = new[] { -1, -1, -1, -1, 1500 },
			Description = "Troca o seu nickname uma vez. O nome é fixo sem este item." }
	};

	private static readonly Dictionary<string, CatalogItem> _byId = _items.ToDictionary(i => i.Id);

	public static IReadOnlyList<CatalogItem> All => _items;

	public static CatalogItem Get(string id) =>
		id != null && _byId.TryGetValue(id, out CatalogItem item) ? item : null;

	public static IEnumerable<CatalogItem> OfKind(ItemKind kind) => _items.Where(i => i.Kind == kind);

	public static string NameOf(string id) => Get(id)?.Name ?? Arsenal.NameOf(id);

	/// <summary>Preço numa duração, ou −1 se não vendido nela.</summary>
	public static int Price(CatalogItem item, Duration d)
	{
		if (item?.Prices == null) return -1;
		int price = item.Prices[(int)d];
		if (price < 0) return -1;

		// Item do dia: 20% de desconto em todas as durações.
		if (IsDaily(item, DateTime.Now)) price = (int)(Mathf.Round(price * 0.8f / 10f) * 10f);
		return price;
	}

	/// <summary>Preço por dia, ou −1 para permanente e não vendido.</summary>
	public static float PerDay(CatalogItem item, Duration d)
	{
		int price = Price(item, d);
		int days = Days(d);
		return price < 0 || days == 0 ? -1f : price / (float)days;
	}

	/// <summary>A duração limitada de menor preço por dia — ganha o selo
	/// "melhor valor". Permanente fica de fora: não tem preço por dia.</summary>
	public static Duration? BestValue(CatalogItem item)
	{
		Duration? best = null;
		float bestPerDay = float.MaxValue;

		foreach (Duration d in new[] { Duration.Day1, Duration.Day7, Duration.Day30, Duration.Day90 })
		{
			float perDay = PerDay(item, d);
			if (perDay < 0 || perDay >= bestPerDay) continue;
			bestPerDay = perDay;
			best = d;
		}

		return best;
	}

	// ------------------------------------------------------------------ do dia

	/// <summary>Quatro itens do dia, sorteados pela data: mesmo dia, mesma
	/// vitrine, para todo mundo e a cada vez que se abre a loja.</summary>
	public static List<CatalogItem> Daily(DateTime day)
	{
		var pool = _items.Where(i => i.ForSale && !i.Consumable).ToList();
		var rng = new Random(day.Year * 1000 + day.DayOfYear);
		return pool.OrderBy(_ => rng.Next()).Take(4).ToList();
	}

	public static bool IsDaily(CatalogItem item, DateTime day) => Daily(day).Contains(item);
}
