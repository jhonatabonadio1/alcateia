using Godot;
using System.Collections.Generic;

/// <summary>
/// Sangue ao acertar (pedido do Jhon, 18/11): tiro em gente e garra de
/// lobo.
///
/// Três camadas, todas curtas, para dizer "acertou" sem sujar a tela:
///
/// - **esguicho**: gotas que saem do ponto do acerto na direção do golpe
///   (o tiro empurra para trás de quem levou; a garra, para o lado do
///   corte) e caem com a gravidade;
/// - **névoa**: um sopro vermelho rápido no ponto, que é o que se lê de
///   longe;
/// - **mancha**: se há parede ou chão logo atrás, uma mancha que fica uns
///   segundos e some. No máximo <see cref="MaxStains"/> no mapa; a mais
///   velha sai primeiro.
///
/// Tudo é desenhado sem textura de arquivo (as gotas e as manchas são
/// geradas aqui), e sem nada que só o Forward+ tenha — vale para o
/// renderizador de compatibilidade também.
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

	/// <summary>Quanto tempo a mancha fica, em segundos, antes de sumir.</summary>
	public const float StainLife = 12f;

	/// <summary>Esguichos ao mesmo tempo. Uma rajada de escopeta em quatro
	/// pessoas não precisa de quarenta.</summary>
	public const int MaxBursts = 24;

	private static readonly Color Dark = new(0.32f, 0.015f, 0.02f);
	private static readonly Color Bright = new(0.55f, 0.03f, 0.03f);

	private static readonly Queue<Node3D> Stains = new();
	private static int _bursts;
	private static StandardMaterial3D _drop;
	private static StandardMaterial3D _mist;
	private static StandardMaterial3D _streak;
	private static StandardMaterial3D[] _stain;
	private static readonly RandomNumberGenerator Rng = new();

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

		var root = new Node3D { Name = "Sangue", TopLevel = true };
		world.AddChild(root);
		root.GlobalPosition = at;
		_bursts++;
		root.TreeExiting += () => _bursts--;

		root.AddChild(Drops(direction, size));
		root.AddChild(Mist(direction, size));
		// Os esguichos e o estouro no ponto (18/11, "FX melhores"): é o que
		// dá o sentido do golpe e marca o instante do acerto.
		root.AddChild(Streaks(direction, size));
		root.AddChild(Pop(size));

		// Mancha atrás de quem levou, ou no chão logo abaixo.
		Stain(world, at, direction, size, ignore);

		root.GetTree().CreateTimer(1.2).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(root)) root.QueueFree();
		};
	}

	// ------------------------------------------------------------------ camadas

	private static CpuParticles3D Drops(Vector3 direction, float size)
	{
		var drops = new CpuParticles3D
		{
			Emitting = true,
			OneShot = true,
			Amount = Mathf.Clamp(Mathf.RoundToInt(34 * size), 12, 80),
			Lifetime = 0.6,
			Explosiveness = 0.92f,
			Randomness = 0.4f,
			LocalCoords = false,
			Mesh = new QuadMesh { Size = Vector2.One, Material = _drop },
			Direction = direction,
			Spread = 32f,
			InitialVelocityMin = 1.8f * Mathf.Sqrt(size),
			InitialVelocityMax = 5.2f * Mathf.Sqrt(size),
			Gravity = new Vector3(0, -11f, 0),
			DampingMin = 1f,
			DampingMax = 3f,
			ScaleAmountMin = 0.05f,
			ScaleAmountMax = 0.13f * Mathf.Sqrt(size),
			ScaleAmountCurve = Fade(1f, 0.35f),
			Color = Bright,
			ColorRamp = Ramp(Bright, Dark, 1f, 0.85f)
		};
		return drops;
	}

	/// <summary>Esguichos: gotas esticadas no sentido em que voam, rápidas,
	/// num leque estreito para onde o golpe empurra.</summary>
	private static GpuParticles3D Streaks(Vector3 direction, float size)
	{
		var process = new ParticleProcessMaterial
		{
			Direction = direction,
			Spread = 20f,
			InitialVelocityMin = 3.5f * Mathf.Sqrt(size),
			InitialVelocityMax = 8.5f * Mathf.Sqrt(size),
			Gravity = new Vector3(0, -12f, 0),
			DampingMin = 2f,
			DampingMax = 5f,
			ScaleMin = 0.6f,
			ScaleMax = 1.35f,
			Color = Bright,
			ColorRamp = new GradientTexture1D { Gradient = Ramp(Bright, Dark, 1f, 0f) }
		};
		return new GpuParticles3D
		{
			Emitting = true,
			OneShot = true,
			Amount = Mathf.Clamp(Mathf.RoundToInt(12 * size), 6, 28),
			Lifetime = 0.42,
			Explosiveness = 0.96f,
			Randomness = 0.5f,
			LocalCoords = false,
			ProcessMaterial = process,
			TransformAlign = GpuParticles3D.TransformAlignEnum.ZBillboardYToVelocity,
			DrawPass1 = new QuadMesh { Size = new Vector2(0.035f, 0.26f) * Mathf.Sqrt(size), Material = _streak },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			VisibilityAabb = new Aabb(new Vector3(-4, -4, -4), new Vector3(8, 8, 8))
		};
	}

	/// <summary>O estouro no ponto do acerto: um borrão que abre e some em
	/// um décimo de segundo.</summary>
	private static MeshInstance3D Pop(float size)
	{
		var material = (StandardMaterial3D)_mist.Duplicate();
		material.AlbedoColor = new Color(Bright.R * 1.25f, Bright.G, Bright.B, 0.85f);
		material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
		material.VertexColorUseAsAlbedo = false;
		var pop = new MeshInstance3D
		{
			Mesh = new QuadMesh { Size = Vector2.One * 0.34f * Mathf.Sqrt(size) },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Scale = Vector3.One * 0.35f
		};
		pop.Ready += () =>
		{
			Tween tween = pop.CreateTween().SetParallel();
			tween.TweenProperty(pop, "scale", Vector3.One * 1.25f, 0.11).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			tween.TweenProperty(material, "albedo_color:a", 0f, 0.13);
		};
		return pop;
	}

	private static CpuParticles3D Mist(Vector3 direction, float size)
	{
		return new CpuParticles3D
		{
			Emitting = true,
			OneShot = true,
			Amount = Mathf.Clamp(Mathf.RoundToInt(7 * size), 4, 16),
			Lifetime = 0.4,
			Explosiveness = 1f,
			LocalCoords = false,
			Mesh = new QuadMesh { Size = Vector2.One, Material = _mist },
			Direction = direction,
			Spread = 50f,
			InitialVelocityMin = 0.4f,
			InitialVelocityMax = 1.4f,
			Gravity = new Vector3(0, -1f, 0),
			ScaleAmountMin = 0.3f * size,
			ScaleAmountMax = 0.6f * size,
			ScaleAmountCurve = Fade(0.6f, 1f),
			Color = Bright,
			ColorRamp = Ramp(Bright, Dark, 0.8f, 0f)
		};
	}

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
		var stain = new MeshInstance3D
		{
			Name = "Mancha",
			TopLevel = true,
			Mesh = new PlaneMesh { Size = Vector2.One * Rng.RandfRange(0.35f, 0.6f) * Mathf.Sqrt(size) },
			MaterialOverride = (StandardMaterial3D)_stain[Rng.RandiRange(0, _stain.Length - 1)].Duplicate(),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		world.AddChild(stain);

		// O plano olha para cima (+Y): o Y vira a normal da superfície, e um
		// giro sorteado em volta dela para as manchas não serem iguais.
		Vector3 y = normal.Normalized();
		Vector3 x = y.Cross(Mathf.Abs(y.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
		Vector3 z = x.Cross(y);
		var basis = new Basis(x, y, z).Rotated(y, Rng.RandfRange(0, Mathf.Tau));
		stain.GlobalTransform = new Transform3D(basis, point + y * 0.012f);

		Stains.Enqueue(stain);
		while (Stains.Count > MaxStains)
		{
			Node3D old = Stains.Dequeue();
			if (GodotObject.IsInstanceValid(old)) old.QueueFree();
		}

		var material = (StandardMaterial3D)stain.MaterialOverride;
		Tween fade = stain.CreateTween();
		fade.TweenInterval(StainLife);
		fade.TweenProperty(material, "albedo_color:a", 0f, 2.0);
		fade.TweenCallback(Callable.From(stain.QueueFree));
	}

	// ------------------------------------------------------------------ materiais

	private static void EnsureMaterials()
	{
		if (_drop != null) return;

		Texture2D dot = Dot();
		_drop = Particle(dot, 1f);
		_mist = Particle(dot, 1f);
		// O esguicho: o alinhamento é das partículas (Y na velocidade), não do material.
		_streak = Particle(dot, 1f);
		_streak.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;

		_stain = new StandardMaterial3D[3];
		for (int i = 0; i < _stain.Length; i++)
		{
			_stain[i] = new StandardMaterial3D
			{
				AlbedoTexture = Splat(i),
				AlbedoColor = new Color(0.42f, 0.02f, 0.025f, 0.92f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				Roughness = 0.25f,
				Metallic = 0f,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
			};
		}
	}

	private static StandardMaterial3D Particle(Texture2D texture, float alpha) => new()
	{
		AlbedoTexture = texture,
		AlbedoColor = new Color(1, 1, 1, alpha),
		VertexColorUseAsAlbedo = true,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
		BillboardKeepScale = true,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled
	};

	/// <summary>Uma gota: disco com a borda macia.</summary>
	private static Texture2D Dot()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.AddPoint(0.55f, new Color(1, 1, 1, 0.9f));
		return new GradientTexture2D
		{
			Gradient = gradient,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(0.5f, 0f),
			Width = 32,
			Height = 32
		};
	}

	/// <summary>
	/// Uma mancha: um borrão central irregular e respingos em volta,
	/// desenhados uma vez numa imagem. Três sementes, três formas.
	/// </summary>
	private static Texture2D Splat(int seed)
	{
		const int side = 128;
		var rng = new RandomNumberGenerator { Seed = (ulong)(1337 + seed * 97) };
		var alpha = new float[side * side];

		void Disc(float cx, float cy, float r)
		{
			int x0 = Mathf.Max((int)(cx - r - 2), 0), x1 = Mathf.Min((int)(cx + r + 2), side - 1);
			int y0 = Mathf.Max((int)(cy - r - 2), 0), y1 = Mathf.Min((int)(cy + r + 2), side - 1);
			for (int y = y0; y <= y1; y++)
			for (int x = x0; x <= x1; x++)
			{
				float d = new Vector2(x - cx, y - cy).Length();
				float a = Mathf.Clamp(r - d + 1f, 0f, 1f);
				int i = y * side + x;
				alpha[i] = Mathf.Max(alpha[i], a);
			}
		}

		// O borrão: vários círculos sobrepostos perto do centro.
		for (int i = 0; i < 9; i++)
		{
			float angle = rng.RandfRange(0, Mathf.Tau), dist = rng.RandfRange(0, 16);
			Disc(64 + Mathf.Cos(angle) * dist, 64 + Mathf.Sin(angle) * dist, rng.RandfRange(10, 22));
		}

		// Respingos: gotas menores em volta, umas com rastro.
		for (int i = 0; i < 26; i++)
		{
			float angle = rng.RandfRange(0, Mathf.Tau), dist = rng.RandfRange(26, 58);
			float r = rng.RandfRange(1.2f, 4.5f) * (1f - dist / 80f);
			float cx = 64 + Mathf.Cos(angle) * dist, cy = 64 + Mathf.Sin(angle) * dist;
			Disc(cx, cy, r);
			if (rng.Randf() < 0.35f)
				for (int s = 1; s < 5; s++)
					Disc(cx - Mathf.Cos(angle) * s * 2.5f, cy - Mathf.Sin(angle) * s * 2.5f, r * (1f - s * 0.18f));
		}

		var image = Image.CreateEmpty(side, side, true, Image.Format.Rgba8);
		for (int y = 0; y < side; y++)
		for (int x = 0; x < side; x++)
		{
			float a = alpha[y * side + x];
			// Mais escuro no meio do borrão, como sangue que empoça.
			float shade = 0.75f + 0.25f * Mathf.Clamp(new Vector2(x - 64, y - 64).Length() / 40f, 0f, 1f);
			image.SetPixel(x, y, new Color(shade, shade, shade, a));
		}
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}

	private static Curve Fade(float start, float end)
	{
		var curve = new Curve();
		curve.AddPoint(new Vector2(0, start));
		curve.AddPoint(new Vector2(1, end));
		return curve;
	}

	private static Gradient Ramp(Color from, Color to, float fromAlpha, float toAlpha)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(from, fromAlpha));
		gradient.SetColor(1, new Color(to, toAlpha));
		return gradient;
	}
}
