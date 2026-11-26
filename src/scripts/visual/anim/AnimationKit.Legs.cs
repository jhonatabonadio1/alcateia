using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O pé e a perna de um clipe do Mixamo num corpo de outras proporções
/// (Jhon, 26/11, no andar agachado: "parece que tá na ponta do pé", "pé
/// afundando no chão").
///
/// <see cref="RetargetPose"/> copia a direção de cada osso. No pé isso não
/// serve: o osso vai do tornozelo à base dos dedos, e o ângulo dele em pé
/// muda de rig para rig (35° para baixo na Eve, 39° no X Bot, 53° no
/// personagem dos arquivos de agachar). Copiando a direção, o pé da Eve
/// agachada apontava 18° a mais para baixo que o do clipe: na ponta do pé.
/// O pé copia o giro a partir do repouso, como o do lobo: sola reta no
/// clipe, sola reta no corpo.
///
/// A perna também tem outra proporção (a coxa da Eve é mais longa que a
/// canela; no personagem do agachar as duas são iguais). Com o quadril na
/// altura certa e as direções copiadas, o tornozelo da Eve agachada ficava
/// 4,8 cm acima de onde pousa, e o pé esticava a ponta até o chão.
/// <see cref="ReachFloor"/> dobra a perna (dois ossos, o joelho para onde já
/// dobrava) até o tornozelo pousar na altura que o do clipe pousa, e nunca
/// tão baixo que a sola da bota passe do chão: a articulação dos dedos da
/// Eve fica 2,2 cm acima da sola (a do personagem do clipe, rente ao chão),
/// e no empurrar do passo, de calcanhar levantado, a bola do pé entrava 3 cm.
/// </summary>
public static partial class AnimationKit
{
	/// <summary>Os ossos que copiam o giro a partir do repouso, não a direção
	/// (os dedos do pé vão junto: herdam o do pé).</summary>
	public static readonly string[] FootBones = { "mixamorig_LeftFoot", "mixamorig_RightFoot" };

	/// <summary>As trilhas de um clipe por osso (o nome depois de ":").</summary>
	private sealed class BoneTracks
	{
		public readonly Dictionary<string, int> Position = new(), Rotation = new(), Scale = new();

		public BoneTracks(Animation animation)
		{
			for (int track = 0; track < animation.GetTrackCount(); track++)
			{
				if (animation.TrackGetKeyCount(track) == 0) continue;
				string path = animation.TrackGetPath(track).ToString();
				string bone = path[(path.LastIndexOf(':') + 1)..];
				switch (animation.TrackGetType(track))
				{
					case Animation.TrackType.Position3D: Position[bone] = track; break;
					case Animation.TrackType.Rotation3D: Rotation[bone] = track; break;
					case Animation.TrackType.Scale3D: Scale[bone] = track; break;
				}
			}
		}
	}

	/// <summary>Onde um osso está no instante <paramref name="time"/> do clipe, no
	/// espaço do esqueleto (o que o clipe não anima fica no repouso).</summary>
	private static Transform3D PoseAt(Animation animation, BoneTracks tracks, Skeleton3D skeleton, int bone, double time)
	{
		Transform3D rest = skeleton.GetBoneRest(bone);
		string name = skeleton.GetBoneName(bone);
		Vector3 position = tracks.Position.TryGetValue(name, out int p) ? animation.PositionTrackInterpolate(p, time) : rest.Origin;
		Quaternion rotation = tracks.Rotation.TryGetValue(name, out int r)
			? animation.RotationTrackInterpolate(r, time).Normalized()
			: rest.Basis.Orthonormalized().GetRotationQuaternion();
		Vector3 scale = tracks.Scale.TryGetValue(name, out int s) ? animation.ScaleTrackInterpolate(s, time) : rest.Basis.Scale;
		var local = new Transform3D(new Basis(rotation) * Basis.FromScale(scale), position);
		int parent = skeleton.GetBoneParent(bone);
		return parent < 0 ? local : PoseAt(animation, tracks, skeleton, parent, time) * local;
	}

	/// <summary>
	/// Dobra as pernas de um clipe já ajustado ao corpo (<paramref name="animation"/>)
	/// até cada tornozelo ficar, acima do chão, onde o do clipe original
	/// (<paramref name="source"/>, no esqueleto <paramref name="reference"/>) fica
	/// acima do repouso dele, na escala do quadril. Só a altura muda: o
	/// tornozelo continua no mesmo ponto do chão, o joelho dobra para onde já
	/// dobrava e o pé mantém o giro (a sola não muda). O quadril não se mexe:
	/// a cabeça, e com ela a câmera, ficam onde estavam.
	/// </summary>
	public static void ReachFloor(Animation animation, Animation source, Skeleton3D reference, Transform3D fr,
		Skeleton3D body, Node bodyRoot, Transform3D fb)
	{
		int refHips = reference.FindBone("mixamorig_Hips"), bodyHips = body.FindBone("mixamorig_Hips");
		if (refHips < 0 || bodyHips < 0) return;
		float ratio = (fb * body.GetBoneGlobalRest(bodyHips).Origin).Y
			/ Mathf.Max((fr * reference.GetBoneGlobalRest(refHips).Origin).Y, 1e-4f);
		var bodyTracks = new BoneTracks(animation);
		var refTracks = new BoneTracks(source);
		Transform3D back = fb.AffineInverse();

		foreach (string side in new[] { "Left", "Right" })
		{
			string thigh = $"mixamorig_{side}UpLeg", shin = $"mixamorig_{side}Leg", foot = $"mixamorig_{side}Foot";
			int bt = body.FindBone(thigh), bs = body.FindBone(shin), bf = body.FindBone(foot), rf = reference.FindBone(foot);
			if (bt < 0 || bs < 0 || bf < 0 || rf < 0) continue;
			if (body.GetBoneParent(bs) != bt || body.GetBoneParent(bf) != bs) continue;
			if (!bodyTracks.Rotation.TryGetValue(thigh, out int tt) || !bodyTracks.Rotation.TryGetValue(shin, out int ts)
				|| !bodyTracks.Rotation.TryGetValue(foot, out int tf)) continue;
			int keys = animation.TrackGetKeyCount(tt);
			if (animation.TrackGetKeyCount(ts) != keys || animation.TrackGetKeyCount(tf) != keys) continue;

			float refRest = (fr * reference.GetBoneGlobalRest(rf).Origin).Y;
			float bodyRest = (fb * body.GetBoneGlobalRest(bf).Origin).Y;
			int parent = body.GetBoneParent(bt);
			List<(int Bone, Vector3 Local)> sole = Sole(bodyRoot, body, fb, side);

			// Tudo medido antes de escrever: cada chave lê o clipe como o
			// RetargetPose deixou.
			var turned = new (Quaternion Thigh, Quaternion Shin, Quaternion Foot)?[keys];
			for (int key = 0; key < keys; key++)
			{
				double time = animation.TrackGetKeyTime(tt, key);
				float lift = (fr * PoseAt(source, refTracks, reference, rf, time).Origin).Y - refRest;
				Transform3D up = PoseAt(animation, bodyTracks, body, bt, time);
				Transform3D knee = PoseAt(animation, bodyTracks, body, bs, time);
				Transform3D ankle = PoseAt(animation, bodyTracks, body, bf, time);
				Vector3 goal = fb * ankle.Origin;
				float ankleNow = goal.Y;
				goal.Y = bodyRest + lift * ratio;
				// A sola gira com o pé e os dedos e anda com o tornozelo: o
				// ponto mais baixo dela, com o tornozelo no alvo, não passa do chão.
				if (sole.Count > 0)
				{
					var pose = new Dictionary<int, Transform3D>();
					float low = float.MaxValue;
					foreach (var (bone, local) in sole)
					{
						if (!pose.TryGetValue(bone, out Transform3D at))
							pose[bone] = at = PoseAt(animation, bodyTracks, body, bone, time);
						low = Mathf.Min(low, (fb * (at * local)).Y);
					}
					goal.Y = Mathf.Max(goal.Y, ankleNow - low);
				}
				turned[key] = Bend(up, knee, ankle, back * goal,
					parent < 0 ? Quaternion.Identity : PoseAt(animation, bodyTracks, body, parent, time).Basis.Orthonormalized().GetRotationQuaternion());
			}

			Quaternion? lastThigh = null, lastShin = null, lastFoot = null;
			for (int key = 0; key < keys; key++)
			{
				if (turned[key] is not var (qt, qs, qf)) continue;
				Keep(ref qt, ref lastThigh);
				Keep(ref qs, ref lastShin);
				Keep(ref qf, ref lastFoot);
				animation.TrackSetKeyValue(tt, key, qt);
				animation.TrackSetKeyValue(ts, key, qs);
				animation.TrackSetKeyValue(tf, key, qf);
			}
		}
	}

	/// <summary>
	/// Os dois pés de uma pose parada (<paramref name="pose"/>, a mesma chave
	/// do começo ao fim) retos no chão: a sola como em repouso, virada para
	/// onde o pé já apontava, o tornozelo na altura do repouso no mesmo ponto
	/// do chão e a perna dobrando até ele. O agachado parado é um instante do
	/// andar, e nele um pé está sempre saindo do chão, de calcanhar levantado
	/// (Jhon, 26/11: "parece que tá na ponta do pé"; no "Idle Crouching" do
	/// Mixamo os dois pés ficam retos). O quadril não se mexe.
	/// </summary>
	public static void FlatFeet(Animation pose, Skeleton3D body, Transform3D fb)
	{
		var tracks = new BoneTracks(pose);
		Quaternion frame = fb.Basis.Orthonormalized().GetRotationQuaternion();
		Transform3D back = fb.AffineInverse();
		Quaternion Rest(int bone) => (frame * body.GetBoneGlobalRest(bone).Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
		static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

		foreach (string side in new[] { "Left", "Right" })
		{
			string thigh = $"mixamorig_{side}UpLeg", shin = $"mixamorig_{side}Leg", foot = $"mixamorig_{side}Foot", toes = $"mixamorig_{side}ToeBase";
			int bt = body.FindBone(thigh), bs = body.FindBone(shin), bf = body.FindBone(foot), bo = body.FindBone(toes);
			if (bt < 0 || bs < 0 || bf < 0 || bo < 0) continue;
			if (body.GetBoneParent(bs) != bt || body.GetBoneParent(bf) != bs || body.GetBoneParent(bo) != bf) continue;
			if (!tracks.Rotation.TryGetValue(thigh, out int tt) || !tracks.Rotation.TryGetValue(shin, out int ts)
				|| !tracks.Rotation.TryGetValue(foot, out int tf)) continue;

			Transform3D up = PoseAt(pose, tracks, body, bt, 0), knee = PoseAt(pose, tracks, body, bs, 0), ankle = PoseAt(pose, tracks, body, bf, 0);
			int parent = body.GetBoneParent(bt);
			Quaternion parentWorld = parent < 0 ? Quaternion.Identity : PoseAt(pose, tracks, body, parent, 0).Basis.Orthonormalized().GetRotationQuaternion();

			// Para onde o pé aponta no chão agora, a partir de para onde apontava em repouso.
			Vector3 restAlong = Flat(fb * body.GetBoneGlobalRest(bo).Origin - fb * body.GetBoneGlobalRest(bf).Origin);
			Quaternion turned = (frame * ankle.Basis.Orthonormalized().GetRotationQuaternion()).Normalized() * Rest(bf).Inverse();
			Vector3 nowAlong = Flat(turned * (fb * body.GetBoneGlobalRest(bo).Origin - fb * body.GetBoneGlobalRest(bf).Origin));
			if (restAlong.LengthSquared() < 1e-10f || nowAlong.LengthSquared() < 1e-10f) continue;
			Quaternion heading = Arc(restAlong.Normalized(), nowAlong.Normalized());

			Quaternion footWorld = (frame.Inverse() * heading * Rest(bf)).Normalized();
			Quaternion toesWorld = (frame.Inverse() * heading * Rest(bo)).Normalized();
			Vector3 goal = fb * ankle.Origin;
			goal.Y = (fb * body.GetBoneGlobalRest(bf).Origin).Y;
			if (Bend(up, knee, ankle, back * goal, parentWorld, footWorld) is not var (qt, qs, qf)) continue;

			void Set(int track, Quaternion value)
			{
				for (int key = 0; key < pose.TrackGetKeyCount(track); key++) pose.TrackSetKeyValue(track, key, value);
			}
			Set(tt, qt);
			Set(ts, qs);
			Set(tf, qf);
			if (tracks.Rotation.TryGetValue(toes, out int to)) Set(to, (footWorld.Inverse() * toesWorld).Normalized());
		}
	}

	/// <summary>Quanto acima do ponto mais baixo da bota em repouso ainda conta como sola (m).</summary>
	private const float SoleBand = 0.015f;

	/// <summary>
	/// A sola de um pé: os pontos da pele presos ao pé e aos dedos
	/// (<paramref name="side"/>), no máximo <see cref="SoleBand"/> acima do mais
	/// baixo deles em repouso, cada um no espaço do osso que mais o puxa.
	/// </summary>
	private static List<(int Bone, Vector3 Local)> Sole(Node bodyRoot, Skeleton3D body, Transform3D fb, string side)
	{
		var points = new List<(int Bone, Vector3 Local, float Height)>();
		if (bodyRoot == null) return new();
		foreach (MeshInstance3D mesh in bodyRoot.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (mesh.Mesh == null || mesh.Skin == null) continue;
			Skin skin = mesh.Skin;
			var bound = new int[skin.GetBindCount()];
			var foot = new bool[bound.Length];
			for (int k = 0; k < bound.Length; k++)
			{
				int bone = skin.GetBindBone(k);
				if (bone < 0) bone = body.FindBone(skin.GetBindName(k));
				bound[k] = bone;
				string name = bone >= 0 && bone < body.GetBoneCount() ? body.GetBoneName(bone) : "";
				foot[k] = name.Contains($"{side}Foot") || name.Contains($"{side}Toe");
			}
			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				Godot.Collections.Array arrays = mesh.Mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				if (vertices.Length == 0) continue;
				int per = bones.Length / vertices.Length;
				for (int v = 0; v < vertices.Length; v++)
				{
					int best = -1;
					float most = 0f;
					for (int j = 0; j < per; j++)
						if (weights[v * per + j] > most) { most = weights[v * per + j]; best = bones[v * per + j]; }
					if (best < 0 || best >= bound.Length || !foot[best] || bound[best] < 0) continue;
					Vector3 local = skin.GetBindPose(best) * vertices[v];
					float height = (fb * (body.GetBoneGlobalRest(bound[best]) * local)).Y;
					points.Add((bound[best], local, height));
				}
			}
		}
		if (points.Count == 0) return new();
		float lowest = points.Min(p => p.Height);
		return points.Where(p => p.Height < lowest + SoleBand).Select(p => (p.Bone, p.Local)).ToList();
	}

	/// <summary>A mesma rotação do lado da anterior (sem a volta inteira na interpolação).</summary>
	private static void Keep(ref Quaternion q, ref Quaternion? last)
	{
		if (last is Quaternion before && before.Dot(q) < 0f) q = -q;
		last = q;
	}

	/// <summary>
	/// A perna de dois ossos (coxa e canela, no espaço do esqueleto) levando o
	/// tornozelo a <paramref name="goal"/>, o joelho dobrando para o lado em
	/// que já dobrava. Devolve o giro local novo da coxa, da canela e do pé (o
	/// pé fica com o giro que tinha no mundo); nulo com a perna reta demais
	/// para saber o lado.
	/// </summary>
	private static (Quaternion, Quaternion, Quaternion)? Bend(Transform3D thigh, Transform3D shin, Transform3D foot,
		Vector3 goal, Quaternion parent, Quaternion? footTurned = null)
	{
		Vector3 h = thigh.Origin, k = shin.Origin, a = foot.Origin;
		float upper = h.DistanceTo(k), lower = k.DistanceTo(a);
		Vector3 toGoal = goal - h;
		float distance = toGoal.Length();
		if (upper < 1e-6f || lower < 1e-6f || distance < 1e-6f) return null;
		Vector3 direction = toGoal / distance;
		float slack = (upper + lower) * 1e-3f;
		float reach = Mathf.Clamp(distance, Mathf.Abs(upper - lower) + slack, upper + lower - slack);

		Vector3 side = (k - h) - direction * (k - h).Dot(direction);
		if (side.LengthSquared() < (upper * 1e-3f) * (upper * 1e-3f)) return null;
		side = side.Normalized();

		float cos = Mathf.Clamp((upper * upper + reach * reach - lower * lower) / (2f * upper * reach), -1f, 1f);
		float sin = Mathf.Sqrt(Mathf.Max(1f - cos * cos, 0f));
		Vector3 kneeTo = h + direction * (upper * cos) + side * (upper * sin);
		Vector3 ankleTo = h + direction * reach;

		Quaternion turnThigh = Arc((k - h).Normalized(), (kneeTo - h).Normalized());
		Vector3 ankleAfter = h + turnThigh * (a - h);
		Quaternion turnShin = Arc((ankleAfter - kneeTo).Normalized(), (ankleTo - kneeTo).Normalized());

		Quaternion thighWorld = (turnThigh * thigh.Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
		Quaternion shinWorld = (turnShin * turnThigh * shin.Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
		Quaternion footWorld = footTurned ?? foot.Basis.Orthonormalized().GetRotationQuaternion();
		return ((parent.Inverse() * thighWorld).Normalized(),
			(thighWorld.Inverse() * shinWorld).Normalized(),
			(shinWorld.Inverse() * footWorld).Normalized());
	}
}
