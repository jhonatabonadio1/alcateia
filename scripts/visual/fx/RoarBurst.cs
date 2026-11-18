using Godot;
using System.Collections.Generic;

/// <summary>
/// O rugido que se vê (pedido do Jhon, 18/11: "tá muito paia o lobo só
/// ficar meio azulzinho"): enquanto o lobo ruge, anéis de choque saem da
/// boca e abrem para a frente, um atrás do outro; riscos de força
/// espirram em cone; uma onda corre pelo chão em volta dele; e uma luz
/// na cor do lobo pulsa. Some sozinho quando o rugido para.
///
/// Fica no corpo. Quem diz de onde sai (a boca) e para onde é o
/// <see cref="WolfFx"/>, pelo osso do focinho.
/// </summary>
public partial class RoarBurst : Node3D
{
	public bool Active { get; set; }

	public Color Tint { get; set; } = new(0.35f, 0.75f, 1f);

	/// <summary>A boca e o sentido do rugido, no espaço deste nó.</summary>
	public Vector3 Mouth { get; set; } = new(0, 0.65f, -0.4f);
	public Vector3 Forward { get; set; } = Vector3.Forward;

	/// <summary>Altura dos pés, relativa ao corpo.</summary>
	public float Feet { get; set; } = -0.875f;

	/// <summary>Anéis vivos agora. Para os testes.</summary>
	public int Rings => _rings.Count;

	private const float RingEvery = 0.15f;
	private const float RingLife = 0.55f;
	private const float GroundEvery = 0.5f;
	private const float GroundLife = 0.6f;

	private struct Wave
	{
		public Vector3 From, Dir;
		public float Age;
		public bool Ground;
	}

	private readonly List<Wave> _rings = new();
	private readonly RandomNumberGenerator _rng = new();
	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;
	private OmniLight3D _light;
	private double _ringClock, _groundClock, _flicker;
	private uint _seed;
	private float _amount;

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		_canvas = FxRibbon.Canvas(_mesh, world: false);
		AddChild(_canvas);
		_light = new OmniLight3D { LightEnergy = 0f, OmniRange = 5f, ShadowEnabled = false };
		AddChild(_light);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_amount = Mathf.MoveToward(_amount, Active ? 1f : 0f, dt / (Active ? 0.1f : 0.25f));

		if (Active)
		{
			_ringClock -= delta;
			if (_ringClock <= 0)
			{
				_ringClock = RingEvery;
				_rings.Add(new Wave { From = Mouth, Dir = Forward.Normalized() });
			}

			_groundClock -= delta;
			if (_groundClock <= 0)
			{
				_groundClock = GroundEvery;
				_rings.Add(new Wave { From = new Vector3(0, Feet + 0.04f, 0), Dir = Vector3.Up, Ground = true });
			}
		}
		else
		{
			// O próximo rugido começa com a onda do chão e um anel, na hora.
			_ringClock = 0;
			_groundClock = 0;
		}

		for (int i = _rings.Count - 1; i >= 0; i--)
		{
			Wave w = _rings[i];
			w.Age += dt;
			if (w.Age >= (w.Ground ? GroundLife : RingLife)) _rings.RemoveAt(i);
			else _rings[i] = w;
		}

		if (_rings.Count == 0 && _amount <= 0.001f)
		{
			if (_canvas.Visible)
			{
				_canvas.Visible = false;
				_mesh.ClearSurfaces();
				_light.LightEnergy = 0f;
			}
			return;
		}

		_canvas.Visible = true;
		_flicker -= delta;
		if (_flicker <= 0)
		{
			_flicker = 0.045;
			_seed = _rng.Randi();
		}

		_light.Position = Mouth + Forward * 0.5f;
		_light.LightColor = Tint.Lerp(Colors.White, 0.25f);
		_light.LightEnergy = _amount * (1.1f + 0.6f * Mathf.Sin((float)Time.GetTicksMsec() * 0.03f));

		Vector3 eye = GetViewport().GetCamera3D() is Camera3D camera ? ToLocal(camera.GlobalPosition) : new Vector3(0, 2, 6);
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		foreach (Wave w in _rings)
		{
			if (w.Ground)
			{
				float t = w.Age / GroundLife;
				float e = 1f - Mathf.Pow(1f - t, 3f);
				FxRibbon.Ring(_mesh, w.From, Vector3.Up, 0.3f + 3.1f * e, 0.55f * (1f - t * 0.6f),
					new Color(Tint.R, Tint.G, Tint.B, 0.5f * (1f - t)), 44);
				FxRibbon.Ring(_mesh, w.From + Vector3.Up * 0.01f, Vector3.Up, 0.3f + 3.1f * e, 0.1f,
					new Color(1f, 0.97f, 0.94f, 0.55f * (1f - t)), 44);
			}
			else
			{
				// O anel sai pequeno e rápido da boca, abre e perde força.
				float t = w.Age / RingLife;
				float e = 1f - Mathf.Pow(1f - t, 2.2f);
				Vector3 center = w.From + w.Dir * (0.25f + 2.9f * e);
				float radius = 0.16f + 1.25f * e;
				float alpha = Mathf.Pow(1f - t, 1.4f);
				FxRibbon.Ring(_mesh, center, w.Dir, radius, 0.3f * (1f - t * 0.5f) + 0.05f,
					new Color(Tint.R, Tint.G, Tint.B, 0.6f * alpha), 36);
				FxRibbon.Ring(_mesh, center, w.Dir, radius, 0.07f,
					new Color(1f, 0.97f, 0.94f, 0.75f * alpha), 36);
			}
		}

		// Riscos de força em cone, saindo da boca: mudam várias vezes por segundo.
		if (_amount > 0.05f)
		{
			var rng = new RandomNumberGenerator { Seed = _seed };
			Vector3 dir = Forward.Normalized();
			Vector3 side = dir.Cross(Vector3.Up).Normalized();
			Vector3 up = side.Cross(dir).Normalized();
			var line = new Vector3[2];
			for (int i = 0; i < 12; i++)
			{
				float angle = rng.Randf() * Mathf.Tau;
				float spread = rng.RandfRange(0.12f, 0.55f);
				Vector3 d = (dir + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * spread).Normalized();
				float near = rng.RandfRange(0.35f, 1.1f);
				float length = rng.RandfRange(0.5f, 1.3f);
				line[0] = Mouth + d * near;
				line[1] = Mouth + d * (near + length);
				float a = _amount * rng.RandfRange(0.25f, 0.6f);
				FxRibbon.Strip(_mesh, line, eye, s => 0.035f * (1f - s * 0.7f),
					s => new Color(Mathf.Lerp(1f, Tint.R, s), Mathf.Lerp(1f, Tint.G, s), Mathf.Lerp(1f, Tint.B, s), a * (1f - s)));
			}
		}

		_mesh.SurfaceEnd();
	}
}
