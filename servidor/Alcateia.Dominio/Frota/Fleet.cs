using System;
using System.Collections.Generic;
using System.Linq;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Dominio.Frota;

/// <summary>Uma batida de um servidor de partida: onde está, quantas
/// partidas aguenta e quais tem abertas agora (<see cref="Open"/> null:
/// servidor antigo, que não conta — vale uma partida e nada é conferido).</summary>
public sealed record Beat(string Id, string Address, int Port, int Players, int Capacity, IReadOnlyCollection<string> Open,
	IReadOnlyDictionary<string, MatchProgress> Progress = null);

/// <summary>
/// Onde uma partida em curso está, como o servidor de partida conta a cada
/// batida: a fase (jogando ou ainda esperando), o tempo que falta e o
/// placar. O saguão usa para não deixar entrar em partida quase acabando
/// (Jhon, 25/11: "não pode entrar em sala cuja partida está quase
/// acabando").
/// </summary>
public sealed record MatchProgress(bool Live, double TimeLeft, int Blue, int Orange, int ScoreToWin, double Duration = 0)
{
	/// <summary>Faltando menos que isto (segundos), a partida fecha para quem
	/// chega: entraria para jogar quase nada, e o resultado dele pesaria pouco.</summary>
	public const double LastSeconds = 120;

	/// <summary>...ou menos que esta parte da partida, se for menor: numa de 3
	/// minutos (o mínimo da sala), 2 minutos fechariam dois terços dela; com
	/// 20%, fecha nos últimos 36 s. De 10 minutos para cima, valem os 2 minutos.</summary>
	public const double LastShare = 0.2;

	/// <summary>A janela do fim, em segundos (sem a duração, de servidor antigo, os 2 minutos).</summary>
	public double Closing => Duration > 0 ? Math.Min(LastSeconds, Duration * LastShare) : LastSeconds;

	/// <summary>Um time com esta parte dos pontos para vencer: a partida está
	/// decidida, e fecha também.</summary>
	public const double DecidedShare = 0.8;

	public bool NearEnd => Live && (TimeLeft <= Closing || Math.Max(Blue, Orange) >= ScoreToWin * DecidedShare);
}

/// <summary>Por que uma partida em curso deixou de existir sem resultado.</summary>
public enum MatchLoss
{
	/// <summary>O servidor de partida parou de bater.</summary>
	ServerDown,

	/// <summary>O servidor de partida fechou a partida sem resultado (ninguém entrou).</summary>
	Closed
}

public sealed record LostMatch(MatchAssignment Assignment, MatchLoss Reason, string ServerId = "");

/// <summary>
/// A frota: os servidores de partida (o próprio jogo rodando sem tela)
/// que se apresentam de tempos em tempos. Cada um aguenta tantas partidas
/// ao mesmo tempo; cada batida diz quais ele tem abertas, e a resposta
/// leva as partidas novas que lhe couberam.
///
/// Regra pura, sem relógio próprio (quem chama diz que horas são) e sem
/// trava: quem usa de várias linhas (a aplicação) protege.
/// </summary>
/// <summary>Uma partida em curso, como o painel a vê.</summary>
public sealed record MatchView(string MatchKey, string Title, string Map, DateTime StartedAt, int Players);

/// <summary>Um servidor de partida, como o painel o vê.</summary>
public sealed record ServerView(string Id, string Address, int Port, int Players, int Capacity, DateTime LastSeen, IReadOnlyList<MatchView> Matches);

public sealed class Fleet
{
	/// <summary>Entregue a partida, o servidor tem este tempo para começar a
	/// listá-la nas batidas; depois, se ela some da lista, acabou sem
	/// resultado.</summary>
	public static readonly TimeSpan DeliveryGrace = TimeSpan.FromSeconds(10);

	/// <summary>Só recebe partida quem bateu há pouco (duas batidas): um
	/// servidor que reiniciou deixa o registro velho até o prazo vencer, e
	/// partida mandada para ele seria partida perdida.</summary>
	public static readonly TimeSpan Fresh = TimeSpan.FromSeconds(5);

	private sealed class Slot
	{
		public MatchAssignment Assignment;
		public DateTime? DeliveredAt;
		public bool Seen;
		public MatchProgress Progress;
	}

	private sealed class GameServer
	{
		public string Id, Address;
		public int Port, Players, Capacity = 1;
		public DateTime LastSeen;
		public readonly List<Slot> Slots = new();
		public int Free => Capacity - Slots.Count;
	}

	private readonly TimeSpan _timeout;
	private readonly Dictionary<string, GameServer> _servers = new();

	// Partida → servidor que a tem: com milhares de partidas, achar uma
	// (o resultado, a liberação) não pode varrer a frota inteira.
	private readonly Dictionary<string, GameServer> _byMatch = new();

	public Fleet(TimeSpan timeout) => _timeout = timeout;

	/// <summary>Batida de um servidor de partida. Devolve as partidas novas
	/// que ele deve abrir (cada uma vai uma vez só) e as que ele fechou
	/// sem resultado.</summary>
	public (List<MatchAssignment> Deliver, List<LostMatch> Lost) Heartbeat(Beat beat, DateTime now)
	{
		if (!_servers.TryGetValue(beat.Id, out GameServer s))
			_servers[beat.Id] = s = new GameServer { Id = beat.Id };

		s.Address = beat.Address;
		s.Port = beat.Port;
		s.Players = beat.Players;
		s.Capacity = Math.Max(beat.Capacity, 1);
		s.LastSeen = now;

		var lost = new List<LostMatch>();
		if (beat.Open != null)
		{
			foreach (Slot slot in s.Slots.ToList())
			{
				// O progresso vale mesmo com a escalação esperando ir de novo
				// (alguém acabou de entrar): a partida continua lá, contando.
				bool open = beat.Open.Contains(slot.Assignment.MatchKey);
				if (open && beat.Progress != null && beat.Progress.TryGetValue(slot.Assignment.MatchKey, out MatchProgress progress))
					slot.Progress = progress;
				if (slot.DeliveredAt == null) continue;
				slot.Seen |= open;

				// Já esteve aberta e sumiu, ou nunca abriu no prazo: acabou sem resultado.
				if (!open && (slot.Seen || now - slot.DeliveredAt.Value > DeliveryGrace))
				{
					s.Slots.Remove(slot);
					_byMatch.Remove(slot.Assignment.MatchKey);
					lost.Add(new LostMatch(slot.Assignment, MatchLoss.Closed, s.Id));
				}
			}
		}

		var deliver = new List<MatchAssignment>();
		foreach (Slot slot in s.Slots.Where(x => x.DeliveredAt == null))
		{
			slot.DeliveredAt = now;
			deliver.Add(slot.Assignment);
		}
		return (deliver, lost);
	}

	/// <summary>Reserva lugar para a partida no servidor mais folgado.
	/// Null se a frota inteira estiver cheia.</summary>
	public (string Address, int Port)? Allocate(MatchAssignment assignment, DateTime now)
	{
		GameServer free = _servers.Values.Where(s => s.Free > 0 && now - s.LastSeen <= Fresh)
			.OrderByDescending(s => s.Free).ThenBy(s => s.Players).FirstOrDefault();
		if (free == null) return null;

		free.Slots.Add(new Slot { Assignment = assignment });
		_byMatch[assignment.MatchKey] = free;
		return (free.Address, free.Port);
	}

	/// <summary>A partida em curso com esta chave, e o servidor que a tem.</summary>
	/// <summary>Lugares livres nos servidores que estão batendo.</summary>
	public int FreeSlots(DateTime now) =>
		_servers.Values.Where(s => now - s.LastSeen <= Fresh).Sum(s => Math.Max(s.Free, 0));

	/// <summary>Um servidor ocioso pede para desligar. Pode, se não tem
	/// partida e se sem ele ainda sobram <paramref name="reserve"/> lugares
	/// livres; aí sai da lista na hora — nenhuma partida nova cai nele
	/// entre o "pode" e o desligar. Quem a lista nem conhece pode ir.</summary>
	public bool Retire(string id, int reserve, DateTime now)
	{
		if (id == null || !_servers.TryGetValue(id, out GameServer s)) return true;
		if (s.Slots.Count > 0) return false;

		int others = _servers.Values.Where(x => x != s && now - x.LastSeen <= Fresh).Sum(x => Math.Max(x.Free, 0));
		if (others < reserve) return false;

		_servers.Remove(id);
		return true;
	}

	public (MatchAssignment Assignment, string ServerId)? Find(string matchKey)
	{
		if (matchKey == null || !_byMatch.TryGetValue(matchKey, out GameServer s)) return null;
		Slot slot = s.Slots.FirstOrDefault(x => x.Assignment.MatchKey == matchKey);
		return slot == null ? null : (slot.Assignment, s.Id);
	}

	/// <summary>Onde a partida está, pela última batida (null: ainda não contou).</summary>
	public MatchProgress Progress(string matchKey)
	{
		if (matchKey == null || !_byMatch.TryGetValue(matchKey, out GameServer s)) return null;
		return s.Slots.FirstOrDefault(x => x.Assignment.MatchKey == matchKey)?.Progress;
	}

	/// <summary>Um lugar a mais numa partida em curso (alguém entrou na sala
	/// com ela jogando): a escalação vai de novo ao servidor dela na próxima
	/// batida, que abre o lugar. Quem já está na escalação (jogou e saiu)
	/// volta para o lugar dele sem mandar nada; se escolheu o outro time na
	/// sala, o lugar muda de time e a escalação vai de novo. Falso se a
	/// partida não está mais na frota.</summary>
	public bool AddSeat(string matchKey, MatchSeat seat)
	{
		if (matchKey == null || !_byMatch.TryGetValue(matchKey, out GameServer s)) return false;
		Slot slot = s.Slots.FirstOrDefault(x => x.Assignment.MatchKey == matchKey);
		if (slot == null) return false;
		int at = slot.Assignment.Players.FindIndex(p => p.AccountId == seat.AccountId);
		if (at >= 0 && slot.Assignment.Players[at].Team == seat.Team) return true;

		if (at >= 0) slot.Assignment.Players[at] = slot.Assignment.Players[at] with { Team = seat.Team };
		else slot.Assignment.Players.Add(seat);
		if (slot.DeliveredAt != null)
		{
			// Já entregue: vai de novo. Ela continua aberta lá, então o prazo
			// de entrega não a dá por perdida.
			slot.DeliveredAt = null;
			slot.Seen = true;
			slot.Assignment.Update = true;
		}
		return true;
	}

	/// <summary>O time de quem já está na escalação da partida (0 se não está).</summary>
	public int SeatTeam(string matchKey, long accountId)
	{
		if (matchKey == null || !_byMatch.TryGetValue(matchKey, out GameServer s)) return 0;
		Slot slot = s.Slots.FirstOrDefault(x => x.Assignment.MatchKey == matchKey);
		return slot?.Assignment.Players.FirstOrDefault(p => p.AccountId == accountId)?.Team ?? 0;
	}

	/// <summary>A partida acabou (com resultado): o lugar fica livre.</summary>
	public void Release(string matchKey)
	{
		if (matchKey == null || !_byMatch.Remove(matchKey, out GameServer s)) return;
		s.Slots.RemoveAll(x => x.Assignment.MatchKey == matchKey);
	}

	/// <summary>Tira quem parou de bater. Devolve as partidas que caíram junto.</summary>
	public List<LostMatch> Sweep(DateTime now)
	{
		var lost = new List<LostMatch>();
		foreach (GameServer s in _servers.Values.Where(s => now - s.LastSeen > _timeout).ToList())
		{
			lost.AddRange(s.Slots.Select(x => new LostMatch(x.Assignment, MatchLoss.ServerDown, s.Id)));
			foreach (Slot slot in s.Slots) _byMatch.Remove(slot.Assignment.MatchKey);
			_servers.Remove(s.Id);
		}
		return lost;
	}

	/// <summary>Servidores, quantos têm partida, lugares no total e partidas em curso.</summary>
	/// <summary>Cada servidor de partida, para o painel.</summary>
	public List<ServerView> Servers() => _servers.Values
		.Select(s => new ServerView(s.Id, s.Address, s.Port, s.Players, s.Capacity, s.LastSeen,
			s.Slots.Select(x => new MatchView(x.Assignment.MatchKey, x.Assignment.Title, x.Assignment.Map, x.Assignment.StartedAt, x.Assignment.Players.Count)).ToList()))
		.OrderBy(s => s.Id).ToList();

	public (int Servers, int Busy, int Capacity, int Running) Status() =>
		(_servers.Count, _servers.Values.Count(s => s.Slots.Count > 0), _servers.Values.Sum(s => s.Capacity), _servers.Values.Sum(s => s.Slots.Count));
}
