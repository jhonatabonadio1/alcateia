using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// O corpo que aparece: os clipes carregados no modelo, o modelo de cada
/// time (ou o xbot pintado), a arma na mão e o corpo escondido da câmera
/// do dono.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>
	/// Nó escondido quando os clipes carregam.
	///
	/// Serve para a cápsula: se o modelo entrar, ela some; se algo
	/// falhar no caminho, ela fica e o bot continua visível em vez de
	/// virar um inimigo invisível que atira.
	/// </summary>
	[ExportGroup("Aparência")]
	[Export] public NodePath HideOnSuccess;

	/// <summary>
	/// Câmera que não deve desenhar este corpo.
	///
	/// Em primeira pessoa o dono tem a cabeça dentro do próprio
	/// modelo, e o que ele veria é o interior de um crânio. A saída
	/// não é esconder o corpo — isso o apagaria também para os outros
	/// jogadores na Fase 2 — e sim tirá-lo da máscara desta câmera.
	/// O corpo continua existindo, continua animado e continua
	/// projetando sombra, que é como se vê o próprio corpo num FPS.
	///
	/// Vazio em bot: bot não tem dono olhando de dentro.
	/// </summary>
	[Export] public NodePath OwnerCameraPath;

	/// <summary>Camada de render do corpo. 2 porque a 1 é o mundo, e
	/// misturar os dois tiraria o cenário junto.</summary>
	[Export] public int BodyLayer = 2;

	/// <summary>
	/// Pintar o corpo com a cor do time.
	///
	/// Num quatro contra quatro com o mesmo boneco para todo mundo, a
	/// primeira pergunta de cada encontro é "é dos meus?", e ela precisa
	/// de resposta antes do primeiro tiro. Cor de corpo inteiro responde
	/// a trinta metros; nome em cima da cabeça, não.
	/// </summary>
	[Export] public bool TintByTeam = true;

	/// <summary>
	/// Personagem de cada time: a Eve no azul e o Barut no laranja (19/11,
	/// Jhon: os do Mixamo, Exo Gray e Ely, saíram). São do pacote Stylized
	/// Sci-Fi Characters, com os nomes de osso do Mixamo e outro repouso:
	/// os clipes entram por <see cref="AnimationKit.FitToBody"/>.
	///
	/// Com personagem próprio o corpo não é pintado: a roupa já diz o
	/// time, e pintar por cima de textura vira lama. Caminho vazio, ou
	/// arquivo que não existe, mantém o xbot pintado da cena.
	/// </summary>
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string BlueModel = "res://src/assets/personagens/eve/humano.glb";
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string OrangeModel = "res://src/assets/personagens/barut/humano.glb";
	[Export] public Color BlueBody = new Color(0.22f, 0.45f, 0.95f);
	[Export] public Color OrangeBody = new Color(1f, 0.48f, 0.16f);

	/// <summary>O modelo do personagem escolhido (CharacterSkin), no lugar
	/// do modelo do time. Quem define é o Player, antes do _Ready.</summary>
	public string ModelOverride { get; set; }

	private bool _teamModel;

	/// <summary>
	/// Traz cada clipe do seu próprio arquivo para a biblioteca do
	/// modelo.
	///
	/// Cada FBX do Mixamo importa como uma cena inteira, com esqueleto
	/// e tudo, carregando uma animação só. Aqui a animação é arrancada
	/// da cena e a cena descartada. As trilhas apontam para
	/// "Skeleton3D:osso", que é o mesmo caminho em todos os arquivos —
	/// foi por isso que valeu conferir que os dezessete compartilham o
	/// mesmo rig antes de escrever qualquer coisa.
	/// </summary>
	private int LoadClips()
	{
		// Corpo de outro rig (os personagens do pacote Sci-Fi): a biblioteca
		// ajustada a ele é a mesma para todo bot com o mesmo modelo, e o
		// ajuste custa — feita uma vez por modelo.
		if (_modelPath != null && FittedLibraries.TryGetValue(_modelPath, out AnimationLibrary cached))
		{
			_player.AddAnimationLibrary("alcateia", cached);
			return cached.GetAnimationList().Count;
		}

		var library = new AnimationLibrary();
		int count = 0;
		bool fitted = false;

		// Os clipes são do X Bot, e o quadril deles fica na altura do X
		// Bot (1,04 m). O Exo Gray (0,96) e a Ely (0,98) têm pernas mais
		// curtas: com o quadril do X Bot os pés ficavam 6 a 8 cm acima do
		// chão. Cada clipe é ajustado para o quadril do modelo em uso.
		float modelHips = HipsRestHeight(_model);
		Skeleton3D body = FindSkeleton(_model);
		string bodyPath = body != null ? _player.GetNode(_player.RootNode).GetPathTo(body).ToString() : "Skeleton3D";

		foreach (string clip in Clips)
		{
			string path = $"{ClipFolder}/{clip}.fbx";

			if (!ResourceLoader.Exists(path)) continue;

			var scene = GD.Load<PackedScene>(path);
			if (scene == null) continue;

			Node temp = scene.Instantiate();
			AnimationPlayer source = FindPlayer(temp);

			Animation animation = TakeAnimation(source, clip, Debug && !_reported);

			if (animation != null)
			{
				if (!SameRig(body, _model, FindSkeleton(temp), temp))
				{
					// Outro rig com os nomes do Mixamo: reorienta osso a osso e
					// leva o quadril ao espaço e à altura do corpo.
					FitToBody(animation, temp, body, _model, bodyPath);
					// Ombros mais altos e braços mais curtos que os do X Bot: o
					// fuzil chega mais perto do rosto, e a cabeça inclinada do
					// clipe entrava na coronha. Ela sobe um pouco.
					TiltBack(animation, body, _model, HeadBones, FittedHeadLift * 0.5f);
					StripRootMotion(animation);
					fitted = true;
				}
				else
				{
					StripRootMotion(animation);

					float clipHips = HipsRestHeight(temp);
					if (modelHips > 0.01f && clipHips > 0.01f)
						MatchHips(animation, modelHips / clipHips);
				}

				// Locomoção precisa repetir; golpe e morte, não.
				if (clip == "crouch_walk" && CrouchWalkTrim > 0
					&& animation.Length > CrouchWalkTrim)
					animation.Length = CrouchWalkTrim;

				// O agachado do corpo visto, mais fundo que o do clipe (26/11).
				Animation stand = library.HasAnimation("idle_rifle") ? library.GetAnimation("idle_rifle") : null;
				if (clip is "crouch_walk" or "crouch_idle")
					Kneel(animation, stand, body, bodyPath);

				animation.LoopMode = IsLooping(clip)
					? Animation.LoopModeEnum.Linear
					: Animation.LoopModeEnum.None;

				library.AddAnimation(clip, animation);
				count++;

				// Sem clipe de agachado parado, a pose do andar agachado com os dois pés no chão.
				if (clip == "crouch_walk" && !library.HasAnimation("crouch_idle")
					&& KneelIdle(stand, body, bodyPath) is Animation idle)
					library.AddAnimation("crouch_idle", idle);
			}

			temp.QueueFree();
		}

		_player.AddAnimationLibrary("alcateia", library);
		if (fitted && _modelPath != null) FittedLibraries[_modelPath] = library;
		return count;
	}

	/// <summary>Quanto a cabeça de um corpo de outro rig sobe nos clipes, em
	/// graus (metade no pescoço, metade na cabeça).</summary>
	[Export] public float FittedHeadLift = 38f;

	private static readonly string[] HeadBones = { "mixamorig_Neck", "mixamorig_Head" };

	/// <summary>As bibliotecas já ajustadas a um corpo de outro rig, por
	/// modelo.</summary>
	private static readonly Dictionary<string, AnimationLibrary> FittedLibraries = new();

	/// <summary>O arquivo do modelo em uso, quando é o de um time ou de um
	/// personagem (nulo no xbot da cena).</summary>
	private string _modelPath;

	private static bool IsLooping(string clip) =>
		clip.StartsWith("idle") || clip.StartsWith("walk") ||
		clip.StartsWith("run") || clip.StartsWith("strafe") ||
		clip == "crouch_idle" || clip == "crouch_walk" || clip == "fall";

	/// <summary>Move o corpo para uma camada própria e tira essa
	/// camada da máscara da câmera do dono.</summary>
	private void HideFromOwner()
	{
		var camera = GetNodeOrNull<Camera3D>(OwnerCameraPath);
		if (camera == null) return;

		uint bit = 1u << (BodyLayer - 1);

		SetLayer(_model, bit);
		camera.CullMask &= ~bit;
	}

	/// <summary>
	/// Corpo na cor do time, juntas num tom escuro dela.
	///
	/// O material é duplicado antes de pintar, pelo mesmo motivo do
	/// desbotar da morte: ele vem da importação e é um só para todos os
	/// bots, e pintar o original pintaria os oito da mesma cor.
	///
	/// O desbotar da morte duplica de novo a partir daqui, porque lê o
	/// material ativo — que passa a ser esta cópia já pintada.
	/// </summary>
	private void ApplyTeamTint()
	{
		if (!TintByTeam || _health == null || _teamModel) return;

		Color body = _health.Team == Teams.Blue ? BlueBody : OrangeBody;
		Color joints = body.Darkened(0.6f);

		MaterialTint.Paint(_model, (mesh, _) =>
			mesh.Name.ToString().ToLowerInvariant().Contains("joint") ? joints : body);
	}

	private static VisualInstance3D FirstVisual(Node node)
	{
		if (node is VisualInstance3D visual) return visual;

		foreach (Node child in node.GetChildren())
			if (FirstVisual(child) is VisualInstance3D inner) return inner;

		return null;
	}

	private static void SetLayer(Node node, uint bit)
	{
		if (node is VisualInstance3D visual) RenderLayers.Set(visual, bit);

		foreach (Node child in node.GetChildren())
			SetLayer(child, bit);
	}

	/// <summary>
	/// Troca o xbot da cena pelo personagem do time. Acontece antes de
	/// carregar os clipes, porque eles entram no tocador do modelo que
	/// fica.
	/// </summary>
	private void SwapTeamModel()
	{
		if (_health == null) return;

		string path = _health.Team == Teams.Orange ? OrangeModel : BlueModel;
		if (!string.IsNullOrEmpty(ModelOverride) && ResourceLoader.Exists(ModelOverride)) path = ModelOverride;
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) return;

		if (GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model) return;

		// Um GLB sem animação (os personagens do pacote Sci-Fi) não traz
		// tocador: os clipes entram num novo, na raiz do modelo.
		if (FindPlayer(model) == null)
			model.AddChild(new AnimationPlayer { Name = "AnimationPlayer" });

		Node3D old = _model;
		model.Name = old.Name;
		model.Transform = old.Transform;
		RemoveChild(old);
		old.QueueFree();
		AddChild(model);

		_model = model;
		SkinCulling.Loosen(_model);
		_modelPath = path;
		_teamModel = true;
	}

	/// <summary>A arma na mão, para os outros verem.</summary>
	private void AttachWeapon()
	{
		if (_weapon == null) return;

		var camera = GetNodeOrNull<Camera3D>(OwnerCameraPath);
		uint layers = camera != null ? 1u << (BodyLayer - 1) : 1u | ViewModel.CharacterLayer;

		// As poses do Blender do personagem (poses_armas.json, ao lado do modelo).
		var held = new HeldWeapon { Name = "HeldWeapon", PosesPath = HoldPoses.PathFor(_modelPath), ClipsModel = _modelPath };
		AddChild(held);
		held.Attach(_model, _body, layers);
		_held = held;
	}

	/// <summary>A arma na mão. Quando ela vem do conjunto de primeira pessoa,
	/// os braços fazem a recarga e a granada de lá (as do clipe de fuzil não
	/// tocam por cima).</summary>
	private HeldWeapon _held;
	private bool ArmsFromRig => _held != null && IsInstanceValid(_held) && _held.ArmsFromRig;
}
