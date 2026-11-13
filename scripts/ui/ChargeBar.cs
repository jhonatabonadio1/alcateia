using Godot;

/// <summary>
/// A barra de força do salto: aparece enquanto o lobo carrega o salto
/// no chão (botão direito seguro) e enche em
/// <see cref="Weapon.PounceChargeTime"/>. Golpe mais curto, menos dano;
/// mais longo, mais forte (decisão do Jhon). Cheia, pulsa: segurar além
/// disso não dá mais nada, e logo ele salta sozinho.
/// </summary>
public partial class ChargeBar : CanvasLayer
{
	private Weapon _weapon;
	private Control _box;
	private ProgressBar _bar;
	private Label _value;
	private StyleBoxFlat _fill;
	private Tween _pulse;

	public override void _Ready()
	{
		Layer = 6;

		var column = UiKit.Column(UiTokens.S1);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;
		column.SetAnchorsPreset(Control.LayoutPreset.Center);
		column.AnchorTop = column.AnchorBottom = 0.64f;
		column.GrowHorizontal = Control.GrowDirection.Both;
		column.GrowVertical = Control.GrowDirection.Both;

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Caption("FORÇA DO SALTO", UiTokens.TextPrimary));
		head.AddChild(UiKit.Spacer());
		_value = UiKit.Caption("0%", UiTokens.Accent);
		head.AddChild(_value);
		column.AddChild(head);

		_bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(280, 10), MouseFilter = Control.MouseFilterEnum.Ignore };
		_bar.AddThemeStyleboxOverride("background", UiTheme.Box(new Color(UiTokens.Bg, 0.7f), new Color(1, 1, 1, 0.15f), 1, 0, 0, 0));
		_fill = UiTheme.Box(UiTokens.Accent, default, 0, 0, 0, 0);
		_bar.AddThemeStyleboxOverride("fill", _fill);
		column.AddChild(_bar);

		_box = UiTheme.Apply(column);
		_box.Visible = false;
		AddChild(_box);
	}

	public override void _Process(double delta)
	{
		_weapon ??= Groups.LocalPlayer(GetTree())?.GetNodeOrNull<Weapon>("Weapon");
		if (_weapon == null) return;

		bool charging = _weapon.IsChargingPounce;
		_box.Visible = charging;
		if (!charging)
		{
			_pulse?.Kill();
			_pulse = null;
			_box.Modulate = Colors.White;
			return;
		}

		float charge = _weapon.PounceCharge;
		_bar.Value = charge;
		_value.Text = $"{Mathf.RoundToInt(charge * 100)}%";
		_fill.BgColor = UiTokens.Accent.Lerp(UiTokens.Danger, charge);

		if (charge >= 1f && _pulse == null)
			_pulse = Motion.Pulse(_box, 0.55f, 0.35f);
	}
}
