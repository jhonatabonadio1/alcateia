using System;
using System.Collections.Generic;
using System.IO;
using Alcateia.Aplicacao.Seguranca;
using Alcateia.Dominio.Seguranca;
using Alcateia.Infra.Banco;

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

	/// <summary>De quanto em quanto tempo a lista de salas que mudou vai
	/// para quem está no saguão.</summary>
	public TimeSpan RoomListEvery = TimeSpan.FromMilliseconds(500);

	/// <summary>Salas abertas ao mesmo tempo, no máximo.</summary>
	public int MaxRooms = Aplicacao.Saguao.LobbyService.DefaultMaxRooms;

	/// <summary>Sem batida há este tempo, o servidor de partida é dado
	/// como caído.</summary>
	public TimeSpan FleetTimeout = TimeSpan.FromSeconds(20);

	/// <summary>Lugares livres que a frota tenta manter de pé
	/// (<c>ALCATEIA_FLEET_RESERVE</c>). Só vale com servidores sob demanda.</summary>
	public int FleetReserve;

	/// <summary>Servidores de partida sob demanda no Fly.io: o app das
	/// máquinas de partida (<c>ALCATEIA_FLY_APP</c>), o token que pode ligá-las
	/// (<c>ALCATEIA_FLY_TOKEN</c>) e a API de máquinas
	/// (<c>ALCATEIA_FLY_API</c>). Sem app e token, a frota é fixa.</summary>
	public string FlyApp = "", FlyToken = "", FlyApi = "https://api.machines.dev";

	/// <summary>Quanto vale o ingresso de entrada numa partida.</summary>
	public TimeSpan TicketLife = TimeSpan.FromMinutes(3);

	// ------------------------------------------------------------ segurança

	public const string DevMasterKey = "chave-mestra-de-desenvolvimento-troque";

	/// <summary>A chave que cifra o que não pode ficar legível no banco (o
	/// segredo da segunda etapa dos administradores). Trocar depois de em
	/// uso desliga a segunda etapa de todos: eles ligam de novo.</summary>
	public string MasterKey = DevMasterKey;

	/// <summary>De quem o servidor aceita os cabeçalhos de endereço real
	/// (<c>CF-Connecting-IP</c>, <c>X-Forwarded-For</c>): o proxy da
	/// frente. Vazio, vale sempre o endereço da conexão — cabeçalho de
	/// desconhecido qualquer um forja.</summary>
	public List<IpRange> TrustedProxies = new();

	/// <summary>Os limites do escudo (freio por minuto, bloqueio automático).</summary>
	public SecurityOptions Security = new();

	/// <summary>Escreve os eventos de segurança também na saída padrão, em JSON.</summary>
	public bool SecurityLog = true;

	// ------------------------------------------------------------ painel

	/// <summary>A porta do painel administrativo. É outra porta de
	/// propósito: a do jogo fica aberta para a internet, esta não — só
	/// quem está na VPN, no túnel ou na lista (<see cref="AdminAllow"/>)
	/// chega nela. Zero desliga o painel.</summary>
	public int AdminPort = 8081;

	/// <summary>Em que endereço a porta do painel escuta. Fora do Docker,
	/// só a própria máquina.</summary>
	public string AdminBind = "127.0.0.1";

	/// <summary>Quem pode falar com o painel. Vazio: quem alcançar a porta.</summary>
	public List<IpRange> AdminAllow = new();

	/// <summary>O primeiro administrador (o dono), criado ao subir se
	/// ainda não há nenhum.</summary>
	public string AdminUser = "", AdminPassword = "";

	/// <summary>Sem a segunda etapa ligada, o administrador só consegue ligá-la.</summary>
	public bool AdminRequireTotp;

	/// <summary>A pasta com o painel já compilado (o React).</summary>
	public string AdminUi = Path.Combine(AppContext.BaseDirectory, "painel");

	/// <summary>A raiz dos documentos da central de ajuda (<c>CLAUDE.md</c>,
	/// <c>docs/</c>...). Sem a variável, a pasta <c>ajuda</c> da imagem ou o
	/// próprio repositório (<c>Rotas.AdminHelpRoutes.DefaultRoot</c>).</summary>
	public string AdminDocs = "";

	/// <summary>Quem pede <c>/metrics</c> apresenta esta chave. Vazia, só a porta protege.</summary>
	public string MetricsKey = "";

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
		c.LoginsPerMinute = int.Parse(Env("ALCATEIA_LOGINS_PER_MINUTE", "12"));
		c.Database.MaxConnections = int.Parse(Env("ALCATEIA_DB_POOL", "32"));

		c.MasterKey = Env("ALCATEIA_MASTER_KEY", c.MasterKey);
		c.TrustedProxies = Ranges(Env("ALCATEIA_TRUSTED_PROXIES", ""), wide: true);
		c.Security.RequestsPerMinute = int.Parse(Env("ALCATEIA_REQUESTS_PER_MINUTE", "600"));
		c.Security.AutoBlock = Env("ALCATEIA_AUTO_BLOCK", "1") != "0";
		c.Security.Allow = Ranges(Env("ALCATEIA_IP_ALLOW", ""), wide: true);
		c.Security.RetentionDays = int.Parse(Env("ALCATEIA_EVENT_DAYS", "90"));

		c.FleetReserve = int.Parse(Env("ALCATEIA_FLEET_RESERVE", "0"));
		c.FlyApp = Env("ALCATEIA_FLY_APP", "");
		c.FlyToken = Env("ALCATEIA_FLY_TOKEN", "");
		c.FlyApi = Env("ALCATEIA_FLY_API", c.FlyApi);
		c.AdminPort = int.Parse(Env("ALCATEIA_ADMIN_PORT", "8081"));
		c.AdminBind = Env("ALCATEIA_ADMIN_BIND", c.AdminBind);
		c.AdminAllow = Ranges(Env("ALCATEIA_ADMIN_ALLOW", ""), wide: true);
		c.AdminUser = Env("ALCATEIA_ADMIN_USER", "");
		c.AdminPassword = Env("ALCATEIA_ADMIN_PASSWORD", "");
		c.AdminRequireTotp = Env("ALCATEIA_ADMIN_REQUIRE_2FA", "0") == "1";
		c.AdminUi = Env("ALCATEIA_ADMIN_UI", c.AdminUi);
		c.AdminDocs = Env("ALCATEIA_ADMIN_DOCS", Rotas.AdminHelpRoutes.DefaultRoot());
		c.MetricsKey = Env("ALCATEIA_METRICS_KEY", "");
		return c;
	}

	/// <summary>Uma lista de endereços ou faixas separada por vírgula.</summary>
	private static List<IpRange> Ranges(string text, bool wide)
	{
		var list = new List<IpRange>();
		foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			list.Add(IpRange.Parse(part, wide));
		return list;
	}

	private static string Env(string name, string fallback)
	{
		string value = Environment.GetEnvironmentVariable(name);
		return string.IsNullOrEmpty(value) ? fallback : value;
	}
}
