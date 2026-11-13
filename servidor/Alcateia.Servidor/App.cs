using System;
using System.IO;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Contas;
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
	FleetService Fleet, MatchService Matches, RateLimit Logins);

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
///
/// Jogador se identifica com <c>Authorization: Bearer token</c> (no
/// WebSocket, <c>?token=</c>); servidor de partida, com
/// <c>X-Fleet-Key</c>.
/// </summary>
public static class App
{
	public const string Version = "0.2.0";

	public static async Task<WebApplication> Build(Config config, string[] args = null)
	{
		var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
		builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");
		builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 128 * 1024);
		builder.Logging.ClearProviders();
		builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
		builder.Logging.SetMinimumLevel(LogLevel.Warning);
		builder.Logging.AddFilter("Alcateia", LogLevel.Information);

		Services services = Compose(config);
		builder.Services.AddSingleton(services.Db);
		WebApplication app = builder.Build();
		ILogger log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Alcateia.Servidor");

		int from = await WaitForDatabase(services.Db, log);
		log.LogInformation("banco na versão {To} (estava na {From})", Schema.Latest, from);
		if (config.FleetKey == Config.DevFleetKey)
			log.LogWarning("ALCATEIA_FLEET_KEY está com o valor de desenvolvimento: troque antes de abrir para a internet");

		app.Lifetime.ApplicationStopped.Register(() =>
		{
			services.Lobby.Dispose();
			services.Db.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
		});
		app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
		app.UseJsonErrors(log);

		HealthRoutes.Map(app, services);
		AccountRoutes.Map(app, services);
		LobbyRoutes.Map(app, services);
		FleetRoutes.Map(app, services);
		MatchRoutes.Map(app, services);
		return app;
	}

	/// <summary>Liga as camadas: os repositórios e a segurança da infra nas
	/// portas da aplicação.</summary>
	private static Services Compose(Config config)
	{
		var db = new PgPool(config.Database);
		var clock = new SystemClock();
		var accounts = new AccountService(new PgAccountRepository(db), new Pbkdf2Hasher(), new Sha256Tokens(), clock, config.SessionDays);
		var fleet = new FleetService(config.FleetTimeout, new FleetSigner(config.FleetKey), clock);
		var lobby = new LobbyService(fleet, accounts, clock, config.TicketLife, config.RoomListEvery, config.MaxRooms);
		var matches = new MatchService(new PgMatchRepository(db), fleet, lobby);
		return new Services(config, db, accounts, lobby, fleet, matches, new RateLimit(config.LoginsPerMinute));
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
