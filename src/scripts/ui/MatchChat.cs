using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O chat da partida online (Jhon, 27/11: "chat em partida, chat na sala"),
/// no canto esquerdo, abaixo do minimapa: Enter abre para todos, T só para o
/// time (as duas se trocam nas Opções → Controles); Enter manda, Esc fecha e
/// Tab troca entre todos e o time sem fechar.
///
/// **É o chat da sala.** A sala continua aberta durante a partida, e a
/// ligação do saguão também (<see cref="LobbyLink"/>): o que se fala aqui
/// aparece na tela da sala, para quem está nela de fora da partida, e
/// continua lá na volta. O servidor de partida não sabe de chat — ele só
/// leva o que é do jogo (decisão 0011). O do time vai com <c>team</c>, e o
/// saguão entrega só para o time de quem falou.
///
/// **Digitando, o jogo não ouve o teclado** (<see cref="Typing"/>): a
/// intenção do jogador sai vazia (<see cref="CharacterIntent.FromInput"/>),
/// o mouse não vira a câmera, e o rádio, o guia, o placar e o menu esperam.
///
/// As linhas somem sozinhas depois de <see cref="LineSeconds"/>; com o campo
/// aberto, as últimas voltam todas. As mensagens diretas dos amigos
/// (<see cref="DirectChat"/>) também aparecem aqui, marcadas.
///
/// Camada própria (a do feed), acima da escolha de conjunto: morto,
/// esperando para renascer, ainda se lê e se fala.
/// </summary>
public partial class MatchChat : CanvasLayer
{
	/// <summary>Quanto tempo cada linha fica na tela, em segundos.</summary>
	[Export] public float LineSeconds = 9f;

	/// <summary>Quanto leva para sumir, no fim.</summary>
	[Export] public float FadeSeconds = 1f;

	/// <summary>Quantas linhas aparecem com o campo fechado e aberto.</summary>
	[Export] public int ShownLines = 6;
	[Export] public int TypingLines = 12;

	private const int Width = 460;
	private const int Kept = 40;

	/// <summary>Há alguém digitando no chat: o jogo não lê o teclado.</summary>
	public static bool Typing { get; private set; }

	private VBoxContainer _lines;
	private PanelContainer _inputPlate;
	private Label _channel;
	private LineEdit _field;
	private bool _team;
	private readonly List<(RichTextLabel Node, double Left)> _shown = new();

	/// <summary>As linhas na tela, em texto. Para os testes.</summary>
	public IEnumerable<string> Lines => _shown.Select(l => l.Node.GetParsedText());

	public override void _Ready()
	{
		Layer = Hud.FeedLayer;
		ProcessMode = ProcessModeEnum.Always;

		Control root = UiTheme.Apply(UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore }));
		AddChild(root);

		// Abaixo do minimapa (o lado dele, o nome do mapa e uma folga).
		VBoxContainer box = UiKit.Column(UiTokens.S1);
		box.MouseFilter = Control.MouseFilterEnum.Ignore;
		box.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		box.Position = new Vector2(UiTokens.S5, UiTokens.S5 + 240 + 48);
		box.CustomMinimumSize = new Vector2(Width, 0);
		root.AddChild(box);

		_lines = UiKit.Column(2);
		_lines.MouseFilter = Control.MouseFilterEnum.Ignore;
		box.AddChild(_lines);

		_inputPlate = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_inputPlate.AddThemeStyleboxOverride("panel", HudStyle.Plate(UiTokens.Accent, skewed: false, padH: UiTokens.S2, padV: 2, accentWidth: 2));
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		_inputPlate.AddChild(row);
		_channel = HudStyle.Outlined(UiKit.Caption("", UiTokens.Accent));
		_channel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(_channel);
		_field = UiKit.Field("", "", LobbyLink.ChatMax);
		_field.ContextMenuEnabled = false;
		_field.TextSubmitted += Submit;
		row.AddChild(_field);
		box.AddChild(_inputPlate);

		LobbyLink.Instance.ChatReceived += OnChat;
		LobbyLink.Instance.Failed += OnFailed;
		DirectChat.Received += OnDirect;
	}

	public override void _ExitTree()
	{
		Typing = false;
		DirectChat.Received -= OnDirect;
		if (LobbyLink.Instance is LobbyLink link && IsInstanceValid(link))
		{
			link.ChatReceived -= OnChat;
			link.Failed -= OnFailed;
		}
	}

	/// <summary>Dá para abrir agora: online, na sala, sem menu aberto, a partida valendo.</summary>
	private static bool CanOpen =>
		LobbyLink.InRoom && LobbyLink.Instance.Connected && !UiState.AnyOpen && !Match.IsOver;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Typing || @event is not InputEventKey { Pressed: true, Echo: false }) return;
		bool all = @event.IsActionPressed("chat_all"), team = @event.IsActionPressed("chat_team");
		if (!(all || team) || !CanOpen) return;
		GetViewport().SetInputAsHandled();
		Open(team);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Typing || @event is not InputEventKey { Pressed: true } key) return;
		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
		if (code == Key.Escape)
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
		else if (code == Key.Tab)
		{
			GetViewport().SetInputAsHandled();
			_team = !_team;
			ShowChannel();
		}
	}

	/// <summary>Abre o campo, para todos ou para o time.</summary>
	public void Open(bool team)
	{
		Typing = true;
		_team = team;
		ShowChannel();
		_field.Clear();
		_inputPlate.Visible = true;
		// Adiado: a tecla que abriu não vira letra no campo.
		Callable.From(() => { if (Typing && IsInstanceValid(_field)) _field.GrabFocus(); }).CallDeferred();
	}

	public void Close()
	{
		Typing = false;
		if (IsInstanceValid(_field)) _field.ReleaseFocus();
		_inputPlate.Visible = false;
	}

	private void ShowChannel()
	{
		_channel.Text = _team ? Loc.Upper("Time") : Loc.Upper("Todos");
		Color tint = _team ? Teams.ColorOf(Groups.LocalPlayer(GetTree())?.GetNodeOrNull<Health>("Health")?.Team ?? Teams.Blue).Lightened(0.3f) : UiTokens.Accent;
		_channel.AddThemeColorOverride("font_color", tint);
	}

	private void Submit(string text)
	{
		text = (text ?? "").Trim();
		if (text.Length > 0) LobbyLink.Instance.Say(text, _team);
		Close();
	}

	public override void _Process(double delta)
	{
		// Um menu abriu por cima (Esc, a escolha de conjunto) ou a partida acabou: o campo fecha.
		if (Typing && (UiState.AnyOpen || Match.IsOver || !LobbyLink.InRoom)) Close();

		int shown = Typing ? TypingLines : ShownLines;
		for (int i = 0; i < _shown.Count; i++)
		{
			(RichTextLabel node, double left) = _shown[i];
			left -= delta;
			_shown[i] = (node, left);
			bool recent = i >= _shown.Count - shown;
			float alpha = Typing ? 1f : Mathf.Clamp((float)(left / FadeSeconds), 0f, 1f);
			node.Visible = recent && alpha > 0f;
			node.Modulate = new Color(1, 1, 1, alpha);
		}
	}

	// ------------------------------------------------------------------ o que chega

	private void OnChat(LobbyLink.ChatLine line)
	{
		// Só o da sala desta partida (a ligação é a mesma do saguão).
		if (line.Room == 0 || line.Room != LobbyLink.Instance.RoomId) return;
		string name = Escape(line.From);
		string color = Teams.ColorOf(line.Side != 0 ? line.Side : Teams.Blue).Lightened(0.3f).ToHtml(false);
		string tag = line.Team ? $"[color=#{color}][lb]{Loc.Upper("Time")}][/color] " : "";
		Add($"{tag}[color=#{color}][b]{name}[/b][/color]: {Escape(line.Text)}");
	}

	private void OnDirect(DirectChat.Line line)
	{
		string from = Social.Friends.FirstOrDefault(f => f.Id == line.From)?.Name ?? "?";
		string color = UiTokens.Accent.ToHtml(false);
		Add($"[color=#{color}][b]{Escape(Loc.F("{0} (amigo)", from))}[/b][/color]: {Escape(line.Text)}");
	}

	private void OnFailed(string code, string message)
	{
		// O freio do chat (e o "fora de sala") aparece aqui; o resto é do saguão.
		if (code is not ("devagar" or "fora_de_sala")) return;
		Add($"[color=#{UiTokens.Warning.ToHtml(false)}]» {Escape(message)}[/color]");
	}

	private void Add(string bbcode)
	{
		var label = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
			Text = bbcode
		};
		label.AddThemeConstantOverride("outline_size", 5);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.75f));
		label.AddThemeColorOverride("default_color", UiTokens.TextPrimary);
		label.AddThemeFontSizeOverride("normal_font_size", 16);
		label.AddThemeFontSizeOverride("bold_font_size", 16);
		_lines.AddChild(label);
		_shown.Add((label, LineSeconds));

		while (_shown.Count > Kept)
		{
			_shown[0].Node.QueueFree();
			_shown.RemoveAt(0);
		}
	}

	private static string Escape(string text) => (text ?? "").Replace("[", "[lb]");
}
