using Godot;

/// <summary>
/// O agachado parado do humano: um instante do próprio andar agachado.
///
/// O andar agachado é o "Crouch Walking" do Mixamo que o Jhon pôs (26/11:
/// "use o da mixamo, a animação completa do jeito que está e adapte para a
/// altura dela"): pernas, quadril, tronco inclinado e braços do clipe,
/// ajustados ao corpo de cada personagem como os outros clipes. Não há clipe
/// parado; usar o andar no lugar fazia o pé patinar (31 cm/s), então o
/// parado é o instante de passada mais aberta com os dois pés baixos: um pé à
/// frente e o outro atrás, a base de quem agacha (no meio do passo uma perna
/// passava pela outra e os joelhos ficavam juntos). Nesse instante um pé está
/// sempre saindo do chão. Desde 28/11 a base é refeita por cima dele: o agachado
/// tático (<see cref="AnimationKit.CrouchStance"/>, os números abaixo).
/// O "Idle Crouching" do Mixamo não serve de parado: o quadril dele fica a 44%
/// da altura de pé (o do andar, a 72%), e a câmera, que fica nos olhos do corpo
/// (<c>CharacterAnimator.Eye</c>), cairia 35 cm cada vez que se para.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>Amostras por segundo para achar o instante do parado.</summary>
	private const float CrouchSampleRate = 30f;

	/// <summary>
	/// O agachado tático parado (Jhon, 28/11: "parece estar sentado em uma cadeira invisível... eu
	/// inclinaria um pouco mais o tronco para frente e ajustaria o quadril para criar uma postura de
	/// prontidão... uma base mais estável, com os joelhos acompanhando a direção dos pés"; aprovado na
	/// vitrine, "esses ficaram bons"). Em metros a partir do quadril: o pé da frente
	/// <see cref="CrouchFront"/> à frente (era 55 cm, a perna esticada), o de trás <see cref="CrouchRear"/>
	/// atrás (era embaixo do quadril, a canela deitada em cima do calcanhar) e na ponta, o calcanhar
	/// erguido <see cref="CrouchRearHeel"/> graus; <see cref="CrouchWidth"/> entre os dois de lado (era
	/// 15 cm); as pontas abertas <see cref="CrouchFrontOut"/> e <see cref="CrouchRearOut"/> graus, e cada
	/// joelho para onde o seu pé aponta. O quadril sobe <see cref="CrouchHipsRaise"/> do instante do andar
	/// (a cintura inclinada desce a cabeça: os olhos ficam onde estavam, ~117 cm).
	/// </summary>
	[ExportGroup("Agachado")]
	[Export] public float CrouchFront = 0.32f;
	[Export] public float CrouchRear = 0.28f;
	[Export] public float CrouchWidth = 0.32f;
	[Export] public float CrouchHipsRaise = 0.04f;
	[Export] public float CrouchRearHeel = 45f;
	[Export] public float CrouchFrontOut = 12f;
	[Export] public float CrouchRearOut = 30f;

	/// <summary>Agachado, quanto o corpo visto vai para trás da cápsula (m), parado e andando: com a
	/// cintura inclinada (<see cref="CrouchLean"/>) a cabeça ia 23 cm à frente da câmera da 1ª pessoa
	/// (em pé, 8,4); para trás 15 cm, fica a ~8 (Jhon, 27/11: "cabeça sempre alinhada com o primeira
	/// pessoa").</summary>
	[Export] public float CrouchBack = 0.15f;

	/// <summary>Andando agachado, quanto cada pé vai para fora do seu lado (m), o mesmo nos dois e no passo
	/// inteiro (Jhon, 28/11, vendo de trás as pernas bem juntas: "na pose anterior de andar agachado, somente
	/// abra um pouco para o lado a perna direita e um pouco para o outro lado a perna esquerda";
	/// <see cref="AnimationKit.OpenStride"/>).</summary>
	// Desligado (0) em 28/11: aberto e esticado por cima do clipe, a perna girava no próprio eixo
	// (Jhon: "não ficou profissional, movimento girando a perna no próprio eixo"). O andar agachado
	// volta a ser o clipe como é, até haver um ciclo feito a partir da pose parada (docs/PENDENCIAS.md).
	[Export] public float CrouchWalkOpen = 0f;

	/// <summary>Andando agachado, quantas vezes o passo fica mais comprido, no mesmo ritmo (Jhon, 28/11: "corrija
	/// a passada agachada para deixar melhor, mais bem feito"): o clipe anda ~1,06 m/s e o corpo agachado 1,27,
	/// e o pé de apoio patinava 0,21 m/s para a frente; o clipe não acelera (regra 9), o passo é que cresce.</summary>
	[Export] public float CrouchWalkStretch = 1f;

	/// <summary>Agachado, quanto a cintura inclina para a frente (graus): o peito volta o mesmo tanto, e a
	/// arma e a cabeça seguem retas (<see cref="HeldWeapon"/>). Parado e andando.</summary>
	[Export] public float CrouchLean = 30f;

	/// <summary>A inclinação da cintura agora (graus): entra e sai com a mistura do agachado.</summary>
	public float CrouchLeanNow { get; private set; }

	private AnimationKit.Stance CrouchStance => new(CrouchFront, CrouchRear, CrouchWidth, CrouchHipsRaise, CrouchBack,
		CrouchRearHeel, CrouchFrontOut, CrouchRearOut);

	/// <summary>A cintura inclina ao agachar e volta ao levantar, no tempo da troca de pose (0,15 s).</summary>
	private void UpdateCrouchLean(double delta) =>
		CrouchLeanNow = Mathf.MoveToward(CrouchLeanNow, _crouched && !_dead ? CrouchLean : 0f, CrouchLean / 0.15f * (float)delta);

	/// <summary>
	/// Na troca do agachado parado para o andar e de volta (a mistura das duas poses, 0,25 s), as pernas
	/// de uma base e da outra se misturam osso a osso, e a bota ou o joelho entrava até 9 cm no chão
	/// (medido em 28/11). Enquanto agachado, o corpo visto sobe o que a bota estiver abaixo do chão (os
	/// pontos da pele dos pés, <see cref="AnimationKit.FootPoints"/>) ou o joelho abaixo de
	/// <see cref="CrouchKneeFloor"/>. Parado e no meio do andar não há o que subir.
	/// </summary>
	[Export] public float CrouchKneeFloor = 0.07f;
	private float _crouchLift;
	private System.Collections.Generic.List<(int Bone, Vector3 Local)> _soles;
	private int _leftKnee = -1, _rightKnee = -1;

	/// <summary>Com a pose do quadro já feita (a árvore acabou de misturar): sobe o corpo o que for preciso.
	/// Medido antes, no _Process, era a pose do quadro anterior, e a bota ficava até 1,8 cm no chão.</summary>
	private void LiftOffFloor()
	{
		if (!IsInstanceValid(this) || _dead) return;
		UpdateCrouchLift();
		Position = new Vector3(Position.X, BaseY + _crouchLift, Position.Z);
	}

	private void UpdateCrouchLift()
	{
		if (!_crouched || _dead || _feetSkeleton == null || !IsInstanceValid(_feetSkeleton)) { _crouchLift = 0f; return; }
		if (_soles == null)
		{
			_soles = AnimationKit.FootPoints(_model, _feetSkeleton);
			_leftKnee = _feetSkeleton.FindBone("mixamorig_LeftLeg");
			_rightKnee = _feetSkeleton.FindBone("mixamorig_RightLeg");
		}
		Transform3D skeleton = _feetSkeleton.GlobalTransform;
		float base_ = GlobalPosition.Y, under = 0f;
		foreach (var (bone, local) in _soles)
			under = Mathf.Max(under, base_ - (skeleton * (_feetSkeleton.GetBoneGlobalPose(bone) * local)).Y);
		foreach (int knee in new[] { _leftKnee, _rightKnee })
			if (knee >= 0) under = Mathf.Max(under, CrouchKneeFloor - BoneHeight(knee));
		// (2 mm de folga: a bota de apoio do andar fica rente ao chão, e o corpo não balança)
		_crouchLift = Mathf.Max(0f, under - 0.002f);
	}

	private static Animation CrouchIdle(Animation walk, Skeleton3D body, string bodyPath, Node bodyRoot, AnimationKit.Stance stance)
	{
		if (body == null) return null;
		int left = body.FindBone("mixamorig_LeftFoot"), right = body.FindBone("mixamorig_RightFoot");
		if (left < 0 || right < 0) return null;
		Vector3 up = (body.GlobalBasis.Inverse() * Vector3.Up).Normalized();
		float meters = Mathf.Max((body.GlobalBasis * up).Length(), 1e-6f);

		var times = new System.Collections.Generic.List<(double T, float High, float Apart)>();
		for (double t = 0; t <= walk.Length; t += 1.0 / CrouchSampleRate)
		{
			Vector3 l = Bone(walk, body, bodyPath, left, t).Origin, r = Bone(walk, body, bodyPath, right, t).Origin;
			Vector3 gap = l - r;
			times.Add((t, Mathf.Max(l.Dot(up), r.Dot(up)), (gap - up * gap.Dot(up)).Length()));
		}
		float lowest = float.MaxValue;
		foreach (var x in times) lowest = Mathf.Min(lowest, x.High);
		// Entre os instantes com os dois pés baixos (a 6 cm do mais baixo), o de
		// passada mais aberta: um pé à frente e o outro atrás, a base firme de
		// quem agacha (no meio do passo, uma perna passava pela outra e os
		// joelhos ficavam juntos). Os dois pés ficam retos no chão depois.
		double best = 0;
		float widest = -1f;
		for (float slack = 0.06f; widest < 0f && slack < 1f; slack *= 2f)
			foreach (var x in times)
				if (x.High <= lowest + slack / meters && x.Apart > widest) { widest = x.Apart; best = x.T; }

		var idle = new Animation { Length = 1f, LoopMode = Animation.LoopModeEnum.Linear };
		for (int track = 0; track < walk.GetTrackCount(); track++)
		{
			Animation.TrackType type = walk.TrackGetType(track);
			Variant value;
			switch (type)
			{
				case Animation.TrackType.Position3D: value = walk.PositionTrackInterpolate(track, best); break;
				case Animation.TrackType.Rotation3D: value = walk.RotationTrackInterpolate(track, best); break;
				case Animation.TrackType.Scale3D: value = walk.ScaleTrackInterpolate(track, best); break;
				default: continue;
			}
			int copy = idle.AddTrack(type);
			idle.TrackSetPath(copy, walk.TrackGetPath(track));
			idle.TrackInsertKey(copy, 0, value);
			idle.TrackInsertKey(copy, 1, value);
		}
		// A base tática por cima do instante (no instante, um pé sempre está saindo do chão).
		AnimationKit.CrouchStance(idle, body, AnimationKit.ChainTo(body, bodyRoot), stance);
		return idle;
	}

	/// <summary>A pose do osso no espaço do esqueleto, no instante
	/// <paramref name="t"/> do clipe (o repouso no que o clipe não mexe).</summary>
	private static Transform3D Bone(Animation animation, Skeleton3D body, string bodyPath, int bone, double t)
	{
		Transform3D Local(int b)
		{
			Transform3D rest = body.GetBoneRest(b);
			string path = $"{bodyPath}:{body.GetBoneName(b)}";
			int p = animation.FindTrack(path, Animation.TrackType.Position3D);
			int r = animation.FindTrack(path, Animation.TrackType.Rotation3D);
			Vector3 origin = p >= 0 ? animation.PositionTrackInterpolate(p, t) : rest.Origin;
			Quaternion rotation = r >= 0 ? animation.RotationTrackInterpolate(r, t) : rest.Basis.GetRotationQuaternion();
			return new Transform3D(new Basis(rotation).Scaled(rest.Basis.Scale), origin);
		}
		Transform3D pose = Local(bone);
		for (int p = body.GetBoneParent(bone); p >= 0; p = body.GetBoneParent(p)) pose = Local(p) * pose;
		return pose;
	}
}
