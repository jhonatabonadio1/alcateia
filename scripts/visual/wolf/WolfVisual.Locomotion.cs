using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// A locomoção por quadro: parado, andando e correndo pela velocidade
/// (o clipe na passada do corpo), virar para onde anda, o agachado que
/// vira engatinhar e a corrida de quatro.
/// </summary>
public partial class WolfVisual
{
	/// <summary>Até que velocidade (m/s) o corpo só anda, e a partir de qual
	/// só corre. Entre as duas, andar e correr se misturam — e nenhuma
	/// velocidade do jogo fica nessa faixa, só se passa por ela: os dois
	/// clipes têm ciclos de tamanhos diferentes (0,67 s e 0,87 s), e
	/// misturados o pé de um puxa o do outro e patina. Agachado (1,7 a
	/// 1,85 m/s) o lobo só anda.</summary>
	[ExportGroup("Locomoção")]
	[Export] public float WalkSpeed = 2.0f;
	[Export] public float RunSpeed = 2.6f;

	/// <summary>Abaixo disto o corpo conta como parado para pousar do
	/// pulo e para o clipe de pés juntos entrar, em m/s.</summary>
	private const float MovingSpeed = 1.5f;

	/// <summary>Quanto tempo o corpo leva para sair do parado para o
	/// andar e para voltar, em segundos. Na volta, o passo congela onde o
	/// corpo parou (o clipe toca na velocidade do corpo, que já é zero) e
	/// as pernas se juntam nesse tempo — sem correr no lugar.</summary>
	[Export] public float StartBlend = 0.12f;
	[Export] public float StopBlend = 0.22f;

	/// <summary>Quão rápido o corpo vira para o lado em que anda, em
	/// graus por segundo. O lobo não tem clipe de andar de lado nem de
	/// costas: de lado, ele vira e anda de frente para onde vai; para
	/// trás, fica de frente e o passo toca ao contrário. Assim o pé de
	/// apoio fica parado no chão em qualquer direção.</summary>
	[Export] public float TurnRate = 540f;

	/// <summary>O trecho do "jump attack" que é a pose agachada, em segundos
	/// do arquivo: o fim da aterrissagem, os dois pés no chão e o corpo
	/// encolhido (0,95 a 1,0 m de altura). Tocado indo e voltando, respira.</summary>
	private const float CrouchFrom = 1.95f;
	private const float CrouchTo = 2.25f;

	/// <summary>Quanto o corpo sobe engatinhando, em metros: no "Running
	/// Crawl" as mãos descem 13 cm abaixo dos pés, e sem isto entrariam no
	/// chão.</summary>
	[Export] public float CrawlLift = 0.1f;

	/// <summary>Atacando, carregando o salto, no bote ou na parede o corpo
	/// volta a olhar para a mira — o golpe sai para onde se mira — nesta
	/// velocidade, em graus por segundo.</summary>
	[Export] public float AimTurnRate = 1400f;

	/// <summary>A velocidade em que o pé de cada clipe não patina, em m/s:
	/// a passada medida no Mutant (andar 1,22 m/s, correr 2,20 m/s) vezes a
	/// escala do modelo. O clipe toca na razão entre a velocidade do corpo
	/// e esta — o Bruto a 3,6 m/s corre o clipe a 1,5×. Antes ele tocava a
	/// 1× até 7 m/s, e o corpo andava mais do dobro do que as pernas. O
	/// limite do que fica natural é a passada: correr além de ~1,6× fica
	/// corrido, e por isso o lobo anda a 3,6 m/s (decisão do Jhon).</summary>
	///
	/// Com os andares do Jhon (15/11 à noite: Walk_Forward é um trote curvado de
	/// 0,67 s por ciclo, feito na velocidade do jogo) a de andar para a
	/// frente é a do pé dele: o pé de apoio anda 3,50 m/s no arquivo, vezes
	/// 1,13 (a razão entre o corpo do jogo e o do arquivo, tirada da corrida
	/// de quatro: 3,50 no arquivo, 3,95 varrida aqui). O Bruto (3,89 m/s)
	/// toca o clipe a ~1× e o Ligeiro (4,22) a ~1,07×; antes o andar de
	/// 1,43 s tocava a ~3×. Varrida no teste de patinação a 4,15 m/s e a
	/// conta fechou: 0,26 m/s de patinação em 3,75, 0,21 em 3,85, o mínimo
	/// (0,13) em 3,95, 0,17 em 4,0, 0,28 em 4,15. RunClipSpeed é a do
	/// "mutant run", só sem os andares dele.</summary>
	[Export] public float WalkClipSpeed = 3.95f;
	[Export] public float RunClipSpeed = 2.42f;

	/// <summary>A passada do andar para trás (Walk_Backward) e de lado
	/// (Walk_Left e Walk_Right), em m/s: o mesmo que
	/// <see cref="WalkClipSpeed"/>, na direção de cada um. Os três saem do
	/// trote de frente (mesmo ciclo de 0,67 s, mesmo pé na mesma hora): para
	/// trás o pé de apoio anda 3,20 m/s no arquivo e de lado 3,30, vezes
	/// 1,13. Tocam entre 1,04× e 1,17× (de lado o antigo tocava a 5,5×).
	/// Varridas a 4,15 m/s: para trás 0,37 m/s de patinação em 3,45, o
	/// mínimo (0,22) em 3,6, 0,31 em 3,72; de lado 0,31 em 3,5, o mínimo
	/// (0,14) em 3,75, 0,24 em 3,9.</summary>
	[Export] public float WalkBackClipSpeed = 3.6f;
	[Export] public float WalkSideClipSpeed = 3.75f;

	/// <summary>A passada do andar agachado, em m/s (o Crouch_Walk do
	/// Jhon, 1,0 s por ciclo desde 15/11 à noite; sem ele, o "Running Crawl" do
	/// Mixamo, que pedia 2,9): 1,42 m/s no arquivo, vezes 1,13. Agachado
	/// (1,75 a 1,90 m/s) toca a 1,1× a 1,2×; o de 1,33 s tocava a 1,8×.
	/// Varrida: 0,38 m/s de patinação em 1,45, 0,34 em 1,52, perto de 0,2
	/// de 1,58 a 1,65, 0,34 em 1,75.</summary>
	[Export] public float CrawlClipSpeed = 1.62f;

	/// <summary>A passada da corrida de quatro (o Run_Quad do Jhon, ciclo
	/// de 0,6 s), em m/s: a velocidade em que o pé não desliza com o clipe
	/// a 1×. O clipe toca na velocidade do corpo dividida por ela — com o
	/// corpo a 5,4 m/s (WolfSprintFactor 1,3), a 1,37×: um pouco mais rápido
	/// que o feito e sem patinar (pedido do Jhon, 15/11; antes tocava a 1×
	/// com o corpo a 6 m/s e o pé deslizava 2,5 m/s). Varrida a 5,4 m/s:
	/// 1,31 m/s de patinação em 3,4, 0,77 em 3,8, o mínimo (0,58) em 3,95,
	/// 0,85 em 4,25, 1,25 em 4,8.</summary>
	[Export] public float SprintClipSpeed = 3.95f;

	/// <summary>Onde, no Run_Quad, as patas da frente e as de trás se
	/// juntam embaixo do corpo (as de trás pousando atrás das mãos), em
	/// segundos do clipe: é ali que sai o som do passo, uma vez por ciclo.</summary>
	public const float SprintGather = 0.567f;

	/// <summary>
	/// Quanto a animação de andar corre em relação ao corpo. 1 = acompanha
	/// a velocidade do corpo, pela passada do clipe (o pé não patina). 0,7
	/// = 30% mais lenta; 1,2 = 20% mais rápida — e a pata desliza a
	/// diferença. Só a animação: o corpo anda igual.
	///
	/// (Era "AnimSpeed", velocidade fixa do clipe; trocado em 13/11 porque
	/// 1 ali já era menos da metade do ritmo normal da corrida, e qualquer
	/// número parecia lento.)
	/// </summary>
	[Export(PropertyHint.Range, "0.2,2,0.05")] public float WalkAnimScale = 1f;

	/// <summary>O mesmo para a corrida de quatro (W duas vezes). O
	/// engatinhar agachado acompanha o corpo.</summary>
	[Export(PropertyHint.Range, "0.2,2,0.05")] public float SprintAnimScale = 1f;

	/// <summary>O ritmo que a velocidade pede, vezes a escala do ajuste.</summary>
	private static float AnimRate(float followRate, float scale) => followRate * Mathf.Max(scale, 0.05f);

	private float _speed;
	private float _moveMix;
	private float _gait;
	private float _yawOffset;
	private bool _backward;

	/// <summary>
	/// O passo acompanha o corpo, em velocidade e em direção.
	///
	/// Velocidade: o clipe toca na do corpo dividida pela passada dele —
	/// sem mínimo, sem atraso. Parou, o passo congela onde estava e as
	/// pernas se juntam em <see cref="StopBlend"/>; antes ele corria no
	/// lugar mais um quarto de segundo, e os pés andavam um metro com o
	/// corpo parado.
	///
	/// Direção: só há clipes de andar para a frente. De lado (A, D e as
	/// diagonais da frente) o corpo vira para onde vai; para trás (S e as
	/// diagonais de trás) fica de frente e o passo toca ao contrário.
	/// Golpe, carga, salto, bote e parede voltam o corpo para a mira.
	/// Medido no esqueleto (pé de apoio, Bruto a 3,78 m/s): de lado ia de
	/// 5,4 m/s de patinação a 0,85; para trás, de 7,3 a 0,72; para a
	/// frente, 0,73 — o resto é o pé rolando no apoio, e a passada
	/// medida (2,42 m/s) é a de menor patinação.
	/// </summary>
	private void UpdateLocomotion(float delta)
	{
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		float speed = flat.Length();
		_speed = speed;

		bool onWall = _player != null && _player.IsWallRunning;
		// Carregando o salto o corpo vira para onde anda e, parando, fica
		// como está (voltar para a mira arrastava os pés); o salto sai para a
		// câmera, e ao soltar o corpo vira para ela.
		// (A carga conta como golpe em curso e prende o corpo na arma: daí
		// o "!charging" nos dois, senão andando de lado o corpo ficava
		// virado para a mira e o engatinhar patinava 2,6 m/s.)
		bool charging = _weapon.IsChargingPounce;
		bool aiming = onWall || (_weapon.IsStriking && !charging) || (_weapon.IsRooted && !charging)
			|| _weapon.IsPounceFlying || _weapon.IsDiving;

		// Parado, fica virado para onde parou: girar de volta para a mira
		// arrastaria os pés pelo chão sem passo nenhum. Volta quando anda
		// para a frente ou ataca.
		// Agachado ou de quatro, só há clipe para a frente: o corpo vira
		// para onde anda. Em pé, com os quatro andares, fica de frente para
		// a mira e o passo escolhe a direção.
		bool crouching = ((_player?.IsCrouching ?? false) || _weapon.IsChargingPounce) && !onWall;
		bool sprinting = (_player?.IsSprinting ?? false) && !onWall;
		bool directional = Directional && !crouching && !sprinting;

		float target = aiming ? 0f : _yawOffset;
		if (!aiming && speed > 0.4f && directional)
		{
			Vector3 local = _body.GlobalTransform.Basis.Inverse() * flat;
			float angle = Mathf.Atan2(local.X, -local.Z);
			_direction = PickDirection(angle, _direction);
			_backward = false;

			// O resto do ângulo (até 45°, nas diagonais) o corpo vira.
			float rest = Mathf.Wrap(angle - DirectionAngle(_direction), -Mathf.Pi, Mathf.Pi);
			target = -rest;
		}
		else if (!aiming && speed > 0.4f)
		{
			// No espaço do corpo: 0 é a frente, positivo é a direita.
			Vector3 local = _body.GlobalTransform.Basis.Inverse() * flat;
			float angle = Mathf.Atan2(local.X, -local.Z);
			float abs = Mathf.Abs(angle);

			// Com folga entre 105° e 120°: perto da divisa, fica no lado
			// em que estava, senão o corpo daria meia-volta à toa.
			if (abs > Mathf.DegToRad(120f)) _backward = true;
			else if (abs < Mathf.DegToRad(105f)) _backward = false;

			if (_backward) angle -= Mathf.Sign(angle) * Mathf.Pi;

			// Girar o modelo em Y positivo leva a frente para a esquerda.
			target = -angle;
		}

		float rate = Mathf.DegToRad(aiming ? AimTurnRate : TurnRate) * delta;
		_yawOffset = Mathf.MoveToward(_yawOffset, target, rate);

		// Andar ou correr, pela velocidade; a troca leva 0,15 s, que é só
		// a passagem — nenhuma velocidade do jogo para no meio.
		if (speed > 0.4f)
		{
			float gaitTarget = Mathf.Clamp((speed - WalkSpeed) / Mathf.Max(RunSpeed - WalkSpeed, 0.01f), 0f, 1f);
			_gait = Mathf.MoveToward(_gait, gaitTarget, delta / 0.15f);
		}

		// Agachado acompanha a cápsula (que desce em 0,03 s) num décimo de
		// segundo: rápido o bastante para a cabeça não ficar fora dela.
		_crouch = Mathf.MoveToward(_crouch, crouching ? 1f : 0f, delta / 0.1f);

		// De quatro: andando agachado o lobo engatinha (o "Running Crawl");
		// na corrida de quatro (W duas vezes) toca o clipe dela, as duas na
		// velocidade do corpo. Sem o clipe da corrida, ela engatinha rápido.
		bool sprintClip = Has("sprint");
		_crawl = Mathf.MoveToward(_crawl, crouching || (sprinting && !sprintClip) ? 1f : 0f, delta / 0.1f);
		_sprint = Mathf.MoveToward(_sprint, sprinting && sprintClip ? 1f : 0f, delta / 0.15f);

		// A direção do passo vai até a escolhida em 0,15 s.
		if (Directional)
			_blend = _blend.MoveToward(DirectionVector(_direction), delta / 0.15f);

		float stride = Directional ? DirectionStride(_blend) : Mathf.Lerp(WalkClipSpeed, RunClipSpeed, _gait);
		float sign = _backward ? -1f : 1f;
		float cadence = AnimRate(Mathf.Clamp(speed / Mathf.Max(stride, 0.01f), 0f, MaxRate) * sign, WalkAnimScale);
		float crawl = Mathf.Clamp(speed / Mathf.Max(CrawlClipSpeed, 0.01f), 0f, MaxRate) * sign;
		if (sprinting) crawl = AnimRate(crawl, SprintAnimScale);
		_crawlRate = crawl;

		// A corrida de quatro acompanha o corpo, como o andar: a 5,4 m/s
		// toca a 1,3× e o pé não desliza.
		float gallop = AnimRate(Mathf.Clamp(speed / Mathf.Max(SprintClipSpeed, 0.01f), 0f, MaxRate) * sign, SprintAnimScale);
		UpdateSprintBeat(sprinting && sprintClip, gallop, delta);

		// A passada do som do passo em pé: dois pés por ciclo do clipe
		// que está tocando.
		StepStride = Directional ? DirectionStride(_blend) * DirectionCycle(_blend) / 2f : 0f;

		float mixTarget = Mathf.Clamp(speed / 0.5f, 0f, 1f);
		float mixTime = mixTarget > _moveMix ? StartBlend : StopBlend;
		_moveMix = Mathf.MoveToward(_moveMix, mixTarget, delta / Mathf.Max(mixTime, 0.01f));

		if (Directional) _tree.Set("parameters/Machine/Locomotion/Gait/blend_position", _blend);
		else _tree.Set("parameters/Machine/Locomotion/Gait/blend_position", _gait);
		_tree.Set("parameters/Machine/Locomotion/Speed/scale", cadence);
		_tree.Set("parameters/Machine/Locomotion/Mix/blend_amount", _moveMix);
		_tree.Set("parameters/Machine/Locomotion/IdleCrouch/blend_amount", _crouch);
		_tree.Set("parameters/Machine/Locomotion/GaitCrouch/blend_amount", _crawl);
		_tree.Set("parameters/Machine/Locomotion/CrawlSpeed/scale", crawl);
		if (sprintClip)
		{
			_tree.Set("parameters/Machine/Locomotion/GaitSprint/blend_amount", _sprint);
			_tree.Set("parameters/Machine/Locomotion/SprintSpeed/scale", gallop);
		}
	}

	private float _crouch;

	/// <summary>Ritmo do engatinhar neste quadro (com o sinal: negativo é
	/// para trás). O andar da carga do salto usa o mesmo.</summary>
	private float _crawlRate;

	/// <summary>Quanto está na corrida de quatro (o clipe dela).</summary>
	private float _sprint;

	/// <summary>Quanto o andar está de quatro (o "Running Crawl"): agachado
	/// andando ou na corrida de quatro.</summary>
	private float _crawl;

	private void ResetLocomotion()
	{
		_yawOffset = 0f;
		_moveMix = 0f;
		_backward = false;
		_direction = 0;
		_blend = new Vector2(0, 1);
	}

	/// <summary>Até quantas vezes o clipe acelera para acompanhar o corpo.
	/// Com os andares de 15/11 à noite nenhum passa de ~1,4×; a folga fica para
	/// velocidades fora do comum (empurrão, queda de bote).</summary>
	private const float MaxRate = 8f;

	/// <summary>Direção do passo em pé: 0 frente, 1 direita, 2 trás, 3
	/// esquerda.</summary>
	private int _direction;
	private Vector2 _blend = new(0, 1);

	/// <summary>A direção pelo ângulo do movimento (0 é a frente, positivo
	/// a direita), com 10° de folga na divisa para não ficar trocando
	/// nas diagonais.</summary>
	private static int PickDirection(float angle, int current)
	{
		float slack = Mathf.DegToRad(10f);
		float off = Mathf.Abs(Mathf.Wrap(angle - DirectionAngle(current), -Mathf.Pi, Mathf.Pi));
		if (off <= Mathf.Pi / 4f + slack) return current;

		float deg = Mathf.RadToDeg(angle);
		if (Mathf.Abs(deg) <= 45f) return 0;
		if (Mathf.Abs(deg) >= 135f) return 2;
		return deg > 0 ? 1 : 3;
	}

	private static float DirectionAngle(int direction) => direction switch
	{
		1 => Mathf.Pi / 2f,
		2 => Mathf.Pi,
		3 => -Mathf.Pi / 2f,
		_ => 0f
	};

	/// <summary>O ponto da direção no espaço 2D do passo (x direita, y
	/// frente).</summary>
	private static Vector2 DirectionVector(int direction) => direction switch
	{
		1 => new Vector2(1, 0),
		2 => new Vector2(0, -1),
		3 => new Vector2(-1, 0),
		_ => new Vector2(0, 1)
	};

	/// <summary>Peso de cada andar no ponto do espaço 2D: frente, trás,
	/// lado.</summary>
	private static (float Front, float Back, float Side) DirectionWeights(Vector2 p)
	{
		float front = Mathf.Max(p.Y, 0f), back = Mathf.Max(-p.Y, 0f), side = Mathf.Abs(p.X);
		float sum = Mathf.Max(front + back + side, 0.001f);
		return (front / sum, back / sum, side / sum);
	}

	private float DirectionStride(Vector2 p)
	{
		var (front, back, side) = DirectionWeights(p);
		return front * WalkClipSpeed + back * WalkBackClipSpeed + side * WalkSideClipSpeed;
	}

	private float DirectionCycle(Vector2 p)
	{
		var (front, back, side) = DirectionWeights(p);
		float Len(string clip) => _lengths.TryGetValue(clip, out float l) ? l : 1f;
		return front * Len("walk") + back * Len("walk_back") + side * Len("walk_left");
	}

	/// <summary>Metros entre um som de passo e o seguinte no andar em pé
	/// (0 sem os andares do Jhon: vale o do <see cref="BodyNoiseProfile"/>).</summary>
	public float StepStride { get; private set; }

	/// <summary>Quantas vezes as patas se juntaram desde que a corrida de
	/// quatro começou: o <see cref="Player"/> faz o som do passo a cada uma.</summary>
	public int SprintBeats { get; private set; }

	/// <summary>Se a corrida de quatro está dando o ritmo do som do passo.</summary>
	public bool SprintDrivesSteps { get; private set; }

	private bool _sprintRunning;
	private float _sprintClock;

	/// <summary>A corrida de quatro sempre começa do começo do clipe; daí
	/// em diante o relógio dele anda no ritmo da animação, e cada vez que
	/// passa por <see cref="SprintGather"/> conta uma batida.</summary>
	private void UpdateSprintBeat(bool running, float rate, float delta)
	{
		if (running && !_sprintRunning)
		{
			_tree.Set("parameters/Machine/Locomotion/SprintSeek/seek_request", 0f);
			_sprintClock = 0f;
			SprintBeats = 0;
		}
		_sprintRunning = running;
		SprintDrivesSteps = running;
		if (!running) return;

		float length = _lengths.TryGetValue("sprint", out float l) ? l : 0.6f;
		_sprintClock += Mathf.Abs(rate) * delta;
		SprintBeats = _sprintClock < SprintGather ? 0 : 1 + (int)((_sprintClock - SprintGather) / length);
	}
}
