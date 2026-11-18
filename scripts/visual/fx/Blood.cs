using Godot;
using System.Collections.Generic;

/// <summary>
/// Sangue ao acertar (pedido do Jhon, 18/11): tiro em gente e garra de
/// lobo.
///
/// **Refeito no mesmo dia** ("cria um fx para o sangue, não deixe-o
/// geométrico assim"). Antes tudo era disco: gota redonda, névoa redonda,
/// mancha de círculos. Agora nenhuma forma é uma figura:
///
/// - as formas são **campos de metabolas** gerados aqui (bolas que se
///   fundem quando estão perto e se separam em gotas quando afastam):
///   borrões irregulares, fios que afinam e se partem em gotas na ponta;
/// - o material corta o campo num limiar em vez de só apagar: a borda é
///   nítida em qualquer tamanho, e envelhecer **afina e parte** o sangue
///   (erosão), como líquido, em vez de um disco ficando transparente;
/// - o miolo grosso é escuro e a borda fina é viva: lê como volume.
///
/// Cinco camadas, todas curtas, para dizer "acertou" sem sujar a tela:
///
/// - **estouro**: um respingo no ponto do acerto, de frente para a
///   câmera, que abre num décimo de segundo e se desfaz;
/// - **jato**: fios de sangue para onde o golpe empurra (o tiro, para
///   trás de quem levou; a garra, para o lado do corte);
/// - **gotas** e **respingo fino**: partículas esticadas no sentido em
///   que voam, cada uma com uma forma sorteada, que caem e afinam;
/// - **névoa**: um sopro de fumaça vermelha, que é o que se lê de longe;
/// - **mancha**: se há parede ou chão logo atrás, uma mancha molhada (com
///   luz e brilho de verdade) que fica uns segundos e seca. Na parede,
///   escorre. No máximo <see cref="MaxStains"/> no mapa; a mais velha sai
///   primeiro.
///
/// Sem textura de arquivo e sem nada que só o Forward+ tenha — vale para
/// o renderizador de compatibilidade também.
///
/// Quem chama é a arma, no ponto exato do acerto (<see cref="Weapon"/>);
/// o boneco de outro jogador também atira, então cada máquina vê o sangue
/// dos tiros que vê.
/// </summary>
public static class Blood
{
	/// <summary>Ligado nas opções (Vídeo → Sangue).</summary>
	public static bool Enabled => Settings.Values.Blood;

	/// <summary>Quantas manchas ficam no mapa ao mesmo tempo.</summary>
	public const int MaxStains = 48;

	/// <summary>Quanto tempo a mancha fica, em segundos, antes de secar.</summary>
	public const float StainLife = 12f;

	/// <summary>Esguichos ao mesmo tempo. Uma rajada de escopeta em quatro
	/// pessoas não precisa de quarenta.</summary>
	public const int MaxBursts = 24;

	/// <summary>O miolo grosso e a borda fina do sangue.</summary>
	private static readonly Color Thick = new(0.27f, 0.010f, 0.016f);
	private static readonly Color Thin = new(0.66f, 0.035f, 0.04f);

	private const int BlobFrames = 4;      // folha de gotas: 4 x 4 formas
	private const int MistFrames = 2;      // folha de névoa: 2 x 2
	private const int Variants = 3;        // estouros, jatos e manchas diferentes

	/// <summary>O som do sangue (<c>sangue_1..n</c>) e até onde chega.</summary>
	public const string Sound = "sangue";
	public const float SoundRadius = 18f;
	private const ulong SoundGapMsec = 70;
	private static ulong _lastSound;

	private static readonly Queue<Node3D> Stains = new();
	private static int _bursts;
	private static Shader _shader;
	private static Texture2D _blobs, _mists, _drip;
	private static Texture2D[] _splash, _jet, _stainShape;
	private static ShaderMaterial _dropMaterial, _mistMaterial;
	private static readonly RandomNumberGenerator Rng = new();

	/// <summary>Gera as formas e os materiais agora (a primeira vez custa
	/// alguns milésimos): chamar na carga da partida evita o tranco no
	/// primeiro tiro que acerta.</summary>
	public static void Warm() => EnsureMaterials();

	/// <summary>
	/// Sangue em <paramref name="at"/>, saindo na direção
	/// <paramref name="direction"/>. <paramref name="size"/> 1 é um tiro no
	/// corpo; cabeça e garra passam disso.
	/// </summary>
	/// <param name="world">Um nó do mundo da partida (o pai do corpo).</param>
	/// <param name="ignore">O corpo atingido: a mancha não vai nele.</param>
	public static void Spray(Node world, Vector3 at, Vector3 direction, float size = 1f, CollisionObject3D ignore = null)
	{
		if (!Enabled || world == null || !world.IsInsideTree() || _bursts >= MaxBursts) return;

		direction = direction.LengthSquared() < 0.0001f ? Vector3.Up : direction.Normalized();
		EnsureMaterials();

		// O molhado do sangue espirrando, por cima do impacto (que é da arma).
		ulong now = Time.GetTicksMsec();
		if (now - _lastSound >= SoundGapMsec)
		{
			_lastSound = now;
			Sfx.At(Sound, at, SoundRadius, -4f);
		}

		var root = new Node3D { Name = "Sangue", TopLevel = true };
		world.AddChild(root);
		root.GlobalPosition = at;
		_bursts++;
		root.TreeExiting += () => _bursts--;

		Vector3 eye = world.GetViewport()?.GetCamera3D()?.GlobalPosition ?? at + Vector3.Back * 3f;

		root.AddChild(Pop(size));
		root.AddChild(Jet(direction, eye - at, size));
		root.AddChild(Drops(direction, size));
		root.AddChild(Fine(direction, size));
		root.AddChild(Mist(direction, size));

		// Mancha atrás de quem levou, ou no chão logo abaixo.
		Stain(world, at, direction, size, ignore);

		root.GetTree().CreateTimer(1.3).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(root)) root.QueueFree();
		};
	}

	// ------------------------------------------------------------------ camadas

	/// <summary>O estouro no ponto do acerto: um respingo de frente para a
	/// câmera que abre e se desfaz em fios e gotas.</summary>
	private static MeshInstance3D Pop(float size)
	{
		ShaderMaterial material = Material(_splash[Rng.RandiRange(0, Variants - 1)], frames: 1);
		material.SetShaderParameter("billboard", true);
		material.SetShaderParameter("spin", Rng.RandfRange(0, Mathf.Tau));
		float side = 0.62f * Mathf.Sqrt(size);
		var pop = new MeshInstance3D
		{
			Mesh = new QuadMesh { Size = Vector2.One * side },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Scale = Vector3.One * 0.3f
		};
		pop.Ready += () =>
		{
			Tween tween = pop.CreateTween().SetParallel();
			tween.TweenProperty(pop, "scale", Vector3.One, 0.11).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			Animate(tween, material, "erode", 0f, 1f, 0.26, delay: 0.05, easeIn: true);
			Animate(tween, material, "opacity", 1f, 0f, 0.08, delay: 0.27);
		};
		return pop;
	}

	/// <summary>Anima um parâmetro do material. Por método, não por
	/// propriedade: num material criado em código o Godot só enxerga
	/// "shader_parameter/x" como propriedade depois que alguém lista as
	/// propriedades dele.</summary>
	private static void Animate(Tween tween, ShaderMaterial material, string parameter, float from, float to, double seconds, double delay = 0, bool easeIn = false)
	{
		MethodTweener step = tween.TweenMethod(Callable.From((float value) => material.SetShaderParameter(parameter, value)), from, to, seconds);
		step.SetDelay(delay);
		if (easeIn) step.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
	}

	/// <summary>O jato: fios que saem do ponto para onde o golpe empurra,
	/// num plano virado o mais possível para a câmera.</summary>
	private static MeshInstance3D Jet(Vector3 direction, Vector3 toEye, float size)
	{
		ShaderMaterial material = Material(_jet[Rng.RandiRange(0, _jet.Length - 1)], frames: 1);
		float length = 0.95f * Mathf.Sqrt(size), height = 0.48f * Mathf.Sqrt(size);
		var jet = new MeshInstance3D
		{
			// A origem fica na ponta de onde o jato sai.
			Mesh = new QuadMesh { Size = new Vector2(length, height), CenterOffset = new Vector3(length * 0.5f, 0, 0) },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};

		// X ao longo do golpe; o plano do desenho, de frente para a câmera.
		Vector3 across = direction.Cross(toEye);
		if (across.LengthSquared() < 0.04f * toEye.LengthSquared())
			across = direction.Cross(Mathf.Abs(direction.Y) < 0.9f ? Vector3.Up : Vector3.Right);
		across = across.Normalized();
		if (Rng.Randf() < 0.5f) across = -across;
		var basis = new Basis(direction, across, direction.Cross(across).Normalized());
		jet.Ready += () =>
		{
			jet.GlobalBasis = basis.Scaled(new Vector3(0.22f, 0.7f, 1f));
			Tween tween = jet.CreateTween().SetParallel();
			tween.TweenProperty(jet, "scale", Vector3.One, 0.15).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
			Animate(tween, material, "erode", 0f, 1f, 0.3, delay: 0.08, easeIn: true);
			Animate(tween, material, "opacity", 1f, 0f, 0.08, delay: 0.32);
		};
		return jet;
	}

	/// <summary>As gotas: caem com a gravidade, esticadas no sentido em que voam.</summary>
	private static GpuParticles3D Drops(Vector3 direction, float size)
	{
		float root = Mathf.Sqrt(size);
		var process = new ParticleProcessMaterial
		{
			Direction = direction,
			Spread = 34f,
			InitialVelocityMin = 1.6f * root,
			InitialVelocityMax = 5.6f * root,
			Gravity = new Vector3(0, -11f, 0),
			DampingMin = 0.8f,
			DampingMax = 2.6f,
			ScaleMin = 0.45f,
			ScaleMax = 1.35f,
			AnimOffsetMin = 0f,
			AnimOffsetMax = 1f
		};
		return Particles(process, _dropMaterial, new Vector2(0.085f, 0.15f) * root,
			Mathf.Clamp(Mathf.RoundToInt(22 * size), 10, 48), 0.72, explosiveness: 0.93f, velocityAligned: true);
	}

	/// <summary>O respingo fino: gotas pequenas e rápidas, num leque estreito.</summary>
	private static GpuParticles3D Fine(Vector3 direction, float size)
	{
		float root = Mathf.Sqrt(size);
		var process = new ParticleProcessMaterial
		{
			Direction = direction,
			Spread = 17f,
			InitialVelocityMin = 4f * root,
			InitialVelocityMax = 9.5f * root,
			Gravity = new Vector3(0, -12f, 0),
			DampingMin = 2f,
			DampingMax = 5f,
			ScaleMin = 0.5f,
			ScaleMax = 1.3f,
			AnimOffsetMin = 0f,
			AnimOffsetMax = 1f
		};
		return Particles(process, _dropMaterial, new Vector2(0.03f, 0.2f) * root,
			Mathf.Clamp(Mathf.RoundToInt(14 * size), 6, 30), 0.42, explosiveness: 0.96f, velocityAligned: true);
	}

	/// <summary>A névoa: fumaça vermelha que abre e some.</summary>
	private static GpuParticles3D Mist(Vector3 direction, float size)
	{
		var grow = new Curve();
		grow.AddPoint(new Vector2(0, 0.45f));
		grow.AddPoint(new Vector2(1, 1.25f));
		var process = new ParticleProcessMaterial
		{
			Direction = direction,
			Spread = 55f,
			InitialVelocityMin = 0.3f,
			InitialVelocityMax = 1.5f,
			Gravity = new Vector3(0, -0.8f, 0),
			DampingMin = 1f,
			DampingMax = 2f,
			ScaleMin = 0.7f,
			ScaleMax = 1.3f,
			ScaleCurve = new CurveTexture { Curve = grow },
			AngleMin = 0f,
			AngleMax = 360f,
			AnimOffsetMin = 0f,
			AnimOffsetMax = 1f
		};
		return Particles(process, _mistMaterial, Vector2.One * 0.5f * size,
			Mathf.Clamp(Mathf.RoundToInt(6 * size), 4, 12), 0.42, explosiveness: 1f, velocityAligned: false);
	}

	private static GpuParticles3D Particles(ParticleProcessMaterial process, Material material, Vector2 quad,
		int amount, double lifetime, float explosiveness, bool velocityAligned) => new()
	{
		Emitting = true,
		OneShot = true,
		Amount = amount,
		Lifetime = lifetime,
		Explosiveness = explosiveness,
		Randomness = 0.45f,
		LocalCoords = false,
		ProcessMaterial = process,
		TransformAlign = velocityAligned
			? GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity
			: GpuParticles3D.TransformAlignEnum.ZBillboard,
		DrawPass1 = new QuadMesh { Size = quad, Material = material },
		CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		VisibilityAabb = new Aabb(new Vector3(-4, -4, -4), new Vector3(8, 8, 8))
	};

	private static void Stain(Node world, Vector3 at, Vector3 direction, float size, CollisionObject3D ignore)
	{
		if (world is not Node3D world3D) return;
		PhysicsDirectSpaceState3D space = world3D.GetWorld3D()?.DirectSpaceState;
		if (space == null) return;

		var exclude = new Godot.Collections.Array<Rid>();
		if (ignore != null) exclude.Add(ignore.GetRid());

		// Primeiro na direção do golpe (a parede atrás); senão, o chão.
		foreach (Vector3 ray in new[] { direction * 2.5f, Vector3.Down * 2.2f })
		{
			var query = PhysicsRayQueryParameters3D.Create(at, at + ray);
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0 || hit["collider"].Obj is CharacterBody3D) continue;

			PlaceStain(world3D, (Vector3)hit["position"], (Vector3)hit["normal"], size);
			return;
		}
	}

	private static void PlaceStain(Node3D world, Vector3 point, Vector3 normal, float size)
	{
		float side = Rng.RandfRange(0.5f, 0.8f) * Mathf.Sqrt(size);
		StandardMaterial3D material = Wet(_stainShape[Rng.RandiRange(0, Variants - 1)]);
		var stain = new MeshInstance3D
		{
			Name = "Mancha",
			TopLevel = true,
			Mesh = new PlaneMesh { Size = Vector2.One * side },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		world.AddChild(stain);

		// O plano olha para cima (+Y): o Y vira a normal da superfície, e um
		// giro sorteado em volta dela para as manchas não serem iguais.
		Vector3 y = normal.Normalized();
		Vector3 x = y.Cross(Mathf.Abs(y.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
		Vector3 z = x.Cross(y);
		var basis = new Basis(x, y, z).Rotated(y, Rng.RandfRange(0, Mathf.Tau));
		// Cada mancha um fio de cabelo acima da outra: duas no mesmo lugar não brigam.
		stain.GlobalTransform = new Transform3D(basis, point + y * Rng.RandfRange(0.010f, 0.016f));

		var materials = new List<StandardMaterial3D> { material };

		// Na parede o sangue escorre: uns fios que descem devagar da mancha.
		if (Mathf.Abs(y.Y) < 0.5f)
		{
			Vector3 down = (Vector3.Down - y * Vector3.Down.Dot(y)).Normalized();
			Vector3 sideways = y.Cross(down).Normalized();
			int drips = Rng.RandiRange(2, 3);
			for (int i = 0; i < drips; i++)
			{
				float length = Rng.RandfRange(0.16f, 0.38f) * Mathf.Sqrt(size);
				float width = Rng.RandfRange(0.035f, 0.06f);
				StandardMaterial3D dripMaterial = Wet(_drip);
				materials.Add(dripMaterial);
				var drip = new MeshInstance3D
				{
					// A origem no topo: crescer na vertical é escorrer.
					Mesh = new PlaneMesh { Size = new Vector2(width, length), CenterOffset = new Vector3(0, 0, length * 0.5f) },
					MaterialOverride = dripMaterial,
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
					TopLevel = true
				};
				stain.AddChild(drip);
				Vector3 from = point + sideways * Rng.RandfRange(-0.3f, 0.3f) * side + down * Rng.RandfRange(-0.05f, 0.12f) * side + y * 0.011f;
				// X de lado, Y a normal, Z para baixo.
				drip.GlobalTransform = new Transform3D(new Basis(sideways, y, down), from);
				drip.Scale = new Vector3(1f, 1f, 0.2f);
				Tween run = drip.CreateTween();
				run.TweenProperty(drip, "scale:z", 1f, Rng.RandfRange(1.4f, 2.6f)).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			}
		}

		Stains.Enqueue(stain);
		while (Stains.Count > MaxStains)
		{
			Node3D old = Stains.Dequeue();
			if (GodotObject.IsInstanceValid(old)) old.QueueFree();
		}

		// Seca: o corte sobe e a mancha encolhe para dentro, até sumir.
		Tween fade = stain.CreateTween();
		fade.TweenInterval(StainLife);
		for (int i = 0; i < materials.Count; i++)
			(i == 0 ? fade : fade.Parallel()).TweenProperty(materials[i], "alpha_scissor_threshold", 0.995f, 2.2).SetEase(Tween.EaseType.In);
		fade.TweenCallback(Callable.From(stain.QueueFree));
	}

	// ------------------------------------------------------------------ materiais

	private const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// O desenho: no alfa, um campo que vale 0,5 na borda da forma e sobe para dentro.
uniform sampler2D shape : filter_linear_mipmap, repeat_disable;
uniform float frames = 1.0;          // formas por lado na folha
uniform vec4 thin : source_color = vec4(0.66, 0.035, 0.04, 1.0);
uniform vec4 thick : source_color = vec4(0.27, 0.010, 0.016, 1.0);
uniform float erode = 0.0;           // 0 inteiro .. 1 desfeito (sem partícula)
uniform float soft = 0.0;            // 1: névoa, sem borda
uniform float opacity = 1.0;
uniform float spin = 0.0;
uniform bool billboard = false;
uniform bool particle = false;

varying vec2 cell;
varying float age;

void vertex() {
	if (particle) {
		float f = floor(INSTANCE_CUSTOM.z * frames * frames * 0.9999);
		cell = vec2(mod(f, frames), floor(f / frames));
		age = INSTANCE_CUSTOM.y;
	} else {
		cell = vec2(0.0);
		age = erode;
	}
	if (billboard) {
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
			vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
			vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
			vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
			vec4(0.0, 0.0, 0.0, 1.0));
		MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	}
}

void fragment() {
	vec2 uv = UV - 0.5;
	float c = cos(spin), s = sin(spin);
	uv = clamp(vec2(uv.x * c - uv.y * s, uv.x * s + uv.y * c) + 0.5, 0.0, 1.0);
	float field = texture(shape, (uv + cell) / frames).a;
	float a;
	if (soft > 0.5) {
		a = field * (1.0 - age) * (1.0 - age);
	} else {
		// Envelhecer sobe o corte: o sangue afina, os fios se partem em gotas, as gotas somem.
		float cut = mix(0.5, 0.985, age * age);
		float w = max(fwidth(field) * 0.9, 0.012);
		a = smoothstep(cut - w, cut + w, field);
	}
	ALBEDO = mix(thin.rgb, thick.rgb, smoothstep(0.56, 0.9, field)) * COLOR.rgb;
	ALPHA = a * opacity * COLOR.a;
}
";

	private static ShaderMaterial Material(Texture2D shape, int frames)
	{
		var material = new ShaderMaterial { Shader = _shader };
		material.SetShaderParameter("shape", shape);
		material.SetShaderParameter("frames", (float)frames);
		material.SetShaderParameter("thin", Thin);
		material.SetShaderParameter("thick", Thick);
		return material;
	}

	/// <summary>A mancha e os fios na superfície: recebem a luz do lugar e
	/// brilham como coisa molhada; a borda é o corte do campo.</summary>
	private static StandardMaterial3D Wet(Texture2D shape) => new()
	{
		AlbedoTexture = shape,
		AlbedoColor = new Color(0.5f, 0.022f, 0.028f),
		Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
		AlphaScissorThreshold = 0.5f,
		AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.AlphaToCoverage,
		AlphaAntialiasingEdge = 0.35f,
		Roughness = 0.18f,
		Metallic = 0f,
		// Um fio de cor própria: na sombra continua vermelho-escuro, não preto.
		EmissionEnabled = true,
		Emission = new Color(0.15f, 0.006f, 0.009f),
		SpecularMode = BaseMaterial3D.SpecularModeEnum.SchlickGgx,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
	};

	private static void EnsureMaterials()
	{
		if (_shader != null) return;

		_shader = new Shader { Code = ShaderCode };
		_blobs = BlobSheet();
		_mists = MistSheet();
		_drip = Drip();
		_splash = new Texture2D[Variants];
		_jet = new Texture2D[Variants];
		_stainShape = new Texture2D[Variants];
		for (int i = 0; i < Variants; i++)
		{
			_splash[i] = Splash(i, tendrils: 11, reach: 0.94f, pool: 0.55f);
			_jet[i] = JetShape(i);
			// A mancha tem mais poça e fios mais curtos que o estouro.
			_stainShape[i] = Splash(40 + i, tendrils: 8, reach: 0.72f, pool: 1f);
		}

		_dropMaterial = Material(_blobs, BlobFrames);
		_dropMaterial.SetShaderParameter("particle", true);
		_mistMaterial = Material(_mists, MistFrames);
		_mistMaterial.SetShaderParameter("particle", true);
		_mistMaterial.SetShaderParameter("soft", 1f);
		_mistMaterial.SetShaderParameter("opacity", 0.55f);
		_mistMaterial.SetShaderParameter("thin", Thin.Lerp(Thick, 0.35f));
	}

	// ------------------------------------------------------------------ formas

	/// <summary>
	/// Um campo de metabolas: cada bola soma r²/d². Onde a soma passa de 1
	/// é "dentro"; bolas vizinhas se fundem com um pescoço macio, e uma
	/// fila de bolas que vai espaçando vira um fio que se parte em gotas.
	/// Vira imagem com o alfa valendo 0,5 na borda e subindo para dentro,
	/// que é o que o material corta.
	/// </summary>
	private sealed class Field
	{
		public readonly int Width, Height;
		private readonly float[] _v;

		public Field(int width, int height)
		{
			Width = width;
			Height = height;
			_v = new float[width * height];
		}

		/// <summary>Uma bola. Os limites prendem a contribuição a um
		/// quadro da folha, para uma forma não vazar na vizinha.</summary>
		public void Ball(float cx, float cy, float r, int x0 = 0, int y0 = 0, int x1 = -1, int y1 = -1)
		{
			if (x1 < 0) x1 = Width - 1;
			if (y1 < 0) y1 = Height - 1;
			float reach = r * 5f + 1f;
			int ax = Mathf.Max((int)(cx - reach), x0), bx = Mathf.Min((int)(cx + reach), x1);
			int ay = Mathf.Max((int)(cy - reach), y0), by = Mathf.Min((int)(cy + reach), y1);
			float r2 = r * r;
			for (int y = ay; y <= by; y++)
			for (int x = ax; x <= bx; x++)
			{
				float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
				_v[y * Width + x] += r2 / (dx * dx + dy * dy + 0.3f);
			}
		}

		/// <summary>Uma fila de bolas que afina e espaça: um fio que termina em gotas.</summary>
		public void Thread(RandomNumberGenerator rng, float x, float y, float angle, float length, float from, float to,
			int x0 = 0, int y0 = 0, int x1 = -1, int y1 = -1, float breakUp = 1.5f)
		{
			float travelled = 0f, bend = rng.RandfRange(-0.25f, 0.25f);
			while (travelled < length)
			{
				float t = travelled / length;
				float r = Mathf.Lerp(from, to, Mathf.Pow(t, 0.8f));
				float a = angle + bend * t;
				Ball(x + Mathf.Cos(a) * travelled + rng.RandfRange(-0.4f, 0.4f), y + Mathf.Sin(a) * travelled + rng.RandfRange(-0.4f, 0.4f), r, x0, y0, x1, y1);
				// Perto da raiz as bolas se fundem; na ponta se soltam.
				travelled += r * (0.75f + breakUp * t * t * rng.RandfRange(0.7f, 1.5f));
			}
		}

		public Texture2D ToTexture()
		{
			Image image = Image.CreateEmpty(Width, Height, true, Image.Format.Rgba8);
			for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
			{
				float f = _v[y * Width + x];
				float a = f / (f + 1f);
				// Na cor, a espessura: escuro onde o sangue é grosso (para a mancha, que é material comum).
				float shade = Mathf.Lerp(1f, 0.5f, Mathf.SmoothStep(0.55f, 0.92f, a));
				image.SetPixel(x, y, new Color(shade, shade, shade, a));
			}
			image.GenerateMipmaps();
			return ImageTexture.CreateFromImage(image);
		}
	}

	/// <summary>A folha de gotas: 16 formas — borrões tortos, lágrimas com cauda, gotas gêmeas.</summary>
	private static Texture2D BlobSheet()
	{
		const int cell = 64;
		var field = new Field(cell * BlobFrames, cell * BlobFrames);
		var rng = new RandomNumberGenerator { Seed = 9001 };
		for (int k = 0; k < BlobFrames * BlobFrames; k++)
		{
			int x0 = k % BlobFrames * cell + 3, y0 = k / BlobFrames * cell + 3, x1 = x0 + cell - 7, y1 = y0 + cell - 7;
			float cx = x0 + cell * 0.5f - 3, cy = y0 + cell * 0.5f - 3;

			// O corpo: bolas juntas, fora do centro, nunca um círculo.
			int body = rng.RandiRange(2, 4);
			for (int i = 0; i < body; i++)
			{
				float a = rng.RandfRange(0, Mathf.Tau), d = rng.RandfRange(0f, 6f);
				field.Ball(cx + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d * 1.4f, rng.RandfRange(5f, 8.5f), x0, y0, x1, y1);
			}
			// A cauda: para cima do quadro, que é para trás de onde a gota voa.
			if (rng.Randf() < 0.75f)
				field.Thread(rng, cx + rng.RandfRange(-2f, 2f), cy, -Mathf.Pi / 2f + rng.RandfRange(-0.3f, 0.3f), rng.RandfRange(12f, 22f), 4.6f, 1.2f, x0, y0, x1, y1);
			// Gotinhas soltas.
			int loose = rng.RandiRange(0, 3);
			for (int i = 0; i < loose; i++)
			{
				float a = rng.RandfRange(0, Mathf.Tau), d = rng.RandfRange(14f, 22f);
				field.Ball(cx + Mathf.Cos(a) * d * 0.7f, cy + Mathf.Sin(a) * d, rng.RandfRange(1.3f, 2.8f), x0, y0, x1, y1);
			}
		}
		return field.ToTexture();
	}

	/// <summary>
	/// Um respingo: a poça torta no meio, fios para todo lado que afinam e
	/// se partem, e gotas soltas em volta. Serve de estouro (fios longos)
	/// e de mancha (mais poça, fios curtos).
	/// </summary>
	private static Texture2D Splash(int seed, int tendrils, float reach, float pool)
	{
		const int side = 160;
		const float mid = side * 0.5f;
		var field = new Field(side, side);
		var rng = new RandomNumberGenerator { Seed = (ulong)(1337 + seed * 97) };

		int blots = 5 + Mathf.RoundToInt(5 * pool);
		for (int i = 0; i < blots; i++)
		{
			float a = rng.RandfRange(0, Mathf.Tau), d = rng.RandfRange(0f, 15f * pool + 4f);
			field.Ball(mid + Mathf.Cos(a) * d, mid + Mathf.Sin(a) * d, rng.RandfRange(6f, 9f + 5f * pool));
		}

		// Os fios não se espalham por igual: sorteados, uns colados, uns sozinhos.
		for (int i = 0; i < tendrils; i++)
		{
			float a = rng.RandfRange(0, Mathf.Tau);
			float length = rng.RandfRange(0.35f, 1f) * (mid - 8f) * reach;
			field.Thread(rng, mid + Mathf.Cos(a) * 6f, mid + Mathf.Sin(a) * 6f, a, length, rng.RandfRange(3.6f, 5.6f), rng.RandfRange(0.9f, 1.5f));
		}

		int loose = 14 + rng.RandiRange(0, 10);
		for (int i = 0; i < loose; i++)
		{
			float a = rng.RandfRange(0, Mathf.Tau), d = rng.RandfRange(0.4f, 0.95f) * (mid - 6f) * reach;
			float r = rng.RandfRange(0.9f, 2.9f) * (1.15f - d / mid * 0.6f);
			field.Ball(mid + Mathf.Cos(a) * d, mid + Mathf.Sin(a) * d, r);
			// Algumas com rastro apontando para o centro: caíram de lado.
			if (rng.Randf() < 0.3f) field.Thread(rng, mid + Mathf.Cos(a) * d, mid + Mathf.Sin(a) * d, a + Mathf.Pi, r * 4f, r * 0.8f, 0.6f, breakUp: 0.6f);
		}
		return field.ToTexture();
	}

	/// <summary>O jato: sai do meio da borda esquerda e abre para a direita em fios.</summary>
	private static Texture2D JetShape(int seed)
	{
		const int width = 224, height = 112;
		var field = new Field(width, height);
		var rng = new RandomNumberGenerator { Seed = (ulong)(4242 + seed * 131) };
		float ox = 16f, oy = height * 0.5f;

		for (int i = 0; i < 5; i++)
			field.Ball(ox + rng.RandfRange(-4f, 12f), oy + rng.RandfRange(-8f, 8f), rng.RandfRange(7f, 11f));

		int threads = 8 + rng.RandiRange(0, 3);
		for (int i = 0; i < threads; i++)
		{
			// Mais fios perto do eixo; os de fora, mais curtos.
			float a = rng.Randfn(0f, 0.2f);
			a = Mathf.Clamp(a, -0.42f, 0.42f);
			float length = rng.RandfRange(0.4f, 1f) * (width - 34f) * (1f - Mathf.Abs(a) * 0.9f);
			field.Thread(rng, ox + 6f, oy + rng.RandfRange(-5f, 5f), a, length, rng.RandfRange(4.2f, 6.6f), rng.RandfRange(0.9f, 1.6f), breakUp: 1.9f);
		}

		for (int i = 0; i < 16; i++)
		{
			float a = rng.RandfRange(-0.44f, 0.44f), d = rng.RandfRange(40f, width - 26f);
			float y = oy + Mathf.Sin(a) * d;
			if (y < 5f || y > height - 5f) continue;
			field.Ball(ox + Mathf.Cos(a) * d, y, rng.RandfRange(0.9f, 2.6f));
		}
		return field.ToTexture();
	}

	/// <summary>Um fio que escorre: fino em cima, a gota pesada embaixo.</summary>
	private static Texture2D Drip()
	{
		const int width = 32, height = 128;
		var field = new Field(width, height);
		var rng = new RandomNumberGenerator { Seed = 77 };
		float x = width * 0.5f;
		for (float y = 6f; y < height - 16f; y += 2.2f)
		{
			float t = y / height;
			x += rng.RandfRange(-0.35f, 0.35f);
			field.Ball(x, y, Mathf.Lerp(2.6f, 3.6f, t) + rng.RandfRange(-0.4f, 0.4f));
		}
		field.Ball(x, height - 15f, 6.2f);
		field.Ball(x + 0.6f, height - 11f, 5.2f);
		return field.ToTexture();
	}

	/// <summary>A folha de névoa: quatro nuvens de ruído, sem borda e sem círculo.</summary>
	private static Texture2D MistSheet()
	{
		const int cell = 64, side = cell * MistFrames;
		Image image = Image.CreateEmpty(side, side, true, Image.Format.Rgba8);
		for (int k = 0; k < MistFrames * MistFrames; k++)
		{
			var noise = new FastNoiseLite
			{
				Seed = 300 + k * 17,
				NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
				FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
				FractalOctaves = 4,
				Frequency = 0.035f
			};
			int x0 = k % MistFrames * cell, y0 = k / MistFrames * cell;
			for (int y = 0; y < cell; y++)
			for (int x = 0; x < cell; x++)
			{
				float dx = (x + 0.5f) / cell * 2f - 1f, dy = (y + 0.5f) / cell * 2f - 1f;
				// A borda da nuvem também é torta: o ruído empurra o raio.
				float wobble = noise.GetNoise2D(x * 0.5f + 90f, y * 0.5f - 40f) * 0.3f;
				float falloff = Mathf.Clamp(1f - (dx * dx + dy * dy) / (0.62f + wobble), 0f, 1f);
				float cloud = Mathf.Clamp(noise.GetNoise2D(x, y) * 0.9f + 0.55f, 0f, 1f);
				float a = falloff * falloff * cloud;
				image.SetPixel(x0 + x, y0 + y, new Color(1, 1, 1, a));
			}
		}
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}
}
