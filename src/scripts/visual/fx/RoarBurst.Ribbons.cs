using Godot;

/// <summary>
/// As fitas de luz do rugido (<see cref="RoarBurst"/>), presas ao corpo: o que
/// é linha nítida e muda de forma a cada instante.
///
/// - os **arcos elétricos**: saltam do chão para o corpo cada vez mais juntos
///   ("o core do lobo é eletricidade"); no escudo cheio, a descarga sai do
///   corpo para todos os lados e corre rente ao chão;
/// - os **raios de luz** baixos em volta dele (a ideia do Jhon, 18/11: "uma
///   luz do céu dando um efeito de regeneração, mas não tão alto"): nascem a
///   <see cref="ShaftTop"/> dos pés já transparentes e só ganham força na
///   altura do peito;
/// - os **riscos de ar** saindo da boca em cone: o traço nítido por cima do
///   sopro (<c>rugido_sopro</c>). Só riscos, nada de anel na frente da boca.
/// </summary>
public partial class RoarBurst
{
	private const int RayCount = 6;

	/// <summary>Riscos de ar da boca por quadro; no estouro do começo saem mais.</summary>
	private const int StreakCount = 12, StreakBlast = 8;

	private enum ArcKind { Charge, Burst, Crawl }

	private struct Ray { public float Angle, Radius, Width, Turn, Beat, Phase; }

	private Ray[] _rays;
	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;
	private readonly Vector3[] _shaft = new Vector3[7];
	private readonly Vector3[] _line = new Vector3[2];
	private readonly RandomNumberGenerator _bolt = new(), _streak = new();

	private void BuildRibbons()
	{
		_mesh = new ImmediateMesh();
		_canvas = FxRibbon.Canvas(_mesh, world: false);
		_canvas.Visible = false;
		AddChild(_canvas);

		_rays = new Ray[RayCount];
		for (int i = 0; i < RayCount; i++)
			_rays[i] = new Ray
			{
				Angle = Mathf.Tau * i / RayCount + _rng.RandfRange(-0.3f, 0.3f),
				// Em volta do corpo, não em cima dele: a luz emoldura o lobo sem lavar a cor.
				Radius = _rng.RandfRange(0.55f, 0.9f),
				Width = _rng.RandfRange(0.12f, 0.28f),
				Turn = _rng.RandfRange(0.12f, 0.4f) * (_rng.Randf() < 0.5f ? -1f : 1f),
				Beat = _rng.RandfRange(1.6f, 3.4f),
				Phase = _rng.Randf() * Mathf.Tau
			};
	}

	// ------------------------------------------------------------------ arcos

	private void TickArcs(float dt)
	{
		// Saltam do chão para o corpo, mais juntos quanto mais carregado — e
		// no passo do tremor do som, para a luz e o estalo baterem.
		_arcClock -= dt;
		if (_glow > 0.12f && _arcClock <= 0)
		{
			_arcClock = Mathf.Lerp(0.16f, 0.04f, _level) * _rng.RandfRange(0.6f, 1.4f);
			if (_arcs.Count < ArcMax) AddArc(ArcKind.Charge);
		}
		for (int i = _arcs.Count - 1; i >= 0; i--)
		{
			Arc arc = _arcs[i];
			arc.Age += dt;
			if (arc.Age >= arc.Life) _arcs.RemoveAt(i);
			else _arcs[i] = arc;
		}
	}

	private void AddArc(ArcKind kind)
	{
		float angle = _rng.Randf() * Mathf.Tau;
		Vector3 around = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
		Vector3 from, to;
		switch (kind)
		{
			case ArcKind.Burst:
				// Escudo cheio: a descarga sai do corpo para todos os lados.
				from = new Vector3(0, Feet + _rng.RandfRange(0.7f, 1.5f), 0) + around * 0.2f;
				to = around * _rng.RandfRange(1.1f, 1.8f) + Vector3.Up * (Feet + _rng.RandfRange(0.05f, 2.0f));
				break;
			case ArcKind.Crawl:
				// E corre rente ao chão, para longe dele.
				from = around * _rng.RandfRange(0.3f, 0.6f) + Vector3.Up * (Feet + 0.06f);
				to = around.Rotated(Vector3.Up, _rng.RandfRange(-0.35f, 0.35f)) * _rng.RandfRange(1.8f, 3.0f) + Vector3.Up * (Feet + 0.06f);
				break;
			default:
				// Carregando: do chão em volta dele para o corpo.
				float turn = angle + _rng.RandfRange(-0.5f, 0.5f);
				from = around * _rng.RandfRange(0.55f, 1.15f) + Vector3.Up * (Feet + 0.03f);
				to = new Vector3(Mathf.Cos(turn), 0, Mathf.Sin(turn)) * _rng.RandfRange(0.12f, 0.32f) + Vector3.Up * (Feet + _rng.RandfRange(0.45f, 1.65f));
				break;
		}
		bool loud = kind != ArcKind.Charge;
		_arcs.Add(new Arc
		{
			From = from,
			To = to,
			Life = loud ? _rng.RandfRange(0.2f, 0.36f) : _rng.RandfRange(0.09f, 0.2f),
			Width = loud ? _rng.RandfRange(0.024f, 0.038f) : _rng.RandfRange(0.012f, 0.024f),
			Seed = _rng.Randi()
		});
	}

	// ------------------------------------------------------------------ desenho

	private void DrawRibbons()
	{
		Vector3 eye = GetViewport().GetCamera3D() is Camera3D camera ? ToLocal(camera.GlobalPosition) : new Vector3(0, 2, 6);
		float now = (float)_time;
		_canvas.Visible = true;
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		// Uma superfície sem vértice nenhum é erro no ImmediateMesh: o primeiro
		// triângulo, de área zero, garante que há o que fechar.
		for (int i = 0; i < 3; i++) { _mesh.SurfaceSetColor(Colors.Transparent); _mesh.SurfaceSetUV(Vector2.Zero); _mesh.SurfaceAddVertex(Vector3.Zero); }

		DrawShafts(eye, now);
		DrawArcs(eye);
		DrawStreaks(eye);
		_mesh.SurfaceEnd();
	}

	/// <summary>Os raios de luz: do alto (estreitos, já transparentes) ao chão
	/// (abertos), girando devagar, e o miolo fraco que os junta.</summary>
	private void DrawShafts(Vector3 eye, float now)
	{
		if (_glow <= 0.01f) return;
		Color soft = Tint.Lerp(Colors.White, 0.45f);
		int steps = _shaft.Length - 1;
		float tremble = 0.85f + 0.15f * Mathf.Sin((float)_phase);
		foreach (Ray ray in _rays)
		{
			float angle = ray.Angle + now * ray.Turn;
			float beat = 0.72f + 0.28f * Mathf.Sin(now * ray.Beat + ray.Phase);
			Vector3 around = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
			for (int i = 0; i <= steps; i++)
			{
				float s = i / (float)steps;                 // 0 no alto, 1 no chão
				_shaft[i] = around * (ray.Radius * (0.6f + 0.55f * s)) + Vector3.Up * (Feet + ShaftTop * (1f - s));
			}
			float strength = 0.13f * _glow * beat * tremble;
			float width = ray.Width;
			FxRibbon.Strip(_mesh, _shaft, eye, s => width * (0.55f + 0.7f * s),
				s => new Color(soft.R, soft.G, soft.B, strength * Mathf.SmoothStep(0f, 0.38f, s) * (1f - 0.5f * s)));
		}

		for (int i = 0; i <= steps; i++)
		{
			float s = i / (float)steps;
			_shaft[i] = Vector3.Up * (Feet + ShaftTop * 0.94f * (1f - s));
		}
		float core = 0.028f * _glow;
		FxRibbon.Strip(_mesh, _shaft, eye, s => 0.6f + 0.6f * s,
			s => new Color(soft.R, soft.G, soft.B, core * Mathf.SmoothStep(0f, 0.45f, s) * (1f - 0.35f * s)));
	}

	/// <summary>Raios quebrados que mudam de forma várias vezes por segundo e
	/// somem rápido, com um galho curto de vez em quando.</summary>
	private void DrawArcs(Vector3 eye)
	{
		foreach (Arc arc in _arcs)
		{
			float t = arc.Age / arc.Life;
			_bolt.Seed = arc.Seed + (ulong)(arc.Age / 0.04f);
			Vector3[] path = FxRibbon.Jagged(_bolt, arc.From, arc.To, 7, (arc.To - arc.From).Length() * 0.13f);
			FxRibbon.Bolt(_mesh, path, eye, arc.Width, Tint, (1f - t * t) * (0.75f + 0.25f * _bolt.Randf()));
			if (_bolt.Randf() < 0.45f)
			{
				Vector3 knot = path[_bolt.RandiRange(2, 5)];
				Vector3 tip = knot + new Vector3(_bolt.RandfRange(-0.3f, 0.3f), _bolt.RandfRange(-0.05f, 0.3f), _bolt.RandfRange(-0.3f, 0.3f));
				FxRibbon.Bolt(_mesh, FxRibbon.Jagged(_bolt, knot, tip, 3, 0.05f), eye, arc.Width * 0.7f, Tint, (1f - t) * 0.7f);
			}
		}
	}

	/// <summary>Riscos de ar em cone, saindo da boca: mudam várias vezes por
	/// segundo. No estouro do começo saem mais, mais compridos e mais fortes.</summary>
	private void DrawStreaks(Vector3 eye)
	{
		if (_amount <= 0.05f) return;
		float blast = _blast >= 0f ? 1f - _blast / BlastLife : 0f;
		_streak.Seed = _seed;
		Vector3 dir = Forward.Normalized();
		Vector3 side = dir.Cross(Vector3.Up).Normalized();
		Vector3 up = side.Cross(dir).Normalized();
		Color tint = Tint;
		int count = StreakCount + Mathf.RoundToInt(StreakBlast * blast);
		for (int i = 0; i < count; i++)
		{
			float angle = _streak.Randf() * Mathf.Tau;
			float spread = _streak.RandfRange(0.1f, 0.5f);
			Vector3 d = (dir + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * spread).Normalized();
			float near = _streak.RandfRange(0.2f, 0.9f);
			float length = _streak.RandfRange(0.3f, 0.75f) * (1f + 0.5f * blast);
			_line[0] = Mouth + d * near;
			_line[1] = Mouth + d * (near + length);
			// Mais fraco quanto mais longe da boca nasce: o ar perde força.
			float a = _amount * _streak.RandfRange(0.25f, 0.6f) * (1f + 0.6f * blast) * (1.15f - 0.6f * near);
			// Aceso a um terço do caminho, sumindo para as duas pontas.
			FxRibbon.Dart(_mesh, _line[0], _line[0].Lerp(_line[1], 0.3f), _line[1], eye, _streak.RandfRange(0.008f, 0.016f), tint, a);
		}
	}

	private void SleepRibbons()
	{
		_mesh.ClearSurfaces();
		_canvas.Visible = false;
	}
}
