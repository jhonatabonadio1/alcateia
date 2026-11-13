using System;
using Alcateia.Servidor.Banco;

namespace Alcateia.Servidor;

/// <summary>
/// A configuração do servidor, toda por variável de ambiente (é como o
/// Docker a entrega). O que tem padrão serve para rodar na máquina de
/// quem desenvolve; em produção, <c>ALCATEIA_FLEET_KEY</c> e a senha do
/// banco têm de ser trocadas — o servidor avisa no log se não foram.
/// </summary>
public sealed class Config
{
	public const string DevFleetKey = "chave-de-desenvolvimento-troque";

	public PgOptions Database = new();

	/// <summary>Porta HTTP (API e WebSocket do lobby).</summary>
	public int Port = 8080;

	/// <summary>O segredo que os servidores de partida apresentam, e com
	/// o qual os ingressos de entrada na partida são assinados.</summary>
	public string FleetKey = DevFleetKey;

	/// <summary>Validade do login, em dias.</summary>
	public int SessionDays = 30;

	/// <summary>Tentativas de login ou cadastro por minuto, por endereço.</summary>
	public int LoginsPerMinute = 12;

	/// <summary>Sem batida há este tempo, o servidor de partida é dado
	/// como caído.</summary>
	public TimeSpan FleetTimeout = TimeSpan.FromSeconds(20);

	/// <summary>Quanto vale o ingresso de entrada numa partida.</summary>
	public TimeSpan TicketLife = TimeSpan.FromMinutes(3);

	public static Config FromEnvironment()
	{
		var c = new Config();
		c.Database.Host = Env("ALCATEIA_DB_HOST", c.Database.Host);
		c.Database.Port = int.Parse(Env("ALCATEIA_DB_PORT", "5432"));
		c.Database.User = Env("ALCATEIA_DB_USER", c.Database.User);
		c.Database.Password = Env("ALCATEIA_DB_PASSWORD", c.Database.Password);
		c.Database.Database = Env("ALCATEIA_DB_NAME", c.Database.Database);
		c.Port = int.Parse(Env("ALCATEIA_PORT", "8080"));
		c.FleetKey = Env("ALCATEIA_FLEET_KEY", c.FleetKey);
		c.SessionDays = int.Parse(Env("ALCATEIA_SESSION_DAYS", "30"));
		return c;
	}

	private static string Env(string name, string fallback)
	{
		string value = Environment.GetEnvironmentVariable(name);
		return string.IsNullOrEmpty(value) ? fallback : value;
	}
}
