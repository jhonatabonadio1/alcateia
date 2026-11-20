using System;

namespace Alcateia.Dominio.Seguranca;

/// <summary>Gravidade de um evento de segurança.</summary>
public static class Severity
{
	public const int Info = 0, Warning = 1, High = 2, Critical = 3;
}

/// <summary>
/// Os tipos de evento que o servidor registra. Sucesso também é evento:
/// um acesso indevido que deu certo só aparece comparado com o normal
/// (lição do CT).
/// </summary>
public static class EventKind
{
	public const string LoginOk = "login.ok";
	public const string LoginFailed = "login.falhou";
	public const string LoginAfterFailures = "login.ok_apos_falhas";
	public const string LoginBanned = "login.banido";
	public const string Register = "conta.criada";
	public const string Rename = "conta.trocou_nome";
	public const string BruteForce = "ataque.forca_bruta";
	public const string Spraying = "ataque.varios_nomes";
	public const string Scanner = "ataque.varredura";
	public const string Injection = "ataque.injecao";
	public const string Enumeration = "ataque.enumeracao";
	public const string RateLimited = "freio.pedidos";
	public const string Blocked = "bloqueio.pedido_recusado";
	public const string AutoBlock = "bloqueio.automatico";
	public const string FleetKey = "frota.chave_invalida";
	public const string ServerError = "erro.servidor";
	public const string AdminLoginOk = "painel.login.ok";
	public const string AdminLoginFailed = "painel.login.falhou";
	public const string AdminLocked = "painel.conta_travada";
	public const string AdminDenied = "painel.sem_permissao";
	public const string Cheat = "trapaca.suspeita";
}

/// <summary>Um evento de segurança: quem, de onde, o quê e o resultado.
/// Nunca leva senha nem token.</summary>
public sealed record SecurityEvent(DateTime At, string Kind, int Severity, string Ip, long? AccountId, string Actor, string Path, string Detail)
{
	public long Id { get; init; }
}

/// <summary>Uma faixa bloqueada, como está guardada.</summary>
public sealed record IpBlock(long Id, string Cidr, string Reason, string Source, DateTime CreatedAt, DateTime? Until, string AdminName);

/// <summary>Um banimento de conta.</summary>
public sealed record Ban(long Id, long AccountId, string AccountName, string Reason, DateTime CreatedAt, DateTime? Until,
	string AdminName, DateTime? LiftedAt, string LiftedBy)
{
	public bool ActiveAt(DateTime now) => LiftedAt == null && (Until == null || Until > now);
}
