using Godot;
using static AnimationKit;

/// <summary>
/// As pisadas do corpo visto (Jhon, 27/11: "os barulhos de passos da corrida não
/// estão sincronizados com as pernas, primeira e terceira pessoa. Arrume e ouça
/// para ficar mais natural e próximo da realidade").
///
/// Até 27/11 o passo do humano saía a cada passada andada (0,94 m; correndo,
/// 1,12), contada à parte da animação: a cadência batia na média, mas o som
/// caía em qualquer ponto do ciclo da perna — e a mistura do andar com a
/// corrida não tem uma passada só. Agora quem diz que o pé tocou o chão é o pé:
/// a cada quadro, o tornozelo e a ponta de cada pé, no esqueleto como ele está
/// desenhado, contra o chão do próprio corpo (o mais baixo que cada um desses
/// dois ossos chega, que é quando o pé está apoiado). O pé saiu do chão
/// (<see cref="FootLift"/>), e o calcanhar ou a ponta voltou a ficar rente a ele
/// (<see cref="FootDown"/>; ou perto, <see cref="FootNear"/>, e parou de
/// descer): uma pisada, no quadro em que o pé encosta. O dono conta as pisadas
/// (<see cref="FootBeats"/>) e publica o passo (<see cref="BodyNoise"/>), no
/// jogador, no bot e no boneco da rede. Na primeira pessoa o corpo não se
/// desenha, mas anima: o som cai com o mesmo pé que os outros veem, e os braços
/// na tela seguem a mesma conta.
///
/// Sem o esqueleto (o servidor sem modelo) não há pisada: vale a passada do
/// <see cref="BodyNoiseProfile"/>.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>Quanto o pé (o calcanhar e a ponta, os dois) fica acima do chão
	/// para contar como fora dele: no mínimo isto (m), e <see cref="FootLiftShare"/>
	/// do quanto esta perna tem subido a cada passada. Medido em 27/11: o pé sobe
	/// 5 a 10 cm andando agachado, 14 a 18 andando e 32 correndo; e apoiado, no
	/// andar (a mistura do clipe de andar com o de correr), fica 3 a 4 cm acima
	/// do ponto mais baixo, que só vem na hora de sair do chão — com o limiar
	/// fixo em 3 cm o pé apoiado contava como no ar e dava um passo a mais.</summary>
	[ExportGroup("Pisadas")]
	[Export] public float FootLift = 0.03f;
	[Export] public float FootLiftShare = 0.45f;

	/// <summary>A esta altura do chão (m) o pé pisou; e perto dele
	/// (<see cref="FootNear"/>, ou <see cref="FootNearShare"/> da subida da perna,
	/// até <see cref="FootNearMax"/>), se parou de descer (<see cref="FootSettle"/>,
	/// em m/s).</summary>
	[Export] public float FootDown = 0.012f;
	[Export] public float FootNear = 0.04f;
	[Export] public float FootNearShare = 0.3f;
	[Export] public float FootNearMax = 0.06f;
	[Export] public float FootSettle = 0.45f;

	/// <summary>O menor tempo entre duas pisadas (s): o pé que treme no chão
	/// numa troca de clipe não vira dois passos, nem o que pousa de ponta e
	/// depois de calcanhar (andando de lado, medido: dois toques a 0,15 s). A
	/// corrida do Shift, a mais rápida, pisa a cada 0,22 s.</summary>
	[Export] public float FootGap = 0.18f;

	/// <summary>Quão rápido o chão medido sobe de volta (m/s), para acompanhar a
	/// postura (o agachado, outro clipe) sem esquecer o apoio de uma passada.</summary>
	private const float GroundRise = 0.02f;

	private Skeleton3D _feetSkeleton;
	private readonly int[] _footBones = { -1, -1, -1, -1 };
	/// <summary>O chão do tornozelo e o da ponta do pé (a altura em que cada um
	/// fica com o pé apoiado), e de cada perna: a altura do quadro anterior e se
	/// está no ar.</summary>
	private readonly float[] _footGround = new float[2], _footLast = new float[2], _footSwing = new float[2];

	/// <summary>Quão rápido a subida guardada de cada perna encolhe (m/s): vale
	/// por algumas passadas e esquece o clipe de antes.</summary>
	private const float SwingFade = 0.03f;
	private readonly bool[] _footUp = new bool[2];
	private bool _feetRead;
	private double _sinceBeat = 1.0, _beatEvery = 0.5;

	/// <summary>Quantas pisadas o corpo já deu (−1: este corpo não tem pés para
	/// ler, e o passo vai pela distância).</summary>
	public int FootBeats { get; private set; } = -1;

	/// <summary>Em que ponto da passada o corpo está: 0 logo depois da pisada, 1
	/// na seguinte (pelo tempo da última passada).</summary>
	public float FootPhase => (float)Mathf.Clamp(_sinceBeat / Mathf.Max(_beatEvery, 0.05), 0.0, 1.0);

	private void FindFeet()
	{
		_feetSkeleton = FindSkeleton(_model);
		string[] names = { "mixamorig_LeftFoot", "mixamorig_LeftToeBase", "mixamorig_RightFoot", "mixamorig_RightToeBase" };
		for (int i = 0; i < names.Length; i++) _footBones[i] = _feetSkeleton?.FindBone(names[i]) ?? -1;
		bool found = _feetSkeleton != null && _footBones[0] >= 0 && _footBones[2] >= 0;
		FootBeats = found ? 0 : -1;
		_footUp[0] = _footUp[1] = false;
		_feetRead = false;
	}

	/// <summary>A altura do osso <paramref name="bone"/> acima da base do corpo, no mundo
	/// (o esqueleto do modelo vem com escala e giro próprios).</summary>
	private float BoneHeight(int bone) =>
		(_feetSkeleton.GlobalTransform * _feetSkeleton.GetBoneGlobalPose(bone).Origin).Y - GlobalPosition.Y;

	/// <summary>
	/// Um quadro das pisadas, com a pose do quadro anterior nos ossos. Só conta
	/// andando no chão: parado, o pé que mexe no clipe de descanso não é passo.
	/// </summary>
	private void UpdateFeet(double delta)
	{
		if (FootBeats < 0 || _feetSkeleton == null || !IsInstanceValid(_feetSkeleton)) return;
		_sinceBeat += delta;
		float dt = (float)delta;

		// O tornozelo e a ponta de cada pé, e o chão de cada um dos dois ossos.
		System.Span<float> ankle = stackalloc float[2], toe = stackalloc float[2];
		for (int side = 0; side < 2; side++)
		{
			ankle[side] = BoneHeight(_footBones[side * 2]);
			toe[side] = _footBones[side * 2 + 1] >= 0 ? BoneHeight(_footBones[side * 2 + 1]) : ankle[side];
		}
		float lowAnkle = Mathf.Min(ankle[0], ankle[1]), lowToe = Mathf.Min(toe[0], toe[1]);
		_footGround[0] = !_feetRead || lowAnkle < _footGround[0] ? lowAnkle : Mathf.Min(_footGround[0] + GroundRise * dt, lowAnkle);
		_footGround[1] = !_feetRead || lowToe < _footGround[1] ? lowToe : Mathf.Min(_footGround[1] + GroundRise * dt, lowToe);

		bool walking = !_dead && _body != null && _body.IsOnFloor()
			&& new Vector2(_body.Velocity.X, _body.Velocity.Z).Length() >= BodyNoise.StillSpeed;
		for (int side = 0; side < 2; side++)
		{
			// Quão longe do chão está o que desta perna chega mais perto dele.
			float height = Mathf.Min(ankle[side] - _footGround[0], toe[side] - _footGround[1]);
			float fell = _footLast[side] - height;
			_footLast[side] = height;
			if (!_feetRead || !walking) { _footUp[side] = false; _footSwing[side] = 0f; continue; }

			// O quanto esta perna tem subido: a régua do que é "no ar" e do que é "perto do chão".
			_footSwing[side] = Mathf.Max(height, _footSwing[side] - SwingFade * dt);
			if (!_footUp[side]) { _footUp[side] = height > Mathf.Max(FootLift, FootLiftShare * _footSwing[side]); continue; }
			float near = Mathf.Clamp(FootNearShare * _footSwing[side], FootNear, FootNearMax);
			if (height > FootDown && (height > near || fell > FootSettle * dt)) continue;
			_footUp[side] = false;
			if (_sinceBeat < FootGap) continue;
			_beatEvery = Mathf.Min(_sinceBeat, 1.0);
			_sinceBeat = 0;
			FootBeats++;
		}
		_feetRead = true;
	}
}
