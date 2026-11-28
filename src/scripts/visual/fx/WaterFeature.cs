using Godot;

/// <summary>
/// Água num lugar do mapa: a bacia de uma fonte, a boca de um poço (pedido do
/// Jhon, 27/11: "a fonte do mapa deserto teria que ter
/// textura, barulho e movimentação de água, não colisão... coloque uma água
/// bem feita"). Até então a água da fonte era a face azul lisa da peça do kit,
/// parada, muda e sólida.
///
/// O nó fica no meio da superfície, na altura dela, e monta tudo no
/// <c>_Ready</c>, por código (nada de recurso guardado: o mapa só diz as
/// medidas):
///
/// - a **superfície**, um disco com o <c>agua.gdshader</c>: a cor pela
///   profundidade, o fundo visto torto, cáusticas, ondas miúdas, os anéis de
///   onde a água cai e espuma na beira;
/// - com queda (<see cref="FallHeight"/>): a **cortina** de fios caindo em
///   volta do pilar (<c>agua_queda.gdshader</c>), o **filme** de água no alto,
///   o **jorro** que borbulha lá em cima, os **respingos** onde os fios batem
///   na bacia e uma névoa fina;
/// - o **som**, em laço, num ponto do mapa (<see cref="Sound"/>).
///
/// É só o que se vê e se ouve: **não tem colisão** (a bacia é da peça do mapa)
/// e não pesa em regra nenhuma. No servidor de partida (sem tela) não monta nada.
/// </summary>
public partial class WaterFeature : Node3D
{
	public const string SurfaceShader = "res://src/assets/efeitos/agua.gdshader";
	public const string FallShader = "res://src/assets/efeitos/agua_queda.gdshader";

	/// <summary>O raio da superfície da água (m).</summary>
	[Export] public float Radius = 1.9f;

	/// <summary>A queda: de quanto acima da água ela vem (m; 0: espelho
	/// d'água parado, sem cortina nem respingo), o raio de onde sai, lá em
	/// cima, e o de onde bate na bacia.</summary>
	[ExportGroup("Queda")]
	[Export] public float FallHeight;
	[Export] public float FallTopRadius = 0.5f;
	[Export] public float FallRadius = 0.62f;

	/// <summary>Quantos fios de água caem em volta (o pilar da fonte do
	/// Deserto tem oito faces).</summary>
	[Export] public int Streams = 8;

	/// <summary>O raio do filme de água em cima do pilar (0: nenhum).</summary>
	[Export] public float TopRadius = 0.38f;

	/// <summary>A cor da água rasa e da funda, e com quantos metros de água o
	/// fundo some.</summary>
	[ExportGroup("Água")]
	[Export] public Color Shallow = new(0.34f, 0.80f, 0.76f);
	[Export] public Color Deep = new(0.05f, 0.33f, 0.48f);
	[Export] public float Murk = 0.8f;

	/// <summary>O quanto a água se mexe: 1 a da fonte; menos, a do poço.</summary>
	[Export] public float Liveliness = 1f;

	/// <summary>O som em laço (o arquivo em <c>src/assets/audio</c>; vazio:
	/// mudo), até onde se ouve (m) e o volume (dB).</summary>
	[ExportGroup("Som")]
	[Export] public string Sound = "";
	[Export] public float SoundRange = 20f;
	[Export] public float SoundDb = -3f;

	/// <summary>O material da superfície (o teste confere que a água tem o shader dela).</summary>
	public ShaderMaterial Surface { get; private set; }

	/// <summary>O som da água tocando (nulo: muda, ou sem o arquivo).</summary>
	public Node SoundPlayer { get; private set; }

	public override void _Ready()
	{
		// O servidor de partida não desenha nem ouve.
		if (Inicio.IsServer(OS.GetCmdlineUserArgs())) return;

		Texture2D waves = Noise(0.9f, normal: true, seed: 11);
		Texture2D blots = Noise(1.6f, normal: false, seed: 29);

		Surface = new ShaderMaterial { Shader = GD.Load<Shader>(SurfaceShader) };
		Surface.SetShaderParameter("raso", new Vector3(Shallow.R, Shallow.G, Shallow.B));
		Surface.SetShaderParameter("fundo", new Vector3(Deep.R, Deep.G, Deep.B));
		Surface.SetShaderParameter("turvo", Murk);
		Surface.SetShaderParameter("agito", Liveliness);
		Surface.SetShaderParameter("queda", FallHeight > 0f ? FallRadius : 0f);
		Surface.SetShaderParameter("ruido", waves);
		Surface.SetShaderParameter("manchas", blots);
		AddChild(Still(new MeshInstance3D { Name = "Superficie", Mesh = Disc(Radius, 48), MaterialOverride = Surface }));

		if (FallHeight > 0f) BuildFall(blots);

		if (Sound != "") SoundPlayer = Sfx.Attached(this, Sound, SoundRange, own: false, SoundDb);
	}

	/// <summary>A cortina, o filme do alto, o jorro, os respingos e a névoa.</summary>
	private void BuildFall(Texture2D blots)
	{
		var fall = new ShaderMaterial { Shader = GD.Load<Shader>(FallShader) };
		fall.SetShaderParameter("fios", (float)Streams);
		fall.SetShaderParameter("manchas", blots);
		var curtain = new CylinderMesh
		{
			TopRadius = FallTopRadius, BottomRadius = FallRadius, Height = FallHeight,
			RadialSegments = Mathf.Max(Streams * 4, 16), Rings = 6, CapTop = false, CapBottom = false
		};
		AddChild(Still(new MeshInstance3D { Name = "Queda", Mesh = curtain, MaterialOverride = fall, Position = Vector3.Up * FallHeight * 0.5f }));

		if (TopRadius > 0f)
		{
			var film = (ShaderMaterial)Surface.Duplicate();
			film.SetShaderParameter("queda", 0f);
			film.SetShaderParameter("turvo", 0.25f);
			AddChild(Still(new MeshInstance3D
			{
				Name = "Alto", Mesh = Disc(TopRadius, 24), MaterialOverride = film,
				Position = Vector3.Up * (FallHeight + 0.012f)
			}));
		}

		Texture2D dot = Dot();
		// O jorro: a água brota no meio do alto e se abre.
		AddChild(Drops("Jorro", 56, 0.5f, Vector3.Up * (FallHeight + 0.02f), ring: 0.03f,
			direction: Vector3.Up, spread: 16f, speed: new Vector2(1.5f, 2.3f), size: new Vector2(0.035f, 0.07f), dot));
		// Os respingos: onde os fios batem na bacia.
		AddChild(Drops("Respingos", 90, 0.42f, Vector3.Up * 0.02f, ring: FallRadius,
			direction: Vector3.Up, spread: 38f, speed: new Vector2(0.7f, 1.7f), size: new Vector2(0.025f, 0.055f), dot));
		// A névoa: fina, subindo devagar em volta da queda.
		GpuParticles3D mist = Drops("Nevoa", 22, 1.6f, Vector3.Up * 0.1f, ring: FallRadius,
			direction: Vector3.Up, spread: 60f, speed: new Vector2(0.12f, 0.35f), size: new Vector2(0.25f, 0.5f), dot, gravity: 0.05f, alpha: 0.10f);
		AddChild(mist);
	}

	/// <summary>Uma malha da água: sem sombra (a água não faz sombra de disco no fundo dela).</summary>
	private static MeshInstance3D Still(MeshInstance3D mesh)
	{
		mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		return mesh;
	}

	/// <summary>Gotas: pontos redondos de borda macia, virados para a câmera.</summary>
	private GpuParticles3D Drops(string name, int amount, float life, Vector3 at, float ring, Vector3 direction, float spread,
		Vector2 speed, Vector2 size, Texture2D dot, float gravity = 9.8f, float alpha = 0.85f)
	{
		var fade = new Gradient();
		fade.SetColor(0, new Color(1, 1, 1, alpha));
		fade.SetColor(1, new Color(1, 1, 1, 0));
		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
			EmissionRingAxis = Vector3.Up, EmissionRingRadius = ring, EmissionRingInnerRadius = Mathf.Max(ring - 0.05f, 0f), EmissionRingHeight = 0.01f,
			Direction = direction, Spread = spread, InitialVelocityMin = speed.X, InitialVelocityMax = speed.Y,
			Gravity = Vector3.Down * gravity, ScaleMin = size.X, ScaleMax = size.Y,
			ColorRamp = new GradientTexture1D { Gradient = fade }
		};
		var material = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.9f, 0.97f, 1f), AlbedoTexture = dot, VertexColorUseAsAlbedo = true,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, BillboardKeepScale = true,
			DisableReceiveShadows = true
		};
		return new GpuParticles3D
		{
			Name = name, Amount = amount, Lifetime = life, Preprocess = life, Position = at,
			ProcessMaterial = process, DrawPass1 = new QuadMesh { Material = material, Size = Vector2.One },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			VisibilityAabb = new Aabb(new Vector3(-Radius, -0.5f, -Radius), new Vector3(Radius * 2f, FallHeight + 1.5f, Radius * 2f))
		};
	}

	/// <summary>Um disco deitado, virado para cima, com a tangente que o mapa
	/// de normais pede.</summary>
	private static ArrayMesh Disc(float radius, int sides)
	{
		var tool = new SurfaceTool();
		tool.Begin(Mesh.PrimitiveType.Triangles);
		for (int i = 0; i < sides; i++)
		{
			float a = Mathf.Tau * i / sides, b = Mathf.Tau * (i + 1) / sides;
			// (em sentido horário visto de cima — do +X para o +Z —: a frente da face é para cima)
			foreach (Vector3 p in new[] { Vector3.Zero, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius })
			{
				tool.SetNormal(Vector3.Up);
				tool.SetTangent(new Plane(Vector3.Right, 1f));
				tool.SetUV(new Vector2(p.X, p.Z) / (radius * 2f) + Vector2.One * 0.5f);
				tool.AddVertex(p);
			}
		}
		return tool.Commit();
	}

	/// <summary>Ruído que se repete sem emenda: em mapa de normais (as ondas)
	/// ou em cinza (a espuma, as cáusticas, os fios da queda).</summary>
	private static NoiseTexture2D Noise(float frequency, bool normal, int seed) => new()
	{
		Width = 256, Height = 256, Seamless = true, AsNormalMap = normal, BumpStrength = 6f, GenerateMipmaps = true,
		Noise = new FastNoiseLite
		{
			NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = frequency / 64f, Seed = seed,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 3
		}
	};

	/// <summary>Um ponto redondo de borda macia (a gota).</summary>
	private static GradientTexture2D Dot()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		return new GradientTexture2D
		{
			Gradient = gradient, Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = Vector2.One * 0.5f, FillTo = new Vector2(1f, 0.5f)
		};
	}
}
