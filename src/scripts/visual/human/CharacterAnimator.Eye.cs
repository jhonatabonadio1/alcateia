using Godot;
using static AnimationKit;

/// <summary>
/// Os olhos do corpo visto (Jhon, 26/11: "o nível de agachamento deve ser
/// idêntico ao de 3ª pessoa, extremamente preciso"). A câmera da 1ª pessoa
/// fica na altura deles: em pé e agachado, o olho de quem joga é o olho do
/// corpo que os outros veem.
///
/// O ponto é o meio dos dois olhos da malha, preso ao osso da cabeça
/// (<see cref="CharacterSkin.EyeInHead"/>). A altura que a câmera usa é
/// medida uma vez, ao montar, nas poses de parado (em pé e agachado), e vai
/// ao <see cref="Player"/> como número: o passo de física não lê o desenho a
/// cada quadro (CLAUDE.md, a predição do cliente), e a câmera não balança com
/// a cabeça do clipe.
/// </summary>
public partial class CharacterAnimator
{
	private Skeleton3D _eyeSkeleton;
	private int _eyeBone = -1;
	private Vector3 _eyeInHead = new(0f, 0.075f, 0.09f);

	/// <summary>Os olhos do corpo visto, no mundo, na pose de agora.</summary>
	public Vector3 EyePosition
	{
		get
		{
			if (_eyeSkeleton == null || _eyeBone < 0 || !IsInstanceValid(_eyeSkeleton)) return GlobalPosition;
			return _eyeSkeleton.GlobalTransform * (_eyeSkeleton.GetBoneGlobalPose(_eyeBone) * _eyeInHead);
		}
	}

	/// <summary>Acha o osso da cabeça e o ponto dos olhos do personagem deste modelo.</summary>
	private void FindEyes()
	{
		_eyeSkeleton = FindSkeleton(_model);
		_eyeBone = _eyeSkeleton?.FindBone("mixamorig_Head") ?? -1;
		if (CharacterSkin.OfHumanModel(_modelPath) is CharacterSkin skin) _eyeInHead = skin.EyeInHead;
	}

	/// <summary>
	/// A altura dos olhos acima dos pés (a base deste nó), em metros, no clipe
	/// <paramref name="clip"/> (o instante 0), com as pernas e o quadril dele e,
	/// se <paramref name="aimUpper"/>, o tronco da pose de mira em pé (o que a
	/// árvore toca agachado: <see cref="CrouchAim"/>). Lida dos ossos, sem tocar
	/// a árvore: as poses locais compostas até a cabeça.
	/// </summary>
	private float EyeHeightIn(string clip, bool aimUpper)
	{
		if (_eyeSkeleton == null || _eyeBone < 0 || !_player.HasAnimation($"alcateia/{clip}")) return float.NaN;
		Animation legs = _player.GetAnimation($"alcateia/{clip}");
		Animation upper = aimUpper && _player.HasAnimation("alcateia/idle_rifle") ? _player.GetAnimation("alcateia/idle_rifle") : legs;
		string bodyPath = _player.GetNode(_player.RootNode).GetPathTo(_eyeSkeleton).ToString();

		// O tronco da mira: os ossos que o filtro do agachado deixa passar (FilterUpperBody).
		Transform3D pose = Transform3D.Identity;
		for (int bone = _eyeBone; bone >= 0; bone = _eyeSkeleton.GetBoneParent(bone))
		{
			Animation from = upper != legs && IsUpperBody(_eyeSkeleton.GetBoneName(bone)) ? upper : legs;
			pose = Local(from, _eyeSkeleton, bodyPath, bone, 0) * pose;
		}
		Vector3 eye = pose * _eyeInHead;

		// Os pés são a base deste nó (CharacterAnimator.BaseY: o fundo da cápsula).
		Transform3D toHere = GlobalTransform.AffineInverse() * _eyeSkeleton.GlobalTransform;
		return (toHere * eye).Y;
	}
}
