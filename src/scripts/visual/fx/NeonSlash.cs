using Godot;

/// <summary>
/// O corte das garras: três riscos de neon em arco na frente do lobo, no
/// instante em que a garra chega (pedido do Jhon, 18/11: "esse neon bem
/// polido pode ser usado nas garras quando eu aperto o ataque"). Varre de
/// um lado ao outro em poucos centésimos e apaga em dois décimos.
///
/// Fica no mundo, onde o golpe foi dado: o lobo segue andando e o corte
/// fica. Usa o material do rastro das garras (assets/efeitos/rastro.tres),
/// que desenha os três fios.
/// </summary>
public partial class NeonSlash : Node3D
{
	/// <summary>Do centro do corpo ao meio do corte, em metros.</summary>
	public float Reach { get; set; } = 1.25f;

	/// <summary>Largura da faixa dos três riscos, em metros.</summary>
	public float Band { get; set; } = 0.62f;

	/// <summary>Abertura do arco, em graus.</summary>
	public float Sweep { get; set; } = 115f;

	/// <summary>Quanto tempo leva para varrer, e para apagar depois.</summary>
	public float SweepTime { get; set; } = 0.07f;
	public float FadeTime { get; set; } = 0.2f;

	/// <summary>+1: da direita para a esquerda (a garra direita); −1: o contrário.</summary>
	public float Side { get; set; } = 1f;

	private const int Steps = 18;

	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;
	private double _age;

	public static NeonSlash Spawn(Node3D world, Transform3D body, Color tint, bool rightHand, float size = 1f)
	{
		Material material = ResourceLoader.Exists(WolfFx.TrailPath) ? GD.Load<Material>(WolfFx.TrailPath) : null;
		if (material == null) return null;

		var slash = new NeonSlash { Side = rightHand ? 1f : -1f, Reach = 1.3f * size, Band = 0.62f * size };
		slash._mesh = new ImmediateMesh();
		slash._canvas = new MeshInstance3D
		{
			Mesh = slash._mesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			ExtraCullMargin = 3f
		};
		slash.AddChild(slash._canvas);
		world.AddChild(slash);

		// O plano do corte: na frente do corpo, inclinado para o lado da
		// mão que bate (de cima de um lado para baixo do outro).
		Basis basis = body.Basis.Orthonormalized();
		basis = basis * new Basis(Vector3.Forward, Mathf.DegToRad(32f) * slash.Side);
		slash.GlobalTransform = new Transform3D(basis, body.Origin + Vector3.Up * 0.3f);
		slash._canvas.SetInstanceShaderParameter("trail_color", tint);
		slash.Draw();
		return slash;
	}

	public override void _Process(double delta)
	{
		_age += delta;
		if (_age >= SweepTime + FadeTime)
		{
			QueueFree();
			return;
		}
		Draw();
	}

	private void Draw()
	{
		float reveal = Mathf.Clamp((float)_age / SweepTime, 0.08f, 1f);
		float fade = 1f - Mathf.Clamp(((float)_age - SweepTime) / FadeTime, 0f, 1f);
		float half = Mathf.DegToRad(Sweep) * 0.5f;

		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
		int steps = Mathf.Max(2, Mathf.CeilToInt(Steps * reveal));
		for (int i = 0; i <= steps; i++)
		{
			float u = i / (float)steps;
			// O arco vai de um lado ao outro pela frente (−Z), no plano local XZ.
			float angle = Side * Mathf.Lerp(half, -half, u * reveal);
			Vector3 dir = new(Mathf.Sin(angle), 0, -Mathf.Cos(angle));
			// Afina nas duas pontas do arco.
			float taper = Mathf.Sin(Mathf.Clamp(u * reveal, 0f, 1f) * Mathf.Pi);
			var color = new Color(1, 1, 1, fade * Mathf.Pow(taper, 0.6f));

			// A faixa fica em pé (os três riscos um acima do outro): de trás,
			// de frente ou de lado, o corte aparece de cara. Deitada, quem
			// olhava da altura do lobo via só um fio.
			float bulge = Mathf.Sin(u * reveal * Mathf.Pi);
			Vector3 at = dir * (Reach * (0.86f + 0.14f * bulge));
			_mesh.SurfaceSetColor(color);
			_mesh.SurfaceSetUV(new Vector2(u, 0f));
			_mesh.SurfaceAddVertex(at + Vector3.Down * Band * 0.72f);
			_mesh.SurfaceSetColor(color);
			_mesh.SurfaceSetUV(new Vector2(u, 1f));
			_mesh.SurfaceAddVertex(at + Vector3.Up * Band * 0.28f);
		}
		_mesh.SurfaceEnd();
	}
}
