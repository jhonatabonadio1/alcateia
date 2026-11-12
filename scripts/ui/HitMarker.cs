using Godot;

/// <summary>
/// Marcador de acerto: quatro traços diagonais em volta da mira que
/// nascem grandes e encolhem. Tiro na cabeça sai vermelho e maior; o
/// abate segura um pouco mais. Junto com o som (acerto, acerto_cabeca,
/// abate), é o que diz "pegou" sem o jogador tirar o olho do alvo.
/// </summary>
public partial class HitMarker : Control
{
    [Export] public float Life = 0.22f;
    [Export] public float KillLife = 0.45f;

    private double _left;
    private double _total = 1;
    private Color _color = Colors.White;
    private float _size = 10f;

    public HitMarker()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Flash(bool headshot, bool kill = false)
    {
        _total = _left = kill ? KillLife : Life;
        _color = headshot || kill ? UiTokens.Danger.Lightened(0.15f) : Colors.White;
        _size = headshot ? 14f : 10f;
        if (kill) _size = 16f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_left <= 0) return;
        _left -= delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_left <= 0) return;

        float t = (float)(_left / _total);       // 1 → 0
        float grow = 1f + 0.5f * t;              // começa maior e encolhe
        float inner = 7f * grow;
        float outer = inner + _size * grow * 0.6f;
        Vector2 c = Size / 2f;
        var color = new Color(_color, Mathf.Clamp(t * 1.6f, 0, 1));

        foreach (Vector2 dir in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            Vector2 d = dir.Normalized();
            DrawLine(c + d * inner, c + d * outer, new Color(0, 0, 0, color.A * 0.5f), 4f);
            DrawLine(c + d * inner, c + d * outer, color, 2f);
        }
    }
}
