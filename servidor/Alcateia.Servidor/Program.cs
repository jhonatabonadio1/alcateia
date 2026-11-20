using System;
using System.Linq;
using Alcateia.Dominio;
using Alcateia.Dominio.Painel;
using Alcateia.Infra.Banco;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace Alcateia.Servidor;

public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		Config config = Config.FromEnvironment();

		// --saude: pergunta ao servidor que já está rodando se está tudo
		// bem e sai com 0 ou 1. É o que o Docker usa para saber se o
		// contêiner está saudável (a imagem não tem curl).
		if (Array.IndexOf(args, "--saude") >= 0)
		{
			try
			{
				using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
				HttpResponseMessage r = await http.GetAsync($"http://127.0.0.1:{config.Port}/health");
				return r.IsSuccessStatusCode ? 0 : 1;
			}
			catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
			{
				return 1;
			}
		}

		// --admin nome [papel]: cria um administrador do painel, ou conserta
		// um que se trancou fora (senha nova, segunda etapa desligada). A
		// senha vem de ALCATEIA_ADMIN_PASSWORD ou é pedida aqui.
		int admin = Array.IndexOf(args, "--admin");
		if (admin >= 0) return await RescueAdmin(config, args.Skip(admin + 1).ToArray());

		WebApplication app = await App.Build(config, args);
		await app.RunAsync();
		return 0;
	}

	private static async Task<int> RescueAdmin(Config config, string[] args)
	{
		if (args.Length == 0)
		{
			Console.Error.WriteLine("uso: Alcateia.Servidor --admin <nome> [dono|admin|moderador|leitor]");
			return 2;
		}

		string password = config.AdminPassword;
		if (string.IsNullOrEmpty(password))
		{
			Console.Error.Write($"Senha nova de {args[0]} (12 ou mais caracteres): ");
			password = Console.ReadLine();
		}

		Services services = App.Compose(config);
		try
		{
			await Schema.Migrate(services.Db);
			await services.AdminAuth.Rescue(args[0], password, args.Length > 1 ? args[1] : Roles.Owner);
			Console.Error.WriteLine($"Pronto: {args[0]} pode entrar no painel. A verificação em duas etapas dele foi desligada — ligue de novo.");
			return 0;
		}
		catch (Failure e)
		{
			Console.Error.WriteLine(e.Message);
			return 1;
		}
		finally
		{
			await services.Db.DisposeAsync();
		}
	}
}
