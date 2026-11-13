using Godot;

/// <summary>
/// Os valores de design do projeto (docs/UI.md, "Design tokens"). Todo
/// espaçamento, cor, tamanho de fonte e altura de componente sai daqui.
///
/// Regra: nada fora destas listas. Espaçamento de 13 px aqui e 17 ali é
/// o que faz uma interface parecer amadora, e é exatamente o que uma
/// tabela fechada impede. Precisa de um valor novo? Ele entra aqui, com
/// nome, e não solto numa tela.
/// </summary>
public static class UiTokens
{
	// ---------------------------------------------------------------- espaço
	// Base 8: 4 · 8 · 16 · 24 · 32 · 48 · 64.
	public const int S1 = 4;
	public const int S2 = 8;
	public const int S3 = 16;
	public const int S4 = 24;
	public const int S5 = 32;
	public const int S6 = 48;
	public const int S7 = 64;

	// ---------------------------------------------------------------- canto
	// Canto duro: estética militar e sombria. HUD não tem canto nenhum.
	public const int RadiusSmall = 2;
	public const int RadiusPanel = 4;
	public const int RadiusHud = 0;

	// ---------------------------------------------------------------- alturas
	public const int ButtonHeight = 40;
	public const int FieldHeight = 36;
	public const int RoomRowHeight = 52;
	public const int ScoreRowHeight = 32;
	public const int HeaderHeight = 64;
	public const int SlotHeight = 64;

	// ---------------------------------------------------------------- tipo
	// Cinco tamanhos, mais o numérico, que usa os mesmos tamanhos com
	// algarismos de largura fixa.
	public const int Display = 48;
	public const int Title = 28;
	public const int Subtitle = 20;
	public const int Body = 15;
	public const int Caption = 13;

	// ---------------------------------------------------------------- paleta
	// Fundo cinza quase preto, levemente azulado — não preto puro.
	public static readonly Color Bg = new("0f1318");
	public static readonly Color Surface1 = new("161b22");
	public static readonly Color Surface2 = new("1c222b");
	public static readonly Color Surface3 = new("242b35");
	public static readonly Color Border = new(1, 1, 1, 0.09f);
	public static readonly Color BorderStrong = new(1, 1, 1, 0.18f);

	public static readonly Color TextPrimary = new("e9ecf0");
	public static readonly Color TextSecondary = new("a1a9b4");
	public static readonly Color TextDisabled = new("5c6470");

	/// <summary>A cor da marca: âmbar-ouro. Botão primário, aba ativa,
	/// foco. Puxada para o amarelo de propósito, para não se confundir
	/// com o laranja do time.</summary>
	public static readonly Color Accent = new("e3b04b");
	public static readonly Color AccentText = new("17130a");

	public static readonly Color Danger = new("e5484d");
	public static readonly Color Success = new("46a758");
	public static readonly Color Warning = new("f0c24b");

	/// <summary>Times: azul e laranja, que daltônicos distinguem bem —
	/// vermelho e verde, não.</summary>
	public static readonly Color TeamBlue = new("3d8bff");
	public static readonly Color TeamOrange = new("ff7a2f");

	/// <summary>Raridade: escala própria de cinco passos, separada do
	/// resto da paleta.</summary>
	public static readonly Color[] Rarity =
	{
		new("9aa3ae"), // comum
		new("4fbf6a"), // incomum
		new("4c8dff"), // raro
		new("a46bff"), // épico
		new("ffb02e")  // lendário
	};

	public static readonly string[] RarityNames = { "Comum", "Incomum", "Raro", "Épico", "Lendário" };

	// ---------------------------------------------------------------- movimento
	/// <summary>Transição padrão: entre 120 e 200 ms, com curva de saída.</summary>
	public const float Fast = 0.12f;
	public const float Normal = 0.18f;

	/// <summary>Número que muda conta de A até B neste tempo.</summary>
	public const float CountUp = 0.4f;

	// Formato montado à mão, não pela cultura "pt-BR": o .NET do Godot
	// pode rodar sem dados de cultura, e aí pedir uma cultura pelo nome
	// lança exceção.
	private static readonly System.Globalization.NumberFormatInfo Grouping = new()
	{
		NumberGroupSeparator = ".",
		NumberDecimalSeparator = ","
	};

	/// <summary>Formata com separador de milhar ("43.000").</summary>
	public static string Thousands(long value) => value.ToString("#,0", Grouping);
}
