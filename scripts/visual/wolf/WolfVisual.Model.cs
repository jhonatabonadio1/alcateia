using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// O Mutant do time e os clipes do Creature Pack: cada arquivo lido uma
/// vez, cortado no trecho que o jogo usa (WolfMoves) e sem andar a raiz.
/// </summary>
public partial class WolfVisual
{
	/// <summary>Quanto da cor do time entra na pele — só quando os dois
	/// times usam o mesmo modelo. Com um modelo por time, a pele já diz
	/// o lado.</summary>
	[ExportGroup("Aparência")]
	[Export(PropertyHint.Range, "0,1")] public float TeamTint = 0.3f;

	/// <summary>Clipes tocados como vêm do arquivo. Os golpes e o rugido
	/// saem de <see cref="WolfMoves.Clips"/>, recortados.</summary>
	private static readonly string[] Clips =
		{ "idle", "walk", "run", "wallrun", "sprint" };

	/// <summary>Disparos por cima da locomoção, na ordem da pilha: o de
	/// baixo é sobreposto pelos de cima.</summary>
	private static readonly string[] Shots =
		{ "claw", "heavy", "howl", "jump_land", "pounce", "dive", "dive_land" };
	private readonly Dictionary<string, float> _lengths = new();
	private bool _sharedModel = true;

	private bool BuildModel()
	{
		bool orange = _health != null && _health.Team == Teams.Orange;
		string path = orange && ResourceLoader.Exists(OrangeModel) ? OrangeModel : BlueModel;
		_sharedModel = !ResourceLoader.Exists(OrangeModel) || OrangeModel == BlueModel;

		if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model)
		{
			GD.PrintErr($"WolfVisual: modelo do lobo não encontrado em {path}.");
			return false;
		}

		_model = model;
		_model.Scale = Vector3.One * ModelScale;
		_model.RotationDegrees = new Vector3(0, ModelYaw, 0);
		AddChild(_model);

		_anims = FindPlayer(_model);

		// Modelo exportado de outro programa (o Mutant vermelho veio do
		// Blender) chega sem AnimationPlayer e com o esqueleto dentro de
		// "Armature". Os clipes apontam para "Skeleton3D:osso", então o
		// tocador nasce ao lado do esqueleto, onde esse caminho existe.
		if (_anims == null)
		{
			Skeleton3D skeleton = FindSkeleton(_model);
			if (skeleton == null)
			{
				GD.PrintErr("WolfVisual: o modelo do lobo não tem esqueleto.");
				return false;
			}

			_anims = new AnimationPlayer { Name = "AnimationPlayer" };
			skeleton.GetParent().AddChild(_anims);
		}

		LoadClips();
		_reference?.Free();
		_reference = null;
		ApplyTeamTint();
		BuildTree();
		return true;
	}

	private void LoadClips()
	{
		var library = new AnimationLibrary();

		// Um arquivo pode dar mais de um clipe (o "jump attack" é o salto,
		// a queda do bote e a aterrissagem): cada um é lido uma vez só.
		var files = new Dictionary<string, Animation>();
		Animation Source(string file)
		{
			if (!files.TryGetValue(file, out Animation animation))
				files[file] = animation = LoadFile(file);
			return animation;
		}

		foreach (string clip in Clips)
		{
			Animation animation = Source(clip);
			if (animation == null) continue;

			animation.LoopMode = clip is "idle" or "walk" or "run" or "wallrun" or "sprint"
				? Animation.LoopModeEnum.Linear
				: Animation.LoopModeEnum.None;

			library.AddAnimation(clip, animation);
			_lengths[clip] = (float)animation.Length;
		}

		// Agachado: o fim da aterrissagem do "jump attack" (1,95 a 2,25 s),
		// encolhido com os dois pés no chão, a 0,98 m do chão ao topo da
		// cabeça — a altura da cápsula agachada (1,0 m). Em pé ele tem
		// 1,65 m; sem isto o lobo agachado continuava em pé, com a cabeça
		// fora da própria cápsula, e o tiro passava por cima do que se via.
		if (Source("leap") is Animation leap)
		{
			Animation crouch = Trim(leap, CrouchFrom, CrouchTo, false, force: true);
			crouch.LoopMode = Animation.LoopModeEnum.Pingpong;
			library.AddAnimation("crouch", crouch);
			_lengths["crouch"] = (float)crouch.Length;
		}

		// Golpes e rugido tocam o trecho medido em WolfMoves — o mesmo que
		// a arma usa para saber quando o dano sai.
		foreach ((string clip, WolfClip window) in WolfMoves.Clips)
		{
			Animation source = Source(window.Source);
			if (source == null) continue;

			Animation animation = Trim(source, window.Start, window.End, window.FallOnly, force: true);
			animation.LoopMode = Animation.LoopModeEnum.None;

			library.AddAnimation(clip, animation);
			_lengths[clip] = (float)animation.Length;
		}

		_anims.AddAnimationLibrary("lobo", library);
	}

	private Animation LoadFile(string file)
	{
		string path = $"{ClipFolder}/{file}.fbx";
		if (!ResourceLoader.Exists(path)) return null;

		Node temp = GD.Load<PackedScene>(path)?.Instantiate();
		if (temp == null) return null;

		Animation animation = TakeAnimation(FindPlayer(temp), file, Debug);
		if (animation != null) FitClip(animation, temp, file);
		temp.Free();
		if (animation == null) return null;

		StripRootMotion(animation);
		return animation;
	}

	/// <summary>O esqueleto do clipe em T de que os outros partem: o do
	/// "walk" (o Mutant do Creature Pack).</summary>
	private Node _reference;

	/// <summary>
	/// Ajusta um clipe ao corpo em uso. O corpo pode ser outro esqueleto
	/// com os mesmos nomes de osso (o lobo do Meshy, em A e em centímetros):
	/// então o clipe é reorientado por <see cref="RetargetPose"/> e o
	/// quadril, na razão das alturas. Com o Mutant original isso não muda
	/// nada.
	/// </summary>
	private void FitClip(Animation animation, Node clipScene, string file)
	{
		Skeleton3D body = FindSkeleton(_model);
		if (body == null) return;

		PointAt(animation, _anims.GetNode(_anims.RootNode).GetPathTo(body).ToString());

		bool sprint = file == "sprint";
		if (sprint) KeepClipRest(animation, FindSkeleton(clipScene), body);

		_reference ??= GD.Load<PackedScene>($"{ClipFolder}/walk.fbx")?.Instantiate();
		Skeleton3D reference = _reference != null ? FindSkeleton(_reference) : null;
		RetargetPose(animation, reference, body);

		float hips = HipsRestHeight(_model);
		float source = sprint ? SprintSourceHips : HipsRestHeight(_reference);
		if (hips > 0.01f && source > 0.01f) MatchHips(animation, hips / source);
		if (sprint) LiftHips(animation, SprintHipsLift * UnitsPerMeter(body));
	}

	/// <summary>Unidades do esqueleto por metro do corpo (1 no Mutant; 100 no
	/// lobo do Meshy, cujo esqueleto está em centímetros dentro de um nó
	/// de escala 0,01).</summary>
	private float UnitsPerMeter(Skeleton3D skeleton)
	{
		float scale = skeleton.GlobalBasis.Scale.Y / Mathf.Max(_model.GlobalBasis.Scale.Y, 1e-6f);
		return scale > 1e-6f ? 1f / scale : 1f;
	}

	private bool Has(string clip) => _lengths.ContainsKey(clip);

	/// <summary>O quadril do X Bot em pé (T), em metros: o corpo em que a
	/// corrida de quatro foi feita. O arquivo dela não serve de medida — o
	/// repouso salvo nele já é a pose de quatro.</summary>
	private const float SprintSourceHips = 1.04f;

	/// <summary>Quanto o quadril da corrida de quatro sobe depois de
	/// ajustado às pernas, em metros do esqueleto: só com a razão das
	/// pernas o pé de trás descia 4,7 cm abaixo do chão no impulso. Com
	/// isto ele desce até 1 cm, como no andar e no correr; o pulso da
	/// frente desce até 6 cm do chão, e a mão grande do Mutant encosta.</summary>
	private const float SprintHipsLift = 0.062f;

	private static void LiftHips(Animation animation, float lift)
	{
		for (int track = 0; track < animation.GetTrackCount(); track++)
		{
			if (animation.TrackGetType(track) != Animation.TrackType.Position3D
				|| !animation.TrackGetPath(track).ToString().Contains("Hips")) continue;
			for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
				animation.TrackSetKeyValue(track, key, (Vector3)animation.TrackGetKeyValue(track, key) + Vector3.Up * lift);
		}
	}

	/// <summary>
	/// Recorta o clipe de <paramref name="start"/> a <paramref name="end"/>
	/// (segundos do arquivo) e começa do zero. É como o bote usa só a
	/// queda do "jump attack" e a garrada pula a preparação longa.
	/// <paramref name="force"/> copia mesmo quando o trecho é o clipe
	/// inteiro: o mesmo arquivo pode dar mais de um clipe.
	///
	/// <paramref name="fallOnly"/>: a altura do quadril fica limitada à
	/// de pé. No ar quem levanta o corpo é a física; somar a subida do
	/// clipe deixaria o lobo flutuando acima da cápsula. A agachada do
	/// impacto, abaixo disso, fica.
	/// </summary>
	private static Animation Trim(Animation source, float start, float end, bool fallOnly, bool force = false)
	{
		end = Mathf.Min(end, (float)source.Length);
		if (!force && start <= 0.001f && end >= source.Length - 0.001f && !fallOnly) return source;

		int hips = -1;
		float rest = 0f;
		for (int t = 0; t < source.GetTrackCount(); t++)
		{
			if (source.TrackGetType(t) == Animation.TrackType.Position3D
				&& source.TrackGetPath(t).ToString().Contains("Hips")
				&& source.TrackGetKeyCount(t) > 0)
			{
				hips = t;
				rest = ((Vector3)source.TrackGetKeyValue(t, 0)).Y;
				break;
			}
		}

		var cut = new Animation { Length = end - start };
		const double step = 1.0 / 30.0;

		for (int t = 0; t < source.GetTrackCount(); t++)
		{
			Animation.TrackType type = source.TrackGetType(t);
			if (type is not (Animation.TrackType.Position3D or Animation.TrackType.Rotation3D or Animation.TrackType.Scale3D))
				continue;

			int track = cut.AddTrack(type);
			cut.TrackSetPath(track, source.TrackGetPath(t));

			for (double time = start; time <= end + 0.0001; time += step)
			{
				double at = time - start;
				switch (type)
				{
					case Animation.TrackType.Position3D:
						Vector3 position = source.PositionTrackInterpolate(t, time);
						if (fallOnly && t == hips) position.Y = Mathf.Min(position.Y, rest);
						cut.PositionTrackInsertKey(track, at, position);
						break;
					case Animation.TrackType.Rotation3D:
						cut.RotationTrackInsertKey(track, at, source.RotationTrackInterpolate(t, time));
						break;
					default:
						cut.ScaleTrackInsertKey(track, at, source.ScaleTrackInterpolate(t, time));
						break;
				}
			}
		}

		return cut;
	}

	/// <summary>
	/// Um toque da cor do time na pele. O material é duplicado: vem da
	/// importação e é um só para todos os lobos, e pintar o original
	/// pintaria o lobo azul e o laranja da mesma cor.
	/// </summary>
	private void ApplyTeamTint()
	{
		if (_health == null || TeamTint <= 0 || !_sharedModel) return;

		Color team = Teams.ColorOf(_health.Team);
		Color tint = Colors.White.Lerp(team, TeamTint);

		MaterialTint.Paint(_model, (_, source) => source.AlbedoColor * tint);
	}
}
