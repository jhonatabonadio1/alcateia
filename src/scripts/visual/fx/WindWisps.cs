using Godot;
using System.Collections.Generic;

/// <summary>
/// O vento passando pelo lobo em velocidade: fios de ar finos e compridos,
/// que ondulam e apagam nas pontas (pedido do Jhon, 18/11: "tá uns
/// cilindros; poderia ser uns ventos mais finos e delicados, com
/// movimento"). Nascem à frente do corpo, em volta do caminho, e ficam
/// no mundo: o lobo passa por eles.
///
/// Cada fio é uma fita macia de frente para a câmera, desenhada todo
/// quadro. Poucos de cada vez; a quantidade segue <see cref="Amount"/>.
/// </summary>
public partial class WindWisps : Node3D
{
	/// <summary>Quanto vento há agora (0 a 1).</summary>
	public float Amount { get; set; }

	/// <summary>A velocidade do corpo: os fios correm no sentido contrário.</summary>
	public Vector3 Velocity { get; set; }

	/// <summary>Quantos fios ao mesmo tempo, no máximo.</summary>
	public int Max { get; set; } = 8;

	/// <summary>Fios vivos agora. Para os testes.</summary>
	public int Count => _wisps.Count;

	private struct Wisp
	{
		public Vector3 Origin, Dir, SideA, SideB;
		public float Age, Life, Length, Amp, Phase, Width, Drift;
	}

	private const int Points = 12;

	private readonly List<Wisp> _wisps = new();
	private readonly RandomNumberGenerator _rng = new();
	private readonly Vector3[] _line = new Vector3[Points];
	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;
	private double _spawn;

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		_canvas = FxRibbon.Canvas(_mesh, world: true);
		AddChild(_canvas);
		_canvas.GlobalTransform = Transform3D.Identity;
	}

	public void Clear()
	{
		_wisps.Clear();
		_mesh?.ClearSurfaces();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		float speed = Velocity.Length();

		if (Amount > 0.05f && speed > 0.5f && _wisps.Count < Max)
		{
			_spawn += dt * (8f + 26f * Amount);
			while (_spawn >= 1.0 && _wisps.Count < Max)
			{
				_spawn -= 1.0;
				Spawn(speed);
			}
		}
		else _spawn = 0;

		for (int i = _wisps.Count - 1; i >= 0; i--)
		{
			Wisp w = _wisps[i];
			w.Age += dt;
			if (w.Age >= w.Life) _wisps.RemoveAt(i);
			else _wisps[i] = w;
		}

		_mesh.ClearSurfaces();
		if (_wisps.Count == 0)
		{
			_canvas.Visible = false;
			return;
		}

		_canvas.Visible = true;
		Vector3 eye = GetViewport().GetCamera3D()?.GlobalPosition ?? GlobalPosition + new Vector3(0, 2, 6);
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		foreach (Wisp w in _wisps)
		{
			float life = w.Age / w.Life;
			float fade = Mathf.Sin(life * Mathf.Pi);
			for (int k = 0; k < Points; k++)
			{
				float s = k / (Points - 1f);
				_line[k] = w.Origin + w.Dir * (w.Drift * w.Age + s * w.Length)
					+ w.SideA * (w.Amp * Mathf.Sin(w.Phase + s * 5.2f + w.Age * 7f))
					+ w.SideB * (w.Amp * 0.6f * Mathf.Cos(w.Phase * 1.7f + s * 3.4f + w.Age * 5f));
			}

			float width = w.Width;
			float alpha = 0.38f * fade * Mathf.Clamp(Amount * 1.4f, 0.25f, 1f);
			FxRibbon.Strip(_mesh, _line, eye,
				s => width * (0.25f + 0.75f * Mathf.Sin(s * Mathf.Pi)),
				s => new Color(0.86f, 0.94f, 1f, alpha * Mathf.Pow(Mathf.Sin(s * Mathf.Pi), 0.8f)));
		}
		_mesh.SurfaceEnd();
	}

	private void Spawn(float speed)
	{
		Vector3 forward = Velocity / speed;
		Vector3 side = forward.Cross(Vector3.Up);
		side = side.LengthSquared() > 1e-4f ? side.Normalized() : Vector3.Right;
		Vector3 up = side.Cross(forward).Normalized();

		// Em volta do caminho, num anel: nunca no meio do corpo.
		float angle = _rng.Randf() * Mathf.Tau;
		float radius = _rng.RandfRange(0.45f, 1.05f);
		Vector3 around = side * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius * 0.8f + 0.1f);
		float length = _rng.RandfRange(0.9f, 1.9f) * Mathf.Clamp(speed / 6f, 0.6f, 1.5f);

		_wisps.Add(new Wisp
		{
			// À frente: o fio aparece e o lobo passa por ele.
			Origin = GlobalPosition + around + forward * (_rng.RandfRange(0.6f, 1.6f) + length),
			Dir = -forward,
			SideA = side,
			SideB = up,
			Age = 0f,
			Life = _rng.RandfRange(0.28f, 0.48f),
			Length = length,
			Amp = _rng.RandfRange(0.05f, 0.14f),
			Phase = _rng.Randf() * Mathf.Tau,
			Width = _rng.RandfRange(0.010f, 0.019f),
			Drift = _rng.RandfRange(1.5f, 4f)
		});
	}
}
