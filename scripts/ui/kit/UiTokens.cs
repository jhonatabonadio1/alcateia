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
	/// <summary>
	/// Paleta clara (pedido do Jhon, 17/11: os menus claros como os de
	/// Overwatch, com Valorant de inspiração): fundo quase branco e frio,
	/// cartões brancos, texto azul-marinho (o de Valorant) e o laranja de
	/// Overwatch no destaque. Vale nos menus fora da partida: o LobbyShell
	/// liga ao abrir e desliga ao sair. Na partida (HUD, menu do Esc,
	/// resultado) a paleta é a escura, por cima do jogo.
	///
	/// As cores são lidas na hora de montar cada controle: troque o modo
	/// antes de montar a tela.
	/// </summary>
	public static bool Light { get; set; }

	// Escura: fundo cinza quase preto, levemente azulado — não preto puro.
	private static readonly Color DarkBg = new("0f1318"), LightBg = new("e9edf2");
	private static readonly Color DarkSurface1 = new("161b22"), LightSurface1 = new("ffffff");
	private static readonly Color DarkSurface2 = new("1c222b"), LightSurface2 = new("f5f7fa");
	private static readonly Color DarkSurface3 = new("242b35"), LightSurface3 = new("e4e9ef");
	private static readonly Color DarkBorder = new(1, 1, 1, 0.09f), LightBorder = new(0.06f, 0.1f, 0.14f, 0.12f);
	private static readonly Color DarkBorderStrong = new(1, 1, 1, 0.18f), LightBorderStrong = new(0.06f, 0.1f, 0.14f, 0.26f);
	private static readonly Color DarkTextPrimary = new("e9ecf0"), LightTextPrimary = new("0f1923");
	private static readonly Color DarkTextSecondary = new("a1a9b4"), LightTextSecondary = new("4b5a69");
	private static readonly Color DarkTextDisabled = new("5c6470"), LightTextDisabled = new("9aa5b1");
	private static readonly Color DarkAccent = new("e3b04b"), LightAccent = new("f06a1d");
	private static readonly Color DarkAccentText = new("17130a"), LightAccentText = new("ffffff");
	private static readonly Color DarkDanger = new("e5484d"), LightDanger = new("e0323d");
	private static readonly Color DarkSuccess = new("46a758"), LightSuccess = new("23874a");
	private static readonly Color DarkWarning = new("f0c24b"), LightWarning = new("c98600");

	public static Color Bg => Light ? LightBg : DarkBg;
	public static Color Surface1 => Light ? LightSurface1 : DarkSurface1;
	public static Color Surface2 => Light ? LightSurface2 : DarkSurface2;
	public static Color Surface3 => Light ? LightSurface3 : DarkSurface3;
	public static Color Border => Light ? LightBorder : DarkBorder;
	public static Color BorderStrong => Light ? LightBorderStrong : DarkBorderStrong;

	public static Color TextPrimary => Light ? LightTextPrimary : DarkTextPrimary;
	public static Color TextSecondary => Light ? LightTextSecondary : DarkTextSecondary;
	public static Color TextDisabled => Light ? LightTextDisabled : DarkTextDisabled;

	/// <summary>A cor da marca. Escura: âmbar-ouro, puxada para o amarelo
	/// para não se confundir com o laranja do time. Clara: o laranja de
	/// Overwatch (no claro o ouro some).</summary>
	public static Color Accent => Light ? LightAccent : DarkAccent;
	public static Color AccentText => Light ? LightAccentText : DarkAccentText;

	public static Color Danger => Light ? LightDanger : DarkDanger;
	public static Color Success => Light ? LightSuccess : DarkSuccess;
	public static Color Warning => Light ? LightWarning : DarkWarning;

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
