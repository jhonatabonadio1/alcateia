using Godot;
using System.Collections.Generic;

/// <summary>
/// O rugido que se vê. Desde 23/11 (Jhon: "deixando apenas a silhueta azul e os raios"), só
/// duas coisas: a silhueta do lobo acende na cor dele (o <see cref="WolfFx"/> lê
/// <see cref="Glow"/> e acende o contorno do corpo) e arcos elétricos saltam do chão para o
/// corpo, cada vez mais juntos enquanto o rugido carrega; se ele rugiu até o fim, uma descarga
/// sai do corpo para todos os lados quando o escudo enche.
///
/// Saíram no mesmo dia, um de cada vez pedidos antes: os raios de luz que desciam do alto, as
/// fagulhas subindo, a poça de luz nos pés, a onda no chão, o ar saindo da boca, o anel que
/// subia com o escudo cheio e a luz que clareava o lobo.
///
/// Fica no corpo; quem liga é o <see cref="WolfFx"/>.
/// </summary>
public partial class RoarBurst : Node3D
{
	public bool Active { get; set; }

	public Color Tint { get; set; } = new(0.35f, 0.75f, 1f);

	/// <summary>Altura dos pés, relativa ao corpo.</summary>
	public float Feet { get; set; } = -0.875f;

	/// <summary>Quanto o rugido carregou, de 0 a 1: acende a silhueta (WolfFx) e junta os arcos.</summary>
	public float Glow => _glow;

	/// <summary>O escudo acabou de encher (rugiu até o fim): a descarga e o clarão da silhueta.</summary>
	public bool Sweeping => _sweep >= 0f;

	/// <summary>O clarão do escudo cheio, de 1 (agora) a 0.</summary>
	public float Flash => _sweep >= 0f ? 1f - _sweep / SweepLife : 0f;

	/// <summary>Arcos elétricos acesos agora. Para os testes.</summary>
	public int Arcs => _arcs.Count;

	private struct Arc { public Vector3 From, To; public float Age, Life, Width; public ulong Seed; }
	private readonly List<Arc> _arcs = new();
	private double _arcClock;
	private const int ArcMax = 6;

	private const float SweepLife = 0.42f;
	private float _glow, _held, _sweep = -1f;
	private bool _wasActive;

	private readonly RandomNumberGenerator _rng = new();
	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		_canvas = FxRibbon.Canvas(_mesh, world: false);
		AddChild(_canvas);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		// Acende devagar, junto com o escudo que está por vir.
		_glow = Mathf.MoveToward(_glow, Active ? 1f : 0f, dt / (Active ? 0.6f : 0.4f));

		if (Active) _held += dt;
		else
		{
			// Rugiu até o fim: o escudo encheu, a descarga sai do corpo.
			if (_wasActive && _held >= WolfMoves.Duration(WolfMove.Howl) * 0.85f) _sweep = 0f;
			_held = 0f;
		}
		if (!Active && _wasActive && _sweep >= 0f)
			for (int i = 0; i < 10; i++) AddArc(burst: true);
		_wasActive = Active;
		if (_sweep >= 0f)
		{
			_sweep += dt;
			if (_sweep >= SweepLife) _sweep = -1f;
		}

		// Arcos: saltam do chão para o corpo, mais juntos quanto mais carregado.
		_arcClock -= delta;
		if (_glow > 0.12f && _arcClock <= 0)
		{
			_arcClock = Mathf.Lerp(0.16f, 0.045f, _glow) * _rng.RandfRange(0.6f, 1.4f);
			if (_arcs.Count < ArcMax) AddArc(burst: false);
		}
		for (int i = _arcs.Count - 1; i >= 0; i--)
		{
			Arc arc = _arcs[i];
			arc.Age += dt;
			if (arc.Age >= arc.Life) _arcs.RemoveAt(i);
			else _arcs[i] = arc;
		}

		if (_arcs.Count == 0)
		{
			if (_canvas.Visible)
			{
				_canvas.Visible = false;
				_mesh.ClearSurfaces();
			}
			return;
		}

		_canvas.Visible = true;
		Vector3 eye = GetViewport().GetCamera3D() is Camera3D camera ? ToLocal(camera.GlobalPosition) : new Vector3(0, 2, 6);
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		DrawArcs(eye);

		_mesh.SurfaceEnd();
	}

	private void AddArc(bool burst)
	{
		float angle = _rng.Randf() * Mathf.Tau;
		Vector3 around = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
		Vector3 from, to;
		if (burst)
		{
			// Escudo cheio: a descarga sai do corpo para todos os lados.
			from = new Vector3(0, Feet + _rng.RandfRange(0.7f, 1.5f), 0) + around * 0.2f;
			to = around * _rng.RandfRange(1.0f, 1.6f) + Vector3.Up * (Feet + _rng.RandfRange(0.05f, 2.0f));
		}
		else
		{
			// Carregando: do chão em volta dele para o corpo.
			float turn = angle + _rng.RandfRange(-0.5f, 0.5f);
			from = around * _rng.RandfRange(0.5f, 1.0f) + Vector3.Up * (Feet + 0.03f);
			to = new Vector3(Mathf.Cos(turn), 0, Mathf.Sin(turn)) * _rng.RandfRange(0.12f, 0.32f) + Vector3.Up * (Feet + _rng.RandfRange(0.45f, 1.65f));
		}
		_arcs.Add(new Arc
		{
			From = from,
			To = to,
			Life = burst ? _rng.RandfRange(0.2f, 0.34f) : _rng.RandfRange(0.09f, 0.2f),
			Width = burst ? _rng.RandfRange(0.024f, 0.036f) : _rng.RandfRange(0.012f, 0.022f),
			Seed = _rng.Randi()
		});
	}

	/// <summary>Os arcos: raios quebrados que mudam de forma várias vezes por segundo e somem
	/// rápido.</summary>
	private void DrawArcs(Vector3 eye)
	{
		// Os arcos: raios quebrados que mudam de forma várias vezes por segundo e somem rápido.
		var bolt = new RandomNumberGenerator();
		foreach (Arc arc in _arcs)
		{
			float t = arc.Age / arc.Life;
			bolt.Seed = arc.Seed + (ulong)(arc.Age / 0.04f);
			Vector3[] path = FxRibbon.Jagged(bolt, arc.From, arc.To, 7, (arc.To - arc.From).Length() * 0.13f);
			FxRibbon.Bolt(_mesh, path, eye, arc.Width, Tint, (1f - t * t) * (0.75f + 0.25f * bolt.Randf()));
			// Um galho curto, de vez em quando.
			if (bolt.Randf() < 0.45f)
			{
				Vector3 knot = path[bolt.RandiRange(2, 5)];
				Vector3 tip = knot + new Vector3(bolt.RandfRange(-0.3f, 0.3f), bolt.RandfRange(-0.05f, 0.3f), bolt.RandfRange(-0.3f, 0.3f));
				FxRibbon.Bolt(_mesh, FxRibbon.Jagged(bolt, knot, tip, 3, 0.05f), eye, arc.Width * 0.7f, Tint, (1f - t) * 0.7f);
			}
		}
	}
}
