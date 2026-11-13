using Godot;
using System;
using System.Linq;

/// <summary>
/// Uma linha da lista de salas. Objetivo: decidir em menos de três
/// segundos em qual sala entrar — então cada campo é lido de relance:
///
/// número curto (para "entra na 12"), estado com ícone + cor + texto
/// (nunca só cor), título do mestre, modo em etiqueta, miniatura do mapa
/// (vale mais que o nome), lotação com barra fina, regra de
/// transformação em ícone, faixa de patente só quando limitada, cadeado
/// se tem senha, e ping em barras.
///
/// Entra com duplo clique ou pelo botão "Entrar", que aparece ao passar
/// o mouse — duplo clique como única forma de entrar é erro clássico.
/// </summary>
public partial class RoomRow : PanelContainer
{
	private RoomListing _room;
	private Action<RoomListing> _enter;
	private Button _button;
	private StyleBoxFlat _normal;
	private StyleBoxFlat _hover;

	public static RoomRow Make(RoomListing room, Action<RoomListing> enter)
	{
		var row = new RoomRow { _room = room, _enter = enter };
		row.Build();
		return row;
	}

	private void Build()
	{
		CustomMinimumSize = new Vector2(0, UiTokens.RoomRowHeight);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;

		_normal = UiTheme.Box(UiTokens.Surface1, UiTokens.Border, 1, UiTokens.RadiusSmall, UiTokens.S2, 0);
		_hover = UiTheme.Box(UiTokens.Surface2, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, 0);
		AddThemeStyleboxOverride("panel", _normal);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(row);

		RoomSettings s = _room.Settings;
		RoomState state = _room.EffectiveState;

		Label number = UiKit.Text($"#{s.Number}", "Numeric", UiTokens.TextSecondary);
		number.CustomMinimumSize = new Vector2(38, 0);
		number.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(number);

		row.AddChild(StateTag(state));

		VBoxContainer titleBox = UiKit.Column(0);
		titleBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		titleBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		titleBox.MouseFilter = MouseFilterEnum.Ignore;
		Label title = UiKit.Truncated(s.Title, null);
		title.AddThemeFontOverride("font", UiFonts.SemiBold);
		titleBox.AddChild(title);

		HBoxContainer sub = UiKit.Row(UiTokens.S1);
		sub.MouseFilter = MouseFilterEnum.Ignore;
		sub.AddChild(UiKit.Text($"{s.ScoreToWin} pts · {s.Minutes} min", "Caption"));
		if (s.Weapons != WeaponRule.Todas)
			sub.AddChild(UiKit.Text("· " + RoomSettings.WeaponNames[(int)s.Weapons], "Caption", UiTokens.Warning));
		titleBox.AddChild(sub);
		row.AddChild(titleBox);

		row.AddChild(UiKit.Chip(RoomSettings.ModeName(s.Mode), UiTokens.TextSecondary));

		row.AddChild(MapThumb(s.Map));
		row.AddChild(Occupancy());

		row.AddChild(TransformIcon(s.Transform));

		if (s.MinRank > 0 || s.MaxRank < Ranks.Count - 1)
		{
			HBoxContainer ranks = UiKit.Row(2);
			ranks.TooltipText = $"Patentes {Ranks.NameOf(s.MinRank)} a {Ranks.NameOf(s.MaxRank)}";
			ranks.AddChild(RankBadge.Make(s.MinRank, 20, true));
			ranks.AddChild(UiKit.Text("–", "Caption"));
			ranks.AddChild(RankBadge.Make(s.MaxRank, 20, true));
			row.AddChild(ranks);
		}

		Icon lockIcon = Icon.Make(IconKind.Lock, 16, _room.HasPassword ? UiTokens.Warning : new Color(0, 0, 0, 0));
		lockIcon.TooltipText = _room.HasPassword ? "Com senha" : "";
		lockIcon.MouseFilter = MouseFilterEnum.Pass;
		row.AddChild(lockIcon);

		row.AddChild(PingBars.Make(0));

		_button = UiKit.Button(state == RoomState.Playing ? "Em jogo" : "Entrar",
			state == RoomState.Playing ? ButtonKind.Secondary : ButtonKind.Primary, () => _enter?.Invoke(_room));
		_button.CustomMinimumSize = new Vector2(84, 34);
		_button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_button.Disabled = state == RoomState.Playing;
		_button.Modulate = new Color(1, 1, 1, 0);
		row.AddChild(_button);

		MouseEntered += () => Hover(true);
		MouseExited += () => Hover(GetGlobalRect().HasPoint(GetGlobalMousePosition()));
		_button.FocusEntered += () => Hover(true);
		_button.FocusExited += () => Hover(false);
	}

	private void Hover(bool on)
	{
		AddThemeStyleboxOverride("panel", on ? _hover : _normal);
		_button.Modulate = new Color(1, 1, 1, on ? 1 : 0);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton click && click.DoubleClick && click.ButtonIndex == MouseButton.Left
			&& _room.EffectiveState != RoomState.Playing)
		{
			AcceptEvent();
			_enter?.Invoke(_room);
		}
	}

	// ------------------------------------------------------------------ campos

	public static Control StateTag(RoomState state)
	{
		(string text, Color color, IconKind icon) = state switch
		{
			RoomState.Playing => ("Em jogo", UiTokens.TextDisabled, IconKind.Play),
			RoomState.Full => ("Cheia", UiTokens.Warning, IconKind.Users),
			_ => ("Aguardando", UiTokens.Success, IconKind.Clock)
		};

		HBoxContainer tag = UiKit.Row(UiTokens.S1);
		tag.CustomMinimumSize = new Vector2(104, 0);
		tag.MouseFilter = MouseFilterEnum.Ignore;
		tag.AddChild(Icon.Make(icon, 14, color));
		tag.AddChild(UiKit.Text(text, "Caption", color));
		return tag;
	}

	private static Control MapThumb(string mapId)
	{
		MapInfo map = Maps.Get(mapId);
		VBoxContainer box = UiKit.Column(0);
		box.MouseFilter = MouseFilterEnum.Ignore;
		box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		box.TooltipText = map.Name;

		var thumb = new TextureRect
		{
			Texture = ResourceLoader.Exists(map.Thumbnail) ? GD.Load<Texture2D>(map.Thumbnail) : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			CustomMinimumSize = new Vector2(56, 30),
			MouseFilter = MouseFilterEnum.Ignore
		};
		box.AddChild(thumb);
		return box;
	}

	private Control Occupancy()
	{
		VBoxContainer box = UiKit.Column(2);
		box.CustomMinimumSize = new Vector2(52, 0);
		box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		box.MouseFilter = MouseFilterEnum.Ignore;

		Label count = UiKit.Text($"{_room.Occupancy}/{_room.Capacity}", "Numeric");
		count.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(count);

		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(48, 3),
			MaxValue = _room.Capacity,
			Value = _room.Occupancy,
			ShowPercentage = false,
			MouseFilter = MouseFilterEnum.Ignore
		};
		Color fill = _room.Occupancy >= _room.Capacity ? UiTokens.Warning : UiTokens.Success;
		bar.AddThemeStyleboxOverride("fill", UiTheme.Box(fill, default, 0, 0, 0, 0));
		box.AddChild(bar);
		return box;
	}

	public static Control TransformIcon(TransformRule rule)
	{
		(IconKind icon, Color color, string tip) = rule switch
		{
			TransformRule.Bloqueada => (IconKind.Human, UiTokens.TextSecondary, "Lobo bloqueado: só humanos"),
			TransformRule.Obrigatoria => (IconKind.Wolf, UiTokens.Danger, "Só lobo"),
			TransformRule.UmLado => (IconKind.Wolf, UiTokens.Warning, "Um time só de lobos"),
			_ => (IconKind.Wolf, UiTokens.Accent, "Lobo liberado")
		};

		Icon glyph = Icon.Make(icon, 18, color);
		glyph.TooltipText = tip;
		glyph.MouseFilter = MouseFilterEnum.Pass;
		return glyph;
	}
}
