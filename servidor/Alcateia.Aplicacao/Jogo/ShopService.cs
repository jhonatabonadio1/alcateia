using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Loja;

namespace Alcateia.Aplicacao.Jogo;

/// <summary>
/// A loja no servidor. O jogo só diz "quero este item por este tempo":
/// preço, desconto, patente mínima e saldo são conferidos aqui, dentro de
/// uma transação que trava a carteira — cliente adulterado não compra
/// mais barato, não compra sem patente e não gasta o mesmo dinheiro duas
/// vezes.
/// </summary>
public sealed class ShopService
{
	private readonly IPlayerRepository _players;
	private readonly GameDataService _data;
	private readonly IClock _clock;

	public ShopService(IPlayerRepository players, GameDataService data, IClock clock)
	{
		_players = players;
		_data = data;
		_clock = clock;
	}

	public Task<IReadOnlyList<OwnedItem>> Inventory(long accountId) => _players.Inventory(accountId);

	public Task<IReadOnlyList<LedgerEntry>> Ledger(long accountId) => _players.Ledger(accountId, 50);

	/// <summary>Joga fora um item do inventário (não devolve o que custou).</summary>
	public async Task Discard(long accountId, string itemId)
	{
		if (!await _players.Revoke(accountId, itemId ?? ""))
			throw new Failure(FailureKind.NotFound, "item_inexistente", "Você não tem esse item.");
	}

	/// <summary>O número de pedido aceito: até 64 letras, números, "-" e "_".</summary>
	public static bool ValidRequestId(string id) =>
		string.IsNullOrEmpty(id) || (id.Length <= 64 && id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_'));

	/// <summary>Compra. Com <paramref name="requestId"/> (o jogo manda um por
	/// clique), repetir o pedido não compra de novo (26/11).</summary>
	public Task<PurchaseReceipt> Buy(long accountId, string itemId, int duration, string requestId = null)
	{
		if (!ValidRequestId(requestId))
			throw new Failure(FailureKind.Invalid, "pedido_invalido", "Número de pedido inválido.");
		GameData data = _data.Current;
		DateTime now = _clock.UtcNow;
		data.Items.TryGetValue(itemId ?? "", out CatalogEntry item);

		return _players.Purchase(accountId, itemId ?? "", wallet =>
		{
			int price = Shop.Quote(item, duration, RankRule.RankFor(wallet.Xp), data.PromotionsAt(now), now);
			if (item.Consumable) return new PurchaseDecision(item.Currency, price, null, Uses: 1);

			if (wallet.Owned != null && wallet.Owned.ExpiresAt == null)
				throw new Failure(FailureKind.Conflict, "ja_tem", "Você já tem esse item para sempre.");
			return new PurchaseDecision(item.Currency, price, Shop.Expiry(wallet.Owned, duration, now));
		}, string.IsNullOrEmpty(requestId) ? null : requestId);
	}
}
