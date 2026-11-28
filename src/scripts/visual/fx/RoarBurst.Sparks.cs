using Godot;
using System.Collections.Generic;

/// <summary>
/// As faíscas e a poeira do rugido (<see cref="RoarBurst"/>). Soltas no mundo,
/// não presas ao corpo: o lobo sai andando quando o rugido acaba e o que ele
/// levantou fica onde estava.
///
/// **Faíscas**: cada uma é um risco curto que estica com a velocidade (cabeça
/// quase branca, cauda na cor do lobo sumindo), com peso e arrasto próprios.
/// Quatro levas, uma por tempo do rugido: as que correm para dentro na
/// antecipação, as que saem da boca, as que sobem do chão girando durante a
/// carga e as que espirram para todos os lados no escudo cheio. Tamanhos e
/// velocidades variados dão a profundidade.
///
/// **Poeira**: tufos macios que o estouro empurra pelo chão em roda. Não
/// brilham (cobrem um pouco, na cor de poeira): é o que dá peso ao golpe.
/// </summary>
public partial class RoarBurst
{
	/// <summary>Quantas faíscas vivas, no máximo, por qualidade (Baixa a Ultra).</summary>
	private static readonly int[] SparkBudget = { 28, 56, 96, 120 };

	/// <summary>Faíscas por segundo subindo do chão, com a carga cheia.</summary>
	[Export] public float RisingRate = 46f;

	/// <summary>Faíscas por segundo saindo da boca.</summary>
	[Export] public float MouthRate = 34f;

	/// <summary>Quanto tempo de caminho a cauda da faísca mostra, em segundos.</summary>
	private const float Trail = 0.05f;

	private const int PuffMax = 24;

	/// <summary>O quanto a poeira cobre, no auge. Pouco: é peso, não fumaça.</summary>
	private const float DustAlpha = 0.2f;

	private struct Spark
	{
		public Vector3 Pos, Vel;
		public float Age, Life, Size, Gravity, Drag;
	}

	private struct Puff
	{
		public Vector3 Pos, Vel;
		public float Age, Life, From, To;
	}

	private readonly List<Spark> _sparks = new();
	private readonly List<Puff> _puffs = new();
	private ImmediateMesh _sparkMesh;
	private MeshInstance3D _sparkCanvas;
	private MultiMeshInstance3D _dust;
	private MultiMesh _dustMesh;
	private float _risingDebt, _mouthDebt;

	private int SparkMax => SparkBudget[Mathf.Clamp(Detail, 0, SparkBudget.Length - 1)];

	private void BuildSparks()
	{
		_sparkMesh = new ImmediateMesh();
		_sparkCanvas = FxRibbon.Canvas(_sparkMesh, world: true);
		_sparkCanvas.Visible = false;
		AddChild(_sparkCanvas);

		// Um ponto redondo e macio: o tufo não é um quadrado.
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.SetOffset(0, 0.05f);
		var dot = new GradientTexture2D
		{
			Gradient = gradient, Width = 48, Height = 48,
			Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f)
		};
		_dustMesh = FxQuads.Cloud(dot, PuffMax);
		_dustMesh.VisibleInstanceCount = 0;
		_dust = new MultiMeshInstance3D
		{
			Multimesh = _dustMesh,
			TopLevel = true,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			ExtraCullMargin = 8f,
			Visible = false
		};
		AddChild(_dust);
	}

	private Vector3 FeetWorld => GlobalPosition + Vector3.Up * Feet;

	private void Add(Spark spark)
	{
		if (_sparks.Count < SparkMax) _sparks.Add(spark);
	}

	// ------------------------------------------------------------------ as levas

	/// <summary>Antecipação: a carga corre do chão em volta para dentro dele.</summary>
	private void SpawnIntake()
	{
		Vector3 feet = FeetWorld;
		int count = Detail >= 2 ? 18 : 10;
		for (int i = 0; i < count; i++)
		{
			float angle = _rng.Randf() * Mathf.Tau;
			float radius = _rng.RandfRange(1.5f, 2.6f);
			var around = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
			Vector3 from = feet + around * radius + Vector3.Up * _rng.RandfRange(0.05f, 0.5f);
			Vector3 to = feet + Vector3.Up * _rng.RandfRange(0.5f, 1.2f);
			float life = IntakeTime * _rng.RandfRange(0.9f, 1.35f);
			Add(new Spark
			{
				// Chega no corpo quando a vida acaba: some dentro dele, junto com o estouro.
				Pos = from, Vel = (to - from) / life, Life = life, Size = _rng.RandfRange(0.03f, 0.055f)
			});
		}
	}

	/// <summary>Da boca: saliva de luz que sai com o ar e some logo.</summary>
	private void SpawnMouthSparks(int count)
	{
		Vector3 mouth = ToGlobal(Mouth);
		Vector3 dir = (GlobalBasis * Forward).Normalized();
		Vector3 side = dir.Cross(Vector3.Up).Normalized();
		Vector3 lift = side.Cross(dir);
		for (int i = 0; i < count; i++)
		{
			float angle = _rng.Randf() * Mathf.Tau;
			Vector3 d = (dir + (side * Mathf.Cos(angle) + lift * Mathf.Sin(angle)) * _rng.RandfRange(0.05f, 0.42f)).Normalized();
			Add(new Spark
			{
				Pos = mouth + d * _rng.RandfRange(0.05f, 0.3f), Vel = d * _rng.RandfRange(4.5f, 9f),
				Life = _rng.RandfRange(0.16f, 0.34f), Size = _rng.RandfRange(0.02f, 0.04f), Gravity = 2.5f, Drag = 3.2f
			});
		}
	}

	/// <summary>Sobem do chão em volta dele, girando: a carga chegando.</summary>
	private void SpawnRising(int count)
	{
		Vector3 feet = FeetWorld;
		for (int i = 0; i < count; i++)
		{
			float angle = _rng.Randf() * Mathf.Tau;
			var around = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
			var turn = new Vector3(-around.Z, 0, around.X);
			float radius = _rng.RandfRange(0.35f, 1.25f);
			Add(new Spark
			{
				Pos = feet + around * radius + Vector3.Up * 0.04f,
				Vel = Vector3.Up * _rng.RandfRange(0.9f, 2.1f) + turn * _rng.RandfRange(0.3f, 0.9f) - around * 0.25f,
				// Sobem leves (o peso é negativo) e somem na altura do peito.
				Life = _rng.RandfRange(0.55f, 0.95f), Size = _rng.RandfRange(0.022f, 0.06f), Gravity = -0.6f, Drag = 0.9f
			});
		}
	}

	/// <summary>Escudo cheio: espirram do corpo para todos os lados e caem.
	/// Mais para os lados que para cima: a luz é baixa.</summary>
	private void SpawnBurst(int count)
	{
		Vector3 chest = FeetWorld + Vector3.Up * ShellCenter;
		if (Detail == 0) count /= 2;
		for (int i = 0; i < count; i++)
		{
			float angle = _rng.Randf() * Mathf.Tau;
			var d = new Vector3(Mathf.Cos(angle), _rng.RandfRange(-0.25f, 0.4f), Mathf.Sin(angle)).Normalized();
			Add(new Spark
			{
				Pos = chest + d * ShellWidth * _rng.RandfRange(0.7f, 1f) + Vector3.Up * _rng.RandfRange(-0.7f, 0.5f),
				Vel = d * _rng.RandfRange(2.8f, 7.5f),
				Life = _rng.RandfRange(0.4f, 0.85f), Size = _rng.RandfRange(0.028f, 0.065f), Gravity = 6.5f, Drag = 2.4f
			});
		}
	}

	/// <summary>A roda de poeira que o estouro empurra pelo chão.</summary>
	private void SpawnDust(int count, float slowest, float fastest)
	{
		if (Detail < 1) return;
		Vector3 feet = FeetWorld;
		float start = _rng.Randf() * Mathf.Tau;
		for (int i = 0; i < count && _puffs.Count < PuffMax; i++)
		{
			float angle = start + Mathf.Tau * i / count + _rng.RandfRange(-0.2f, 0.2f);
			var around = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
			_puffs.Add(new Puff
			{
				Pos = feet + around * _rng.RandfRange(0.5f, 0.8f),
				Vel = around * _rng.RandfRange(slowest, fastest),
				Life = _rng.RandfRange(0.55f, 0.95f), From = _rng.RandfRange(0.25f, 0.4f), To = _rng.RandfRange(0.8f, 1.2f)
			});
		}
	}

	// ------------------------------------------------------------------ por quadro

	private void TickSparks(float dt)
	{
		// As levas contínuas: a da boca enquanto ele ruge, a do chão crescendo com a carga.
		if (Active && _blasted)
		{
			float share = SparkMax / 96f;
			_mouthDebt += dt * MouthRate * share;
			_risingDebt += dt * RisingRate * share * (0.25f + 0.75f * _level) * (Grounded ? 1f : 0.4f);
			int mouth = (int)_mouthDebt, rising = (int)_risingDebt;
			_mouthDebt -= mouth;
			_risingDebt -= rising;
			if (mouth > 0) SpawnMouthSparks(mouth);
			if (rising > 0) SpawnRising(rising);
		}
		else
		{
			_mouthDebt = _risingDebt = 0f;
		}

		for (int i = _sparks.Count - 1; i >= 0; i--)
		{
			Spark s = _sparks[i];
			s.Age += dt;
			if (s.Age >= s.Life) { _sparks.RemoveAt(i); continue; }
			s.Vel += Vector3.Down * (s.Gravity * dt);
			s.Vel *= Mathf.Max(0f, 1f - s.Drag * dt);
			s.Pos += s.Vel * dt;
			_sparks[i] = s;
		}

		for (int i = _puffs.Count - 1; i >= 0; i--)
		{
			Puff p = _puffs[i];
			p.Age += dt;
			if (p.Age >= p.Life) { _puffs.RemoveAt(i); continue; }
			// Freia logo: o empurrão é um golpe, não um vento.
			p.Vel *= Mathf.Max(0f, 1f - 4.2f * dt);
			p.Pos += p.Vel * dt;
			_puffs[i] = p;
		}
	}

	private void DrawSparks()
	{
		Camera3D camera = GetViewport().GetCamera3D();
		Vector3 eye = camera?.GlobalPosition ?? GlobalPosition + new Vector3(0, 2, 6);

		_sparkMesh.ClearSurfaces();
		_sparkCanvas.Visible = _sparks.Count > 0;
		if (_sparks.Count > 0)
		{
			// O canvas é do mundo: os pontos vão em coordenadas do mundo.
			_sparkCanvas.GlobalTransform = Transform3D.Identity;
			Color tint = Tint;
			_sparkMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
			foreach (Spark s in _sparks)
			{
				float t = s.Age / s.Life;
				// Nasce de uma vez e apaga no fim; esfria do branco para a cor.
				float alpha = Mathf.Min(1f, t * 12f) * (1f - t * t);
				Vector3 back = s.Vel * Trail;
				if (back.LengthSquared() < s.Size * s.Size) back = Vector3.Up * s.Size;
				FxRibbon.Dart(_sparkMesh, s.Pos + back * 0.3f, s.Pos, s.Pos - back, eye, s.Size * (0.55f - 0.2f * t), tint, alpha);
			}
			_sparkMesh.SurfaceEnd();
		}

		_dust.Visible = _puffs.Count > 0;
		_dustMesh.VisibleInstanceCount = _puffs.Count;
		if (_puffs.Count == 0) return;
		_dust.GlobalTransform = Transform3D.Identity;
		// Cor de poeira, com um fio da cor do lobo: ela está acesa por ele.
		var dustColor = new Color(0.62f, 0.6f, 0.56f).Lerp(Tint, 0.16f);
		for (int i = 0; i < _puffs.Count; i++)
		{
			Puff p = _puffs[i];
			float t = p.Age / p.Life;
			float size = Mathf.Lerp(p.From, p.To, 1f - (1f - t) * (1f - t));
			// O tufo senta no chão: o centro a meio tufo de altura, para a beira
			// (já transparente) encostar nele. Enterrado, o chão o cortava reto.
			_dustMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), p.Pos + Vector3.Up * (size * 0.42f)));
			_dustMesh.SetInstanceColor(i, new Color(dustColor, DustAlpha * Mathf.Min(1f, t * 8f) * (1f - t)));
		}
	}

	private void SleepSparks()
	{
		_sparkMesh.ClearSurfaces();
		_sparkCanvas.Visible = false;
		_dust.Visible = false;
		_dustMesh.VisibleInstanceCount = 0;
	}
}
