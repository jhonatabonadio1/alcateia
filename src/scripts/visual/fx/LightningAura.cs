using Godot;

/// <summary>
/// Os raios que ficam no lobo com o especial armado (pedido do Jhon,
/// 18/11: "no momento que eu apertar o Q já aparecem os raios nele, e
/// ficam até eu avançar"): arcos curtos de neon correndo pelo corpo, uns
/// descendo até o chão, e uma luz tremendo. Liga e desliga por
/// <see cref="Active"/>; apaga em um instante, sem estalo.
///
/// Fica no corpo (anda com ele). O desenho muda umas vinte vezes por
/// segundo.
/// </summary>
public partial class LightningAura : Node3D
{
	public bool Active { get; set; }

	public Color Tint { get; set; } = new(0.35f, 0.75f, 1f);

	/// <summary>Arcos pelo corpo a cada desenho.</summary>
	public int Arcs { get; set; } = 6;

	/// <summary>Altura dos pés, relativa ao corpo.</summary>
	public float Feet { get; set; } = -0.875f;

	private const double Flicker = 0.05;
	private const float Width = 0.028f;

	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;
	private OmniLight3D _light;
	private readonly RandomNumberGenerator _rng = new();
	private double _nextDraw;
	private float _amount;

	/// <summary>Quanto aparece agora (0 a 1). Para os testes.</summary>
	public float Amount => _amount;

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		_canvas = FxRibbon.Canvas(_mesh, world: false);
		AddChild(_canvas);
		_light = new OmniLight3D { LightEnergy = 0f, OmniRange = 3.2f, ShadowEnabled = false, Position = new Vector3(0, 0.2f, 0) };
		AddChild(_light);
	}

	public override void _Process(double delta)
	{
		_amount = Mathf.MoveToward(_amount, Active ? 1f : 0f, (float)delta / (Active ? 0.08f : 0.18f));
		if (_amount <= 0.001f)
		{
			if (_canvas.Visible)
			{
				_canvas.Visible = false;
				_light.LightEnergy = 0f;
				_mesh.ClearSurfaces();
			}
			return;
		}

		_canvas.Visible = true;
		_nextDraw -= delta;
		if (_nextDraw > 0) return;
		_nextDraw = Flicker;

		_light.LightColor = Tint.Lerp(Colors.White, 0.3f);
		_light.LightEnergy = _amount * _rng.RandfRange(0.8f, 2.0f);

		Vector3 eye = GetViewport().GetCamera3D() is Camera3D camera ? ToLocal(camera.GlobalPosition) : new Vector3(0, 2, 6);
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		// Arcos agarrados ao corpo: de um ponto da "casca" a outro perto.
		for (int i = 0; i < Arcs; i++)
		{
			Vector3 from = Shell(_rng.Randf() * Mathf.Tau, _rng.RandfRange(0.1f, 1.0f));
			float turn = _rng.RandfRange(0.5f, 1.3f) * (_rng.Randf() < 0.5f ? -1f : 1f);
			float angle = Mathf.Atan2(from.Z, from.X) + turn;
			float height = Mathf.Clamp((from.Y - Feet) / Height + _rng.RandfRange(-0.3f, 0.3f), 0.05f, 1.05f);
			Vector3 to = Shell(angle, height);
			Vector3[] arc = FxRibbon.Jagged(_rng, from, to, 6, 0.09f);
			// Barriga para fora: o arco salta do corpo, não corta por dentro.
			for (int k = 1; k < arc.Length - 1; k++)
			{
				Vector3 outward = arc[k] with { Y = 0 };
				if (outward.LengthSquared() > 1e-6f)
					arc[k] += outward.Normalized() * 0.16f * Mathf.Sin(k / (arc.Length - 1f) * Mathf.Pi);
			}
			FxRibbon.Bolt(_mesh, arc, eye, Width, Tint, _amount * _rng.RandfRange(0.6f, 1f));
		}

		// Dois ou três descem até o chão e correm um pouco por ele.
		int ground = _rng.RandiRange(2, 3);
		for (int i = 0; i < ground; i++)
		{
			float angle = _rng.Randf() * Mathf.Tau;
			Vector3 from = Shell(angle, _rng.RandfRange(0.15f, 0.5f));
			Vector3 to = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _rng.RandfRange(0.7f, 1.5f) + Vector3.Up * (Feet + 0.03f);
			FxRibbon.Bolt(_mesh, FxRibbon.Jagged(_rng, from, to, 6, 0.12f), eye, Width * 0.9f, Tint, _amount * 0.9f);
		}

		_mesh.SurfaceEnd();
	}

	private const float Height = 1.85f;

	/// <summary>Um ponto na casca do corpo: um ovo em pé em volta dele.</summary>
	private Vector3 Shell(float angle, float height01)
	{
		float y = Feet + Height * height01;
		// Mais largo nos ombros, fino nos pés e na cabeça.
		float radius = 0.28f + 0.3f * Mathf.Sin(Mathf.Clamp(height01, 0f, 1f) * Mathf.Pi * 0.85f + 0.25f);
		return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
	}
}
