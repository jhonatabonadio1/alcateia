using Godot;

/// <summary>
/// Minimapa no canto superior esquerdo.
///
/// Desenhado em 2D a partir das posições do mundo, não por câmera
/// ortogonal numa viewport. A diferença não é só custo: uma câmera de
/// cima desenharia o chão, as sombras e a iluminação, e nada disso
/// ajuda a ler posição. Um minimapa é um diagrama, não uma foto.
///
/// Gira com o jogador — o que está em cima do mapa é o que está à
/// frente. Minimapa fixo obriga o jogador a traduzir mentalmente
/// enquanto corre, e ninguém faz isso no meio de um tiroteio.
///
/// Só mostra quem está dentro do alcance. Saber onde está todo mundo
/// o tempo todo destruiria a razão de existir do agachar, do andar
/// devagar e da audição dos bots.
/// </summary>
public partial class Minimap : Control
{
    [Export] public float Range = 42f;

    /// <summary>Lado do quadrado, em pixels.</summary>
    [Export] public float Side = 168f;

    [Export] public Color BackColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);
    [Export] public Color EdgeColor = new Color(1f, 1f, 1f, 0.22f);
    [Export] public Color WallColor = new Color(1f, 1f, 1f, 0.3f);
    [Export] public Color SelfColor = new Color(0.95f, 0.95f, 0.95f);

    private Node3D _player;
    private Health _playerHealth;
    private Node3D _walls;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        _playerHealth = _player?.GetNodeOrNull<Health>("Health");

        _walls = GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Walls");

        CustomMinimumSize = new Vector2(Side, Side);
        Size = CustomMinimumSize;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (_player != null) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_player == null) return;

        var center = new Vector2(Side * 0.5f, Side * 0.5f);
        var frame = new Rect2(Vector2.Zero, new Vector2(Side, Side));

        DrawRect(frame, BackColor);
        DrawRect(frame, EdgeColor, false, 1.5f);

        // Cruz discreta nos eixos: dá referência de distância sem
        // competir com os pontos.
        var faint = new Color(EdgeColor, EdgeColor.A * 0.4f);
        DrawLine(new Vector2(center.X, 0), new Vector2(center.X, Side), faint, 1f);
        DrawLine(new Vector2(0, center.Y), new Vector2(Side, center.Y), faint, 1f);

        // O mundo é girado pelo inverso do olhar: o que está à frente
        // sobe na tela.
        float yaw = _player.Rotation.Y;

        DrawWalls(center, yaw);
        DrawActors(center, yaw);
        DrawSelf(center);
    }

    // ------------------------------------------------------------------

    /// <summary>Converte uma posição do mundo em ponto do minimapa, ou
    /// devolve falso se estiver fora do alcance.</summary>
    private bool ToMap(Vector3 world, Vector2 center, float yaw, out Vector2 point)
    {
        point = Vector2.Zero;

        Vector3 delta = world - _player.GlobalPosition;
        var flat = new Vector2(delta.X, delta.Z);

        if (flat.Length() > Range) return false;

        // Girar o mundo é o oposto de girar a câmera.
        Vector2 rotated = flat.Rotated(yaw);
        point = center + rotated / Range * (Side * 0.5f);

        return true;
    }

    private void DrawWalls(Vector2 center, float yaw)
    {
        if (_walls == null) return;

        foreach (Node node in _walls.GetChildren())
        {
            if (node is not Node3D wall) continue;

            var shape = wall.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (shape?.Shape is not BoxShape3D box) continue;

            // Quatro cantos do retângulo, girados junto com o resto.
            Vector3 half = box.Size * 0.5f;
            var corners = new Vector3[]
            {
                new(-half.X, 0, -half.Z), new(half.X, 0, -half.Z),
                new(half.X, 0, half.Z), new(-half.X, 0, half.Z)
            };

            var points = new Vector2[4];
            bool visible = false;

            for (int i = 0; i < 4; i++)
            {
                Vector3 world = wall.GlobalTransform * corners[i];

                if (ToMap(world, center, yaw, out points[i]))
                    visible = true;
                else
                    ToMapUnclamped(world, center, yaw, out points[i]);
            }

            if (!visible) continue;

            for (int i = 0; i < 4; i++)
                DrawLine(points[i], points[(i + 1) % 4], WallColor, 1.5f);
        }
    }

    private void ToMapUnclamped(Vector3 world, Vector2 center, float yaw, out Vector2 point)
    {
        Vector3 delta = world - _player.GlobalPosition;
        Vector2 rotated = new Vector2(delta.X, delta.Z).Rotated(yaw);

        point = center + rotated / Range * (Side * 0.5f);
    }

    private void DrawActors(Vector2 center, float yaw)
    {
        foreach (Node node in GetTree().GetNodesInGroup("bot"))
        {
            if (node is not Node3D bot) continue;

            var health = bot.GetNodeOrNull<Health>("Health");
            if (health == null || health.IsDead) continue;

            if (!ToMap(bot.GlobalPosition, center, yaw, out Vector2 point)) continue;

            Color color = Teams.ColorOf(health.Team);

            // Altura relativa: quem está acima aparece maior. É a única
            // dica de verticalidade que um mapa de cima consegue dar.
            float height = bot.GlobalPosition.Y - _player.GlobalPosition.Y;
            float size = Mathf.Clamp(4f + height * 0.6f, 2.5f, 7f);

            DrawCircle(point, size, color);
        }
    }

    /// <summary>O jogador é um triângulo no centro, sempre apontando
    /// para cima: ele é a referência, não um ponto entre outros.</summary>
    private void DrawSelf(Vector2 center)
    {
        Color color = _playerHealth != null
            ? Teams.ColorOf(_playerHealth.Team)
            : SelfColor;

        var points = new[]
        {
            center + new Vector2(0, -8),
            center + new Vector2(-5.5f, 6),
            center + new Vector2(5.5f, 6)
        };

        DrawColoredPolygon(points, color);
    }
}
