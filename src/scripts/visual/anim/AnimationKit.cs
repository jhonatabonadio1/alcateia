using Godot;
using System.Collections.Generic;

/// <summary>
/// Ferramentas de animação que o humano (<see cref="CharacterAnimator"/>),
/// o lobo (<see cref="WolfVisual"/>) e a vitrine do lobby usam: tirar o
/// clipe certo de um FBX do Mixamo, matar o deslocamento da raiz, ajustar
/// o quadril a outro corpo, achar o esqueleto, filtrar a metade de cima do
/// corpo e ligar estados da máquina. Antes cada um tinha a sua cópia.
/// </summary>
public static partial class AnimationKit
{
	/// <summary>
	/// A animação de verdade do tocador de origem.
	///
	/// Cada FBX importado traz duas: o clipe do Mixamo e um "Take 001"
	/// que o importador deixa junto. Eu escolhia pela **duração**, e
	/// esse foi o erro: quando o take vazio é o mais longo, carrega-se
	/// um clipe sem osso nenhum. Clipe sem trilha não dá erro, não
	/// avisa e não move nada — o esqueleto fica na pose de repouso, e
	/// a pose de repouso de um rig humanoide é o T.
	///
	/// Escolher pelo número de trilhas também não resolve: nos
	/// arquivos da segunda leva o "Take 001" tem 53 trilhas de 3,33 s
	/// — trilhas existem, mas não animam, e todas as quatro vítimas
	/// tinham exatamente essa assinatura.
	///
	/// O nome é o sinal confiável: o clipe do Mixamo se chama
	/// "mixamo_com" e ganha de qualquer outro. Trilhas e duração
	/// viram desempate para arquivos de outra procedência.
	/// </summary>
	public static Animation TakeAnimation(AnimationPlayer source, string clip, bool debug)
	{
		if (source == null) return null;

		Animation best = null;
		int mostTracks = 0;
		double longest = 0;
		string chosen = "";

		foreach (StringName entry in source.GetAnimationList())
		{
			Animation candidate = source.GetAnimation(entry);
			if (candidate == null) continue;

			string name = entry.ToString();
			int tracks = candidate.GetTrackCount();

			// O clipe do Mixamo ganha de qualquer outro, sempre. Entre
			// os demais, mais trilhas e depois mais longo.
			bool isMixamo = name.ToLowerInvariant().Contains("mixamo");
			bool chosenIsMixamo = chosen.ToLowerInvariant().Contains("mixamo");

			bool better = best == null
				|| (isMixamo && !chosenIsMixamo)
				|| (isMixamo == chosenIsMixamo
					&& (tracks > mostTracks
						|| (tracks == mostTracks && candidate.Length > longest)));

			if (!better) continue;

			mostTracks = tracks;
			longest = candidate.Length;
			best = candidate;
			chosen = name;
		}

		if (debug && mostTracks == 0)
			GD.PrintErr($"Animation: '{clip}' has no tracks — it will T-pose.");
		else if (debug)
			GD.Print($"  {clip}: '{chosen}', {mostTracks} tracks, {longest:0.00}s");

		return best?.Duplicate() as Animation;
	}

	/// <summary>
	/// Mata o deslocamento horizontal da raiz, preservando o vertical.
	///
	/// Oito dos clipes andam de verdade: a caminhada viaja 1,36 m em
	/// 1,37 s, a corrida 2,21 m em 0,70 s. Como o CharacterBody3D já
	/// calcula o deslocamento, deixar isso ligado soma dois movimentos
	/// e o boneco patina.
	///
	/// O eixo Y fica: é ele que dá o peso do passo e os 32 cm de
	/// subida do pulo. Por isso as chaves são achatadas em X e Z em
	/// vez da trilha ser removida.
	///
	/// O osso é encontrado por busca, e não por nome fixo, porque o
	/// importador reescreve nome de osso e o caminho exato depende de
	/// uma opção de importação que ninguém vai lembrar de conferir.
	/// </summary>
	public static void StripRootMotion(Animation animation)
	{
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Position3D)
				continue;

			if (!animation.TrackGetPath(track).ToString().Contains("Hips"))
				continue;

			int keys = animation.TrackGetKeyCount(track);
			if (keys == 0) continue;

			var first = (Vector3)animation.TrackGetKeyValue(track, 0);

			for (int key = 0; key < keys; key++)
			{
				var value = (Vector3)animation.TrackGetKeyValue(track, key);
				animation.TrackSetKeyValue(track, key,
					new Vector3(first.X, value.Y, first.Z));
			}
		}
	}

	/// <summary>
	/// Leva a trilha do quadril de um clipe feito para outro corpo à
	/// altura deste: multiplica a posição do quadril pela razão entre os
	/// dois quadris em repouso. Os ossos giram igual em qualquer corpo; é
	/// só a posição do quadril que vem em metros do corpo original, e é
	/// ela que deixa os pés no ar (ou enterrados) quando as pernas mudam.
	/// </summary>
	public static void MatchHips(Animation animation, float ratio)
	{
		if (Mathf.IsEqualApprox(ratio, 1f)) return;

		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Position3D) continue;
			if (!animation.TrackGetPath(track).ToString().Contains("Hips")) continue;

			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
				animation.TrackSetKeyValue(track, key, (Vector3)animation.TrackGetKeyValue(track, key) * ratio);
		}
	}

	/// <summary>
	/// Prende no lugar os ossos que o clipe não anima: cada osso do corpo
	/// que não tem trilha de giro ganha uma, parada no repouso do esqueleto
	/// do próprio clipe.
	///
	/// Sem isso, o osso sem trilha cai no repouso do corpo que toca o
	/// clipe. Nos clipes do Mixamo é o mesmo T dos dois lados e não muda
	/// nada; num clipe salvo com a pose como repouso (a corrida de quatro
	/// do Jhon, editada no Blender), os ombros iam para o T do Mutant e os
	/// braços saíam do lugar.
	/// </summary>
	public static void KeepClipRest(Animation animation, Skeleton3D clip, Skeleton3D body)
	{
		if (clip == null || body == null) return;

		string prefix = null;
		var rotated = new HashSet<string>();
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			string path = animation.TrackGetPath(track).ToString();
			int colon = path.LastIndexOf(':');
			if (colon < 0) continue;
			prefix ??= path[..colon];
			if (animation.TrackGetType(track) == Animation.TrackType.Rotation3D)
				rotated.Add(path[(colon + 1)..]);
		}
		if (prefix == null) return;

		for (int bone = 0; bone < clip.GetBoneCount(); bone++)
		{
			string name = clip.GetBoneName(bone);
			if (rotated.Contains(name) || body.FindBone(name) < 0) continue;

			int track = animation.AddTrack(Animation.TrackType.Rotation3D);
			animation.TrackSetPath(track, $"{prefix}:{name}");
			animation.RotationTrackInsertKey(track, 0, clip.GetBoneRest(bone).Basis.GetRotationQuaternion());
		}
	}

	/// <summary>Osso principal de cada osso do corpo: a direção dele é a do
	/// osso até este filho (as pontas herdam a do pai).</summary>
	private static readonly Dictionary<string, string> MainChild = BuildMainChild();

	private static Dictionary<string, string> BuildMainChild()
	{
		var map = new Dictionary<string, string>
		{
			["mixamorig_Hips"] = "mixamorig_Spine",
			["mixamorig_Spine"] = "mixamorig_Spine1",
			["mixamorig_Spine1"] = "mixamorig_Spine2",
			["mixamorig_Spine2"] = "mixamorig_Neck",
			["mixamorig_Neck"] = "mixamorig_Head",
			["mixamorig_Head"] = "mixamorig_HeadTop_End"
		};
		foreach (string side in new[] { "Left", "Right" })
		{
			map[$"mixamorig_{side}Shoulder"] = $"mixamorig_{side}Arm";
			map[$"mixamorig_{side}Arm"] = $"mixamorig_{side}ForeArm";
			map[$"mixamorig_{side}ForeArm"] = $"mixamorig_{side}Hand";
			map[$"mixamorig_{side}UpLeg"] = $"mixamorig_{side}Leg";
			map[$"mixamorig_{side}Leg"] = $"mixamorig_{side}Foot";
			map[$"mixamorig_{side}Foot"] = $"mixamorig_{side}ToeBase";
		}
		return map;
	}

	/// <summary>
	/// Leva um clipe do Mixamo para um corpo de outro esqueleto (mesmos
	/// nomes de osso, repouso diferente): o Meshy entrega o lobo em A
	/// (braços caídos) e com o eixo de cada osso girado de um jeito, e os
	/// clipes são do T do Mutant.
	///
	/// Em vez de copiar o giro local, mantém a **direção de cada osso no
	/// mundo**: o giro que o osso faz do repouso para a pose no esqueleto
	/// do clipe (<paramref name="reference"/>, em T) vale também para o
	/// osso do corpo, depois de ele ser girado do repouso dele para o
	/// repouso do clipe (o arco mais curto entre as duas direções do osso).
	/// Reamostra a 30 quadros por segundo. Ossos do clipe que o corpo não
	/// tem (dedos) saem; ossos do corpo sem osso no clipe ficam parados.
	/// </summary>
	/// <param name="referenceFrame">Giro do esqueleto do clipe até o espaço
	/// do modelo (o "Armature" de um GLB do Blender gira 90° e vale 0,01):
	/// sem ele, clipes de um arquivo com outra orientação ficam deitados.</param>
	/// <param name="bodyFrame">O mesmo para o esqueleto do corpo.</param>
	/// <param name="keepRest">Ossos que não copiam a direção do osso do clipe:
	/// o giro que o clipe faz a partir do repouso dele vale a partir do
	/// repouso do corpo (o osso parado no clipe fica parado no repouso do
	/// corpo). É o caso do pé e da mão do lobo: a sola foi desenhada para o
	/// repouso do corpo, e o pé do esqueleto dos clipes do Jhon aponta mais
	/// para baixo — copiando a direção dele, a ponta das garras entrava de 6
	/// a 18 cm no chão em toda pose (alpha fechado, 26/11).</param>
	public static void RetargetPose(Animation animation, Skeleton3D reference, Skeleton3D body,
		Quaternion? referenceFrame = null, Quaternion? bodyFrame = null, ICollection<string> keepRest = null)
	{
		if (reference == null || body == null) return;
		Quaternion fr = (referenceFrame ?? Quaternion.Identity).Normalized();
		Quaternion fb = (bodyFrame ?? Quaternion.Identity).Normalized();

		// Os giros originais: o clipe é lido desta cópia, antes de apagar chaves.
		Animation source = (Animation)animation.Duplicate();

		int Find(Skeleton3D s, string name) => s.FindBone(name);
		var refRest = new Quaternion[reference.GetBoneCount()];
		for (int i = 0; i < refRest.Length; i++)
			refRest[i] = (fr * reference.GetBoneGlobalRest(i).Basis.GetRotationQuaternion()).Normalized();
		var bodyRest = new Quaternion[body.GetBoneCount()];
		for (int i = 0; i < bodyRest.Length; i++)
			bodyRest[i] = (fb * body.GetBoneGlobalRest(i).Basis.GetRotationQuaternion()).Normalized();

		// Do repouso do corpo para o do clipe, osso a osso.
		var delta = new Quaternion[body.GetBoneCount()];
		var done = new bool[body.GetBoneCount()];
		Quaternion DeltaOf(int bone)
		{
			if (done[bone]) return delta[bone];
			done[bone] = true;
			string name = body.GetBoneName(bone);
			Quaternion result = Quaternion.Identity;
			int refBone = Find(reference, name);
			if (keepRest != null && keepRest.Contains(name))
				return delta[bone] = result;
			if (refBone >= 0 && MainChild.TryGetValue(name, out string child)
				&& Find(body, child) is int bc && bc >= 0 && Find(reference, child) is int rc && rc >= 0)
			{
				Vector3 from = fb * (body.GetBoneGlobalRest(bc).Origin - body.GetBoneGlobalRest(bone).Origin).Normalized();
				Vector3 to = fr * (reference.GetBoneGlobalRest(rc).Origin - reference.GetBoneGlobalRest(refBone).Origin).Normalized();
				result = Arc(from, to);
			}
			else
			{
				int parent = body.GetBoneParent(bone);
				if (parent >= 0) result = DeltaOf(parent);
			}
			return delta[bone] = result;
		}
		for (int i = 0; i < body.GetBoneCount(); i++) DeltaOf(i);

		// Trilhas de giro do clipe, por osso; o resto do que o corpo não tem sai.
		string prefix = null;
		var tracks = new Dictionary<string, int>();
		for (int track = animation.GetTrackCount() - 1; track >= 0; track--)
		{
			string path = animation.TrackGetPath(track).ToString();
			int colon = path.LastIndexOf(':');
			if (colon < 0) continue;
			prefix ??= path[..colon];
			string bone = path[(colon + 1)..];
			if (body.FindBone(bone) < 0) { animation.RemoveTrack(track); continue; }
			if (animation.TrackGetType(track) == Animation.TrackType.Rotation3D) tracks[bone] = track;
		}
		if (prefix == null) return;

		// Osso do corpo que o clipe não anima: trilha própria (a do repouso).
		var driven = new List<int>();
		for (int bone = 0; bone < body.GetBoneCount(); bone++)
		{
			string name = body.GetBoneName(bone);
			if (Find(reference, name) < 0) continue;
			driven.Add(bone);
			if (!tracks.ContainsKey(name))
			{
				int track = animation.AddTrack(Animation.TrackType.Rotation3D);
				animation.TrackSetPath(track, $"{prefix}:{name}");
				tracks[name] = track;
			}
			else
			{
				int track = tracks[name];
				while (animation.TrackGetKeyCount(track) > 0) animation.TrackRemoveKey(track, 0);
			}
		}

		var sourceTracks = new Dictionary<string, int>();
		for (int track = 0; track < source.GetTrackCount(); track++)
		{
			if (source.TrackGetType(track) != Animation.TrackType.Rotation3D) continue;
			string path = source.TrackGetPath(track).ToString();
			sourceTracks[path[(path.LastIndexOf(':') + 1)..]] = track;
		}

		int frames = Mathf.Max(1, Mathf.CeilToInt((float)animation.Length * 30f));
		var previous = new Dictionary<int, Quaternion>();
		for (int f = 0; f <= frames; f++)
		{
			double time = f == frames ? animation.Length : f / 30.0;

			var sMemo = new Quaternion?[reference.GetBoneCount()];
			Quaternion SGlobal(int bone)
			{
				if (sMemo[bone] is Quaternion cached) return cached;
				Quaternion local = sourceTracks.TryGetValue(reference.GetBoneName(bone), out int t)
					&& source.TrackGetKeyCount(t) > 0
					? source.RotationTrackInterpolate(t, time).Normalized()
					: reference.GetBoneRest(bone).Basis.GetRotationQuaternion().Normalized();
				int parent = reference.GetBoneParent(bone);
				Quaternion global = parent < 0 ? local : SGlobal(parent) * local;
				global = global.Normalized();
				sMemo[bone] = global;
				return global;
			}

			var nMemo = new Quaternion?[body.GetBoneCount()];
			Quaternion NGlobal(int bone)
			{
				if (nMemo[bone] is Quaternion cached) return cached;
				int refBone = Find(reference, body.GetBoneName(bone));
				Quaternion global;
				if (refBone >= 0)
					global = fb.Inverse() * (fr * SGlobal(refBone)) * refRest[refBone].Inverse() * delta[bone] * bodyRest[bone];
				else
				{
					int parent = body.GetBoneParent(bone);
					Quaternion rest = body.GetBoneRest(bone).Basis.GetRotationQuaternion();
					global = parent < 0 ? rest : NGlobal(parent) * rest;
				}
				global = global.Normalized();
				nMemo[bone] = global;
				return global;
			}

			foreach (int bone in driven)
			{
				int parent = body.GetBoneParent(bone);
				Quaternion local = parent < 0 ? NGlobal(bone) : NGlobal(parent).Inverse() * NGlobal(bone);
				local = local.Normalized();
				if (previous.TryGetValue(bone, out Quaternion before) && before.Dot(local) < 0f) local = -local;
				previous[bone] = local;
				animation.RotationTrackInsertKey(tracks[body.GetBoneName(bone)], time, local);
			}
		}
	}

	/// <summary>Transformação de <paramref name="from"/> até (sem incluir)
	/// <paramref name="stop"/>, somando os nós do caminho. Funciona fora da
	/// árvore, onde não há transformação global.</summary>
	public static Transform3D ChainTo(Node from, Node stop)
	{
		Transform3D t = Transform3D.Identity;
		for (Node n = from; n != null && n != stop; n = n.GetParent())
			if (n is Node3D n3) t = n3.Transform * t;
		return t;
	}

	/// <summary>
	/// O corpo é do mesmo rig do clipe (mesmo repouso de cada osso, mesma
	/// orientação do esqueleto no modelo)? É o caso do X Bot, da Ely e do
	/// Exo Gray, todos do Mixamo: o clipe serve como está. Um corpo de
	/// outra procedência com os nomes do Mixamo (os personagens do pacote
	/// Sci-Fi, 19/11) tem outro repouso e precisa de <see cref="FitToBody"/>.
	/// </summary>
	public static bool SameRig(Skeleton3D body, Node bodyRoot, Skeleton3D clip, Node clipRoot)
	{
		if (body == null || clip == null) return true;
		Quaternion fb = ChainTo(body, bodyRoot).Basis.Orthonormalized().GetRotationQuaternion();
		Quaternion fc = ChainTo(clip, clipRoot).Basis.Orthonormalized().GetRotationQuaternion();
		if (fb.AngleTo(fc) > 0.035f) return false;
		foreach (string name in new[] { "mixamorig_Hips", "mixamorig_Spine", "mixamorig_LeftArm", "mixamorig_RightForeArm", "mixamorig_LeftUpLeg", "mixamorig_RightHand" })
		{
			int a = body.FindBone(name), b = clip.FindBone(name);
			if (a < 0 || b < 0) return false;
			if (body.GetBoneRest(a).Basis.GetRotationQuaternion().AngleTo(clip.GetBoneRest(b).Basis.GetRotationQuaternion()) > 0.035f)
				return false;
		}
		return true;
	}

	/// <summary>
	/// Leva um clipe do Mixamo a um corpo de outro rig com os mesmos nomes
	/// de osso: aponta as trilhas para o esqueleto do corpo
	/// (<paramref name="bodyPath"/>, a partir da raiz do tocador), passa o
	/// quadril para o espaço e a altura do corpo e reorienta cada osso por
	/// <see cref="RetargetPose"/> (a direção no mundo, não o giro local).
	/// Só o quadril anda; escala e posição dos outros ossos saem. O mesmo
	/// ajuste que o lobo faz com os clipes do Jhon (WolfOwnClips.Fit).
	///
	/// O pé não copia a direção do osso: copia o giro a partir do repouso
	/// (<see cref="FootBones"/>). Com <paramref name="reachFloor"/>, as pernas
	/// ainda se ajustam para o tornozelo pousar onde o do clipe pousa
	/// (<see cref="ReachFloor"/>).
	/// </summary>
	/// <param name="sink">Metros a mais que o quadril desce (o agachado mais baixo que o do clipe, 27/11): as
	/// pernas dobram até o pé pousar no mesmo lugar (só com <paramref name="reachFloor"/>).</param>
	public static void FitToBody(Animation animation, Node clipRoot, Skeleton3D body, Node bodyRoot, string bodyPath,
		bool reachFloor = false, float sink = 0f)
	{
		Skeleton3D reference = FindSkeleton(clipRoot);
		if (reference == null || body == null) return;
		int refHips = reference.FindBone("mixamorig_Hips");
		int bodyHips = body.FindBone("mixamorig_Hips");
		// O clipe como veio, para saber onde o tornozelo dele pousa.
		Animation source = reachFloor ? (Animation)animation.Duplicate() : null;

		for (int track = animation.GetTrackCount() - 1; track >= 0; track--)
		{
			Animation.TrackType type = animation.TrackGetType(track);
			bool hips = animation.TrackGetPath(track).ToString().Contains("Hips");
			if (type == Animation.TrackType.Scale3D || (type == Animation.TrackType.Position3D && !hips))
				animation.RemoveTrack(track);
		}
		PointAt(animation, bodyPath);

		Transform3D fr = ChainTo(reference, clipRoot);
		Transform3D fb = ChainTo(body, bodyRoot);

		if (refHips >= 0 && bodyHips >= 0)
			MoveHips(animation, reference, refHips, fr, body, bodyHips, fb);
		if (sink > 0f && source != null) SinkHips(animation, fb, sink);

		RetargetPose(animation, reference, body,
			fr.Basis.Orthonormalized().GetRotationQuaternion(), fb.Basis.Orthonormalized().GetRotationQuaternion(),
			FootBones);

		if (source != null) ReachFloor(animation, source, reference, fr, body, bodyRoot, fb);
	}

	/// <summary>
	/// Inclina ossos para trás (a cabeça e o pescoço), em graus, girando cada
	/// um em volta do eixo lateral do corpo (+X do modelo, visto no repouso
	/// do osso). Ângulo positivo leva o alto do osso para as costas.
	/// </summary>
	public static void TiltBack(Animation animation, Skeleton3D body, Node bodyRoot, string[] bones, float degrees)
	{
		if (body == null || Mathf.IsZeroApprox(degrees)) return;
		Quaternion frame = ChainTo(body, bodyRoot).Basis.Orthonormalized().GetRotationQuaternion();
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Rotation3D) continue;
			string path = animation.TrackGetPath(track).ToString();
			string name = path[(path.LastIndexOf(':') + 1)..];
			if (System.Array.IndexOf(bones, name) < 0) continue;
			int bone = body.FindBone(name);
			if (bone < 0) continue;
			Quaternion rest = (frame * body.GetBoneGlobalRest(bone).Basis.GetRotationQuaternion()).Normalized();
			Vector3 axis = (rest.Inverse() * Vector3.Right).Normalized();
			var tilt = new Quaternion(axis, Mathf.DegToRad(-degrees));
			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
			{
				var q = (Quaternion)animation.TrackGetKeyValue(track, key);
				animation.TrackSetKeyValue(track, key, (q * tilt).Normalized());
			}
		}
	}

	/// <summary>
	/// As trilhas de posição (o quadril) do espaço do clipe
	/// (<paramref name="fr"/>, o esqueleto de referência) para o do corpo
	/// (<paramref name="fb"/>), na razão das alturas do quadril: o corpo mais
	/// baixo anda e pula proporcionalmente. A posição da trilha é local ao pai
	/// do quadril; o pai é a raiz do esqueleto nos dois, então o espaço do
	/// esqueleto serve. O humano e os clipes do lobo do Jhon (WolfOwnClips)
	/// usam.
	/// </summary>
	public static void MoveHips(Animation animation, Skeleton3D reference, int refHips, Transform3D fr,
		Skeleton3D body, int bodyHips, Transform3D fb)
	{
		float ratio = (fb * body.GetBoneGlobalRest(bodyHips).Origin).Y
			/ Mathf.Max((fr * reference.GetBoneGlobalRest(refHips).Origin).Y, 1e-4f);
		Transform3D back = fb.AffineInverse();
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Position3D) continue;
			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
				animation.TrackSetKeyValue(track, key, back * ((fr * (Vector3)animation.TrackGetKeyValue(track, key)) * ratio));
		}
	}

	/// <summary>O giro mais curto que leva a direção <paramref name="from"/> à
	/// <paramref name="to"/> (as duas unitárias). Usado também pela HeroPose.</summary>
	public static Quaternion Arc(Vector3 from, Vector3 to)
	{
		float dot = Mathf.Clamp(from.Dot(to), -1f, 1f);
		if (dot > 0.99999f) return Quaternion.Identity;
		Vector3 axis = from.Cross(to);
		if (axis.LengthSquared() < 1e-10f)
			axis = from.Cross(Mathf.Abs(from.X) < 0.9f ? Vector3.Right : Vector3.Up);
		return new Quaternion(axis.Normalized(), Mathf.Acos(dot));
	}

	/// <summary>Faz cada trilha de osso apontar para o esqueleto em
	/// <paramref name="skeleton"/> (o caminho dele a partir da raiz do
	/// tocador), seja qual for o caminho com que o arquivo veio.</summary>
	public static void PointAt(Animation animation, string skeleton)
	{
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			string path = animation.TrackGetPath(track).ToString();
			int colon = path.LastIndexOf(':');
			if (colon >= 0) animation.TrackSetPath(track, $"{skeleton}:{path[(colon + 1)..]}");
		}
	}

	/// <summary>Desce o quadril <paramref name="meters"/> (no mundo do corpo) em todas as chaves.</summary>
	private static void SinkHips(Animation animation, Transform3D fb, float meters)
	{
		Vector3 down = (fb.Basis.Inverse() * (Vector3.Down * meters));
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Position3D) continue;
			if (!animation.TrackGetPath(track).ToString().Contains("Hips")) continue;
			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
				animation.TrackSetKeyValue(track, key, (Vector3)animation.TrackGetKeyValue(track, key) + down);
		}
	}

	/// <summary>Altura do quadril em repouso no esqueleto de uma cena, ou 0.</summary>
	public static float HipsRestHeight(Node root)
	{
		Skeleton3D skeleton = FindSkeleton(root);
		if (skeleton == null) return 0f;

		int hips = skeleton.FindBone("mixamorig_Hips");
		return hips < 0 ? 0f : skeleton.GetBoneGlobalRest(hips).Origin.Y;
	}

	public static Skeleton3D FindSkeleton(Node root) => Nodes.FindFirst<Skeleton3D>(root);

	public static AnimationPlayer FindPlayer(Node root) => Nodes.FindFirst<AnimationPlayer>(root);

	/// <summary>A trilha é de um osso da metade de cima (coluna, pescoço,
	/// cabeça, ombros, braços, mãos)? É o que deixa recarregar, jogar granada
	/// e mirar agachado por cima das pernas, que seguem andando.</summary>
	public static bool IsUpperBody(string path)
	{
		int colon = path.LastIndexOf(':');
		string bone = colon >= 0 ? path[(colon + 1)..] : path;
		return bone.Contains("Spine") || bone.Contains("Neck") || bone.Contains("Head")
			|| bone.Contains("Shoulder") || bone.Contains("Arm") || bone.Contains("Hand");
	}

	/// <summary>Deixa passar pelo nó só as trilhas da metade de cima do
	/// corpo neste clipe.</summary>
	public static void FilterUpperBody(AnimationNode node, Animation clip)
	{
		for (int track = 0; track < clip.GetTrackCount(); track++)
		{
			NodePath path = clip.TrackGetPath(track);
			if (IsUpperBody(path.ToString())) node.SetFilterPath(path, true);
		}
	}

	/// <summary>Transição entre dois estados da máquina (se os dois
	/// existirem). <paramref name="auto"/>: volta sozinha quando o clipe
	/// acaba.</summary>
	public static void Link(AnimationNodeStateMachine machine,
		string from, string to, bool auto = false)
	{
		if (!machine.HasNode(from) || !machine.HasNode(to)) return;

		// Volta automática espera o clipe acabar; ida é imediata,
		// porque pulo que começa meio segundo depois do salto não é
		// animação, é atraso.
		var transition = new AnimationNodeStateMachineTransition
		{
			XfadeTime = 0.15f,
			SwitchMode = auto
				? AnimationNodeStateMachineTransition.SwitchModeEnum.AtEnd
				: AnimationNodeStateMachineTransition.SwitchModeEnum.Immediate,
			AdvanceMode = auto
				? AnimationNodeStateMachineTransition.AdvanceModeEnum.Auto
				: AnimationNodeStateMachineTransition.AdvanceModeEnum.Enabled
		};

		machine.AddTransition(from, to, transition);
	}
}
