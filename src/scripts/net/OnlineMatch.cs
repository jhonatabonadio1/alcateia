using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// A partida online para a qual o saguão mandou este jogador: onde fica o
/// servidor de partida, o ingresso para entrar e quem mais joga.
///
/// Estático pelo mesmo motivo do <see cref="RoomConfig"/>: a sala e a
/// partida são cenas diferentes. Fora de partida online,
/// <see cref="Active"/> é falso e nada aqui é lido.
/// </summary>
public static class OnlineMatch
{
	/// <summary>Um lugar na partida: um jogador, ou um bot (número
	/// negativo; <see cref="Owner"/> é a conta do jogador cuja máquina o
	/// move). Bot não tem XP nem patente.</summary>
	public sealed record Seat(long Id, string Name, int Team, long Xp, long Owner = 0, string Photo = null)
	{
		public bool IsBot => Id < 0;

		/// <summary>É esta máquina que move este bot.</summary>
		public bool Mine => IsBot && Owner == AccountId;
	}

	public static bool Active { get; private set; }
	public static string Address { get; private set; }
	public static int Port { get; private set; }
	public static string MatchKey { get; private set; }
	public static long AccountId { get; private set; }
	public static long Expires { get; private set; }
	public static string Ticket { get; private set; }
	public static List<Seat> Roster { get; } = new();

	/// <summary>Um recado para a tela Online mostrar na volta (a conexão
	/// caiu, o servidor recusou).</summary>
	public static string Notice { get; set; }

	public static Seat Me => Roster.FirstOrDefault(s => s.Id == AccountId);

	/// <summary>O servidor mandou começar: guarda tudo, monta a sala como
	/// a partida espera e troca de cena.</summary>
	public static void Launch(SceneTree tree, JsonObject start, JsonObject room)
	{
		// O mestre reiniciou com a partida em curso: a de agora larga o
		// servidor dela já e não encerra esta (o carregamento leva para a nova).
		NetMatch.Current?.Supersede();

		Address = start["address"].GetValue<string>();
		Port = start["port"].GetValue<int>();
		MatchKey = start["matchKey"].GetValue<string>();
		AccountId = start["accountId"].GetValue<long>();
		Expires = start["expires"].GetValue<long>();
		Ticket = start["ticket"].GetValue<string>();

		// O teste aponta todos os jogos para o servidor de partida local.
		string forced = OS.GetEnvironment("ALCATEIA_GAME_ADDRESS");
		if (!string.IsNullOrEmpty(forced)) Address = forced;

		// Entrou com a partida em curso: os bots já estão sendo movidos por
		// alguém, e quem é o servidor de partida diz (NetWire.BotOwner) se um
		// dia passarem para cá. Até lá, todos são bonecos aqui.
		bool late = start["late"]?.GetValue<bool>() ?? false;
		Roster.Clear();
		foreach (JsonNode m in room?["members"] as JsonArray ?? new JsonArray())
		{
			// Entrando no meio, quem está na sala mas de fora da partida (ainda
			// escolhendo o time) não está jogando: não vira boneco aqui.
			if (late && !(m["inMatch"]?.GetValue<bool>() ?? true) && m["id"].GetValue<long>() != AccountId) continue;
			Roster.Add(new Seat(m["id"].GetValue<long>(), m["name"].GetValue<string>(), m["team"].GetValue<int>(),
				m["xp"]?.GetValue<long>() ?? 0, late ? 0 : m["owner"]?.GetValue<long>() ?? 0, m["photo"]?.GetValue<string>()));
		}
		Roster.Sort((a, b) => a.Id.CompareTo(b.Id));

		RoomConfig.Settings = SettingsFrom(start["settings"] as JsonObject);

		// Os bots da sala jogam na dificuldade que o dono escolheu. Só a
		// máquina do dono os move, mas todas ficam com o mesmo número.
		Difficulty.Set(RoomConfig.Settings.Difficulty);
		RoomConfig.Members.Clear();
		Seat me = Me;
		RoomConfig.Members.Add(new RoomMember { Name = me?.Name ?? Backend.AccountName, Team = me?.Team ?? Teams.Blue, IsHost = true });
		RoomConfig.Active = true;
		RoomConfig.ReturnTo = ReturnTarget.Room;

		Active = true;
		Notice = null;
		UiState.Reset();
		LoadingScreen.Open(tree);
	}

	/// <summary>Saiu da partida (acabou, desistiu ou caiu).</summary>
	public static void Finish()
	{
		Active = false;
		RoomConfig.Active = false;
	}

	/// <summary>As regras de uma sala, do jeito que vão para o servidor.</summary>
	public static JsonObject SettingsTo(RoomSettings s) => new()
	{
		["title"] = s.Title,
		["password"] = s.Password,
		["mode"] = RoomSettings.ModeName(s.Mode),
		["teamSize"] = s.TeamSize,
		["map"] = s.Map,
		["scoreToWin"] = s.ScoreToWin,
		["minutes"] = s.Minutes,
		["weapons"] = (int)s.Weapons,
		["play"] = (int)s.Play,
		["minRank"] = s.MinRank,
		["maxRank"] = s.MaxRank,
		["spectators"] = s.Spectators,
		["swapSides"] = s.SwapSides,
		["friendlyFire"] = s.FriendlyFire,
		["difficulty"] = (int)s.Difficulty
	};

	/// <summary>As regras como o servidor as devolve.</summary>
	public static RoomSettings SettingsFrom(JsonObject j)
	{
		var s = new RoomSettings();
		if (j == null) return s;

		s.Title = j["title"]?.GetValue<string>() ?? s.Title;
		s.TeamSize = j["teamSize"]?.GetValue<int>() ?? s.TeamSize;
		s.Map = j["map"]?.GetValue<string>() ?? s.Map;
		s.ScoreToWin = j["scoreToWin"]?.GetValue<int>() ?? s.ScoreToWin;
		s.Minutes = j["minutes"]?.GetValue<int>() ?? s.Minutes;
		s.FriendlyFire = j["friendlyFire"]?.GetValue<bool>() ?? false;
		s.MinRank = System.Math.Clamp(j["minRank"]?.GetValue<int>() ?? 0, 0, Ranks.Count - 1);
		s.MaxRank = System.Math.Clamp(j["maxRank"]?.GetValue<int>() ?? Ranks.Count - 1, s.MinRank, Ranks.Count - 1);
		s.Spectators = j["spectators"]?.GetValue<bool>() ?? true;
		s.SwapSides = j["swapSides"]?.GetValue<bool>() ?? false;

		int weapons = j["weapons"]?.GetValue<int>() ?? 0, play = j["play"]?.GetValue<int>() ?? 0;
		if (System.Enum.IsDefined(typeof(WeaponRule), weapons)) s.Weapons = (WeaponRule)weapons;
		if (System.Enum.IsDefined(typeof(PlayMode), play)) s.Play = (PlayMode)play;

		int difficulty = j["difficulty"]?.GetValue<int>() ?? (int)BotLevel.Medium;
		if (System.Enum.IsDefined(typeof(BotLevel), difficulty)) s.Difficulty = (BotLevel)difficulty;
		if (!Maps.All.Any(m => m.Id == s.Map)) s.Map = Maps.All[0].Id;
		return s;
	}
}
