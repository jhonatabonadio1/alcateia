using Godot;

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
