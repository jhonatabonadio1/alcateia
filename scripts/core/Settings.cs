using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Opções do jogador: volumes por canal, sensibilidade do mouse e a mira
/// (<see cref="CrosshairSettings"/>). Salvas
/// em user://opcoes.json, separadas do perfil — opção é da máquina, não
/// da conta.
/// </summary>
public static class Settings
{
	private const string FilePath = "user://opcoes.json";

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

		/// <summary>Sangue ao acertar tiro e garra (<see cref="Blood"/>).
		/// Ligado por padrão; arquivo antigo sem o campo, também.</summary>
		public bool Blood { get; set; } = true;

		/// <summary>O idioma dos textos, um dos de <see cref="Loc.Languages"/>
		/// (as colunas de assets/i18n/textos.csv). Português por padrão,
		/// seja qual for o idioma do sistema.</summary>
		public string Language { get; set; } = Loc.Source;

		/// <summary>A mira do jogador. Arquivo antigo sem ela: a de sempre.</summary>
		public CrosshairSettings Crosshair { get; set; } = new();
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
		}
		catch (Exception error)
		{
			GD.PrintErr($"Opções: não consegui ler ({error.Message}); usando o padrão.");
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
			GD.PrintErr($"Opções: não consegui salvar ({error.Message}).");
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
		"Sombra simples, sem oclusão nem brilho. Para máquina fraca.",
		"Sombra suave, oclusão nos cantos e brilho das luzes.",
		"Mais a luz que rebate nas paredes e sombra mais nítida. O padrão.",
		"Mais reflexo no chão e nas superfícies lisas, e a sombra mais fina."
	};

	/// <summary>
	/// Aplica a qualidade: o tamanho e o filtro das sombras (no motor,
	/// para todos os mapas) e os efeitos de cada ambiente aberto —
	/// oclusão nos cantos (SSAO), luz que rebate (SSIL), reflexo na tela
	/// (SSR) e brilho (glow). Os efeitos só ligam onde o mapa os quer: um
	/// mapa sem brilho continua sem brilho na Ultra.
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

		if (tree?.Root == null) return;
		tree.Root.MeshLodThreshold = q == 0 ? 2f : q == 3 ? 0.5f : 1f;
		// As sombras das luminárias (focos) dividem este atlas: maior, mais nítidas.
		tree.Root.PositionalShadowAtlasSize = q switch { 0 => 2048, 3 => 8192, _ => 4096 };
		foreach (Node node in tree.Root.FindChildren("*", nameof(WorldEnvironment), true, false))
			if (node is WorldEnvironment world) ApplyQuality(world.Environment);
	}

	/// <summary>Os efeitos de um ambiente na qualidade escolhida. Guarda,
	/// na primeira vez, o que o mapa pediu.</summary>
	public static void ApplyQuality(Godot.Environment env)
	{
		if (env == null) return;
		int q = Math.Clamp(Values.Quality, 0, QualityNames.Length - 1);

		if (!env.HasMeta("mapa_ssao"))
		{
			env.SetMeta("mapa_ssao", env.SsaoEnabled);
			env.SetMeta("mapa_glow", env.GlowEnabled);
		}
		bool ssao = env.GetMeta("mapa_ssao").AsBool(), glow = env.GetMeta("mapa_glow").AsBool();

		// A luz rebatida (SDFGI) e a névoa com raio de sol são as mais
		// caras: só da Alta para cima, e só no mapa que as pede.
		if (!env.HasMeta("mapa_sdfgi"))
		{
			env.SetMeta("mapa_sdfgi", env.SdfgiEnabled);
			env.SetMeta("mapa_nevoa", env.VolumetricFogEnabled);
		}
		env.SdfgiEnabled = env.GetMeta("mapa_sdfgi").AsBool() && q >= 2;
		env.VolumetricFogEnabled = env.GetMeta("mapa_nevoa").AsBool() && q >= 2;

		env.SsaoEnabled = ssao && q >= 1;
		env.GlowEnabled = glow && q >= 1;
		env.SsilEnabled = ssao && q >= 2;
		env.SsilIntensity = 0.8f;
		env.SsrEnabled = ssao && q >= 3;
		env.SsrMaxSteps = 48;
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
