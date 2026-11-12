using Godot;

/// <summary>
/// As duas famílias do projeto: Barlow (neutra, para ler) e Barlow
/// Condensed (display, para títulos, patentes e números grandes). Ambas
/// livres, licença OFL — o texto está em assets/fonts/OFL.txt.
///
/// <see cref="Numeric"/> e <see cref="NumericDisplay"/> ligam os
/// algarismos tabulares da fonte (largura fixa por dígito). Sem isso a
/// munição treme ao contar e o header "pula" quando o saldo muda.
/// </summary>
public static class UiFonts
{
    private const string Dir = "res://assets/fonts/";

    public static readonly Font Regular = Load("Barlow-Regular.ttf");
    public static readonly Font Medium = Load("Barlow-Medium.ttf");
    public static readonly Font SemiBold = Load("Barlow-SemiBold.ttf");
    public static readonly Font Bold = Load("Barlow-Bold.ttf");
    public static readonly Font Display = Load("BarlowCondensed-Bold.ttf");
    public static readonly Font DisplaySemi = Load("BarlowCondensed-SemiBold.ttf");

    /// <summary>Barlow Medium com algarismos tabulares.</summary>
    public static readonly Font Numeric = Tabular(Medium);

    /// <summary>Barlow Condensed Bold com algarismos tabulares.</summary>
    public static readonly Font NumericDisplay = Tabular(Display);

    /// <summary>Rótulo: Medium com 5% a mais de espaço entre letras,
    /// para caixa alta pequena continuar legível.</summary>
    public static readonly Font Label = Spaced(Medium, 1);

    private static Font Load(string file)
    {
        var font = GD.Load<Font>(Dir + file);
        if (font == null) GD.PrintErr($"UiFonts: não achei {Dir}{file}");
        return font ?? ThemeDB.FallbackFont;
    }

    private static Font Tabular(Font font)
    {
        long tnum = TextServerManager.GetPrimaryInterface().NameToTag("tnum");
        return new FontVariation
        {
            BaseFont = font,
            OpentypeFeatures = new Godot.Collections.Dictionary { { tnum, 1 } }
        };
    }

    private static Font Spaced(Font font, int glyphSpacing) => new FontVariation
    {
        BaseFont = font,
        SpacingGlyph = glyphSpacing
    };
}
