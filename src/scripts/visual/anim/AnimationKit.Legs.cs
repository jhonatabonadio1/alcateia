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
		Dictionary<string, List<(int Bone, Vector3 Local)>> soles = Soles(bodyRoot, body, fb);

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
			List<(int Bone, Vector3 Local)> sole = soles[side];

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

			WriteLeg(animation, tt, ts, tf, turned);
		}
	}

	/// <summary>Escreve as rotações da coxa, da canela e do pé chave a chave, cada uma do mesmo lado da
	/// anterior (sem a volta inteira na interpolação).</summary>
	private static void WriteLeg(Animation animation, int tt, int ts, int tf, (Quaternion, Quaternion, Quaternion)?[] turned)
	{
		Quaternion? lastThigh = null, lastShin = null, lastFoot = null;
		for (int key = 0; key < turned.Length; key++)
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

	/// <summary>
	/// O andar agachado com as pernas um pouco abertas (Jhon, 28/11, vendo-o de trás, as pernas bem juntas:
	/// "na pose anterior de andar agachado, somente abra um pouco para o lado a perna direita e um pouco para o
	/// outro lado a perna esquerda"): cada tornozelo vai <paramref name="meters"/> para fora do seu lado, o
	/// mesmo tanto nos dois e no passo inteiro — sem mudar de altura nem de ritmo —, e o joelho dobra para onde
	/// o pé aponta.
	/// </summary>
	/// <param name="stretch">Quantas vezes o passo fica mais comprido para a frente e para trás (em volta do meio
	/// do caminho do pé), no mesmo ritmo: o "Crouch Walking" anda menos que o corpo agachado (1,27 m/s), e o pé de
	/// apoio patinava (Jhon, 28/11: "corrija a passada agachada para deixar melhor, mais bem feito").</param>
	public static void OpenStride(Animation animation, Skeleton3D body, Transform3D fb, float meters, float stretch = 1f)
	{
		if (meters <= 0f && Mathf.IsEqualApprox(stretch, 1f)) return;
		var tracks = new BoneTracks(animation);
		Transform3D back = fb.AffineInverse();
		foreach (string side in new[] { "Left", "Right" })
		{
			string thigh = $"mixamorig_{side}UpLeg", shin = $"mixamorig_{side}Leg", foot = $"mixamorig_{side}Foot";
			int bt = body.FindBone(thigh), bs = body.FindBone(shin), bf = body.FindBone(foot), bo = body.FindBone($"mixamorig_{side}ToeBase");
			if (bt < 0 || bs < 0 || bf < 0 || bo < 0 || body.GetBoneParent(bs) != bt || body.GetBoneParent(bf) != bs) continue;
			if (!tracks.Rotation.TryGetValue(thigh, out int tt) || !tracks.Rotation.TryGetValue(shin, out int ts)
				|| !tracks.Rotation.TryGetValue(foot, out int tf)) continue;
			int keys = animation.TrackGetKeyCount(tt);
			if (keys == 0 || animation.TrackGetKeyCount(ts) != keys || animation.TrackGetKeyCount(tf) != keys) continue;

			// Para fora é o lado do pé no repouso; o passo cresce em volta do meio do caminho do pé.
			var push = new Vector3(Mathf.Sign((fb * body.GetBoneGlobalRest(bf).Origin).X) * meters, 0f, 0f);
			float middle = 0f;
			for (int key = 0; key < keys; key++)
				middle += (fb * PoseAt(animation, tracks, body, bf, animation.TrackGetKeyTime(tt, key)).Origin).Z / keys;
			int parent = body.GetBoneParent(bt);
			var turned = new (Quaternion, Quaternion, Quaternion)?[keys];
			for (int key = 0; key < keys; key++)
			{
				double time = animation.TrackGetKeyTime(tt, key);
				Transform3D up = PoseAt(animation, tracks, body, bt, time), knee = PoseAt(animation, tracks, body, bs, time);
				Transform3D ankle = PoseAt(animation, tracks, body, bf, time), toe = PoseAt(animation, tracks, body, bo, time);
				Vector3 along = fb * toe.Origin - fb * ankle.Origin;
				var pole = new Vector3(along.X, 0f, along.Z);
				if (pole.LengthSquared() < 1e-8f) pole = Vector3.Back;
				Quaternion parentWorld = parent < 0 ? Quaternion.Identity : PoseAt(animation, tracks, body, parent, time).Basis.Orthonormalized().GetRotationQuaternion();
				Vector3 at = fb * ankle.Origin;
				var reach = new Vector3(0f, 0f, (at.Z - middle) * (stretch - 1f));
				turned[key] = Bend(up, knee, ankle, back * (at + push + reach), parentWorld,
					ankle.Basis.Orthonormalized().GetRotationQuaternion(), back.Basis * pole.Normalized());
			}
			WriteLeg(animation, tt, ts, tf, turned);
			tracks = new BoneTracks(animation);
		}
	}

	/// <summary>A base do agachado parado (<see cref="CrouchStance"/>), em metros e graus.</summary>
	/// <param name="Front">O tornozelo da frente, à frente do quadril.</param>
	/// <param name="Rear">O tornozelo de trás, atrás do quadril.</param>
	/// <param name="Width">A distância de lado entre os dois tornozelos.</param>
	/// <param name="HipsRaise">Quanto o quadril sobe em relação ao instante do andar.</param>
	/// <param name="Back">Quanto o corpo inteiro (quadril e base) vai para trás da cápsula.</param>
	/// <param name="RearHeel">O calcanhar de trás erguido, em volta da base dos dedos.</param>
	/// <param name="FrontOut">Quanto a ponta do pé da frente abre para fora.</param>
	/// <param name="RearOut">Quanto a ponta do pé de trás abre para fora.</param>
	public readonly record struct Stance(float Front, float Rear, float Width, float HipsRaise, float Back,
		float RearHeel, float FrontOut, float RearOut);

	/// <summary>
	/// O agachado parado tático (Jhon, 28/11, vendo o agachado mais fundo de 27/11: "parece estar sentado
	/// em uma cadeira invisível... o quadril está muito deslocado para trás... a perna traseira está
	/// bastante comprimida... o ideal é criar uma base mais estável, com os joelhos acompanhando a direção
	/// dos pés"; aprovado na vitrine: "esses ficaram bons"). Sobre uma pose parada (<paramref name="pose"/>,
	/// a mesma chave do começo ao fim; o instante do andar agachado): o quadril vai para cima do meio da
	/// base; o pé da frente fica à frente dele, reto no chão; o de trás, atrás e na ponta (com ele reto,
	/// a canela deitava em cima do calcanhar da bota); os dois abertos de lado e com a ponta para fora; e
	/// cada joelho dobra para onde o seu pé aponta. A frente é +Z do modelo; o pé da frente é o que já
	/// estava à frente no instante. Até 28/11 era <c>FlatFeet</c>: os dois pés retos onde o andar os
	/// deixava, um 55 cm à frente e o outro embaixo do quadril.
	/// </summary>
	public static void CrouchStance(Animation pose, Skeleton3D body, Transform3D fb, Stance s)
	{
		var tracks = new BoneTracks(pose);
		Quaternion frame = fb.Basis.Orthonormalized().GetRotationQuaternion();
		Transform3D back = fb.AffineInverse();
		Quaternion Rest(int bone) => (frame * body.GetBoneGlobalRest(bone).Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
		int lf = body.FindBone("mixamorig_LeftFoot"), rf = body.FindBone("mixamorig_RightFoot");
		if (lf < 0 || rf < 0 || !tracks.Position.TryGetValue("mixamorig_Hips", out int hp)) return;
		string front = (fb * PoseAt(pose, tracks, body, lf, 0).Origin).Z > (fb * PoseAt(pose, tracks, body, rf, 0).Origin).Z ? "Left" : "Right";

		// O quadril, por cima do meio da base (e a base inteira Back para trás).
		Vector3 hips = fb * (Vector3)pose.TrackGetKeyValue(hp, 0);
		Vector3 shift = new Vector3(0f, hips.Y + s.HipsRaise, -s.Back) - hips;
		for (int key = 0; key < pose.TrackGetKeyCount(hp); key++)
			pose.TrackSetKeyValue(hp, key, back * (fb * (Vector3)pose.TrackGetKeyValue(hp, key) + shift));
		tracks = new BoneTracks(pose);

		foreach (string side in new[] { "Left", "Right" })
		{
			bool isFront = side == front;
			string thigh = $"mixamorig_{side}UpLeg", shin = $"mixamorig_{side}Leg", foot = $"mixamorig_{side}Foot", toes = $"mixamorig_{side}ToeBase";
			int bt = body.FindBone(thigh), bs = body.FindBone(shin), bf = body.FindBone(foot), bo = body.FindBone(toes);
			if (bt < 0 || bs < 0 || bf < 0 || bo < 0) continue;
			if (body.GetBoneParent(bs) != bt || body.GetBoneParent(bf) != bs || body.GetBoneParent(bo) != bf) continue;
			if (!tracks.Rotation.TryGetValue(thigh, out int tt) || !tracks.Rotation.TryGetValue(shin, out int ts)
				|| !tracks.Rotation.TryGetValue(foot, out int tf)) continue;
			Transform3D up = PoseAt(pose, tracks, body, bt, 0), knee = PoseAt(pose, tracks, body, bs, 0), ankle = PoseAt(pose, tracks, body, bf, 0);
			int parent = body.GetBoneParent(bt);
			Quaternion parentWorld = parent < 0 ? Quaternion.Identity : PoseAt(pose, tracks, body, parent, 0).Basis.Orthonormalized().GetRotationQuaternion();

			// Para onde o pé aponta: para a frente, aberto para o lado dele.
			float outside = Mathf.Sign((fb * body.GetBoneGlobalRest(bf).Origin).X);
			float open = outside * Mathf.DegToRad(isFront ? s.FrontOut : s.RearOut);
			var dir = new Vector3(Mathf.Sin(open), 0f, Mathf.Cos(open));
			Vector3 along = fb * body.GetBoneGlobalRest(bo).Origin - fb * body.GetBoneGlobalRest(bf).Origin;
			Quaternion heading = Arc(new Vector3(along.X, 0f, along.Z).Normalized(), dir);

			// O tornozelo na altura do repouso, à frente ou atrás do quadril, aberto de lado.
			var goal = new Vector3(outside * s.Width * 0.5f, (fb * body.GetBoneGlobalRest(bf).Origin).Y, -s.Back + (isFront ? s.Front : -s.Rear));
			Quaternion footWorld = (frame.Inverse() * heading * Rest(bf)).Normalized();
			Quaternion toesWorld = (frame.Inverse() * heading * Rest(bo)).Normalized();
			if (!isFront && s.RearHeel > 0f)
			{
				// Na ponta: o pé gira em volta da base dos dedos, que fica no chão.
				var pitch = new Quaternion(Vector3.Up.Cross(dir).Normalized(), Mathf.DegToRad(s.RearHeel));
				Vector3 ball = goal + heading * along;
				goal = ball + pitch * (goal - ball);
				footWorld = (frame.Inverse() * pitch * heading * Rest(bf)).Normalized();
			}

			if (Bend(up, knee, ankle, back * goal, parentWorld, footWorld, back.Basis * dir) is not var (qt, qs, qf)) continue;
			void Set(int track, Quaternion value)
			{
				for (int key = 0; key < pose.TrackGetKeyCount(track); key++) pose.TrackSetKeyValue(track, key, value);
			}
			Set(tt, qt);
			Set(ts, qs);
			Set(tf, qf);
			if (tracks.Rotation.TryGetValue(toes, out int to)) Set(to, (footWorld.Inverse() * toesWorld).Normalized());
			tracks = new BoneTracks(pose);
		}
	}

	/// <summary>A pele dos dois pés do corpo, a bota inteira (cada ponto e o osso dele), para medir na pose de
	/// agora: na ponta do pé, a ponta da bota é que desce.</summary>
	public static List<(int Bone, Vector3 Local)> FootPoints(Node bodyRoot, Skeleton3D body)
	{
		var all = new List<(int Bone, Vector3 Local)>();
		foreach (var points in Soles(bodyRoot, body, ChainTo(body, bodyRoot), float.MaxValue).Values) all.AddRange(points);
		return all;
	}

	/// <summary>Quanto acima do ponto mais baixo da bota em repouso ainda conta como sola (m).</summary>
	private const float SoleBand = 0.015f;

	/// <summary>
	/// A sola de cada pé ("Left", "Right"): os pontos da pele presos ao pé e
	/// aos dedos, no máximo <see cref="SoleBand"/> acima do mais baixo deles em
	/// repouso, cada um no espaço do osso que mais o puxa. Lê as malhas uma vez
	/// para os dois pés e solta cada arranjo do Godot assim que usa: são alguns
	/// MB por malha, e deixá-los para o coletor (duas vezes, um por pé) mudava a
	/// hora em que ele passa e derrubava a saída de um boneco no merge de 26/11
	/// ("Handle is not initialized", BonecoMaos).
	/// </summary>
	private static Dictionary<string, List<(int Bone, Vector3 Local)>> Soles(Node bodyRoot, Skeleton3D body, Transform3D fb,
		float band = SoleBand)
	{
		var found = new Dictionary<string, List<(int Bone, Vector3 Local, float Height)>> { ["Left"] = new(), ["Right"] = new() };
		if (bodyRoot != null)
		{
			Godot.Collections.Array<Node> meshes = bodyRoot.FindChildren("*", "MeshInstance3D", true, false);
			foreach (Node node in meshes)
			{
				if (node is not MeshInstance3D mesh || mesh.Mesh == null || mesh.Skin == null) continue;
				Skin skin = mesh.Skin;
				int binds = skin.GetBindCount();
				var bound = new int[binds];
				var sideOf = new string[binds];
				var pose = new Transform3D[binds];
				for (int k = 0; k < binds; k++)
				{
					int bone = skin.GetBindBone(k);
					if (bone < 0) bone = body.FindBone(skin.GetBindName(k));
					bound[k] = bone;
					if (bone < 0 || bone >= body.GetBoneCount()) continue;
					string name = body.GetBoneName(bone);
					foreach (string side in found.Keys)
						if (name.Contains($"{side}Foot") || name.Contains($"{side}Toe")) sideOf[k] = side;
					if (sideOf[k] == null) continue;
					pose[k] = skin.GetBindPose(k);
				}
				for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
				{
					using Godot.Collections.Array arrays = mesh.Mesh.SurfaceGetArrays(s);
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
						if (best < 0 || best >= binds || sideOf[best] == null) continue;
						Vector3 local = pose[best] * vertices[v];
						float height = (fb * (body.GetBoneGlobalRest(bound[best]) * local)).Y;
						found[sideOf[best]].Add((bound[best], local, height));
					}
				}
			}
		}
		var soles = new Dictionary<string, List<(int Bone, Vector3 Local)>>();
		foreach (var (side, points) in found)
		{
			float lowest = points.Count > 0 ? points.Min(p => p.Height) : 0f;
			soles[side] = points.Where(p => p.Height < lowest + band).Select(p => (p.Bone, p.Local)).ToList();
		}
		return soles;
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
		Vector3 goal, Quaternion parent, Quaternion? footTurned = null, Vector3? pole = null)
	{
		Vector3 h = thigh.Origin, k = shin.Origin, a = foot.Origin;
		float upper = h.DistanceTo(k), lower = k.DistanceTo(a);
		Vector3 toGoal = goal - h;
		float distance = toGoal.Length();
		if (upper < 1e-6f || lower < 1e-6f || distance < 1e-6f) return null;
		Vector3 direction = toGoal / distance;
		float slack = (upper + lower) * 1e-3f;
		float reach = Mathf.Clamp(distance, Mathf.Abs(upper - lower) + slack, upper + lower - slack);

		// O joelho vai para o lado em que já estava, ou para o pole (o agachado: para onde o pé aponta).
		Vector3 bentFrom = pole ?? (k - h);
		Vector3 side = bentFrom - direction * bentFrom.Dot(direction);
		if (side.LengthSquared() < (upper * 1e-3f) * (upper * 1e-3f)) return null;
		side = side.Normalized();

		float cos = Mathf.Clamp((upper * upper + reach * reach - lower * lower) / (2f * upper * reach), -1f, 1f);
		float sin = Mathf.Sqrt(Mathf.Max(1f - cos * cos, 0f));
		Vector3 kneeTo = h + direction * (upper * cos) + side * (upper * sin);
		Vector3 ankleTo = h + direction * reach;

		Quaternion turnThigh = Arc((k - h).Normalized(), (kneeTo - h).Normalized());
		Vector3 ankleAfter = h + turnThigh * (a - h);
		if (pole != null)
		{
			// Com o pole a coxa ainda gira em volta de si até a canela dobrar para o lado dele: o joelho olha
			// para onde o pé aponta, sem torcer.
			Vector3 axis = (kneeTo - h).Normalized();
			Vector3 now = (ankleAfter - kneeTo) - axis * (ankleAfter - kneeTo).Dot(axis);
			Vector3 want = (ankleTo - kneeTo) - axis * (ankleTo - kneeTo).Dot(axis);
			if (now.LengthSquared() > 1e-8f && want.LengthSquared() > 1e-8f)
			{
				turnThigh = Arc(now.Normalized(), want.Normalized()) * turnThigh;
				ankleAfter = h + turnThigh * (a - h);
			}
		}
		Quaternion turnShin = Arc((ankleAfter - kneeTo).Normalized(), (ankleTo - kneeTo).Normalized());

		Quaternion thighWorld = (turnThigh * thigh.Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
		Quaternion shinWorld = (turnShin * turnThigh * shin.Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
		Quaternion footWorld = footTurned ?? foot.Basis.Orthonormalized().GetRotationQuaternion();
		return ((parent.Inverse() * thighWorld).Normalized(),
			(thighWorld.Inverse() * shinWorld).Normalized(),
			(shinWorld.Inverse() * footWorld).Normalized());
	}
}
