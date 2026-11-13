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
/// jogo de verdade falando com o servidor de verdade. O que cria atira no
/// boneco do outro com a arma de sempre; o que entra só fica de pé e
/// renasce. No fim cada um imprime uma linha <c>ROBO ...</c> com o que
/// viu, e o script confere as duas.
///
/// <c>--conta=Nome</c> escolhe o nome da conta.
/// </summary>
public partial class OnlineRobot : Node
{
	public string Role = "criar";

	private const string Password = "senha-de-robo-1";

	private string _name;
	private bool _inMatch, _done, _started;
	private int _puppetShots, _hitsTaken, _lobbyEnds;
	private double _aimTimer, _matchTime, _endTime;
	private JsonObject _lobbyResult;

	private bool Host => Role == "criar";

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_name = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--conta="))?["--conta=".Length..]
			?? (Host ? "RoboCriador" : "RoboVisitante");
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
			lobby.CreateRoom(OnlineMatch.SettingsTo(new RoomSettings { Title = "Sala dos robôs", TeamSize = 4, ScoreToWin = 10, Minutes = 3 }));
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

		var members = lobby.Room["members"] as JsonArray;
		JsonNode me = members.FirstOrDefault(m => m["id"].GetValue<long>() == Backend.AccountId);
		if (me == null) return;

		if (!Host)
		{
			if (!me["ready"].GetValue<bool>()) lobby.SetReady(true);
			return;
		}

		// O criador começa quando o visitante chegou, do outro lado e pronto.
		JsonNode guest = members.FirstOrDefault(m => m != me);
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
			// De volta ao menu depois da partida: confere e sai.
			if (_inMatch) Finish();
			return;
		}

		if (!_inMatch)
		{
			_inMatch = true;
			foreach (Bot puppet in GetTree().GetNodesInGroup(Groups.Bots).OfType<Bot>().Where(b => b.Remote))
			{
				puppet.GetNode<Weapon>("Weapon").Fired += () => _puppetShots++;
			}
			(Groups.LocalPlayer(GetTree())?.GetNode<Health>("Health")).Damaged += (_, _) => _hitsTaken++;
		}

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

		if (!Host || match.Phase != MatchPhase.Live) return;

		Bot target = GetTree().GetNodesInGroup(Groups.Bots).OfType<Bot>()
			.FirstOrDefault(b => b.Remote && b.Visible && !b.GetNode<Health>("Health").IsDead);
		if (target == null) return;

		// Para a uns metros do alvo, num ponto de onde dá para vê-lo, e
		// atira com a arma de sempre.
		_aimTimer -= delta;
		if (_aimTimer <= 0)
		{
			_aimTimer = 0.4;
			player.GlobalPosition = Vantage(player, target);
			player.Velocity = Vector3.Zero;
		}

		Vector3 to = target.GlobalPosition - player.GlobalPosition;
		player.Rotation = new Vector3(0, Mathf.Atan2(-to.X, -to.Z), 0);
		player.GetNode<Node3D>("Head").Rotation = Vector3.Zero;
		player.GetNode<Weapon>("Weapon").TryAttack();
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

		JsonNode m = _lobbyResult?["match"];
		GD.Print($"ROBO {_name} fim: partidas={matches} xp={xp} abates={kills} mortes={deaths} " +
			$"placar={m?["scoreBlue"]}x{m?["scoreOrange"]} vencedor={m?["winner"]} " +
			$"tiros_do_boneco={_puppetShots} acertos_levados={_hitsTaken} avisos_do_saguao={_lobbyEnds} " +
			$"sala={(LobbyLink.Instance.Room != null ? LobbyLink.Instance.Room["state"] : "nenhuma")}");

		GetTree().Quit(0);
	}

	private void Fail(string why)
	{
		_done = true;
		GD.Print($"ROBO {_name} FALHOU: {why}");
		GetTree().Quit(1);
	}
}
