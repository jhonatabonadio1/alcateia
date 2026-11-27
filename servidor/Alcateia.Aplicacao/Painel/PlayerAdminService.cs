using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Contas;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Aplicacao.Portas;
using Alcateia.Aplicacao.Saguao;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Loja;
using Alcateia.Dominio.Painel;
using Alcateia.Dominio.Partidas;
using Alcateia.Dominio.Seguranca;

namespace Alcateia.Aplicacao.Painel;

/// <summary>A ficha de um jogador, como o painel a mostra.</summary>
public sealed record PlayerFile(PlayerDetail Player, bool Online, IReadOnlyList<OwnedItem> Inventory, IReadOnlyList<LedgerEntry> Ledger,
	IReadOnlyList<Ban> Bans, IReadOnlyList<MatchHistoryEntry> Matches, IReadOnlyList<SecurityEvent> Events,
	IReadOnlyList<(long Id, string Name)> SameIp);

/// <summary>
/// O que o painel faz com um jogador: procurar, ver a ficha, banir e
/// desbanir, derrubar a conexão, dar e tirar moeda e item. Toda ação
/// confere o papel de quem pede e vai para a auditoria.
/// </summary>
public sealed class PlayerAdminService
{
	/// <summary>O máximo que uma canetada do painel dá ou tira de uma vez.</summary>
	public const long MaxAdjust = 10_000_000;

	private readonly IPlayerRepository _players;
	private readonly ISecurityRepository _security;
	private readonly IMatchRepository _matches;
	private readonly AccountService _accounts;
	private readonly LobbyService _lobby;
	private readonly GameDataService _data;
	private readonly Auditor _audit;
	private readonly IClock _clock;

	public PlayerAdminService(IPlayerRepository players, ISecurityRepository security, IMatchRepository matches, AccountService accounts,
		LobbyService lobby, GameDataService data, Auditor audit, IClock clock)
	{
		_players = players;
		_security = security;
		_matches = matches;
		_accounts = accounts;
		_lobby = lobby;
		_data = data;
		_audit = audit;
		_clock = clock;
	}

	public Task<IReadOnlyList<PlayerSummary>> Search(AdminContext who, string query, bool bannedOnly)
	{
		who.Require(Permission.View);
		return _players.Search(query, 50, bannedOnly);
	}

	public async Task<PlayerFile> File(AdminContext who, long accountId)
	{
		who.Require(Permission.View);
		PlayerDetail player = await Existing(accountId);
		return new PlayerFile(player, _lobby.IsOnline(accountId),
			await _players.Inventory(accountId), await _players.Ledger(accountId, 50),
			await _security.Bans(accountId, false, 20), await _matches.Recent(accountId, 20),
			await _security.Events(new EventFilter(AccountId: accountId, Limit: 30)),
			await _players.AccountsByIp(player.LastIp, 20));
	}

	/// <summary>Bane a conta: os logins dela param de valer agora, a conexão
	/// cai e ela não entra de novo até o fim do prazo.
	/// <paramref name="hours"/> zero é para sempre.</summary>
	public async Task<Ban> Ban(AdminContext who, long accountId, string reason, double hours)
	{
		who.Require(Permission.Moderate);
		reason = (reason ?? "").Trim();
		if (reason.Length is < 3 or > 300) throw new Failure(FailureKind.Invalid, "sem_motivo", "Diga o motivo do banimento (3 a 300 caracteres).");
		if (hours < 0 || hours > 24 * 365 * 10) throw new Failure(FailureKind.Invalid, "prazo_invalido", "Prazo inválido.");
		PlayerDetail player = await Existing(accountId);

		DateTime? until = hours > 0 ? _clock.UtcNow.AddHours(hours) : null;
		await _security.LiftBan(accountId, who.Admin.Name);
		Ban ban = await _security.AddBan(accountId, reason, until, who.Admin.Id, who.Admin.Name);
		await _players.SetBan(accountId, until, reason, true);
		await Drop(accountId, "conta banida");

		await _audit.Note(who, "jogador.baniu", Target(player), $"{(until == null ? "para sempre" : $"por {hours:0.#} h")}; motivo: {reason}");
		return ban;
	}

	public async Task Unban(AdminContext who, long accountId)
	{
		who.Require(Permission.Moderate);
		PlayerDetail player = await Existing(accountId);
		await _security.LiftBan(accountId, who.Admin.Name);
		await _players.SetBan(accountId, null, null, false);
		await _audit.Note(who, "jogador.desbaniu", Target(player));
	}

	/// <summary>Derruba a conexão e os logins da conta (ela entra de novo com a senha).</summary>
	public async Task Kick(AdminContext who, long accountId)
	{
		who.Require(Permission.Moderate);
		PlayerDetail player = await Existing(accountId);
		await Drop(accountId, "desconectado pela moderação");
		await _audit.Note(who, "jogador.derrubou", Target(player));
	}

	private async Task Drop(long accountId, string why)
	{
		await _players.RemoveSessions(accountId);
		_accounts.Forget(accountId);
		await _lobby.Kick(accountId, why);
	}

	/// <summary>Dá (ou tira, com valor negativo) Presas ou Sangue.</summary>
	public async Task<long> Adjust(AdminContext who, long accountId, string currency, long amount, string reason)
	{
		who.Require(Permission.Economy);
		reason = (reason ?? "").Trim();
		if (!Currency.Valid(currency)) throw new Failure(FailureKind.Invalid, "moeda_invalida", "Moeda inválida.");
		if (amount == 0 || Math.Abs(amount) > MaxAdjust) throw new Failure(FailureKind.Invalid, "valor_invalido", $"Valor de 1 a {MaxAdjust:N0}.");
		if (reason.Length is < 3 or > 200) throw new Failure(FailureKind.Invalid, "sem_motivo", "Diga o motivo (3 a 200 caracteres).");
		PlayerDetail player = await Existing(accountId);

		long balance = await _players.Adjust(accountId, currency, amount, "painel", reason, who.Admin.Id);
		await _audit.Note(who, amount > 0 ? "jogador.deu_moeda" : "jogador.tirou_moeda", Target(player), $"{amount:+#;-#} {currency}; saldo {balance}; motivo: {reason}");
		await _lobby.AccountChanged(accountId);
		return balance;
	}

	/// <summary>
	/// Põe a conta num nível (Jhon, 27/11: "coloque a opção de inserir nível
	/// no player pelo servidor"): o XP vai para o começo dele. Sobe ou desce.
	/// Não paga o prêmio de subir de nível (as Presas de cada nível
	/// alcançado): é correção de quem cuida do jogo, não progresso. O jogo
	/// ligado recebe na hora (<see cref="LobbyService.AccountChanged"/>) e
	/// comemora só o último nível marcante do salto, de volta ao saguão.
	/// Pede motivo, que vai para a auditoria.
	/// </summary>
	public async Task<(int Level, long Xp)> SetLevel(AdminContext who, long accountId, int level, string reason)
	{
		who.Require(Permission.Economy);
		reason = (reason ?? "").Trim();
		if (level < 1 || level > RankRule.Count) throw new Failure(FailureKind.Invalid, "nivel_invalido", $"Nível de 1 a {RankRule.Count}.");
		if (reason.Length is < 3 or > 200) throw new Failure(FailureKind.Invalid, "sem_motivo", "Diga o motivo (3 a 200 caracteres).");
		PlayerDetail player = await Existing(accountId);
		int before = RankRule.LevelOf(RankRule.RankFor(player.Summary.Xp));
		if (before == level) throw new Failure(FailureKind.Conflict, "mesmo_nivel", $"Ele já está no nível {level}.");

		long xp = RankRule.XpFor(level - 1);
		long old = await _players.SetXp(accountId, xp);
		await _audit.Note(who, "jogador.nivel", Target(player), $"nível {before} → {level} (XP {old} → {xp}); motivo: {reason}");
		await _lobby.AccountChanged(accountId);
		return (level, xp);
	}

	/// <summary>Entrega um item. <paramref name="days"/> zero é para sempre.</summary>
	public async Task GrantItem(AdminContext who, long accountId, string itemId, int days)
	{
		who.Require(Permission.Economy);
		if (!_data.Current.Items.TryGetValue(itemId ?? "", out CatalogEntry item))
			throw new Failure(FailureKind.NotFound, "item_inexistente", "Esse item não existe no catálogo.");
		if (days is < 0 or > 3650) throw new Failure(FailureKind.Invalid, "prazo_invalido", "Prazo inválido.");
		PlayerDetail player = await Existing(accountId);

		DateTime? until = days > 0 && !item.Consumable ? _clock.UtcNow.AddDays(days) : null;
		await _players.Grant(accountId, item.Id, until, "painel", item.Consumable ? 1 : 0);
		await _audit.Note(who, "jogador.deu_item", Target(player), $"{item.Id} ({(until == null ? "para sempre" : $"{days} dias")})");
		await _lobby.AccountChanged(accountId);
	}

	public async Task RevokeItem(AdminContext who, long accountId, string itemId)
	{
		who.Require(Permission.Economy);
		PlayerDetail player = await Existing(accountId);
		if (!await _players.Revoke(accountId, itemId))
			throw new Failure(FailureKind.NotFound, "item_inexistente", "A conta não tem esse item.");
		await _audit.Note(who, "jogador.tirou_item", Target(player), itemId);
		await _lobby.AccountChanged(accountId);
	}

	private async Task<PlayerDetail> Existing(long accountId) =>
		await _players.Detail(accountId) ?? throw new Failure(FailureKind.NotFound, "conta_inexistente", "Essa conta não existe.");

	private static string Target(PlayerDetail p) => $"{p.Summary.Name} (#{p.Summary.Id})";
}
