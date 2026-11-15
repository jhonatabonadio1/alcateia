using Godot;
using System.Collections.Generic;

/// <summary>
/// Ferramentas de animação que o humano (<see cref="CharacterAnimator"/>),
/// o lobo (<see cref="WolfVisual"/>) e a vitrine do lobby usam: tirar o
/// clipe certo de um FBX do Mixamo, matar o deslocamento da raiz, ajustar
/// o quadril a outro corpo, achar o esqueleto, filtrar a metade de cima do
/// corpo e ligar estados da máquina. Antes cada um tinha a sua cópia.
/// </summary>
public static class AnimationKit
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
			GD.PrintErr($"Animação: '{clip}' não traz trilha nenhuma — vai dar pose em T.");
		else if (debug)
			GD.Print($"  {clip}: '{chosen}', {mostTracks} trilhas, {longest:0.00}s");

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
	public static void RetargetPose(Animation animation, Skeleton3D reference, Skeleton3D body)
	{
		if (reference == null || body == null) return;

		// Os giros originais: o clipe é lido desta cópia, antes de apagar chaves.
		Animation source = (Animation)animation.Duplicate();

		int Find(Skeleton3D s, string name) => s.FindBone(name);
		var refRest = new Quaternion[reference.GetBoneCount()];
		for (int i = 0; i < refRest.Length; i++)
			refRest[i] = reference.GetBoneGlobalRest(i).Basis.GetRotationQuaternion().Normalized();
		var bodyRest = new Quaternion[body.GetBoneCount()];
		for (int i = 0; i < bodyRest.Length; i++)
			bodyRest[i] = body.GetBoneGlobalRest(i).Basis.GetRotationQuaternion().Normalized();

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
			if (refBone >= 0 && MainChild.TryGetValue(name, out string child)
				&& Find(body, child) is int bc && bc >= 0 && Find(reference, child) is int rc && rc >= 0)
			{
				Vector3 from = (body.GetBoneGlobalRest(bc).Origin - body.GetBoneGlobalRest(bone).Origin).Normalized();
				Vector3 to = (reference.GetBoneGlobalRest(rc).Origin - reference.GetBoneGlobalRest(refBone).Origin).Normalized();
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
					global = SGlobal(refBone) * refRest[refBone].Inverse() * delta[bone] * bodyRest[bone];
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

	private static Quaternion Arc(Vector3 from, Vector3 to)
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
