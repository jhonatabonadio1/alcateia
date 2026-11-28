using Godot;
using System.Collections.Generic;

/// <summary>
/// As letras do projeto. São dois conjuntos, e o que vale é o da paleta
/// em uso (<see cref="UiTokens.Menu"/>), como as cores:
///
/// - **Na partida** (HUD, menu do Esc, resultado): Barlow (neutra, para
///   ler) e Barlow Condensed (display, para números grandes).
/// - **Nos menus** (pedido do Jhon, 17/11: mais suave, letra própria):
///   Figtree para ler — redonda, calma, com pesos de 400 a 700 num
///   arquivo só — e Kanit para títulos e números, no itálico de verdade
///   da família (o inclinado por transformação do HUD é mais duro).
///
/// Todas livres, licença OFL — os textos estão em assets/fonts/OFL*.txt.
///
/// <see cref="Numeric"/> e <see cref="NumericDisplay"/> ligam os
/// algarismos tabulares da fonte (largura fixa por dígito). Sem isso a
/// munição treme ao contar e o header "pula" quando o saldo muda.
/// </summary>
public static class UiFonts
{
	private const string Dir = "res://src/assets/fonts/";

	// ---------------------------------------------------------------- partida
	private static readonly Font GameRegular = Load("Barlow-Regular.ttf");
	private static readonly Font GameMedium = Load("Barlow-Medium.ttf");
	private static readonly Font GameSemiBold = Load("Barlow-SemiBold.ttf");
	private static readonly Font GameBold = Load("Barlow-Bold.ttf");
	private static readonly Font GameDisplay = Load("BarlowCondensed-Bold.ttf");
	private static readonly Font GameDisplaySemi = Load("BarlowCondensed-SemiBold.ttf");
	private static readonly Font GameNumeric = Vary(GameMedium, tabular: true);
	private static readonly Font GameNumericDisplay = Vary(GameDisplay, tabular: true);
	private static readonly Font GameLabel = Vary(GameMedium, spacing: 1);

	// ---------------------------------------------------------------- menus
	private static readonly Font Figtree = Load("Figtree-Variable.ttf");
	private static readonly Font KanitItalic = Load("Kanit-SemiBoldItalic.ttf");
	private static readonly Font KanitHeavy = Load("Kanit-BoldItalic.ttf");
	private static readonly Font KanitUpright = Load("Kanit-SemiBold.ttf");
	private static readonly Font MenuRegular = Vary(Figtree, weight: 400);
	private static readonly Font MenuMedium = Vary(Figtree, weight: 500);
	private static readonly Font MenuSemiBold = Vary(Figtree, weight: 600);
	private static readonly Font MenuBold = Vary(Figtree, weight: 700);
	private static readonly Font MenuNumeric = Vary(Figtree, weight: 600, tabular: true);
	private static readonly Font MenuNumericDisplay = Vary(KanitItalic, tabular: true);
	private static readonly Font MenuLabel = Vary(Figtree, weight: 600, spacing: 1);
	/// <summary>As que já são itálicas de desenho: não levam a inclinação
	/// por transformação (<see cref="HudStyle.Slanted"/>).</summary>
	private static readonly HashSet<Font> Italics = new() { KanitItalic, KanitHeavy, MenuNumericDisplay };

	public static bool IsItalic(Font font) => font != null && Italics.Contains(font);

	public static Font Regular => UiTokens.Menu ? MenuRegular : GameRegular;
	public static Font Medium => UiTokens.Menu ? MenuMedium : GameMedium;
	public static Font SemiBold => UiTokens.Menu ? MenuSemiBold : GameSemiBold;
	public static Font Bold => UiTokens.Menu ? MenuBold : GameBold;

	/// <summary>Títulos e números grandes.</summary>
	public static Font Display => UiTokens.Menu ? KanitItalic : GameDisplay;

	/// <summary>Display mais leve; nos menus, a Kanit em pé.</summary>
	public static Font DisplaySemi => UiTokens.Menu ? KanitUpright : GameDisplaySemi;

	/// <summary>O título mais pesado (nome do jogador, tela de entrada).</summary>
	public static Font DisplayHeavy => UiTokens.Menu ? KanitHeavy : GameDisplay;

	/// <summary>A letra do logotipo ("WOLFLEAGUE" no header): a Kanit itálica
	/// de sempre (Jhon, 27/11: "a logotipo pode manter a mesma").</summary>
	public static Font Brand => KanitItalic;

	/// <summary>O título grande das telas novas (nome de tela, de mapa, de
	/// modo): a Kanit itálica pesada, a mesma letra de sempre. (Em 27/11
	/// chegou a ser uma condensada em pé com as letras afastadas; o Jhon
	/// pediu de volta: "fonte do jeito que era antes, mais grossinha e
	/// inclinada, estilo Overwatch", "não precisa muito separada".)</summary>
	public static Font Wide => KanitHeavy;

	/// <summary>Para ler, com algarismos tabulares.</summary>
	public static Font Numeric => UiTokens.Menu ? MenuNumeric : GameNumeric;

	/// <summary>Display com algarismos tabulares.</summary>
	public static Font NumericDisplay => UiTokens.Menu ? MenuNumericDisplay : GameNumericDisplay;

	/// <summary>Rótulo: um pouco mais de espaço entre letras, para caixa
	/// alta pequena continuar legível.</summary>
	public static Font Label => UiTokens.Menu ? MenuLabel : GameLabel;

	/// <summary>Etiqueta: caixa alta pequena (abas do header, cabeçalho de
	/// coluna, "// MISSÕES DO DIA"). A mesma letra dos rótulos.</summary>
	public static Font Tag => Label;

	private static Font Load(string file)
	{
		var font = GD.Load<Font>(Dir + file);
		if (font == null) GD.PrintErr($"UiFonts: not found: {Dir}{file}");
		return font ?? ThemeDB.FallbackFont;
	}

	/// <summary>Uma variação de um arquivo de fonte: peso (nas variáveis),
	/// algarismos tabulares, espaço entre letras. Sempre direto sobre o
	/// arquivo, nunca variação de variação.</summary>
	private static Font Vary(Font font, int weight = 0, bool tabular = false, int spacing = 0)
	{
		TextServer server = TextServerManager.GetPrimaryInterface();
		var variation = new FontVariation { BaseFont = font, SpacingGlyph = spacing };
		if (weight > 0)
			variation.VariationOpentype = new Godot.Collections.Dictionary { { server.NameToTag("wght"), weight } };
		if (tabular)
			variation.OpentypeFeatures = new Godot.Collections.Dictionary { { server.NameToTag("tnum"), 1 } };
		return variation;
	}
}
