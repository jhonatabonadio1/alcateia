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
/// o lado. Andando, uma passada montada aqui (<see cref="KneelWalk"/>), com o
/// quadril na mesma altura (a câmera é uma só), os pés para a frente e o pé de
/// apoio parado no chão. O "Crouched Walking" do Mixamo saiu: com o pé do
/// parado em pé (o direito aberto 54°, a base de tiro) o pé entortava ao andar
/// (Jhon, 26/11: "veja o pé entortando"), e ele andava a 0,67 m/s com o corpo
/// a 1,35.
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

	/// <summary>A velocidade do corpo agachado andando, em m/s: a passada é
	/// calculada por ela, para o pé de apoio ficar parado no chão.</summary>
	[Export] public float CrouchStrideSpeed = 1.35f;

	/// <summary>Quanto dura um ciclo (os dois passos), em segundos.</summary>
	[Export] public float CrouchStrideTime = 0.72f;

	/// <summary>Fração do ciclo com o pé no chão.</summary>
	[Export(PropertyHint.Range, "0.5,0.75")] public float CrouchStance = 0.6f;

	/// <summary>Quanto o pé sobe no ar ao dar o passo, em metros.</summary>
	[Export] public float CrouchStepLift = 0.09f;

	/// <summary>Quanto o calcanhar levanta na saída do passo, em graus.</summary>
	[Export] public float CrouchHeelLift = 35f;

	/// <summary>O vai e volta do quadril a cada passo, em metros.</summary>
	[Export] public float CrouchBob = 0.012f;


	/// <summary>O pé em repouso (o do modelo, virado para a frente), no espaço
	/// do esqueleto.</summary>
	private static Basis RestFoot(Skeleton3D body, int foot) => body.GetBoneGlobalRest(foot).Basis.Orthonormalized();

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
			Vector3 restFoot = body.GetBoneGlobalRest(rig.Legs[1, 3]).Origin - body.GetBoneGlobalRest(rig.Legs[1, 2]).Origin;
			Basis footBasis = new Basis(Arc(restFoot, -up + forward * 0.25f)) * RestFoot(body, rig.Legs[1, 2]);
			rot[1] = Bend(rig, 1, hips, a, c, forward - up * 0.2f, footBasis);
			// Os dedos deitados no chão, virados para a frente (com o pé na ponta,
			// os dedos seguiam o pé e entravam 7 cm no chão).
			toeBack = (footBasis.Inverse() * body.GetBoneGlobalRest(rig.Legs[1, 3]).Basis.Orthonormalized()).GetRotationQuaternion();
		}
		// Esquerda, à frente: pé chapado no chão, joelho para a frente e para cima.
		{
			Vector3 a = rig.Stand[0, 0].Origin - down;
			float thigh = rig.Stand[0, 0].Origin.DistanceTo(rig.Stand[0, 1].Origin);
			float ankle = rig.Stand[0, 2].Origin.Dot(up);
			Vector3 c = a + forward * (thigh * KneelFrontReach);
			c += up * (ankle - c.Dot(up));
			rot[0] = Bend(rig, 0, hips, a, c, forward + up, RestFoot(body, rig.Legs[0, 2]));
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
	/// O agachado andando, montado por cima do parado em pé: o quadril reto na
	/// altura do agachado parado (com um vai e volta de <see cref="CrouchBob"/>),
	/// e cada pé num ciclo: no apoio, a planta recua exatamente na velocidade do
	/// corpo (o pé fica parado no chão), chapada e depois com o calcanhar
	/// levantando; no ar, vai para a frente erguido e pousa chapado. Os pés
	/// sempre para a frente, no plano da perna.
	/// </summary>
	private Animation KneelWalk(Animation stand, Skeleton3D body, string bodyPath)
	{
		KneelRig rig = BuildKneelRig(stand, body, bodyPath);
		if (rig == null) return null;
		Vector3 up = rig.Up, forward = rig.Forward, side = rig.Side;
		float units = 1f / rig.Meters;
		Basis hips = rig.StandHips.Basis.Orthonormalized();

		float cycle = Mathf.Max(CrouchStrideTime, 0.2f);
		float travel = CrouchStrideSpeed * cycle * CrouchStance * units;   // o quanto a planta recua no apoio
		float lift = CrouchStepLift * units, bob = CrouchBob * units;

		// A planta (a base dos dedos) na altura em que fica em pé, e o tornozelo
		// em relação a ela com o pé chapado (o pé do repouso, para a frente).
		float floor = Mathf.Min(rig.Stand[0, 3].Origin.Dot(up), rig.Stand[1, 3].Origin.Dot(up));
		var ankleFromBall = new Vector3[2];
		var flat = new Basis[2];
		var toeFlat = new Basis[2];
		for (int s = 0; s < 2; s++)
		{
			Vector3 v = body.GetBoneGlobalRest(rig.Legs[s, 2]).Origin - body.GetBoneGlobalRest(rig.Legs[s, 3]).Origin;
			float back = new Vector3(v.X, v.Y, v.Z).Dot(forward);
			float high = v.Dot(up);
			ankleFromBall[s] = forward * back + up * high;
			flat[s] = RestFoot(body, rig.Legs[s, 2]);
			toeFlat[s] = RestFoot(body, rig.Legs[s, 3]);
		}

		var anim = new Animation { Length = cycle, LoopMode = Animation.LoopModeEnum.Linear };
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
			int copy = anim.AddTrack(type);
			anim.TrackSetPath(copy, stand.TrackGetPath(track));
			anim.TrackInsertKey(copy, 0, value);
		}

		int samples = Mathf.CeilToInt(cycle * 60f);
		var hipPos = new Vector3[samples + 1];
		var rot = new Quaternion[2, 4, samples + 1];
		var upOffset = new Vector3[2];
		for (int s = 0; s < 2; s++) upOffset[s] = rig.Stand[s, 0].Origin - rig.StandHips.Origin;
		Vector3 standHipsLow = rig.StandHips.Origin - up * rig.Drop;

		for (int i = 0; i <= samples; i++)
		{
			float phase = (float)i / samples % 1f;
			// O quadril sobe no meio de cada passo (duas vezes por ciclo).
			Vector3 center = standHipsLow + up * (bob * 0.5f * -Mathf.Cos(phase * Mathf.Tau * 2f));
			hipPos[i] = HipsLocal(body, rig.Hips, center);
			for (int s = 0; s < 2; s++)
			{
				float p = (phase + s * 0.5f) % 1f;
				Vector3 a = center + upOffset[s];
				float middle = a.Dot(forward);
				float z, high, pitch;
				if (p < CrouchStance)
				{
					// Apoio: a planta recua na velocidade do corpo, parada no chão.
					float u = p / CrouchStance;
					z = middle + travel * (0.5f - u);
					high = 0f;
					pitch = u > 0.55f ? Mathf.SmoothStep(0f, 1f, (u - 0.55f) / 0.45f) : 0f;
				}
				else
				{
					// No ar: volta para a frente, erguido, e pousa chapado.
					float u = (p - CrouchStance) / (1f - CrouchStance);
					z = middle + travel * (-0.5f + Mathf.SmoothStep(0f, 1f, u));
					high = lift * Mathf.Sin(u * Mathf.Pi);
					pitch = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Min(u * 1.6f, 1f));
				}
				// A planta, no plano da perna.
				Vector3 ball = a - up * a.Dot(up) - forward * a.Dot(forward) + forward * z + up * (floor + high);
				// O calcanhar sobe girando em volta da planta (a ponta do pé desce).
				var heel = new Basis(side, Mathf.DegToRad(CrouchHeelLift * pitch));
				if ((heel * ankleFromBall[s]).Dot(up) < ankleFromBall[s].Dot(up)) heel = new Basis(side, -Mathf.DegToRad(CrouchHeelLift * pitch));
				Vector3 ankle = ball + heel * ankleFromBall[s];
				Basis footBasis = heel * flat[s];
				var r = Bend(rig, s, hips, a, ankle, forward + up * 0.2f, footBasis);
				rot[s, 0, i] = r.Up;
				rot[s, 1, i] = r.Knee;
				rot[s, 2, i] = r.Foot;
				// Os dedos continuam deitados no chão.
				rot[s, 3, i] = (footBasis.Inverse() * toeFlat[s]).GetRotationQuaternion();
			}
		}

		void Fill(int bone, Animation.TrackType type, System.Func<int, Variant> value)
		{
			int track = Track(anim, $"{bodyPath}:{body.GetBoneName(bone)}", type);
			for (int k = anim.TrackGetKeyCount(track) - 1; k >= 0; k--) anim.TrackRemoveKey(track, k);
			for (int i = 0; i <= samples; i++) anim.TrackInsertKey(track, cycle * i / samples, value(i));
		}
		Fill(rig.Hips, Animation.TrackType.Position3D, i => hipPos[i]);
		Fill(rig.Hips, Animation.TrackType.Rotation3D, _ => rig.StandHips.Basis.GetRotationQuaternion());
		for (int s = 0; s < 2; s++)
			for (int j = 0; j < 4; j++)
			{
				int side2 = s, joint = j;
				Fill(rig.Legs[s, j], Animation.TrackType.Rotation3D, i => rot[side2, joint, i]);
			}
		return anim;
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

}
