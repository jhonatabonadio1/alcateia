using Godot;

/// <summary>
/// Boneco de teste. Existe só para você enxergar o dano acontecendo:
/// pisca vermelho ao levar tiro, some ao morrer, volta no respawn.
///
/// Descartável. Quando o bot da Fase 1 ficar pronto, este nó sai.
/// </summary>
public partial class Dummy : StaticBody3D
{
    [Export] public Color BaseColor = new Color(0.75f, 0.75f, 0.78f);
    [Export] public Color HitColor = new Color(0.95f, 0.2f, 0.15f);

    /// <summary>Duração do flash vermelho, em segundos.</summary>
    [Export] public float FlashDuration = 0.12f;

    private Health _health;
    private MeshInstance3D _mesh;
    private CollisionShape3D _shape;
    private StandardMaterial3D _material;
    private double _flashTimer;

    public override void _Ready()
    {
        _health = GetNode<Health>("Health");
        _mesh = GetNode<MeshInstance3D>("MeshInstance3D");
        _shape = GetNode<CollisionShape3D>("CollisionShape3D");

        // Material próprio por instância, senão os três bonecos
        // compartilham o mesmo e piscam juntos.
        _material = new StandardMaterial3D { AlbedoColor = BaseColor };
        _mesh.MaterialOverride = _material;

        _health.Changed += OnHealthChanged;
        _health.Died += OnDied;
    }

    public override void _Process(double delta)
    {
        if (_flashTimer <= 0) return;

        _flashTimer -= delta;
        if (_flashTimer <= 0)
            _material.AlbedoColor = BaseColor;
    }

    private void OnHealthChanged(float hp, float maxHp, float shield)
    {
        if (_health.IsDead)
        {
            SetVisible(false);
            return;
        }

        // Voltou do respawn.
        if (!_mesh.Visible)
        {
            SetVisible(true);
            return;
        }

        _material.AlbedoColor = HitColor;
        _flashTimer = FlashDuration;

        GD.Print($"{Name}: {hp:0}/{maxHp:0}");
    }

    private void OnDied(int killerId)
    {
        GD.Print($"{Name} morreu");
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        _mesh.Visible = visible;
        _shape.Disabled = !visible;

        if (visible)
        {
            _material.AlbedoColor = BaseColor;
            _flashTimer = 0;
        }
    }
}
