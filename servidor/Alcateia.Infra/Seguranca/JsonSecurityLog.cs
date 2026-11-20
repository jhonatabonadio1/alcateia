using System;
using System.IO;
using System.Text.Json;
using Alcateia.Aplicacao.Portas;
using Alcateia.Dominio.Seguranca;

namespace Alcateia.Infra.Seguranca;

/// <summary>
/// Cada evento de segurança como uma linha de JSON na saída padrão. É a
/// cópia que sai da máquina: o Docker entrega ao Loki (ou a qualquer
/// coletor), e quem invadir o servidor e apagar a tabela não apaga o que
/// já saiu. Nunca leva senha nem token.
/// </summary>
public sealed class JsonSecurityLog : ISecurityLog
{
	private readonly TextWriter _out;
	private readonly int _minSeverity;
	private readonly object _lock = new();

	public JsonSecurityLog(TextWriter output, int minSeverity = 0)
	{
		_out = output;
		_minSeverity = minSeverity;
	}

	public void Write(SecurityEvent e)
	{
		if (_out == null || e.Severity < _minSeverity) return;
		string line = JsonSerializer.Serialize(new
		{
			log = "seguranca", at = e.At.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"), kind = e.Kind, severity = e.Severity, ip = e.Ip,
			account = e.AccountId, actor = e.Actor, path = e.Path, detail = e.Detail
		});
		lock (_lock) _out.WriteLine(line);
	}
}
