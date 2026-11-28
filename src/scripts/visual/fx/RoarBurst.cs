using Godot;
using System.Collections.Generic;

/// <summary>
/// O rugido que se vê. Refeito em 27/11 (pedido do Jhon: "refaça o rugido do
/// lobo, coloque um fx e vfx mais profissional"), em quatro tempos, cada um
/// com a sua leitura:
///
/// 1. **Antecipação** (<see cref="IntakeTime"/>): faíscas correm do chão em
///    volta para dentro dele — o lobo puxa a carga antes de soltar.
/// 2. **Pico** (<see cref="Blast"/>): o estouro. Uma onda abre pelo chão
///    entortando a imagem, a poeira levanta em roda, o sopro sai cheio da
///    boca e a luz dá o clarão.
/// 3. **Sustentação**: o chão racha em veios de luz que avançam com a carga,
///    arcos saltam do chão para o corpo cada vez mais juntos, faíscas sobem
///    girando, o ar corre da boca e — o que conta a regra — **a casca do
///    escudo se fecha de baixo para cima** no ritmo do rugido. Quem solta no
///    meio vê a casca se desfazer aberta.
/// 4. **Escudo cheio** (<see cref="Seal"/>) e dissipação: a casca fecha e
///    acende inteira, uma faixa varre dos pés à cabeça, a descarga sai para
///    todos os lados e corre pelo chão, a bolha de pressão empurra o ar e a
///    poeira. Depois o chão esfria e as faíscas caem.
///
/// As decisões de antes continuam valendo:
///
/// - **Nada de anel na frente da boca** (Jhon, 18/11: "pode tirar somente o
///   círculo na frente, deixe só os arzinhos saindo da boca"): da boca só sai
///   ar, em fios ao comprido.
/// - **É recarga, não ataque** (18/11: "combinaria mais com aumentar
///   escudo"), e **é eletricidade** ("o core do lobo é eletricidade, não
///   brilho brilho").
/// - **A luz é BAIXA** ("não tão alto para não entregar posicionamento de
///   lobos escondidos"): nada passa de <see cref="ShaftTop"/> acima dos pés —
///   os raios, a casca, a bolha e as faíscas —, e tudo some atrás de parede.
/// - A onda do chão existe (saiu em 18/11 e voltou em 19/11).
///
/// As partes: este arquivo guarda o ciclo e o estado; <c>.Layers</c> monta as
/// camadas de shader (chão, onda, casca, bolha, sopro) e as acerta por quadro;
/// <c>.Sparks</c> são as faíscas e a poeira; <c>.Ribbons</c>, as fitas (arcos,
/// raios de luz, riscos de ar).
///
/// Fica no corpo. Quem diz de onde sai (a boca), para onde e se há chão
/// embaixo é o <see cref="WolfFx"/>.
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

	/// <summary>O lobo está no chão. No ar não há chão para rachar nem
	/// poeira para levantar: só o corpo acende.</summary>
	public bool Grounded { get; set; } = true;

	/// <summary>Quanto do efeito aparece, pela qualidade das opções (0 a 3):
	/// na Baixa, só luz e fitas; da Média para cima, o sopro e a poeira; da
	/// Alta para cima, a distorção (que lê a tela).</summary>
	public int Detail { get; set; } = 2;

	/// <summary>Ondas vivas no chão agora. Para os testes.</summary>
	public int Rings => _waves.Count;

	/// <summary>A onda do chão em volta do lobo.</summary>
	public static bool SpawnGroundWaves = true;

	/// <summary>Altura da luz, a partir dos pés. Baixa: não entrega o lobo
	/// atrás de uma parede. É o teto de tudo o que o rugido desenha.</summary>
	public const float ShaftTop = 2.35f;

	/// <summary>Quanto a luz está acesa agora, de 0 a 1. Para os testes.</summary>
	public float Glow => _glow;

	/// <summary>O escudo cheio está fechando (a faixa sobe). Para os testes.</summary>
	public bool Sweeping => _sweep >= 0f;

	/// <summary>O clarão do escudo cheio, de 1 (agora) a 0.</summary>
	public float Flash => _sweep >= 0f ? 1f - _sweep / SweepLife : 0f;

	/// <summary>Arcos elétricos acesos agora. Para os testes.</summary>
	public int Arcs => _arcs.Count;

	/// <summary>Quanto da casca do escudo já fechou, de 0 (pés) a 1 (cabeça).</summary>
	public float ShieldLevel => _level;

	/// <summary>O coice do rugido para a câmera do dono, de 1 (agora) a 0: o
	/// estouro do começo vale <see cref="BlastKick"/>, o escudo cheio vale 1.</summary>
	public float Kick => Mathf.Max(_blast >= 0f ? BlastKick * (1f - _blast / BlastLife) : 0f, Flash);

	// ------------------------------------------------------------------ tempos

	/// <summary>A antecipação: do R até o estouro. Curta — a voz já começou.</summary>
	private const float IntakeTime = 0.12f;

	/// <summary>Quanto dura o estouro do começo (a bolha, o clarão, o sopro cheio).</summary>
	private const float BlastLife = 0.4f;

	/// <summary>O coice do estouro do começo, em fração do do escudo cheio.</summary>
	private const float BlastKick = 0.45f;

	/// <summary>Quanto dura o fecho do escudo (a faixa subindo e o clarão).</summary>
	private const float SweepLife = 0.5f;

	/// <summary>A fração do rugido a partir da qual ele vale como inteiro (o
	/// escudo enche): a mesma folga de antes.</summary>
	private const float FullAt = 0.85f;

	/// <summary>De quanto em quanto tempo sai uma onda pelo chão na sustentação.</summary>
	private const float GroundEvery = 0.5f;

	/// <summary>O tremor da luz acelera com a carga, de tantas a tantas vezes
	/// por segundo — o mesmo passo do tremor do som (<c>tools/sons/uivo.py</c>).</summary>
	private const float FlickerFrom = 7f, FlickerTo = 20f;

	private float _glow, _held, _level, _shell, _amount, _ground, _charge;
	private float _blast = -1f, _sweep = -1f;
	private bool _wasActive, _blasted;
	private double _time, _groundClock, _flicker, _arcClock, _phase;
	private uint _seed;
	private int _warm = 3;

	private struct Arc { public Vector3 From, To; public float Age, Life, Width; public ulong Seed; }
	private readonly List<Arc> _arcs = new();
	private const int ArcMax = 6;

	/// <summary>A descarga do escudo cheio: quantos arcos saem do corpo e quantos correm pelo chão.</summary>
	private const int SealArcs = 7, SealCrawls = 4;

	/// <summary>Uma onda abrindo pelo chão: até onde vai, quão forte e larga.</summary>
	private struct Wave { public float Age, Life, Reach, Strength, Width; }
	private readonly List<Wave> _waves = new();

	private readonly RandomNumberGenerator _rng = new();
	private OmniLight3D _light;

	public override void _Ready()
	{
		_rng.Randomize();
		BuildRibbons();
		BuildLayers();
		BuildSparks();
		_light = new OmniLight3D { LightEnergy = 0f, OmniRange = 4.2f, ShadowEnabled = false, Visible = false };
		AddChild(_light);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += delta;
		float full = WolfMoves.Duration(WolfMove.Howl) * FullAt;

		if (Active && !_wasActive) Begin();
		if (Active)
		{
			_held += dt;
			if (!_blasted && _held >= IntakeTime) Blast();
			_level = Mathf.Clamp(_held / full, 0f, 1f);
		}
		else if (_wasActive)
		{
			// Rugiu até o fim: o escudo encheu. Soltou antes: a casca se desfaz aberta.
			if (_held >= full) Seal();
			_held = 0f;
		}
		_wasActive = Active;

		_amount = Mathf.MoveToward(_amount, Active ? 1f : 0f, dt / (Active ? 0.1f : 0.25f));
		// A luz acende devagar, junto com o escudo que está por vir.
		_glow = Mathf.MoveToward(_glow, Active ? 1f : 0f, dt / (Active ? 0.6f : 0.4f));
		// A casca aparece logo e some depressa se o rugido foi cortado; no escudo
		// cheio ela fica até a faixa terminar de subir.
		bool shellOn = Active || _sweep >= 0f;
		_shell = Mathf.MoveToward(_shell, shellOn ? 1f : 0f, dt / (shellOn ? 0.25f : 0.22f));
		// O chão esfria mais devagar que o resto: é a dissipação.
		_ground = Mathf.MoveToward(_ground, Active ? 1f : 0f, dt / (Active ? 0.3f : 0.75f));
		if (Active) _charge = Mathf.Max(_charge, Mathf.SmoothStep(0f, 1f, _level));
		else if (_ground <= 0.001f) _charge = 0f;
		// O tremor da luz e dos arcos anda no passo do som.
		_phase += delta * Mathf.Tau * Mathf.Lerp(FlickerFrom, FlickerTo, _level);

		if (_blast >= 0f && (_blast += dt) >= BlastLife) _blast = -1f;
		if (_sweep >= 0f && (_sweep += dt) >= SweepLife) _sweep = -1f;

		TickArcs(dt);
		TickWaves(dt);
		TickSparks(dt);

		if (Idle)
		{
			if (_warm > 0) { _warm--; PushLayers(warm: true); }
			else Sleep();
			return;
		}

		_awake = true;
		_flicker -= delta;
		if (_flicker <= 0)
		{
			_flicker = 0.045;
			_seed = _rng.Randi();
		}

		PushLight();
		PushLayers(warm: false);
		DrawRibbons();
		DrawSparks();
	}

	/// <summary>Nada aceso: as camadas dormem (não custam desenho nem conta).</summary>
	private bool Idle => _waves.Count == 0 && _arcs.Count == 0 && _sparks.Count == 0 && _puffs.Count == 0
		&& _amount <= 0.001f && _glow <= 0.001f && _shell <= 0.001f && _ground <= 0.001f && _blast < 0f && _sweep < 0f;

	// ------------------------------------------------------------------ os quatro tempos

	/// <summary>Antecipação: a carga corre do chão em volta para dentro dele.</summary>
	private void Begin()
	{
		_held = 0f;
		_blasted = false;
		_level = 0f;
		_charge = 0f;
		_groundClock = GroundEvery;
		_phase = 0;
		_layerSeed = _rng.Randf() * 50f;
		SpawnIntake();
	}

	/// <summary>Pico: o estouro do rugido.</summary>
	private void Blast()
	{
		_blasted = true;
		_blast = 0f;
		if (Grounded && SpawnGroundWaves)
			_waves.Add(new Wave { Life = 0.55f, Reach = 3.2f, Strength = 1f, Width = 0.95f });
		SpawnMouthSparks(10);
		if (Grounded) SpawnDust(9, 2.6f, 4.2f);
	}

	/// <summary>Escudo cheio: a casca fecha, a descarga sai.</summary>
	private void Seal()
	{
		_sweep = 0f;
		_level = 1f;
		if (Grounded && SpawnGroundWaves)
		{
			_waves.Add(new Wave { Life = 0.6f, Reach = 3.4f, Strength = 1.3f, Width = 1.1f });
			// A segunda vem atrás, mais lenta: o chão ainda treme.
			_waves.Add(new Wave { Life = 0.95f, Reach = 2.4f, Strength = 0.6f, Width = 0.7f });
		}
		for (int i = 0; i < SealArcs; i++) AddArc(ArcKind.Burst);
		if (Grounded) for (int i = 0; i < SealCrawls; i++) AddArc(ArcKind.Crawl);
		SpawnBurst(30);
		if (Grounded) SpawnDust(12, 3.2f, 5.2f);
	}

	// ------------------------------------------------------------------ ondas e luz

	private void TickWaves(float dt)
	{
		// A onda do chão da sustentação: fraca, de meio em meio segundo.
		if (Active && _blasted && Grounded && SpawnGroundWaves)
		{
			_groundClock -= dt;
			if (_groundClock <= 0)
			{
				_groundClock = GroundEvery;
				_waves.Add(new Wave { Life = 0.6f, Reach = 2.9f, Strength = 0.5f, Width = 0.6f });
			}
		}

		for (int i = _waves.Count - 1; i >= 0; i--)
		{
			Wave w = _waves[i];
			w.Age += dt;
			if (w.Age >= w.Life) _waves.RemoveAt(i);
			else _waves[i] = w;
		}
	}

	/// <summary>Uma luz só, de cima e baixa: clareia a cabeça e os ombros.
	/// Treme no passo do som, dá um clarão no estouro e outro, maior, no
	/// escudo cheio.</summary>
	private void PushLight()
	{
		float tremble = 0.5f + 0.5f * Mathf.Sin((float)_phase);
		float blast = _blast >= 0f ? Mathf.Pow(1f - _blast / BlastLife, 2f) : 0f;
		float energy = _glow * (0.85f + 0.4f * tremble) + 1.5f * blast + 2.6f * Flash * Flash;
		_light.Visible = energy > 0.01f;
		_light.Position = new Vector3(0, Feet + 2.1f, 0) + Forward * 0.25f;
		_light.LightColor = Tint.Lerp(Colors.White, 0.45f);
		_light.LightEnergy = energy;
	}

	private void Sleep()
	{
		if (!_awake) return;
		_awake = false;
		_light.Visible = false;
		_light.LightEnergy = 0f;
		SleepLayers();
		SleepRibbons();
		SleepSparks();
	}

	private bool _awake = true;
}
