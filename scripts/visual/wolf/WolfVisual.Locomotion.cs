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
	/// clipes têm ciclos de tamanhos diferentes (1,43 s e 0,87 s), e
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
	[Export] public float WalkClipSpeed = 1.34f;
	[Export] public float RunClipSpeed = 2.42f;

	/// <summary>A passada do "Running Crawl" no chão, em m/s: o andar de
	/// quatro, agachado e na corrida de quatro. Medida no esqueleto: a 2,9 a
	/// mão de apoio patina 0,55 m/s engatinhando e 1,46 correndo de quatro
	/// a 5,06 m/s (~29%, o clipe rola as mãos); com a passada da parede (2,3), que era a
	/// usada, 0,64 e 1,64.</summary>
	[Export] public float CrawlClipSpeed = 2.9f;

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
	/// engatinhar agachado continua acompanhando o corpo.</summary>
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
		bool aiming = onWall || _weapon.IsStriking || _weapon.IsChargingPounce || _weapon.IsRooted
			|| _weapon.IsPounceFlying || _weapon.IsDiving;

		// Parado, fica virado para onde parou: girar de volta para a mira
		// arrastaria os pés pelo chão sem passo nenhum. Volta quando anda
		// para a frente ou ataca.
		float target = aiming ? 0f : _yawOffset;
		if (!aiming && speed > 0.4f)
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
		bool crouching = (_player?.IsCrouching ?? false) && !onWall;
		_crouch = Mathf.MoveToward(_crouch, crouching ? 1f : 0f, delta / 0.1f);

		// De quatro: andando agachado (engatinha) e na corrida de quatro
		// (W duas vezes) toca o "Running Crawl", na velocidade do corpo.
		bool sprinting = (_player?.IsSprinting ?? false) && !onWall;
		_crawl = Mathf.MoveToward(_crawl, crouching || sprinting ? 1f : 0f, delta / 0.1f);

		float stride = Mathf.Lerp(WalkClipSpeed, RunClipSpeed, _gait);
		float sign = _backward ? -1f : 1f;
		float cadence = AnimRate(Mathf.Clamp(speed / Mathf.Max(stride, 0.01f), 0f, 3f) * sign, WalkAnimScale);
		float crawl = Mathf.Clamp(speed / Mathf.Max(CrawlClipSpeed, 0.01f), 0f, 3f) * sign;
		if (sprinting) crawl = AnimRate(crawl, SprintAnimScale);

		float mixTarget = Mathf.Clamp(speed / 0.5f, 0f, 1f);
		float mixTime = mixTarget > _moveMix ? StartBlend : StopBlend;
		_moveMix = Mathf.MoveToward(_moveMix, mixTarget, delta / Mathf.Max(mixTime, 0.01f));

		_tree.Set("parameters/Machine/Locomotion/Gait/blend_position", _gait);
		_tree.Set("parameters/Machine/Locomotion/Speed/scale", cadence);
		_tree.Set("parameters/Machine/Locomotion/Mix/blend_amount", _moveMix);
		_tree.Set("parameters/Machine/Locomotion/IdleCrouch/blend_amount", _crouch);
		_tree.Set("parameters/Machine/Locomotion/GaitCrouch/blend_amount", _crawl);
		_tree.Set("parameters/Machine/Locomotion/CrawlSpeed/scale", crawl);
	}

	private float _crouch;

	/// <summary>Quanto o andar está de quatro (o "Running Crawl"): agachado
	/// andando ou na corrida de quatro.</summary>
	private float _crawl;

	private void ResetLocomotion()
	{
		_yawOffset = 0f;
		_moveMix = 0f;
		_backward = false;
	}
}
