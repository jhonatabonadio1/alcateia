using Godot;
using System.Collections.Generic;

/// <summary>
/// Arco vermelho em volta da mira apontando de onde veio o dano.
///
/// Guarda a posição de origem no mundo, não o ângulo do momento do
/// tiro. É o que faz o arco girar quando você vira a câmera: ele
/// continua apontando para o atirador, e não para onde o atirador
/// estava em relação à sua tela meio segundo atrás. Virar na direção
/// do arco é a reação natural, e ela só funciona se o arco acompanhar.
///
/// A borda vermelha da tela inteira continua existindo, mas fraca: ela
/// diz "você levou dano"; o arco diz "de onde".
/// </summary>
public partial class DamageIndicator : Control
{
    /// <summary>Distância do arco até o centro da tela, em pixels.</summary>
    [Export] public float Radius = 150f;

    /// <summary>Meia abertura do arco, em graus.</summary>
    [Export] public float HalfWidth = 24f;

    [Export] public float Thickness = 14f;

    /// <summary>Quanto tempo cada arco fica na tela.</summary>
    [Export] public float Life = 1.5f;

    [Export] public Color ArcColor = new Color(0.95f, 0.1f, 0.08f);

    private Health _health;
    private readonly List<(Vector3 From, double Age, float Strength)> _hits = new();

    public override void _Ready()
    {
        AnchorRight = 1f;
        AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Ignore;

        var player = GetTree().GetFirstNodeInGroup("player");
        _health = player?.GetNodeOrNull<Health>("Health");

        if (_health == null) return;

        _health.Damaged += OnDamaged;
        _health.Respawned += () => _hits.Clear();
    }

    private void OnDamaged(float amount, Vector3 from)
    {
        // Sem origem não há direção. Melhor não desenhar do que
        // desenhar um arco apontando para lugar nenhum.
        if (from == Vector3.Zero || _health.IsDead) return;

        // Tiros do mesmo lugar em sequência renovam um arco só, em vez
        // de empilhar dez arcos idênticos.
        for (int i = 0; i < _hits.Count; i++)
        {
            if (_hits[i].From.DistanceTo(from) > 2f) continue;

            _hits[i] = (from, 0, Mathf.Min(1f, _hits[i].Strength + 0.25f));
            return;
        }

        _hits.Add((from, 0, Mathf.Clamp(0.55f + amount / 60f, 0.55f, 1f)));
    }

    public override void _Process(double delta)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            var hit = _hits[i];
            hit.Age += delta;

            if (hit.Age >= Life) _hits.RemoveAt(i);
            else _hits[i] = hit;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (camera == null || _hits.Count == 0) return;

        Vector2 center = Size * 0.5f;

        foreach (var hit in _hits)
        {
            // Origem no referencial da câmera, achatada no plano do chão.
            // Frente é −Z; o ângulo zero aponta para cima na tela.
            Vector3 local = camera.GlobalTransform.Basis.Inverse() * (hit.From - camera.GlobalPosition);
            float angle = Mathf.Atan2(local.X, -local.Z);

            // Na tela o zero do arco é a direita e o sentido é horário;
            // "para cima" é −90°.
            float mid = angle - Mathf.Pi / 2f;
            float half = Mathf.DegToRad(HalfWidth);

            float fade = 1f - (float)(hit.Age / Life);
            var color = new Color(ArcColor, 0.85f * fade * hit.Strength);

            DrawArc(center, Radius, mid - half, mid + half, 24, color, Thickness, true);

            // Ponta mais grossa no meio do arco: sem ela, um arco largo
            // não deixa claro para onde exatamente olhar.
            DrawArc(center, Radius + Thickness * 0.6f, mid - half * 0.25f, mid + half * 0.25f,
                8, new Color(color, color.A * 0.9f), Thickness * 0.7f, true);
        }
    }
}
