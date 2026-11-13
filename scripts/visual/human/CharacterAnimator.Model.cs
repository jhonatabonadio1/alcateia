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
	/// Personagem de cada time. O time azul é o Exo Gray, o laranja é a
	/// Ely — decisão do Jhon. Os dois são do Mixamo, com o mesmo
	/// esqueleto do xbot, então todos os clipes servem sem conversão.
	///
	/// Com personagem próprio o corpo não é pintado: a roupa já diz o
	/// time, e pintar por cima de textura vira lama. Caminho vazio, ou
	/// arquivo que não existe, mantém o xbot pintado da cena.
	/// </summary>
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string BlueModel = "res://assets/characters/human/exo_gray.fbx";
	[Export(PropertyHint.File, "*.fbx,*.glb,*.tscn")]
	public string OrangeModel = "res://assets/characters/human/ely.fbx";
	[Export] public Color BlueBody = new Color(0.22f, 0.45f, 0.95f);
	[Export] public Color OrangeBody = new Color(1f, 0.48f, 0.16f);

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
		var library = new AnimationLibrary();
		int count = 0;

		// Os clipes são do X Bot, e o quadril deles fica na altura do X
		// Bot (1,04 m). O Exo Gray (0,96) e a Ely (0,98) têm pernas mais
		// curtas: com o quadril do X Bot os pés ficavam 6 a 8 cm acima do
		// chão. Cada clipe é ajustado para o quadril do modelo em uso.
		float modelHips = HipsRestHeight(_model);

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
				StripRootMotion(animation);

				float clipHips = HipsRestHeight(temp);
				if (modelHips > 0.01f && clipHips > 0.01f)
					MatchHips(animation, modelHips / clipHips);

				// Locomoção precisa repetir; golpe e morte, não.
				if (clip == "crouch_walk" && CrouchWalkTrim > 0
					&& animation.Length > CrouchWalkTrim)
					animation.Length = CrouchWalkTrim;

				animation.LoopMode = IsLooping(clip)
					? Animation.LoopModeEnum.Linear
					: Animation.LoopModeEnum.None;

				library.AddAnimation(clip, animation);
				count++;
			}

			temp.QueueFree();
		}

		_player.AddAnimationLibrary("alcateia", library);
		return count;
	}

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
		if (node is VisualInstance3D visual) visual.Layers = bit;

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
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) return;

		if (GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model) return;

		Node3D old = _model;
		model.Name = old.Name;
		model.Transform = old.Transform;
		RemoveChild(old);
		old.QueueFree();
		AddChild(model);

		_model = model;
		_teamModel = true;
	}

	/// <summary>A arma na mão, para os outros verem.</summary>
	private void AttachWeapon()
	{
		if (_weapon == null) return;

		var camera = GetNodeOrNull<Camera3D>(OwnerCameraPath);
		uint layers = camera != null ? 1u << (BodyLayer - 1) : 1u;

		var held = new HeldWeapon { Name = "HeldWeapon" };
		AddChild(held);
		held.Attach(_model, _body, layers);
	}
}
