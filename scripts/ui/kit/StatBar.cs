using Godot;

/// <summary>
/// Barra de atributo comparativa: nome, barra de 0 a 1 e, se houver,
/// a diferença para o item equipado — trecho verde se o novo é melhor,
/// vermelho se é pior. É o que deixa o jogador decidir sem abrir
/// planilha.
/// </summary>
public partial class StatBar : Control
{
    private string _name = "";
    private float _value;
    private float _compare = -1f;
    private string _text = "";

    public static StatBar Make(string name, float value01, string valueText, float compare01 = -1f)
    {
        var bar = new StatBar
        {
            _name = name.ToUpperInvariant(),
            _value = Mathf.Clamp(value01, 0f, 1f),
            _compare = compare01 < 0 ? -1f : Mathf.Clamp(compare01, 0f, 1f),
            _text = valueText ?? "",
            CustomMinimumSize = new Vector2(0, 30),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        return bar;
    }

    /// <summary>Barra decorativa de "estrelas" (0 a 5) dos cartões.</summary>
    public static StatBar Stars(string name, float stars, float compare = -1f) =>
        Make(name, stars / 5f, $"{stars:0.#}", compare < 0 ? -1f : compare / 5f);

    public override void _Draw()
    {
        Font caption = UiFonts.Label;
        Font numeric = UiFonts.Numeric;
        const int captionSize = UiTokens.Caption;

        DrawString(caption, new Vector2(0, 12), _name, HorizontalAlignment.Left, -1, captionSize, UiTokens.TextSecondary);
        DrawString(numeric, new Vector2(0, 12), _text, HorizontalAlignment.Right, Size.X, captionSize, UiTokens.TextPrimary);

        var track = new Rect2(0, 18, Size.X, 6);
        DrawRect(track, UiTokens.Surface3);

        float now = track.Size.X * _value;

        if (_compare < 0 || Mathf.IsEqualApprox(_compare, _value))
        {
            DrawRect(new Rect2(track.Position, new Vector2(now, track.Size.Y)), UiTokens.TextPrimary);
            return;
        }

        float was = track.Size.X * _compare;
        float low = Mathf.Min(now, was);
        DrawRect(new Rect2(track.Position, new Vector2(low, track.Size.Y)), UiTokens.TextPrimary);

        Color delta = _value > _compare ? UiTokens.Success : UiTokens.Danger;
        DrawRect(new Rect2(track.Position.X + low, track.Position.Y, Mathf.Abs(now - was), track.Size.Y), delta);
    }
}
