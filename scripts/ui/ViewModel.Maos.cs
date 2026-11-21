using Godot;

/// <summary>
/// As mãos de primeira pessoa: nas armas que usam o esqueleto do AR-15
/// (o manequim da Unreal) os braços que vêm na arma dão lugar às mãos
/// padrão do jogo, iguais para os dois times. As mãos padrão são os
/// braços do <c>RIG_Manny.blend</c> do Jhon, os mesmos do Blender
/// (tools/armas/blender/maos.py as tira do <c>ar15.glb</c>).
///
/// Tudo fica em <c>assets/armas/fp/maos/</c>:
/// - <c>maos.glb</c>: a malha dos dois braços, já presa ao esqueleto;
/// - <c>padrao.tres</c>: o material. É aqui, e só aqui, que se troca a
///   textura das mãos de todas as armas. Desde 20/11 é um material de
///   shader (<c>maos.gdshader</c>, feito por tools/armas/maos.py): cor,
///   relevo, rugosidade e o LED do bracelete, que acende na cor do time
///   (<see cref="PaintBracelet"/>).
///
/// A arma não muda: o <c>ar15.glb</c> continua o mesmo, com as animações
/// dele. A malha entra no esqueleto da arma pelo nome dos ossos. Armas de
/// outro esqueleto ficam com as mãos delas.
/// </summary>
public partial class ViewModel
{
	public const string RigHandsScenePath = "res://assets/armas/fp/maos/maos.glb";
	private const string RigHandsMaterialPath = "res://assets/armas/fp/maos/padrao.tres";

	private static PackedScene _rigHandsScene;

	private MeshInstance3D _rigHands;

	/// <summary>As mãos padrão estão no esqueleto da arma em uso.</summary>
	public bool HasRigHands => _rigHands != null && IsInstanceValid(_rigHands);

	/// <summary>Põe as mãos padrão no esqueleto desta arma, se ele for o do AR-15.</summary>
	private void AttachRigHands(Node3D scene)
	{
		_rigHands = PutStandardHands(scene);
		PaintBracelet(_rigHands, _body?.GetNodeOrNull<Health>("Health")?.Team ?? Teams.Blue);
	}

	/// <summary>O LED do bracelete das mãos padrão, na cor do time de quem
	/// as usa (pedido do Jhon, 20/11): azul na Vanguarda, vermelho no
	/// Sindicato. É o parâmetro <c>led</c> do material das mãos
	/// (<c>maos.gdshader</c>), posto por instância — o material é um só
	/// para todo mundo.</summary>
	internal static void PaintBracelet(MeshInstance3D hands, int team)
	{
		if (hands == null || !IsInstanceValid(hands)) return;
		Color led = team == Teams.Orange ? new Color(1f, 0.13f, 0.1f) : new Color(0.1f, 0.5f, 1f);
		hands.SetInstanceShaderParameter("led", led);
	}

	/// <summary>As mãos padrão no esqueleto do conjunto (o do AR-15), no
	/// lugar dos braços que vieram nele. Serve também à câmera de abate e
	/// ao espectador (<see cref="KillCam"/>). Null se o esqueleto não é
	/// esse.</summary>
	internal static MeshInstance3D PutStandardHands(Node3D scene)
	{
		Skeleton3D skeleton = null;
		foreach (Node node in scene.FindChildren("*", "Skeleton3D", true, false))
		{
			skeleton = (Skeleton3D)node;
			break;
		}
		if (skeleton == null || skeleton.FindBone("upperarm_l") < 0 || skeleton.FindBone("hand_l") < 0) return null;

		_rigHandsScene ??= ResourceLoader.Exists(RigHandsScenePath) ? GD.Load<PackedScene>(RigHandsScenePath) : null;
		if (_rigHandsScene == null) return null;

		Node loaded = _rigHandsScene.Instantiate();
		if (loaded.FindChild("maos", true, false) is not MeshInstance3D hands)
		{
			loaded.Free();
			return null;
		}

		hands.GetParent().RemoveChild(hands);
		loaded.Free();
		skeleton.AddChild(hands);
		hands.Skeleton = new NodePath("..");
		if (ResourceLoader.Exists(RigHandsMaterialPath)) hands.MaterialOverride = GD.Load<Material>(RigHandsMaterialPath);

		// Os braços que vieram na arma saem de cena; a arma e o carregador ficam.
		if (scene.FindChild("bracos", true, false) is Node3D xbot) xbot.Visible = false;
		return hands;
	}

	private void ForgetRigHands() => _rigHands = null;
}
