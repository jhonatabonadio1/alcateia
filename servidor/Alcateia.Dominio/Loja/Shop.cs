using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Alcateia.Dominio.Loja;

/// <summary>Os tipos de item do catálogo (os do jogo, em <c>ItemKind</c>).</summary>
public static class ItemKinds
{
	public const string Weapon = "weapon", Utility = "utility", Tattoo = "tattoo", Breed = "breed", Character = "character", Service = "service";
	public static readonly string[] All = { Weapon, Utility, Tattoo, Breed, Character, Service };
}

public static class Currency
{
	public const string Presas = "presas", Sangue = "sangue";
	public static bool Valid(string c) => c is Presas or Sangue;
}

/// <summary>
/// Um item do catálogo como o servidor o guarda: o que o painel edita
/// (preço, raridade, patente mínima, à venda ou não). Os números de
/// combate da arma continuam no jogo — aqui fica a economia.
///
/// <see cref="Prices"/>: um preço por duração (1, 7, 30, 90 dias e
/// permanente); negativo é "não se vende por esse tempo".
/// </summary>
public sealed record CatalogEntry(string Id, string Name, string Type, string Kind, int Rarity, int MinRank, string Currency,
	IReadOnlyList<int> Prices, bool Starter, bool Enabled, bool Hot, int Team, string Image, int Sort)
{
	/// <summary>Consumível: some ao usar (troca de nome). Compra soma um uso.</summary>
	public bool Consumable { get; init; }

	public bool ForSale => Enabled && !Starter && Prices.Any(p => p >= 0);
}

/// <summary>Um desconto com hora para começar e acabar.</summary>
public sealed record Promotion(long Id, string ItemId, string Title, int Percent, DateTime StartsAt, DateTime EndsAt, bool Active)
{
	public bool RunningAt(DateTime now) => Active && StartsAt <= now && now < EndsAt;
}

/// <summary>Um item no inventário de uma conta. <see cref="ExpiresAt"/>
/// nulo é permanente.</summary>
public sealed record OwnedItem(string ItemId, DateTime AcquiredAt, DateTime? ExpiresAt, string Source)
{
	/// <summary>Usos que restam, se o item é consumível.</summary>
	public int Uses { get; init; }

	public bool ValidAt(DateTime now) => ExpiresAt == null || ExpiresAt > now;
}

/// <summary>Uma linha do extrato de uma conta.</summary>
public sealed record LedgerEntry(long Id, DateTime At, string Currency, long Amount, long BalanceAfter, string Reason, string Ref);

/// <summary>
/// As regras da loja. Tudo é conferido no servidor: o jogo só diz "quero
/// este item por este tempo" — preço, desconto, patente e saldo são daqui.
/// </summary>
public static class Shop
{
	/// <summary>Dias de cada duração; 0 é permanente.</summary>
	public static readonly int[] DurationDays = { 1, 7, 30, 90, 0 };

	public const int MaxPercent = 90;

	/// <summary>Moeda paga nunca compra poder (regra 3 do jogo: nenhuma
	/// vantagem permanente): só personagem e serviço podem custar Sangue.</summary>
	public static bool SangueAllowed(string kind) => kind is ItemKinds.Character or ItemKinds.Service;

	public static void Check(CatalogEntry item)
	{
		if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 40) throw Invalid("id_invalido", "Identificador inválido.");
		if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 60) throw Invalid("nome_invalido", "Nome inválido.");
		if (Array.IndexOf(ItemKinds.All, item.Kind) < 0) throw Invalid("tipo_invalido", "Tipo de item inválido.");
		if (item.Rarity is < 0 or > 4) throw Invalid("raridade_invalida", "Raridade de 0 (comum) a 4 (lendário).");
		if (item.MinRank is < 0 or >= Contas.RankRule.Count) throw Invalid("patente_invalida", "Patente mínima inválida.");
		if (!Currency.Valid(item.Currency)) throw Invalid("moeda_invalida", "Moeda inválida.");
		if (item.Currency == Currency.Sangue && !SangueAllowed(item.Kind))
			throw Invalid("sangue_nao_compra_poder", "Sangue só compra personagem e serviço: moeda paga nunca compra poder.");
		if (item.Prices.Count != 0 && item.Prices.Count != DurationDays.Length) throw Invalid("precos_invalidos", "São cinco preços: 1, 7, 30, 90 dias e permanente.");
		if (item.Prices.Any(p => p > 10_000_000)) throw Invalid("precos_invalidos", "Preço alto demais.");
	}

	public static void Check(Promotion promo)
	{
		if (promo.Percent is < 1 or > MaxPercent) throw Invalid("desconto_invalido", $"Desconto de 1 a {MaxPercent}%.");
		if (promo.EndsAt <= promo.StartsAt) throw Invalid("periodo_invalido", "A promoção acaba antes de começar.");
		if ((promo.Title ?? "").Length > 60) throw Invalid("titulo_invalido", "Título comprido demais.");
	}

	/// <summary>Quantos itens entram na vitrine do dia, e com que desconto.</summary>
	public const int DailyCount = 4, DailyPercent = 20;

	/// <summary>
	/// A vitrine do dia: alguns itens à venda com desconto, sorteados pela
	/// data — o mesmo dia dá a mesma vitrine, para todo mundo. Consumível
	/// não entra. Sai como promoções comuns, que valem até o dia virar.
	/// </summary>
	public static List<Promotion> DailyShowcase(IEnumerable<CatalogEntry> catalog, DateTime utc, int count = DailyCount, int percent = DailyPercent)
	{
		if (count <= 0 || percent <= 0) return new List<Promotion>();
		string day = GameClock.DayKey(utc);
		// A ordem sai da data e do identificador (não de GetHashCode, que
		// muda a cada execução): reiniciar o servidor não troca a vitrine.
		return catalog.Where(i => i.ForSale && !i.Consumable)
			.OrderBy(i => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(day + "|" + i.Id))))
			.Take(count)
			.Select(i => new Promotion(0, i.Id, "Item do dia", Math.Min(percent, MaxPercent), GameClock.DayStart(utc), GameClock.NextDay(utc), true))
			.ToList();
	}

	/// <summary>O maior desconto valendo agora para o item (promoções não somam).</summary>
	public static int Discount(string itemId, IEnumerable<Promotion> promotions, DateTime now) =>
		promotions.Where(p => p.ItemId == itemId && p.RunningAt(now)).Select(p => p.Percent).DefaultIfEmpty(0).Max();

	public static int Discounted(int price, int percent) =>
		percent <= 0 ? price : Math.Max(1, (int)Math.Round(price * (100 - percent) / 100.0));

	/// <summary>
	/// O preço de comprar o item por essa duração, agora, para essa
	/// patente — ou a recusa, com o motivo.
	/// </summary>
	public static int Quote(CatalogEntry item, int duration, int rank, IEnumerable<Promotion> promotions, DateTime now)
	{
		if (item == null || !item.Enabled) throw new Failure(FailureKind.NotFound, "item_inexistente", "Esse item não existe.");
		if (item.Starter) throw new Failure(FailureKind.Conflict, "item_de_serie", "Esse item já vem com a conta.");
		if (duration < 0 || duration >= DurationDays.Length || item.Prices.Count != DurationDays.Length || item.Prices[duration] < 0)
			throw new Failure(FailureKind.Invalid, "duracao_indisponivel", "O item não se vende por esse tempo.");
		if (rank < item.MinRank) throw new Failure(FailureKind.Forbidden, "patente_insuficiente", "Sua patente ainda não libera esse item.");
		return Discounted(item.Prices[duration], Discount(item.Id, promotions, now));
	}

	/// <summary>Até quando vale uma compra: soma ao que já tinha, se ainda valia.</summary>
	public static DateTime? Expiry(OwnedItem owned, int duration, DateTime now)
	{
		int days = DurationDays[duration];
		if (days == 0) return null;
		if (owned != null && owned.ExpiresAt == null) return null;
		DateTime from = owned != null && owned.ExpiresAt > now ? owned.ExpiresAt.Value : now;
		return from.AddDays(days);
	}

	private static Failure Invalid(string code, string message) => new(FailureKind.Invalid, code, message);
}

/// <summary>Um mapa como o servidor o conhece: o painel liga e desliga.</summary>
public sealed record GameMap(string Id, string Name, string Size, string Description, IReadOnlyList<string> Modes, bool Enabled, string Image, int Sort);
