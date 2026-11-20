using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Alcateia.Aplicacao.Painel;
using Alcateia.Dominio;
using Alcateia.Dominio.Painel;
using Alcateia.Infra.Seguranca;
using Alcateia.Servidor.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Alcateia.Servidor.Rotas;

/// <summary>Painel: a visão geral, a frota e as salas ao vivo — e as
/// métricas para o Prometheus (<c>/metrics</c>, só na porta do painel).</summary>
public static class AdminOverviewRoutes
{
	public static void Map(IEndpointRouteBuilder app, Services s)
	{
		app.Get(s, "/overview", async (ctx, who) =>
		{
			Overview o = await s.Overview.Now(who);
			return new JsonObject
			{
				["online"] = o.Online, ["rooms"] = o.Rooms, ["roomsPlaying"] = o.RoomsPlaying,
				["fleet"] = new JsonObject { ["servers"] = o.FleetServers, ["slots"] = o.FleetSlots, ["matches"] = o.MatchesRunning },
				["accounts"] = o.Numbers.Accounts, ["newToday"] = o.Numbers.NewToday, ["activeDay"] = o.Numbers.ActiveDay,
				["matchesToday"] = o.Numbers.MatchesToday, ["matches"] = o.Numbers.Matches, ["presas"] = o.Numbers.PresasInGame,
				["sangue"] = o.Numbers.SangueInGame, ["activeBans"] = o.Numbers.ActiveBans, ["ipBlocks"] = o.IpBlocks,
				["days"] = new JsonArray(o.Numbers.Days.Select(d => (JsonNode)new JsonObject
				{
					["day"] = d.Day.ToString("yyyy-MM-dd"), ["accounts"] = d.Accounts, ["matches"] = d.Matches
				}).ToArray()),
				["security"] = AdminSecurityRoutes.ToJson(o.Security),
				["counters"] = new JsonObject
				{
					["requests"] = o.Counters.Requests, ["refused"] = o.Counters.Refused, ["rateLimited"] = o.Counters.RateLimited,
					["autoBlocks"] = o.Counters.AutoBlocks, ["events"] = o.Counters.Events
				},
				["version"] = App.Version
			};
		});

		app.Get(s, "/fleet", (ctx, who) => System.Threading.Tasks.Task.FromResult<JsonNode>(new JsonObject
		{
			["servers"] = new JsonArray(s.Overview.Fleet(who).Select(v => (JsonNode)new JsonObject
			{
				["id"] = v.Id, ["address"] = v.Address, ["port"] = v.Port, ["players"] = v.Players, ["capacity"] = v.Capacity,
				["lastSeen"] = Api.Date(v.LastSeen),
				["matches"] = new JsonArray(v.Matches.Select(m => (JsonNode)new JsonObject
				{
					["matchKey"] = m.MatchKey, ["title"] = m.Title, ["map"] = m.Map, ["startedAt"] = Api.Date(m.StartedAt), ["players"] = m.Players
				}).ToArray())
			}).ToArray())
		}));

		app.Get(s, "/rooms", (ctx, who) =>
		{
			who.Require(Permission.View);
			return System.Threading.Tasks.Task.FromResult<JsonNode>(new JsonObject { ["rooms"] = s.Lobby.Rooms() });
		});

		app.MapGet("/metrics", async ctx =>
		{
			if (!Shield.OnAdminPort(ctx)) throw new Failure(FailureKind.NotFound, "nao_encontrado", "Não encontrado.");
			if (s.Config.MetricsKey.Length > 0 && !Passwords.SameSecret(Api.Token(ctx), s.Config.MetricsKey))
				throw new Failure(FailureKind.Unauthorized, "chave_invalida", "Chave das métricas inválida.");

			var (rooms, playing) = s.Lobby.RoomCount();
			var (servers, busy, slots, running) = s.Fleet.Status();
			var c = s.Security.Counters;
			var text = new StringBuilder();
			void Line(string name, string type, string help, long value) =>
				text.Append("# HELP ").Append(name).Append(' ').Append(help).Append("\n# TYPE ").Append(name).Append(' ').Append(type).Append('\n')
					.Append(name).Append(' ').Append(value).Append('\n');

			Line("wolfleague_online", "gauge", "Jogadores ligados no saguao.", s.Lobby.Online);
			Line("wolfleague_rooms", "gauge", "Salas abertas.", rooms);
			Line("wolfleague_rooms_playing", "gauge", "Salas em partida.", playing);
			Line("wolfleague_fleet_servers", "gauge", "Servidores de partida vivos.", servers);
			Line("wolfleague_fleet_servers_busy", "gauge", "Servidores de partida com partida.", busy);
			Line("wolfleague_fleet_slots", "gauge", "Lugares de partida na frota.", slots);
			Line("wolfleague_matches_running", "gauge", "Partidas em curso.", running);
			Line("wolfleague_ip_blocks", "gauge", "Faixas de IP bloqueadas.", s.Security.Blocks.Count);
			Line("wolfleague_requests_total", "counter", "Pedidos recebidos.", c.Requests);
			Line("wolfleague_requests_refused_total", "counter", "Pedidos recusados por bloqueio de IP.", c.Refused);
			Line("wolfleague_requests_rate_limited_total", "counter", "Pedidos recusados pelo freio.", c.RateLimited);
			Line("wolfleague_auto_blocks_total", "counter", "Bloqueios automaticos de IP.", c.AutoBlocks);
			Line("wolfleague_security_events_total", "counter", "Eventos de seguranca registrados.", c.Events);
			Line("wolfleague_security_events_dropped_total", "counter", "Eventos de seguranca que nao chegaram ao banco.", c.EventsDropped);

			ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
			await ctx.Response.WriteAsync(text.ToString());
		});
	}
}
