using System;
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

		WebApplication app = await App.Build(config, args);
		await app.RunAsync();
		return 0;
	}
}
