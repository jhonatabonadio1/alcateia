using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alcateia.Dominio.Desafios;
using Alcateia.Dominio.Loja;
using Alcateia.Dominio.Painel;
using Alcateia.Dominio.Seguranca;

namespace Alcateia.Aplicacao.Portas;

/// <summary>Filtro da lista de eventos de segurança.</summary>
public sealed record EventFilter(string KindPrefix = null, string Ip = null, int MinSeverity = 0, long BeforeId = 0, int Limit = 100, long? AccountId = null);

/// <summary>Uma hora no gráfico de eventos.</summary>
public sealed record EventBucket(DateTime Hour, int Info, int Warning, int High, int Critical);

/// <summary>O resumo de segurança de um período: por hora, por tipo e os
/// endereços que mais aparecem.</summary>
public sealed record SecurityOverview(IReadOnlyList<EventBucket> ByHour, IReadOnlyList<(string Kind, int Count)> ByKind,
	IReadOnlyList<(string Ip, int Count, int Worst)> TopIps);

/// <summary>Blacklist de IP, eventos de segurança e banimentos.</summary>
public interface ISecurityRepository
{
	/// <summary>As faixas bloqueadas que ainda valem.</summary>
	Task<IReadOnlyList<IpBlock>> IpBlocks();
	/// <summary>Bloqueia (ou renova o bloqueio de) uma faixa.</summary>
	Task<IpBlock> AddIpBlock(string cidr, string reason, string source, DateTime? until, string adminName);
	Task<IpBlock> RemoveIpBlock(long id);
	Task AddEvents(IReadOnlyList<SecurityEvent> events);
	Task<IReadOnlyList<SecurityEvent>> Events(EventFilter filter);
	Task<SecurityOverview> Overview(int hours);
	Task<Ban> AddBan(long accountId, string reason, DateTime? until, long adminId, string adminName);
	/// <summary>Levanta o banimento em vigor. Falso se não havia.</summary>
	Task<bool> LiftBan(long accountId, string adminName);
	Task<IReadOnlyList<Ban>> Bans(long? accountId, bool activeOnly, int limit);
	/// <summary>Apaga eventos mais velhos que isso (o registro não cresce para sempre).</summary>
	Task<int> PruneEvents(DateTime olderThan);
}

/// <summary>O que o login do painel precisa saber de um administrador.</summary>
public sealed record AdminCredentials(AdminUser User, string PasswordHash, string TotpSecretCipher, int FailedLogins, DateTime? LockedUntil);

/// <summary>Os administradores, os logins deles e a auditoria.</summary>
public interface IAdminRepository
{
	Task<int> Count();
	Task<long?> TryCreate(string name, string nameKey, string passwordHash, string role);
	Task<AdminCredentials> FindByName(string nameKey);
	Task<AdminCredentials> FindById(long id);
	Task<AdminUser> FindBySession(string tokenHash);
	Task AddSession(string tokenHash, long adminId, DateTime expiresAt, string ip, string userAgent);
	Task RemoveSession(string tokenHash);
	Task RemoveSessions(long adminId);
	Task LoginFailed(long adminId, int failedLogins, DateTime? lockedUntil);
	Task LoginOk(long adminId, string ip);
	Task SetTotp(long adminId, string secretCipher, bool enabled);
	Task SetPassword(long adminId, string passwordHash);
	Task SetRole(long adminId, string role);
	Task SetDisabled(long adminId, bool disabled);
	Task<IReadOnlyList<AdminUser>> List();
	Task Audit(long adminId, string adminName, string action, string target, string detail, string ip);
	Task<IReadOnlyList<AuditEntry>> AuditLog(int limit, long beforeId, string search);
}

/// <summary>O que o painel edita e o jogo lê: catálogo, mapas, promoções,
/// desafios e configurações.</summary>
public interface IGameDataRepository
{
	Task<IReadOnlyList<CatalogEntry>> Catalog();
	Task SaveItem(CatalogEntry item);
	/// <summary>Põe os itens que ainda não existem. Nos que existem, só a
	/// arte acompanha o jogo; o que o painel edita fica como está.</summary>
	Task<int> SeedItems(IEnumerable<CatalogEntry> items);
	Task<IReadOnlyList<GameMap>> Maps();
	Task SaveMap(GameMap map);
	/// <summary>Põe os mapas que ainda não existem. Nos que existem, a foto
	/// e os modos (que são do jogo) acompanham a semente.</summary>
	Task<int> SeedMaps(IEnumerable<GameMap> maps);
	Task<IReadOnlyList<Promotion>> Promotions(bool includeEnded);
	Task<Promotion> AddPromotion(Promotion promotion, long adminId);
	Task<bool> RemovePromotion(long id);
	Task<IReadOnlyList<Challenge>> Challenges();
	Task SaveChallenge(Challenge challenge);
	Task<bool> RemoveChallenge(string id);
	Task<int> SeedChallenges(IEnumerable<Challenge> challenges);
	Task<IReadOnlyDictionary<string, string>> Settings();
	Task SaveSetting(string key, string json);
}

/// <summary>Uma conta na busca do painel.</summary>
public sealed record PlayerSummary(long Id, string Name, long Xp, long Presas, long Sangue, int Matches, DateTime CreatedAt,
	DateTime? LastLoginAt, DateTime? BannedUntil, bool Banned);

/// <summary>Uma conta por inteiro, para o painel e para o jogo.</summary>
public sealed record PlayerDetail(PlayerSummary Summary, string LastIp, string BanReason, IReadOnlyDictionary<string, int> Stats);

/// <summary>O que a conta tem na hora de comprar: a regra decide com isto.</summary>
public sealed record PurchaseContext(long Xp, long Presas, long Sangue, OwnedItem Owned);

/// <summary>O que a regra decidiu cobrar e até quando o item vale.</summary>
public sealed record PurchaseDecision(string Currency, int Price, DateTime? ExpiresAt, int Uses = 0);

public sealed record PurchaseReceipt(string ItemId, string Currency, int Price, long Balance, DateTime? ExpiresAt);

/// <summary>Os números da visão geral.</summary>
public sealed record ServerNumbers(long Accounts, long NewToday, long ActiveDay, long MatchesToday, long Matches, long PresasInGame, long SangueInGame,
	long ActiveBans, IReadOnlyList<(DateTime Day, int Accounts, int Matches)> Days);

/// <summary>As contas vistas pelo painel e a economia de cada uma
/// (carteira, inventário, extrato, progresso nos desafios).</summary>
public interface IPlayerRepository
{
	Task<IReadOnlyList<PlayerSummary>> Search(string query, int limit, bool bannedOnly);
	Task<PlayerDetail> Detail(long accountId);
	/// <summary>Soma (ou tira) moeda, com linha no extrato. Recusa se o saldo ficaria negativo.</summary>
	Task<long> Adjust(long accountId, string currency, long amount, string reason, string reference, long? adminId);
	Task<IReadOnlyList<OwnedItem>> Inventory(long accountId);
	/// <summary>Entrega o item. <paramref name="uses"/>: usos a somar, se consumível.</summary>
	Task Grant(long accountId, string itemId, DateTime? expiresAt, string source, int uses = 0);
	/// <summary>Gasta um uso de um consumível. Falso se não tinha.</summary>
	Task<bool> Consume(long accountId, string itemId);
	Task<bool> Revoke(long accountId, string itemId);
	/// <summary>A compra, tudo ou nada: trava a carteira, pergunta à regra o
	/// preço (<paramref name="decide"/> pode recusar), desconta, entrega e
	/// escreve o extrato.</summary>
	Task<PurchaseReceipt> Purchase(long accountId, string itemId, Func<PurchaseContext, PurchaseDecision> decide);
	Task<IReadOnlyList<LedgerEntry>> Ledger(long accountId, int limit);
	Task<IReadOnlyList<(string ChallengeId, string PeriodKey, int Progress, bool Claimed)>> Progress(long accountId);
	Task AddProgress(long accountId, string challengeId, string periodKey, int amount);
	/// <summary>Paga o prêmio uma vez só. Falso se não completou ou já recebeu.</summary>
	Task<bool> Claim(long accountId, Challenge challenge, string periodKey, DateTime? itemExpiresAt);
	Task SetBan(long accountId, DateTime? until, string reason, bool banned);
	Task RemoveSessions(long accountId);
	Task NoteIp(long accountId, string ip);
	Task<ServerNumbers> Numbers();
	Task<IReadOnlyList<(long Id, string Name)>> AccountsByIp(string ip, int limit);
}

/// <summary>Cifra o que não pode ficar legível no banco (o segredo do TOTP).</summary>
public interface ICipher
{
	string Encrypt(string plain);
	string Decrypt(string cipher);
}

/// <summary>Para onde vai a linha de log de segurança (a saída padrão,
/// de onde o Loki a recolhe).</summary>
public interface ISecurityLog
{
	void Write(SecurityEvent e);
}
