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

	// O que deu errado nas partidas (painel: Segurança → "Partidas"). Uns
	// o servidor de partida conta na batida, outros a própria API percebe.
	public const string MatchPrefix = "partida.";
	/// <summary>A API: o servidor de partida parou de bater e as partidas dele se perderam.</summary>
	public const string MatchServerGone = "partida.servidor_sumiu";
	/// <summary>A API: o servidor de partida fechou a partida sem resultado.</summary>
	public const string MatchClosed = "partida.encerrada_sem_resultado";
	/// <summary>A API: o servidor de partida ocioso foi liberado e desligou.</summary>
	public const string MatchServerStopped = "partida.servidor_desligou";
	/// <summary>O jogador saiu (ou a ligação dele caiu) com a partida em curso.</summary>
	public const string MatchConnectionLost = "partida.conexao_caiu";
	/// <summary>Recusado na porta: ingresso vencido ou inválido, não é desta partida.</summary>
	public const string MatchRefused = "partida.recusado";
	/// <summary>Ligou na porta e ficou calado: nunca mostrou o ingresso.</summary>
	public const string MatchNoHello = "partida.sem_apresentacao";
	/// <summary>A partida fechou sem ninguém ter entrado.</summary>
	public const string MatchEmpty = "partida.vazia";
	/// <summary>A partida acabou antes da hora porque todo mundo saiu.</summary>
	public const string MatchAbandoned = "partida.abandonada";
	/// <summary>O resultado não chegou à API depois de várias tentativas.</summary>
	public const string MatchResultFailed = "partida.resultado_falhou";
	/// <summary>Exceção no servidor de partida.</summary>
	public const string MatchServerError = "partida.erro_servidor";
	/// <summary>O passo da física passou do orçamento (o servidor atrasa todo mundo).</summary>
	public const string MatchSlowStep = "partida.passo_lento";

	/// <summary>Os tipos que um servidor de partida pode contar na batida.
	/// Qualquer outro é ignorado: a chave da frota vale, mas o painel só
	/// mostra o que conhece.</summary>
	public static readonly string[] MatchReported =
	{
		MatchConnectionLost, MatchRefused, MatchNoHello, MatchEmpty, MatchAbandoned, MatchResultFailed, MatchServerError, MatchSlowStep
	};
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
