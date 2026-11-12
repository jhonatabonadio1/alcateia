using Godot;

/// <summary>
/// O que o jogador vê nas próprias mãos: a arma que ele escolheu, não
/// uma genérica. A AK aparece como AK, a M3 como M3 — o modelo vem de
/// <see cref="WeaponModel"/>, o mesmo que os outros veem na sua mão e
/// no chão quando você solta.
///
/// Reage a tudo o que a arma faz: coice e clarão no tiro, balanço no
/// passo e a **recarga** — a arma inclina, o carregador sai por baixo e
/// volta, e ela endireita no fim. A recarga segue o relógio da própria
/// arma (<see cref="Weapon.ReloadProgress"/>), então uma recarga vazia
/// mais longa dura mais na tela também. Na escopeta cada cartucho é um
/// empurrão.
/// </summary>
public partial class ViewModel : Node3D
{
    [Export] public Color ClawColor = new Color(0.55f, 0.5f, 0.48f);

    /// <summary>Recuo do disparo, em metros.</summary>
    [Export] public float Kick = 0.08f;

    /// <summary>Balanço do passo.</summary>
    [Export] public float BobAmount = 0.015f;
    [Export] public float BobSpeed = 9f;

    /// <summary>Duração do clarão do cano.</summary>
    [Export] public float FlashLife = 0.045f;

    [Export] public Color FlashColor = new Color(1f, 0.85f, 0.45f);

    // Onde fica o punho de cada tipo de arma, a partir da câmera.
    // O modelo é em tamanho real; na mão ele fica um pouco menor e
    // levemente virado para o centro, para se ver o lado da arma e não
    // só a coronha encostada na câmera.
    private static readonly Vector3 RifleAnchor = new(0.17f, -0.17f, -0.44f);
    private static readonly Vector3 PistolAnchor = new(0.14f, -0.16f, -0.34f);
    private static readonly Vector3 KnifeAnchor = new(0.17f, -0.2f, -0.3f);
    private static readonly Vector3 GrenadeAnchor = new(0.17f, -0.19f, -0.32f);
    private const float ModelScale = 0.72f;
    private const float AimYaw = 4f;

    private Weapon _weapon;
    private FormController _form;
    private CharacterBody3D _body;

    private Node3D _held;
    private string _heldId = "";
    private Node3D _muzzle;
    private Node3D _magazine;
    private Vector3 _magRest;
    private Node3D _claws;
    private MeshInstance3D _flash;
    private double _flashTimer;

    private Vector3 _rest;
    private float _recoil;
    private float _bobTime;
    private float _reloadPose;
    private float _draw;

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
        if (_muzzle == null || _form.IsWolf || !Visible || _weapon.Held == null) return false;

        position = _muzzle.GlobalPosition;
        return true;
    }

    public override void _Ready()
    {
        _body = GetParent().GetParent().GetParent<CharacterBody3D>();
        _weapon = _body.GetNode<Weapon>("Weapon");
        _form = _body.GetNode<FormController>("FormController");

        _rest = Position;
        _claws = BuildClaws();

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
        float dt = (float)delta;
        _recoil = Mathf.MoveToward(_recoil, 0f, dt * 1.4f);
        _draw = Mathf.MoveToward(_draw, 0f, dt * 4f);

        if (_flashTimer > 0)
        {
            _flashTimer -= delta;
            if (_flashTimer <= 0) _flash.Visible = false;
        }

        var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
        bool walking = _body.IsOnFloor() && flat.Length() > 0.5f;
        if (walking) _bobTime += dt * BobSpeed;

        float bobX = walking ? Mathf.Sin(_bobTime) * BobAmount : 0f;
        float bobY = walking ? Mathf.Abs(Mathf.Cos(_bobTime)) * BobAmount : 0f;

        Position = _rest + new Vector3(bobX, bobY - _draw * 0.18f, _recoil);

        UpdateReload(dt);
    }

    /// <summary>
    /// A recarga em três tempos: inclina, o carregador sai e volta,
    /// endireita. A pose inclinada entra e sai suave; o carregador
    /// segue o progresso exato da recarga.
    /// </summary>
    private void UpdateReload(float dt)
    {
        if (_held == null) return;

        bool reloading = _weapon.IsReloading && _weapon.Held != null && !_form.IsWolf;
        _reloadPose = Mathf.MoveToward(_reloadPose, reloading ? 1f : 0f, dt * 6f);

        float p = reloading ? _weapon.ReloadProgress : 1f;
        float jolt = 0f;

        if (_magazine != null)
        {
            // 0–40%: sai por baixo. 40–55%: fora. 55–85%: volta. Depois,
            // um tapa no carregador — o tranco que diz "pronto".
            float drop = 0f;
            if (reloading)
            {
                if (p < 0.4f) drop = Smooth(p / 0.4f);
                else if (p < 0.55f) drop = 1f;
                else if (p < 0.85f) drop = 1f - Smooth((p - 0.55f) / 0.3f);
                else jolt = Mathf.Sin((p - 0.85f) / 0.15f * Mathf.Pi);
            }

            _magazine.Position = _magRest + new Vector3(0, -0.28f * drop, 0.04f * drop);
            _magazine.Visible = drop < 0.95f;
        }
        else if (reloading)
        {
            // Escopeta: cada cartucho é um empurrão para dentro.
            jolt = Mathf.Max(0f, Mathf.Sin(p * Mathf.Pi)) * 0.6f;
        }

        float r = Smooth(_reloadPose);
        _held.RotationDegrees = new Vector3(8f * r + 4f * jolt, AimYaw + 12f * r, 22f * r);
        _held.Position = Anchor(_heldId) + new Vector3(-0.05f * r, -0.05f * r + 0.012f * jolt, 0.04f * r);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void OnFired()
    {
        _recoil = Kick;
        if (_form.IsWolf || _muzzle == null || _weapon.Held == null) return;

        _flash.Position = ToLocal(_muzzle.GlobalPosition);
        _flash.Visible = true;
        _flashTimer = FlashLife;
    }

    /// <summary>Mostra só o que está na mão, com o modelo da arma
    /// escolhida. Trocar de arma reconstrói e a nova sobe de baixo.</summary>
    private void Refresh()
    {
        bool wolf = _form.IsWolf;
        _claws.Visible = wolf;

        string id = wolf ? "" : _weapon.HumanSlot switch
        {
            Slot.Primary => _weapon.PrimaryDef?.Id ?? "",
            Slot.Secondary => _weapon.SecondaryDef?.Id ?? "",
            Slot.Knife => "faca",
            Slot.Grenade => "choque",
            _ => ""
        };

        if (id == _heldId && _held != null) return;

        _held?.QueueFree();
        _held = null;
        _muzzle = null;
        _magazine = null;
        _heldId = id;

        if (id == "") return;

        _held = WeaponModel.Build(id);
        _held.Position = Anchor(id);
        _held.Scale = Vector3.One * ModelScale;
        _held.RotationDegrees = new Vector3(0, AimYaw, 0);
        AddChild(_held);

        _muzzle = _held.GetNodeOrNull<Node3D>(WeaponModel.MuzzleName);
        _magazine = _held.GetNodeOrNull<Node3D>(WeaponModel.MagazineName);
        if (_magazine != null) _magRest = _magazine.Position;

        _draw = 1f;
    }

    private static Vector3 Anchor(string id) => id switch
    {
        "faca" => KnifeAnchor,
        "choque" => GrenadeAnchor,
        _ when WeaponModel.IsPistol(id) => PistolAnchor,
        _ => RifleAnchor
    };

    // ------------------------------------------------------------------

    /// <summary>Garras: três lâminas em leque, uma de cada lado.</summary>
    private Node3D BuildClaws()
    {
        var root = new Node3D();
        AddChild(root);

        var material = new StandardMaterial3D { AlbedoColor = ClawColor, Roughness = 0.55f, Metallic = 0.1f };

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                var claw = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.025f, 0.025f, 0.22f) },
                    MaterialOverride = material,
                    Position = new Vector3(side * (0.18f + i * 0.05f), -0.26f + i * 0.02f, -0.34f),
                    RotationDegrees = new Vector3(-12f, side * (8f + i * 6f), 0)
                };
                root.AddChild(claw);
            }
        }

        return root;
    }
}
