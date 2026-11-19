using Godot;

/// <summary>
/// O que a câmera de abate e o espectador escrevem por cima da tela de
/// quem se vê (o HUD dele é o do jogo, <c>KillCam.Pov.cs</c>):
///
/// - **câmera de abate**: faixas escuras finas em cima e embaixo, a imagem
///   levemente tingida e com linhas (é gravação), "ABATIDO POR" e o nome
///   de quem matou no alto, ao lado do minimapa, e **"CÂMERA DE ABATE"**
///   grande no meio de baixo, acima do HUD dele, com "RENASCE EM" e
///   "ESPAÇO: ESPECTADOR";
/// - **espectador**: sem faixas nem tinta; "ASSISTINDO" e o nome; o título
///   menor, "ESPECTADOR", e as teclas.
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
	float a = (0.05 + 0.04 * lines + edge * 0.28) * amount;
	COLOR = vec4(mix(tint, vec3(0.05, 0.02, 0.0), edge), a);
}";

	private CanvasLayer _hudLayer;
	private ColorRect _tint, _barTop, _barBottom;
	private Label _whoCaption, _whoName, _whoForm, _respawnCaption, _respawnNumber, _title, _hint;

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

		// Finas: a de baixo não pode cobrir o HUD de quem se vê.
		_barTop = Bar(root, top: true, 18);
		_barBottom = Bar(root, top: false, 10);

		// No alto, ao lado do minimapa: quem.
		VBoxContainer who = Box(root, Control.LayoutPreset.TopLeft, 0);
		who.OffsetLeft = 236; who.OffsetTop = 14;
		_whoCaption = HudStyle.Outlined(UiKit.Caption("ABATIDO POR", UiTokens.TextSecondary));
		who.AddChild(_whoCaption);
		_whoName = HudStyle.Number(UiKit.Text("", "Display"), 34, UiTokens.Danger);
		_whoName.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
		who.AddChild(_whoName);
		_whoForm = HudStyle.Outlined(UiKit.Caption("", UiTokens.TextSecondary));
		who.AddChild(_whoForm);

		// No meio de baixo, acima do HUD de quem se vê: quando renasce, o
		// título e as teclas.
		VBoxContainer middle = Box(root, Control.LayoutPreset.CenterBottom, 0);
		middle.OffsetBottom = -205; middle.OffsetLeft = -360; middle.OffsetRight = 360;
		middle.GrowVertical = Control.GrowDirection.Begin;
		middle.Alignment = BoxContainer.AlignmentMode.End;

		HBoxContainer respawn = UiKit.Row(UiTokens.S2);
		respawn.Alignment = BoxContainer.AlignmentMode.Center;
		_respawnCaption = HudStyle.Outlined(UiKit.Caption("RENASCE EM", UiTokens.Accent));
		_respawnCaption.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		respawn.AddChild(_respawnCaption);
		_respawnNumber = HudStyle.Number(UiKit.Text("", "NumericLarge"), 34, UiTokens.TextPrimary);
		respawn.AddChild(_respawnNumber);
		middle.AddChild(respawn);

		_title = HudStyle.Number(UiKit.Text("", "Display"), 60, UiTokens.TextPrimary);
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(_title);
		_hint = HudStyle.Outlined(UiKit.Caption("", UiTokens.Accent));
		_hint.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(_hint);
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

	/// <summary>Atualiza o que vai por cima, a cada quadro.</summary>
	private void UpdateHud()
	{
		if (_hudLayer == null) return;

		// A escolha de conjunto (na hora de renascer) cobre tudo.
		_hudLayer.Visible = !UiState.AnyOpen;
		if (_subjectHud != null && IsInstanceValid(_subjectHud)) _subjectHud.Visible = !UiState.AnyOpen;
		bool kill = _phase == Phase.Kill;
		bool watching = _phase is Phase.First or Phase.Above && _mate != null && IsInstanceValid(_mate);

		_tint.Visible = kill;
		_barTop.Visible = _barBottom.Visible = kill;
		((ShaderMaterial)_tint.Material).SetShaderParameter("amount", Mathf.Clamp((float)_time * 3f, 0f, 1f));

		// Quem.
		Node3D who = watching ? _mate : _killer;
		Health whoHealth = who != null && IsInstanceValid(who) && _phase != Phase.Free ? who.GetNodeOrNull<Health>("Health") : null;
		_whoCaption.Visible = _whoName.Visible = _whoForm.Visible = whoHealth != null;
		if (whoHealth != null)
		{
			_whoCaption.Text = watching ? "ASSISTINDO" : "ABATIDO POR";
			_whoName.Text = whoHealth.PublicName.ToUpper();
			_whoName.AddThemeColorOverride("font_color", Teams.ColorOf(whoHealth.Team).Lightened(0.2f));
			Node3D shown = _replaying && _copy != null && IsInstanceValid(_copy) ? _copy : who;
			bool wolf = shown.GetNodeOrNull<FormController>("FormController")?.ShowsWolf ?? false;
			_whoForm.Text = wolf ? "COMO LOBO" : "COMO HUMANO";
		}

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
		_title.AddThemeFontSizeOverride("font_size", kill ? 60 : 28);
		_hint.Text = _phase switch
		{
			Phase.Death or Phase.Kill => "ESPAÇO: ESPECTADOR",
			Phase.First => "CLIQUES: TROCAR ALIADO  ·  ESPAÇO: VISTA DE CIMA",
			Phase.Above => "CLIQUES: TROCAR ALIADO  ·  ESPAÇO: CÂMERA LIVRE",
			_ => "WASD E MOUSE  ·  ESPAÇO: VOLTAR AOS ALIADOS"
		};
	}
}
