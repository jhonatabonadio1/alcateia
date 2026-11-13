using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Alcateia.Servidor;

/// <summary>Um lugar numa partida: um jogador, ou um bot. O bot tem
/// número negativo no lugar da conta e um dono (<see cref="Owner"/>): a
/// conta do jogador cuja máquina o move. Bot não é conta — não ganha XP,
/// não tem patente e não entra no resultado.</summary>
public sealed record MatchSeat(long AccountId, string Name, int Team, long Owner = 0)
{
	public bool IsBot => AccountId < 0;
}

/// <summary>Uma partida entregue a um servidor de partida.</summary>
public sealed class Assignment
{
	public string MatchKey;
	public int RoomId;
	public JsonObject Settings;
	public List<MatchSeat> Players = new();
	public DateTime StartedAt = DateTime.UtcNow;
	public bool Delivered;
}

/// <summary>
/// A frota: os servidores de partida (o próprio jogo rodando sem tela)
/// que se apresentam a este servidor de tempos em tempos. Cada batida
/// diz "estou vivo, neste endereço, livre ou ocupado"; a resposta leva a
/// partida que lhe coube, se houver.
///
/// Fica em memória: servidor de partida é coisa de agora. Se este
/// processo reiniciar, eles voltam a bater e a lista se refaz.
/// </summary>
public sealed class Fleet
{
	private sealed class GameServer
	{
		public string Id, Address;
		public int Port, Players;
		public DateTime LastSeen;
		public Assignment Assignment;
	}

	private readonly Config _config;
	private readonly Dictionary<string, GameServer> _servers = new();
	private readonly object _lock = new();

	/// <summary>Um servidor com partida entregue sumiu: a sala volta a esperar.</summary>
	public event Action<Assignment> Lost;

	public Fleet(Config config) => _config = config;

	/// <summary>Batida de um servidor de partida. Devolve a partida que
	/// ele deve abrir, uma vez.</summary>
	public Assignment Heartbeat(string id, string address, int port, int players)
	{
		lock (_lock)
		{
			if (!_servers.TryGetValue(id, out GameServer s))
				_servers[id] = s = new GameServer { Id = id };

			s.Address = address;
			s.Port = port;
			s.Players = players;
			s.LastSeen = DateTime.UtcNow;

			if (s.Assignment == null || s.Assignment.Delivered) return null;
			s.Assignment.Delivered = true;
			return s.Assignment;
		}
	}

	/// <summary>Reserva um servidor livre para a partida. Null se não houver.</summary>
	public (string Address, int Port)? Allocate(Assignment assignment)
	{
		List<Assignment> lost = Sweep();
		(string, int)? result = null;

		lock (_lock)
		{
			GameServer free = _servers.Values.Where(s => s.Assignment == null).OrderBy(s => s.Players).FirstOrDefault();
			if (free != null)
			{
				free.Assignment = assignment;
				result = (free.Address, free.Port);
			}
		}

		foreach (Assignment a in lost) Lost?.Invoke(a);
		return result;
	}

	/// <summary>A partida em curso com esta chave, e o servidor que a tem.</summary>
	public (Assignment Assignment, string ServerId)? Find(string matchKey)
	{
		lock (_lock)
		{
			GameServer s = _servers.Values.FirstOrDefault(x => x.Assignment?.MatchKey == matchKey);
			return s == null ? null : (s.Assignment, s.Id);
		}
	}

	/// <summary>A partida acabou: o servidor fica livre de novo.</summary>
	public void Release(string matchKey)
	{
		lock (_lock)
			foreach (GameServer s in _servers.Values)
				if (s.Assignment?.MatchKey == matchKey) s.Assignment = null;
	}

	public JsonObject Status()
	{
		List<Assignment> lost = Sweep();
		JsonObject status;
		lock (_lock)
			status = new JsonObject { ["servers"] = _servers.Count, ["busy"] = _servers.Values.Count(s => s.Assignment != null) };

		foreach (Assignment a in lost) Lost?.Invoke(a);
		return status;
	}

	/// <summary>O ingresso de um jogador para uma partida: só quem tem a
	/// chave da frota consegue fazer ou conferir.</summary>
	public string Ticket(string matchKey, long accountId, long expiresUnix) =>
		Passwords.Sign(_config.FleetKey, $"{matchKey}|{accountId}|{expiresUnix}");

	/// <summary>Tira quem parou de bater. Devolve as partidas que caíram junto.</summary>
	private List<Assignment> Sweep()
	{
		var lost = new List<Assignment>();
		lock (_lock)
		{
			foreach (GameServer s in _servers.Values.Where(s => DateTime.UtcNow - s.LastSeen > _config.FleetTimeout).ToList())
			{
				if (s.Assignment != null) lost.Add(s.Assignment);
				_servers.Remove(s.Id);
			}
		}
		return lost;
	}
}
