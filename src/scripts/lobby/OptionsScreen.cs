using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Opções, no jeito do Overwatch (Jhon, 22/11: "não consigo descer. organize
/// isso como overwatch"): abas no alto (Vídeo, Áudio, Controles, Mira, Jogo),
/// um ajuste por faixa — o nome à esquerda, o controle à direita —, a lista
/// rola, e o painel ao lado explica a faixa sob o mouse. As explicações
/// ficavam embaixo de cada ajuste e empurravam a tela para fora sem rolagem.
/// "Restaurar padrão" volta a aba aberta ao de fábrica.
///
/// Tudo aplica na hora e salva em user://settings.json. A mesma vista
/// (<see cref="View"/>) serve à tela do saguão e ao menu do Esc na partida.
/// </summary>
public partial class OptionsScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Options;

	protected override void Build()
	{
		VBoxContainer page = Page();
		Heading(page, "Opções");
		page.AddChild(View(Shell.GoCredits));
	}

	// ------------------------------------------------------------------ a vista

	private static readonly string[] TabNames = { "Vídeo", "Áudio", "Controles", "Mira", "Jogo" };
	private const int VideoTab = 0, AudioTab = 1, ControlsTab = 2, CrosshairTab = 3, GameTab = 4;

	/// <summary>A aba aberta por último: voltar às opções cai nela.</summary>
	private static int _lastTab;

	/// <summary>Abas, a lista que rola, o painel de explicação e o rodapé.
	/// <paramref name="credits"/>: o botão dos créditos (só no saguão).</summary>
	/// <summary>O botão da tela de créditos está escondido por ora (Jhon,
	/// 25/11). A tela e o conteúdo (<c>Credits.cs</c>) continuam; ligue
	/// aqui para voltar. As licenças pedem crédito visível no jogo
	/// publicado (CC BY): religue antes de publicar.</summary>
	public const bool ShowCredits = false;

	public static Control View(Action credits = null) => new Sheet(credits).Root;

	/// <summary>O estado de uma vista aberta: a aba, a lista e a faixa apontada.</summary>
	private sealed class Sheet
	{
		/// <summary>Largura do controle de cada faixa e do painel ao lado.</summary>
		private const int ValueWidth = 400, SideWidth = 360, RowHeight = 46;

		private const string Hint = "Passe o mouse sobre um ajuste para ver o que ele faz. Tudo vale na hora e fica salvo.";

		public readonly VBoxContainer Root;
		private readonly bool _inLobby;
		private readonly ScrollContainer _scroll;
		private readonly VBoxContainer _list;
		private readonly VBoxContainer _preview;
		private readonly Label _title, _body;
		private PanelContainer _current;
		private Func<string> _currentNote;
		private int _tab;

		public Sheet(Action credits)
		{
			_inLobby = credits != null;
			Root = UiKit.Column(UiTokens.S3);
			Root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			Root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

			HBoxContainer top = UiKit.Row(UiTokens.S3);
			top.AddChild(Tabs.Make(TabNames, _lastTab, Show));
			top.AddChild(UiKit.Spacer());
			if (credits != null && ShowCredits) top.AddChild(UiKit.Button("Créditos", ButtonKind.Tertiary, credits));
			Root.AddChild(top);

			HBoxContainer body = UiKit.Row(UiTokens.S4);
			body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			Root.AddChild(body);

			_scroll = new ScrollContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
			};
			body.AddChild(_scroll);
			// Folga à direita para a barra de rolagem não cobrir os controles.
			MarginContainer pad = UiKit.Margin(0, 0, UiTokens.S3, UiTokens.S3);
			pad.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_scroll.AddChild(pad);
			_list = UiKit.Column(UiTokens.S1);
			_list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			pad.AddChild(_list);

			// Ao lado: a prévia da mira (na aba Mira) e o que a faixa apontada faz.
			PanelContainer side = UiKit.Card(out VBoxContainer info, UiTokens.S4, UiTokens.S3);
			side.CustomMinimumSize = new Vector2(SideWidth, 0);
			side.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			body.AddChild(side);
			_preview = UiKit.Column(0);
			info.AddChild(_preview);
			_title = UiKit.Text("", "Subtitle");
			_title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			info.AddChild(_title);
			_body = UiKit.Paragraph("");
			info.AddChild(_body);

			HBoxContainer foot = UiKit.Row(UiTokens.S3);
			foot.AddChild(UiKit.Spacer());
			foot.AddChild(UiKit.Button("Restaurar padrão", ButtonKind.Secondary, Restore));
			Root.AddChild(foot);

			Show(_lastTab);
		}

		// -------------------------------------------------------------- abas

		private void Show(int tab)
		{
			_lastTab = _tab = Math.Clamp(tab, 0, TabNames.Length - 1);
			UiKit.Clear(_list);
			UiKit.Clear(_preview);
			_current = null;
			_currentNote = null;
			_title.Text = TabNames[_tab];
			_body.Text = Hint;

			switch (_tab)
			{
				case VideoTab: Video(); break;
				case AudioTab: Audio(); break;
				case ControlsTab: Controls(); break;
				case CrosshairTab: Crosshair(); break;
				case GameTab: Game(); break;
			}
			_scroll.ScrollVertical = 0;
		}

		/// <summary>Refaz a aba aberta no quadro seguinte (quem chamou é um
		/// controle dela, que sai junto).</summary>
		private void Rebuild() => Callable.From(() => Show(_tab)).CallDeferred();

		private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

		private void Video()
		{
			Settings.Data v = Settings.Values;

			Section("Imagem");
			Row("Suavização", UiKit.Picker(Settings.AntiAliasNames, v.AntiAliasing, i =>
			{
				v.AntiAliasing = i;
				Settings.Save();
				Settings.ApplyVideo(Tree.Root);
				Again();
			}), () => Settings.AntiAliasNotes[Math.Clamp(v.AntiAliasing, 0, Settings.AntiAliasNotes.Length - 1)]);

			Row("Qualidade", UiKit.Picker(Settings.QualityNames, v.Quality, i =>
			{
				v.Quality = i;
				Settings.Save();
				Settings.ApplyQuality(Tree);
				Again();
			}), () => Settings.QualityNotes[Math.Clamp(v.Quality, 0, Settings.QualityNotes.Length - 1)]);

			Row("Resolução 3D", UiKit.Picker(Settings.RenderScaleNames, Settings.RenderScaleIndex(), i =>
			{
				v.RenderScale = Settings.RenderScaleValues[i];
				Settings.Save();
				Settings.ApplyVideo(Tree.Root);
			}), () => "Quanto da tela o jogo desenha em 3D. Menos é mais rápido; o menu e a mira ficam nítidos. A luz não muda. É o que mais ajuda em placa fraca.");

			Row("Resolução dinâmica", Switch(v.DynamicResolution, on =>
			{
				v.DynamicResolution = on;
				Settings.Save();
				Settings.ApplyVideo(Tree.Root);
			}), () => "Quando a placa não segura o limite de FPS, o 3D baixa a resolução sozinho (até 50%) e volta quando sobra. A resolução 3D é o máximo.");

			Section("Quadros");
			Row("Limite de FPS", UiKit.Picker(Settings.FpsNames(), Settings.FpsIndex(), i =>
			{
				v.MaxFps = Settings.FpsValues[i];
				Settings.Save();
				Settings.ApplyFrameRate();
			}), () => "O máximo de quadros por segundo. O do monitor acompanha os Hz dele (60, 75, 144…). Para passar dos Hz, desligue o V-Sync.");

			Row("V-Sync", UiKit.Picker(Settings.VSyncNames, v.VSync, i =>
			{
				v.VSync = i;
				Settings.Save();
				Settings.ApplyFrameRate();
				Again();
			}), () => Settings.VSyncNotes[Math.Clamp(v.VSync, 0, Settings.VSyncNotes.Length - 1)]);

			Row("Mostrar desempenho", Switch(v.ShowPerf, on =>
			{
				v.ShowPerf = on;
				Settings.Save();
			}), () => "O FPS e o ping no canto da tela.");

			// Windows: o modo de vídeo que o iniciador usa (VideoMode). Para quem
			// abriu, mas com defeito na imagem; vale ao abrir o jogo de novo.
			if (VideoMode.Available)
			{
				Section("Sistema");
				Row("Modo de vídeo", UiKit.Picker(VideoMode.Names, VideoMode.Chosen(), i =>
				{
					VideoMode.Choose(i);
					Again();
				}), () => Loc.F("Agora em {0}. A troca vale na próxima vez que o jogo abrir.", VideoMode.Current));
			}
		}

		private void Audio()
		{
			Settings.Data v = Settings.Values;
			Section("Volume");
			Row("Geral", Slider(v.Master, x => { v.Master = x; Changed(); }), () => "O volume de tudo.");
			Row("Efeitos", Slider(v.Sfx, x => { v.Sfx = x; Changed(); }), () => "Tiros, passos, golpes, uivos e explosões.");
			Row("Interface", Slider(v.Interface, x => { v.Interface = x; Changed(); }), () => "Os cliques e avisos dos menus e do HUD.");
			Row("Música e ambiente", Slider(v.Music, x => { v.Music = x; Changed(); }), () => "A música dos menus e o som de fundo dos mapas.");

			static void Changed()
			{
				Settings.ApplyAudio();
				Settings.Save();
			}
		}

		private void Controls()
		{
			Settings.Data v = Settings.Values;
			Section("Mouse");
			Row("Sensibilidade", Slider(v.Sensitivity, x => { v.Sensitivity = x; Settings.Save(); }, 0.25f, 3f, x => $"{x:0.00}×"),
				() => "Quanto a mira gira por centímetro de mouse. Vale também no meio da partida.");

			Section("Teclas");
			foreach (var (key, action) in ControlsTable.Entries)
				Row(action, Key(key), () => Loc.F("Tecla: {0}. Ainda não dá para trocar as teclas.", key));
		}

		private void Crosshair()
		{
			_preview.AddChild(CrosshairPreview());
			_preview.AddChild(UiKit.Gap(UiTokens.S2));

			CrosshairSettings c = Settings.Values.Crosshair;
			Section("Formato");
			Row("Estilo", UiKit.Picker(new[] { "Cruz com ponto", "Cruz", "Ponto" }, (int)c.Style, i =>
			{
				c.Style = (CrosshairSettings.Shape)i;
				Settings.Save();
				Rebuild();
			}), () => "Só o ponto tem um tamanho só: escolhido ele, os ajustes das hastes somem.");

			if (c.Style != CrosshairSettings.Shape.Dot)
			{
				Row("Tamanho", Slider(c.Length, x => { c.Length = x; Settings.Save(); }, 2f, 20f, Pixels, 1), () => "O comprimento de cada haste.");
				Row("Espessura", Slider(c.Thickness, x => { c.Thickness = x; Settings.Save(); }, 1f, 6f, Pixels, 1), () => "A largura das hastes.");
				Row("Espaço no centro", Slider(c.Gap, x => { c.Gap = x; Settings.Save(); }, 0f, 20f, Pixels, 1), () => "A folga entre o centro e o começo das hastes.");
				Row("Abrir ao atirar e ao correr", Switch(c.Dynamic, on => { c.Dynamic = on; Settings.Save(); }),
					() => "A cruz abre com o espalhamento do tiro: ao atirar seguido, andar e pular. Desligada, fica parada.");
			}

			Section("Cor");
			Row("Cor", Colors(c), () => "As prontas, ou uma livre no último quadrado.");
			Row("Opacidade", Slider(c.Opacity, x => { c.Opacity = x; Settings.Save(); }, 0.2f, 1f), () => "O quanto a mira cobre o que está atrás.");
			Row("Contorno escuro", Switch(c.Outline, on => { c.Outline = on; Settings.Save(); }),
				() => "Uma borda escura em volta: a mira lê em fundo claro e escuro.");
		}

		private void Game()
		{
			Settings.Data v = Settings.Values;
			Section("Partida");
			Row("Sangue ao acertar", Switch(v.Blood, on => { v.Blood = on; Settings.Save(); }),
				() => "O respingo de sangue quando o tiro ou a garra acerta.");

			// O idioma: as colunas de assets/i18n/textos.csv. Só entra na lista o
			// idioma que já tem texto traduzido (uma coluna vazia mostraria o jogo
			// inteiro em português com outro nome).
			var languages = new List<string>(Loc.Languages).FindAll(code => code == Loc.Source || Loc.Count(code) > 0);
			if (languages.Count > 1)
			{
				Section("Idioma");
				string[] names = languages.ConvertAll(Loc.NameOf).ToArray();
				Row("Idioma", UiKit.Picker(names, Math.Max(languages.IndexOf(v.Language), 0), i =>
				{
					v.Language = languages[i];
					Settings.Save();
					Loc.Apply(languages[i]);
					// Os textos montados em código só mudam quando a tela é refeita.
					if (_inLobby) LobbyShell.Instance?.GoOptions();
					else Rebuild();
				}), () => "O que ainda não tem tradução aparece em português.");
			}
		}

		/// <summary>A aba aberta de volta ao de fábrica.</summary>
		private void Restore()
		{
			var d = new Settings.Data();
			Settings.Data v = Settings.Values;
			switch (_tab)
			{
				case VideoTab:
					v.AntiAliasing = d.AntiAliasing;
					v.Quality = d.Quality;
					v.RenderScale = d.RenderScale;
					v.DynamicResolution = d.DynamicResolution;
					v.MaxFps = d.MaxFps;
					v.VSync = d.VSync;
					v.ShowPerf = d.ShowPerf;
					Settings.ApplyVideo(Tree.Root);
					Settings.ApplyQuality(Tree);
					Settings.ApplyFrameRate();
					break;
				case AudioTab:
					v.Master = d.Master;
					v.Sfx = d.Sfx;
					v.Interface = d.Interface;
					v.Music = d.Music;
					Settings.ApplyAudio();
					break;
				case ControlsTab:
					v.Sensitivity = d.Sensitivity;
					break;
				case CrosshairTab:
					v.Crosshair = new CrosshairSettings();
					break;
				case GameTab:
					v.Blood = d.Blood;
					break;
			}
			Settings.Save();
			Show(_tab);
		}

		// -------------------------------------------------------------- faixas

		private void Section(string title)
		{
			if (_list.GetChildCount() > 0) _list.AddChild(UiKit.Gap(UiTokens.S3));
			Label caption = UiKit.Caption(title, UiTokens.Accent);
			_list.AddChild(caption);
		}

		/// <summary>Uma faixa: o nome à esquerda, o controle à direita. Sob o
		/// mouse (ou com o foco num controle dela) ela acende e o painel ao
		/// lado mostra <paramref name="note"/>.</summary>
		private void Row(string label, Control value, Func<string> note)
		{
			var row = new PanelContainer { CustomMinimumSize = new Vector2(0, RowHeight), MouseFilter = Control.MouseFilterEnum.Pass };
			row.AddThemeStyleboxOverride("panel", RowBox(false));

			HBoxContainer line = UiKit.Row(UiTokens.S3);
			row.AddChild(line);
			Label name = UiKit.Text(label);
			name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			line.AddChild(name);

			value.CustomMinimumSize = new Vector2(ValueWidth, value.CustomMinimumSize.Y);
			value.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
			value.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			line.AddChild(value);

			void Point() => Describe(row, label, note);
			row.MouseEntered += Point;
			foreach (Control c in value.FindChildren("*", "", true, false).OfType<Control>().Prepend(value))
			{
				c.MouseEntered += Point;
				c.FocusEntered += Point;
			}
			_list.AddChild(row);
		}

		private void Describe(PanelContainer row, string title, Func<string> note)
		{
			if (_current != row)
			{
				if (_current != null && GodotObject.IsInstanceValid(_current)) _current.AddThemeStyleboxOverride("panel", RowBox(false));
				row.AddThemeStyleboxOverride("panel", RowBox(true));
				_current = row;
			}
			_currentNote = note;
			_title.Text = title;
			_body.Text = note();
		}

		/// <summary>O ajuste mudou e a explicação depende dele: refaz o texto.</summary>
		private void Again()
		{
			if (_currentNote != null) _body.Text = _currentNote();
		}

		private static StyleBoxFlat RowBox(bool lit)
		{
			StyleBoxFlat box = UiTheme.Box(lit ? UiTokens.Surface3 : new Color(UiTokens.Surface2, 0.86f),
				default, 0, UiTokens.RadiusSmall, UiTokens.S3, UiTokens.S1);
			if (lit)
			{
				box.BorderColor = UiTokens.Accent;
				box.BorderWidthLeft = 3;
			}
			return box;
		}

		// -------------------------------------------------------------- controles

		/// <summary>Liga e desliga, com as setas dos lados, como no Overwatch.</summary>
		private static Control Switch(bool on, Action<bool> changed)
		{
			HBoxContainer box = UiKit.Row(0);
			Label value = UiKit.Text("");
			value.HorizontalAlignment = HorizontalAlignment.Center;
			value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			void Show()
			{
				value.Text = on ? "Ligado" : "Desligado";
				value.AddThemeColorOverride("font_color", on ? UiTokens.Accent : UiTokens.TextSecondary);
			}
			void Flip()
			{
				on = !on;
				Show();
				changed(on);
			}

			box.AddChild(Arrow("‹", Flip));
			box.AddChild(value);
			box.AddChild(Arrow("›", Flip));
			Show();
			return box;
		}

		private static Button Arrow(string text, Action pressed)
		{
			var button = new Button
			{
				Text = text,
				Flat = true,
				CustomMinimumSize = new Vector2(40, 32),
				FocusMode = Control.FocusModeEnum.All,
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			button.Pressed += pressed;
			Sfx.Wire(button);
			return button;
		}

		/// <summary>O número e o deslizante, como no Overwatch.</summary>
		private static Control Slider(float value, Action<float> changed,
			float min = 0f, float max = 1f, Func<float, string> format = null, double step = 0.01)
		{
			format ??= x => $"{x * 100:0}%";

			HBoxContainer row = UiKit.Row(UiTokens.S3);
			Label shown = UiKit.Text(format(value), "Numeric");
			shown.CustomMinimumSize = new Vector2(64, 0);
			shown.HorizontalAlignment = HorizontalAlignment.Right;
			row.AddChild(shown);

			var slider = new HSlider
			{
				MinValue = min,
				MaxValue = max,
				Step = step,
				Value = value,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				CustomMinimumSize = new Vector2(0, 20),
				FocusMode = Control.FocusModeEnum.All
			};
			row.AddChild(slider);

			slider.ValueChanged += x =>
			{
				shown.Text = format((float)x);
				changed((float)x);
			};
			slider.DragEnded += _ => Sfx.Ui("ui_clique");
			return row;
		}

		/// <summary>A tecla, numa caixinha à direita.</summary>
		private static Control Key(string key)
		{
			HBoxContainer row = UiKit.Row(0);
			row.AddChild(UiKit.Spacer());
			var box = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
			box.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S3, 3));
			box.AddChild(UiKit.Text(key, "Caption", UiTokens.TextPrimary));
			row.AddChild(box);
			return row;
		}

		/// <summary>As cores prontas e uma livre.</summary>
		private Control Colors(CrosshairSettings c)
		{
			HBoxContainer colors = UiKit.Row(UiTokens.S1);
			colors.AddChild(UiKit.Spacer());
			foreach (var (name, hex) in CrosshairSettings.Presets)
			{
				bool chosen = string.Equals(c.Color, hex, StringComparison.OrdinalIgnoreCase);
				var swatch = new Button
				{
					TooltipText = name,
					CustomMinimumSize = new Vector2(30, 30),
					FocusMode = Control.FocusModeEnum.All,
					MouseDefaultCursorShape = Control.CursorShape.PointingHand
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
				swatch.Pressed += () =>
				{
					c.Color = pick;
					Settings.Save();
					Rebuild();
				};
				Sfx.Wire(swatch);
				colors.AddChild(swatch);
			}
			var custom = new ColorPickerButton
			{
				Color = new Color(c.Color),
				EditAlpha = false,
				CustomMinimumSize = new Vector2(48, 30),
				TooltipText = "Outra cor"
			};
			custom.ColorChanged += color => { c.Color = color.ToHtml(false); Settings.Save(); };
			custom.PopupClosed += Rebuild;
			colors.AddChild(custom);
			return colors;
		}

		/// <summary>
		/// A prévia da mira, sobre um fundo meio claro meio escuro (a mira tem
		/// de ler nos dois). Ela abre e fecha como num tiro seguido.
		/// </summary>
		private static Control CrosshairPreview()
		{
			var preview = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0, 190),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			preview.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("5d6b78") });
			var half = new ColorRect { Color = new Color("1a2027"), MouseFilter = Control.MouseFilterEnum.Ignore };
			half.SetAnchorsPreset(Control.LayoutPreset.RightWide);
			half.AnchorLeft = 0.5f;
			var stage = new Control { CustomMinimumSize = new Vector2(0, 190), ClipContents = true };
			stage.AddChild(half);
			var sample = UiKit.Fill(new global::Crosshair());
			stage.AddChild(sample);
			preview.AddChild(stage);

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
			return preview;
		}

		private static string Pixels(float x) => Loc.F("{0:0} px", x);
	}
}
