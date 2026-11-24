using Godot;

/// <summary>
/// O traçante de um disparo (Jhon, 23/11: "deixar o tiro que sai do cano mais elaborado;
/// atualmente é só uma linha branca"). Três peças, todas fitas de luz de frente para a câmera
/// (<see cref="FxRibbon"/>, o mesmo material dos outros efeitos — um material novo por disparo
/// travava o quadro montando o pipeline de render):
///
/// - a bala: um risco curto e quente que viaja do cano até onde o tiro parou, mais grosso e
///   branco na frente e apagando para trás (o clarão na cor do traçante, o miolo quase branco);
/// - o rastro: o ar quente por onde ela passou, fino e fraco, que alarga, se desfaz e ondula um
///   pouco enquanto some — cada trecho envelhece desde o instante em que a bala passou por ele;
/// - o clarão do cano: uma estrela de luz no primeiro instante, na boca da arma.
///
/// Só visual: quem acerta é o raio do <see cref="Weapon"/>, no instante do disparo. O risco anda
/// mais devagar que uma bala de verdade para dar para ver (a 190 m/s cruza 30 m em 9 quadros).
/// </summary>
public partial class BulletTracer : Node3D
{
	/// <summary>A velocidade do risco (m/s) e o comprimento dele (m).</summary>
	[Export] public float Speed = 190f;
	[Export] public float StreakLength = 3.2f;

	/// <summary>A largura do miolo e do clarão da bala (m).</summary>
	[Export] public float CoreWidth = 0.022f;
	[Export] public float GlowWidth = 0.12f;

	/// <summary>Quanto o rastro dura (s), a largura com que nasce (m, e alarga até 3×), quão
	/// forte ele é (0 a 1) e quanto ondula ao sumir (m).</summary>
	[Export] public float TrailLife = 0.45f;
	[Export] public float TrailWidth = 0.03f;
	[Export] public float TrailStrength = 0.38f;
	[Export] public float TrailWisp = 0.03f;

	/// <summary>O clarão do cano: tamanho (m) e duração (s).</summary>
	[Export] public float FlashSize = 0.22f;
	[Export] public float FlashLife = 0.05f;

	/// <summary>Perto do olho a fita fica larga na tela (o tiro sai a meio metro da câmera em
	/// primeira pessoa): a largura não passa desta fração da distância até o olho.</summary>
	[Export] public float MaxWidthPerMeter = 0.012f;

	private const int TrailPoints = 14;

	private Vector3 _from, _to, _dir, _side, _lift;
	private float _length, _age, _phase;
	private Color _tint;
	private ImmediateMesh _mesh;
	private readonly Vector3[] _trail = new Vector3[TrailPoints];
	private readonly float[] _trailAge = new float[TrailPoints];

	/// <summary>Dispara um traçante de <paramref name="from"/> (a boca do cano) até
	/// <paramref name="to"/> (onde o tiro parou), na cor dada. Sem <paramref name="flash"/>, sem
	/// o clarão do cano (a arma da primeira pessoa tem o dela).</summary>
	public static BulletTracer Spawn(Node parent, Vector3 from, Vector3 to, Color tint, bool flash = true)
	{
		var tracer = new BulletTracer();
		if (!flash) tracer.FlashSize = 0f;
		tracer.Setup(from, to, tint);
		parent.AddChild(tracer);
		return tracer;
	}

	private void Setup(Vector3 from, Vector3 to, Color tint)
	{
		_from = from;
		_to = to;
		_tint = tint;
		Vector3 path = to - from;
		_length = path.Length();
		_dir = _length > 1e-4f ? path / _length : Vector3.Forward;
		_side = _dir.Cross(Mathf.Abs(_dir.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
		_lift = _dir.Cross(_side);
		// A ondulação de cada traçante começa num ponto diferente (da posição, sem sorteio).
		_phase = Mathf.PosMod(from.X * 7.31f + from.Z * 3.17f + to.Y * 5.03f, Mathf.Tau);
	}

	public override void _Ready()
	{
		TopLevel = true;
		_mesh = new ImmediateMesh();
		AddChild(FxRibbon.Canvas(_mesh, true));
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		float travel = _length / Mathf.Max(Speed, 1f);
		if (_age > travel + TrailLife)
		{
			QueueFree();
			return;
		}
		Vector3 eye = GetViewport()?.GetCamera3D()?.GlobalPosition ?? _from + _lift;
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		DrawTrail(eye);
		DrawBullet(eye);
		DrawFlash(eye);
		_mesh.SurfaceEnd();
	}

	/// <summary>A largura de uma fita num ponto, sem passar do teto perto do olho.</summary>
	private float Width(float width, Vector3 at, Vector3 eye) =>
		Mathf.Min(width, Mathf.Max(at.DistanceTo(eye), 0.05f) * MaxWidthPerMeter * (width / Mathf.Max(CoreWidth, 1e-4f)));

	/// <summary>O ar quente por onde a bala passou: do cano até o rabo do risco.</summary>
	private void DrawTrail(Vector3 eye)
	{
		float head = Mathf.Min(_age * Speed, _length);
		if (head < 0.05f || TrailStrength <= 0f) return;
		for (int i = 0; i < TrailPoints; i++)
		{
			float d = head * i / (TrailPoints - 1f);
			float age = Mathf.Clamp((_age - d / Speed) / TrailLife, 0f, 1f);
			float wisp = TrailWisp * age * age;
			_trailAge[i] = age;
			_trail[i] = _from + _dir * d
				+ _side * (Mathf.Sin(d * 2.3f + _phase) * wisp)
				+ _lift * (Mathf.Sin(d * 1.7f + _phase * 1.3f) * wisp + 0.04f * age * age);
		}
		Color warm = _tint.Lerp(Colors.White, 0.5f);
		FxRibbon.Strip(_mesh, _trail, eye,
			t => Width(TrailWidth * (1f + 2f * At(t)), At(t, _trail), eye),
			t =>
			{
				float a = At(t);
				// nasce forte, perto do cano mais fraco (o clarão já está lá), e some
				float fade = (1f - a) * (1f - a) * Mathf.Clamp(t * 6f, 0f, 1f);
				return new Color(warm.R, warm.G, warm.B, TrailStrength * fade);
			});
	}

	private float At(float t) => _trailAge[Mathf.Clamp(Mathf.RoundToInt(t * (TrailPoints - 1)), 0, TrailPoints - 1)];
	private static Vector3 At(float t, Vector3[] points) => points[Mathf.Clamp(Mathf.RoundToInt(t * (points.Length - 1)), 0, points.Length - 1)];

	/// <summary>A bala: o risco que viaja, enquanto não chegou.</summary>
	private void DrawBullet(Vector3 eye)
	{
		float headD = _age * Speed;
		if (headD > _length + StreakLength * 0.25f) return;
		float tailD = Mathf.Max(0f, headD - StreakLength);
		headD = Mathf.Min(headD, _length);
		if (headD - tailD < 0.02f) return;
		var points = new[] { _from + _dir * tailD, _from + _dir * Mathf.Lerp(tailD, headD, 0.7f), _from + _dir * headD };
		var glow = new Color(_tint.R, _tint.G, _tint.B, 0.55f * _tint.A);
		FxRibbon.Strip(_mesh, points, eye,
			t => Width(GlowWidth * (0.25f + 0.75f * t), At(t, points), eye),
			t => new Color(glow.R, glow.G, glow.B, glow.A * t * t));
		FxRibbon.Strip(_mesh, points, eye,
			t => Width(CoreWidth * (0.3f + 0.7f * t), At(t, points), eye),
			t => new Color(1f, 0.97f, 0.9f, t * t));
	}

	/// <summary>O clarão do cano: uma estrela de três fitas que gira e some.</summary>
	private void DrawFlash(Vector3 eye)
	{
		if (_age > FlashLife || FlashSize <= 0f) return;
		float k = 1f - _age / FlashLife;
		float size = FlashSize * (0.6f + 0.4f * k);
		Vector3 toEye = (eye - _from).Normalized();
		Vector3 u = toEye.Cross(_dir);
		u = u.LengthSquared() > 1e-6f ? u.Normalized() : _side;
		Vector3 v = toEye.Cross(u).Normalized();
		var glow = new Color(_tint.R, _tint.G, _tint.B, 0.8f * k);
		// para a frente, ao longo do cano, mais comprido
		Vector3 tip = _from + _dir * size;
		FxRibbon.Strip(_mesh, new[] { _from - _dir * size * 0.15f, _from + _dir * size * 0.4f, tip }, eye,
			t => Width(size * 0.45f * (1f - t * 0.8f), _from, eye),
			t => new Color(glow.R, glow.G, glow.B, glow.A * (1f - t)));
		// e a cruz de lado
		for (int i = 0; i < 2; i++)
		{
			float a = _phase + i * Mathf.Pi * 0.5f;
			Vector3 arm = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * size * 0.45f;
			FxRibbon.Strip(_mesh, new[] { _from - arm, _from, _from + arm }, eye,
				t => Width(size * 0.12f, _from, eye),
				t => new Color(1f, 0.95f, 0.85f, k * (1f - Mathf.Abs(t * 2f - 1f))));
		}
	}
}
