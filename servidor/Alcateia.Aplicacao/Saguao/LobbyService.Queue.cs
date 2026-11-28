using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Alcateia.Dominio;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Fila;
using Alcateia.Dominio.Partidas;
using Alcateia.Dominio.Salas;

namespace Alcateia.Aplicacao.Saguao;

/// <summary>
/// O jogo rápido (Jhon, 27/11: "sistema de jogo rápido que escolhe os players
/// baseado nas suas habilidades, níveis, experiências [...] extremamente bem
/// equilibrado. modo aleatório, mapa aleatório"; "aí você decide a quantidade
/// de players"): uma fila. O jogador (ou o grupo inteiro, pelo líder) entra
/// nela e o servidor junta gente de habilidade parecida, divide em dois times
/// da mesma força, sorteia o modo e o mapa, abre a sala e começa a partida.
///
/// O jogo manda <c>queue.join</c> e <c>queue.leave</c>; o servidor responde
/// com <c>queue</c> (procurando, com quantos há na fila; parado, com o
/// porquê; ou partida encontrada, com o mapa, o modo e o tamanho dos times) e,
/// achada a partida, com a <c>room</c> e o <c>match.start</c> de sempre. O
/// formato está em servidor/LEIA-ME.md.
///
/// **Quem decide.** As regras são do domínio e não sabem de rede:
/// <see cref="Matchmaker"/> (quem joga com quem, quantos por time, a divisão,
/// o mapa e o modo) e <see cref="SkillRules"/> (a nota de habilidade: de onde
/// parte e quanto anda por partida). Aqui ficam a fila em si, o relógio que a
/// faz andar (o mesmo da lista de salas, uma vez por segundo) e a ponte com a
/// sala e a frota.
///
/// **A sala do jogo rápido** (<see cref="Room.Quick"/>) nasce com os times
/// prontos e já em partida. Ninguém entra nela, troca de time nem mexe nas
/// regras; não aparece na lista; acaba quando a partida acaba, e aí as notas
/// de quem jogou são acertadas (quem largou no meio leva derrota).
///
/// **O que tira alguém da fila**: cancelar, ir para uma sala, o grupo mudar
/// (entrou ou saiu alguém) e a ligação cair. Num grupo, a busca é de todos:
/// qualquer um que a pare, para para todos.
///
/// A fila vive em memória, como as salas: se o servidor reiniciar, quem
/// estava procurando procura de novo.
/// </summary>
public sealed partial class LobbyService
{
	/// <summary>Quanto dura a partida do jogo rápido, em minutos. Os pontos
	/// para vencer saem do tamanho dos times (<see cref="Matchmaker.ScoreFor"/>).</summary>
	public const int QuickMinutes = 10;

	/// <summary>Entradas na fila a cada 20 s, por conexão (quem fica
	/// entrando e saindo para "pescar" adversário para aqui).</summary>
	public const int QueueJoinsPerWindow = 8;

	/// <summary>De quanto em quanto tempo quem está procurando fica sabendo
	/// quantos há na fila, mesmo que nada tenha mudado.</summary>
	private static readonly TimeSpan QueueTellEvery = TimeSpan.FromSeconds(10);

	/// <summary>Os mapas que o jogo rápido pode sortear (o identificador e o
	/// porte: Pequeno, Médio, Grande): os que o painel deixou ligados. Sem
	/// isso (testes), o Armazém.</summary>
	public Func<IReadOnlyList<(string Id, string Size)>> QuickMaps { get; set; }

	/// <summary>O sorteio (um número de 0 a n-1): o lado de cada time, o modo e o mapa.</summary>
	public Func<int, int> Random { get; set; } = System.Random.Shared.Next;

	/// <summary>Onde a nota de habilidade fica guardada. Sem isso (um saguão
	/// montado sem banco), ela vale só enquanto a conta está ligada.</summary>
	public Func<long, double, int, Task> SaveSkill { get; set; }

	/// <summary>Uma partida do jogo rápido em curso: quem foi escalado, em
	/// que time e com que nota — é com isso que as notas são acertadas no fim.</summary>
	private sealed record QuickRoster(Dictionary<long, (int Team, Skill Skill)> Players);

	private readonly Dictionary<long, QueueTicket> _queue = new();          // por quem pediu (o líder)
	private readonly Dictionary<string, QuickRoster> _quick = new();        // por chave da partida
	private bool _queueTick;
	private int _queueTold = -1;
	private DateTime _queueToldAt;

	/// <summary>Quantos jogadores estão procurando partida agora (para o painel).</summary>
	public int Searching { get { lock (_lock) return _queue.Values.Sum(t => t.Size); } }

	// ------------------------------------------------------------ a nota

	/// <summary>A nota de uma conta: a que ela construiu na fila, ou — se
	/// nunca jogou por ela — a largada pelo histórico (nível, vitórias, K/D,
	/// MVPs de tudo o que já jogou).</summary>
	private static Skill SkillOf(Profile profile)
	{
		if (profile.Skill is double rating) return new Skill(rating, profile.SkillMatches);
		int Stat(string name) => profile.Stats != null && profile.Stats.TryGetValue(name, out int value) ? value : 0;
		return new Skill(SkillRules.Seed(RankRule.LevelOf(profile.Rank), Stat("matches"), Stat("wins"), Stat("losses"), Stat("kills"), Stat("deaths"), Stat("mvps")), 0);
	}

	// ------------------------------------------------------------ entrar e sair

	/// <summary>Entra na fila: sozinho, ou com o grupo inteiro (só o líder pede).</summary>
	private void QueueJoin(Session c, Outbox outbox)
	{
		if (c.Ticket != null)
		{
			outbox.To(c, QueueStatus(c.Ticket, _clock.UtcNow));
			return;
		}

		List<Session> party;
		if (c.Group is Group group)
		{
			if (group.Leader != c.Account.Id)
				throw new Failure(FailureKind.Conflict, "so_o_lider", $"Você está num grupo: quem procura partida é o líder ({LeaderName(group)}).");
			party = group.Members.Select(id => _sessions.TryGetValue(id, out Session s) ? s : null).Where(s => s != null).ToList();
		}
		else party = new List<Session> { c };

		foreach (Session s in party)
			if (s.Room != null)
				throw new Failure(FailureKind.Conflict, "em_sala", s == c
					? "Saia da sala antes de procurar partida."
					: $"{s.Account.Name} está numa sala: o grupo todo precisa estar fora de sala para procurar partida.");
		Throttle(c.QueueJoins, QueueJoinsPerWindow, InviteWindow, "Devagar: espere uns segundos para procurar partida de novo.");

		DateTime now = _clock.UtcNow;
		var ticket = new QueueTicket(c.Account.Id,
			party.Select(s => new QueueMember(s.Account.Id, s.Account.Name, s.Skill)).ToList(), now);
		_queue[ticket.Id] = ticket;
		foreach (Session s in party) s.Ticket = ticket;

		_queueTold = -1;   // a fila mudou: todos ficam sabendo na próxima passada
		outbox.To(party, QueueStatus(ticket, now));
		TickQueue(outbox, now: true);
	}

	/// <summary>Tira da fila a entrada desta conexão (a dele, ou a do grupo
	/// dele). <paramref name="why"/>: o recado para quem estava procurando
	/// (null: foi ele mesmo que cancelou).</summary>
	private void QueueLeave(Session c, Outbox outbox, string why)
	{
		QueueTicket ticket = c.Ticket;
		if (ticket == null) return;

		_queue.Remove(ticket.Id);
		_queueTold = -1;
		foreach (QueueMember member in ticket.Members)
		{
			if (!_sessions.TryGetValue(member.AccountId, out Session s) || s.Ticket != ticket) continue;
			s.Ticket = null;
			if (s.Gone) continue;
			// Num grupo, quem não cancelou fica sabendo quem parou a busca.
			string note = why ?? (s == c ? null : $"{c.Account.Name} cancelou a busca.");
			outbox.To(s, new JsonObject { ["t"] = "queue", ["state"] = "idle", ["note"] = note });
		}
	}

	private JsonObject QueueStatus(QueueTicket ticket, DateTime now, string note = null) => new()
	{
		["t"] = "queue",
		["state"] = "searching",
		// Quantos há procurando (com ele), há quanto tempo ele procura e o
		// menor time que o formador já aceita para ele.
		["players"] = _queue.Values.Sum(t => t.Size),
		["waited"] = (int)ticket.Wait(now),
		["size"] = ticket.Size,
		["teamMin"] = Math.Max(Matchmaker.MinTeam(ticket.Wait(now)), ticket.Size),
		["note"] = note
	};

	// ------------------------------------------------------------ o relógio

	/// <summary>
	/// A fila anda: forma todas as partidas que dá para formar agora e conta
	/// a quem ficou quantos há. Chamado pelo relógio da lista de salas (uma
	/// passada sim, uma não: uma vez por segundo) e na hora em que alguém
	/// entra. Dentro da trava.
	/// </summary>
	private void TickQueue(Outbox outbox, bool now = false)
	{
		if (_queue.Count == 0) return;
		if (!now)
		{
			_queueTick = !_queueTick;
			if (!_queueTick) return;
		}

		DateTime at = _clock.UtcNow;
		string note = null;
		foreach (QuickMatch match in Matchmaker.FormAll(_queue.Values, at))
		{
			if (LaunchQuick(match, outbox)) continue;
			// Sem lugar na frota: as outras partidas desta passada também não teriam.
			note = "Partida formada, mas não há servidor de partida livre agora. Seguimos tentando.";
			break;
		}

		int players = _queue.Values.Sum(t => t.Size);
		if (players == 0 || (players == _queueTold && note == null && at - _queueToldAt < QueueTellEvery)) return;
		_queueTold = players;
		_queueToldAt = at;
		foreach (QueueTicket ticket in _queue.Values)
			outbox.To(ticket.Members.Select(m => _sessions.TryGetValue(m.AccountId, out Session s) && s.Ticket == ticket ? s : null),
				QueueStatus(ticket, at, note));
	}

	/// <summary>
	/// Abre a sala de uma partida formada e a põe para jogar: sorteia o lado
	/// de cada time, o modo e o mapa (entre os que servem para o tamanho do
	/// time), pede lugar à frota e manda a cada um a sala e o ingresso. Falso
	/// se não havia servidor de partida livre: todos continuam na fila.
	/// </summary>
	private bool LaunchQuick(QuickMatch match, Outbox outbox)
	{
		// Alguém da partida não está mais ligado ou foi para uma sala sem a
		// fila saber: a entrada dele sai e o formador tenta de novo sem ela.
		List<QueueTicket> stale = match.Tickets.Where(t => t.Members.Any(m =>
			!_sessions.TryGetValue(m.AccountId, out Session s) || s.Ticket != t || s.Room != null)).ToList();
		if (stale.Count > 0)
		{
			foreach (QueueTicket ticket in stale)
			{
				_queue.Remove(ticket.Id);
				foreach (QueueMember m in ticket.Members)
					if (_sessions.TryGetValue(m.AccountId, out Session s) && s.Ticket == ticket)
					{
						s.Ticket = null;
						outbox.To(s, new JsonObject { ["t"] = "queue", ["state"] = "idle", ["note"] = "A busca parou: procure de novo." });
					}
			}
			return true;
		}

		if (Random(2) == 1) match = match.Swapped();
		IReadOnlyList<(string Id, string Size)> maps = QuickMaps?.Invoke();
		string map = Matchmaker.PickMap(maps, match.TeamSize, Random) ?? "armazem_estendido";
		int mode = Matchmaker.PickMode(Random);

		var settings = new JsonObject
		{
			["title"] = "Jogo rápido",
			["quick"] = true,
			["mode"] = "Equipe",
			["teamSize"] = match.TeamSize,
			["map"] = map,
			["play"] = mode,
			["minutes"] = QuickMinutes,
			["scoreToWin"] = Matchmaker.ScoreFor(match.TeamSize),
			["weapons"] = 0,
			["friendlyFire"] = false,
			["spectators"] = false
		};

		List<Session> Team(IEnumerable<QueueTicket> tickets) => tickets.SelectMany(t => t.Members).Select(m => _sessions[m.AccountId]).ToList();
		List<Session> blue = Team(match.One), orange = Team(match.Two);

		Room room = Room.Matched(_nextRoom, blue.Select(s => (s.Account, s.Xp)).ToList(), orange.Select(s => (s.Account, s.Xp)).ToList(), settings);
		MatchAssignment assignment = room.QuickLineup(Guid.NewGuid().ToString("N"));
		AddItems(room, assignment);

		(string Address, int Port)? server = _fleet.Allocate(assignment);
		if (server == null) return false;

		_nextRoom++;
		_rooms[room.Id] = room;
		var roster = new Dictionary<long, (int Team, Skill Skill)>();
		var found = new JsonObject
		{
			["t"] = "queue", ["state"] = "found", ["room"] = room.Id,
			["map"] = map, ["play"] = mode, ["teamSize"] = match.TeamSize
		};
		foreach (QueueTicket ticket in match.Tickets) _queue.Remove(ticket.Id);
		foreach (Session s in blue.Concat(orange))
		{
			roster[s.Account.Id] = (blue.Contains(s) ? Room.Blue : Room.Orange, s.Skill);
			s.Ticket = null;
			s.Room = room;
		}
		_quick[assignment.MatchKey] = new QuickRoster(roster);
		_queueTold = -1;

		room.BeginMatch(assignment.MatchKey, server.Value.Address, server.Value.Port);
		outbox.To(blue.Concat(orange), found);
		// A sala chega antes do ingresso: é com ela que o jogo monta a escalação.
		RoomState(room, outbox);
		foreach (Session s in blue.Concat(orange)) SendTicket(room, assignment.MatchKey, s, false, outbox);
		return true;
	}

	// ------------------------------------------------------------ o fim da partida

	/// <summary>
	/// Acerta as notas de quem jogou uma partida da fila. Quem foi escalado e
	/// não estava mais nela quando acabou (<paramref name="present"/>) leva
	/// derrota, ganhe o time ou não. Dentro da trava; quem grava é
	/// <see cref="SaveRated"/>, fora dela.
	/// </summary>
	private IReadOnlyList<SkillRules.Change> RateQuick(SettledMatch match, HashSet<long> present)
	{
		if (!_quick.Remove(match.Assignment.MatchKey, out QuickRoster roster)) return null;

		Dictionary<long, int> points = match.Lines.ToDictionary(l => l.Seat.AccountId, l => l.Points);
		List<SkillRules.Played> played = roster.Players.Select(p => new SkillRules.Played(
			p.Key, p.Value.Team, p.Value.Skill, points.GetValueOrDefault(p.Key), !present.Contains(p.Key))).ToList();

		IReadOnlyList<SkillRules.Change> changes = SkillRules.Update(played, match.Winner, match.ScoreBlue, match.ScoreOrange);
		foreach (SkillRules.Change change in changes)
			if (_sessions.TryGetValue(change.AccountId, out Session s)) s.Skill = change.After;
		return changes;
	}

	private async Task SaveRated(IReadOnlyList<SkillRules.Change> changes)
	{
		if (changes == null || SaveSkill == null) return;
		foreach (SkillRules.Change change in changes)
		{
			try
			{
				await SaveSkill(change.AccountId, change.After.Rating, change.After.Matches);
			}
			catch (Exception e) when (e is not Failure)
			{
				// O banco não respondeu: a nota fica valendo em memória e a próxima partida grava por cima.
			}
		}
	}

	/// <summary>A sala do jogo rápido acabou: cada um volta para o saguão.</summary>
	private void Dissolve(Room room, Outbox outbox)
	{
		foreach (RoomMember m in room.Members.ToList())
		{
			room.Leave(m.Account.Id);
			Session s = SessionOf(m);
			if (s == null || s.Room != room) continue;
			s.Room = null;
			outbox.To(s, new JsonObject { ["t"] = "left", ["id"] = room.Id });
			SyncPresence(s, outbox);
		}
		_rooms.Remove(room.Id);
	}
}
