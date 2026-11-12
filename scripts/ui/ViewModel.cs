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
    private Node3D _knife;

    private Node3D _rifleMuzzle;
    private Node3D _pistolMuzzle;
    private MeshInstance3D _flash;
    private double _flashTimer;

    private Vector3 _rest;
    private float _recoil;
    private float _bobTime;

    /// <summary>Duração do clarão do cano.</summary>
    [Export] public float FlashLife = 0.045f;

    [Export] public Color FlashColor = new Color(1f, 0.85f, 0.45f);

    /// <summary>
    /// Ponta do cano da arma na mão, em coordenadas de mundo.
    ///
    /// O traçante sai daqui, não da câmera. O raio continua partindo
    /// do centro da tela, porque é dele que vem a precisão — mas o
    /// jogador vê a linha nascer no cano, que é de onde ela deveria
    /// nascer. Desenhar a partir da câmera deixa o tiro saindo da
    /// testa, e é o artefato que mais entrega um FPS improvisado.
    /// </summary>
    public bool TryGetMuzzle(out Vector3 position)
    {
        position = Vector3.Zero;

        Node3D muzzle = _weapon.HumanSlot switch
        {
            Slot.Primary => _rifleMuzzle,
            Slot.Secondary => _pistolMuzzle,
            _ => null
        };

        if (muzzle == null || _form.IsWolf || !Visible) return false;

        position = muzzle.GlobalPosition;
        return true;
    }

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
        _knife = BuildKnife();

        _weapon.SlotChanged += _ => Refresh();
        _form.FormChanged += _ => Refresh();
        _weapon.Fired += OnFired;

        _flash = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = FlashColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            },
            Visible = false
        };
        AddChild(_flash);

        Refresh();
    }

    public override void _Process(double delta)
    {
        _recoil = Mathf.MoveToward(_recoil, 0f, (float)delta * 1.4f);

        if (_flashTimer > 0)
        {
            _flashTimer -= delta;

            if (_flashTimer <= 0)
                _flash.Visible = false;
        }

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

        if (_form.IsWolf) return;

        Node3D muzzle = _weapon.HumanSlot == Slot.Primary ? _rifleMuzzle : _pistolMuzzle;
        if (muzzle == null) return;

        _flash.Position = muzzle.Position + muzzle.GetParent<Node3D>().Position;
        _flash.Visible = true;
        _flashTimer = FlashLife;
    }

    /// <summary>Mostra só o que está na mão.</summary>
    private void Refresh()
    {
        bool wolf = _form.IsWolf;

        _claws.Visible = wolf;
        _rifle.Visible = !wolf && _weapon.HumanSlot == Slot.Primary;
        _pistol.Visible = !wolf && _weapon.HumanSlot == Slot.Secondary;
        _grenade.Visible = !wolf && _weapon.HumanSlot == Slot.Grenade;
        _knife.Visible = !wolf && _weapon.HumanSlot == Slot.Knife;
    }

    // ------------------------------------------------------------------

    private Node3D BuildRifle()
    {
        var root = NewPart();

        Box(root, RifleColor, new Vector3(0.07f, 0.09f, 0.5f), new Vector3(0.22f, -0.2f, -0.38f));
        Box(root, RifleColor, new Vector3(0.04f, 0.05f, 0.34f), new Vector3(0.22f, -0.17f, -0.72f));

        _rifleMuzzle = new Node3D { Position = new Vector3(0.22f, -0.17f, -0.9f) };
        root.AddChild(_rifleMuzzle);
        Box(root, RifleColor, new Vector3(0.05f, 0.16f, 0.07f), new Vector3(0.22f, -0.3f, -0.26f));
        Box(root, RifleColor, new Vector3(0.05f, 0.07f, 0.2f), new Vector3(0.22f, -0.21f, -0.1f));

        return root;
    }

    private Node3D BuildPistol()
    {
        var root = NewPart();

        Box(root, PistolColor, new Vector3(0.06f, 0.08f, 0.26f), new Vector3(0.2f, -0.22f, -0.3f));
        Box(root, PistolColor, new Vector3(0.05f, 0.15f, 0.07f), new Vector3(0.2f, -0.31f, -0.22f));

        _pistolMuzzle = new Node3D { Position = new Vector3(0.2f, -0.22f, -0.44f) };
        root.AddChild(_pistolMuzzle);

        return root;
    }

    private Node3D BuildKnife()
    {
        var root = NewPart();

        Box(root, new Color(0.62f, 0.63f, 0.66f),
            new Vector3(0.02f, 0.06f, 0.26f), new Vector3(0.2f, -0.24f, -0.34f));

        Box(root, new Color(0.16f, 0.16f, 0.18f),
            new Vector3(0.035f, 0.05f, 0.12f), new Vector3(0.2f, -0.25f, -0.16f));

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
