using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Alcateia.Dominio.Contas;
using Alcateia.Dominio.Partidas;

namespace Alcateia.Dominio.Salas;

/// <summary>Alguém sentado na sala.</summary>
public sealed class RoomMember
{
	public Account Account;
	public long Xp;
	public int Team;
	public bool Ready;

	/// <summary>Está jogando a partida da sala. Com ela em curso, quem entra
	/// na sala fica de fora até escolher o time e entrar (<see cref="Room.Enter"/>).</summary>
	public bool InMatch;
}

/// <summary>Um bot posto na sala pelo dono. Número negativo, único na
/// sala; sem conta, sem XP, sem patente.</summary>
public sealed class BotSeat
{
	public long Id;
	public string Name;
	public int Team;
}

public enum RoomState
{
	Waiting,
	Playing
}

/// <summary>
/// Uma sala do saguão e as regras dela: quem entra e em que time, o dono,
/// os bots, as regras da partida e o começar. Quem chama é o saguão; aqui
/// não há rede nem trava.
/// </summary>
public sealed class Room
{
	public const int MaxTitle = 40, MaxPassword = 32, Blue = 1, Orange = 2;

	/// <summary>Os nomes dos bots, na ordem em que entram.</summary>
	private static readonly string[] BotNames =
		{ "Fenrir", "Lobo-Guará", "Presa", "Uivo", "Sombra", "Garra", "Alfa", "Matilha", "Caçador", "Lua", "Breu", "Rastro", "Faro", "Vulto", "Trovão", "Cinza" };

	public int Id { get; }
	public JsonObject Settings { get; private set; } = new();
	public string Password { get; private set; } = "";
	public long HostId { get; private set; }
	public RoomState State { get; private set; } = RoomState.Waiting;
	public string MatchKey { get; private set; }

	private readonly List<RoomMember> _members = new();
	private readonly List<BotSeat> _bots = new();
	private long _nextBot = -1;

	public IReadOnlyList<RoomMember> Members => _members;
	public IReadOnlyList<BotSeat> Bots => _bots;

	public int TeamSize => Settings["teamSize"]?.GetValue<int>() ?? 6;

	/// <summary>O modo de jogo é contra bots ("play" de 4 em diante, a
	/// ordem de PlayMode no jogo): o dono põe quantos bots quiser em cada
	/// time, e cada jogador fica no time que quiser. Nos outros modos não
	/// há bot.</summary>
	public bool VsBots => (Settings["play"]?.GetValue<int>() ?? 0) >= FirstBotMode;
	public const int FirstBotMode = 4;
	public const int LastMode = 8;
	public const int MaxTeamSize = 6;
	public int Capacity => TeamSize * 2;
	public bool HasPassword => Password != "";
	public bool IsEmpty => _members.Count == 0;
	public string StateName => State == RoomState.Waiting ? "waiting" : "playing";
	public string HostName => _members.FirstOrDefault(m => m.Account.Id == HostId)?.Account.Name ?? "";

	private Room(int id) => Id = id;

	/// <summary>Sala nova, com quem a criou dentro (no azul) e como dono.</summary>
	public static Room Create(int id, Account host, long xp, JsonObject settings)
	{
		var room = new Room(id) { HostId = host.Id };
		room.ApplySettings(settings ?? new JsonObject());
		room._members.Add(new RoomMember { Account = host, Xp = xp, Team = Blue });
		return room;
	}

	/// <summary>Quantos lugares o time tem ocupados, gente e bots.</summary>
	public int Taken(int team, RoomMember except = null) =>
		_members.Count(m => m.Team == team && m != except) + _bots.Count(b => b.Team == team);

	public bool Has(long accountId) => _members.Any(m => m.Account.Id == accountId);

	// ------------------------------------------------------------ entrar e sair

	/// <summary>Entra: vai para o time com menos gente. Se o time está cheio
	/// de bots, um bot dá o lugar: gente tem preferência.
	///
	/// Com a partida em curso também (Jhon, 24/11: "ao sair da partida, posso
	/// entrar nela novamente caso tenha vaga"). Desde 25/11 entra na sala, não
	/// direto na partida ("se a partida tiver iniciado, ele entra na sala para
	/// poder escolher o time"): fica de fora (<see cref="RoomMember.InMatch"/>
	/// falso), no time sugerido, e entra com <see cref="Enter"/>. Os bots já
	/// estão jogando e não dão o lugar. <paramref name="team"/> é a sugestão:
	/// quem já jogou esta partida começa no time em que estava, se ainda
	/// couber; senão, no que dá para entrar.</summary>
	public void Join(Account account, long xp, string password, int team = 0)
	{
		if (Has(account.Id)) return;
		if (_members.Count >= Capacity) throw new Failure(FailureKind.Conflict, "sala_cheia", "A sala está cheia.");
		if (HasPassword && !Secrets.Same(Password, password))
			throw new Failure(FailureKind.Forbidden, "senha_errada", "Senha da sala incorreta.");

		if (State == RoomState.Playing)
		{
			int[] open = new[] { Blue, Orange }.Where(CanEnter).ToArray();
			if (open.Length == 0) throw new Failure(FailureKind.Conflict, "sala_cheia", "A partida está cheia.");
			int chosen = open.Contains(team) ? team : open.OrderBy(PlayingOn).ThenBy(t => t).First();
			_members.Add(new RoomMember { Account = account, Xp = xp, Team = chosen });
			return;
		}

		int blue = _members.Count(m => m.Team == Blue), orange = _members.Count(m => m.Team == Orange);
		int side = blue <= orange ? Blue : Orange;
		MakeRoom(side);
		_members.Add(new RoomMember { Account = account, Xp = xp, Team = side });
	}

	/// <summary>Há lugar para mais alguém agora (gente e, com a partida em
	/// curso, os bots que estão jogando, sem deixar um time com dois a mais).</summary>
	public bool HasRoom => State == RoomState.Waiting
		? _members.Count < Capacity
		: _members.Count < Capacity && (CanEnter(Blue) || CanEnter(Orange));

	/// <summary>Quantos estão jogando a partida no time: gente dentro dela e bots.</summary>
	public int PlayingOn(int team) =>
		_members.Count(m => m.InMatch && m.Team == team) + _bots.Count(b => b.Team == team);

	/// <summary>Quantos lugares a sala mostra ocupados: esperando, gente e
	/// bots; jogando, quem está na partida (Jhon, 25/11: "partida com bot deve
	/// contabilizar aqui").</summary>
	public int Occupied => State == RoomState.Waiting
		? _members.Count + _bots.Count
		: PlayingOn(Blue) + PlayingOn(Orange);

	/// <summary>Dá para entrar na partida em curso por este time: tem lugar, e
	/// o time não fica com dois a mais que o outro (Jhon, 25/11: "um time só
	/// pode ter 1 a mais do que o outro").</summary>
	public bool CanEnter(int team) =>
		State == RoomState.Playing && team is Blue or Orange
		&& PlayingOn(team) < TeamSize && PlayingOn(team) <= PlayingOn(Other(team));

	public static int Other(int team) => team == Blue ? Orange : Blue;

	/// <summary>Quem está na sala, de fora da partida em curso, entra nela
	/// pelo time que escolheu.</summary>
	public void Enter(long accountId)
	{
		RoomMember seat = Seat(accountId);
		if (State != RoomState.Playing) throw new Failure(FailureKind.Conflict, "sem_partida", "A sala não está em partida.");
		if (seat.InMatch) return;
		if (PlayingOn(seat.Team) >= TeamSize) throw new Failure(FailureKind.Conflict, "time_cheio", "Esse time está cheio.");
		if (!CanEnter(seat.Team))
			throw new Failure(FailureKind.Conflict, "time_desequilibrado", "Esse time ficaria com dois a mais. Entre no outro.");
		seat.InMatch = true;
	}

	/// <summary>Volta atrás de um <see cref="Enter"/> que a partida não aceitou.</summary>
	public void Unenter(long accountId)
	{
		RoomMember seat = _members.FirstOrDefault(m => m.Account.Id == accountId);
		if (seat != null) seat.InMatch = false;
	}

	/// <summary>Sai. Se quem saiu era o dono, o mais antigo assume (Jhon,
	/// 24/11: "o mestre passa a ser o próximo jogador"); com a partida em
	/// curso, ela continua. Sala vazia acaba (os bots vão junto, e a partida
	/// dela é descartada): quem chama a tira da lista.</summary>
	public void Leave(long accountId)
	{
		_members.RemoveAll(m => m.Account.Id == accountId);
		if (_members.Count > 0 && HostId == accountId) HostId = _members[0].Account.Id;
	}

	public void ChangeTeam(long accountId, int team)
	{
		RoomMember seat = Seat(accountId);
		if (team is not (Blue or Orange)) throw new Failure(FailureKind.Invalid, "time_invalido", "Time inválido.");

		// Com a partida em curso, quem ainda está de fora escolhe o time por
		// onde vai entrar: só um que tenha lugar e não fique desequilibrado.
		if (State == RoomState.Playing && !seat.InMatch)
		{
			if (PlayingOn(team) >= TeamSize) throw new Failure(FailureKind.Conflict, "time_cheio", "Esse time está cheio.");
			if (!CanEnter(team))
				throw new Failure(FailureKind.Conflict, "time_desequilibrado", "Esse time ficaria com dois a mais.");
			seat.Team = team;
			return;
		}
		RequireWaiting("A sala está em partida.");
		if (_members.Count(m => m.Team == team && m != seat) >= TeamSize)
			throw new Failure(FailureKind.Conflict, "time_cheio", "Esse time está cheio.");

		MakeRoom(team, seat);
		seat.Team = team;
		seat.Ready = false;
	}

	public void SetReady(long accountId, bool ready) => Seat(accountId).Ready = ready;

	// ------------------------------------------------------------ o dono

	public void ChangeSettings(long accountId, JsonObject settings)
	{
		RequireHost(accountId);
		RequireWaiting("A sala está em partida.");
		ApplySettings(settings);
	}

	public void AddBot(long accountId, int team)
	{
		RequireHost(accountId);
		RequireWaiting("A sala está em partida.");
		if (!VsBots) throw new Failure(FailureKind.Conflict, "sem_bots", "Bots só nos modos contra bots.");
		if (team is not (Blue or Orange)) throw new Failure(FailureKind.Invalid, "time_invalido", "Time inválido.");
		if (Taken(team) >= TeamSize) throw new Failure(FailureKind.Conflict, "time_cheio", "Esse time está cheio.");

		string name = BotNames.FirstOrDefault(n => _bots.All(b => b.Name != n)) ?? $"Bot {-_nextBot}";
		_bots.Add(new BotSeat { Id = _nextBot--, Name = name, Team = team });
	}

	/// <summary>Tira um bot. Falso se não havia esse bot.</summary>
	public bool RemoveBot(long accountId, long botId)
	{
		RequireHost(accountId);
		RequireWaiting("A sala está em partida.");
		return _bots.RemoveAll(b => b.Id == botId) > 0;
	}

	// ------------------------------------------------------------ a partida

	/// <summary>A escalação para começar: só o dono, com todos prontos. Os
	/// bots vão com o dono da sala como dono — é a máquina dele que os move
	/// durante a partida. A sala só passa a "em partida" com
	/// <see cref="BeginMatch"/>, quando há servidor para ela.</summary>
	public MatchAssignment Lineup(long accountId, string matchKey)
	{
		RequireHost(accountId);
		RequireWaiting("A partida já começou.");
		if (_members.Any(m => !m.Ready && m.Account.Id != HostId))
			throw new Failure(FailureKind.Conflict, "nem_todos_prontos", "Nem todos estão prontos.");

		return Assignment(matchKey);
	}

	/// <summary>
	/// O dono recomeça (Jhon, 21/11: "somente o mestre da sala pode reiniciar
	/// partida"): no meio da partida ou na tela do resultado, a sala inteira
	/// vai para uma partida nova, sem esperar ninguém marcar pronto — quem
	/// está na sala jogou a de agora (no meio dela ninguém entra). A partida
	/// de agora, se houver, quem descarta é o saguão.
	/// </summary>
	public MatchAssignment RestartLineup(long accountId, string matchKey)
	{
		RequireHost(accountId);
		return Assignment(matchKey);
	}

	private MatchAssignment Assignment(string matchKey)
	{
		var bots = VsBots ? _bots.ToList() : new List<BotSeat>();
		return new MatchAssignment
		{
			MatchKey = matchKey,
			RoomId = Id,
			Settings = (JsonObject)Settings.DeepClone(),
			Players = _members.Select(m => new MatchSeat(m.Account.Id, m.Account.Name, m.Team))
				.Concat(bots.Select(b => new MatchSeat(b.Id, b.Name, b.Team, HostId))).ToList()
		};
	}

	/// <summary>Onde a partida em curso está (para quem entra no meio dela).</summary>
	public string MatchAddress { get; private set; }
	public int MatchPort { get; private set; }

	/// <summary>A sala entra em partida: todos que estão nela jogam.</summary>
	public void BeginMatch(string matchKey, string address = null, int port = 0)
	{
		foreach (RoomMember m in _members) m.InMatch = true;
		State = RoomState.Playing;
		MatchKey = matchKey;
		MatchAddress = address;
		MatchPort = port;
	}

	/// <summary>A partida acabou (com resultado ou não): a sala volta a
	/// esperar, ninguém pronto. Falso se a partida não era desta sala.</summary>
	public bool EndMatch(string matchKey)
	{
		if (MatchKey != matchKey) return false;
		State = RoomState.Waiting;
		MatchKey = null;
		MatchAddress = null;
		MatchPort = 0;
		foreach (RoomMember m in _members) m.Ready = m.InMatch = false;
		return true;
	}

	// ------------------------------------------------------------ regras

	public RoomMember Seat(long accountId) =>
		_members.FirstOrDefault(m => m.Account.Id == accountId)
		?? throw new Failure(FailureKind.Conflict, "fora_de_sala", "Você não está numa sala.");

	private void RequireHost(long accountId)
	{
		Seat(accountId);
		if (HostId != accountId) throw new Failure(FailureKind.Forbidden, "nao_e_dono", "Só o dono da sala pode fazer isso.");
	}

	private void RequireWaiting(string message)
	{
		if (State != RoomState.Waiting) throw new Failure(FailureKind.Conflict, "em_partida", message);
	}

	/// <summary>Abre um lugar no time para uma pessoa, tirando um bot se o
	/// time estiver lotado.</summary>
	private void MakeRoom(int team, RoomMember coming = null)
	{
		while (Taken(team, coming) >= TeamSize)
		{
			BotSeat last = _bots.LastOrDefault(b => b.Team == team);
			if (last == null) return;
			_bots.Remove(last);
		}
	}

	/// <summary>Confere e guarda as regras que o dono mandou. O que o
	/// servidor não entende passa adiante como veio (é o servidor de
	/// partida que aplica), desde que caiba.</summary>
	private void ApplySettings(JsonObject wanted)
	{
		if (wanted == null) throw new Failure(FailureKind.Invalid, "regras_invalidas", "Faltam as regras da sala.");
		if (wanted.ToJsonString().Length > 2048) throw new Failure(FailureKind.TooLarge, "grande_demais", "Regras grandes demais.");

		var s = (JsonObject)wanted.DeepClone();
		string title = (s["title"]?.GetValue<string>() ?? "").Trim();
		if (title.Length == 0) title = "Sala";
		if (title.Length > MaxTitle) title = title[..MaxTitle];
		s["title"] = title;

		// Times de 1 a 6 jogadores (decisão do Jhon, 17/11).
		int teamSize = s["teamSize"]?.GetValue<int>() ?? 6;
		if (teamSize is < 1 or > MaxTeamSize) throw new Failure(FailureKind.Invalid, "regras_invalidas", "Times de 1 a 6.");
		if (_members.Count > teamSize * 2) throw new Failure(FailureKind.Conflict, "regras_invalidas", "Há mais gente na sala do que cabe nesses times.");
		s["teamSize"] = teamSize;

		// O modo de jogo (PlayMode no jogo). Contra bots, todos os jogadores
		// vão para o azul — se couberem.
		int play = s["play"]?.GetValue<int>() ?? 0;
		if (play is < 0 or > LastMode) throw new Failure(FailureKind.Invalid, "regras_invalidas", "Modo de jogo inválido.");
		s["play"] = play;
		if (play < FirstBotMode) _bots.Clear();         // contra jogadores não há bot

		// Time menor: os bots que não cabem mais saem.
		foreach (int team in new[] { Blue, Orange })
			while (_bots.Any(b => b.Team == team) && Taken(team) > teamSize)
				_bots.Remove(_bots.Last(b => b.Team == team));

		s["minutes"] = Math.Clamp(s["minutes"]?.GetValue<int>() ?? 10, 3, 30);

		// Dificuldade dos bots: 0 fáceis, 1 médios, 2 difíceis.
		s["difficulty"] = Math.Clamp(s["difficulty"]?.GetValue<int>() ?? 1, 0, 2);
		s["scoreToWin"] = Math.Clamp(s["scoreToWin"]?.GetValue<int>() ?? 40, 10, 200);

		string map = s["map"]?.GetValue<string>() ?? "armazem_estendido";
		if (map.Length is 0 or > 24) throw new Failure(FailureKind.Invalid, "regras_invalidas", "Mapa inválido.");
		s["map"] = map;

		string password = s["password"]?.GetValue<string>() ?? "";
		if (password.Length > MaxPassword) throw new Failure(FailureKind.Invalid, "regras_invalidas", "Senha longa demais.");
		s.Remove("password");
		s.Remove("number");

		Password = password;
		Settings = s;
	}
}
