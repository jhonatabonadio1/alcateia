using Godot;

/// <summary>
/// O personagem em 3D no inventário e na loja, girando devagar sobre um
/// pedestal, com humano e lobo alternáveis. É o que dá peso à tela.
///
/// Mundo próprio (SubViewport com own_world_3d): nada da cena vaza para
/// dentro, nem a luz da vitrine vaza para fora. Arrastar com o mouse
/// gira o modelo.
///
/// **Estúdio** (Jhon, 25/11: "coloque uma iluminação melhor, textura
/// melhor e cole eles no chão, está muito sem qualidade, meio escuro,
/// deixe AAA"):
/// - três pontos de luz: a principal quente, de cima e da frente, com
///   sombra macia; um preenchimento frio e fraco do outro lado; e dois
///   recortes atrás que separam a silhueta do fundo escuro. O céu do mapa
///   de dia (o HDR) dá o ambiente e o reflexo no metal da armadura;
/// - os pés no chão: o pedestal é escuro e fosco, recebe a sombra e a
///   oclusão (SSAO) e, embaixo dos pés, uma sombra de contato;
/// - nitidez: a imagem é desenhada na resolução da tela (numa tela Retina
///   eram 320 × 300 pontos esticados para 576 × 540), com MSAA e filtro
///   anisotrópico.
///
/// Os mesmos corpos da partida — o humano e o lobo de um personagem
/// (HumanModel, WolfModel), cada um com o seu clipe de parado — e o mesmo
/// material do lobo (WolfSkin).
/// </summary>
public partial class CharacterPreview : Control
{
	private const string HumanPath = "res://src/assets/personagens/eve/humano.glb";
	private const string HumanFallback = "res://src/assets/personagens/_comum/humano/xbot.fbx";
	private const string HumanIdle = "res://src/assets/personagens/_comum/humano/idle_rifle.fbx";
	private const string WolfPath = "res://src/assets/personagens/eve/lobo_azul.glb";
	private const string WolfIdle = "res://src/assets/personagens/_comum/lobo/idle.fbx";
	private const string SkyPath = "res://src/assets/mapas/ceus/dia_claro.hdr";

	/// <summary>Outro humano e outro lobo (um CharacterSkin). Antes de
	/// entrar na árvore.</summary>
	public string HumanModel { get; set; }
	public string WolfModel { get; set; }

	[ExportGroup("Estúdio")]
	/// <summary>A luz principal (quente, de cima e da frente), em energia
	/// do Godot. O suficiente para a pele e o pelo terem volume sem estourar
	/// o branco da pelagem do lobo.</summary>
	[Export] public float KeyEnergy = 1.3f;

	/// <summary>O preenchimento (frio, do outro lado): o lado de sombra não
	/// some no fundo escuro.</summary>
	[Export] public float FillEnergy = 0.4f;

	/// <summary>Os dois recortes atrás (frios), que desenham a borda da
	/// silhueta contra o fundo.</summary>
	[Export] public float RimEnergy = 3.5f;

	/// <summary>Quanto do céu do mapa entra como luz ambiente e reflexo.</summary>
	[Export] public float SkyEnergy = 0.25f;

	/// <summary>A cor da imagem: saturação, contraste e exposição. O tom de
	/// cinema desbota as cores fortes; isto as devolve (Jhon, 27/11: "deixe a
	/// saturação igual da foto"; "a luz tá muito forte, deixando eles
	/// brancos" — até 27/11 as luzes eram 2,1 / 0,55 / 7 / 0,55 e a
	/// saturação 1,05: a roupa da Eve saía cinza-esverdeada e o vermelho do
	/// Barut, apagado).</summary>
	[Export] public float Saturation = 1.4f;
	[Export] public float Contrast = 1.12f;
	[Export] public float Exposure = 0.9f;

	/// <summary>O quanto a sombra de contato escurece o tampo no meio,
	/// embaixo dos pés (0 a 1).</summary>
	[Export(PropertyHint.Range, "0,1")] public float ContactShadow = 0.75f;

	/// <summary>O halo atrás do personagem: a cor e o quanto ele aparece no
	/// meio (0 a 1).</summary>
	[Export] public Color BackdropColor = new(0.5f, 0.62f, 0.82f);
	[Export(PropertyHint.Range, "0,1")] public float BackdropStrength = 0.32f;

	/// <summary>Volta por segundo do giro sozinho, em radianos.</summary>
	[Export] public float Spin = 0.35f;

	private SubViewport _viewport;
	private TextureRect _image;
	private Node3D _pivot;
	private Node3D _human;
	private Node3D _wolfModel;
	private MeshInstance3D _contact;
	private bool _wolf;
	private bool _dragging;

	public bool Wolf
	{
		get => _wolf;
		set { _wolf = value; ShowForm(); }
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.Drag;
		ClipContents = true;

		_viewport = new SubViewport
		{
			OwnWorld3D = true,
			TransparentBg = true,
			Msaa3D = Viewport.Msaa.Msaa4X,
			ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
			AnisotropicFilteringLevel = Viewport.AnisotropicFiltering.Anisotropy16X,
			// Um pouco mais nítido que o padrão: a textura do lobo (4096) e a
			// do humano aparecem de perto e pequenas ao mesmo tempo.
			TextureMipmapBias = -0.5f,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Size = new Vector2I(320, 300)
		};
		AddChild(_viewport);

		// A imagem do estúdio, do tamanho do quadro. O viewport é desenhado na
		// resolução de verdade da tela (Resize), e aqui só encaixa.
		_image = new TextureRect
		{
			Texture = _viewport.GetTexture(),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = TextureFilterEnum.Linear
		};
		_image.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_image);
		Resized += Resize;

		_viewport.AddChild(new WorldEnvironment { Environment = Studio() });
		Lights();

		// Enquadra do pedestal à cabeça, com folga: o personagem inteiro, em
		// pé, de um pouco abaixo do peito (ele fica mais alto e mais forte do
		// que olhado de cima).
		var camera = new Camera3D { Position = new Vector3(0, 1.2f, 5.0f), Fov = 28f };
		_viewport.AddChild(camera);
		camera.LookAt(new Vector3(0, 0.76f, 0));

		_viewport.AddChild(Pedestal());
		_viewport.AddChild(Backdrop());

		_pivot = new Node3D();
		_viewport.AddChild(_pivot);

		string human = !string.IsNullOrEmpty(HumanModel) && ResourceLoader.Exists(HumanModel) ? HumanModel
			: ResourceLoader.Exists(HumanPath) ? HumanPath : HumanFallback;
		string wolf = !string.IsNullOrEmpty(WolfModel) && ResourceLoader.Exists(WolfModel) ? WolfModel : WolfPath;
		_human = Load(human, HumanIdle, 1f);
		_wolfModel = Load(wolf, WolfIdle, 1.0f, refit: true);
		// O mesmo material do jogo: neon aceso e pelo mexendo na vitrine.
		WolfSkin.Apply(_wolfModel);

		ShowForm();
		Resize();
	}

	/// <summary>O viewport na resolução da tela: o tamanho do quadro vezes
	/// quantos pixels há em cada ponto (2 numa tela Retina, 1,8 no Mac com a
	/// janela em 1920 × 1080 de pontos), até 2,5.</summary>
	private void Resize()
	{
		if (_viewport == null || Size.X < 1 || Size.Y < 1) return;
		Window root = GetTree()?.Root;
		float dpi = root == null ? 1f : root.Size.X / Mathf.Max(root.GetVisibleRect().Size.X, 1f);
		float scale = Mathf.Clamp(dpi * GetGlobalTransformWithCanvas().Scale.X, 1f, 2.5f);
		_viewport.Size = new Vector2I(Mathf.CeilToInt(Size.X * scale), Mathf.CeilToInt(Size.Y * scale));
	}

	public override void _Process(double delta)
	{
		if (!_dragging && _pivot != null)
			_pivot.RotateY((float)delta * Spin);
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

	/// <summary>O ambiente: o céu do mapa de dia como luz e reflexo (o fundo
	/// continua transparente, o painel aparece atrás), tons de cinema, um
	/// brilho leve no neon e oclusão de contato.</summary>
	private Godot.Environment Studio()
	{
		var env = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.ClearColor,
			AmbientLightSource = Godot.Environment.AmbientSource.Color,
			AmbientLightColor = new Color(0.62f, 0.68f, 0.78f),
			AmbientLightEnergy = 0.35f,
			ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
			TonemapMode = Godot.Environment.ToneMapper.Filmic,
			TonemapExposure = Exposure,
			TonemapWhite = 5f,
			SsaoEnabled = true,
			SsaoRadius = 0.45f,
			SsaoIntensity = 1.6f,
			SsaoPower = 1.4f,
			SsaoDetail = 0.6f,
			GlowEnabled = true,
			GlowIntensity = 0.55f,
			GlowBloom = 0.02f,
			GlowHdrThreshold = 1.1f,
			AdjustmentEnabled = true,
			AdjustmentContrast = Contrast,
			AdjustmentSaturation = Saturation
		};

		if (ResourceLoader.Exists(SkyPath) && GD.Load<Texture2D>(SkyPath) is Texture2D panorama)
		{
			env.Sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = panorama, EnergyMultiplier = SkyEnergy } };
			env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
			env.AmbientLightSkyContribution = 1f;
			env.AmbientLightEnergy = 1f;
			env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
		}
		return env;
	}

	private void Lights()
	{
		// A principal: quente, de cima, à frente e à esquerda de quem olha.
		// Sombra macia (o sol de 1,6° de largura) e curta: num quadro de 2 m,
		// toda a resolução da sombra fica nos pés.
		_viewport.AddChild(new DirectionalLight3D
		{
			Name = "Principal",
			RotationDegrees = new Vector3(-46, -52, 0),
			LightColor = new Color(1f, 0.93f, 0.83f),
			LightEnergy = KeyEnergy,
			ShadowEnabled = true,
			ShadowBlur = 1.4f,
			LightAngularDistance = 1.6f,
			DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
			DirectionalShadowMaxDistance = 9f,
			ShadowBias = 0.03f,
			ShadowNormalBias = 1.2f
		});

		// O preenchimento: frio, baixo, do outro lado, sem sombra.
		_viewport.AddChild(new DirectionalLight3D
		{
			Name = "Preenchimento",
			RotationDegrees = new Vector3(-12, 48, 0),
			LightColor = new Color(0.72f, 0.82f, 1f),
			LightEnergy = FillEnergy,
			LightSpecular = 0.2f
		});

		// Os recortes: dois focos atrás, um de cada lado, mirando o peito.
		foreach (float side in new[] { -1f, 1f })
		{
			var rim = new SpotLight3D
			{
				Name = side < 0 ? "RecorteEsquerdo" : "RecorteDireito",
				Position = new Vector3(1.5f * side, 2.3f, -2.1f),
				LightColor = side < 0 ? new Color(0.66f, 0.82f, 1f) : new Color(1f, 0.86f, 0.62f).Lerp(UiTokens.Accent, 0.35f),
				LightEnergy = RimEnergy * (side < 0 ? 1f : 0.75f),
				SpotRange = 6f,
				SpotAngle = 32f,
				SpotAttenuation = 0.8f,
				LightSpecular = 0.9f
			};
			_viewport.AddChild(rim);
			rim.LookAtFromPosition(rim.Position, new Vector3(0, 1.05f, 0), Vector3.Up);
		}
	}

	/// <summary>O pedestal: um disco escuro e fosco, com a borda chanfrada e
	/// um filete de luz na cor da marca. Recebe a sombra e a oclusão, e
	/// embaixo dos pés vai uma sombra de contato: é o que segura o corpo no
	/// chão.</summary>
	private Node3D Pedestal()
	{
		var root = new Node3D { Name = "Pedestal" };

		var top = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.92f, BottomRadius = 0.92f, Height = 0.05f, RadialSegments = 96 },
			Position = new Vector3(0, -0.025f, 0),
			MaterialOverride = new StandardMaterial3D
			{
				// Fosco o bastante para a sombra aparecer: polido, a câmera
				// baixa via o céu refletido no tampo (um espelho azul claro), e
				// a sombra dos pés sumia nele.
				AlbedoColor = new Color(0.055f, 0.06f, 0.07f),
				Metallic = 0.1f,
				MetallicSpecular = 0.25f,
				Roughness = 0.72f
			}
		};
		root.AddChild(top);

		var bevel = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.92f, BottomRadius = 0.98f, Height = 0.06f, RadialSegments = 96 },
			Position = new Vector3(0, -0.08f, 0),
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.055f, 0.065f), Metallic = 0.6f, Roughness = 0.45f }
		};
		root.AddChild(bevel);

		var ring = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = 0.905f, OuterRadius = 0.93f, Rings = 96, RingSegments = 8 },
			Position = new Vector3(0, -0.004f, 0),
			Scale = new Vector3(1, 0.3f, 1),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = UiTokens.Accent,
				EmissionEnabled = true,
				Emission = UiTokens.Accent,
				EmissionEnergyMultiplier = 1.6f
			}
		};
		root.AddChild(ring);

		// A sombra de contato: um degradê redondo escuro deitado no tampo,
		// embaixo dos pés (a principal faz a sombra do corpo; esta é a do
		// chão encostado nos pés, que a sombra direcional e a SSAO quase não
		// pegam com a luz de frente). Sem luz própria: escurece o que estiver
		// embaixo, seja qual for a luz.
		_contact = Glow("SombraDeContato", new PlaneMesh { Size = Vector2.One }, new Color(0, 0, 0), ContactShadow, 0.5f);
		_contact.Position = new Vector3(0, 0.003f, 0);
		root.AddChild(_contact);
		return root;
	}

	/// <summary>A luz no fundo: um halo frio e largo atrás do personagem,
	/// como o foco de estúdio batendo na parede. Tira o quadro do "azul
	/// escuro chapado" e a silhueta escura (o lobo do Barut) se destaca dele.
	/// Fica atrás e não recebe luz, então não muda o personagem.</summary>
	private Node3D Backdrop()
	{
		MeshInstance3D halo = Glow("Fundo", new QuadMesh { Size = new Vector2(4.4f, 3.4f) }, BackdropColor, BackdropStrength, 0.45f);
		halo.Position = new Vector3(0, 1.05f, -2.2f);
		return halo;
	}

	/// <summary>Uma malha sem luz própria com um degradê redondo: a cor no
	/// meio com a força <paramref name="strength"/>, metade dela em
	/// <paramref name="half"/> do raio, nada na borda.</summary>
	private static MeshInstance3D Glow(string name, Mesh mesh, Color color, float strength, float half)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color, strength));
		gradient.SetColor(1, new Color(color, 0f));
		gradient.AddPoint(half, new Color(color, strength * 0.5f));
		return new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoTexture = new GradientTexture2D
				{
					Gradient = gradient,
					Fill = GradientTexture2D.FillEnum.Radial,
					FillFrom = new Vector2(0.5f, 0.5f),
					FillTo = new Vector2(0.5f, 0f),
					Width = 256,
					Height = 256
				}
			}
		};
	}

	/// <summary>Um corpo com o clipe de parado em laço. O clipe vem de
	/// outro arquivo do Mixamo, escolhido como o CharacterAnimator escolhe.</summary>
	private Node3D Load(string modelPath, string idlePath, float scale, bool refit = false)
	{
		if (!ResourceLoader.Exists(modelPath)) return null;

		var model = GD.Load<PackedScene>(modelPath).Instantiate<Node3D>();
		model.Scale = Vector3.One * scale;
		_pivot.AddChild(model);

		AnimationPlayer player = Nodes.FindFirst<AnimationPlayer>(model);
		Skeleton3D body = AnimationKit.FindSkeleton(model);

		// O lobo do Meshy não traz tocador: ele nasce ao lado do esqueleto,
		// onde o caminho "Skeleton3D:osso" dos clipes existe.
		if (player == null && refit && body != null)
		{
			player = new AnimationPlayer { Name = "AnimationPlayer" };
			body.GetParent().AddChild(player);
		}
		// O humano de um GLB sem animação (os personagens do pacote Sci-Fi):
		// tocador na raiz, como no jogo (CharacterAnimator.SwapTeamModel).
		if (player == null && !refit && body != null)
		{
			player = new AnimationPlayer { Name = "AnimationPlayer" };
			model.AddChild(player);
		}
		if (player == null) return Seat(model, null, 0);

		// O humano fica na pose da vitrine: de pé, posturado, sem arma
		// (HeroPose), e não no parado de fuzil do jogo.
		Animation idle = null;
		if (!refit && body != null)
			idle = HeroPose.Make(body, model, player.GetNode(player.RootNode).GetPathTo(body).ToString());

		// O parado do Jhon (lobo_anim.glb), no mesmo ajuste do jogo.
		if (refit && body != null && WolfOwnClips.Has("idle"))
		{
			Node own = WolfOwnClips.Open();
			idle = WolfOwnClips.Fit(own, "idle", body, model, player.GetNode(player.RootNode).GetPathTo(body).ToString());
			own?.Free();
		}
		if (idle == null && !ResourceLoader.Exists(idlePath)) return Seat(model, null, 0);

		Node temp = idle == null ? GD.Load<PackedScene>(idlePath).Instantiate() : null;
		if (temp != null) idle = AnimationKit.TakeAnimation(Nodes.FindFirst<AnimationPlayer>(temp), "idle", false);
		if (temp != null && idle != null && refit && body != null)
		{
			// Mesmo ajuste do jogo (WolfVisual.FitClip): reorienta os ossos
			// ao corpo e leva o quadril à altura dele.
			AnimationKit.PointAt(idle, player.GetNode(player.RootNode).GetPathTo(body).ToString());
			AnimationKit.RetargetPose(idle, AnimationKit.FindSkeleton(temp), body);
			float hips = AnimationKit.HipsRestHeight(model), source = AnimationKit.HipsRestHeight(temp);
			if (hips > 0.01f && source > 0.01f) AnimationKit.MatchHips(idle, hips / source);
		}
		temp?.Free();
		if (idle == null) return Seat(model, null, 0);

		AnimationKit.StripRootMotion(idle);
		idle.LoopMode = Animation.LoopModeEnum.Linear;

		var library = new AnimationLibrary();
		library.AddAnimation("idle", idle);
		player.AddAnimationLibrary("vitrine", library);
		player.Play("vitrine/idle");
		return Seat(model, player, idle.Length);
	}

	/// <summary>
	/// Assenta o modelo no tampo do pedestal: o ponto mais baixo da pele, ao
	/// longo de todo o parado, encosta nele. O lobo, no parado agachado do
	/// Jhon, afundava 13 cm (o vermelho, 15): os pés e as garras
	/// atravessavam o disco (19/11: "pé atravessando o limite"), então ele
	/// não desce como no jogo (FootSink): fica pelas garras, e quem o segura
	/// no chão é a sombra de contato. Encostava 2 mm acima (25/11: zero).
	/// </summary>
	private static Node3D Seat(Node3D model, AnimationPlayer player, double length)
	{
		float low = float.MaxValue;
		int samples = player != null && length > 0.01 ? 16 : 1;
		for (int i = 0; i < samples; i++)
		{
			if (player != null && length > 0.01) player.Seek(length * i / samples, true);
			low = Mathf.Min(low, SkinPoints.Lowest(model).Low);
		}
		if (player != null && length > 0.01) player.Seek(0, true);
		if (low < float.MaxValue / 2) model.Position += Vector3.Up * -low;
		return model;
	}

	private void ShowForm()
	{
		if (_human != null) _human.Visible = !_wolf;
		if (_wolfModel != null) _wolfModel.Visible = _wolf;
		// O lobo é mais largo: a sombra de contato acompanha.
		if (_contact != null) _contact.Scale = _wolf ? new Vector3(1.35f, 1f, 1.1f) : new Vector3(0.8f, 1f, 0.7f);
	}
}
