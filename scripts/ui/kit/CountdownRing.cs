using Godot;

/// <summary>
/// Contagem regressiva em círculo grande, com o número no meio e um arco
/// que se esvazia. Na sala ela fica sobre a área dos slots — espaço que
/// estaria morto, e onde é impossível não ver.
/// </summary>
public partial class CountdownRing : Control
{
    public double Total { get; set; } = 5;
    public double Left { get; set; } = 5;
    public string Caption { get; set; } = "";

    public CountdownRing()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(180, 180);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) / 2f - 8f;
        Vector2 c = Size / 2f;

        DrawCircle(c, r + 6, new Color(UiTokens.Bg, 0.85f));
        DrawArc(c, r, 0, Mathf.Tau, 64, UiTokens.Surface3, 6, true);

        float frac = Total <= 0 ? 0 : (float)Mathf.Clamp(Left / Total, 0, 1);
        DrawArc(c, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * frac, 64, UiTokens.Accent, 6, true);

        string number = Mathf.CeilToInt((float)Left).ToString();
        Font font = UiFonts.NumericDisplay;
        int size = (int)(r * 0.95f);
        Vector2 textSize = font.GetStringSize(number, HorizontalAlignment.Center, -1, size);
        DrawString(font, new Vector2(c.X - textSize.X / 2f, c.Y + size * 0.36f), number,
            HorizontalAlignment.Left, -1, size, UiTokens.TextPrimary);

        if (!string.IsNullOrEmpty(Caption))
        {
            Vector2 capSize = UiFonts.Label.GetStringSize(Caption, HorizontalAlignment.Center, -1, UiTokens.Caption);
            DrawString(UiFonts.Label, new Vector2(c.X - capSize.X / 2f, c.Y + r * 0.62f), Caption,
                HorizontalAlignment.Left, -1, UiTokens.Caption, UiTokens.TextSecondary);
        }
    }
}
