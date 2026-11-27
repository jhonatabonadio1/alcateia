using Godot;
using System.Collections.Generic;

/// <summary>
/// A montagem do HUD, uma vez no _Ready: cada canto com o seu bloco
/// (docs/UI.md) e as peças repetidas (barra, texto com contorno).
///
/// Sem painel atrás (pedido do Jhon, 17/11, com Overwatch de
/// referência): vida, munição e transformação ficam direto sobre o jogo,
/// com contorno escuro no texto. Os dois blocos de baixo são levemente
/// inclinados para dentro (<see cref="TiltDegrees"/>).
/// </summary>
public partial class Hud
{
	/// <summary>Quanto os blocos de baixo (vida à esquerda, arma à
	/// direita) giram, em graus: a base sobe em direção ao meio da tela.</summary>
	private const float TiltDegrees = 3f;

	/// <summary>Onde começa a chamada da sequência, a partir do alto: logo
	/// abaixo da barra do placar (MatchHud).</summary>
	private const int StreakCallTop = 104;

	private T Corner<T>(T control, Control.LayoutPreset preset) where T : Control
	{
		control.SetAnchorsPreset(preset);
		control.GrowHorizontal = preset is Control.LayoutPreset.TopRight or Control.LayoutPreset.BottomRight
			? Control.GrowDirection.Begin
			: preset is Control.LayoutPreset.CenterBottom or Control.LayoutPreset.CenterTop ? Control.GrowDirection.Both
			: Control.GrowDirection.End;
		control.GrowVertical = preset is Control.LayoutPreset.BottomLeft or Control.LayoutPreset.BottomRight or Control.LayoutPreset.CenterBottom
			? Control.GrowDirection.Begin
			: Control.GrowDirection.End;
		control.MouseFilter = Control.MouseFilterEnum.Ignore;
		_root.AddChild(control);
		return control;
	}

	/// <summary>Gira um bloco em torno do canto de baixo
	/// (<paramref name="right"/>: o da direita). O pivô acompanha o
	/// tamanho, que muda com o conteúdo.</summary>
	private static void Tilt(Control control, float degrees, bool right)
	{
		void Pivot() => control.PivotOffset = new Vector2(right ? control.Size.X : 0, control.Size.Y);
		control.Rotation = Mathf.DegToRad(degrees);
		control.Resized += Pivot;
		Pivot();
	}

	private void BuildVignette()
	{
		// Vinheta radial: transparente no meio, vermelha nas bordas.
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0.7f, 0.02f, 0.02f, 0f));
		gradient.SetColor(1, new Color(0.7f, 0.02f, 0.02f, 0.85f));
		gradient.SetOffset(0, 0.55f);
		gradient.SetOffset(1, 1f);

		var texture = new GradientTexture2D
		{
			Gradient = gradient,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1.05f, 1.05f),
			Width = 256,
			Height = 256
		};

		_vignette = UiKit.Fill(new TextureRect
		{
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0)
		});
		_root.AddChild(_vignette);
	}

	private void BuildCenter()
	{
		_crosshair = UiKit.Fill(new Crosshair { Weapon = _weapon, Form = _form });
		_root.AddChild(_crosshair);

		_hitMarker = UiKit.Fill(new HitMarker());
		_root.AddChild(_hitMarker);

		_warning = UiKit.Text("", "Subtitle", UiTokens.Warning);
		_warning.HorizontalAlignment = HorizontalAlignment.Center;
		_warning.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_warning.AnchorTop = _warning.AnchorBottom = 0.62f;
		_warning.GrowHorizontal = Control.GrowDirection.Both;
		_warning.AddThemeConstantOverride("outline_size", 6);
		_warning.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
		_root.AddChild(_warning);

		// "ELIMINOU <jogador>", com a caveira, e a chamada da sequência
		// (DOUBLE KILL…): abaixo da mira, crescendo para cima a partir de
		// logo acima do disco do especial, para nunca cobrir ele.
		_killBanner = new KillBanner();
		_killBanner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_killBanner.AnchorTop = _killBanner.AnchorBottom = 0.77f;
		_killBanner.GrowHorizontal = Control.GrowDirection.Both;
		_killBanner.GrowVertical = Control.GrowDirection.Begin;
		_root.AddChild(_killBanner);

		// A chamada da sequência (DOUBLE KILL…), no alto, logo abaixo da
		// barra do placar (pedido do Jhon, 18/11).
		_streakCall = new KillBanner { Alignment = BoxContainer.AlignmentMode.Begin };
		_streakCall.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_streakCall.OffsetTop = StreakCallTop;
		_streakCall.GrowHorizontal = Control.GrowDirection.Both;
		_streakCall.GrowVertical = Control.GrowDirection.End;
		_root.AddChild(_streakCall);

		_death = UiKit.Column(UiTokens.S1);
		_death.Alignment = BoxContainer.AlignmentMode.Center;
		_death.SetAnchorsPreset(Control.LayoutPreset.Center);
		_death.AnchorTop = _death.AnchorBottom = 0.36f;
		_death.GrowHorizontal = Control.GrowDirection.Both;
		_death.GrowVertical = Control.GrowDirection.Both;
		_death.MouseFilter = Control.MouseFilterEnum.Ignore;
		_death.Visible = false;

		Label title = HudStyle.Number(UiKit.Text("ABATIDO", "Display"), 72, UiTokens.Danger);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		_death.AddChild(title);
		_deathBy = Outlined(UiKit.Text("", "Subtitle"));
		_deathBy.HorizontalAlignment = HorizontalAlignment.Center;
		_death.AddChild(_deathBy);
		_deathTimer = Outlined(UiKit.Text("", "Muted"));
		_deathTimer.HorizontalAlignment = HorizontalAlignment.Center;
		_death.AddChild(_deathTimer);
		_root.AddChild(_death);
	}

	private void BuildMinimap()
	{
		VBoxContainer box = Corner(UiKit.Column(UiTokens.S1), Control.LayoutPreset.TopLeft);
		box.Position = new Vector2(Edge, Edge);

		// O minimapa pinta o próprio fundo (chapado, sem borrar).
		var map = new Minimap { MouseFilter = Control.MouseFilterEnum.Ignore, Subject = Subject };
		map.Side = MinimapSide;
		box.AddChild(map);

		MapInfo info = Maps.Get(RoomConfig.Rules.Map);
		string mode = RoomSettings.ModeName(RoomConfig.Rules.Mode);
		Label where = Outlined(UiKit.Caption($"{Loc.T(info.Name)}  ·  {Loc.T(mode)}", UiTokens.TextPrimary));
		where.AddThemeFontOverride("font", HudStyle.Slanted(UiFonts.DisplaySemi));
		where.AddThemeFontSizeOverride("font_size", 16);
		box.AddChild(where);
	}

	/// <summary>
	/// O feed de quem joga mora numa camada própria, acima da escolha de
	/// conjunto (pedido do Jhon, 21/11: "quando eu morrer, o feed de kills
	/// deve continuar aparecendo, mesmo no modo espectador"). Antes ele ia
	/// junto com o HUD: morto, o HUD sai (a câmera de morte põe o de quem
	/// se vê) e a escolha de conjunto cobria o de quem se vê — o feed sumia
	/// os 15 s da escolha. O HUD de quem se vê não mostra o dele: seria o
	/// mesmo feed duas vezes.
	/// </summary>
	private CanvasLayer _feedLayer;

	/// <summary>Acima da escolha de conjunto (9) e do menu (10).</summary>
	public const int FeedLayer = 11;

	private void BuildFeed()
	{
		if (Subject == null)
		{
			_feedLayer = new CanvasLayer { Layer = FeedLayer, Name = "FeedDeAbates" };
			AddChild(_feedLayer);
			var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
			root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			_feedLayer.AddChild(root);
			_feed = UiKit.Column(UiTokens.S1);
			_feed.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			_feed.GrowHorizontal = Control.GrowDirection.Begin;
			_feed.MouseFilter = Control.MouseFilterEnum.Ignore;
			root.AddChild(_feed);
			_feed.Position = new Vector2(-Edge, Edge);
			_feed.CustomMinimumSize = new Vector2(440, 0);
			_feed.OffsetLeft = -Edge - 440;
			_feed.OffsetRight = -Edge;
			return;
		}

		_feed = Corner(UiKit.Column(UiTokens.S1), Control.LayoutPreset.TopRight);
		_feed.Visible = false;
		_feed.Position = new Vector2(-Edge, Edge);
		_feed.CustomMinimumSize = new Vector2(440, 0);
		_feed.OffsetLeft = -Edge - 440;
		_feed.OffsetRight = -Edge;
	}

	private void BuildVitals()
	{
		// Sem painel: a insígnia da forma, o número e os gomos, direto
		// sobre o jogo.
		HBoxContainer block = UiKit.Row(UiTokens.S3);
		Corner(block, Control.LayoutPreset.BottomLeft);
		block.OffsetLeft = Edge;
		block.OffsetBottom = -Edge;
		Tilt(block, -TiltDegrees, right: false);

		// Forma sempre visível, ao lado da vida. Parece redundante; em
		// partida rápida não é.
		_formBadge = new HexBadge { Edge = Teams.ColorOf(_health.Team), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd, Rank = Profile.Rank, Photo = Avatars.OfCharacter(_player?.Character, _health.Team) };
		_formIcon = _formBadge.Glyph;
		block.AddChild(_formBadge);

		VBoxContainer column = UiKit.Column(UiTokens.S1);
		column.CustomMinimumSize = new Vector2(330, 0);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;
		block.AddChild(column);

		_shieldRow = UiKit.Row(UiTokens.S2);
		_shieldBar = new SegmentBar
		{
			CustomMinimumSize = new Vector2(0, 11),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			Fill = UiTokens.WolfOrange.Lightened(0.15f),
			Trail = UiTokens.WolfOrange.Darkened(0.4f)
		};
		_shieldRow.AddChild(_shieldBar);
		_shieldNumber = HudStyle.Number(UiKit.Text("", "Numeric"), 20, UiTokens.WolfOrange.Lightened(0.25f));
		_shieldNumber.CustomMinimumSize = new Vector2(44, 0);
		_shieldNumber.HorizontalAlignment = HorizontalAlignment.Right;
		_shieldRow.AddChild(_shieldNumber);
		column.AddChild(_shieldRow);

		HBoxContainer hp = UiKit.Row(UiTokens.S2);
		_hpNumber = HudStyle.Number(UiKit.Text("200", "NumericLarge"), 60);
		hp.AddChild(_hpNumber);
		_hpMax = HudStyle.Number(UiKit.Text("/ 200", "Numeric"), 26, UiTokens.TextSecondary);
		_hpMax.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		hp.AddChild(_hpMax);
		column.AddChild(hp);

		_hpBar = new SegmentBar
		{
			CustomMinimumSize = new Vector2(0, 24),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		column.AddChild(_hpBar);

		_formLabel = Outlined(UiKit.Caption("Humano", UiTokens.TextPrimary));
		_formLabel.AddThemeFontOverride("font", HudStyle.Slanted(UiFonts.DisplaySemi));
		_formLabel.AddThemeFontSizeOverride("font_size", 16);
		column.AddChild(_formLabel);
	}

	private void BuildEquipment()
	{
		VBoxContainer side = Corner(UiKit.Column(UiTokens.S2), Control.LayoutPreset.BottomRight);
		side.OffsetRight = -Edge;
		side.OffsetBottom = -Edge;
		side.CustomMinimumSize = new Vector2(380, 0);
		side.OffsetLeft = -Edge - 380;
		Tilt(side, TiltDegrees, right: true);

		// O fôlego da corrida, em cima do KP (Hud.Breath).
		BuildBreath(side);

		// WP, perto da munição: é o que se gasta ao renascer.
		HBoxContainer wpRow = UiKit.Row(UiTokens.S2);
		wpRow.Alignment = BoxContainer.AlignmentMode.End;
		wpRow.AddChild(HudStyle.Number(UiKit.Caption("KP"), 18, UiTokens.Accent));
		_wp = HudStyle.Number(UiKit.Text("0", "NumericLarge"), 34, UiTokens.Accent);
		wpRow.AddChild(_wp);
		side.AddChild(wpRow);

		// Sem painel: arma, pente e reserva direto sobre o jogo.
		var stack = new Control { CustomMinimumSize = new Vector2(0, 150), MouseFilter = Control.MouseFilterEnum.Ignore };
		side.AddChild(stack);

		_human = UiKit.Fill(HumanPanel());
		stack.AddChild(_human);
		_wolf = UiKit.Fill(WolfPanel());
		stack.AddChild(_wolf);
	}

	private VBoxContainer HumanPanel()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S1);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;
		column.Alignment = BoxContainer.AlignmentMode.End;

		// Tudo encostado à direita: sem painel, o desenho da arma solto
		// na outra ponta do bloco ficava perdido.
		HBoxContainer top = UiKit.Row(UiTokens.S3);
		top.Alignment = BoxContainer.AlignmentMode.End;
		_weaponArt = WeaponArt.Make(Arsenal.DefaultPrimary, new Vector2(120, 44));
		_weaponArt.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		_weaponArt.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		top.AddChild(_weaponArt);

		VBoxContainer names = UiKit.Column(0);
		names.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		_weaponName = HudStyle.Number(UiKit.Text("", "Subtitle"), 22);
		_weaponName.HorizontalAlignment = HorizontalAlignment.Right;
		names.AddChild(_weaponName);
		_reloading = Outlined(UiKit.Caption("", UiTokens.Warning));
		_reloading.HorizontalAlignment = HorizontalAlignment.Right;
		names.AddChild(_reloading);
		top.AddChild(names);
		column.AddChild(top);

		HBoxContainer ammo = UiKit.Row(UiTokens.S2);
		ammo.Alignment = BoxContainer.AlignmentMode.End;

		_grenades = HudStyle.Number(UiKit.Text("", "Numeric"), 22, UiTokens.TextPrimary);
		HBoxContainer grenade = UiKit.Row(UiTokens.S1);
		grenade.AddChild(Icon.Make(IconKind.Grenade, 20, UiTokens.TextPrimary));
		grenade.AddChild(_grenades);
		grenade.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		ammo.AddChild(grenade);
		ammo.AddChild(UiKit.Gap(UiTokens.S3));

		_mag = HudStyle.Number(UiKit.Text("30", "NumericLarge"), 68);
		ammo.AddChild(_mag);
		// A reserva: o que sobra fora do pente.
		_reserve = HudStyle.Number(UiKit.Text("/ 90", "Subtitle"), 28, UiTokens.TextSecondary);
		_reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		ammo.AddChild(_reserve);
		column.AddChild(ammo);

		HBoxContainer slots = UiKit.Row(UiTokens.S1);
		slots.Alignment = BoxContainer.AlignmentMode.End;
		foreach (var (slot, label) in new[] { (Slot.Primary, "1"), (Slot.Secondary, "2"), (Slot.Wolf, "3"), (Slot.Knife, "4"), (Slot.Grenade, "5") })
		{
			var chip = new PanelContainer { CustomMinimumSize = new Vector2(28, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
			Label text = HudStyle.Number(UiKit.Text(label, "Numeric"), 15);
			text.HorizontalAlignment = HorizontalAlignment.Center;
			chip.AddChild(text);
			_slots[slot] = chip;
			slots.AddChild(chip);
		}
		column.AddChild(slots);
		return column;
	}

	private VBoxContainer WolfPanel()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;
		column.Alignment = BoxContainer.AlignmentMode.End;

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(Icon.Make(IconKind.Wolf, 24, UiTokens.WolfOrange));
		_breedName = HudStyle.Number(UiKit.Text("", "Subtitle"), 26);
		head.AddChild(_breedName);
		head.AddChild(UiKit.Spacer());
		column.AddChild(head);

		// As habilidades em quadradinhos, à direita (18/11, "igual
		// Overwatch"): a garrada, o bote e o uivo — que estava escondido
		// numa linha de texto e muita gente não sabia que existia. O
		// especial (Q) continua no disco do meio. O que cada uma faz está
		// no guia (segurar E).
		HBoxContainer squares = UiKit.Row(UiTokens.S2);
		squares.MouseFilter = Control.MouseFilterEnum.Ignore;
		squares.Alignment = BoxContainer.AlignmentMode.End;
		_skills[0] = new SkillSquare { Kind = IconKind.Claw, Key = KeyBindings.Short("attack") };
		_skills[2] = new SkillSquare { Kind = IconKind.FromAbove, Key = KeyBindings.Short("special") };
		_howlSquare = new SkillSquare { Kind = IconKind.Shield, Key = KeyBindings.Short("howl"), Hot = UiTokens.Accent };
		squares.AddChild(_skills[0]);
		squares.AddChild(_skills[2]);
		squares.AddChild(_howlSquare);
		column.AddChild(squares);

		HBoxContainer hint = UiKit.Row(UiTokens.S1);
		hint.Alignment = BoxContainer.AlignmentMode.End;
		_howlLabel = Outlined(UiKit.Caption(Loc.F("Segure {0} · habilidades", KeyBindings.Label("guide")), UiTokens.TextSecondary));
		hint.AddChild(_howlLabel);
		column.AddChild(hint);

		return column;
	}

	private void BuildTransform()
	{
		// Inferior centro: o especial do lobo (o disco que acende) e,
		// embaixo, a faixa da transformação. Sem painel.
		VBoxContainer column = Corner(UiKit.Column(UiTokens.S2), Control.LayoutPreset.CenterBottom);
		column.OffsetBottom = -Edge;
		column.OffsetLeft = -170;
		column.OffsetRight = 170;

		_pounceTile = new AbilityTile { Title = "ESPECIAL", Key = KeyBindings.Short("pounce"), Kind = IconKind.Claw };
		_pounceTile.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		column.AddChild(_pounceTile);

		_shiftPanel = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_shiftPanel.AddThemeConstantOverride("separation", UiTokens.S1);
		column.AddChild(_shiftPanel);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		var key = new PanelContainer { CustomMinimumSize = new Vector2(24, 22) };
		key.AddThemeStyleboxOverride("panel", HudStyle.Facet(new Color(0.03f, 0.04f, 0.06f, 0.85f), new Color(1, 1, 1, 0.3f), 1, HudStyle.Skew, 4, 6, 0));
		_shiftKey = HudStyle.Number(UiKit.Text("3", "Numeric"), 16);
		_shiftKey.HorizontalAlignment = HorizontalAlignment.Center;
		key.AddChild(_shiftKey);
		row.AddChild(key);
		_shiftText = Outlined(UiKit.Caption("Virar lobo", UiTokens.TextPrimary));
		row.AddChild(_shiftText);
		row.AddChild(UiKit.Spacer());
		_shiftState = Outlined(UiKit.Caption("PRONTO", UiTokens.Success));
		row.AddChild(_shiftState);
		_shiftPanel.AddChild(row);

		_shiftBar = Bar(UiTokens.WolfOrange, 7);
		_shiftBar.Value = 1;
		_shiftPanel.AddChild(_shiftBar);
	}

	private static ProgressBar Bar(Color fill, int height)
	{
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, height),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MaxValue = 1,
			Step = 0.001,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		HudStyle.SlantBar(bar, fill);
		return bar;
	}

	/// <summary>Contorno escuro no texto solto sobre o mundo — sem painel
	/// atrás, texto claro some em parede clara.</summary>
	private static T Outlined<T>(T label) where T : Label => HudStyle.Outlined(label);
}
