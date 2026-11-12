using Godot;

/// <summary>
/// O personagem em 3D na loja, girando devagar sobre um pedestal, com
/// humano e lobo alternáveis. Ocupa um terço da tela e é o que dá peso
/// à loja.
///
/// Mundo próprio (SubViewport com own_world_3d): nada da cena vaza para
/// dentro, nem a luz da vitrine vaza para fora. Arrastar com o mouse
/// gira o modelo.
///
/// O lobo ainda não tem modelo: a vitrine mostra o corpo do humano maior
/// e na cor da pelagem — o mesmo provisório que a partida usa.
/// </summary>
public partial class CharacterPreview : SubViewportContainer
{
    private const string ModelPath = "res://assets/characters/human/xbot.fbx";
    private const string IdlePath = "res://assets/characters/human/idle_rifle.fbx";

    private SubViewport _viewport;
    private Node3D _pivot;
    private Node3D _model;
    private bool _wolf;
    private bool _dragging;

    public bool Wolf
    {
        get => _wolf;
        set { _wolf = value; Paint(); }
    }

    public override void _Ready()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Drag;

        _viewport = new SubViewport
        {
            OwnWorld3D = true,
            TransparentBg = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(_viewport);

        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("aeb8c8"),
                AmbientLightEnergy = 0.3f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        };
        _viewport.AddChild(env);

        var key = new DirectionalLight3D { RotationDegrees = new Vector3(-35, 35, 0), LightEnergy = 0.75f, ShadowEnabled = true };
        _viewport.AddChild(key);

        // Luz de recorte atrás, na cor da marca: separa a silhueta do fundo.
        var rim = new OmniLight3D { Position = new Vector3(-1.4f, 2.2f, -1.6f), LightColor = UiTokens.Accent, LightEnergy = 2.2f, OmniRange = 5f };
        _viewport.AddChild(rim);

        // Enquadra do pedestal à cabeça, com folga: o personagem inteiro,
        // em pé, um pouco de cima.
        var camera = new Camera3D { Position = new Vector3(0, 1.35f, 4.6f), Fov = 30f };
        _viewport.AddChild(camera);
        camera.LookAt(new Vector3(0, 0.82f, 0));

        _viewport.AddChild(Pedestal());

        _pivot = new Node3D();
        _viewport.AddChild(_pivot);

        if (ResourceLoader.Exists(ModelPath))
        {
            _model = GD.Load<PackedScene>(ModelPath).Instantiate<Node3D>();
            _pivot.AddChild(_model);
            PlayIdle();
        }

        Paint();
    }

    public override void _Process(double delta)
    {
        if (!_dragging && _pivot != null)
            _pivot.RotateY((float)delta * 0.35f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _dragging = button.Pressed;
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion motion && _dragging && _pivot != null)
        {
            _pivot.RotateY(motion.Relative.X * 0.01f);
            AcceptEvent();
        }
    }

    private static Node3D Pedestal()
    {
        var root = new Node3D();

        var disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.85f, BottomRadius = 0.9f, Height = 0.06f, RadialSegments = 48 },
            Position = new Vector3(0, -0.03f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("1c222b"), Roughness = 0.8f }
        };
        root.AddChild(disc);

        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.86f, OuterRadius = 0.9f, Rings = 48, RingSegments = 8 },
            Position = new Vector3(0, 0.005f, 0),
            Scale = new Vector3(1, 0.2f, 1),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = UiTokens.Accent,
                EmissionEnabled = true,
                Emission = UiTokens.Accent,
                EmissionEnergyMultiplier = 1.4f
            }
        };
        root.AddChild(ring);
        return root;
    }

    /// <summary>A animação de parado vem de outro arquivo do Mixamo; é a
    /// mesma escolha que o CharacterAnimator faz (o clipe "mixamo").</summary>
    private void PlayIdle()
    {
        AnimationPlayer player = Find<AnimationPlayer>(_model);
        if (player == null || !ResourceLoader.Exists(IdlePath)) return;

        Node temp = GD.Load<PackedScene>(IdlePath).Instantiate();
        AnimationPlayer source = Find<AnimationPlayer>(temp);
        Animation idle = null;

        if (source != null)
        {
            foreach (StringName name in source.GetAnimationList())
            {
                Animation candidate = source.GetAnimation(name);
                if (idle == null || name.ToString().ToLowerInvariant().Contains("mixamo")) idle = candidate;
            }
        }

        temp.QueueFree();
        if (idle == null) return;

        idle = (Animation)idle.Duplicate();
        idle.LoopMode = Animation.LoopModeEnum.Linear;

        var library = new AnimationLibrary();
        library.AddAnimation("idle", idle);
        player.AddAnimationLibrary("vitrine", library);
        player.Play("vitrine/idle");
    }

    private void Paint()
    {
        if (_model == null) return;

        Color body = _wolf ? new Color("3a3029") : new Color("4f5b6b");
        Color joints = _wolf ? new Color("1b1613") : new Color("2b313b");
        _pivot.Scale = Vector3.One * (_wolf ? 1.12f : 1f);

        Apply(_model);

        void Apply(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                bool isJoint = mesh.Name.ToString().ToLowerInvariant().Contains("joint");
                mesh.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = isJoint ? joints : body,
                    Roughness = _wolf ? 0.95f : 0.55f,
                    Metallic = _wolf ? 0f : 0.25f
                };
            }

            foreach (Node child in node.GetChildren()) Apply(child);
        }
    }

    private static T Find<T>(Node node) where T : Node
    {
        if (node is T found) return found;
        foreach (Node child in node.GetChildren())
        {
            T inner = Find<T>(child);
            if (inner != null) return inner;
        }
        return null;
    }
}
