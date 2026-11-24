using Godot;
using System.Collections.Generic;

/// <summary>
/// O rugido que se vê (pedido do Jhon, 18/11: "tá muito paia o lobo só
/// ficar meio azulzinho"): enquanto o lobo ruge, riscos de ar espirram da
/// boca em cone; uma onda corre pelo chão em volta dele; e uma luz na
/// cor do lobo pulsa. Some sozinho quando o rugido para.
///
/// Os anéis de choque que abriam na frente da boca saíram no mesmo dia
/// ("pode tirar somente o círculo na frente, deixe só os arzinhos saindo
/// da boca"): da boca agora só sai o ar.
///
/// **A luz de regeneração** (ideia do Jhon, 18/11: "poderia vir uma luz
/// do céu dando um efeito de regeneração, mas não tão alto para não
/// entregar posicionamento de lobos escondidos; combinaria mais com
/// aumentar escudo"). Rugir enche o escudo, então o rugido parece uma
/// recarga, não um ataque: raios de luz descem sobre o lobo, fagulhas
/// sobem do chão em volta dele, uma poça de luz acende nos pés e
/// **arcos elétricos** saltam do chão para o corpo, cada vez mais
/// juntos ("o core do lobo é eletricidade, não brilho brilho") — e, se
/// ele rugiu até o fim, um anel sobe dos pés à cabeça com uma descarga
/// para todos os lados na hora em que o escudo enche.
///
/// A luz é BAIXA de propósito: os raios nascem a <see cref="ShaftTop"/>
/// dos pés (meio metro acima da cabeça) já transparentes e só ganham
/// força na altura do peito. Não passa por cima de parede nem de caixote
/// alto, e some atrás de qualquer coisa como o resto do lobo.
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

	/// <summary>Ondas vivas no chão agora. Para os testes.</summary>
	public int Rings => _rings.Count;

	/// <summary>A onda do chão em volta do lobo.</summary>
	public static bool SpawnGroundWaves = true;

	private const float GroundEvery = 0.5f;
	private const float GroundLife = 0.6f;

	/// <summary>Altura dos raios de luz, a partir dos pés. Baixa: não
	/// entrega o lobo atrás de uma parede.</summary>
	public const float ShaftTop = 2.35f;

	/// <summary>Quanto a luz está acesa agora, de 0 a 1. Para os testes.</summary>
	public float Glow => _glow;

	/// <summary>O anel de "escudo cheio" está subindo. Para os testes.</summary>
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
	private const int RayCount = 7;
	private const int MoteCount = 20;

	private struct Ray { public float Angle, Radius, Width, Turn, Beat, Phase; }
	private struct Mote { public float Angle, Radius, Speed, Phase, Size; }

	private Ray[] _rays;
	private Mote[] _motes;
	private float _glow, _held, _sweep = -1f;
	private bool _wasActive;
	private double _time;

	private struct Wave
	{
		public Vector3 From;
		public float Age;
	}

	private readonly List<Wave> _rings = new();
	private readonly RandomNumberGenerator _rng = new();
	private ImmediateMesh _mesh;
	private MeshInstance3D _canvas;
	private OmniLight3D _light;
	private double _groundClock, _flicker;
	private uint _seed;
	private float _amount;

	public override void _Ready()
	{
		_rng.Randomize();
		_mesh = new ImmediateMesh();
		_canvas = FxRibbon.Canvas(_mesh, world: false);
		AddChild(_canvas);
		_light = new OmniLight3D { LightEnergy = 0f, OmniRange = 3.8f, ShadowEnabled = false };
		AddChild(_light);

		_rays = new Ray[RayCount];
		for (int i = 0; i < RayCount; i++)
			_rays[i] = new Ray
			{
				Angle = Mathf.Tau * i / RayCount + _rng.RandfRange(-0.3f, 0.3f),
				// Em volta do corpo, não em cima dele: a luz emoldura o lobo sem lavar a cor.
				Radius = _rng.RandfRange(0.5f, 0.85f),
				Width = _rng.RandfRange(0.12f, 0.3f),
				Turn = _rng.RandfRange(0.12f, 0.4f) * (_rng.Randf() < 0.5f ? -1f : 1f),
				Beat = _rng.RandfRange(1.6f, 3.4f),
				Phase = _rng.Randf() * Mathf.Tau
			};
		_motes = new Mote[MoteCount];
		for (int i = 0; i < MoteCount; i++)
			_motes[i] = new Mote
			{
				Angle = _rng.Randf() * Mathf.Tau,
				Radius = _rng.RandfRange(0.3f, 0.85f),
				Speed = _rng.RandfRange(0.55f, 1.05f),
				Phase = _rng.Randf(),
				Size = _rng.RandfRange(0.035f, 0.07f)
			};
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += delta;
		_amount = Mathf.MoveToward(_amount, Active ? 1f : 0f, dt / (Active ? 0.1f : 0.25f));
		// A luz acende devagar, junto com o escudo que está por vir.
		_glow = Mathf.MoveToward(_glow, Active ? 1f : 0f, dt / (Active ? 0.6f : 0.4f));

		if (Active) _held += dt;
		else
		{
			// Rugiu até o fim: o escudo encheu, o anel sobe.
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

		// A onda do chão: um círculo com borda abrindo em volta. Saiu em
		// 18/11 e voltou em 19/11, a pedido do Jhon.
		if (Active && SpawnGroundWaves)
		{
			_groundClock -= delta;
			if (_groundClock <= 0)
			{
				_groundClock = GroundEvery;
				_rings.Add(new Wave { From = new Vector3(0, Feet + 0.04f, 0) });
			}
		}
		else
		{
			// O próximo rugido começa com a onda do chão, na hora.
			_groundClock = 0;
		}

		for (int i = _rings.Count - 1; i >= 0; i--)
		{
			Wave w = _rings[i];
			w.Age += dt;
			if (w.Age >= GroundLife) _rings.RemoveAt(i);
			else _rings[i] = w;
		}

		if (_rings.Count == 0 && _arcs.Count == 0 && _amount <= 0.001f && _glow <= 0.001f && _sweep < 0f)
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

		// A luz de verdade vem de cima: clareia a cabeça e os ombros.
		float now = (float)_time;
		float sweepFlash = _sweep >= 0f ? 1f - _sweep / SweepLife : 0f;
		_light.Position = new Vector3(0, Feet + 2.1f, 0) + Forward * 0.25f;
		_light.LightColor = Tint.Lerp(Colors.White, 0.45f);
		_light.LightEnergy = _glow * (1.05f + 0.2f * Mathf.Sin(now * 5f)) + 1.8f * sweepFlash;

		Vector3 eye = GetViewport().GetCamera3D() is Camera3D camera ? ToLocal(camera.GlobalPosition) : new Vector3(0, 2, 6);
		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

		foreach (Wave w in _rings)
		{
			float t = w.Age / GroundLife;
			float e = 1f - Mathf.Pow(1f - t, 3f);
			FxRibbon.Ring(_mesh, w.From, Vector3.Up, 0.3f + 3.1f * e, 0.55f * (1f - t * 0.6f),
				new Color(Tint.R, Tint.G, Tint.B, 0.5f * (1f - t)), 44);
			FxRibbon.Ring(_mesh, w.From + Vector3.Up * 0.01f, Vector3.Up, 0.3f + 3.1f * e, 0.1f,
				new Color(1f, 0.97f, 0.94f, 0.55f * (1f - t)), 44);
		}

		DrawRegen(eye, now);

		// Riscos de ar em cone, saindo da boca: mudam várias vezes por segundo.
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

	/// <summary>A luz de regeneração: raios que descem, fagulhas que
	/// sobem, arcos elétricos, a poça nos pés e o anel de escudo cheio.</summary>
	private void DrawRegen(Vector3 eye, float now)
	{
		Color soft = Tint.Lerp(Colors.White, 0.45f);
		Color hot = Tint.Lerp(Colors.White, 0.8f);
		float ground = Feet + 0.03f;

		if (_glow > 0.01f)
		{
			// Os raios: do alto (estreitos, já transparentes) ao chão (abertos).
			const int Steps = 6;
			var points = new Vector3[Steps + 1];
			foreach (Ray ray in _rays)
			{
				float angle = ray.Angle + now * ray.Turn;
				float beat = 0.72f + 0.28f * Mathf.Sin(now * ray.Beat + ray.Phase);
				Vector3 around = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
				for (int i = 0; i <= Steps; i++)
				{
					float s = i / (float)Steps;                 // 0 no alto, 1 no chão
					points[i] = around * (ray.Radius * (0.6f + 0.55f * s)) + Vector3.Up * (Feet + ShaftTop * (1f - s));
				}
				float strength = 0.16f * _glow * beat;
				float width = ray.Width;
				FxRibbon.Strip(_mesh, points, eye, s => width * (0.55f + 0.7f * s),
					s => new Color(soft.R, soft.G, soft.B, strength * Mathf.SmoothStep(0f, 0.38f, s) * (1f - 0.5f * s)));
			}

			// O miolo: uma coluna larga e fraca que junta os raios.
			for (int i = 0; i <= Steps; i++)
			{
				float s = i / (float)Steps;
				points[i] = Vector3.Up * (Feet + ShaftTop * 0.94f * (1f - s));
			}
			float core = 0.03f * _glow;
			FxRibbon.Strip(_mesh, points, eye, s => 0.6f + 0.6f * s,
				s => new Color(soft.R, soft.G, soft.B, core * Mathf.SmoothStep(0f, 0.45f, s) * (1f - 0.35f * s)));

			// As fagulhas: sobem do chão em volta do lobo, girando devagar, e somem na altura do peito.
			// Cada uma é um ponto de luz macio: a fita some nos lados pela textura e nas pontas pela cor.
			const int Dot = 4;
			var streak = new Vector3[Dot + 1];
			foreach (Mote mote in _motes)
			{
				float f = Mathf.PosMod(now * mote.Speed + mote.Phase, 1f);
				float angle = mote.Angle + f * 1.1f;
				Vector3 at = new(Mathf.Cos(angle) * mote.Radius * (1f - 0.25f * f), Feet + 0.06f + f * 1.75f, Mathf.Sin(angle) * mote.Radius * (1f - 0.25f * f));
				// Um rastro curto para baixo: sobem, então esticam um pouco.
				float tall = mote.Size * 1.5f;
				for (int i = 0; i <= Dot; i++) streak[i] = at + Vector3.Up * (tall * i / Dot);
				float a = _glow * 0.8f * Mathf.Sin(Mathf.Pi * f);
				float size = mote.Size;
				FxRibbon.Strip(_mesh, streak, eye, _ => size,
					s => new Color(hot.R, hot.G, hot.B, a * Mathf.Pow(Mathf.Sin(Mathf.Pi * s), 1.5f)));
			}

			// A poça de luz nos pés.
			float breath = 0.85f + 0.15f * Mathf.Sin(now * 3.2f);
			FxRibbon.Ring(_mesh, new Vector3(0, ground, 0), Vector3.Up, 0.55f, 1.1f,
				new Color(soft.R, soft.G, soft.B, 0.38f * _glow * breath), 36);
			FxRibbon.Ring(_mesh, new Vector3(0, ground + 0.01f, 0), Vector3.Up, 0.3f, 0.6f,
				new Color(hot.R, hot.G, hot.B, 0.18f * _glow * breath), 28);
		}

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

		// Escudo cheio: um anel sobe dos pés à cabeça, com um rastro logo abaixo.
		if (_sweep >= 0f)
		{
			float t = _sweep / SweepLife;
			float rise = 1f - Mathf.Pow(1f - t, 2f);
			for (int i = 0; i < 3; i++)
			{
				float y = Feet + 0.12f + 1.7f * rise - 0.16f * i;
				if (y < Feet) continue;
				float fade = (1f - t) * (1f - 0.33f * i);
				FxRibbon.Ring(_mesh, new Vector3(0, y, 0), Vector3.Up, 0.58f - 0.12f * rise, 0.26f - 0.05f * i,
					new Color(hot.R, hot.G, hot.B, 0.75f * fade), 32);
			}
		}
	}
}
