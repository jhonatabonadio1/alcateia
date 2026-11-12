using Godot;

/// <summary>
/// O que o jogador vê nas próprias mãos.
///
/// Formas geométricas por enquanto — substituir por modelo é trocar a
/// malha, não reescrever isto. O que importa já funciona: saber qual
/// slot está na mão sem olhar o HUD, e ver a arma reagir ao tiro e ao
/// passo.
///
/// Fica numa camada de render própria, com a câmera enxergando-a por
/// cima do mundo: sem isso a arma atravessa parede quando você encosta
/// nela, que é o artefato mais visível de um FPS mal montado.
/// </summary>
public partial class ViewModel : Node3D
{
    [Export] public Color RifleColor = new Color(0.22f, 0.23f, 0.26f);
    [Export] public Color PistolColor = new Color(0.3f, 0.31f, 0.34f);
    [Export] public Color GrenadeColor = new Color(0.3f, 0.38f, 0.26f);
    [Export] public Color ClawColor = new Color(0.55f, 0.5f, 0.48f);

    /// <summary>Recuo do disparo, em metros.</summary>
    [Export] public float Kick = 0.08f;

    /// <summary>Balanço do passo.</summary>
    [Export] public float BobAmount = 0.015f;
    [Export] public float BobSpeed = 9f;

    private Weapon _weapon;
    private FormController _form;
    private CharacterBody3D _body;

    private Node3D _rifle;
    private Node3D _pistol;
    private Node3D _grenade;
    private Node3D _claws;

    private Vector3 _rest;
    private float _recoil;
    private float _bobTime;

    public override void _Ready()
    {
        _body = GetParent().GetParent().GetParent<CharacterBody3D>();
        _weapon = _body.GetNode<Weapon>("Weapon");
        _form = _body.GetNode<FormController>("FormController");

        _rest = Position;

        _rifle = BuildRifle();
        _pistol = BuildPistol();
        _grenade = BuildGrenade();
        _claws = BuildClaws();

        _weapon.SlotChanged += _ => Refresh();
        _form.FormChanged += _ => Refresh();
        _weapon.Fired += OnFired;

        Refresh();
    }

    public override void _Process(double delta)
    {
        _recoil = Mathf.MoveToward(_recoil, 0f, (float)delta * 1.4f);

        var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
        bool walking = _body.IsOnFloor() && flat.Length() > 0.5f;

        if (walking)
            _bobTime += (float)delta * BobSpeed;

        float bobX = walking ? Mathf.Sin(_bobTime) * BobAmount : 0f;
        float bobY = walking ? Mathf.Abs(Mathf.Cos(_bobTime)) * BobAmount : 0f;

        Position = _rest + new Vector3(bobX, bobY, _recoil);
    }

    private void OnFired()
    {
        _recoil = Kick;
    }

    /// <summary>Mostra só o que está na mão.</summary>
    private void Refresh()
    {
        bool wolf = _form.IsWolf;

        _claws.Visible = wolf;
        _rifle.Visible = !wolf && _weapon.HumanSlot == Slot.Primary;
        _pistol.Visible = !wolf && _weapon.HumanSlot == Slot.Secondary;
        _grenade.Visible = !wolf && _weapon.HumanSlot == Slot.Grenade;
    }

    // ------------------------------------------------------------------

    private Node3D BuildRifle()
    {
        var root = NewPart();

        Box(root, RifleColor, new Vector3(0.07f, 0.09f, 0.5f), new Vector3(0.22f, -0.2f, -0.38f));
        Box(root, RifleColor, new Vector3(0.04f, 0.05f, 0.34f), new Vector3(0.22f, -0.17f, -0.72f));
        Box(root, RifleColor, new Vector3(0.05f, 0.16f, 0.07f), new Vector3(0.22f, -0.3f, -0.26f));
        Box(root, RifleColor, new Vector3(0.05f, 0.07f, 0.2f), new Vector3(0.22f, -0.21f, -0.1f));

        return root;
    }

    private Node3D BuildPistol()
    {
        var root = NewPart();

        Box(root, PistolColor, new Vector3(0.06f, 0.08f, 0.26f), new Vector3(0.2f, -0.22f, -0.3f));
        Box(root, PistolColor, new Vector3(0.05f, 0.15f, 0.07f), new Vector3(0.2f, -0.31f, -0.22f));

        return root;
    }

    private Node3D BuildGrenade()
    {
        var root = NewPart();

        var sphere = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
            MaterialOverride = Flat(GrenadeColor),
            Position = new Vector3(0.2f, -0.24f, -0.3f)
        };
        root.AddChild(sphere);

        return root;
    }

    /// <summary>Garras: três lâminas em leque, uma de cada lado.</summary>
    private Node3D BuildClaws()
    {
        var root = NewPart();

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                var claw = Box(root, ClawColor,
                    new Vector3(0.025f, 0.025f, 0.22f),
                    new Vector3(side * (0.18f + i * 0.05f), -0.26f + i * 0.02f, -0.34f));

                claw.RotationDegrees = new Vector3(-12f, side * (8f + i * 6f), 0);
            }
        }

        return root;
    }

    private Node3D NewPart()
    {
        var node = new Node3D();
        AddChild(node);
        return node;
    }

    private MeshInstance3D Box(Node parent, Color color, Vector3 size, Vector3 position)
    {
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Flat(color),
            Position = position
        };

        parent.AddChild(node);
        return node;
    }

    private static StandardMaterial3D Flat(Color color) => new()
    {
        AlbedoColor = color,
        Roughness = 0.55f,
        Metallic = 0.1f
    };
}
