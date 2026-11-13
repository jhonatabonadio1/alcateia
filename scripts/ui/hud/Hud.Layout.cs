using Godot;
using System.Collections.Generic;

/// <summary>
/// A montagem do HUD, uma vez no _Ready: cada canto com o seu bloco
/// (docs/UI.md) e as peças repetidas (barra, texto com contorno).
/// </summary>
public partial class Hud
{
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

		_death = UiKit.Column(UiTokens.S1);
		_death.Alignment = BoxContainer.AlignmentMode.Center;
		_death.SetAnchorsPreset(Control.LayoutPreset.Center);
		_death.AnchorTop = _death.AnchorBottom = 0.36f;
		_death.GrowHorizontal = Control.GrowDirection.Both;
		_death.GrowVertical = Control.GrowDirection.Both;
		_death.MouseFilter = Control.MouseFilterEnum.Ignore;
		_death.Visible = false;

		Label title = UiKit.Text("ABATIDO", "Display", UiTokens.Danger);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		_death.AddChild(title);
		_deathBy = UiKit.Text("", "Subtitle");
		_deathBy.HorizontalAlignment = HorizontalAlignment.Center;
		_death.AddChild(_deathBy);
		_deathTimer = UiKit.Text("", "Muted");
		_deathTimer.HorizontalAlignment = HorizontalAlignment.Center;
		_death.AddChild(_deathTimer);
		_root.AddChild(_death);
	}

	private void BuildMinimap()
	{
		VBoxContainer box = Corner(UiKit.Column(UiTokens.S1), Control.LayoutPreset.TopLeft);
		box.Position = new Vector2(Edge, Edge);

		var frame = new PanelContainer { ThemeTypeVariation = "HudPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
		frame.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.55f), new Color(1, 1, 1, 0.08f), 1, 0, 0, 0));
		var map = new Minimap { MouseFilter = Control.MouseFilterEnum.Ignore };
		map.Side = MinimapSide;
		frame.AddChild(map);
		box.AddChild(frame);

		MapInfo info = Maps.Get(RoomConfig.Rules.Map);
		string mode = RoomSettings.ModeName(RoomConfig.Rules.Mode);
		box.AddChild(Outlined(UiKit.Caption($"{info.Name}  ·  {mode}", UiTokens.TextPrimary)));
	}

	private void BuildFeed()
	{
		_feed = Corner(UiKit.Column(UiTokens.S1), Control.LayoutPreset.TopRight);
		_feed.Position = new Vector2(-Edge, Edge);
		_feed.CustomMinimumSize = new Vector2(440, 0);
		_feed.OffsetLeft = -Edge - 440;
		_feed.OffsetRight = -Edge;
	}

	private void BuildVitals()
	{
		var panel = new PanelContainer { ThemeTypeVariation = "HudPanel", CustomMinimumSize = new Vector2(360, 0) };
		Corner(panel, Control.LayoutPreset.BottomLeft);
		panel.OffsetLeft = Edge;
		panel.OffsetBottom = -Edge;

		VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
		panel.AddChild(column);

		// Forma sempre visível, ao lado da vida. Parece redundante; em
		// partida rápida não é.
		HBoxContainer form = UiKit.Row(UiTokens.S2);
		_formIcon = Icon.Make(IconKind.Human, 22, UiTokens.TextPrimary);
		form.AddChild(_formIcon);
		_formLabel = UiKit.Caption("Humano", UiTokens.TextPrimary);
		form.AddChild(_formLabel);
		column.AddChild(form);

		_shieldRow = UiKit.Row(UiTokens.S2);
		_shieldBar = Bar(UiTokens.TeamOrange.Lightened(0.15f), 6);
		_shieldRow.AddChild(_shieldBar);
		_shieldNumber = UiKit.Text("", "Numeric", UiTokens.TeamOrange.Lightened(0.25f));
		_shieldNumber.CustomMinimumSize = new Vector2(44, 0);
		_shieldNumber.HorizontalAlignment = HorizontalAlignment.Right;
		_shieldRow.AddChild(_shieldNumber);
		column.AddChild(_shieldRow);

		HBoxContainer hp = UiKit.Row(UiTokens.S2);
		_hpNumber = UiKit.Text("200", "NumericLarge");
		_hpNumber.AddThemeFontSizeOverride("font_size", UiTokens.Display);
		hp.AddChild(_hpNumber);
		_hpMax = UiKit.Text("/ 200", "Numeric", UiTokens.TextSecondary);
		_hpMax.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		hp.AddChild(_hpMax);
		column.AddChild(hp);

		_hpBar = Bar(UiTokens.TextPrimary, 10);
		column.AddChild(_hpBar);
	}

	private void BuildEquipment()
	{
		VBoxContainer side = Corner(UiKit.Column(UiTokens.S2), Control.LayoutPreset.BottomRight);
		side.OffsetRight = -Edge;
		side.OffsetBottom = -Edge;
		side.CustomMinimumSize = new Vector2(380, 0);
		side.OffsetLeft = -Edge - 380;

		// WP, perto da munição: é o que se gasta ao renascer.
		HBoxContainer wpRow = UiKit.Row(UiTokens.S2);
		wpRow.Alignment = BoxContainer.AlignmentMode.End;
		wpRow.AddChild(Outlined(UiKit.Caption("WP", UiTokens.Accent)));
		_wp = Outlined(UiKit.Text("0", "NumericLarge", UiTokens.Accent));
		wpRow.AddChild(_wp);
		side.AddChild(wpRow);

		var panel = new PanelContainer { ThemeTypeVariation = "HudPanel" };
		side.AddChild(panel);
		var stack = new Control { CustomMinimumSize = new Vector2(0, 150), MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddChild(stack);

		_human = UiKit.Fill(HumanPanel());
		stack.AddChild(_human);
		_wolf = UiKit.Fill(WolfPanel());
		stack.AddChild(_wolf);
	}

	private VBoxContainer HumanPanel()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S1);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;

		HBoxContainer top = UiKit.Row(UiTokens.S2);
		_weaponArt = WeaponArt.Make("m16a2", new Vector2(120, 44));
		_weaponArt.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		top.AddChild(_weaponArt);

		VBoxContainer names = UiKit.Column(0);
		names.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_weaponName = UiKit.Text("", "Subtitle");
		_weaponName.HorizontalAlignment = HorizontalAlignment.Right;
		names.AddChild(_weaponName);
		_reloading = UiKit.Caption("", UiTokens.Warning);
		_reloading.HorizontalAlignment = HorizontalAlignment.Right;
		names.AddChild(_reloading);
		top.AddChild(names);
		column.AddChild(top);

		HBoxContainer ammo = UiKit.Row(UiTokens.S2);
		ammo.Alignment = BoxContainer.AlignmentMode.End;

		_grenades = UiKit.Text("", "Numeric", UiTokens.TextSecondary);
		HBoxContainer grenade = UiKit.Row(UiTokens.S1);
		grenade.AddChild(Icon.Make(IconKind.Grenade, 16, UiTokens.TextSecondary));
		grenade.AddChild(_grenades);
		grenade.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		ammo.AddChild(grenade);
		ammo.AddChild(UiKit.Spacer());

		_mag = UiKit.Text("30", "NumericLarge");
		_mag.AddThemeFontSizeOverride("font_size", 52);
		ammo.AddChild(_mag);
		// Reserva infinita: o custo de atirar é o tempo de recarga.
		_reserve = UiKit.Text("/ ∞", "Subtitle", UiTokens.TextSecondary);
		_reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		ammo.AddChild(_reserve);
		column.AddChild(ammo);

		HBoxContainer slots = UiKit.Row(UiTokens.S1);
		slots.Alignment = BoxContainer.AlignmentMode.End;
		foreach (var (slot, label) in new[] { (Slot.Primary, "1"), (Slot.Secondary, "2"), (Slot.Wolf, "3"), (Slot.Knife, "4"), (Slot.Grenade, "5") })
		{
			var chip = new PanelContainer { CustomMinimumSize = new Vector2(28, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
			Label text = UiKit.Text(label, "Numeric");
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

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(Icon.Make(IconKind.Wolf, 22, UiTokens.TeamOrange));
		_breedName = UiKit.Text("", "Subtitle", UiTokens.TextPrimary);
		head.AddChild(_breedName);
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.Caption("sem munição", UiTokens.TextDisabled));
		column.AddChild(head);

		string[] names = { "Garrada · clique", "Salto · direito no chão", "Bote · direito no ar" };
		for (int i = 0; i < 3; i++)
		{
			HBoxContainer row = UiKit.Row(UiTokens.S2);
			Label label = UiKit.Caption(names[i], UiTokens.TextPrimary);
			label.CustomMinimumSize = new Vector2(190, 0);
			row.AddChild(label);
			_cooldownBars[i] = Bar(UiTokens.TeamOrange.Lightened(0.2f), 6);
			_cooldownBars[i].Value = 1;
			row.AddChild(_cooldownBars[i]);
			column.AddChild(row);
		}

		HBoxContainer howl = UiKit.Row(UiTokens.S2);
		_howlLabel = UiKit.Caption("Uivo · segure R", UiTokens.TextPrimary);
		_howlLabel.CustomMinimumSize = new Vector2(190, 0);
		howl.AddChild(_howlLabel);
		_howlBar = Bar(UiTokens.Accent, 6);
		howl.AddChild(_howlBar);
		column.AddChild(howl);

		return column;
	}

	private void BuildTransform()
	{
		_shiftPanel = new PanelContainer { ThemeTypeVariation = "HudPanel", CustomMinimumSize = new Vector2(380, 0) };
		Corner(_shiftPanel, Control.LayoutPreset.CenterBottom);
		_shiftPanel.OffsetBottom = -Edge;
		_shiftPanel.OffsetLeft = -190;
		_shiftPanel.OffsetRight = 190;

		VBoxContainer column = UiKit.Column(UiTokens.S1);
		_shiftPanel.AddChild(column);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		var key = new PanelContainer { CustomMinimumSize = new Vector2(24, 22) };
		key.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, 6, 0));
		_shiftKey = UiKit.Text("3", "Numeric");
		_shiftKey.HorizontalAlignment = HorizontalAlignment.Center;
		key.AddChild(_shiftKey);
		row.AddChild(key);
		_shiftText = UiKit.Caption("Virar lobo", UiTokens.TextPrimary);
		row.AddChild(_shiftText);
		row.AddChild(UiKit.Spacer());
		_shiftState = UiKit.Caption("PRONTO", UiTokens.Success);
		row.AddChild(_shiftState);
		column.AddChild(row);

		_shiftBar = Bar(UiTokens.TeamOrange, 6);
		_shiftBar.Value = 1;
		column.AddChild(_shiftBar);
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
		bar.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(1, 1, 1, 0.1f), default, 0, 0, 0, 0));
		bar.AddThemeStyleboxOverride("fill", UiTheme.Box(fill, default, 0, 0, 0, 0));
		return bar;
	}

	/// <summary>Contorno escuro no texto solto sobre o mundo — sem painel
	/// atrás, texto claro some em parede clara.</summary>
	private static T Outlined<T>(T label) where T : Label
	{
		label.AddThemeConstantOverride("outline_size", 5);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.75f));
		return label;
	}
}
