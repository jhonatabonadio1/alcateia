using System;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using Alcateia.Aplicacao.Contas;
using Alcateia.Aplicacao.Jogo;
using Alcateia.Aplicacao.Painel;
using Alcateia.Aplicacao.Portas;
using Alcateia.Aplicacao.Seguranca;
using Alcateia.Dominio.Seguranca;
using Alcateia.Infra.Semente;
using Alcateia.Aplicacao.Frota;
using Alcateia.Aplicacao.Partidas;
using Alcateia.Aplicacao.Saguao;
using Alcateia.Infra.Banco;
using Alcateia.Infra.Repositorios;
using Alcateia.Infra.Seguranca;
using Alcateia.Servidor.Http;
using Alcateia.Servidor.Rotas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Alcateia.Servidor;

/// <summary>Tudo o que as rotas usam, já montado.</summary>
public sealed record Services(Config Config, PgPool Db, AccountService Accounts, LobbyService Lobby,
	FleetService Fleet, MatchService Matches, RateLimit Logins)
{
	public SecurityService Security { get; init; }
	public GameDataService GameData { get; init; }
	public ShopService Shop { get; init; }
	public ChallengeService Challenges { get; init; }
	public AdminAuthService AdminAuth { get; init; }
	public PlayerAdminService Players { get; init; }
	public ContentService Content { get; init; }
	public OverviewService Overview { get; init; }
	public Auditor Audit { get; init; }
	public IPlayerRepository PlayerStore { get; init; }
}

/// <summary>
/// O servidor montado: o banco, os serviços e as rotas. É o único lugar
/// que conhece todas as camadas — a raiz da composição.
///
/// | Rota | Quem | O quê |
/// |---|---|---|
/// | GET /health | qualquer um | está de pé? o banco responde? |
/// | POST /v1/accounts | qualquer um | cria conta, já entra |
/// | POST /v1/sessions | qualquer um | entra (login) |
/// | DELETE /v1/sessions | jogador | sai |
/// | GET /v1/profile | jogador | o perfil |
/// | PUT /v1/profile/data | jogador | guarda conjuntos, personagem... |
/// | GET /v1/ranking | qualquer um | os primeiros por XP |
/// | GET /v1/rooms | qualquer um | as salas abertas |
/// | GET /v1/matches | jogador | as últimas partidas dele |
/// | GET /v1/lobby (WebSocket) | jogador | salas e chat ao vivo |
/// | POST /v1/fleet/heartbeat | servidor de partida | "estou vivo, com estas partidas"; recebe as novas |
/// | POST /v1/matches | servidor de partida | o resultado de uma partida |
/// | GET /v1/gamedata | qualquer um | catálogo, mapas, promoções, desafios e configurações |
/// | GET /v1/inventory | jogador | o que ele tem, a carteira e a patente |
/// | POST /v1/shop/buy | jogador | compra (preço, patente e saldo conferidos aqui) |
/// | GET /v1/challenges | jogador | os desafios e o progresso dele |
/// | POST /v1/challenges/{id}/claim | jogador | recebe o prêmio |
/// | /admin/v1/... | administrador | o painel — só na porta do painel |
/// | GET /metrics | Prometheus | os números — só na porta do painel |
///
/// Jogador se identifica com <c>Authorization: Bearer token</c> (no
/// WebSocket, <c>?token=</c>); servidor de partida, com
/// <c>X-Fleet-Key</c>; administrador, com o cookie de sessão do painel.
///
/// Antes de qualquer rota vem o escudo (<see cref="Shield"/>): endereço
/// real, blacklist de IP, freio de pedidos, detecção de varredura e de
/// injeção, cabeçalhos de segurança.
/// </summary>
public static class App
{
	public const string Version = "0.3.0";

	public static async Task<WebApplication> Build(Config config, string[] args = null)
	{
		var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
		builder.WebHost.ConfigureKestrel(k =>
		{
			k.AddServerHeader = false;
			k.Limits.MaxRequestBodySize = 128 * 1024;
			k.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
			k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
			k.ListenAnyIP(config.Port);
			// O painel escuta em outra porta, que não se abre para a internet.
			if (config.AdminPort != 0) k.Listen(IPAddress.Parse(config.AdminBind), config.AdminPort);
		});
		builder.Logging.ClearProviders();
		builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
		builder.Logging.SetMinimumLevel(LogLevel.Warning);
		builder.Logging.AddFilter("Alcateia", LogLevel.Information);

		Services services = Compose(config);
		builder.Services.AddSingleton(services.Db);
		builder.Services.AddSingleton(services);
		WebApplication app = builder.Build();
		ILogger log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Alcateia.Servidor");

		int from = await WaitForDatabase(services.Db, log);
		log.LogInformation("banco na versão {To} (estava na {From})", Schema.Latest, from);
		if (config.FleetKey == Config.DevFleetKey)
			log.LogWarning("ALCATEIA_FLEET_KEY está com o valor de desenvolvimento: troque antes de abrir para a internet");
		if (config.MasterKey == Config.DevMasterKey)
			log.LogWarning("ALCATEIA_MASTER_KEY está com o valor de desenvolvimento: troque antes de abrir para a internet");

		await Prepare(services, log);

		app.Lifetime.ApplicationStopped.Register(() =>
		{
			services.Lobby.Dispose();
			services.Security.Dispose();
			services.Db.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
		});
		app.UseShield(config, services.Security);
		app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
		app.UseJsonErrors(log);
		app.UsePanelFiles(config);

		Api.OnBadFleetKey = ctx => services.Security.Suspicious(ClientIp.Address(ctx), ClientIp.Of(ctx), EventKind.FleetKey, ctx.Request.Path);

		HealthRoutes.Map(app, services);
		AccountRoutes.Map(app, services);
		LobbyRoutes.Map(app, services);
		FleetRoutes.Map(app, services);
		MatchRoutes.Map(app, services);
		GameRoutes.Map(app, services);
		if (config.AdminPort != 0)
		{
			AdminSessionRoutes.Map(app, services);
			AdminOverviewRoutes.Map(app, services);
			AdminPlayerRoutes.Map(app, services);
			AdminContentRoutes.Map(app, services);
			AdminSecurityRoutes.Map(app, services);
		}
		return app;
	}

	/// <summary>Liga as camadas: os repositórios e a segurança da infra nas
	/// portas da aplicação.</summary>
	public static Services Compose(Config config)
	{
		var db = new PgPool(config.Database);
		var clock = new SystemClock();
		var hasher = new Pbkdf2Hasher();
		var tokens = new Sha256Tokens();
		var playerStore = new PgPlayerRepository(db);
		var securityStore = new PgSecurityRepository(db);
		var adminStore = new PgAdminRepository(db);
		var matchStore = new PgMatchRepository(db);

		var security = new SecurityService(securityStore, new JsonSecurityLog(config.SecurityLog ? Console.Out : null), clock, config.Security);
		var gameData = new GameDataService(new PgGameDataRepository(db));
		var accounts = new AccountService(new PgAccountRepository(db), hasher, tokens, clock, config.SessionDays);
		IFleetScaler scaler = config.FlyApp != "" && config.FlyToken != ""
			? new Alcateia.Infra.Fly.FlyMachines(config.FlyApi, config.FlyApp, config.FlyToken) : null;
		var fleet = new FleetService(config.FleetTimeout, new FleetSigner(config.FleetKey), clock, scaler, config.FleetReserve);
		var lobby = new LobbyService(fleet, accounts, clock, config.TicketLife, config.RoomListEvery, config.MaxRooms) { MapAllowed = gameData.MapAllowed, Inventory = playerStore.Inventory };
		var challenges = new ChallengeService(playerStore, gameData, clock);
		var matches = new MatchService(matchStore, fleet, lobby, gameData, challenges, security, clock);

		var audit = new Auditor(adminStore);
		var adminAuth = new AdminAuthService(adminStore, hasher, tokens, new AesCipher(config.MasterKey), clock, security, audit)
		{
			RequireTotp = config.AdminRequireTotp
		};

		return new Services(config, db, accounts, lobby, fleet, matches, new RateLimit(config.LoginsPerMinute))
		{
			Security = security,
			GameData = gameData,
			Shop = new ShopService(playerStore, gameData, clock),
			Challenges = challenges,
			AdminAuth = adminAuth,
			Players = new PlayerAdminService(playerStore, securityStore, matchStore, accounts, lobby, gameData, audit, clock),
			Content = new ContentService(new PgGameDataRepository(db), gameData, audit),
			Overview = new OverviewService(playerStore, lobby, fleet, security),
			Audit = audit,
			PlayerStore = playerStore
		};
	}

	/// <summary>Com o banco pronto: a semente do jogo, a blacklist em
	/// memória e o primeiro administrador.</summary>
	public static async Task Prepare(Services services, ILogger log)
	{
		SeedData.Seed seed = SeedData.Load();
		await services.GameData.Seed(seed.Catalog, seed.Maps, seed.Challenges, seed.ReleasedModes);
		await services.Security.Start();

		Config config = services.Config;
		if (config.AdminPort == 0) return;
		if (await services.AdminAuth.Bootstrap(config.AdminUser, config.AdminPassword))
			log?.LogWarning("primeiro administrador criado: {Name}. Entre no painel, troque a senha e ligue a verificação em duas etapas", config.AdminUser);
		if (!Directory.Exists(config.AdminUi))
			log?.LogWarning("a pasta do painel ({Path}) não existe: a API do painel responde, mas a tela não", config.AdminUi);
	}

	/// <summary>Espera o banco subir (no Docker ele pode demorar uns
	/// segundos a mais que o servidor) e aplica as migrações.</summary>
	private static async Task<int> WaitForDatabase(PgPool db, ILogger log)
	{
		for (int attempt = 1; ; attempt++)
		{
			try
			{
				return await Schema.Migrate(db);
			}
			catch (Exception e) when (attempt < 30 && e is System.Net.Sockets.SocketException or IOException or PgException { Code: "57P03" })
			{
				log.LogInformation("esperando o banco ({Attempt}/30): {Message}", attempt, e.Message);
				await Task.Delay(1000);
			}
		}
	}
}
