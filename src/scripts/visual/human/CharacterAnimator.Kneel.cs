using Godot;

/// <summary>
/// O agachado do corpo visto (Jhon, 26/11): ajoelhado, **reto**. Vendo o
/// primeiro rascunho: "muito baixo, perna entornando para o lado, tem que
/// agachar no limite da perna ficando de joelho, porém reta".
///
/// Parado é um joelho no chão, montado por cima do parado em pé
/// ("idle_rifle"): o tronco igual ao de pé (fuzil no ombro, regra 11), o
/// quadril na altura que a coxa de trás deixa com o joelho encostado no chão
/// (coxa na vertical, canela deitada para trás, o pé na ponta), a perna da
/// frente a 90° com o pé chapado — as duas no plano de andar, sem abrir para
/// o lado. Andando, o clipe "Crouched Walking" do Mixamo com o quadril na
/// mesma altura (a câmera é uma só) e as pernas dobrando para a frente.
///
/// É a animação, e não um modificador por cima dela, de propósito: o que lê
/// os ossos durante o quadro (as peças de acerto do <see cref="BodyHitbox"/>,
/// a arma na mão) vê o corpo já abaixado. Um SkeletonModifier3D só valeria no
/// fim do quadro, e o tiro e a arma ficariam na altura antiga.
/// </summary>
public partial class CharacterAnimator
{
	[ExportGroup("Agachado")]
	/// <summary>A altura do centro do joelho no chão, em metros acima da altura
	/// do tornozelo em pé. −3 cm: a pele do joelho encosta no chão (medido,
	/// 26/11: a 0 ela ficava 3 cm no ar).</summary>
	[Export] public float KneeClearance = -0.03f;

	/// <summary>Quanto o tornozelo da perna da frente fica à frente do quadril,
	/// em comprimentos de coxa (perto de 1: coxa deitada, canela em pé).</summary>
	[Export(PropertyHint.Range, "0.5,1.3")] public float KneelFrontReach = 0.92f;

	/// <summary>O quanto a perna pode se dobrar andando: a distância do quadril
	/// ao tornozelo não fica abaixo desta fração da perna esticada (0,36: 3,5 cm
	/// de pele no chão, medido em 26/11).</summary>
	[Export(PropertyHint.Range, "0.2,0.95")] public float KneelMinReach = 0.39f;

	/// <summary>Quanto o quadril desceu de fato no agachado parado, em metros.
	/// Para os testes e o AJUSTES.</summary>
	public float KneelLower { get; private set; }

	/// <summary>Amostras por segundo das trilhas refeitas.</summary>
	private const float KneelRate = 30f;

	private static readonly string[] KneelSides = { "Left", "Right" };

	/// <summary>As referências do esqueleto para ajoelhar: o "para cima", o
	/// "para a frente" e o lado, no espaço dele; os ossos das duas pernas; e o
	/// corpo em pé (o clipe parado).</summary>
	private sealed class KneelRig
	{
		public Vector3 Up, Forward, Side;
		public float Meters;
		public int Hips;
		public int[,] Legs = new int[2, 4];
		public Transform3D StandHips;
		public Transform3D[,] Stand = new Transform3D[2, 4];
		/// <summary>Quanto o quadril desce para o joelho de trás encostar no chão.</summary>
		public float Drop;
	}

	private KneelRig BuildKneelRig(Animation stand, Skeleton3D body, string bodyPath)
	{
		if (body == null || stand == null) return null;
		var rig = new KneelRig { Hips = body.FindBone("mixamorig_Hips") };
		if (rig.Hips < 0) return null;
		for (int s = 0; s < 2; s++)
		{
			string side = KneelSides[s];
			rig.Legs[s, 0] = body.FindBone($"mixamorig_{side}UpLeg");
			rig.Legs[s, 1] = body.FindBone($"mixamorig_{side}Leg");
			rig.Legs[s, 2] = body.FindBone($"mixamorig_{side}Foot");
			rig.Legs[s, 3] = body.FindBone($"mixamorig_{side}ToeBase");
			for (int j = 0; j < 4; j++) if (rig.Legs[s, j] < 0) return null;
		}
		Basis toWorld = body.GlobalBasis;
		rig.Up = (toWorld.Inverse() * Vector3.Up).Normalized();
		rig.Meters = (toWorld * rig.Up).Length();
		if (rig.Meters < 1e-6f) return null;

		rig.StandHips = Global(stand, body, bodyPath, rig.Hips, 0);
		for (int s = 0; s < 2; s++)
			for (int j = 0; j < 4; j++) rig.Stand[s, j] = Global(stand, body, bodyPath, rig.Legs[s, j], 0);

		// Para a frente: o pé em repouso, do tornozelo à ponta, no plano do chão.
		Vector3 toe = Vector3.Zero;
		for (int s = 0; s < 2; s++)
			toe += body.GetBoneGlobalRest(rig.Legs[s, 3]).Origin - body.GetBoneGlobalRest(rig.Legs[s, 2]).Origin;
		toe -= rig.Up * toe.Dot(rig.Up);
		if (toe.LengthSquared() < 1e-9f) return null;
		rig.Forward = toe.Normalized();
		rig.Side = rig.Up.Cross(rig.Forward).Normalized();

		// O joelho de trás (o direito) no chão, a coxa na vertical.
		Transform3D upLeg = rig.Stand[1, 0], knee = rig.Stand[1, 1], foot = rig.Stand[1, 2];
		float thigh = upLeg.Origin.DistanceTo(knee.Origin);
		float ankle = Mathf.Min(rig.Stand[0, 2].Origin.Dot(rig.Up), foot.Origin.Dot(rig.Up));
		float kneeAt = ankle + KneeClearance / rig.Meters;
		rig.Drop = Mathf.Max(upLeg.Origin.Dot(rig.Up) - (kneeAt + thigh), 0f);
		return rig;
	}

	/// <summary>Dobra uma perna: o quadril em <paramref name="a"/>, o tornozelo
	/// em <paramref name="c"/> e o joelho virado para <paramref name="pole"/>.
	/// Devolve as rotações locais da coxa, da canela e do pé (o pé com a base
	/// <paramref name="footBasis"/> no espaço do esqueleto).</summary>
	private static (Quaternion Up, Quaternion Knee, Quaternion Foot) Bend(KneelRig rig, int s, Basis hips, Vector3 a, Vector3 c, Vector3 pole, Basis footBasis)
	{
		Transform3D upLeg = rig.Stand[s, 0], knee = rig.Stand[s, 1], foot = rig.Stand[s, 2];
		float l1 = upLeg.Origin.DistanceTo(knee.Origin), l2 = knee.Origin.DistanceTo(foot.Origin);
		Vector3 toFoot = c - a;
		float d = Mathf.Clamp(toFoot.Length(), Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
		Vector3 dir = toFoot.Normalized();
		Vector3 bend = pole - dir * pole.Dot(dir);
		bend = bend.Length() > 1e-6f ? bend.Normalized() : rig.Forward;
		float along = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
		Vector3 k = a + dir * along + bend * Mathf.Sqrt(Mathf.Max(l1 * l1 - along * along, 0f));

		Quaternion turnUp = Arc(knee.Origin - upLeg.Origin, k - a);
		Basis upBasis = new Basis(turnUp) * upLeg.Basis.Orthonormalized();
		Vector3 footAfter = k + new Basis(turnUp) * (foot.Origin - knee.Origin);
		Quaternion turnKnee = Arc(footAfter - k, c - k);
		Basis kneeBasis = new Basis(turnKnee * turnUp) * knee.Basis.Orthonormalized();

		return ((hips.Inverse() * upBasis).GetRotationQuaternion(),
			(upBasis.Inverse() * kneeBasis).GetRotationQuaternion(),
			(kneeBasis.Inverse() * footBasis).GetRotationQuaternion());
	}

	/// <summary>A posição local do quadril para ele ir ao ponto
	/// <paramref name="at"/> do esqueleto (o pai dele não gira com o clipe).</summary>
	private static Vector3 HipsLocal(Skeleton3D body, int hips, Vector3 at)
	{
		int parent = body.GetBoneParent(hips);
		Transform3D from = parent >= 0 ? body.GetBoneGlobalRest(parent) : Transform3D.Identity;
		return from.AffineInverse() * at;
	}

	/// <summary>
	/// O agachado parado: de joelho, reto, por cima do parado em pé. O joelho
	/// direito no chão sob o quadril, a canela para trás e o pé na ponta; a
	/// perna esquerda à frente, a coxa quase deitada e o pé chapado.
	/// </summary>
	private Animation KneelIdle(Animation stand, Skeleton3D body, string bodyPath)
	{
		KneelRig rig = BuildKneelRig(stand, body, bodyPath);
		if (rig == null) return null;
		Vector3 up = rig.Up, forward = rig.Forward;
		Vector3 down = up * rig.Drop;
		KneelLower = rig.Drop * rig.Meters;
		Basis hips = rig.StandHips.Basis.Orthonormalized();

		var rot = new (Quaternion Up, Quaternion Knee, Quaternion Foot)[2];
		Quaternion toeBack = Quaternion.Identity;

		// Direita, atrás: coxa na vertical, canela deitada para trás.
		{
			Vector3 a = rig.Stand[1, 0].Origin - down;
			float thigh = rig.Stand[1, 0].Origin.DistanceTo(rig.Stand[1, 1].Origin);
			float shin = rig.Stand[1, 1].Origin.DistanceTo(rig.Stand[1, 2].Origin);
			Vector3 knee = a - up * thigh;
			float rise = 0.11f / rig.Meters;
			Vector3 c = knee - forward * Mathf.Sqrt(Mathf.Max(shin * shin - rise * rise, 0f)) + up * rise;
			// O pé na ponta: o tornozelo em cima, os dedos no chão.
			Vector3 standFoot = rig.Stand[1, 3].Origin - rig.Stand[1, 2].Origin;
			Basis footBasis = new Basis(Arc(standFoot, -up + forward * 0.25f)) * rig.Stand[1, 2].Basis.Orthonormalized();
			rot[1] = Bend(rig, 1, hips, a, c, forward - up * 0.2f, footBasis);
			// Os dedos deitados no chão, virados para a frente (com o pé na ponta,
			// os dedos seguiam o pé e entravam 7 cm no chão).
			toeBack = (footBasis.Inverse() * rig.Stand[1, 3].Basis.Orthonormalized()).GetRotationQuaternion();
		}
		// Esquerda, à frente: pé chapado no chão, joelho para a frente e para cima.
		{
			Vector3 a = rig.Stand[0, 0].Origin - down;
			float thigh = rig.Stand[0, 0].Origin.DistanceTo(rig.Stand[0, 1].Origin);
			float ankle = rig.Stand[0, 2].Origin.Dot(up);
			Vector3 c = a + forward * (thigh * KneelFrontReach);
			c += up * (ankle - c.Dot(up));
			rot[0] = Bend(rig, 0, hips, a, c, forward + up, rig.Stand[0, 2].Basis.Orthonormalized());
		}

		var idle = new Animation { Length = 1f, LoopMode = Animation.LoopModeEnum.Linear };
		for (int track = 0; track < stand.GetTrackCount(); track++)
		{
			Animation.TrackType type = stand.TrackGetType(track);
			Variant value;
			switch (type)
			{
				case Animation.TrackType.Position3D: value = stand.PositionTrackInterpolate(track, 0); break;
				case Animation.TrackType.Rotation3D: value = stand.RotationTrackInterpolate(track, 0); break;
				case Animation.TrackType.Scale3D: value = stand.ScaleTrackInterpolate(track, 0); break;
				default: continue;
			}
			int copy = idle.AddTrack(type);
			idle.TrackSetPath(copy, stand.TrackGetPath(track));
			idle.TrackInsertKey(copy, 0, value);
			idle.TrackInsertKey(copy, 1, value);
		}

		void Set(int bone, Animation.TrackType type, Variant value)
		{
			int track = Track(idle, $"{bodyPath}:{body.GetBoneName(bone)}", type);
			for (int k = idle.TrackGetKeyCount(track) - 1; k >= 0; k--) idle.TrackRemoveKey(track, k);
			idle.TrackInsertKey(track, 0, value);
			idle.TrackInsertKey(track, 1, value);
		}
		Set(rig.Hips, Animation.TrackType.Position3D, HipsLocal(body, rig.Hips, rig.StandHips.Origin - down));
		for (int s = 0; s < 2; s++)
		{
			Set(rig.Legs[s, 0], Animation.TrackType.Rotation3D, rot[s].Up);
			Set(rig.Legs[s, 1], Animation.TrackType.Rotation3D, rot[s].Knee);
			Set(rig.Legs[s, 2], Animation.TrackType.Rotation3D, rot[s].Foot);
		}
		Set(rig.Legs[1, 3], Animation.TrackType.Rotation3D, toeBack);
		return idle;
	}

	/// <summary>
	/// O agachado andando: o clipe, com o quadril reto como o de pé e na altura
	/// média do agachado parado (a câmera é uma só), e cada perna dobrando para
	/// a frente, no plano dela.
	/// </summary>
	private void Kneel(Animation walk, Animation stand, Skeleton3D body, string bodyPath)
	{
		KneelRig rig = BuildKneelRig(stand, body, bodyPath);
		if (rig == null) return;
		Vector3 up = rig.Up, forward = rig.Forward, side = rig.Side;
		Basis hips = rig.StandHips.Basis.Orthonormalized();

		int samples = Mathf.Max(2, Mathf.CeilToInt(walk.Length * KneelRate) + 1);
		var hipAt = new Vector3[samples];
		var feet = new Vector3[samples, 2];
		float meanHips = 0f;
		for (int i = 0; i < samples; i++)
		{
			double t = Mathf.Min(walk.Length, i / KneelRate);
			Transform3D h = Global(walk, body, bodyPath, rig.Hips, t);
			hipAt[i] = h.Origin;
			meanHips += h.Origin.Dot(up);
			for (int s = 0; s < 2; s++) feet[i, s] = Global(walk, body, bodyPath, rig.Legs[s, 2], t).Origin;
		}
		meanHips /= samples;

		// O quadril na altura do parado, sem passar do que a perna dobra.
		float lower = meanHips - (rig.StandHips.Origin.Dot(up) - rig.Drop);
		var upOffset = new Vector3[2];
		for (int s = 0; s < 2; s++) upOffset[s] = rig.Stand[s, 0].Origin - rig.StandHips.Origin;

		var rot = new Quaternion[2, 3, samples];
		var hipPos = new Vector3[samples];
		for (int i = 0; i < samples; i++)
		{
			Vector3 center = hipAt[i] - up * lower;
			hipPos[i] = HipsLocal(body, rig.Hips, center);
			for (int s = 0; s < 2; s++)
			{
				Vector3 a = center + upOffset[s];
				// O pé no plano da perna: o que o clipe anda para a frente, sem abrir.
				Vector3 c = feet[i, s] - side * (feet[i, s] - a).Dot(side);
				float l1 = rig.Stand[s, 0].Origin.DistanceTo(rig.Stand[s, 1].Origin);
				float l2 = rig.Stand[s, 1].Origin.DistanceTo(rig.Stand[s, 2].Origin);
				float least = (l1 + l2) * KneelMinReach;
				if (a.DistanceTo(c) < least) c = a + (c - a).Normalized() * least;
				var r = Bend(rig, s, hips, a, c, forward, rig.Stand[s, 2].Basis.Orthonormalized());
				rot[s, 0, i] = r.Up;
				rot[s, 1, i] = r.Knee;
				rot[s, 2, i] = r.Foot;
			}
		}

		Write(walk, Track(walk, $"{bodyPath}:{body.GetBoneName(rig.Hips)}", Animation.TrackType.Position3D), samples, i => hipPos[i]);
		Write(walk, Track(walk, $"{bodyPath}:{body.GetBoneName(rig.Hips)}", Animation.TrackType.Rotation3D), samples,
			_ => rig.StandHips.Basis.GetRotationQuaternion());
		for (int s = 0; s < 2; s++)
			for (int j = 0; j < 3; j++)
			{
				int bone = rig.Legs[s, j], leg = s, joint = j;
				Write(walk, Track(walk, $"{bodyPath}:{body.GetBoneName(bone)}", Animation.TrackType.Rotation3D), samples, i => rot[leg, joint, i]);
			}
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
