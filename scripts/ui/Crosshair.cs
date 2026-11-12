using Godot;

/// <summary>
/// Mira com abertura dinâmica: as quatro hastes se afastam conforme a
/// dispersão da arma cresce (correr, atirar seguido) e voltam quando ela
/// recupera. Custa pouco e informa muito — o jogador vê, na mira, quando
/// o próximo tiro vai sair torto.
///
/// Na forma de lobo não aparece: o lobo não mira, ele chega — e em
/// terceira pessoa o próprio lobo no meio da tela já diz para onde vai.
/// </summary>
public partial class Crosshair : Control
{
    /// <summary>Pixels de abertura por grau de dispersão.</summary>
    [Export] public float PixelsPerDegree = 9f;
    [Export] public float MinGap = 5f;
    [Export] public float Length = 8f;
    [Export] public float Thickness = 2f;

    public Weapon Weapon { get; set; }
    public FormController Form { get; set; }

    private float _gap;

    public Crosshair()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        // A abertura é visual e contínua: ler a dispersão a cada quadro é
        // o certo aqui (não existe "evento" de dispersão).
        float target = MinGap + (Weapon?.CurrentSpread ?? 0f) * PixelsPerDegree;
        _gap = Mathf.Lerp(_gap, target, (float)Mathf.Min(1.0, delta * 18.0));
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 c = Size / 2f;
        Color line = new(1, 1, 1, 0.92f);
        Color shadow = new(0, 0, 0, 0.55f);

        if (Form != null && Form.IsWolf) return;

        foreach (Vector2 dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
        {
            Vector2 from = c + dir * _gap;
            Vector2 to = c + dir * (_gap + Length);
            DrawLine(from, to, shadow, Thickness + 2f);
            DrawLine(from, to, line, Thickness);
        }

        DrawRect(new Rect2(c - Vector2.One, Vector2.One * 2), line);
    }
}
