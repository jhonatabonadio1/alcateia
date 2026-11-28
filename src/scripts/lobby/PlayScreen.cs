using Godot;
using System;

/// <summary>
/// A tela JOGAR (Jhon, 27/11: "ao clicar, abre uma outra tela com 3
/// opções: jogo rápido, treinamento e salas"): três cartões altos, lado a
/// lado, cada um uma porta.
///
/// - **Jogo rápido** — a fila do servidor junta jogadores do mesmo nível em
///   dois times da mesma força, com modo e mapa sorteados (<see cref="MatchSearch"/>);
/// - **Treinamento** — uma sala só dele contra bots, sem servidor de
///   partida e sem valer XP;
/// - **Salas** — a lista de salas online, para escolher ou criar a sua.
///
/// **O treinamento ainda não abriu** (<see cref="TrainingOpen"/>): o cartão
/// aparece trancado, com o cadeado por cima. O jogo rápido abriu em 27/11.
///
/// Cada cartão tem a foto de um mapa ao fundo, o número, o nome em letra
/// de placa, uma linha do que é e, embaixo, um dado vivo (quantas salas
/// há agora). Com o mouse o cartão sobe, a foto acende e chega mais perto,
/// e o fio da cor dele cresce embaixo.
/// </summary>
public partial class PlayScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Play;

	/// <summary>O jogo rápido e o treinamento ainda não abriram (Jhon, 27/11:
	/// "ainda não estarão disponíveis, coloca um cadeado por cima"): os dois
	/// cartões aparecem trancados, com "em breve". Para abrir, é virar isto.</summary>
	public static readonly bool QuickOpen = true, TrainingOpen = false;

	private Label _quickInfo, _roomsInfo;

	public override void _Ready()
	{
		base._Ready();
		LobbyLink.Instance.Changed += UpdateInfo;
		// As salas que existem agora, para os cartões dizerem.
		_ = Ask();
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (LobbyLink.Instance is LobbyLink link && IsInstanceValid(link)) link.Changed -= UpdateInfo;
	}

	private async System.Threading.Tasks.Task Ask()
	{
		await Shell.EnsureLink();
		if (IsInstanceValid(this) && IsInsideTree()) LobbyLink.Instance.RefreshRooms();
	}

	protected override void Build()
	{
		VBoxContainer page = Page(UiTokens.S4);

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(Deco.Title("Jogar", "Escolha como entrar na caçada", 56));
		head.AddChild(UiKit.Spacer());
		page.AddChild(head);

		HBoxContainer cards = UiKit.Row(UiTokens.S4);
		cards.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(cards);

		cards.AddChild(Card(1, "Jogo rápido", "O servidor junta jogadores do seu nível em dois times da mesma força e sorteia o modo e o mapa. É entrar e caçar.",
			"res://src/assets/ui/mapas/neon_foto.png", IconKind.Play, UiTokens.Accent, out _quickInfo,
			() => Shell.Search.Begin(), locked: !QuickOpen));
		cards.AddChild(Card(2, "Treinamento", "Uma sala só sua, contra bots do nível que você escolher. Para aquecer a mira e a garra.",
			"res://src/assets/ui/mapas/armazem_estendido_foto.png", IconKind.Bot, UiTokens.TeamBlue, out Label training,
			() => Shell.GoTraining(), locked: !TrainingOpen));
		cards.AddChild(Card(3, "Salas", "Veja as salas abertas, entre na de um amigo ou crie a sua com o mapa e as regras que quiser.",
			"res://src/assets/ui/mapas/cidade_foto.png", IconKind.Rooms, UiTokens.Paper, out _roomsInfo,
			() => Shell.GoRooms()));

		training.Text = Loc.Upper(TrainingOpen ? "Offline · não vale XP nem Cash" : "Em breve");
		UpdateInfo();

		Motion.Rise(head, 0f, new Vector2(-24, 0));
		Motion.Cascade(cards, 0.06f, 0.08f, new Vector2(0, 44));
	}

	private void UpdateInfo()
	{
		if (_quickInfo == null || !IsInstanceValid(_quickInfo)) return;
		LobbyLink lobby = LobbyLink.Instance;
		int rooms = lobby.Rooms.Count;
		_quickInfo.Text = Loc.Upper(!QuickOpen ? "Em breve" : Shell.Search.Searching ? "Procurando…"
			: !lobby.Connected ? "Conectando ao servidor…" : "Até 4 contra 4 · times equilibrados");
		_roomsInfo.Text = Loc.Upper(!lobby.Connected ? "Conectando ao servidor…" : Loc.F("{0} aberta(s) · crie a sua", rooms));
	}

	// ------------------------------------------------------------------ o cartão

	private static Control Card(int number, string title, string body, string picture, IconKind icon, Color color,
		out Label info, Action pressed, bool locked = false)
	{
		// Trancado: a mesma porta, apagada, com o cadeado por cima.
		if (locked) color = UiTokens.TextDisabled;

		// O cartão mora num espaço próprio: é o espaço que o container
		// arruma, e o cartão fica livre para subir com o mouse.
		var slot = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };

		var card = new Button
		{
			ClipContents = true,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand
		};
		UiKit.Fill(card);
		var idle = UiTheme.Box(UiTokens.Bg, UiTokens.Border, 1, 0, 0, 0);
		var hot = UiTheme.Box(UiTokens.Bg, new Color(color, 0.9f), 1, 0, 0, 0);
		foreach (string state in new[] { "normal", "disabled" }) card.AddThemeStyleboxOverride(state, idle);
		foreach (string state in new[] { "hover", "pressed", "hover_pressed" }) card.AddThemeStyleboxOverride(state, hot);
		if (locked)
		{
			card.Disabled = true;
			card.FocusMode = FocusModeEnum.None;
			card.MouseDefaultCursorShape = CursorShape.Arrow;
			card.TooltipText = Loc.T("Em breve");
		}
		else
		{
			card.Pressed += pressed;
			Sfx.Wire(card);
		}
		Deco.Ticks(card, new Color(UiTokens.Paper, 0.7f), 14f);
		slot.AddChild(card);

		Control photo = UiKit.Fill(Deco.Photo(picture, 0.98f, fromLeft: false, drift: 0f));
		card.AddChild(photo);
		TextureRect image = photo.GetNode<TextureRect>("Imagem");
		Color dim = locked ? new Color(0.2f, 0.22f, 0.26f) : new Color(0.5f, 0.54f, 0.6f);
		image.Modulate = dim;

		// O número grande, no alto, quase transparente: a ordem das portas.
		Label index = UiKit.Text($"{number:00}", "Display", new Color(UiTokens.Paper, 0.16f));
		index.AddThemeFontOverride("font", UiFonts.NumericDisplay);
		index.AddThemeFontSizeOverride("font_size", 150);
		index.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
		index.GrowHorizontal = GrowDirection.Begin;
		index.OffsetRight = -UiTokens.S4;
		index.OffsetTop = -UiTokens.S2;
		card.AddChild(index);

		VBoxContainer text = UiKit.Column(UiTokens.S2);
		text.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		text.GrowVertical = GrowDirection.Begin;
		text.OffsetLeft = UiTokens.S5;
		text.OffsetRight = -UiTokens.S5;
		text.OffsetBottom = -UiTokens.S5;
		text.MouseFilter = MouseFilterEnum.Ignore;
		card.AddChild(text);

		Icon glyph = Icon.Make(icon, 40, color);
		glyph.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		text.AddChild(glyph);
		text.AddChild(Deco.Big(title, 62));
		Label line = UiKit.Paragraph(body, null);
		line.AddThemeFontSizeOverride("font_size", 16);
		line.AddThemeColorOverride("font_color", new Color(UiTokens.TextPrimary, 0.82f));
		line.CustomMinimumSize = new Vector2(0, 66);
		text.AddChild(line);
		text.AddChild(Deco.Hairline(new Color(UiTokens.Paper, 0.3f)));
		info = UiKit.Text("", "Caption", color);
		info.AddThemeFontOverride("font", UiFonts.Tag);
		text.AddChild(info);

		// O fio da cor do cartão, embaixo: cresce da esquerda com o mouse.
		var bar = new ColorRect { Color = color, MouseFilter = MouseFilterEnum.Ignore };
		bar.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		bar.OffsetTop = -4;
		bar.Scale = new Vector2(0.12f, 1f);
		card.AddChild(bar);

		if (locked)
		{
			// O cadeado no meio do cartão, com a etiqueta embaixo.
			text.Modulate = new Color(1, 1, 1, 0.45f);
			index.Modulate = new Color(1, 1, 1, 0.5f);
			bar.Visible = false;
			var center = UiKit.Fill(new CenterContainer { MouseFilter = MouseFilterEnum.Ignore });
			center.OffsetBottom = -160;
			VBoxContainer mark = UiKit.Column(UiTokens.S3);
			mark.MouseFilter = MouseFilterEnum.Ignore;
			Icon lockIcon = Icon.Make(IconKind.Lock, 96, new Color(UiTokens.Paper, 0.92f));
			lockIcon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			mark.AddChild(lockIcon);
			PanelContainer soon = Deco.Tag("Em breve", UiTokens.Paper, UiTokens.PaperText);
			soon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			mark.AddChild(soon);
			center.AddChild(mark);
			card.AddChild(center);
			return slot;
		}

		slot.Resized += () => image.PivotOffset = slot.Size / 2f;
		void Hover(bool on)
		{
			Tween t = card.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			t.TweenProperty(card, "position:y", on ? -10f : 0f, 0.22f);
			t.TweenProperty(image, "modulate", on ? Colors.White : dim, 0.25f);
			t.TweenProperty(image, "scale", Vector2.One * (on ? 1.07f : 1f), 0.6f);
			t.TweenProperty(bar, "scale:x", on ? 1f : 0.12f, 0.3f);
			t.TweenProperty(index, "modulate:a", on ? 2.2f : 1f, 0.25f);
		}
		card.MouseEntered += () => Hover(true);
		card.MouseExited += () => Hover(false);
		card.FocusEntered += () => Hover(true);
		card.FocusExited += () => Hover(false);
		return slot;
	}
}
