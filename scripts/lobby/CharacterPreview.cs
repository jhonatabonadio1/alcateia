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
/// Os mesmos corpos da partida: o soldado do time azul (Exo Gray) e o
/// lobo (Mutant), cada um com o seu clipe de parado.
/// </summary>
public partial class CharacterPreview : SubViewportContainer
{
	private const string HumanPath = "res://assets/characters/human/exo_gray.fbx";
	private const string HumanFallback = "res://assets/characters/human/xbot.fbx";
	private const string HumanIdle = "res://assets/characters/human/idle_rifle.fbx";
	private const string WolfPath = "res://assets/characters/wolf/mutant.fbx";
	private const string WolfIdle = "res://assets/characters/wolf/idle.fbx";

	private SubViewport _viewport;
	private Node3D _pivot;
	private Node3D _human;
	private Node3D _wolfModel;
	private bool _wolf;
	private bool _dragging;

	public bool Wolf
	{
		get => _wolf;
		set { _wolf = value; ShowForm(); }
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

		_human = Load(ResourceLoader.Exists(HumanPath) ? HumanPath : HumanFallback, HumanIdle, 1f);
		_wolfModel = Load(WolfPath, WolfIdle, 1.0f);

		ShowForm();
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

	/// <summary>Um corpo com o clipe de parado em laço. O clipe vem de
	/// outro arquivo do Mixamo, escolhido como o CharacterAnimator escolhe.</summary>
	private Node3D Load(string modelPath, string idlePath, float scale)
	{
		if (!ResourceLoader.Exists(modelPath)) return null;

		var model = GD.Load<PackedScene>(modelPath).Instantiate<Node3D>();
		model.Scale = Vector3.One * scale;
		_pivot.AddChild(model);

		AnimationPlayer player = Nodes.FindFirst<AnimationPlayer>(model);
		if (player == null || !ResourceLoader.Exists(idlePath)) return model;

		Node temp = GD.Load<PackedScene>(idlePath).Instantiate();
		Animation idle = AnimationKit.TakeAnimation(Nodes.FindFirst<AnimationPlayer>(temp), "idle", false);
		temp.Free();
		if (idle == null) return model;

		AnimationKit.StripRootMotion(idle);
		idle.LoopMode = Animation.LoopModeEnum.Linear;

		var library = new AnimationLibrary();
		library.AddAnimation("idle", idle);
		player.AddAnimationLibrary("vitrine", library);
		player.Play("vitrine/idle");
		return model;
	}

	private void ShowForm()
	{
		if (_human != null) _human.Visible = !_wolf;
		if (_wolfModel != null) _wolfModel.Visible = _wolf;
	}
}
