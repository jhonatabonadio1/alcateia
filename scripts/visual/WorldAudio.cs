using Godot;
using System.Collections.Generic;

/// <summary>
/// O som do mundo: passos, pousos, tiros, uivos, explosões, e os avisos
/// da partida.
///
/// **Não inventa evento.** Escuta o <see cref="NoiseBus"/> — o mesmo
/// barramento que os bots ouvem — e toca no ponto e no raio do ruído.
/// É a regra do docs/DESIGN.md: o que o jogador ouve e o que o bot ouve são a
/// mesma coisa, senão o jogador não consegue prever a reação de quem
/// ouviu. Agachado e andando com Shift não emitem ruído, então também
/// não fazem som.
///
/// Os sons do próprio jogador tocam sem posição (iguais nos dois
/// ouvidos) e um pouco mais baixos: o passo de quem você é não pode
/// cobrir o passo de quem está atrás de você.
///
/// Fica uma vez em cada cena de jogo (main e treino).
/// </summary>
public partial class WorldAudio : Node
{
	/// <summary>Volume dos seus próprios passos e pousos, em dB. Um
	/// pouco abaixo dos outros: o seu passo não pode cobrir o de quem
	/// está atrás de você. Abaixo de uns −6 ele some no meio do jogo.</summary>
	[Export] public float OwnStepVolume = -3f;

	/// <summary>Escreve no Output cada ruído recebido e o som que ele
	/// tocou — ou "SEM ARQUIVO". Para descobrir por que algo está mudo.</summary>
	[Export] public bool Debug;

	/// <summary>Distância a partir da qual um tiro usa a versão
	/// distante (<c>tiro_distante</c>), se ela existir.</summary>
	[Export] public float DistantShot = 35f;

	/// <summary>Até onde se ouve o tiro de outro, em metros (decisão do
	/// Jhon: o tiro do inimigo vem dele, da distância dele, de qualquer
	/// ponto do mapa). É o único som que passa do raio do ruído: o bot
	/// continua ouvindo só até o raio da arma, e o volume cai com a
	/// distância a partir dele — longe, abafado, mas presente.</summary>
	[Export] public float ShotReach = 120f;

	/// <summary>O uivo emite ruído a cada meio segundo para os bots;
	/// o som toca uma vez por uivo.</summary>
	[Export] public float HowlRetrigger = 1.5f;

	/// <summary>
	/// Som do uivo (R, de lobo). É o mesmo da transformação, decisão do
	/// Jhon: o rugido de virar lobo é o grito da fera, e o uivo de cura
	/// usa a mesma voz. Para um uivo próprio, ponha o arquivo em
	/// <c>assets/audio/</c> e troque este nome.
	/// </summary>
	/// <summary>O rugido (18/11: gravação própria, do pacote que o Jhon
	/// mandou; antes era o som da transformação, que fica de reserva).</summary>
	[Export] public string HowlSound = "lobo_rugido";
	[Export] public string HowlFallback = "lobo_transformar";

	private Node _local;
	private Health _localHealth;
	private readonly Dictionary<ulong, double> _lastHowl = new();
	private double _clock;

	private MatchPhase _phase = MatchPhase.Countdown;
	private int _countdownSecond = -1;

	private MapInfo _map;
	private GameRandom _rng;
	private double _eventTimer;

	public override void _Ready()
	{
		NoiseBus.Subscribe(OnNoise);
		KillFeed.Reported += OnKill;
		EnterMap();
	}

	/// <summary>
	/// O som do lugar: a reverberação, o fundo em laço e o que acontece
	/// de vez em quando (o trem no túnel ao lado). Vem do
	/// <see cref="MapInfo"/> do mapa carregado; cena sem mapa (o treino)
	/// fica com um ar livre neutro.
	/// </summary>
	private void EnterMap()
	{
		_map = Nodes.FindFirst<MapSlot>(GetTree().CurrentScene ?? GetParent())?.Info;
		_rng = MatchRandom.Stream("ambiente");

		Sfx.SetRoom(_map?.RoomSize ?? 0.3f, _map?.RoomWet ?? 0.06f);
		if (_map == null) return;

		Sfx.Ambience(this, _map.Ambience, _map.AmbienceDb);
		_eventTimer = _rng.Range(_map.EventEvery.X * 0.4f, _map.EventEvery.Y * 0.6f);
	}

	private void UpdateAmbientEvent(double delta)
	{
		if (string.IsNullOrEmpty(_map?.EventSound)) return;

		_eventTimer -= delta;
		if (_eventTimer > 0) return;

		_eventTimer = _rng.Range(_map.EventEvery.X, _map.EventEvery.Y);
		Sfx.Own(_map.EventSound, _map.AmbienceDb);
	}

	public override void _ExitTree()
	{
		NoiseBus.Unsubscribe(OnNoise);
		KillFeed.Reported -= OnKill;
	}

	public override void _Process(double delta)
	{
		_clock += delta;
		FindLocal();
		UpdateMatch();
		UpdateAmbientEvent(delta);
	}

	private void FindLocal()
	{
		if (_local != null && IsInstanceValid(_local)) return;

		_local = Groups.LocalPlayer(GetTree());
		_localHealth = _local?.GetNodeOrNull<Health>("Health");
	}

	private void OnNoise(Noise noise)
	{
		Node source = noise.Source;
		bool mine = source != null && source == _local;
		bool wolf = source?.GetNodeOrNull<FormController>("FormController")?.IsWolf ?? false;

		switch (noise.Kind)
		{
			case NoiseKind.Step:
				Play(OnSurface(wolf ? "lobo_passo" : "passo", noise), noise, mine, mine ? OwnStepVolume : 0f);
				break;

			case NoiseKind.Land:
				Play(OnSurface(wolf ? "lobo_pouso" : "pouso", noise), noise, mine, mine ? OwnStepVolume : 0f);
				break;

			case NoiseKind.Howl:
				if (source == null) break;
				ulong id = source.GetInstanceId();
				if (_lastHowl.TryGetValue(id, out double last) && _clock - last < HowlRetrigger) break;
				_lastHowl[id] = _clock;
				Play(Sfx.First(HowlSound, HowlFallback), noise, mine);
				break;

			case NoiseKind.Shot:
				Play(ShotName(source, noise.Position, mine), noise, mine, reach: ShotReach);
				break;

			case NoiseKind.Explosion:
				Play("granada_explosao", noise, mine: false);
				break;
		}
	}

	/// <summary>O som do chão que está debaixo de quem pisou:
	/// <c>passo_neve</c>, <c>passo_metal</c>... Sem arquivo para a
	/// superfície (ou chão sem metadado), o som padrão.</summary>
	private static string OnSurface(string name, Noise noise)
	{
		string surface = Surfaces.Under(noise.Position, noise.Source);
		return surface == "" ? name : Sfx.First($"{name}_{surface}", name);
	}

	/// <summary>Cada arma tem a voz dela: <c>tiro_ak47</c>,
	/// <c>tiro_m3</c>... Sem arquivo da arma, cai no <c>tiro</c>.
	/// Longe, prefere <c>tiro_distante</c>: tiro distante é abafado,
	/// não só mais baixo.</summary>
	private string ShotName(Node source, Vector3 position, bool mine)
	{
		string id = source?.GetNodeOrNull<Weapon>("Weapon")?.Held?.Id ?? "";
		// Uma skin (ar15_ultimate) tem a voz da arma dela (ar15).
		string model = Sfx.ModelOf(id);

		if (!mine && Listener(out Vector3 ear) && ear.DistanceTo(position) > DistantShot)
		{
			string far = Sfx.First($"tiro_distante_{id}", $"tiro_distante_{model}", "tiro_distante");
			if (far != null) return far;
		}

		// O seu tiro é outra gravação: estéreo e com mais grave. Quem
		// atira ouve o corpo da arma; quem está na frente, o estalo.
		return mine
			? Sfx.First($"tiro_proprio_{id}", $"tiro_proprio_{model}", $"tiro_{id}", $"tiro_{model}", "tiro")
			: Sfx.First($"tiro_{id}", $"tiro_{model}", "tiro");
	}

	private void Play(string name, Noise noise, bool mine, float volumeDb = 0f, float reach = 0f)
	{
		if (Debug)
			GD.Print($"[som] {noise.Kind} de {noise.Source?.Name} ({(mine ? "seu" : $"raio {noise.Radius:0} m")}) → "
				+ (name != null && Sfx.Has(name) ? name : $"SEM ARQUIVO ({name})"));

		if (name == null) return;

		if (mine) Sfx.Own(name, volumeDb);
		else Sfx.At(name, noise.Position, noise.Radius, volumeDb, reach);
	}

	private bool Listener(out Vector3 position)
	{
		Camera3D camera = GetViewport()?.GetCamera3D();
		position = camera?.GlobalPosition ?? Vector3.Zero;
		return camera != null;
	}

	/// <summary>Confirmação de abate: o som que diz "foi seu" sem
	/// precisar olhar o feed.</summary>
	private void OnKill(Kill kill)
	{
		if (_localHealth != null && kill.Killer == _localHealth.PublicName)
			Sfx.Local("abate");
	}

	/// <summary>A música de tensão entra quando falta isto da partida, em
	/// segundos — só em partida que durou mais que isso e um pouco.</summary>
	[Export] public float TensionTime = 60f;

	/// <summary>A música do fim também entra quando um time chega a esta
	/// fração da pontuação da vitória (0,1 = faltando 10% dos pontos).</summary>
	[Export] public float TensionScore = 0.1f;

	/// <summary>Volume da música de tensão, em dB: por baixo do tiro.</summary>
	[Export] public float TensionDb = -5f;

	private AudioStreamPlayer _tension;

	/// <summary>A música de início (<c>partida_inicio</c>): entra quando a
	/// tela de carregamento sai, toca durante a contagem e acaba com ela
	/// (pedido do Jhon, 18/11). Era tocada na largada, e seus 13 s caíam em
	/// cima dos primeiros tiros.</summary>
	private AudioStreamPlayer _intro;
	private bool _introPlayed;

	/// <summary>Volume da música de início, em dB.</summary>
	[Export] public float IntroDb = -2f;

	private void UpdateIntro(Match match)
	{
		if (match.Phase == MatchPhase.Countdown)
		{
			if (_introPlayed || LoadingScreen.Busy || match.CountdownLeft < 1.5) return;
			_introPlayed = true;
			_intro = Sfx.Once(this, "partida_inicio", IntroDb);
			return;
		}

		// Acabou a contagem: a música acaba junto, num fade curto.
		if (_intro == null || !IsInstanceValid(_intro)) return;
		AudioStreamPlayer ending = _intro;
		_intro = null;
		Tween fade = ending.CreateTween();
		fade.TweenProperty(ending, "volume_db", -60f, 0.8);
		fade.TweenCallback(Callable.From(ending.QueueFree));
	}

	/// <summary>
	/// O último minuto tem música (a única dentro da partida): entra em 3 s
	/// e sai em 1,5 s no fim, antes da vinheta de vitória ou derrota.
	/// </summary>
	private void UpdateTension(Match match)
	{
		// Pelo relógio (o último minuto) ou pelo placar: um time a
		// TensionScore da vitória. Sem o placar, a partida que acabava por
		// pontos nunca tocava a música (relato do Jhon, 18/11: acabou em
		// 100 pontos com 11 minutos no relógio, em silêncio).
		int lead = Mathf.Max(match.ScoreOf(Teams.Blue), match.ScoreOf(Teams.Orange));
		bool byScore = match.ScoreToWin >= 10 && lead >= match.ScoreToWin - Mathf.Max(3, Mathf.RoundToInt(match.ScoreToWin * TensionScore));
		bool byClock = match.Duration > TensionTime + 30f && match.TimeLeft <= TensionTime;
		bool wanted = match.Phase == MatchPhase.Live && (byClock || byScore);

		if (wanted && _tension == null)
		{
			_tension = Sfx.Music(this, "musica_fim_proximo", -40f);
			_tension?.CreateTween().TweenProperty(_tension, "volume_db", TensionDb, 3.0);
		}
		else if (!wanted && _tension != null && IsInstanceValid(_tension))
		{
			AudioStreamPlayer leaving = _tension;
			Tween fade = leaving.CreateTween();
			fade.TweenProperty(leaving, "volume_db", -60f, 1.5);
			fade.TweenCallback(Callable.From(leaving.QueueFree));
			_tension = null;
		}
	}

	private void UpdateMatch()
	{
		Match match = Match.Current;
		if (match == null) return;

		UpdateTension(match);
		UpdateIntro(match);

		// A narração (18/11): o locutor avisa o começo, conta 3, 2, 1,
		// manda lutar e diz o resultado. Cada fala só toca se o arquivo
		// existir (assets/audio/voz_*; docs/AUDIO.md).
		if (match.Phase == MatchPhase.Countdown)
		{
			int second = Mathf.CeilToInt((float)match.CountdownLeft);
			if (second != _countdownSecond)
			{
				if (_countdownSecond < 0 && second >= 5) Say("voz_prepare");
				if (second is >= 1 and <= 3)
				{
					Sfx.Local("contagem");
					Say($"voz_{second}");
				}
			}
			_countdownSecond = second;
		}

		if (match.Phase == _phase) return;
		_phase = match.Phase;

		if (_phase == MatchPhase.Live)
			Say("voz_lutar");
		else if (_phase == MatchPhase.Ended)
		{
			bool draw = match.Winner == 0;
			bool won = match.Winner == (_localHealth?.Team ?? 0);
			Sfx.Local(draw ? "empate" : won ? "vitoria" : "derrota");
			// A fala entra depois do acorde, não em cima dele.
			string voice = draw ? "voz_empate" : won ? "voz_vitoria" : "voz_derrota";
			GetTree().CreateTimer(1.1).Timeout += () => Say(voice);
		}
	}

	/// <summary>
	/// A voz da locutora: seca (não está no mundo), um pouco acima dos
	/// efeitos, e **uma fala por vez** (pedido do Jhon, 18/11).
	///
	/// - Fala nova corta a que está tocando: matou o terceiro enquanto ela
	///   ainda dizia "double kill", ela para e diz "triple kill". Antes as
	///   duas tocavam juntas e a segunda se perdia embaixo da primeira.
	/// - Falas pedidas quase juntas (<see cref="VoiceGather"/>) viram uma
	///   só, a última: três abates no mesmo tiro de escopeta ou na mesma
	///   granada chegam como 1, 2, 3 no mesmo quadro, e ela diz direto
	///   "triple kill".
	/// </summary>
	public static void Say(string voice)
	{
		if (string.IsNullOrEmpty(voice) || !Sfx.Has(voice)) return;
		if ((Engine.GetMainLoop() as SceneTree)?.Root is not Window root) return;

		bool waiting = _pendingVoice != null;
		_pendingVoice = voice;
		if (waiting) return;

		root.GetTree().CreateTimer(VoiceGather, true, false, true).Timeout += () =>
		{
			string say = _pendingVoice;
			_pendingVoice = null;
			if (say == null) return;

			if (_voice == null || !GodotObject.IsInstanceValid(_voice))
			{
				_voice = new AudioStreamPlayer { Name = "Locutora", Bus = Sfx.BusSfx, VolumeDb = VoiceDb };
				root.AddChild(_voice);
			}

			_voice.Stop();
			_voice.Stream = Sfx.Stream(say);
			_voice.Play();
		};
	}

	/// <summary>Janela em que falas seguidas viram uma só, em segundos.
	/// Curta o bastante para não atrasar a fala; longa o bastante para
	/// pegar abates do mesmo disparo.</summary>
	public const float VoiceGather = 0.12f;

	private static AudioStreamPlayer _voice;
	private static string _pendingVoice;

	/// <summary>Volume das falas do locutor, em dB.</summary>
	public const float VoiceDb = 1f;
}
