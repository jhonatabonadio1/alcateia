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
/// sempre saindo do chão; os dois ficam retos (<see cref="AnimationKit.FlatFeet"/>).
/// O "Idle Crouching" do Mixamo não serve de parado: o quadril dele fica a 44%
/// da altura de pé (o do andar, a 72%), e a câmera, que fica nos olhos do corpo
/// (<c>CharacterAnimator.Eye</c>), cairia 35 cm cada vez que se para.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>Amostras por segundo para achar o instante do parado.</summary>
	private const float CrouchSampleRate = 30f;

	private static Animation CrouchIdle(Animation walk, Skeleton3D body, string bodyPath, Node bodyRoot)
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
		// No instante escolhido um pé sempre está saindo do chão, de calcanhar
		// levantado: parado, os dois ficam retos no chão.
		AnimationKit.FlatFeet(idle, body, AnimationKit.ChainTo(body, bodyRoot));
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
