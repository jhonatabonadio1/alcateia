using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Opções do jogador: volumes por canal, sensibilidade do mouse e a mira
/// (<see cref="CrosshairSettings"/>). Salvas
/// em user://settings.json (<see cref="UserFiles"/>), separadas do perfil — opção é da máquina, não
/// da conta.
/// </summary>
public static class Settings
{
	private static string FilePath => UserFiles.Resolve(UserFiles.Settings, "user://opcoes.json", perAccount: false);

	public sealed class Data
	{
		public float Master { get; set; } = 0.9f;
		public float Sfx { get; set; } = 1f;
		public float Interface { get; set; } = 0.8f;
		public float Music { get; set; } = 0.6f;
		/// <summary>Multiplicador sobre a sensibilidade base do Player.</summary>
		public float Sensitivity { get; set; } = 1f;

		/// <summary>Suavização das bordas (anti-aliasing), um de
		/// <see cref="AntiAliasNames"/>. MSAA 4× por padrão.</summary>
		public int AntiAliasing { get; set; } = 3;

		/// <summary>Qualidade dos efeitos, um de <see cref="QualityNames"/>.
		/// Alta por padrão.</summary>
		public int Quality { get; set; } = 2;

		/// <summary>Quanto da tela o 3D desenha (0,5 a 1): menos pixels
		/// para a placa, o HUD segue inteiro. A luz não muda.</summary>
		public float RenderScale { get; set; } = 1f;

		/// <summary>Já olhou a placa de vídeo uma vez (<see cref="AutoTune"/>).
		/// Arquivo antigo sem o campo olha na próxima abertura.</summary>
		public bool GpuChecked { get; set; }

		/// <summary>A resolução do 3D desce sozinha quando a placa não segura
		/// a meta de FPS (<see cref="ResolutionTuner"/>), até 50%, e volta
		/// quando sobra. <see cref="RenderScale"/> é o teto. Ligada por padrão:
		/// em máquina que aguenta, fica no teto e nada muda.</summary>
		public bool DynamicResolution { get; set; } = true;

		/// <summary>Limite de FPS: -1 o do monitor (Hz), 0 sem limite, ou o
		/// número. Ver <see cref="FpsValues"/>.</summary>
		public int MaxFps { get; set; } = -1;

		/// <summary>V-Sync, um de <see cref="VSyncNames"/>. Ligado por padrão
		/// (o padrão do Godot, o que o jogo sempre usou).</summary>
		public int VSync { get; set; } = 1;

		/// <summary>Mostrar no canto o FPS, o tempo da placa, a resolução
		/// do 3D e o ping (<see cref="PerfOverlay"/>).</summary>
		public bool ShowPerf { get; set; }

		/// <summary>Sangue ao acertar tiro e garra (<see cref="Blood"/>).
		/// Ligado por padrão; arquivo antigo sem o campo, também.</summary>
		public bool Blood { get; set; } = true;

		/// <summary>O idioma dos textos, um dos de <see cref="Loc.Languages"/>
		/// (as colunas de assets/i18n/textos.csv). Português por padrão,
		/// seja qual for o idioma do sistema.</summary>
		public string Language { get; set; } = Loc.Source;

		/// <summary>A mira do jogador. Arquivo antigo sem ela: a de sempre.</summary>
		public CrosshairSettings Crosshair { get; set; } = new();

		/// <summary>As teclas trocadas nas Opções → Controles: a ação e a tecla no
		/// texto curto do <see cref="KeyBindings"/> ("K:87", "M:1"). Só o que difere
		/// do padrão; arquivo antigo sem o campo, as de fábrica.</summary>
		public System.Collections.Generic.Dictionary<string, string> Keys { get; set; } = new();
	}

	public static Data Values { get; private set; } = new();

	public static event Action Changed;

	static Settings() => Load();

	public static void Load()
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(FilePath);
			if (System.IO.File.Exists(path))
				Values = JsonSerializer.Deserialize<Data>(System.IO.File.ReadAllText(path)) ?? new Data();
			Values.Crosshair ??= new CrosshairSettings();
			Values.Crosshair.Clamp();
			Values.Keys ??= new System.Collections.Generic.Dictionary<string, string>();
		}
		catch (Exception error)
		{
			GD.PrintErr($"Settings: could not read ({error.Message}); using defaults.");
			Values = new Data();
		}
	}

	public static void Save()
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(FilePath);
			System.IO.File.WriteAllText(path, JsonSerializer.Serialize(Values, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception error)
		{
			GD.PrintErr($"Settings: could not save ({error.Message}).");
		}

		Changed?.Invoke();
	}

	/// <summary>As opções de suavização, na ordem salva.</summary>
	public static readonly string[] AntiAliasNames =
		{ "Desligada", "FXAA (leve)", "MSAA 2×", "MSAA 4×", "MSAA 4× + FXAA", "TAA (mais lisa)" };

	/// <summary>O que cada opção custa e como fica.</summary>
	public static readonly string[] AntiAliasNotes =
	{
		"Bordas serrilhadas. A mais leve.",
		"Filtro na tela inteira: quase de graça, mas borra um pouco.",
		"Bordas lisas nos objetos, custo baixo.",
		"Bordas lisas nos objetos. O padrão.",
		"MSAA 4× e o filtro por cima, que pega também folha e grade.",
		"A mais lisa, também em folha e grade; deixa um leve rastro no que se move rápido."
	};

	public static readonly string[] QualityNames = { "Baixa", "Média", "Alta", "Ultra" };

	public static readonly string[] QualityNotes =
	{
		"A mesma luz das outras, mais barata: sombra simples e os efeitos com poucas amostras. Para máquina fraca.",
		"A mesma luz, sombra suave e os efeitos com menos amostras.",
		"A mesma luz, sombra mais nítida e os efeitos inteiros. O padrão.",
		"A mesma luz, sombra mais fina e os efeitos na resolução cheia."
	};

	/// <summary>
	/// Aplica a qualidade. **A luz é a mesma nas quatro** (pedido do Jhon,
	/// 20/11: "regule o gráfico baixo, médio, alto e ultra para a luz ficar
	/// padrão em todos, não pode ficar mais claro"): todo efeito que muda a
	/// luz do quadro — oclusão nos cantos (SSAO), luz que rebate na tela
	/// (SSIL), luz rebatida do mapa (SDFGI), névoa com raio de sol e brilho
	/// (glow) — fica ligado em todas, como o mapa pediu. Antes a Baixa
	/// desligava a oclusão e a Média a luz rebatida, e o mapa clareava (sem
	/// oclusão o canto não escurece; sem SDFGI a sala volta a receber o céu).
	/// O que muda de uma para outra é só o custo: o tamanho e o filtro das
	/// sombras, e as amostras e a resolução de cada efeito
	/// (<see cref="ApplyEffectCost"/>). A referência é a Alta, o padrão.
	/// </summary>
	public static void ApplyQuality(SceneTree tree)
	{
		int q = Math.Clamp(Values.Quality, 0, QualityNames.Length - 1);

		RenderingServer.DirectionalShadowAtlasSetSize(q switch { 0 => 2048, 3 => 8192, _ => 4096 }, true);
		var filter = q switch
		{
			0 => RenderingServer.ShadowQuality.Hard,
			1 => RenderingServer.ShadowQuality.SoftLow,
			2 => RenderingServer.ShadowQuality.SoftMedium,
			_ => RenderingServer.ShadowQuality.SoftHigh
		};
		RenderingServer.DirectionalSoftShadowFilterSetQuality(filter);
		RenderingServer.PositionalSoftShadowFilterSetQuality(filter);
		ApplyEffectCost(q);

		if (tree?.Root == null) return;
		tree.Root.MeshLodThreshold = q == 0 ? 2f : q == 3 ? 0.5f : 1f;
		// As sombras das luminárias (focos) dividem este atlas: maior, mais nítidas.
		tree.Root.PositionalShadowAtlasSize = q switch { 0 => 2048, 3 => 8192, _ => 4096 };
		foreach (Node node in tree.Root.FindChildren("*", nameof(WorldEnvironment), true, false))
			if (node is WorldEnvironment world) ApplyQuality(world.Environment);
	}

	/// <summary>
	/// O custo dos efeitos, para todos os mapas (no motor): amostras,
	/// meia ou inteira resolução, passadas de desfoque, raios da luz
	/// rebatida e o tamanho da grade da névoa. Nada disto muda quanto o
	/// efeito escurece ou clareia, só o quanto ele é fino. A Alta usa os
	/// valores padrão do Godot.
	/// </summary>
	private static void ApplyEffectCost(int q)
	{
		var ssao = q switch
		{
			0 => RenderingServer.EnvironmentSsaoQuality.VeryLow,
			1 => RenderingServer.EnvironmentSsaoQuality.Low,
			2 => RenderingServer.EnvironmentSsaoQuality.Medium,
			_ => RenderingServer.EnvironmentSsaoQuality.High
		};
		RenderingServer.EnvironmentSetSsaoQuality(ssao, q < 3, 0.5f, 2, 50f, 300f);

		var ssil = q switch
		{
			0 => RenderingServer.EnvironmentSsilQuality.VeryLow,
			1 => RenderingServer.EnvironmentSsilQuality.Low,
			2 => RenderingServer.EnvironmentSsilQuality.Medium,
			_ => RenderingServer.EnvironmentSsilQuality.High
		};
		RenderingServer.EnvironmentSetSsilQuality(ssil, q < 3, 0.5f, 4, 50f, 300f);

		RenderingServer.EnvironmentSetSdfgiRayCount(q switch
		{
			0 => RenderingServer.EnvironmentSdfgiRayCount.Count4,
			1 => RenderingServer.EnvironmentSdfgiRayCount.Count8,
			2 => RenderingServer.EnvironmentSdfgiRayCount.Count16,
			_ => RenderingServer.EnvironmentSdfgiRayCount.Count32
		});
		RenderingServer.GISetUseHalfResolution(q < 2);

		int fog = q switch { 0 => 32, 1 => 48, 2 => 64, _ => 96 };
		RenderingServer.EnvironmentSetVolumetricFogVolumeSize(fog, fog);
		RenderingServer.EnvironmentSetVolumetricFogFilterActive(q > 0);
	}

	/// <summary>Os efeitos de um ambiente: os que o mapa pediu, em todas as
	/// qualidades (o que a qualidade muda é o custo, em
	/// <see cref="ApplyEffectCost"/>). Guarda, na primeira vez, o que o
	/// mapa pediu, porque as versões antigas desligavam efeitos aqui.</summary>
	public static void ApplyQuality(Godot.Environment env)
	{
		if (env == null) return;

		if (!env.HasMeta("mapa_ssao"))
		{
			env.SetMeta("mapa_ssao", env.SsaoEnabled);
			env.SetMeta("mapa_glow", env.GlowEnabled);
		}
		if (!env.HasMeta("mapa_sdfgi"))
		{
			env.SetMeta("mapa_sdfgi", env.SdfgiEnabled);
			env.SetMeta("mapa_nevoa", env.VolumetricFogEnabled);
		}
		bool ssao = env.GetMeta("mapa_ssao").AsBool();

		env.SsaoEnabled = ssao;
		env.GlowEnabled = env.GetMeta("mapa_glow").AsBool();
		env.SdfgiEnabled = env.GetMeta("mapa_sdfgi").AsBool();
		env.VolumetricFogEnabled = env.GetMeta("mapa_nevoa").AsBool();
		// A luz que rebate na tela, como a Alta sempre pôs (em todo mapa com
		// oclusão, a 0,8): era o que se via no padrão.
		env.SsilEnabled = ssao;
		env.SsilIntensity = 0.8f;
		// O reflexo na tela (SSR) era só da Ultra e clareava o chão: sai,
		// para a Ultra ter a mesma luz das outras.
		env.SsrEnabled = false;
	}

	/// <summary>Aplica a suavização na tela do jogo (a janela inteira:
	/// vale do menu à partida, sem recarregar nada).</summary>
	public static void ApplyVideo(Viewport view)
	{
		if (view == null) return;
		int mode = Math.Clamp(Values.AntiAliasing, 0, AntiAliasNames.Length - 1);

		view.Msaa3D = mode switch
		{
			2 => Viewport.Msaa.Msaa2X,
			3 or 4 => Viewport.Msaa.Msaa4X,
			_ => Viewport.Msaa.Disabled
		};
		view.ScreenSpaceAA = mode is 1 or 4 ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
		view.UseTaa = mode == 5;

		// Resolução do 3D: FSR 1 devolve a nitidez (só no Forward+/Mobile; na
		// compatibilidade, filtro simples). O HUD não escala.
		// Com a resolução dinâmica, quem mexe na escala é o ResolutionTuner;
		// aqui ela só não passa do teto.
		float max = Math.Clamp(Values.RenderScale, 0.5f, 1f);
		float scale = Values.DynamicResolution ? Math.Min(view.Scaling3DScale, max) : max;
		bool compat = RenderingServer.GetCurrentRenderingMethod() == "gl_compatibility";
		view.Scaling3DScale = scale;
		view.Scaling3DMode = compat || (!Values.DynamicResolution && scale >= 0.999f)
			? Viewport.Scaling3DModeEnum.Bilinear
			: Viewport.Scaling3DModeEnum.Fsr;
	}

	/// <summary>As opções de limite de FPS, na ordem do seletor: o do
	/// monitor, números fixos e sem limite.</summary>
	public static readonly int[] FpsValues = { -1, 30, 60, 75, 120, 144, 165, 240, 0 };

	public static string[] FpsNames()
	{
		var names = new string[FpsValues.Length];
		for (int i = 0; i < FpsValues.Length; i++)
			names[i] = FpsValues[i] switch
			{
				-1 => $"{Loc.T("Monitor")} ({MonitorHz} Hz)",
				0 => Loc.T("Sem limite"),
				var n => $"{n} FPS"
			};
		return names;
	}

	public static int FpsIndex() => Math.Max(0, Array.IndexOf(FpsValues, Values.MaxFps));

	public static readonly string[] VSyncNames = { "Desligado", "Ligado", "Adaptativo" };

	public static readonly string[] VSyncNotes =
	{
		"A placa entrega o quadro na hora: menos atraso no mouse, mas a imagem pode rasgar.",
		"Sem rasgo na imagem; o FPS não passa dos Hz do monitor. O padrão.",
		"Como o ligado, mas quando o FPS cai abaixo dos Hz do monitor ele desliga, para não travar."
	};

	/// <summary>Os Hz do monitor da janela (60 se o sistema não disser).</summary>
	public static int MonitorHz
	{
		get
		{
			double hz = DisplayServer.ScreenGetRefreshRate(DisplayServer.WindowGetCurrentScreen());
			return hz < 30 ? 60 : (int)Math.Round(hz);
		}
	}

	/// <summary>O FPS que o jogo tenta segurar: o limite escolhido ou, sem
	/// número, os Hz do monitor. A resolução dinâmica mira nele.</summary>
	public static int FrameTarget => Values.MaxFps > 0 ? Values.MaxFps : MonitorHz;

	/// <summary>Aplica o limite de FPS e o V-Sync (só no jogo; o servidor de
	/// partida tem o limite dele).</summary>
	public static void ApplyFrameRate()
	{
		Engine.MaxFps = Values.MaxFps < 0 ? MonitorHz : Values.MaxFps;
		DisplayServer.WindowSetVsyncMode(Math.Clamp(Values.VSync, 0, 2) switch
		{
			0 => DisplayServer.VSyncMode.Disabled,
			2 => DisplayServer.VSyncMode.Adaptive,
			_ => DisplayServer.VSyncMode.Enabled
		});
	}

	/// <summary>As opções de resolução do 3D, na ordem do seletor.</summary>
	public static readonly string[] RenderScaleNames = { "100%", "85%", "75%", "65%", "50%" };
	public static readonly float[] RenderScaleValues = { 1f, 0.85f, 0.75f, 0.65f, 0.5f };

	public static int RenderScaleIndex()
	{
		int best = 0;
		for (int i = 1; i < RenderScaleValues.Length; i++)
			if (Math.Abs(RenderScaleValues[i] - Values.RenderScale) < Math.Abs(RenderScaleValues[best] - Values.RenderScale)) best = i;
		return best;
	}

	/// <summary>Placas que não seguram o jogo na Alta em tela cheia: MX, GT
	/// de entrada e vídeo integrado.</summary>
	private static readonly System.Text.RegularExpressions.Regex WeakGpu = new(
		@"\bMX\s?[1-3]\d\d\b|\bGT\s?[1-9]\d\d\b|\bGeForce\s?(8|9)\d\d|Intel.*(HD|UHD|Iris|Graphics)|Radeon.*(Vega\s?\d|Graphics)|Microsoft Basic|llvmpipe|SwiftShader",
		System.Text.RegularExpressions.RegexOptions.IgnoreCase);

	/// <summary>Na primeira abertura (e uma vez só), se a placa é fraca, põe
	/// as opções de custo no mínimo: qualidade Baixa, FXAA e 3D a 65%. O
	/// jogador muda quando quiser nas Opções.</summary>
	public static void AutoTune()
	{
		if (Values.GpuChecked) return;
		Values.GpuChecked = true;
		string gpu = RenderingServer.GetVideoAdapterName() ?? "";
		bool weak = WeakGpu.IsMatch(gpu);
		GD.Print($"GPU: {gpu} ({(weak ? "weak: settings lowered to minimum" : "ok")})");
		if (weak)
		{
			Values.Quality = 0;
			Values.AntiAliasing = 1;
			Values.RenderScale = 0.65f;
			Values.DynamicResolution = true;
		}
		Save();
	}

	/// <summary>Aplica os volumes nos canais de áudio. Os canais são
	/// criados pelo Sfx; chamar antes disso é seguro (o Sfx cria).</summary>
	public static void ApplyAudio()
	{
		Sfx.EnsureBuses();
		SetBus("Master", Values.Master);
		SetBus(Sfx.BusSfx, Values.Sfx);
		SetBus(Sfx.BusUi, Values.Interface);
		SetBus(Sfx.BusMusic, Values.Music);
	}

	private static void SetBus(string name, float linear)
	{
		int index = AudioServer.GetBusIndex(name);
		if (index < 0) return;
		AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
		AudioServer.SetBusMute(index, linear <= 0.001f);
	}
}

/// <summary>O desenho da mira (decisão do Jhon, 13/11: o jogador monta a
/// dele). O padrão é a mira de sempre: cruz branca com ponto, contorno
/// escuro e abertura com a dispersão.</summary>
public sealed class CrosshairSettings
{
	public enum Shape
	{
		/// <summary>As quatro hastes e o ponto no meio.</summary>
		CrossDot = 0,
		/// <summary>Só as quatro hastes.</summary>
		Cross = 1,
		/// <summary>Só o ponto, de um tamanho só (<see cref="DotSize"/>).</summary>
		Dot = 2
	}

	/// <summary>Lado do ponto, em pixels. Fixo de propósito: o ponto é um
	/// só para todo mundo.</summary>
	public const float DotSize = 4f;

	public Shape Style { get; set; } = Shape.CrossDot;

	/// <summary>Cor em hexadecimal (RRGGBB).</summary>
	public string Color { get; set; } = "ffffff";

	public float Opacity { get; set; } = 0.92f;

	/// <summary>As hastes se afastam com a dispersão da arma (correr,
	/// atirar seguido). Desligado, a mira fica parada no tamanho de
	/// <see cref="Gap"/> — a dispersão continua valendo no tiro.</summary>
	public bool Dynamic { get; set; } = true;

	/// <summary>Tamanho de cada haste, em pixels.</summary>
	public float Length { get; set; } = 8f;

	/// <summary>Espessura das hastes, em pixels.</summary>
	public float Thickness { get; set; } = 2f;

	/// <summary>Espaço entre o centro e o começo das hastes, em pixels.</summary>
	public float Gap { get; set; } = 5f;

	/// <summary>Contorno escuro de 1 px em volta, para ler sobre fundo claro.</summary>
	public bool Outline { get; set; } = true;

	/// <summary>As cores prontas do seletor: nome e RRGGBB.</summary>
	public static readonly (string Name, string Hex)[] Presets =
	{
		("Branco", "ffffff"), ("Verde", "3cff5a"), ("Amarelo", "ffe23c"),
		("Ciano", "3ce6ff"), ("Magenta", "ff3cdc"), ("Vermelho", "ff3c3c")
	};

	public Godot.Color Tint
	{
		get
		{
			Godot.Color c = Godot.Color.FromString(Color ?? "", Colors.White);
			c.A = Opacity;
			return c;
		}
	}

	/// <summary>Valores fora do que o menu deixa escolher (arquivo editado
	/// à mão) voltam para dentro.</summary>
	public void Clamp()
	{
		if (!System.Enum.IsDefined(Style)) Style = Shape.CrossDot;
		Opacity = Mathf.Clamp(Opacity, 0.2f, 1f);
		Length = Mathf.Clamp(Length, 2f, 20f);
		Thickness = Mathf.Clamp(Thickness, 1f, 6f);
		Gap = Mathf.Clamp(Gap, 0f, 20f);
		if (!Godot.Color.HtmlIsValid(Color ?? "")) Color = "ffffff";
	}
}
