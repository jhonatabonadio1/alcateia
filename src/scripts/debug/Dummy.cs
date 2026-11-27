using Godot;
using System.Collections.Generic;

/// <summary>Como o boneco de treino se move.</summary>
public enum DummyMove
{
	/// <summary>Parado.</summary>
	Still,
	/// <summary>Vai e volta em linha reta, de onde está até <see cref="Dummy.MoveTo"/>.</summary>
	Line,
	/// <summary>Dá voltas em torno de onde está, a <see cref="Dummy.MoveRadius"/> metros.</summary>
	Circle,
	/// <summary>Segue o percurso <see cref="Dummy.Route"/>, ponto a ponto.</summary>
	Route,
}

/// <summary>
/// Boneco de treino: o inimigo em que se atira. **É um inimigo de verdade,
/// menos a arma** (decisão do Jhon, 26/11: "os robôs são inimigos, não
/// atiram, mas seguem sempre um percurso, tomam dano igual, morrem,
/// regeneram, exibe a vida deles"): é do outro time (o tiro, a garra e a
/// granada ferem como ferem um jogador, e o abate entra no feed), a vida é
/// a de qualquer corpo (<see cref="Health"/>: volta sozinha depois de um
/// tempo sem levar dano), a cabeça vale tiro na cabeça
/// (<see cref="IHeadBand"/>), morre, some e volta uns segundos depois — e
/// mostra a vida numa barra por cima dele.
///
/// Sem <see cref="Model"/> é a caixa cinza de sempre (o campo de treino
/// antigo, `treino.tscn`). Com ele, é o robô de treino do Setor Zero
/// (`src/assets/mapas/pecas/robo_treino.glb`, feito por
/// `src/tools/mapas/robo_treino.py`): o desenho paira e balança, olha para
/// onde anda (e para o jogador, quando ele chega perto), e o corpo segue o
/// percurso dele (<see cref="Move"/>). A colisão continua a caixa de
/// 1 × 2 × 1 m, em pé, sem girar: o que leva tiro é ela.
///
/// O movimento é conta do relógio da física (o mesmo caminho a cada vez),
/// sem sorteio, e o relógio não para quando o robô cai: ele volta onde
/// estaria, junto dos que andam em grupo com ele. Quem decide onde cada
/// robô fica e por onde anda é o mapa (src/tools/mapas/setor_zero.py:
/// ALVOS).
///
/// Era descartável ("quando o bot ficar pronto, este nó sai"); ficou: o
/// tutorial precisa de um alvo que não revida.
/// </summary>
public partial class Dummy : StaticBody3D, IHeadBand
{
	[Export] public Color BaseColor = new Color(0.75f, 0.75f, 0.78f);
	[Export] public Color HitColor = new Color(0.95f, 0.2f, 0.15f);

	/// <summary>Duração do flash vermelho, em segundos.</summary>
	[Export] public float FlashDuration = 0.12f;

	[ExportGroup("Inimigo")]
	/// <summary>O time do boneco: o outro (o jogador sozinho é da Vanguarda). Com o time do jogador, o tiro passa sem
	/// ferir — era o defeito do boneco antigo.</summary>
	[Export] public int Team = Teams.Orange;

	/// <summary>O nome no feed de abates.</summary>
	[Export] public string DisplayName = "Robô de treino";

	/// <summary>Segundos caído até voltar.</summary>
	[Export] public float RespawnDelay = 4f;

	/// <summary>Segundos sem levar dano ao voltar (curto: é um alvo).</summary>
	[Export] public float SpawnProtection = 0.6f;

	/// <summary>Segundos sem levar dano até a vida começar a voltar (o jogador espera 15; o alvo, menos).</summary>
	[Export] public float RegenDelay = 6f;

	/// <summary>Onde a cabeça do robô começa, em metros acima do meio do corpo (a cunha da cabeça vai de 1,40 a
	/// 1,79 m do chão; o meio do corpo está a 1 m).</summary>
	[Export] public float RobotHeadFrom = 0.4f;

	/// <summary>Sem modelo (a caixa cinza), a cabeça é a faixa de cima, como na cápsula do humano.</summary>
	[Export] public float BoxHeadFrom = 0.72f;

	public float HeadFrom => Model != null ? RobotHeadFrom : BoxHeadFrom;

	[ExportGroup("Barra de vida")]
	/// <summary>Largura e altura da barra, em metros, e a que altura fica (acima do meio do corpo).</summary>
	[Export] public Vector2 BarSize = new(0.9f, 0.1f);
	[Export] public float BarHeight = 1.28f;

	/// <summary>A barra cresce com a distância (para se ler de longe) até tantas vezes, e some de
	/// <see cref="BarFadeFrom"/> a <see cref="BarFadeTo"/> metros.</summary>
	[Export] public float BarGrowEvery = 9f;
	[Export] public float BarGrowMax = 3f;
	[Export] public float BarFadeFrom = 55f;
	[Export] public float BarFadeTo = 70f;

	/// <summary>O rastro do dano espera tanto (s) e encolhe a tanto da barra por segundo.</summary>
	[Export] public float BarTrailWait = 0.35f;
	[Export] public float BarTrailSpeed = 0.9f;

	[ExportGroup("Robô")]
	/// <summary>O desenho do robô (uma cena). Vazio: a caixa cinza.</summary>
	[Export] public PackedScene Model;

	/// <summary>Quanto o desenho sobe e desce ao pairar, em metros, e em quantos segundos dá uma volta.</summary>
	[Export] public float HoverBob = 0.06f;
	[Export] public float HoverPeriod = 2.6f;

	/// <summary>Vira-se para o jogador que estiver a até tantos metros (0: nunca); mais longe, olha para onde anda.</summary>
	[Export] public float FaceWithin = 12f;

	/// <summary>Graus por segundo que o desenho gira para encarar.</summary>
	[Export] public float TurnSpeed = 240f;

	/// <summary>Graus por segundo que o anel de energia do robô gira (as peças de nome "…_f_gira" do modelo).</summary>
	[Export] public float RingSpeed = 70f;

	[ExportGroup("Movimento")]
	[Export] public DummyMove Move = DummyMove.Still;

	/// <summary>Para <see cref="DummyMove.Line"/>: até onde vai, a partir de onde está (metros, no mundo).</summary>
	[Export] public Vector3 MoveTo = Vector3.Zero;

	/// <summary>Para <see cref="DummyMove.Circle"/>: o raio da volta.</summary>
	[Export] public float MoveRadius = 3f;

	/// <summary>Para <see cref="DummyMove.Route"/>: os pontos do percurso, a partir de onde está (o primeiro ponto
	/// é o próprio lugar, e não se escreve).</summary>
	[Export] public Vector3[] Route = System.Array.Empty<Vector3>();

	/// <summary>O percurso fecha (do último ponto volta ao começo, e roda sempre no mesmo sentido). Desligado: vai
	/// até o fim, espera e volta pelo mesmo caminho.</summary>
	[Export] public bool RouteLoop;

	/// <summary>Metros por segundo.</summary>
	[Export] public float MoveSpeed = 2f;

	/// <summary>Quanto espera em cada ponta (linha, ou percurso que não fecha), em segundos.</summary>
	[Export] public float MovePause = 0.7f;

	private const string BarShader = "res://src/assets/mapas/detalhe/barra_vida.gdshader";

	private Health _health;
	private MeshInstance3D _mesh;
	private CollisionShape3D _shape;
	private StandardMaterial3D _material;
	private StandardMaterial3D _flash;
	private Node3D _visual;
	private readonly List<GeometryInstance3D> _parts = new();
	private readonly List<Node3D> _spinning = new();
	private double _flashTimer;
	private Vector3 _home;
	private double _clock;
	private float _bob;
	private MeshInstance3D _bar;
	private ShaderMaterial _barLook;
	private Label3D _barText;
	private float _trail = 1f;
	private double _trailWait;

	/// <summary>Onde o corpo nasceu: o começo do percurso.</summary>
	public Vector3 Home => _home;

	/// <summary>O corpo está à vista (vivo).</summary>
	public bool Shown => _visual != null ? _visual.Visible : _mesh.Visible;

	/// <summary>A barra de vida está à vista, e quanto ela marca (0 a 1).</summary>
	public bool BarShown => _bar != null && _bar.Visible;
	public float BarValue => _barLook != null ? _barLook.GetShaderParameter("vida").AsSingle() : 0f;

	public override void _Ready()
	{
		_health = GetNode<Health>("Health");
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_shape = GetNode<CollisionShape3D>("CollisionShape3D");
		_home = GlobalPosition;

		_health.Team = Team;
		_health.DisplayName = Loc.T(DisplayName);
		_health.TrainingRespawnDelay = RespawnDelay;
		_health.SpawnProtection = SpawnProtection;
		_health.RegenDelay = RegenDelay;

		// Material próprio por instância, senão os três bonecos
		// compartilham o mesmo e piscam juntos.
		_material = new StandardMaterial3D { AlbedoColor = BaseColor };
		_mesh.MaterialOverride = _material;

		if (Model != null)
		{
			// O robô: o desenho pendurado no corpo, com os pés no pé da caixa.
			_visual = new Node3D { Name = "Visual", Position = new Vector3(0, -1f, 0) };
			AddChild(_visual);
			_visual.AddChild(Model.Instantiate());
			foreach (Node node in _visual.FindChildren("*", nameof(GeometryInstance3D), true, false))
			{
				_parts.Add((GeometryInstance3D)node);
				if (node.Name.ToString().Contains("_f_gira")) _spinning.Add((Node3D)node);
			}
			_mesh.Visible = false;
			// É um personagem: entra na luz dos personagens e faz a sombra de apoio (GroundingShadow).
			ViewModel.MarkCharacter(_visual);
			// O piscar do robô: uma película vermelha por cima de todas as peças.
			_flash = new StandardMaterial3D
			{
				AlbedoColor = new Color(HitColor, 0.55f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			};
		}

		BuildBar();
		_health.Changed += OnHealthChanged;
		_health.Died += OnDied;
		ShowHealth(false);
	}

	/// <summary>A barra de vida e o número, por cima do corpo.</summary>
	private void BuildBar()
	{
		if (!ResourceLoader.Exists(BarShader)) return;

		_barLook = new ShaderMaterial { Shader = GD.Load<Shader>(BarShader) };
		_barLook.SetShaderParameter("proporcao", BarSize.X / Mathf.Max(BarSize.Y, 0.001f));
		_bar = new MeshInstance3D
		{
			Name = "Vida",
			Mesh = new QuadMesh { Size = BarSize },
			MaterialOverride = _barLook,
			Position = new Vector3(0, BarHeight, 0),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(_bar);

		_barText = new Label3D
		{
			Name = "Numero",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			PixelSize = 0.0022f,
			FontSize = 64,
			OutlineSize = 14,
			Modulate = Colors.White,
			OutlineModulate = new Color(0.02f, 0.03f, 0.05f, 0.9f),
			Position = new Vector3(0, BarSize.Y * 1.9f, 0),
			Shaded = false,
			DoubleSided = true,
		};
		_bar.AddChild(_barText);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Move == DummyMove.Still) return;

		// O relógio não para com o robô caído: ele volta onde estaria (os que andam em grupo continuam juntos).
		_clock += delta;
		GlobalPosition = _home + Offset(_clock);
	}

	/// <summary>Quanto dura uma volta inteira do movimento, em segundos (0: parado).</summary>
	public double Cycle
	{
		get
		{
			if (MoveSpeed <= 0f) return 0;
			switch (Move)
			{
				case DummyMove.Line:
					return MoveTo.Length() < 0.01f ? 0 : 2 * (MoveTo.Length() / MoveSpeed + MovePause);
				case DummyMove.Circle:
					return MoveRadius < 0.01f ? 0 : Mathf.Tau * MoveRadius / MoveSpeed;
				case DummyMove.Route:
				{
					float length = RouteLength();
					if (length < 0.01f) return 0;
					return RouteLoop ? length / MoveSpeed : 2 * (length / MoveSpeed + MovePause);
				}
				default:
					return 0;
			}
		}
	}

	/// <summary>Onde o corpo está, a partir de onde nasceu, no instante <paramref name="time"/>.</summary>
	public Vector3 Offset(double time)
	{
		if (MoveSpeed <= 0f) return Vector3.Zero;
		switch (Move)
		{
			case DummyMove.Line:
			{
				float length = MoveTo.Length();
				if (length < 0.01f) return Vector3.Zero;
				return MoveTo * BackAndForth(time, length);
			}
			case DummyMove.Circle:
			{
				if (MoveRadius < 0.01f) return Vector3.Zero;
				float angle = (float)(time * MoveSpeed / MoveRadius);
				return new Vector3(Mathf.Cos(angle) - 1f, 0, Mathf.Sin(angle)) * MoveRadius;
			}
			case DummyMove.Route:
			{
				float length = RouteLength();
				if (length < 0.01f) return Vector3.Zero;
				float along = RouteLoop ? (float)(time * MoveSpeed % length) : BackAndForth(time, length) * length;
				return AlongRoute(along);
			}
			default:
				return Vector3.Zero;
		}
	}

	/// <summary>Quanto do caminho de <paramref name="length"/> metros já foi (0 a 1), indo, esperando, voltando e
	/// esperando — saindo e chegando devagar (o robô freia nas pontas).</summary>
	private float BackAndForth(double time, float length)
	{
		double leg = length / MoveSpeed, cycle = 2 * (leg + MovePause);
		double t = time % cycle;
		float along = t < leg ? (float)(t / leg)
			: t < leg + MovePause ? 1f
			: t < 2 * leg + MovePause ? 1f - (float)((t - leg - MovePause) / leg)
			: 0f;
		return along * along * (3f - 2f * along);
	}

	/// <summary>O comprimento do percurso (fechado: com a volta ao começo).</summary>
	private float RouteLength()
	{
		float length = 0f;
		Vector3 last = Vector3.Zero;
		foreach (Vector3 point in Route)
		{
			length += last.DistanceTo(point);
			last = point;
		}
		return RouteLoop ? length + last.Length() : length;
	}

	/// <summary>O ponto a <paramref name="along"/> metros do começo do percurso.</summary>
	private Vector3 AlongRoute(float along)
	{
		Vector3 last = Vector3.Zero;
		int count = Route.Length + (RouteLoop ? 1 : 0);
		for (int i = 0; i < count; i++)
		{
			Vector3 next = i < Route.Length ? Route[i] : Vector3.Zero;
			float step = last.DistanceTo(next);
			if (along <= step) return step < 0.0001f ? next : last.Lerp(next, along / step);
			along -= step;
			last = next;
		}
		return last;
	}

	public override void _Process(double delta)
	{
		if (_visual != null && _visual.Visible)
		{
			// Paira: sobe e desce devagar (só o desenho; a caixa não sai do lugar).
			_bob += (float)delta;
			_visual.Position = new Vector3(0, -1f + HoverBob * (1f + Mathf.Sin(_bob * Mathf.Tau / Mathf.Max(HoverPeriod, 0.1f))), 0);
			Face((float)delta);
			foreach (Node3D ring in _spinning)
				ring.RotateY(Mathf.DegToRad(RingSpeed) * (float)delta);
		}

		TickBar(delta);

		if (_flashTimer <= 0) return;

		_flashTimer -= delta;
		if (_flashTimer <= 0) SetFlash(false);
	}

	/// <summary>A barra: o rastro do dano encolhe, e ela cresce e some com a distância de quem olha.</summary>
	private void TickBar(double delta)
	{
		if (_bar == null || !_bar.Visible) return;

		float value = _health.MaxHp > 0 ? Mathf.Clamp(_health.Hp / _health.MaxHp, 0f, 1f) : 0f;
		if (_trail > value)
		{
			_trailWait -= delta;
			if (_trailWait <= 0) _trail = Mathf.Max(value, _trail - BarTrailSpeed * (float)delta);
		}
		else _trail = value;
		_barLook.SetShaderParameter("vida", value);
		_barLook.SetShaderParameter("rastro", _trail);

		if (GetViewport()?.GetCamera3D() is not Camera3D camera) return;
		float distance = camera.GlobalPosition.DistanceTo(_bar.GlobalPosition);
		_bar.Scale = Vector3.One * Mathf.Clamp(distance / Mathf.Max(BarGrowEvery, 0.1f), 1f, BarGrowMax);
		float show = 1f - Mathf.Clamp((distance - BarFadeFrom) / Mathf.Max(BarFadeTo - BarFadeFrom, 0.1f), 0f, 1f);
		_barLook.SetShaderParameter("mostra", show);
		_barText.Modulate = new Color(1f, 1f, 1f, show);
		_barText.OutlineModulate = new Color(0.02f, 0.03f, 0.05f, 0.9f * show);
	}

	/// <summary>Vira o desenho para o jogador, se estiver perto; se não, para onde anda.</summary>
	private void Face(float delta)
	{
		Vector3? toward = null;
		if (FaceWithin > 0 && Groups.LocalPlayer(GetTree()) is Node3D player)
		{
			Vector3 to = player.GlobalPosition - GlobalPosition;
			to.Y = 0;
			if (to.Length() is > 0.3f and var distance && distance <= FaceWithin) toward = to;
		}
		if (toward == null && Move != DummyMove.Still)
		{
			// Para onde anda agora (parado numa ponta, fica como estava).
			Vector3 ahead = Offset(_clock + 0.1) - Offset(_clock);
			ahead.Y = 0;
			if (ahead.LengthSquared() > 0.0004f) toward = ahead;
		}
		if (toward is not Vector3 dir || dir.LengthSquared() < 0.0001f) return;

		// A frente do robô é +Z.
		float want = Mathf.Atan2(dir.X, dir.Z);
		float step = Mathf.DegToRad(TurnSpeed) * delta;
		float now = _visual.Rotation.Y;
		_visual.Rotation = new Vector3(0, now + Mathf.Clamp(Mathf.AngleDifference(now, want), -step, step), 0);
	}

	private void OnHealthChanged(float hp, float maxHp, float shield)
	{
		ShowHealth(true);
		if (_health.IsDead)
		{
			ShowBody(false);
			return;
		}

		// Voltou do respawn.
		if (!Shown)
		{
			ShowBody(true);
			return;
		}

		SetFlash(true);
		_flashTimer = FlashDuration;
	}

	/// <summary>Põe o número na barra; <paramref name="hit"/>: a vida mudou agora (o rastro espera antes de
	/// encolher).</summary>
	private void ShowHealth(bool hit)
	{
		if (_barText == null) return;
		_barText.Text = Mathf.CeilToInt(_health.Hp).ToString();
		float value = _health.MaxHp > 0 ? Mathf.Clamp(_health.Hp / _health.MaxHp, 0f, 1f) : 0f;
		if (hit && value < _trail) _trailWait = BarTrailWait;
		if (!hit) _trail = value;
		_barLook.SetShaderParameter("vida", value);
	}

	private void OnDied(int killerId)
	{
		ShowBody(false);
	}

	private void SetFlash(bool on)
	{
		if (_visual == null)
		{
			_material.AlbedoColor = on ? HitColor : BaseColor;
			return;
		}
		foreach (GeometryInstance3D part in _parts)
			part.MaterialOverlay = on ? _flash : null;
	}

	/// <summary>Nome próprio em vez de SetVisible: Node3D já tem um
	/// método com esse nome, e sobrepor por acidente é a origem de bug
	/// difícil de achar.</summary>
	private void ShowBody(bool visible)
	{
		if (_visual != null) _visual.Visible = visible;
		else _mesh.Visible = visible;
		if (_bar != null) _bar.Visible = visible;
		_shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, !visible);

		if (visible)
		{
			SetFlash(false);
			_flashTimer = 0;
			_trail = 1f;
		}
	}
}
