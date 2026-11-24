using Godot;

/// <summary>
/// O que o espectador escreve por cima da tela de quem se vê (o HUD dele é
/// o do jogo, <c>KillCam.Pov.cs</c>): no alto, ao lado do minimapa,
/// "ABATIDO POR" (no segundo do corpo caindo) ou "ASSISTINDO" e o nome;
/// no meio de baixo, acima do HUD dele, quando você renasce, "ESPECTADOR"
/// e as teclas.
/// </summary>
public partial class KillCam
{
	private CanvasLayer _hudLayer;
	private Label _whoCaption, _whoName, _whoForm, _respawnCaption, _respawnNumber, _title, _hint;

	private void BuildHud()
	{
		_hudLayer = new CanvasLayer { Layer = 8 };
		AddChild(_hudLayer);
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_hudLayer.AddChild(root);

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

		_title = HudStyle.Number(UiKit.Text("", "Display"), 28, UiTokens.TextPrimary);
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(_title);
		_hint = HudStyle.Outlined(UiKit.Caption("", UiTokens.Accent));
		_hint.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(_hint);
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
		bool watching = _phase is Phase.First or Phase.Above && _mate != null && IsInstanceValid(_mate);

		// Quem: no segundo do corpo caindo, quem te matou; depois, quem se vê.
		Node3D who = watching ? _mate : _phase == Phase.Death ? _killer : null;
		Health whoHealth = who != null && IsInstanceValid(who) ? who.GetNodeOrNull<Health>("Health") : null;
		_whoCaption.Visible = _whoName.Visible = _whoForm.Visible = whoHealth != null;
		if (whoHealth != null)
		{
			_whoCaption.Text = watching ? "ASSISTINDO" : "ABATIDO POR";
			_whoName.Text = whoHealth.PublicName.ToUpper();
			_whoName.AddThemeColorOverride("font_color", Teams.ColorOf(whoHealth.Team).Lightened(0.2f));
			bool wolf = who.GetNodeOrNull<FormController>("FormController")?.ShowsWolf ?? false;
			_whoForm.Text = wolf ? "COMO LOBO" : "COMO HUMANO";
		}

		// Quando renasce.
		float left = _victim?.GetNodeOrNull<Health>("Health")?.RespawnIn ?? 0f;
		_respawnCaption.Text = left > 0.05f ? "RENASCE EM" : "PRONTO PARA RENASCER";
		_respawnNumber.Text = left > 0.05f ? Mathf.CeilToInt(left).ToString() : "";

		// O título e as teclas.
		_title.Text = _phase == Phase.Death ? "" : "ESPECTADOR";
		_hint.Text = _phase switch
		{
			Phase.Death => "ESPAÇO: ESPECTADOR",
			Phase.First => "CLIQUES: TROCAR ALIADO  ·  ESPAÇO: VISTA DE CIMA",
			Phase.Above => "CLIQUES: TROCAR ALIADO  ·  ESPAÇO: CÂMERA LIVRE",
			_ => "WASD E MOUSE  ·  ESPAÇO: VOLTAR AOS ALIADOS"
		};
	}
}
