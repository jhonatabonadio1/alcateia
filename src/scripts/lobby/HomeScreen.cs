using Godot;
using System;

/// <summary>
/// A tela inicial (Jhon, 27/11: "a tela inicial ao logar não será mais
/// salas, e sim uma área de banners e notícias"): o que há de novo, antes
/// de qualquer lista.
///
/// - **O banner grande**, que troca sozinho a cada <see cref="BannerSeconds"/>
///   s (as marcas embaixo enchem com o tempo; as setas e as marcas trocam na
///   hora): a foto de um mapa andando devagar, a etiqueta, o título em
///   letra de placa e o botão que leva aonde o banner aponta.
/// - **As notícias curtas**, três cartões embaixo dele.
/// - **À direita**, a carreira (nível e XP até o próximo) e as missões do
///   dia.
///
/// Cabe numa tela, sem rolar. O conteúdo vem de <see cref="News"/>, que o
/// painel publica (a tela se refaz quando ele muda); o aviso do painel
/// (<see cref="ServerData.Notice"/>) entra numa faixa no alto.
/// </summary>
public partial class HomeScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Home;

	protected override bool FollowsAccount => true;

	/// <summary>Quanto tempo cada banner fica, em segundos.</summary>
	private const double BannerSeconds = 7.0;

	private int _index;
	private double _held;
	private bool _shown;
	private Control _stage;
	private Control[] _marks;
	private Label _counter;
	private News.Banner[] _banners = System.Array.Empty<News.Banner>();

	protected override void Build()
	{
		HBoxContainer split = SplitPage(UiTokens.S4);

		VBoxContainer main = UiKit.Column(UiTokens.S3);
		main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		split.AddChild(main);

		if (ServerData.Notice.Length > 0) main.AddChild(NoticeStrip(ServerData.Notice));
		Control banner = BannerArea();
		main.AddChild(banner);
		HBoxContainer stories = Stories();
		main.AddChild(stories);

		VBoxContainer side = UiKit.Column(UiTokens.S3);
		side.CustomMinimumSize = new Vector2(380, 0);
		split.AddChild(side);
		side.AddChild(Career());
		side.AddChild(DailyMissions.Card(Shell));

		// A entrada em cascata só na primeira vez: a tela se refaz quando a
		// conta muda, e não é para piscar de novo.
		if (_shown) return;
		_shown = true;
		Motion.Rise(banner, 0.02f, new Vector2(0, 28), 0.5f);
		Motion.Cascade(stories, 0.18f, 0.07f);
		Motion.Cascade(side, 0.1f, 0.07f, new Vector2(28, 0));
	}

	// ------------------------------------------------------------------ o aviso do painel

	private static Control NoticeStrip(string text)
	{
		var strip = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = new Color(UiTokens.Accent, 0.13f), BorderColor = UiTokens.Accent };
		box.BorderWidthLeft = 3;
		box.ContentMarginLeft = UiTokens.S3;
		box.ContentMarginRight = UiTokens.S3;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		strip.AddThemeStyleboxOverride("panel", box);
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		strip.AddChild(row);
		row.AddChild(Deco.Tag("Aviso", UiTokens.Accent, UiTokens.AccentText));
		row.AddChild(UiKit.Truncated(text, null, UiTokens.TextPrimary));
		return strip;
	}

	// ------------------------------------------------------------------ o banner

	private Control BannerArea()
	{
		var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
		frame.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Bg, UiTokens.Border, 1, 0, 0, 0));
		Deco.Ticks(frame, new Color(UiTokens.Paper, 0.7f), 14f);

		_stage = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
		frame.AddChild(_stage);

		// Por cima do banner: as setas, o contador e as marcas do tempo.
		var chrome = new Control { MouseFilter = MouseFilterEnum.Ignore };
		frame.AddChild(chrome);

		HBoxContainer foot = UiKit.Row(UiTokens.S2);
		foot.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		foot.GrowHorizontal = GrowDirection.Begin;
		foot.GrowVertical = GrowDirection.Begin;
		foot.OffsetRight = -UiTokens.S4;
		foot.OffsetBottom = -UiTokens.S4;
		chrome.AddChild(foot);

		_banners = News.Banners;
		_index = Mathf.Clamp(_index, 0, _banners.Length - 1);
		_marks = new Control[_banners.Length];
		for (int i = 0; i < _marks.Length; i++)
		{
			int index = i;
			var mark = new Button
			{
				Flat = true,
				CustomMinimumSize = new Vector2(54, 22),
				FocusMode = FocusModeEnum.None,
				MouseDefaultCursorShape = CursorShape.PointingHand,
				TooltipText = Loc.T(_banners[i].Title)
			};
			mark.Pressed += () => Show(index, byHand: true);
			mark.Draw += () =>
			{
				// O trilho, e por cima quanto do tempo deste banner já passou.
				var rail = new Rect2(0, 9, mark.Size.X, 3);
				mark.DrawRect(rail, new Color(UiTokens.Paper, 0.25f));
				float fill = index < _index ? 1f : index == _index ? (float)Mathf.Clamp(_held / BannerSeconds, 0, 1) : 0f;
				if (fill > 0f) mark.DrawRect(new Rect2(rail.Position, new Vector2(rail.Size.X * fill, rail.Size.Y)), index == _index ? UiTokens.Accent : UiTokens.Paper);
			};
			_marks[i] = mark;
			foot.AddChild(mark);
		}

		_counter = UiKit.Text("", "Numeric", UiTokens.TextSecondary);
		_counter.AddThemeFontOverride("font", UiFonts.NumericDisplay);
		_counter.AddThemeFontSizeOverride("font_size", 20);
		_counter.CustomMinimumSize = new Vector2(64, 0);
		_counter.HorizontalAlignment = HorizontalAlignment.Right;
		foot.AddChild(_counter);
		foot.AddChild(Arrow(IconKind.Back, "Banner anterior", -1));
		foot.AddChild(Arrow(IconKind.Go, "Próximo banner", +1));

		Show(_index, byHand: false, instant: true);
		return frame;
	}

	private Button Arrow(IconKind icon, string tooltip, int step)
	{
		Button button = UiKit.IconButton(icon, tooltip, () => Show(_index + step, byHand: true), 16);
		button.CustomMinimumSize = new Vector2(34, 34);
		button.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0, 0, 0, 0.35f), new Color(UiTokens.Paper, 0.3f), 1, 0, 0, 0));
		return button;
	}

	public override void _Process(double delta)
	{
		if (_marks == null || _stage == null || !IsInstanceValid(_stage)) return;
		_held += delta;
		if (_held >= BannerSeconds) Show(_index + 1, byHand: false);
		if (IsInstanceValid(_marks[_index])) _marks[_index].QueueRedraw();
	}

	/// <summary>Troca o banner: o de antes some, o novo entra com a foto
	/// vindo de lado e o texto em cascata.</summary>
	private void Show(int index, bool byHand, bool instant = false)
	{
		int count = _banners.Length;
		_index = ((index % count) + count) % count;
		_held = 0;
		if (byHand) Sfx.Ui("ui_passar");

		foreach (Node old in _stage.GetChildren())
		{
			if (old is not Control gone) continue;
			if (instant) { gone.QueueFree(); continue; }
			Tween fade = gone.CreateTween();
			fade.TweenProperty(gone, "modulate:a", 0f, 0.35f);
			fade.TweenCallback(Callable.From(gone.QueueFree));
		}

		Control slide = Slide(_banners[_index], instant);
		_stage.AddChild(slide);
		if (!instant) Motion.FadeIn(slide, 0.45f);

		_counter.Text = $"{_index + 1:00} / {count:00}";
		foreach (Control mark in _marks) mark.QueueRedraw();
	}

	private Control Slide(News.Banner banner, bool instant)
	{
		Control slide = UiKit.Fill(new Control { MouseFilter = MouseFilterEnum.Ignore });

		Control photo = UiKit.Fill(Deco.Photo(News.Picture(banner.Image), 0.96f, fromLeft: true, drift: 0.07f));
		slide.AddChild(photo);
		// O chão do banner também escurece: é onde ficam as marcas.
		Control floor = UiKit.Fill(Deco.Photo(null, 0.7f, fromLeft: false, drift: 0f));
		slide.AddChild(floor);

		VBoxContainer text = UiKit.Column(UiTokens.S2);
		text.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
		text.GrowVertical = GrowDirection.Begin;
		text.OffsetLeft = UiTokens.S6;
		text.OffsetBottom = -UiTokens.S6;
		text.CustomMinimumSize = new Vector2(600, 0);
		text.MouseFilter = MouseFilterEnum.Ignore;
		slide.AddChild(text);

		if (banner.Tag.Length > 0) text.AddChild(Deco.Tag(banner.Tag, UiTokens.Accent, UiTokens.AccentText));
		Label title = Deco.Big(banner.Title, 68);
		title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		title.AddThemeConstantOverride("line_spacing", -10);
		text.AddChild(title);
		Label body = UiKit.Paragraph(banner.Body, null);
		body.AddThemeFontSizeOverride("font_size", 17);
		body.AddThemeColorOverride("font_color", new Color(UiTokens.TextPrimary, 0.86f));
		body.CustomMinimumSize = new Vector2(520, 0);
		body.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		text.AddChild(body);
		text.AddChild(UiKit.Gap(UiTokens.S2));

		// Banner sem botão (só o anúncio) é permitido.
		News.Target target = banner.Go;
		if (target != News.Target.None && banner.Cta.Length > 0)
		{
			CtaButton go = CtaButton.Make(banner.Cta, CtaKind.Paper, () => Follow(target), 50, 20);
			go.Arrow = true;
			go.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			text.AddChild(go);
		}

		if (!instant) Motion.Cascade(text, 0.08f, 0.06f, new Vector2(-30, 0));
		return slide;
	}

	private void Follow(News.Target target)
	{
		switch (target)
		{
			case News.Target.Play: Shell.GoPlay(); break;
			case News.Target.Shop: Shell.GoShop(); break;
			case News.Target.Inventory: Shell.GoInventory(); break;
			case News.Target.Missions: Shell.GoMissions(); break;
		}
	}

	// ------------------------------------------------------------------ as notícias

	private static HBoxContainer Stories()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		row.CustomMinimumSize = new Vector2(0, 176);
		foreach (News.Story story in News.Stories) row.AddChild(StoryCard(story));
		return row;
	}

	private static Control StoryCard(News.Story story)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
		var idle = UiTheme.Box(UiTokens.Bg, UiTokens.Border, 1, 0, 0, 0);
		var hot = UiTheme.Box(UiTokens.Bg, new Color(UiTokens.Paper, 0.7f), 1, 0, 0, 0);
		card.AddThemeStyleboxOverride("panel", idle);

		Control photo = Deco.Photo(News.Picture(story.Image), 0.97f, fromLeft: false, drift: 0f);
		card.AddChild(photo);
		TextureRect image = photo.GetNode<TextureRect>("Imagem");
		image.Modulate = new Color(0.62f, 0.66f, 0.72f);

		MarginContainer pad = UiKit.Margin(UiTokens.S3);
		pad.MouseFilter = MouseFilterEnum.Ignore;
		card.AddChild(pad);
		VBoxContainer column = UiKit.Column(UiTokens.S1);
		column.Alignment = BoxContainer.AlignmentMode.End;
		column.MouseFilter = MouseFilterEnum.Ignore;
		pad.AddChild(column);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		if (story.Tag.Length > 0) head.AddChild(Deco.Tag(story.Tag));
		Label date = UiKit.Text(story.Date, "Numeric", UiTokens.TextSecondary);
		date.AddThemeFontSizeOverride("font_size", 12);
		head.AddChild(date);
		column.AddChild(head);
		Label title = UiKit.Text(Loc.T(story.Title), "Subtitle");
		title.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		title.AddThemeFontSizeOverride("font_size", 24);
		column.AddChild(title);
		Label body = UiKit.Paragraph(story.Body, "Caption");
		body.AddThemeColorOverride("font_color", new Color(UiTokens.TextPrimary, 0.74f));
		// Três linhas sempre: as etiquetas e os títulos dos três cartões ficam na mesma altura.
		body.CustomMinimumSize = new Vector2(0, 54);
		body.MaxLinesVisible = 3;
		column.AddChild(body);

		// Com o mouse: a foto acende e chega mais perto.
		card.Resized += () => image.PivotOffset = card.Size / 2f;
		card.MouseEntered += () =>
		{
			card.AddThemeStyleboxOverride("panel", hot);
			Tween t = image.CreateTween().SetParallel();
			t.TweenProperty(image, "modulate", Colors.White, 0.25f);
			t.TweenProperty(image, "scale", Vector2.One * 1.06f, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		};
		card.MouseExited += () =>
		{
			card.AddThemeStyleboxOverride("panel", idle);
			Tween t = image.CreateTween().SetParallel();
			t.TweenProperty(image, "modulate", new Color(0.62f, 0.66f, 0.72f), 0.3f);
			t.TweenProperty(image, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		};
		return card;
	}

	// ------------------------------------------------------------------ a coluna da direita

	/// <summary>A carreira: o nível agora, a barra e quanto falta.</summary>
	private Control Career()
	{
		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);
		column.AddChild(Deco.Kicker("Carreira"));

		int rank = Profile.Rank;
		bool max = rank >= Ranks.Count - 1;
		long into = Profile.RankXp - Ranks.XpFor(rank);
		long span = Ranks.XpFor(rank + 1) - Ranks.XpFor(rank);

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		column.AddChild(row);
		// A placa do nível passa um pouco da caixa: folga embaixo.
		var portrait = new MarginContainer();
		portrait.AddThemeConstantOverride("margin_bottom", 10);
		portrait.AddChild(RankBadge.Make(rank, 76, avatar: Avatars.Selected));
		row.AddChild(portrait);

		VBoxContainer text = UiKit.Column(UiTokens.S1);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(text);
		Label name = UiKit.Truncated(Profile.PlayerName, "Subtitle");
		name.AddThemeFontOverride("font", UiFonts.Display);
		name.AddThemeFontSizeOverride("font_size", 30);
		text.AddChild(name);
		HBoxContainer level = UiKit.Row(UiTokens.S2);
		level.AddChild(LevelChip.Make(rank, 20));
		level.AddChild(UiKit.Text(Ranks.NameOf(rank), "Caption", Ranks.InkOf(rank)));
		text.AddChild(level);

		var bar = new ProgressBar { CustomMinimumSize = new Vector2(0, 6), MaxValue = 1, Step = 0.001, Value = 0, ShowPercentage = false };
		bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(UiTokens.Paper, 0.12f) });
		bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = UiTokens.Accent });
		column.AddChild(bar);
		// A barra enche até onde está: o progresso se lê melhor andando.
		bar.CreateTween().TweenProperty(bar, "value", Ranks.Progress(Profile.RankXp), 0.9f)
			.SetDelay(0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		HBoxContainer foot = UiKit.Row(UiTokens.S2);
		foot.AddChild(UiKit.Text(max ? Loc.T("Patente máxima") : Loc.F("{0} / {1} XP", UiTokens.Thousands(into), UiTokens.Thousands(span)), "Numeric", UiTokens.TextSecondary));
		foot.AddChild(UiKit.Spacer());
		Button profile = UiKit.Button("Ver perfil", ButtonKind.Tertiary, () => Shell.GoProfile());
		profile.CustomMinimumSize = new Vector2(0, 28);
		foot.AddChild(profile);
		column.AddChild(foot);
		return card;
	}
}
