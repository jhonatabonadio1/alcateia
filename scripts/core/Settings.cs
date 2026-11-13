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
