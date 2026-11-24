using Godot;
using System;

/// <summary>
/// Os calls de rádio do time (pedido do Jhon, 18/11): Z abre a lista, de 1
/// a 10 (o 0 do teclado é o 10), e a fala escolhida toca **para todo o
/// time** de quem falou — e só para ele.
///
/// A voz é a do time: **azul, feminina** (<c>radio_f_1..10</c>);
/// **vermelho, masculina** (<c>radio_m_1..10</c>). Os arquivos saem de
/// <c>tools/sons/radio.py</c>.
///
/// Rádio não vem de um lugar: toca sem posição, no mesmo volume para todos
/// do time. Online, o call viaja pela partida (<see cref="NetWire.Radio"/>)
/// e cada máquina decide se ouve, pelo time do corpo de quem falou.
/// </summary>
public static class Radio
{
	/// <summary>As falas, na ordem das teclas (1…9, 0). Em inglês, como a voz.</summary>
	public static readonly string[] Calls =
	{
		"Enemy Spotted!", "Roger That!", "Follow Me!", "Cover Me!", "Go, Go, Go!",
		"Fall Back!", "Hold Position!", "Need Backup!", "Sector Clear!", "Nice Shot!"
	};

	/// <summary>Segundos entre um call e o próximo do mesmo jogador.</summary>
	public const double Cooldown = 1.2;

	/// <summary>Volume do call (dB).</summary>
	public static float VolumeDb = 1f;

	/// <summary>A lista está aberta: os números escolhem a fala, não a arma.</summary>
	public static bool MenuOpen { get; set; }

	private static ulong _numbersBusyUntil;

	/// <summary>Os números estão com o rádio (a lista aberta, ou a tecla
	/// que acabou de escolher a fala ainda no ar): não trocam de arma.</summary>
	public static bool HoldsNumbers => MenuOpen || Time.GetTicksMsec() < _numbersBusyUntil;

	/// <summary>A tecla que escolheu a fala não vale como troca de arma.</summary>
	public static void SwallowNumber() => _numbersBusyUntil = Time.GetTicksMsec() + 250;

	/// <summary>Um call que esta máquina ouviu: quem falou, o time e qual fala.</summary>
	public static event Action<string, int, int> Heard;

	private static double _lastSent = -10;

	public static string SoundOf(int team, int call) => $"radio_{(team == Teams.Orange ? "m" : "f")}_{call + 1}";

	/// <summary>O jogador desta máquina fala: toca aqui e vai para o time.
	/// Falso se ainda está no intervalo, morto ou fora da lista.</summary>
	public static bool Send(Node3D speaker, int call)
	{
		if (call < 0 || call >= Calls.Length || speaker == null) return false;
		if (speaker.GetNodeOrNull<Health>("Health") is not Health health || health.IsDead) return false;

		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastSent < Cooldown) return false;
		_lastSent = now;

		Play(speaker, health, call, mine: true);
		NetMatch.Current?.SendRadio(call);
		return true;
	}

	/// <summary>Um call chegou de outro corpo (pela rede, ou de um bot):
	/// toca se quem joga aqui é do mesmo time.</summary>
	public static void Receive(Node3D speaker, int call)
	{
		if (call < 0 || call >= Calls.Length || speaker == null) return;
		if (speaker.GetNodeOrNull<Health>("Health") is not Health health) return;
		if (health.Team != LocalTeam(speaker)) return;
		Play(speaker, health, call, mine: false);
	}

	private static int LocalTeam(Node from)
	{
		foreach (Node node in from.GetTree().GetNodesInGroup(Groups.Players))
			if (node is Player player && player.IsMultiplayerAuthority() && player.GetNodeOrNull<Health>("Health") is Health health)
				return health.Team;
		return RoomConfig.PlayerTeam;
	}

	/// <summary>Segundos com o ícone do rádio em cima de quem falou (e
	/// no minimapa).</summary>
	public const float TalkTime = 2.6f;

	private const string IconPath = "res://src/assets/ui/icones/radio.png";
	private const string IconName = "RadioIcone";

	private static readonly System.Collections.Generic.Dictionary<ulong, ulong> _talking = new();
	private static Texture2D _icon;

	/// <summary>Este corpo está falando no rádio agora? De 1 (acabou de
	/// falar) a 0 (não está). Para o minimapa.</summary>
	public static float Talking(Node body)
	{
		if (body == null || !_talking.TryGetValue(body.GetInstanceId(), out ulong until)) return 0f;
		ulong now = Time.GetTicksMsec();
		return now >= until ? 0f : (until - now) / (TalkTime * 1000f);
	}

	private static void Play(Node3D speaker, Health health, int call, bool mine)
	{
		Sfx.Ui(SoundOf(health.Team, call), VolumeDb);
		_talking[speaker.GetInstanceId()] = Time.GetTicksMsec() + (ulong)(TalkTime * 1000f);
		// Em cima de quem falou (o próprio jogador não se vê: só no minimapa).
		if (!mine) ShowIcon(speaker, health.Team);
		Heard?.Invoke(health.PublicName, health.Team, call);
	}

	/// <summary>
	/// O ícone do rádio flutuando em cima de quem falou (pedido do Jhon,
	/// 18/11: "ao dar rádio, aparece um ícone em cima do player que está
	/// falando, e no minimapa também"): sempre de frente para a câmera e
	/// visível através da parede — é um aliado, e o ponto é achar quem
	/// chamou. Entra com um pulo, fica, e some.
	/// </summary>
	private static void ShowIcon(Node3D speaker, int team)
	{
		speaker.GetNodeOrNull(IconName)?.Free();
		_icon ??= ResourceLoader.Exists(IconPath) ? GD.Load<Texture2D>(IconPath) : null;
		if (_icon == null) return;

		var icon = new Sprite3D
		{
			Name = IconName,
			Texture = _icon,
			PixelSize = 0.0042f,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			Shaded = false,
			FixedSize = false,
			RenderPriority = 10,
			Modulate = Teams.ColorOf(team).Lightened(0.55f),
			Position = new Vector3(0, 1.62f, 0),
			Scale = Vector3.One * 0.4f
		};
		speaker.AddChild(icon);

		Tween life = icon.CreateTween();
		life.TweenProperty(icon, "scale", Vector3.One * 1.15f, 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		life.TweenProperty(icon, "scale", Vector3.One, 0.1);
		life.TweenInterval(TalkTime - 0.6f);
		life.TweenProperty(icon, "modulate:a", 0f, 0.35);
		life.TweenCallback(Callable.From(icon.QueueFree));
	}

	/// <summary>Para os testes: zera o intervalo.</summary>
	public static void Reset() => _lastSent = -10;
}
