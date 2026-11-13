using System;
using System.Net.WebSockets;
using System.Threading.Channels;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Alcateia.Aplicacao.Portas;
using Alcateia.Aplicacao.Saguao;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>O saguão ao vivo (WebSocket) e a lista de salas (HTTP).</summary>
public static class LobbyRoutes
{
	private const int MaxMessage = 16 * 1024;

	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.MapGet("/v1/rooms", async ctx => await Api.Send(ctx, new JsonObject { ["rooms"] = s.Lobby.Rooms() }));

		app.Map("/v1/lobby", async ctx =>
		{
			if (!ctx.WebSockets.IsWebSocketRequest) throw new Failure(FailureKind.Invalid, "so_websocket", "Esta rota é WebSocket.");
			Account account = await s.Accounts.FromToken(ctx.Request.Query["token"]) ?? await s.Accounts.Require(Api.Token(ctx));

			using WebSocket socket = await ctx.WebSockets.AcceptWebSocketAsync();
			await Run(s.Lobby, account, socket);
		});
	}

	/// <summary>Atende uma conexão do começo ao fim: cada mensagem vai para
	/// o saguão; caiu ou fechou, sai da sala.</summary>
	private static async Task Run(LobbyService lobby, Account account, WebSocket socket)
	{
		var connection = new SocketConnection(socket);
		LobbyService.Session session = await lobby.Connect(account, connection);
		var buffer = new byte[8 * 1024];
		try
		{
			while (socket.State == WebSocketState.Open)
			{
				string text = await Receive(socket, buffer);
				if (text == null) break;
				await lobby.Handle(session, text);
			}
		}
		catch (WebSocketException)
		{
			// Caiu a conexão: sai como quem fechou.
		}
		finally
		{
			await lobby.Disconnect(session);
			connection.Stop();
		}
	}

	private static async Task<string> Receive(WebSocket socket, byte[] buffer)
	{
		var text = new StringBuilder();
		while (true)
		{
			WebSocketReceiveResult r = await socket.ReceiveAsync(buffer, CancellationToken.None);
			if (r.MessageType == WebSocketMessageType.Close) return null;
			text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
			if (text.Length > MaxMessage) throw new WebSocketException("mensagem grande demais");
			if (r.EndOfMessage) return text.ToString();
		}
	}

	/// <summary>
	/// A conexão de um jogador, como o saguão a vê. Mandar é pôr na fila:
	/// quem escreve no socket é um laço só desta conexão, uma mensagem por
	/// vez (o WebSocket não aceita dois envios ao mesmo tempo). Assim uma
	/// lista para milhares não espera a rede de nenhum deles.
	///
	/// Fila cheia (<see cref="MaxQueued"/>) é cliente que não dá conta: em
	/// vez de guardar mensagem sem fim na memória do servidor, ele é
	/// desligado e reconecta do zero.
	/// </summary>
	private sealed class SocketConnection : ILobbyConnection
	{
		public const int MaxQueued = 512;

		private readonly WebSocket _socket;
		private readonly Channel<string> _queue = Channel.CreateBounded<string>(
			new BoundedChannelOptions(MaxQueued) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

		public SocketConnection(WebSocket socket)
		{
			_socket = socket;
			_ = Write();
		}

		public bool IsOpen => _socket.State == WebSocketState.Open;

		public Task Send(string text)
		{
			if (!_queue.Writer.TryWrite(text) && IsOpen) _ = Close("lento demais");
			return Task.CompletedTask;
		}

		/// <summary>A conexão acabou: o laço de escrita para.</summary>
		public void Stop() => _queue.Writer.TryComplete();

		private async Task Write()
		{
			try
			{
				await foreach (string text in _queue.Reader.ReadAllAsync())
					await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception e) when (e is WebSocketException or ObjectDisposedException or InvalidOperationException)
			{
				// Quem caiu no meio é varrido pelo próprio laço de leitura.
			}
		}

		public async Task Close(string why)
		{
			try
			{
				// Avisa que fechou e, se o outro lado não responder logo, corta:
				// cliente travado não pode segurar a conexão aberta.
				if (_socket.State == WebSocketState.Open)
				{
					using var soon = new CancellationTokenSource(TimeSpan.FromSeconds(2));
					await _socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, why, soon.Token);
				}
				await Task.Delay(500);
				_socket.Abort();
			}
			catch (Exception e) when (e is WebSocketException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
			{
				_socket.Abort();
			}
		}
	}
}
