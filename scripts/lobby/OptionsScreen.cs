using Godot;
using System;

/// <summary>
/// Opções: volume por canal, sensibilidade do mouse, a mira, a tabela
/// de controles e recomeçar o perfil. Tudo aplica na hora e salva em
/// user://opcoes.json.
/// </summary>
public partial class OptionsScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Options;

	protected override void Build()
	{
		VBoxContainer page = Page();

		HBoxContainer head = Heading(page, "Opções");
		head.AddChild(UiKit.Button("Créditos", ButtonKind.Secondary, Shell.GoCredits));

		HBoxContainer columns = UiKit.Row(UiTokens.S4);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(columns);

		VBoxContainer left = UiKit.Column(UiTokens.S4);
		left.CustomMinimumSize = new Vector2(480, 0);
		columns.AddChild(left);
		left.AddChild(AudioPanel());
		left.AddChild(MousePanel());
		left.AddChild(ProfilePanel());

		// A mira numa coluna própria: embaixo das outras não cabia na tela.
		VBoxContainer middle = UiKit.Column(UiTokens.S4);
		middle.CustomMinimumSize = new Vector2(540, 0);
		columns.AddChild(middle);
		middle.AddChild(CrosshairPanel());

		columns.AddChild(ControlsPanel());
	}

	// ------------------------------------------------------------------ painéis

	public static Control AudioPanel()
	{
		(PanelContainer panel, VBoxContainer column) = Section("Áudio");
		Settings.Data v = Settings.Values;

		column.AddChild(Slider("Geral", v.Master, x => { v.Master = x; Changed(); }));
		column.AddChild(Slider("Efeitos", v.Sfx, x => { v.Sfx = x; Changed(); }));
		column.AddChild(Slider("Interface", v.Interface, x => { v.Interface = x; Changed(); }));
		column.AddChild(Slider("Música e ambiente", v.Music, x => { v.Music = x; Changed(); }));
		return panel;

		static void Changed()
		{
			Settings.ApplyAudio();
			Settings.Save();
		}
	}

	public static Control MousePanel()
	{
		(PanelContainer panel, VBoxContainer column) = Section("Mouse");
		Settings.Data v = Settings.Values;
		column.AddChild(Slider("Sensibilidade", v.Sensitivity, x => { v.Sensitivity = x; Settings.Save(); },
			0.25f, 3f, x => $"{x:0.00}×"));
		column.AddChild(UiKit.Paragraph("Vale também no meio da partida.", "Caption"));
		return panel;
	}

	/// <summary>
	/// A mira: estilo, cor, tamanho, espessura, espaço, contorno, opacidade
	/// e a abertura ao atirar, com a prévia ao lado — nela a mira abre e
	/// fecha como num tiro seguido. Só o ponto tem um tamanho só, então
	/// escolher "Ponto" esconde os ajustes das hastes.
	/// </summary>
	public static Control CrosshairPanel()
	{
		(PanelContainer panel, VBoxContainer column) = Section("Mira");
		HBoxContainer body = UiKit.Row(UiTokens.S4);
		column.AddChild(body);

		// A prévia, sobre um fundo meio claro meio escuro: a mira tem de
		// ler nos dois.
		var preview = new PanelContainer { CustomMinimumSize = new Vector2(150, 150), SizeFlagsVertical = SizeFlags.ShrinkBegin };
		preview.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("5d6b78") });
		var half = new ColorRect { Color = new Color("1a2027"), MouseFilter = Control.MouseFilterEnum.Ignore };
		half.SetAnchorsPreset(Control.LayoutPreset.RightWide);
		half.AnchorLeft = 0.5f;
		var stage = new Control { CustomMinimumSize = new Vector2(150, 150), ClipContents = true };
		stage.AddChild(half);
		var sample = UiKit.Fill(new Crosshair());
		stage.AddChild(sample);
		preview.AddChild(stage);
		body.AddChild(preview);

		var pulse = new Timer { WaitTime = 0.05, Autostart = true };
		double clock = 0;
		pulse.Timeout += () =>
		{
			clock += 0.05;
			// Tiro seguido por 0,8 s, depois a mira recupera.
			double t = clock % 2.0;
			sample.PreviewSpread = t < 0.8 ? (float)(1.0 + 3.0 * t) : 0f;
		};
		stage.AddChild(pulse);

		VBoxContainer controls = UiKit.Column(UiTokens.S2);
		controls.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.AddChild(controls);
		FillCrosshair(controls);
		return panel;
	}

	private static void FillCrosshair(VBoxContainer controls)
	{
		CrosshairSettings c = Settings.Values.Crosshair;
		void Rebuild()
		{
			Settings.Save();
			Callable.From(() =>
			{
				UiKit.Clear(controls);
				FillCrosshair(controls);
			}).CallDeferred();
		}

		controls.AddChild(Labeled("Estilo", UiKit.Picker(new[] { "Cruz com ponto", "Cruz", "Ponto" }, (int)c.Style, i =>
		{
			c.Style = (CrosshairSettings.Shape)i;
			Rebuild();
		})));

		// Cor: as prontas e uma livre.
		HBoxContainer colors = UiKit.Row(UiTokens.S1);
		foreach (var (name, hex) in CrosshairSettings.Presets)
		{
			bool chosen = string.Equals(c.Color, hex, StringComparison.OrdinalIgnoreCase);
			var swatch = new Button
			{
				TooltipText = name,
				CustomMinimumSize = new Vector2(26, 26),
				FocusMode = FocusModeEnum.All,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			var box = new StyleBoxFlat
			{
				BgColor = new Color(hex),
				BorderColor = chosen ? UiTokens.TextPrimary : new Color(0, 0, 0, 0.6f)
			};
			box.SetBorderWidthAll(chosen ? 3 : 1);
			box.SetCornerRadiusAll(UiTokens.RadiusSmall);
			foreach (string state in new[] { "normal", "hover", "pressed", "focus" }) swatch.AddThemeStyleboxOverride(state, box);
			string pick = hex;
			swatch.Pressed += () => { c.Color = pick; Rebuild(); };
			Sfx.Wire(swatch);
			colors.AddChild(swatch);
		}
		var custom = new ColorPickerButton
		{
			Color = new Color(c.Color),
			EditAlpha = false,
			CustomMinimumSize = new Vector2(44, 26),
			TooltipText = "Outra cor"
		};
		custom.ColorChanged += color => { c.Color = color.ToHtml(false); Settings.Save(); };
		custom.PopupClosed += Rebuild;
		colors.AddChild(custom);
		controls.AddChild(Labeled("Cor", colors));

		controls.AddChild(Slider("Opacidade", c.Opacity, x => { c.Opacity = x; Settings.Save(); }, 0.2f, 1f));

		if (c.Style != CrosshairSettings.Shape.Dot)
		{
			controls.AddChild(Slider("Tamanho", c.Length, x => { c.Length = x; Settings.Save(); }, 2f, 20f, Pixels, 1));
			controls.AddChild(Slider("Espessura", c.Thickness, x => { c.Thickness = x; Settings.Save(); }, 1f, 6f, Pixels, 1));
			controls.AddChild(Slider("Espaço no centro", c.Gap, x => { c.Gap = x; Settings.Save(); }, 0f, 20f, Pixels, 1));
			controls.AddChild(UiKit.Check("Abrir ao atirar e ao correr", c.Dynamic, on => { c.Dynamic = on; Settings.Save(); }));
		}
		else
		{
			controls.AddChild(UiKit.Paragraph("O ponto tem um tamanho só.", "Caption"));
		}

		controls.AddChild(UiKit.Check("Contorno escuro", c.Outline, on => { c.Outline = on; Settings.Save(); }));

		Button reset = UiKit.Button("Mira padrão", ButtonKind.Secondary, () =>
		{
			Settings.Values.Crosshair = new CrosshairSettings();
			Settings.Save();
			Callable.From(() =>
			{
				UiKit.Clear(controls);
				FillCrosshair(controls);
			}).CallDeferred();
		});
		reset.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		controls.AddChild(reset);

		static string Pixels(float x) => $"{x:0} px";
	}

	/// <summary>Um rótulo e o controle, no mesmo alinhamento dos deslizantes.</summary>
	private static Control Labeled(string label, Control value)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		Label name = UiKit.Text(label, "Muted");
		name.CustomMinimumSize = new Vector2(LabelWidth, 0);
		row.AddChild(name);
		value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(value);
		return row;
	}

	private Control ProfilePanel()
	{
		(PanelContainer panel, VBoxContainer column) = Section("Perfil");
		column.AddChild(UiKit.Paragraph("O perfil fica salvo neste computador. Recomeçar apaga patente, XP, Presas, inventário, conjuntos e missões."));

		Button reset = UiKit.Button("Recomeçar perfil", ButtonKind.Danger, ConfirmReset);
		reset.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		column.AddChild(reset);
		return panel;
	}

	private static Control ControlsPanel()
	{
		(PanelContainer panel, VBoxContainer column) = Section("Controles");
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		column.AddChild(ControlsTable.Build());
		return panel;
	}

	private void ConfirmReset()
	{
		Label body = UiKit.Paragraph("Tudo volta ao começo: Filhote I, 3.000 Presas, só o equipamento inicial. Não dá para desfazer.");
		body.CustomMinimumSize = new Vector2(400, 0);

		Shell.Modal("Recomeçar perfil?", body,
			new Modal.Choice("Recomeçar", ButtonKind.Danger, () =>
			{
				Profile.Reset();
				RoomDirectory.Current = null;
				Shell.Go(new WelcomeScreen());
			}),
			new Modal.Choice("Cancelar", ButtonKind.Primary, null));
	}

	// ------------------------------------------------------------------ peças

	private static (PanelContainer, VBoxContainer) Section(string title)
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column);
		column.AddChild(UiKit.Caption(title));
		return (panel, column);
	}

	/// <summary>Largura dos rótulos à esquerda dos controles.</summary>
	private const int LabelWidth = 130;

	private static Control Slider(string label, float value, Action<float> changed,
		float min = 0f, float max = 1f, Func<float, string> format = null, double step = 0.01)
	{
		format ??= x => $"{x * 100:0}%";

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		Label name = UiKit.Text(label, "Muted");
		name.CustomMinimumSize = new Vector2(LabelWidth, 0);
		row.AddChild(name);

		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = value,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(0, 20),
			FocusMode = FocusModeEnum.All
		};
		row.AddChild(slider);

		Label shown = UiKit.Text(format(value), "Numeric");
		shown.CustomMinimumSize = new Vector2(64, 0);
		shown.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(shown);

		slider.ValueChanged += x =>
		{
			shown.Text = format((float)x);
			changed((float)x);
		};
		slider.DragEnded += _ => Sfx.Ui("ui_clique");
		return row;
	}
}
