using Godot;
using System.Collections.Generic;
using System.Linq;

public enum MatchPhase
{
	/// <summary>Todos na base, ninguém se mexe. Hora de escolher o conjunto.</summary>
	Countdown,

	/// <summary>Valendo.</summary>
	Live,

	/// <summary>Alguém fez os pontos ou o tempo acabou.</summary>
	Ended
}

/// <summary>Linha do placar de um combatente.</summary>
public sealed class CombatantStats
{
	public string Name;
	public int Team;
	public int Kills;
	public int Deaths;
	public int Assists;
	/// <summary>Abates seguidos sem morrer, agora.</summary>
	public int Spree;
	public int BestSpree;
	public int Rank;
	/// <summary>A foto de perfil (<see cref="Avatars"/>); nula em bot.</summary>
	public string Photo;
	/// <summary>A foto de perfil de verdade (a escolhida no perfil, não o
	/// personagem da partida): é a que a tela de resultado mostra.</summary>
	public string ProfilePhoto;
	/// <summary>O rage (em chamas). Aqui é para mostrar (HUD, placar); o XP
	/// de verdade vem do servidor de partida, que conta o mesmo.</summary>
	public readonly Rage Rage = new();
	public bool IsLocal;
	public bool IsBot;

	/// <summary>O corpo, para o placar ler vivo/morto e forma na hora.</summary>
	public Health Health;
	public FormController Form;

	public int Points => Kills * Match.PointsPerKill + Assists * Match.PointsPerAssist;
	public bool IsDead => Health != null && GodotObject.IsInstanceValid(Health) && Health.IsDead;
	public bool IsWolf => Form != null && GodotObject.IsInstanceValid(Form) && Form.IsWolf;
}

/// <summary>
/// A partida: mata-mata em equipe, Azul contra Laranja.
///
/// Ganha o time que fizer <see cref="ScoreToWin"/> abates primeiro. Se o
/// tempo acabar antes, ganha quem tiver mais; empate é empate.
///
/// A partida não sabe quem atirou em quem. Ela escuta o
/// <see cref="KillFeed"/>, o mesmo barramento que alimenta o feed na
/// tela, e conta. Quem mata não precisa saber que existe placar.
///
/// No fim ela monta o <see cref="MatchResult"/> do jogador local e passa
/// pela <see cref="Progression"/> — uma vez só — guardando o resumo em
/// <see cref="Reward"/> para a tela de resultado animar.
///
/// Fora de uma partida — a cena de treino — não há instância, e
/// <see cref="IsLive"/> responde verdadeiro: tudo funciona como antes,
/// sem contagem nem fim.
/// </summary>
public partial class Match : Node
{
	[Signal] public delegate void PhaseChangedEventHandler(int phase);
	[Signal] public delegate void ScoreChangedEventHandler(int blue, int orange);

	/// <summary>Passou a metade do tempo. No Humanos × Lobos é quando os
	/// papéis trocam (<see cref="PlayModes.RuleFor"/>).</summary>
	[Signal] public delegate void HalfTimeEventHandler();

	public const int PointsPerKill = 10;
	public const int PointsPerAssist = 5;

	[Export] public int ScoreToWin = 40;

	/// <summary>Contagem antes de valer, em segundos. É também o tempo
	/// da tela de conjunto, de propósito: escolhe-se o equipamento
	/// enquanto se espera, e ninguém sai da base com vantagem.</summary>
	[Export] public float CountdownTime = 10f;

	/// <summary>Duração da partida, em segundos.</summary>
	[Export] public float Duration = 600f;

	[Export] public bool Debug = true;

	public static Match Current { get; private set; }

	/// <summary>Pode-se andar e atirar? Verdadeiro também quando não
	/// há partida nenhuma na cena.</summary>
	public static bool IsLive => Current == null || Current.Phase == MatchPhase.Live;

	/// <summary>Contagem antes de valer: os corpos ficam parados.</summary>
	public static bool IsCountdown => Current != null && Current.Phase == MatchPhase.Countdown;

	/// <summary>A partida acabou: ninguém comanda mais nada, mas o que
	/// estava em curso (pulo, golpe, queda) termina, com a animação.</summary>
	public static bool IsOver => Current != null && Current.Phase == MatchPhase.Ended;

	/// <summary>Já passou da metade do tempo da partida em curso.</summary>
	public static bool SecondHalf => Current != null && Current.Phase != MatchPhase.Countdown
		&& Current.TimeLeft <= Current.Duration / 2.0;
	private bool _halfAnnounced;

	public MatchPhase Phase { get; private set; } = MatchPhase.Countdown;
	public double CountdownLeft { get; private set; }
	public double TimeLeft { get; private set; }

	/// <summary>Time vencedor. 0 é empate.</summary>
	public int Winner { get; private set; }

	/// <summary>Terminou por pontos, e não por tempo.</summary>
	public bool EndedByScore { get; private set; }

	/// <summary>O que o jogador ganhou — preenchido no fim.</summary>
	public RewardSummary Reward { get; private set; }
	public MatchResult Result { get; private set; }

	public IEnumerable<CombatantStats> Combatants => _stats.Values;

	/// <summary>Maior pontuação da partida (desempate por abates).</summary>
	public CombatantStats Mvp => _stats.Values
		.OrderByDescending(s => s.Points).ThenByDescending(s => s.Kills).ThenBy(s => s.Deaths)
		.FirstOrDefault(s => s.Points > 0);

	private readonly int[] _score = new int[3];
	private readonly Dictionary<string, CombatantStats> _stats = new();
	private bool _rostered;

	// Só do jogador local, para o resultado.
	private CombatantStats _local;
	private int _localHeadshots, _localKillsWolf, _localKillsHuman;
	private double _localSeconds, _localSecondsHuman, _localSecondsWolf;
	private readonly Dictionary<string, int> _localByWeapon = new();

	public int ScoreOf(int team) => team == Teams.Blue || team == Teams.Orange ? _score[team] : 0;

	public override void _EnterTree() => Current = this;

	public override void _Ready()
	{
		ScoreToWin = RoomConfig.Active ? RoomConfig.ScoreToWin : ScoreToWin;
		Duration = RoomConfig.Active ? RoomConfig.Duration : Duration;

		// Atalhos de desenvolvimento, para ver o fim sem jogar dez
		// minutos: -- --duracao=30 --contagem=2
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--duracao=") && float.TryParse(arg["--duracao=".Length..], out float d)) Duration = d;
			if (arg.StartsWith("--contagem=") && float.TryParse(arg["--contagem=".Length..], out float c)) CountdownTime = c;
		}

		// A semente da partida vai para o Output: um defeito que dependeu
		// da sorte volta rodando com -- --seed=N (GameRandom).
		GD.Print($"Match seed: {MatchRandom.Seed} (repeat with -- --seed={MatchRandom.Seed})");

		CountdownLeft = CountdownTime;
		TimeLeft = Duration;
		Online = OnlineMatch.Active;

		KillFeed.Reported += OnKill;
	}

	public override void _ExitTree()
	{
		KillFeed.Reported -= OnKill;
		if (Current == this) Current = null;
	}

	/// <summary>Partida online: o relógio, a fase e o placar vêm do
	/// servidor (<see cref="NetSync"/>); daqui só se mostra.</summary>
	public bool Online { get; private set; }

	/// <summary>O servidor disse em que pé a partida está.</summary>
	public void NetSync(RefPhase phase, double countdownLeft, double timeLeft, int blue, int orange)
	{
		NetScore(blue, orange);
		TimeLeft = timeLeft;
		CountdownLeft = phase == RefPhase.Waiting ? CountdownTime : countdownLeft;

		MatchPhase now = phase switch
		{
			RefPhase.Live => MatchPhase.Live,
			RefPhase.Ended => MatchPhase.Ended,
			_ => MatchPhase.Countdown
		};
		if (now == Phase) return;

		if (now == MatchPhase.Ended) End(byScore: Mathf.Max(blue, orange) >= ScoreToWin);
		else SetPhase(now);
	}

	/// <summary>O placar que o servidor informou.</summary>
	public void NetScore(int blue, int orange)
	{
		if (_score[Teams.Blue] == blue && _score[Teams.Orange] == orange) return;
		_score[Teams.Blue] = blue;
		_score[Teams.Orange] = orange;
		EmitSignal(SignalName.ScoreChanged, blue, orange);
	}

	public override void _Process(double delta)
	{
		if (!_halfAnnounced && SecondHalf && Current == this && Phase == MatchPhase.Live)
		{
			_halfAnnounced = true;
			EmitSignal(SignalName.HalfTime);
		}

		// A lista sai no primeiro quadro e é refeita durante a contagem:
		// os bots nascem no _Ready do MatchSetup, e confiar que isso
		// acontece antes do primeiro quadro daqui seria apostar na ordem
		// da cena.
		if (!_rostered || Phase == MatchPhase.Countdown)
		{
			Roster();
			_rostered = true;
		}

		if (Phase == MatchPhase.Live) TickRage((float)delta);

		if (Online)
		{
			// Entre um aviso do servidor e o próximo, o relógio daqui anda
			// sozinho — mas quem muda de fase é só o servidor.
			if (Phase == MatchPhase.Countdown) CountdownLeft = Mathf.Max(CountdownLeft - delta, 0);
			if (Phase == MatchPhase.Live)
			{
				TimeLeft = Mathf.Max(TimeLeft - delta, 0);
				TrackLocal(delta);
			}
			return;
		}

		switch (Phase)
		{
			case MatchPhase.Countdown:
				// A contagem só anda depois da tela de carregamento.
				if (LoadingScreen.Busy) break;
				CountdownLeft -= delta;
				if (CountdownLeft <= 0)
				{
					CountdownLeft = 0;
					SetPhase(MatchPhase.Live);
				}
				break;

			case MatchPhase.Live:
				TimeLeft -= delta;
				TrackLocal(delta);
				if (TimeLeft <= 0)
				{
					TimeLeft = 0;
					End(byScore: false);
				}
				break;
		}
	}

	/// <summary>Quem pode recomeçar: fora do online, sempre; online, só o
	/// mestre da sala (Jhon, 21/11: "somente o mestre da sala pode reiniciar
	/// partida").</summary>
	public bool CanRestart => !Online || LobbyLink.IsRoomHost;

	/// <summary>Recomeça a mesma partida, com a mesma sala. Online, o mestre
	/// pede ao saguão: a partida de agora é descartada (ninguém ganha nada
	/// por ela) e a sala inteira vai para uma nova — chega como o começar de
	/// sempre (<c>match.start</c>), para todos. Quem não é mestre não
	/// recomeça.</summary>
	public void Restart()
	{
		if (Online)
		{
			if (CanRestart) LobbyLink.Instance.RestartMatch();
			return;
		}

		// O contador de menus é estático e sobrevive à troca de cena. Se
		// algum menu estava aberto, a cena nova nasceria achando que já há
		// menus por cima.
		UiState.Reset();
		GetTree().ReloadCurrentScene();
	}

	/// <summary>Volta ao menu: para a sala, ou para a lista de salas.</summary>
	public void Leave(ReturnTarget target)
	{
		// Sair para o lobby é sair da sala online também (Jhon, 24/11: "se eu
		// sair da partida, deve ir para o lobby e o mestre passa a ser o próximo
		// jogador"); a partida dos outros continua.
		if (target == ReturnTarget.RoomList && LobbyLink.InRoom) LobbyLink.Instance.LeaveRoom();
		UiState.Reset();
		RoomConfig.ReturnTo = RoomConfig.Active ? target : ReturnTarget.RoomList;
		GetTree().ChangeSceneToFile("res://src/scenes/menu/menu.tscn");
	}

	// ------------------------------------------------------------------

	/// <summary>Quem chegou com a partida em curso (online) entra no placar,
	/// e o bot que passou a ser movido por esta máquina troca de corpo nele.</summary>
	public void Enlist() => Roster();

	/// <summary>Todo mundo que está na cena entra no placar já no
	/// começo, com zero. Placar que só mostra quem já matou esconde
	/// quem ainda não fez nada, que é metade da informação.</summary>
	private void Roster()
	{
		foreach (string group in Groups.Fighters)
		{
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				var health = node.GetNodeOrNull<Health>("Health");
				if (health == null) continue;

				CombatantStats entry = Entry(health.PublicName, health.Team);
				var remote = node as Bot is { Remote: true } puppet ? puppet : null;
				entry.IsLocal = group == Groups.Players;
				entry.IsBot = group == Groups.Bots && (remote == null || remote.RemoteIsBot);
				entry.Health = health;
				entry.Form = node.GetNodeOrNull<FormController>("FormController");
				entry.Rank = entry.IsLocal ? Profile.Rank
					: remote != null ? remote.RemoteRank
					: Ranks.None;   // bot não tem nível
				// Na partida, a foto é o retrato do personagem com que ele joga
				// (Jhon, 21/11), não a foto de perfil: Eve, Barut...
				entry.Photo = Avatars.OfCharacter((node as Player)?.Character, health.Team);
				// No resultado, o ícone do jogador (Jhon, 25/11), não o personagem.
				entry.ProfilePhoto = entry.IsLocal ? Avatars.Selected : remote?.RemotePhoto;

				if (entry.IsLocal) _local = entry;
			}
		}
	}

	private CombatantStats Entry(string name, int team)
	{
		if (!_stats.TryGetValue(name, out CombatantStats entry))
		{
			entry = new CombatantStats { Name = name, Team = team };
			_stats[name] = entry;
		}

		return entry;
	}

	public CombatantStats StatsOf(string name) => _stats.TryGetValue(name ?? "", out CombatantStats s) ? s : null;

	private void TrackLocal(double delta)
	{
		if (_local == null) return;
		_localSeconds += delta;
		if (_local.IsDead) return;
		if (_local.IsWolf) _localSecondsWolf += delta;
		else _localSecondsHuman += delta;
	}

	private void OnKill(Kill kill)
	{
		// Abate na contagem ou depois do fim não vale nada.
		if (Phase != MatchPhase.Live) return;

		CombatantStats victim = Entry(kill.Victim, kill.VictimTeam);
		victim.Deaths++;
		victim.Spree = 0;

		// Só abate de inimigo pontua. Queda, granada nos próprios pés e
		// qualquer coisa sem autor contam como morte e nada mais.
		bool scores = (kill.KillerTeam == Teams.Blue || kill.KillerTeam == Teams.Orange)
			&& kill.KillerTeam != kill.VictimTeam;

		if (!scores) return;

		CombatantStats killer = Entry(kill.Killer, kill.KillerTeam);
		killer.Kills++;
		killer.Spree++;
		killer.BestSpree = Mathf.Max(killer.BestSpree, killer.Spree);
		killer.Rage.Kill();

		foreach (string name in kill.Assisters)
			if (name != kill.Killer)
			{
				CombatantStats helper = Entry(name, kill.KillerTeam);
				helper.Assists++;
				helper.Rage.Assist();
			}

		if (killer == _local)
		{
			if (kill.Cause == KillCause.Headshot) _localHeadshots++;
			if (kill.ByWolf) _localKillsWolf++; else _localKillsHuman++;
			if (!string.IsNullOrEmpty(kill.Weapon) && kill.Weapon != "lobo")
				_localByWeapon[kill.Weapon] = (_localByWeapon.TryGetValue(kill.Weapon, out int n) ? n : 0) + 1;
		}

		// Online, o placar e o fim são do servidor (NetScore, NetSync).
		if (Online) return;

		_score[kill.KillerTeam]++;
		EmitSignal(SignalName.ScoreChanged, _score[Teams.Blue], _score[Teams.Orange]);

		if (_score[kill.KillerTeam] >= ScoreToWin)
			End(byScore: true);
	}

	private void End(bool byScore)
	{
		EndedByScore = byScore;

		int blue = _score[Teams.Blue];
		int orange = _score[Teams.Orange];
		Winner = blue > orange ? Teams.Blue : orange > blue ? Teams.Orange : 0;

		Payout();
		SetPhase(MatchPhase.Ended);
	}

	/// <summary>Monta o resultado do jogador local e paga XP e Presas.
	/// Antes da troca de fase, para a tela de resultado já encontrar o
	/// resumo pronto quando o sinal chegar.</summary>
	/// <summary>Alguém pegou fogo ou apagou (o placar e o HUD acendem as chamas).</summary>
	public event System.Action<CombatantStats> FireChanged;

	/// <summary>Online: o rage como o servidor de partida tem (ele conta, ele
	/// manda no resultado; aqui é só o que se mostra).</summary>
	public void NetFire(string name, int team, float heat, bool onFire, float seconds)
	{
		CombatantStats entry = Entry(name, team);
		bool was = entry.Rage.OnFire;
		entry.Rage.Sync(heat, onFire, seconds);
		if (was != onFire) FireChanged?.Invoke(entry);
	}

	private void TickRage(float delta)
	{
		foreach (CombatantStats entry in _stats.Values)
			if (entry.Rage.Tick(delta)) FireChanged?.Invoke(entry);
	}

	private void Payout()
	{
		if (_local == null || Reward != null) return;

		CombatantStats mvp = Mvp;

		Result = new MatchResult
		{
			Won = Winner != 0 && Winner == _local.Team,
			Draw = Winner == 0,
			Kills = _local.Kills,
			Deaths = _local.Deaths,
			Assists = _local.Assists,
			Headshots = _localHeadshots,
			KillsWolf = _localKillsWolf,
			KillsHuman = _localKillsHuman,
			Points = _local.Points,
			Mvp = mvp == _local,
			Seconds = _localSeconds,
			SecondsHuman = _localSecondsHuman,
			SecondsWolf = _localSecondsWolf,
			KillsByWeapon = new Dictionary<string, int>(_localByWeapon),
			FireSeconds = (int)_local.Rage.FireSeconds
		};

		Reward = Progression.Apply(Result, Online);
		// O que a conta ganhou de verdade vem do servidor, daqui a pouco.
		if (Reward.Earns)
		{
			AccountSync.AlreadyShown(Reward.Promotions);
			_ = AccountSync.RefreshAfterMatch();
		}
	}

	private void SetPhase(MatchPhase phase)
	{
		Phase = phase;
		EmitSignal(SignalName.PhaseChanged, (int)phase);

		if (Debug)
			GD.Print($"Match: {phase}  (VANGUARD {_score[Teams.Blue]} x {_score[Teams.Orange]} SYNDICATE)");
	}
}
