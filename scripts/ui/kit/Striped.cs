using Godot;

/// <summary>
/// Fundo listrado em diagonal. Marca o que existe mas está vazio (slot de
/// jogador livre) ou bloqueado (slot de inventário a destravar) — os dois
/// casos que precisam parecer diferentes de "não há nada aqui".
/// </summary>
public partial class Striped : Control
{
    public Color StripeColor { get; set; } = new(1, 1, 1, 0.035f);
    public float Spacing { get; set; } = 10f;

    public Striped()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(UiTokens.Bg, 0.5f));
        for (float x = -Size.Y; x < Size.X; x += Spacing)
            DrawLine(new Vector2(x, Size.Y), new Vector2(x + Size.Y, 0), StripeColor, 4f);
    }
}
