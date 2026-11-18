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
	/// A paleta dos **menus** de fora da partida (17/11, terceira volta: o
	/// claro ficou perto demais de Overwatch; o Jhon pediu "no modo mais
	/// escuro mesmo", com Valorant e League of Legends de referência):
	/// azul-noite profundo, painéis de vidro escuro, texto claro e o
	/// destaque em ouro. O LobbyShell liga ao abrir e desliga ao sair. Na
	/// partida (HUD, menu do Esc, resultado) vale a paleta do jogo, um
	/// cinza-azulado mais neutro, por cima do mundo.
	///
	/// A mesma chave troca o **estilo**: nos menus os cantos são
	/// arredondados, os botões retos e as letras são Kanit e Figtree
	/// (<see cref="UiTheme"/>, <see cref="UiFonts"/>); na partida, facetado
	/// e Barlow.
	///
	/// As cores são lidas na hora de montar cada controle: troque o modo
	/// antes de montar a tela.
	/// </summary>
	public static bool Menu { get; set; }

	// Jogo: fundo cinza quase preto, levemente azulado — não preto puro.
	// Menus: azul-noite.
	private static readonly Color DarkBg = new("0f1318"), MenuBg = new("0a1119");
	private static readonly Color DarkSurface1 = new("161b22"), MenuSurface1 = new("111a26");
	private static readonly Color DarkSurface2 = new("1c222b"), MenuSurface2 = new("16212f");
	private static readonly Color DarkSurface3 = new("242b35"), MenuSurface3 = new("1f2c3d");
	private static readonly Color DarkBorder = new(1, 1, 1, 0.09f), MenuBorder = new(0.75f, 0.86f, 1f, 0.1f);
	private static readonly Color DarkBorderStrong = new(1, 1, 1, 0.18f), MenuBorderStrong = new(0.75f, 0.86f, 1f, 0.2f);
	private static readonly Color DarkTextPrimary = new("e9ecf0"), MenuTextPrimary = new("edf2f7");
	private static readonly Color DarkTextSecondary = new("a1a9b4"), MenuTextSecondary = new("9db0c4");
	private static readonly Color DarkTextDisabled = new("5c6470"), MenuTextDisabled = new("56667a");
	private static readonly Color DarkAccent = new("e3b04b"), MenuAccent = new("f3b950");
	private static readonly Color DarkAccentText = new("17130a"), MenuAccentText = new("1a1206");
	private static readonly Color DarkDanger = new("e5484d"), MenuDanger = new("f0555a");
	private static readonly Color DarkSuccess = new("46a758"), MenuSuccess = new("4cc38a");
	private static readonly Color DarkWarning = new("f0c24b"), MenuWarning = new("f0c24b");

	public static Color Bg => Menu ? MenuBg : DarkBg;
	public static Color Surface1 => Menu ? MenuSurface1 : DarkSurface1;
	public static Color Surface2 => Menu ? MenuSurface2 : DarkSurface2;
	public static Color Surface3 => Menu ? MenuSurface3 : DarkSurface3;
	public static Color Border => Menu ? MenuBorder : DarkBorder;
	public static Color BorderStrong => Menu ? MenuBorderStrong : DarkBorderStrong;

	public static Color TextPrimary => Menu ? MenuTextPrimary : DarkTextPrimary;
	public static Color TextSecondary => Menu ? MenuTextSecondary : DarkTextSecondary;
	public static Color TextDisabled => Menu ? MenuTextDisabled : DarkTextDisabled;

	/// <summary>A cor da marca: âmbar-ouro, puxada para o amarelo para
	/// não se confundir com o laranja do time. Nos menus, um ouro um pouco
	/// mais vivo.</summary>
	public static Color Accent => Menu ? MenuAccent : DarkAccent;
	public static Color AccentText => Menu ? MenuAccentText : DarkAccentText;

	public static Color Danger => Menu ? MenuDanger : DarkDanger;
	public static Color Success => Menu ? MenuSuccess : DarkSuccess;
	public static Color Warning => Menu ? MenuWarning : DarkWarning;

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
