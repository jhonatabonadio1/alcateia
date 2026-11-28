using Godot;

/// <summary>
/// A lista dos calls de rádio (<see cref="Radio"/>): Z abre, no canto
/// inferior esquerdo, acima da barra de vida; um número (1…9, 0 = 10) fala
/// e fecha; Z de novo, Esc ou <see cref="RadioListTime"/> segundos sem
/// escolher fecham. Com a lista aberta os números não trocam de arma nem
/// transformam.
///
/// Quando alguém do time fala, a fala aparece por um instante no mesmo
/// canto, com o nome de quem falou.
/// </summary>
public partial class Hud
{
	[Export] public float RadioListTime = 5f;

	private PanelContainer _radioList;
	private Label _radioSaid;
	private double _radioLeft, _radioSaidLeft;

	/// <summary>A lista do rádio está na tela. Para os testes.</summary>
	public bool RadioOpen => _radioList != null && _radioList.Visible;

	/// <summary>A última fala mostrada. Para os testes.</summary>
	public string RadioSaid => _radioSaid?.Text ?? "";

	private void BuildRadio()
	{
		VBoxContainer corner = Corner(UiKit.Column(UiTokens.S2), Control.LayoutPreset.BottomLeft);
		corner.OffsetLeft = Edge;
		corner.OffsetBottom = -Edge - 118;
		corner.GrowVertical = Control.GrowDirection.Begin;
		corner.Alignment = BoxContainer.AlignmentMode.End;

		_radioList = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
		_radioList.AddThemeStyleboxOverride("panel", HudStyle.Plate(UiTokens.Accent, padH: UiTokens.S3, padV: UiTokens.S2, accentWidth: 3));
		VBoxContainer rows = UiKit.Column(2);
		_radioList.AddChild(rows);
		Label title = UiKit.Caption("RÁDIO DO TIME", UiTokens.Accent);
		rows.AddChild(title);
		for (int i = 0; i < Radio.Calls.Length; i++)
		{
			HBoxContainer row = UiKit.Row(UiTokens.S2);
			Label key = HudStyle.Number(UiKit.Text(i == 9 ? "0" : (i + 1).ToString(), "Numeric"), 15, UiTokens.Accent);
			key.CustomMinimumSize = new Vector2(16, 0);
			key.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
			row.AddChild(key);
			// As falas ficam em inglês, como a voz.
			Label call = UiKit.Text(Radio.Calls[i], null, UiTokens.TextPrimary);
			call.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
			row.AddChild(call);
			rows.AddChild(row);
		}
		corner.AddChild(_radioList);

		_radioSaid = Outlined(UiKit.Text("", null, UiTokens.TextPrimary));
		_radioSaid.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
		_radioSaid.AddThemeFontOverride("font", UiFonts.SemiBold);
		_radioSaid.Visible = false;
		corner.AddChild(_radioSaid);

		Radio.Heard += OnRadioHeard;
		TreeExiting += () =>
		{
			Radio.Heard -= OnRadioHeard;
			Radio.MenuOpen = false;
		};
	}

	private void OnRadioHeard(string who, int team, int call)
	{
		if (_radioSaid == null || !IsInstanceValid(_radioSaid)) return;
		_radioSaid.Text = $"{who}: {Radio.Calls[call]}";
		_radioSaid.AddThemeColorOverride("font_color", Teams.ColorOf(team).Lightened(0.35f));
		_radioSaid.Visible = true;
		_radioSaidLeft = 2.6;
	}

	private void SetRadio(bool open)
	{
		if (_radioList == null) return;
		_radioList.Visible = open;
		Radio.MenuOpen = open;
		_radioLeft = open ? RadioListTime : 0;
		if (open) Motion.FadeIn(_radioList, UiTokens.Fast);
	}

	private void UpdateRadio(double delta)
	{
		if (_radioList == null) return;

		if (_radioSaidLeft > 0)
		{
			_radioSaidLeft -= delta;
			if (_radioSaidLeft <= 0) _radioSaid.Visible = false;
		}

		bool can = Subject == null && !_health.IsDead && !UiState.AnyOpen && !MatchChat.Typing && Match.Current?.Phase != MatchPhase.Ended;
		if (RadioOpen && !can) SetRadio(false);
		if (can && Input.IsActionJustPressed("radio")) SetRadio(!RadioOpen);
		if (!RadioOpen) return;

		_radioLeft -= delta;
		if (_radioLeft <= 0) SetRadio(false);
	}

	public override void _Input(InputEvent @event)
	{
		if (Subject != null) return;
		if (!RadioOpen || @event is not InputEventKey { Pressed: true, Echo: false } key) return;

		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
		if (code == Key.Escape)
		{
			SetRadio(false);
			GetViewport().SetInputAsHandled();
			return;
		}

		int call = code switch
		{
			>= Key.Key1 and <= Key.Key9 => (int)(code - Key.Key1),
			Key.Key0 => 9,
			>= Key.Kp1 and <= Key.Kp9 => (int)(code - Key.Kp1),
			Key.Kp0 => 9,
			_ => -1
		};
		if (call < 0) return;

		Radio.Send(_player, call);
		Radio.SwallowNumber();
		SetRadio(false);
		GetViewport().SetInputAsHandled();
	}
}
