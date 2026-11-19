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
	Character,
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

/// <summary>Efeito de uma melhoria: multiplicadores sobre a classe de
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
	/// <summary>Pode ir no conjunto padrão mesmo custando WP (decisão do
	/// Jhon, 18/11: "algumas armas podem ser equipadas no padrão, outras
	/// não; isso deve ser configurável por arma"). No padrão ela não
	/// custa WP. Item gratuito sempre pode.</summary>
	public bool DefaultOk;
	/// <summary>Vai para a aba DESTAQUE da loja.</summary>
	public bool Hot;
	/// <summary>Consumível: some ao usar (troca de nome).</summary>
	public bool Consumable;
	public TattooEffect Tattoo;
	/// <summary>Classe de lobo: o recurso com as estatísticas.</summary>
	public string BreedPath;
	/// <summary>Personagem: a pasta dele em assets/personagens.</summary>
	public CharacterSkin Character;
	/// <summary>Time em que o personagem pode ser usado (0 = os dois).</summary>
	public int Team;

	public bool ForSale => Prices != null && Prices.Any(p => p >= 0);

	public WeaponDefinition Weapon => Kind == ItemKind.Weapon ? Arsenal.Get(Id) : null;

	/// <summary>Menor preço à venda, para "a partir de" no cartão.</summary>
	public int FromPrice => ForSale ? Prices.Where(p => p >= 0).Min() : 0;
}

/// <summary>
/// Tudo o que existe para possuir: armas, granada, melhorias, classes de
/// lobo, personagens e serviços. Os personagens não estão escritos aqui:
/// cada pasta de assets/personagens vira um item (CharacterSkin). É a
/// fonte única de nome, raridade, patente mínima e preço — loja,
/// inventário e editor de conjuntos leem daqui.
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

	private static readonly List<CatalogItem> _items = WithCharacters(new()
	{
		// ---------------------------------------------------- armas
		// Desde 19/11 só a família AR-15 (com as skins), a pistola, a faca e a
		// granada: as outras armas saíram a pedido do Jhon.
		new() { Id = "ar15", Name = "AR-15", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "O fuzil padrão. Preciso no tiro a tiro, previsível na rajada. Não é o melhor em nada e não é ruim em nada." },
		new() { Id = "m9", Name = "M9", Type = "Pistola", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "A secundária de todo conjunto. Troca rápida para quando o pente acaba no meio da luta." },
		new() { Id = Arsenal.KnifeId, Name = "Faca", Type = "Corpo a corpo", Kind = ItemKind.Weapon,
			Rarity = 0, Starter = true,
			Description = "Silenciosa: não faz ruído para os bots. A arma de quem quer o abate sem entregar a posição." },
		// O AR-15 um degrau acima (18/11): mesmo fuzil, mesmas animações, um pouco
		// mais forte em tudo. Pele roxa com flames (tools/armas/blender/ar15_ultimate.py).
		new() { Id = "ar15_ultimate", Name = "AR-15 Ultimate", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 }, Hot = true, DefaultOk = true,
			Description = "O AR-15 no limite: um pouco mais de dano, cadência e pente, e o recuo mais domado. Pele roxa com flames que brilham." },
		// As skins de 19/11 ("crie as outras armas, épicas e lendárias"; à
		// tarde: "uma toda dourada, outra diamante, outra 8 bit"): o mesmo
		// fuzil e as mesmas animações, cada uma com um jeito (dano, cadência,
		// precisão, pente) e uma pintura (tools/armas/blender/ar15_ultimate.py --tema).
		new() { Id = "ar15_8bit", Name = "AR-15 8 Bit", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Cadência mais alta e tiro mais junto, com um pouco menos de dano. Pintado em pixel, como nos jogos de antigamente." },
		new() { Id = "ar15_lava", Name = "AR-15 Lava", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 3, MinRank = 3, Prices = new[] { 350, 1700, 5200, 11500, 28000 },
			Description = "Um pouco mais de dano por tiro, um pouco menos de cadência. Rocha escura com lava acesa nas rachaduras." },
		new() { Id = "ar15_ouro", Name = "AR-15 Ouro", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. O mais forte por tiro do arsenal, pente de 40, recarga rápida e o recuo mais domado. Ouro inteiro, polido e gravado." },
		new() { Id = "ar15_diamante", Name = "AR-15 Diamante", Type = "Fuzil de assalto", Kind = ItemKind.Weapon,
			Rarity = 4, MinRank = 5, Prices = new[] { 450, 2200, 6600, 14500, 35000 }, Hot = true,
			Description = "Lendária. O tiro mais preciso e a maior cadência do arsenal, pente de 42 e recarga rápida. Facetas de cristal que brilham." },

		// ---------------------------------------------------- utilitário
		new() { Id = Arsenal.GrenadeId, Name = "Granada de choque", Type = "Granada", Kind = ItemKind.Utility,
			Rarity = 1, Prices = new[] { 60, 300, 900, 2000, 4500 }, DefaultOk = true,
			Description = "Anti-lobo: atordoa e desacelera quem pega o estouro. Não existe para matar; existe para o lobo não chegar." },

		// ---------------------------------------------------- melhorias
		new() { Id = "tat_pata", Name = "Pata Leve", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 1, MinRank = 1, Prices = new[] { 120, 600, 1800, 4000, 9500 },
			Tattoo = new TattooEffect { Speed = 1.05f },
			Description = "+5% de velocidade na forma de lobo." },
		new() { Id = "tat_salto", Name = "Salto Longo", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 1, MinRank = 2, Prices = new[] { 120, 600, 1800, 4000, 9500 },
			Tattoo = new TattooEffect { Jump = 1.06f },
			Description = "+6% de força no pulo do lobo." },
		new() { Id = "tat_couro", Name = "Couro Grosso", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 2, MinRank = 3, Prices = new[] { 150, 750, 2200, 5000, 12000 }, Hot = true,
			Tattoo = new TattooEffect { Shield = 1.10f },
			Description = "+10% de escudo máximo do lobo." },
		new() { Id = "tat_garra", Name = "Garra Afiada", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 2, MinRank = 4, Prices = new[] { 150, 750, 2200, 5000, 12000 },
			Tattoo = new TattooEffect { ClawDamage = 1.06f },
			Description = "+6% de dano da garra." },
		new() { Id = "tat_pelo", Name = "Pelo Denso", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 6, Prices = new[] { 180, 900, 2700, 6000, 14000 },
			Tattoo = new TattooEffect { ShieldDecay = 0.8f },
			Description = "O escudo do lobo se desfaz 20% mais devagar." },
		// Épicas e lendárias (19/11): duas coisas de uma vez, ainda ajuste
		// fino — a maior soma fica perto de duas comuns.
		new() { Id = "tat_cacador", Name = "Caçador Noturno", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 5, Prices = new[] { 220, 1100, 3300, 7300, 17000 },
			Tattoo = new TattooEffect { Speed = 1.06f, Jump = 1.05f },
			Description = "+6% de velocidade e +5% de força no pulo, na forma de lobo." },
		new() { Id = "tat_presas", Name = "Presas de Aço", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 3, MinRank = 5, Prices = new[] { 220, 1100, 3300, 7300, 17000 },
			Tattoo = new TattooEffect { ClawDamage = 1.09f },
			Description = "+9% de dano da garra." },
		new() { Id = "tat_alfa", Name = "Alfa", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 4, MinRank = 7, Prices = new[] { 280, 1400, 4200, 9300, 22000 }, Hot = true,
			Tattoo = new TattooEffect { Shield = 1.12f, ShieldDecay = 0.8f },
			Description = "Lendária. +12% de escudo máximo do lobo, e ele se desfaz 20% mais devagar." },
		new() { Id = "tat_lua", Name = "Fúria da Lua", Type = "Melhoria · lobo", Kind = ItemKind.Tattoo,
			Rarity = 4, MinRank = 7, Prices = new[] { 280, 1400, 4200, 9300, 22000 }, Hot = true,
			Tattoo = new TattooEffect { ClawDamage = 1.07f, Speed = 1.05f },
			Description = "Lendária. +7% de dano da garra e +5% de velocidade na forma de lobo." },

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
	});

	/// <summary>
	/// Um item por pasta de personagem, antes dos serviços. Por enquanto
	/// todo personagem vem com o perfil (Starter): preço de personagem é
	/// decisão de economia ainda não tomada.
	/// </summary>
	private static List<CatalogItem> WithCharacters(List<CatalogItem> items)
	{
		int at = items.FindIndex(i => i.Kind == ItemKind.Service);
		if (at < 0) at = items.Count;

		foreach (CharacterSkin skin in CharacterSkin.All())
		{
			string team = skin.Team == Teams.Blue ? "Vanguarda" : skin.Team == Teams.Orange ? "Sindicato" : "os dois times";
			items.Insert(at++, new CatalogItem
			{
				Id = skin.Id, Name = skin.DisplayName, Type = Loc.F("Personagem · {0}", team), Kind = ItemKind.Character,
				Rarity = skin.Rarity, Starter = true, Character = skin, Team = skin.Team,
				Description = skin.Description
			});
		}

		return items;
	}

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
