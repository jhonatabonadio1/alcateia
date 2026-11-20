using System;

namespace Alcateia.Dominio.Seguranca;

/// <summary>
/// O que nenhum jogo de verdade pede. O jogo só fala <c>/v1/...</c> e
/// <c>/health</c>: quem pede <c>/.env</c> ou <c>/wp-admin</c> está
/// varrendo, e quem manda <c>UNION SELECT</c> está tentando injetar.
/// Uma ocorrência já basta para registrar; poucas, para bloquear.
/// </summary>
public static class ThreatRules
{
	private static readonly string[] ScannerMarks =
	{
		"/.env", "/.git", "/.aws", "/.ssh", "/.svn", "/.ds_store", "/wp-", "/wordpress", "/phpmyadmin", "/pma", "/actuator",
		"/cgi-bin", "/vendor/", "/boaform", "/hnap1", "/owa/", "/solr/", "/jenkins", "/manager/html", "/config.json",
		"/appsettings", "/web.config", "/server-status", "/telescope", "/_profiler", "/xmlrpc", "/shell", "/eval-stdin",
		".php", ".asp", ".jsp", ".cgi", ".bak", ".sql", ".old", ".swp"
	};

	private static readonly string[] InjectionMarks =
	{
		"union select", "union all select", "' or '1'='1", "' or 1=1", "\" or 1=1", "or 1=1--", "sleep(", "benchmark(", "pg_sleep",
		"information_schema", "xp_cmdshell", "../", "..\\\\", "%2e%2e", "<script", "%3cscript", "javascript:", "${jndi", "%00",
		"/etc/passwd", "cmd.exe", "/bin/sh", "; drop ", "onerror="
	};

	/// <summary>O caminho é de quem está procurando painel, backup ou
	/// tecnologia que este servidor não tem.</summary>
	public static bool LooksLikeScanner(string path)
	{
		if (string.IsNullOrEmpty(path)) return false;
		string lower = path.ToLowerInvariant();
		foreach (string mark in ScannerMarks)
			if (lower.Contains(mark, StringComparison.Ordinal)) return true;
		return false;
	}

	/// <summary>O caminho ou a consulta trazem um padrão de injeção.</summary>
	public static bool LooksLikeInjection(string pathAndQuery)
	{
		if (string.IsNullOrEmpty(pathAndQuery)) return false;
		string lower = pathAndQuery.ToLowerInvariant();
		foreach (string mark in InjectionMarks)
			if (lower.Contains(mark, StringComparison.Ordinal)) return true;
		return false;
	}
}
