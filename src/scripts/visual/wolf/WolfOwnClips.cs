using Godot;
using System.Collections.Generic;

/// <summary>
/// As animações do lobo feitas pelo Jhon no Blender, no esqueleto do lobo
/// do Meshy (<c>lobo_anim.glb</c>: só o esqueleto e os clipes, sem malha
/// nem textura). Cada uma vale no lugar do clipe do Mixamo de mesmo papel.
///
/// O arquivo vem do Blender: o esqueleto está em outro espaço (o nó
/// "Armature" gira 90° e vale 0,01) e tem proporções um pouco diferentes
/// do corpo do time (o azul foi feito 8% maior que o vermelho).
/// <see cref="Fit"/> leva o clipe ao corpo em uso: reorienta cada osso
/// (<see cref="AnimationKit.RetargetPose"/>) e põe o quadril na altura do
/// corpo. Só o quadril anda: o deslocamento dos outros ossos é o do
/// esqueleto do clipe, e deixá-lo esticaria um corpo de outro tamanho.
/// </summary>
public static class WolfOwnClips
{
	public const string Path = "res://src/assets/personagens/_comum/lobo/lobo_anim.glb";

	/// <summary>Papel no jogo → nome do clipe no arquivo (o Godot tira o
	/// "_Loop" do nome e põe o clipe em laço). Os papéis são os nomes dos
	/// arquivos do Mixamo que cada um substitui ("leap" é o "jump attack":
	/// salto, bote e aterrissagem saem do Special_Attack).</summary>
	private static readonly Dictionary<string, string> Names = new()
	{
		["idle"] = "Idle_Pose",
		["walk"] = "Walk_Forward",
		["walk_back"] = "Walk_Backward",
		["walk_left"] = "Walk_Left",
		["walk_right"] = "Walk_Right",
		["sprint"] = "Run_Quad",
		["crouch"] = "Crouch_Idle",
		["crawl"] = "Crouch_Walk",
		["claw"] = "Attack_Primary",
		["heavy"] = "Attack_Secondary",
		["howl"] = "Howl",
		["breath_add"] = "Breath_Add",
		["charge_walk"] = "Charge_Walk",
		["leap"] = "Special_Attack",
		["jump_start"] = "Jump_Start",
		["jump_air"] = "Jump_Air",
		["jump_land"] = "Jump_Land"
	};

	/// <summary>Os ossos que apoiam no chão (pés e mãos): giram a partir do
	/// repouso do corpo, não na direção do osso do clipe
	/// (<see cref="AnimationKit.RetargetPose"/>, parâmetro keepRest). O pé
	/// do esqueleto dos clipes aponta mais para baixo que o do corpo: copiando
	/// a direção dele, o pé inclinava para a frente e a ponta das garras
	/// entrava de 6 a 18 cm no chão (26/11).</summary>
	public static readonly HashSet<string> SoleBones = new()
	{
		"mixamorig_LeftUpLeg", "mixamorig_LeftLeg", "mixamorig_RightUpLeg", "mixamorig_RightLeg",
		"mixamorig_LeftFoot", "mixamorig_LeftToeBase", "mixamorig_RightFoot", "mixamorig_RightToeBase",
		"mixamorig_LeftHand", "mixamorig_RightHand"
	};

	/// <summary>Os papéis que seguem na direção dos ossos do clipe, pés
	/// inclusive: engatinhar e de quatro. Neles a pata de apoio patinava mais
	/// a partir do repouso (medido em 26/11, PassadaDoLobo: engatinhando 0,27
	/// → 0,65 m/s, de quatro 0,56 → 0,65), e as patas no chão
	/// (<see cref="WolfFootPlant"/>) já levantam as garras até o chão. Nos
	/// andares em pé o repouso patina menos (de lado 0,49 → 0,26, para trás
	/// 0,46 → 0,15).</summary>
	private static ICollection<string> KeepRestFor(string role) =>
		role is "crawl" or "sprint" ? null : SoleBones;

	public static bool Has(string role) => Names.ContainsKey(role);

	/// <summary>Abre o arquivo; quem chama libera a cena. Null se o arquivo
	/// não existe (o jogo volta aos clipes do Mixamo).</summary>
	public static Node Open()
	{
		if (_scene == null || !GodotObject.IsInstanceValid(_scene))
			_scene = ResourceLoader.Exists(Path) ? GD.Load<PackedScene>(Path) : null;
		return _scene?.Instantiate();
	}

	/// <summary>O arquivo carregado, guardado entre um lobo e o outro. Sem
	/// isto cada lobo montado carregava o arquivo de novo, e o Godot devolvia
	/// o mesmo recurso enquanto o coletor do C# ainda soltava o invólucro da
	/// vez anterior: de vez em quando, "Handle is not initialized" no Output
	/// (o erro intermitente dos testes, achado em 25/11 sempre nesta linha).
	/// É um arquivo do disco, não um recurso montado em runtime; o
	/// <see cref="Saida"/> o solta no fim.</summary>
	private static PackedScene _scene;

	/// <summary>Solta o arquivo guardado (a saída do jogo).</summary>
	public static void Release() => _scene = null;

	/// <summary>Duração do clipe do papel no arquivo, em segundos (0 se não
	/// existe). Os testes conferem o passo do som com ela.</summary>
	public static float Length(string role)
	{
		Node scene = Open();
		if (scene == null || !Names.TryGetValue(role, out string name)) return 0f;
		AnimationPlayer player = AnimationKit.FindPlayer(scene);
		float length = player != null && player.HasAnimation(name) ? (float)player.GetAnimation(name).Length : 0f;
		scene.Free();
		return length;
	}

	/// <summary>Transformação do nó até a raiz da cena (sem a da própria
	/// <paramref name="stop"/>).</summary>
	private static Transform3D Chain(Node from, Node stop)
	{
		Transform3D t = Transform3D.Identity;
		for (Node n = from; n != null && n != stop; n = n.GetParent())
			if (n is Node3D n3) t = n3.Transform * t;
		return t;
	}

	/// <summary>O clipe do papel <paramref name="role"/> pronto para o corpo
	/// <paramref name="body"/> (null se o papel ou o osso não existem).
	/// <paramref name="bodyPath"/> é o caminho do esqueleto do corpo a
	/// partir da raiz do tocador.</summary>
	public static Animation Fit(Node scene, string role, Skeleton3D body, Node modelRoot, string bodyPath)
	{
		if (scene == null || body == null || !Names.TryGetValue(role, out string name)) return null;

		AnimationPlayer player = AnimationKit.FindPlayer(scene);
		Skeleton3D reference = AnimationKit.FindSkeleton(scene);
		if (player == null || reference == null || !player.HasAnimation(name)) return null;

		int refHips = reference.FindBone("mixamorig_Hips");
		int bodyHips = body.FindBone("mixamorig_Hips");
		if (refHips < 0 || bodyHips < 0) return null;

		var animation = (Animation)player.GetAnimation(name).Duplicate();

		// Só o quadril anda; escala e deslocamento dos outros ossos saem.
		for (int track = animation.GetTrackCount() - 1; track >= 0; track--)
		{
			Animation.TrackType type = animation.TrackGetType(track);
			bool hips = animation.TrackGetPath(track).ToString().Contains("Hips");
			if (type == Animation.TrackType.Scale3D || (type == Animation.TrackType.Position3D && !hips))
				animation.RemoveTrack(track);
		}
		AnimationKit.PointAt(animation, bodyPath);

		Transform3D fr = Chain(reference, scene.GetParent());
		Transform3D fb = Chain(body, modelRoot);

		// O quadril: do espaço do clipe para o do corpo, na razão das alturas.
		AnimationKit.MoveHips(animation, reference, refHips, fr, body, bodyHips, fb);

		AnimationKit.RetargetPose(animation, reference, body,
			fr.Basis.Orthonormalized().GetRotationQuaternion(), fb.Basis.Orthonormalized().GetRotationQuaternion(), KeepRestFor(role));

		// Os clipes do Jhon são no lugar: o quadril vai e volta dentro do
		// ciclo (11 cm no andar) sem sair do lugar. Esse vaivém fica — tirá-lo
		// (StripRootMotion) fazia o pé de apoio andar junto com o quadril.
		return animation;
	}
}
