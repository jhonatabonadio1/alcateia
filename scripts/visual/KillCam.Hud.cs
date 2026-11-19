using Godot;

/// <summary>
/// O HUD da câmera de abate e do espectador (pedido do Jhon, 18/11, com o
/// Overwatch de referência): o HUD de quem se vê, não o seu.
///
/// - **Câmera de abate**: faixas escuras em cima e embaixo, a imagem um
///   pouco tingida e com linhas (é gravação), "ABATIDO POR" e o nome de
///   quem matou no alto à esquerda, "RENASCE EM" e os segundos no alto à
///   direita, **"CÂMERA DE ABATE"** grande embaixo, no meio, com "ESPAÇO:
///   ESPECTADOR" abaixo;
/// - **espectador**: sem faixas nem tinta; "ASSISTINDO" e o nome; embaixo,
///   "ESPECTADOR" e as teclas;
/// - nos dois: embaixo à esquerda o nome, a vida e o escudo de quem se vê;
///   embaixo à direita a arma e a munição dele (ou "LOBO"); e a mira no
///   meio, em primeira pessoa.
///
/// O HUD do próprio jogador esconde a vida, a arma e a mira enquanto isto
/// está na tela (<see cref="KillCam.Active"/>); o placar, o relógio, o
/// minimapa e o feed ficam, e o feed marca o nome de quem se vê
/// (<see cref="KillCam.Focus"/>).
/// </summary>
public partial class KillCam
{
	private const string TintCode = @"
shader_type canvas_item;
uniform float amount = 1.0;
void fragment() {
	vec2 p = UV - 0.5;
	float edge = smoothstep(0.35, 0.85, length(p * vec2(1.0, 1.25)));
	float lines = 0.5 + 0.5 * sin(UV.y * 900.0);
	vec3 tint = vec3(1.0, 0.82, 0.45);
	float a = (0.07 + 0.05 * lines + edge * 0.32) * amount;
	COLOR = vec4(mix(tint, vec3(0.05, 0.02, 0.0), edge), a);
}";

	private CanvasLayer _hudLayer;
	private ColorRect _tint, _barTop, _barBottom;
	private Label _whoCaption, _whoName, _whoForm, _respawnCaption, _respawnNumber, _title, _hint;
	private Label _vitalName, _hpNumber, _hpMax, _gearName, _ammo, _ammoMax;
	private PovBars _bars;
	private PovCross _cross;
	private Control _vitals, _gear;

	private void BuildHud()
	{
		_hudLayer = new CanvasLayer { Layer = 8 };
		AddChild(_hudLayer);
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_hudLayer.AddChild(root);

		_tint = new ColorRect { Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = new Shader { Code = TintCode } } };
		_tint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(_tint);

		_barTop = Bar(root, top: true, 74);
		_barBottom = Bar(root, top: false, 178);

		_cross = new PovCross { MouseFilter = Control.MouseFilterEnum.Ignore };
		_cross.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(_cross);

		// No alto à esquerda: quem.
		VBoxContainer who = Box(root, Control.LayoutPreset.TopLeft, 0);
		// Ao lado do minimapa (que fica, como o placar e o feed).
		who.OffsetLeft = 236; who.OffsetTop = 14;
		_whoCaption = Outlined(UiKit.Caption("ABATIDO POR", UiTokens.TextSecondary));
		who.AddChild(_whoCaption);
		_whoName = HudStyle.Number(UiKit.Text("", "Display"), 34, UiTokens.Danger);
		_whoName.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
		who.AddChild(_whoName);
		_whoForm = Outlined(UiKit.Caption("", UiTokens.TextSecondary));
		who.AddChild(_whoForm);

		// Embaixo, no meio: o título.
		VBoxContainer middle = Box(root, Control.LayoutPreset.CenterBottom, 0);
		middle.OffsetBottom = -24; middle.OffsetLeft = -360; middle.OffsetRight = 360;
		middle.GrowVertical = Control.GrowDirection.Begin;
		middle.Alignment = BoxContainer.AlignmentMode.End;
		// Quando renasce, logo acima do título (o alto à direita é do feed).
		HBoxContainer respawn = UiKit.Row(UiTokens.S2);
		respawn.Alignment = BoxContainer.AlignmentMode.Center;
		_respawnCaption = Outlined(UiKit.Caption("RENASCE EM", UiTokens.Accent));
		_respawnCaption.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		respawn.AddChild(_respawnCaption);
		_respawnNumber = HudStyle.Number(UiKit.Text("", "NumericLarge"), 34, UiTokens.TextPrimary);
		respawn.AddChild(_respawnNumber);
		middle.AddChild(respawn);

		_title = HudStyle.Number(UiKit.Text("", "Display"), 64, UiTokens.TextPrimary);
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(_title);
		_hint = Outlined(UiKit.Caption("", UiTokens.Accent));
		_hint.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(_hint);

		// Embaixo à esquerda: a vida de quem se vê.
		VBoxContainer vitals = Box(root, Control.LayoutPreset.BottomLeft, 2);
		vitals.OffsetLeft = 28; vitals.OffsetBottom = -26;
		vitals.GrowVertical = Control.GrowDirection.Begin;
		_vitalName = HudStyle.Number(UiKit.Text("", "Display"), 22, UiTokens.TextPrimary);
		_vitalName.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
		vitals.AddChild(_vitalName);
		HBoxContainer hpRow = UiKit.Row(4);
		_hpNumber = HudStyle.Number(UiKit.Text("", "NumericLarge"), 40, UiTokens.TextPrimary);
		hpRow.AddChild(_hpNumber);
		_hpMax = HudStyle.Number(UiKit.Text("", "Numeric"), 20, UiTokens.TextSecondary);
		_hpMax.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		hpRow.AddChild(_hpMax);
		vitals.AddChild(hpRow);
		_bars = new PovBars { CustomMinimumSize = new Vector2(300, 26), MouseFilter = Control.MouseFilterEnum.Ignore };
		vitals.AddChild(_bars);
		_vitals = vitals;

		// Embaixo à direita: a arma e a munição.
		VBoxContainer gear = Box(root, Control.LayoutPreset.BottomRight, 0);
		gear.OffsetRight = -28; gear.OffsetBottom = -26;
		gear.GrowHorizontal = Control.GrowDirection.Begin;
		gear.GrowVertical = Control.GrowDirection.Begin;
		gear.Alignment = BoxContainer.AlignmentMode.End;
		HBoxContainer ammoRow = UiKit.Row(4);
		ammoRow.Alignment = BoxContainer.AlignmentMode.End;
		_ammo = HudStyle.Number(UiKit.Text("", "NumericLarge"), 44, UiTokens.TextPrimary);
		ammoRow.AddChild(_ammo);
		_ammoMax = HudStyle.Number(UiKit.Text("", "Numeric"), 20, UiTokens.TextSecondary);
		_ammoMax.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		ammoRow.AddChild(_ammoMax);
		gear.AddChild(ammoRow);
		_gearName = HudStyle.Number(UiKit.Text("", "Display"), 20, UiTokens.TextSecondary);
		_gearName.HorizontalAlignment = HorizontalAlignment.Right;
		gear.AddChild(_gearName);
		_gear = gear;
	}

	private static ColorRect Bar(Control root, bool top, float height)
	{
		var bar = new ColorRect { Color = new Color(0.01f, 0.012f, 0.02f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		bar.SetAnchorsPreset(top ? Control.LayoutPreset.TopWide : Control.LayoutPreset.BottomWide);
		if (top) bar.OffsetBottom = height;
		else bar.OffsetTop = -height;
		root.AddChild(bar);
		return bar;
	}

	private static VBoxContainer Box(Control root, Control.LayoutPreset preset, int gap)
	{
		VBoxContainer box = UiKit.Column(gap);
		box.MouseFilter = Control.MouseFilterEnum.Ignore;
		box.SetAnchorsPreset(preset);
		root.AddChild(box);
		return box;
	}

	private static Label Outlined(Label label) => HudStyle.Outlined(label);

	/// <summary>Atualiza o HUD por cima a cada quadro.</summary>
	private void UpdateHud()
	{
		if (_hudLayer == null) return;

		// A escolha de conjunto (na hora de renascer) cobre tudo.
		_hudLayer.Visible = !UiState.AnyOpen;
		bool kill = _phase == Phase.Kill;
		bool watching = _phase is Phase.First or Phase.Above && _mate != null && IsInstanceValid(_mate);
		bool someone = (kill && _shownBody != null && IsInstanceValid(_shownBody)) || watching;

		_tint.Visible = kill;
		_barTop.Visible = _barBottom.Visible = kill;
		((ShaderMaterial)_tint.Material).SetShaderParameter("amount", Mathf.Clamp((float)_time * 3f, 0f, 1f));

		// Quem.
		Node3D who = kill ? _killer : watching ? _mate : _phase == Phase.Death ? _killer : null;
		Health whoHealth = who != null && IsInstanceValid(who) ? who.GetNodeOrNull<Health>("Health") : null;
		_whoCaption.Visible = _whoName.Visible = whoHealth != null;
		if (whoHealth != null)
		{
			_whoCaption.Text = watching ? "ASSISTINDO" : "ABATIDO POR";
			_whoName.Text = whoHealth.PublicName.ToUpper();
			_whoName.AddThemeColorOverride("font_color", Teams.ColorOf(whoHealth.Team).Lightened(0.2f));
		}
		bool wolf = someone && _shown.Wolf;
		_whoForm.Visible = someone;
		_whoForm.Text = wolf ? "COMO LOBO" : "COMO HUMANO";

		// Quando renasce.
		float left = _victim?.GetNodeOrNull<Health>("Health")?.RespawnIn ?? 0f;
		_respawnCaption.Text = left > 0.05f ? "RENASCE EM" : "PRONTO PARA RENASCER";
		_respawnNumber.Text = left > 0.05f ? Mathf.CeilToInt(left).ToString() : "";

		// O título e as teclas.
		_title.Text = _phase switch
		{
			Phase.Kill => "CÂMERA DE ABATE",
			Phase.Death => "",
			_ => "ESPECTADOR"
		};
		_title.AddThemeFontSizeOverride("font_size", kill ? 64 : 30);
		_hint.Text = _phase switch
		{
			Phase.Death or Phase.Kill => "ESPAÇO: ESPECTADOR",
			Phase.First => "CLIQUES: TROCAR ALIADO  ·  ESPAÇO: VISTA DE CIMA",
			Phase.Above => "CLIQUES: TROCAR ALIADO  ·  ESPAÇO: CÂMERA LIVRE",
			_ => "WASD E MOUSE  ·  ESPAÇO: VOLTAR AOS ALIADOS"
		};

		// A vida e a arma de quem se vê.
		_vitals.Visible = _gear.Visible = someone;
		_cross.Visible = someone && _shownFirst && !wolf;
		if (!someone) return;

		if (watching && _phase == Phase.Above) _shown = ReplayTape.Read(_mate);
		_vitalName.Text = whoHealth?.PublicName.ToUpper() ?? "";
		_hpNumber.Text = Mathf.CeilToInt(Mathf.Max(_shown.Hp, 0)).ToString();
		_hpMax.Text = $"/ {_shown.MaxHp:0}";
		_bars.Set(_shown.Hp, _shown.MaxHp, _shown.Shield, _shown.MaxShield);

		if (wolf)
		{
			_ammo.Text = "";
			_ammoMax.Text = "";
			_gearName.Text = "LOBO";
		}
		else
		{
			string id = _shown.Weapon ?? "";
			bool gun = _shown.Mag > 0 && Arsenal.Get(id) != null;
			_ammo.Text = gun ? _shown.Ammo.ToString() : "";
			// Munição infinita (decisão do Jhon): a reserva é "∞", como no HUD do jogo.
			_ammoMax.Text = gun ? "/ ∞" : "";
			_gearName.Text = Arsenal.NameOf(id).ToUpper();
		}
	}
}

/// <summary>A vida em gomos e o escudo em cima, de quem se vê.</summary>
public partial class PovBars : Control
{
	private float _hp, _maxHp = 1, _shield, _maxShield;

	public void Set(float hp, float maxHp, float shield, float maxShield)
	{
		_hp = hp; _maxHp = Mathf.Max(maxHp, 1); _shield = shield; _maxShield = maxShield;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float w = Size.X, skew = 6f;
		int cells = Mathf.Max(1, Mathf.RoundToInt(_maxHp / 25f));
		float gap = 3f, cell = (w - gap * (cells - 1)) / cells;
		float hpRow = _maxShield > 0 ? 14f : Size.Y;
		float y0 = Size.Y - hpRow;
		float filled = Mathf.Clamp(_hp / _maxHp, 0f, 1f) * cells;
		bool low = _hp / _maxHp < 0.3f;
		for (int i = 0; i < cells; i++)
		{
			float x = i * (cell + gap);
			Color back = new(0.02f, 0.025f, 0.035f, 0.6f);
			Color fill = low ? UiTokens.Danger : Colors.White;
			Slant(x, y0, cell, hpRow, skew, back);
			float part = Mathf.Clamp(filled - i, 0f, 1f);
			if (part > 0f) Slant(x, y0, cell * part, hpRow, skew, fill);
		}

		if (_maxShield <= 0) return;
		float sh = Mathf.Clamp(_shield / _maxShield, 0f, 1f);
		Slant(0, 0, w, 7f, 3f, new Color(0.02f, 0.025f, 0.035f, 0.6f));
		if (sh > 0f) Slant(0, 0, w * sh, 7f, 3f, new Color("6fd3ff"));
	}

	private void Slant(float x, float y, float width, float height, float skew, Color color)
	{
		DrawColoredPolygon(new[]
		{
			new Vector2(x + skew, y), new Vector2(x + width + skew, y),
			new Vector2(x + width, y + height), new Vector2(x, y + height)
		}, color);
	}
}

/// <summary>A mira de quem se vê, no meio da tela.</summary>
public partial class PovCross : Control
{
	public override void _Draw()
	{
		Vector2 c = Size / 2f;
		var white = new Color(1, 1, 1, 0.9f);
		var dark = new Color(0, 0, 0, 0.55f);
		DrawArc(c, 15f, 0, Mathf.Tau, 40, dark, 3.2f, true);
		DrawArc(c, 15f, 0, Mathf.Tau, 40, white, 1.4f, true);
		DrawCircle(c, 2.4f, dark);
		DrawCircle(c, 1.5f, white);
	}
}
