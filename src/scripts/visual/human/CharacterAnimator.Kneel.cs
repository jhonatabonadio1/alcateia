using Godot;

/// <summary>
/// O agachado mais fundo do corpo visto (Jhon, 26/11, alpha fechado: "ele
/// agacha mais [em primeira pessoa] que a 3ª pessoa, deve ter um equilíbrio
/// aí"; a primeira pessoa fica como está, quem desce é o corpo que os outros
/// veem).
///
/// O único clipe agachado que existe é o "Crouched Walking" do Mixamo, que
/// mal dobra os joelhos: o quadril ficava só 8 cm abaixo do de pé e a cabeça
/// 10 cm, enquanto o olho de quem joga desce 87 cm. Em vez de trocar o clipe,
/// ele é aprofundado ao carregar: o quadril desce <see cref="KneelDrop"/> e as
/// pernas se dobram por cinemática inversa de dois ossos (coxa e canela),
/// com cada pé no mesmo lugar e na mesma direção do clipe original — o pé não
/// flutua nem entra no chão, e o joelho dobra para onde já apontava.
///
/// É a animação, e não um modificador por cima dela, de propósito: o que lê
/// os ossos durante o quadro (as peças de acerto do <see cref="BodyHitbox"/>,
/// a arma na mão) vê o corpo já abaixado. Um SkeletonModifier3D só valeria no
/// fim do quadro, e o tiro e a arma ficariam na altura antiga.
/// </summary>
public partial class CharacterAnimator
{
	[ExportGroup("Agachado")]
	/// <summary>Quanto o quadril do clipe agachado desce, em metros (o
	/// tronco e a cabeça descem junto).</summary>
	[Export] public float KneelDrop = 0.5f;

	/// <summary>O quanto a perna pode se dobrar: a distância do quadril ao
	/// tornozelo não fica abaixo desta fração da perna esticada (0,42 é o
	/// joelho fechado a uns 50°, o agachado fundo). O quadril para de descer
	/// antes de passar disso.</summary>
	[Export(PropertyHint.Range, "0.2,0.95")] public float KneelMinReach = 0.42f;

	/// <summary>Amostras por segundo das trilhas refeitas.</summary>
	private const float KneelRate = 30f;

	private static readonly string[] KneelSides = { "Left", "Right" };

	/// <summary>Aprofunda o clipe agachado no corpo <paramref name="body"/>
	/// (as trilhas apontam para "<paramref name="bodyPath"/>:osso").</summary>
	private void Kneel(Animation animation, Skeleton3D body, string bodyPath)
	{
		if (body == null || KneelDrop <= 0f) return;
		int hips = body.FindBone("mixamorig_Hips");
		if (hips < 0) return;

		// O "para cima" no espaço do esqueleto e quantos metros vale uma unidade
		// dele (o esqueleto do personagem é em centímetros, dentro da escala do modelo).
		Basis toWorld = body.GlobalBasis;
		Vector3 up = (toWorld.Inverse() * Vector3.Up).Normalized();
		float meters = (toWorld * up).Length();
		if (meters < 1e-6f) return;
		float drop = KneelDrop / meters;

		var legs = new int[2, 4];
		for (int s = 0; s < 2; s++)
		{
			string side = KneelSides[s];
			legs[s, 0] = body.FindBone($"mixamorig_{side}UpLeg");
			legs[s, 1] = body.FindBone($"mixamorig_{side}Leg");
			legs[s, 2] = body.FindBone($"mixamorig_{side}Foot");
			legs[s, 3] = body.FindBone($"mixamorig_{side}ToeBase");
			if (legs[s, 0] < 0 || legs[s, 1] < 0 || legs[s, 2] < 0) return;
		}

		int samples = Mathf.Max(2, Mathf.CeilToInt(animation.Length * KneelRate) + 1);
		var hipPos = new Vector3[samples];
		var rot = new Quaternion[2, 3, samples];
		for (int i = 0; i < samples; i++)
		{
			double t = Mathf.Min(animation.Length, i / KneelRate);
			Transform3D hipsGlobal = Global(animation, body, bodyPath, hips, t);
			var leg = new (Transform3D Up, Transform3D Knee, Transform3D Foot, Transform3D Toe)[2];
			for (int s = 0; s < 2; s++)
			{
				leg[s].Up = hipsGlobal * Local(animation, body, bodyPath, legs[s, 0], t);
				leg[s].Knee = leg[s].Up * Local(animation, body, bodyPath, legs[s, 1], t);
				leg[s].Foot = leg[s].Knee * Local(animation, body, bodyPath, legs[s, 2], t);
				leg[s].Toe = legs[s, 3] >= 0 ? leg[s].Foot * Local(animation, body, bodyPath, legs[s, 3], t) : leg[s].Foot;
			}

			// O quadril desce o que as duas pernas deixam.
			float lower = drop;
			for (int s = 0; s < 2; s++)
			{
				float l1 = leg[s].Up.Origin.DistanceTo(leg[s].Knee.Origin), l2 = leg[s].Knee.Origin.DistanceTo(leg[s].Foot.Origin);
				float least = (l1 + l2) * KneelMinReach;
				while (lower > 0f && (leg[s].Up.Origin - up * lower).DistanceTo(leg[s].Foot.Origin) < least)
					lower -= drop * 0.02f;
			}
			lower = Mathf.Max(lower, 0f);

			Transform3D localHips = Local(animation, body, bodyPath, hips, t);
			Vector3 moved = up * lower;
			// A posição do quadril é a do pai dele; o pai (se houver) não gira com o clipe.
			int parent = body.GetBoneParent(hips);
			Basis parentBasis = parent >= 0 ? body.GetBoneGlobalRest(parent).Basis : Basis.Identity;
			hipPos[i] = localHips.Origin - parentBasis.Inverse() * moved;

			for (int s = 0; s < 2; s++)
			{
				Transform3D upLeg = leg[s].Up, knee = leg[s].Knee, foot = leg[s].Foot;
				float l1 = upLeg.Origin.DistanceTo(knee.Origin), l2 = knee.Origin.DistanceTo(foot.Origin);
				Vector3 a = upLeg.Origin - moved, b = knee.Origin - moved, c = foot.Origin;
				Vector3 toFoot = c - a;
				float d = Mathf.Clamp(toFoot.Length(), Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
				Vector3 dir = toFoot.Normalized();

				// O joelho dobra para onde já apontava; com a perna reta, para a ponta do pé.
				Vector3 pole = (b - a) - dir * (b - a).Dot(dir);
				if (pole.Length() < l1 * 0.05f) pole = (leg[s].Toe.Origin - foot.Origin) - dir * (leg[s].Toe.Origin - foot.Origin).Dot(dir);
				pole = pole.Length() > 1e-6f ? pole.Normalized() : dir.Cross(up).Normalized();

				float along = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
				float height = Mathf.Sqrt(Mathf.Max(l1 * l1 - along * along, 0f));
				Vector3 k = a + dir * along + pole * height;

				Quaternion turnUp = Arc(b - a, k - a);
				Basis upBasis = new Basis(turnUp) * upLeg.Basis.Orthonormalized();
				Vector3 footAfter = k + new Basis(turnUp) * (foot.Origin - knee.Origin);
				Quaternion turnKnee = Arc(footAfter - k, c - k);
				Basis kneeBasis = new Basis(turnKnee * turnUp) * knee.Basis.Orthonormalized();
				Basis hipsBasis = hipsGlobal.Basis.Orthonormalized();

				rot[s, 0, i] = (hipsBasis.Inverse() * upBasis).GetRotationQuaternion();
				rot[s, 1, i] = (upBasis.Inverse() * kneeBasis).GetRotationQuaternion();
				// O pé fica como estava no mundo: plantado e virado para o mesmo lado.
				rot[s, 2, i] = (kneeBasis.Inverse() * foot.Basis.Orthonormalized()).GetRotationQuaternion();
			}
		}

		Write(animation, Track(animation, $"{bodyPath}:{body.GetBoneName(hips)}", Animation.TrackType.Position3D), samples, i => hipPos[i]);
		for (int s = 0; s < 2; s++)
			for (int j = 0; j < 3; j++)
			{
				int bone = legs[s, j], side = s, joint = j;
				Write(animation, Track(animation, $"{bodyPath}:{body.GetBoneName(bone)}", Animation.TrackType.Rotation3D), samples, i => rot[side, joint, i]);
			}
	}

	/// <summary>
	/// O agachado parado: não há clipe ("crouch_idle" saiu em 16/11) e a
	/// mistura tocava o andar agachado parado no lugar — o pé andava 30 cm por
	/// segundo com o corpo parado (a "animação de agachar quebrada" do alpha).
	/// Vira a pose do andar agachado no instante em que os dois pés estão mais
	/// baixos (os dois no chão), parada. A mistura do agachado já põe o
	/// parado em 0 e o andar em 1, pela velocidade.
	/// </summary>
	private static Animation KneelIdle(Animation walk, Skeleton3D body, string bodyPath)
	{
		if (body == null) return null;
		int left = body.FindBone("mixamorig_LeftFoot"), right = body.FindBone("mixamorig_RightFoot");
		if (left < 0 || right < 0) return null;
		Vector3 up = (body.GlobalBasis.Inverse() * Vector3.Up).Normalized();

		double best = 0;
		float lowest = float.MaxValue;
		for (double t = 0; t <= walk.Length; t += 1.0 / KneelRate)
		{
			float high = Mathf.Max(Global(walk, body, bodyPath, left, t).Origin.Dot(up), Global(walk, body, bodyPath, right, t).Origin.Dot(up));
			if (high < lowest) { lowest = high; best = t; }
		}

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
		return idle;
	}

	private static Quaternion Arc(Vector3 from, Vector3 to)
	{
		if (from.LengthSquared() < 1e-12f || to.LengthSquared() < 1e-12f) return Quaternion.Identity;
		Vector3 f = from.Normalized(), t = to.Normalized();
		if (f.Dot(t) > 0.999999f) return Quaternion.Identity;
		return new Quaternion(f, t);
	}

	/// <summary>A pose local do osso no clipe, no instante <paramref name="t"/>
	/// (o repouso no que o clipe não mexe).</summary>
	private static Transform3D Local(Animation animation, Skeleton3D body, string bodyPath, int bone, double t)
	{
		Transform3D rest = body.GetBoneRest(bone);
		string path = $"{bodyPath}:{body.GetBoneName(bone)}";
		int p = animation.FindTrack(path, Animation.TrackType.Position3D);
		int r = animation.FindTrack(path, Animation.TrackType.Rotation3D);
		Vector3 origin = p >= 0 ? animation.PositionTrackInterpolate(p, t) : rest.Origin;
		Quaternion rotation = r >= 0 ? animation.RotationTrackInterpolate(r, t) : rest.Basis.GetRotationQuaternion();
		return new Transform3D(new Basis(rotation).Scaled(rest.Basis.Scale), origin);
	}

	/// <summary>A pose do osso no espaço do esqueleto (os ancestrais pelo clipe).</summary>
	private static Transform3D Global(Animation animation, Skeleton3D body, string bodyPath, int bone, double t)
	{
		Transform3D pose = Local(animation, body, bodyPath, bone, t);
		for (int p = body.GetBoneParent(bone); p >= 0; p = body.GetBoneParent(p))
			pose = Local(animation, body, bodyPath, p, t) * pose;
		return pose;
	}

	private static int Track(Animation animation, string path, Animation.TrackType type)
	{
		int track = animation.FindTrack(path, type);
		if (track >= 0) return track;
		track = animation.AddTrack(type);
		animation.TrackSetPath(track, path);
		return track;
	}

	private static void Write(Animation animation, int track, int samples, System.Func<int, Variant> value)
	{
		for (int k = animation.TrackGetKeyCount(track) - 1; k >= 0; k--) animation.TrackRemoveKey(track, k);
		for (int i = 0; i < samples; i++)
			animation.TrackInsertKey(track, Mathf.Min(animation.Length, i / KneelRate), value(i));
	}
}
