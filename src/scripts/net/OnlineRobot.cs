using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// Um robô que joga uma partida online do começo ao fim, sem tela:
/// cria a conta, entra no saguão, abre a sala (<c>--robo=criar</c>) ou
/// entra na primeira que aparecer (<c>--robo=entrar</c>), joga e confere
/// o resultado.
///
/// Existe para o teste de ponta a ponta (tools/testar_online.sh): é o
/// jogo de verdade falando com o servidor de verdade. O que cria põe um
/// bot em cada time e atira no boneco do outro com a arma de sempre; o
/// que entra abate uma vez o bot inimigo (que a máquina do criador
/// move) e depois só fica de pé e renasce. No fim cada um imprime uma
/// linha <c>ROBO ...</c> com o que viu, e o script confere as duas.
///
/// <c>--conta=Nome</c> escolhe o nome da conta. <c>--sair=S</c> (no
/// criador): depois de S segundos de partida valendo, ele sai dela (vai para o
/// lobby e deixa a sala; os bots dele passam para o visitante) e volta pela
/// lista de salas, para a mesma partida (Jhon, 24/11).
/// </summary>
public partial class OnlineRobot : Node
{
	public string Role = "criar";

	private const string Password = "senha-de-robo-1";

	private string _name;
	private bool _inMatch, _done, _started;
	private int _puppetShots, _hitsTaken, _lobbyEnds;
	private int _botPuppets, _botShots, _botKills, _killsOnBots, _myBots, _botsAdded;

	// Partida autoritativa (decisão 0011): o modo que o servidor disse e
	// quantas vezes ele corrigiu o corpo deste robô.
	private bool _authoritative;
	private int _corrections;
	private readonly System.Collections.Generic.Dictionary<Bot, Vector3> _botStart = new();
	private readonly System.Collections.Generic.HashSet<string> _botsMoved = new();
	private BotLevel _level;

	// Na autoritativa o criador também tenta trapacear (decisão 0016):
	// manda acertos inventados, de longe do próprio corpo, e o servidor
	// tem de recusar todos.
	public const int Forgeries = 3;
	private int _forged;
	private double _aimTimer, _matchTime, _endTime;
	private JsonObject _lobbyResult;

	private bool Host => Role == "criar";

	// Sair e voltar (--sair=S): quando sai, a sala de onde saiu, e quantas
	// vezes voltou. E quantos bots esta máquina chegou a mover (o visitante
	// passa a mover os do criador quando ele sai).
	private double _leaveAt, _liveTime, _rejoinTimer;
	private bool _leftOnce, _away, _hooked;
	private int _roomId, _rejoins, _maxMyBots;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_name = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--conta="))?["--conta=".Length..]
			?? (Host ? "RoboCriador" : "RoboVisitante");
		string leave = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--sair="))?["--sair=".Length..];
		if (Host && leave != null) _leaveAt = double.Parse(leave, System.Globalization.CultureInfo.InvariantCulture);
		_ = Run();
	}

	private async Task Run()
	{
		Backend.Reply reply = await Backend.Register(_name, Password);
		if (reply.Status == 409) reply = await Backend.Login(_name, Password);
		if (!reply.Ok)
		{
			Fail($"não entrei na conta: {reply.Status} {reply.Error}");
			return;
		}

		LobbyLink lobby = LobbyLink.Instance;
		lobby.Failed += (code, message) => GD.Print($"ROBO {_name} aviso: {code} {message}");
		lobby.MatchEnded += m =>
		{
			_lobbyEnds++;
			_lobbyResult = m;
		};
		lobby.Changed += OnLobbyChanged;

		if (!await lobby.Open())
		{
			Fail("não abri o saguão");
			return;
		}

		if (Host)
			lobby.CreateRoom(OnlineMatch.SettingsTo(new RoomSettings { Title = "Sala dos robôs", TeamSize = 4, ScoreToWin = 10, Minutes = 3, Difficulty = BotLevel.Hard, Play = PlayMode.BotsLivre }));
	}

	private void OnLobbyChanged()
	{
		LobbyLink lobby = LobbyLink.Instance;
		if (_started || _done) return;

		if (lobby.Room == null)
		{
			// O visitante entra na primeira sala que estiver esperando.
			JsonNode open = lobby.Rooms.FirstOrDefault(r => r["state"]?.GetValue<string>() == "waiting");
			if (!Host && open != null) lobby.JoinRoom(open["id"].GetValue<int>());
			return;
		}

		_roomId = lobby.Room["id"].GetValue<int>();
		var members = lobby.Room["members"] as JsonArray;
		JsonNode me = members.FirstOrDefault(m => m["id"].GetValue<long>() == Backend.AccountId);
		if (me == null) return;

		// O criador põe um bot em cada time, uma vez.
		if (Host && _botsAdded == 0)
		{
			_botsAdded = 2;
			lobby.AddBot(Teams.Blue);
			lobby.AddBot(Teams.Orange);
		}
		bool IsBot(JsonNode m) => m["bot"]?.GetValue<bool>() ?? false;

		if (!Host)
		{
			if (!me["ready"].GetValue<bool>()) lobby.SetReady(true);
			return;
		}

		// O criador começa quando o visitante chegou, do outro lado e pronto.
		JsonNode guest = members.FirstOrDefault(m => m != me && !IsBot(m));
		if (members.Count(IsBot) < 2) return;
		if (guest == null || !guest["ready"].GetValue<bool>() || guest["team"].GetValue<int>() == me["team"].GetValue<int>()) return;

		_started = true;
		lobby.StartMatch();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done) return;

		Match match = Match.Current;
		if (match == null || NetMatch.Current == null)
		{
			// Saiu no meio: volta pela lista de salas, para a mesma partida.
			if (_away)
			{
				Rejoin(delta);
				return;
			}
			// De volta ao menu depois da partida: confere e sai.
			if (_inMatch) Finish();
			return;
		}
		if (_away)
		{
			_away = false;
			_rejoins++;
			GD.Print($"ROBO {_name} voltou para a partida.");
		}

		_authoritative |= NetMatch.Current.Authoritative;
		_corrections = Math.Max(_corrections, NetMatch.Current.Corrections);

		if (!_inMatch)
		{
			_inMatch = true;
			if (_hooked) return;
			_hooked = true;
			foreach (Bot puppet in GetTree().GetNodesInGroup(Groups.Bots).OfType<Bot>().Where(b => b.Remote))
			{
				if (puppet.RemoteIsBot)
				{
					_botPuppets++;
					_botStart[puppet] = puppet.GlobalPosition;
					puppet.GetNode<Weapon>("Weapon").Fired += () => _botShots++;
				}
				else puppet.GetNode<Weapon>("Weapon").Fired += () => _puppetShots++;
			}
			_myBots = GetTree().GetNodesInGroup(Groups.Bots).OfType<Bot>().Count(b => !b.Remote);
			_level = Difficulty.Current;

			// Os abates que o servidor anuncia: quantos têm bot, e quantos
			// são meus em cima de um bot.
			var bots = OnlineMatch.Roster.Where(s => s.IsBot).Select(s => s.Name).ToHashSet();
			string mine = OnlineMatch.Me?.Name;
			KillFeed.Reported += kill =>
			{
				if (bots.Contains(kill.Killer) || bots.Contains(kill.Victim)) _botKills++;
				if (kill.Killer == mine && bots.Contains(kill.Victim)) _killsOnBots++;
			};
			(Groups.LocalPlayer(GetTree())?.GetNode<Health>("Health")).Damaged += (_, _) => _hitsTaken++;
		}

		_maxMyBots = Math.Max(_maxMyBots, GetTree().GetNodesInGroup(Groups.Bots).OfType<Bot>().Count(b => !b.Remote));
		foreach (var (puppet, start) in _botStart)
			if (IsInstanceValid(puppet) && puppet.GlobalPosition.DistanceTo(start) > 2f) _botsMoved.Add(puppet.Name);

		_matchTime += delta;
		if (_matchTime > 150)
		{
			Fail("a partida não acabou em 150 s");
			return;
		}

		if (match.Phase == MatchPhase.Ended)
		{
			// Dá tempo de o servidor registrar o resultado e avisar o saguão.
			_endTime += delta;
			if (_endTime > 6 || _lobbyEnds > 0) match.Leave(ReturnTarget.Room);
			return;
		}

		var player = Groups.LocalPlayer(GetTree()) as CharacterBody3D;
		var health = player.GetNode<Health>("Health");
		if (health.IsDead)
		{
			health.TryRespawn();
			return;
		}

		if (match.Phase != MatchPhase.Live) return;

		_liveTime += delta;
		if (_leaveAt > 0 && !_leftOnce && _liveTime > _leaveAt)
		{
			_leftOnce = _away = true;
			_inMatch = false;
			GD.Print($"ROBO {_name} saiu da partida.");
			match.Leave(ReturnTarget.RoomList);
			return;
		}

		// O criador atira no boneco do visitante. O visitante atira no bot
		// inimigo (que a máquina do criador move) até abatê-lo uma vez.
		if (!Host && _killsOnBots > 0) return;
		Bot target = GetTree().GetNodesInGroup(Groups.Bots).OfType<Bot>()
			.FirstOrDefault(b => b.Remote && b.Visible && !b.GetNode<Health>("Health").IsDead
				&& (Host ? !b.RemoteIsBot : b.RemoteIsBot && b.Team != health.Team));
		if (target == null) return;

		// Para a uns metros do alvo, num ponto de onde dá para vê-lo, e
		// atira com a arma de sempre.
		_aimTimer -= delta;
		if (_aimTimer <= 0)
		{
			_aimTimer = 0.4;
			// Na partida autoritativa, mover o corpo à mão vale como
			// renascer ali (é o servidor que move; nesta leva ele confia).
			Vector3 spot = Vantage(player, target);
			if (NetMatch.Current is { Authoritative: true } net) net.Teleport(spot);
			else player.GlobalPosition = spot;
			player.Velocity = Vector3.Zero;
		}

		if (Host && _authoritative && _forged < Forgeries && _matchTime > 8 + 2 * _forged && NetMatch.Current is { } cheat)
		{
			cheat.SendForgedHit(target.RemoteId, 400f, player.GlobalPosition + new Vector3(0, 60, 0));
			_forged++;
		}

		Vector3 to = target.GlobalPosition - player.GlobalPosition;
		player.Rotation = new Vector3(0, Mathf.Atan2(-to.X, -to.Z), 0);
		player.GetNode<Node3D>("Head").Rotation = Vector3.Zero;
		player.GetNode<Weapon>("Weapon").TryAttack();
	}

	/// <summary>Fora da partida, depois de sair: entra de novo na sala de onde
	/// saiu, quando a lista a mostrar jogando com vaga; na sala, escolhe um
	/// lado aberto e entra na partida (25/11: o saguão manda o ingresso e a
	/// partida carrega).</summary>
	private void Rejoin(double delta)
	{
		_rejoinTimer -= delta;
		if (_rejoinTimer > 0) return;
		_rejoinTimer = 1;

		LobbyLink lobby = LobbyLink.Instance;
		if (lobby.Room != null)
		{
			JsonNode me = (lobby.Room["members"] as JsonArray)?.FirstOrDefault(m => m?["id"]?.GetValue<long>() == Backend.AccountId);
			int[] enter = (lobby.Room["enter"] as JsonArray)?.Select(t => t.GetValue<int>()).ToArray() ?? Array.Empty<int>();
			if (me == null || (me["inMatch"]?.GetValue<bool>() ?? false) || enter.Length == 0) return;
			int team = me["team"].GetValue<int>();
			if (enter.Contains(team)) lobby.EnterMatch();
			else lobby.SetTeam(enter[0]);
			return;
		}
		JsonNode room = lobby.Rooms.FirstOrDefault(r => r["id"].GetValue<int>() == _roomId);
		if (room == null || !(room["open"]?.GetValue<bool>() ?? false))
		{
			lobby.RefreshRooms();
			return;
		}
		lobby.JoinRoom(_roomId);
	}

	/// <summary>Um ponto em volta do alvo com linha de tiro livre.</summary>
	private static Vector3 Vantage(CharacterBody3D player, Bot target)
	{
		PhysicsDirectSpaceState3D space = player.GetWorld3D().DirectSpaceState;
		Vector3 chest = target.GlobalPosition + Vector3.Up * 0.4f;

		for (int i = 0; i < 12; i++)
		{
			float angle = Mathf.Tau * i / 12f;
			Vector3 spot = target.GlobalPosition + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 5f + Vector3.Up * 0.2f;

			var ray = PhysicsRayQueryParameters3D.Create(spot + Vector3.Up * 0.6f, chest);
			ray.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
			Godot.Collections.Dictionary hit = space.IntersectRay(ray);
			if (hit.Count > 0 && hit["collider"].AsGodotObject() == target) return spot;
		}

		return target.GlobalPosition + new Vector3(0, 0.2f, 5f);
	}

	private async void Finish()
	{
		_done = true;

		Backend.Reply profile = await Backend.Profile();
		JsonNode stats = profile.Body?["stats"];
		long xp = profile.Body?["xp"]?.GetValue<long>() ?? -1;
		string matches = stats?["matches"]?.ToString() ?? "?", kills = stats?["kills"]?.ToString() ?? "?", deaths = stats?["deaths"]?.ToString() ?? "?";

		// A conta, pelo mesmo caminho das telas (AccountSync → Profile): o
		// que o servidor pagou pela partida, as missões que ele andou, uma
		// compra que ele aceita e uma que ele recusa.
		bool account = await AccountSync.Refresh();
		long presas = Profile.Presas;
		int missions = Profile.Missions.All().Count(), moving = Profile.Missions.All().Count(p => p.Progress > 0);
		string bought = await AccountSync.Buy(AccountSync.RenameItem, Duration.Permanent);
		int uses = Profile.Find(AccountSync.RenameItem)?.Uses ?? 0;
		long spent = presas - Profile.Presas;
		string refused = await AccountSync.Buy("scar", Duration.Day1);        // pede patente que conta nova não tem

		JsonNode m = _lobbyResult?["match"];
		int botsMoved = _botsMoved.Count;
		GD.Print($"ROBO {_name} fim: partidas={matches} xp={xp} abates={kills} mortes={deaths} " +
			$"placar={m?["scoreBlue"]}x{m?["scoreOrange"]} vencedor={m?["winner"]} " +
			$"tiros_do_boneco={_puppetShots} acertos_levados={_hitsTaken} avisos_do_saguao={_lobbyEnds} " +
			$"bots_meus={_myBots} bonecos_bot={_botPuppets} bots_andaram={botsMoved} tiros_de_bot={_botShots} dificuldade={_level} " +
			$"abates_com_bot={_botKills} abateu_bot={_killsOnBots} " +
			$"modo={(_authoritative ? "autoritativo" : "repasse")} correcoes={_corrections} forjados={_forged} " +
			$"conta={(account && Profile.HasAccount ? "ok" : "falhou")} xp_no_jogo={Profile.Xp} presas={presas} missoes={missions} missoes_andando={moving} " +
			$"compra={(bought == null ? "ok" : "recusada")} usos={uses} gasto={spent} recusa={(refused != null ? "ok" : "comprou")} " +
			$"sala={(LobbyLink.Instance.Room != null ? LobbyLink.Instance.Room["state"] : "nenhuma")} " +
			$"saiu={(_leftOnce ? 1 : 0)} voltou={_rejoins} bots_meus_max={_maxMyBots}");

		GetTree().Quit(0);
	}

	private void Fail(string why)
	{
		_done = true;
		GD.Print($"ROBO {_name} FALHOU: {why}");
		GetTree().Quit(1);
	}
}
