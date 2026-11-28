using Godot;
using static AnimationKit;

/// <summary>
/// Os olhos do corpo visto (Jhon, 26/11: "o humano tá agachando mais do que na
/// animação de 3ª pessoa. O nível de agachamento deve ser idêntico ao de 3ª
/// pessoa, extremamente preciso"). A câmera da 1ª pessoa fica na altura deles:
/// em pé e agachado, o olho de quem joga é o olho do corpo que os outros veem.
///
/// O ponto é o meio dos dois olhos da malha, preso ao osso da cabeça
/// (<see cref="CharacterSkin.EyeInHead"/>). A altura vem do corpo como ele é
/// desenhado — o clipe, a mistura do tronco de mira e o que a arma na mão faz
/// com o tronco (a arma na mão mexe na coluna; montar a conta só pelos clipes
/// errava 13 a 20 cm agachado). Ela é lida com o corpo parado e a cabeça reta,
/// em pé e agachado, e vai ao <see cref="Player"/> como os dois números do
/// olho (<see cref="Player.TuneHumanEye"/>): a câmera não balança com a cabeça
/// do clipe, e o passo de física continua lendo só números dele.
/// </summary>
public partial class CharacterAnimator
{
	private Skeleton3D _eyeSkeleton;
	private int _eyeBone = -1;
	private Vector3 _eyeInHead = new(0f, 0.075f, 0.09f);
	private double _eyeSettled;

	/// <summary>Os olhos do corpo visto, no mundo, na pose de agora.</summary>
	public Vector3 EyePosition
	{
		get
		{
			if (_eyeSkeleton == null || _eyeBone < 0 || !IsInstanceValid(_eyeSkeleton)) return GlobalPosition;
			return _eyeSkeleton.GlobalTransform * (_eyeSkeleton.GetBoneGlobalPose(_eyeBone) * _eyeInHead);
		}
	}

	/// <summary>A altura dos olhos do corpo visto acima dos pés (a base deste
	/// nó: o fundo da cápsula), em metros, agora.</summary>
	public float EyeHeight => EyePosition.Y - GlobalPosition.Y;

	/// <summary>Acha o osso da cabeça e o ponto dos olhos do personagem deste modelo.</summary>
	private void FindEyes()
	{
		_eyeSkeleton = FindSkeleton(_model);
		_eyeBone = _eyeSkeleton?.FindBone("mixamorig_Head") ?? -1;
		if (CharacterSkin.OfHumanModel(_modelPath) is CharacterSkin skin) _eyeInHead = skin.EyeInHead;
	}

	/// <summary>Quanto tempo parado, na mesma postura, antes de ler o olho (a
	/// mistura de em pé para agachado e a arma na mão assentarem).</summary>
	private const double EyeSettleTime = 0.35;

	/// <summary>No passo de física: a pose que está nos ossos é a final do
	/// quadro anterior (a arma na mão mexe no tronco depois da animação, com
	/// prioridade 100).</summary>
	public override void _PhysicsProcess(double delta)
	{
		TuneEye(delta);
		UpdateFeet(delta);
	}

	/// <summary>
	/// Lê o olho do corpo parado, de cabeça reta, e o dá ao dono: o de pé ou o
	/// agachado, conforme a postura. Depois que a pose do quadro está feita (os
	/// ossos com a arma na mão). Só o humano do próprio jogador.
	/// </summary>
	private void TuneEye(double delta)
	{
		if (_owner == null || _eyeSkeleton == null || _eyeBone < 0) return;
		bool crouched = _owner.IsCrouching;
		bool still = _owner.IsOnFloor() && new Vector2(_owner.Velocity.X, _owner.Velocity.Z).Length() < 0.15f
			&& Mathf.Abs(_owner.HeadPitch) < Mathf.DegToRad(8f)
			&& _health is not { IsDead: true } && _form is not { IsWolf: true } && _form is not { ShowsWolf: true }
			&& _crouched == crouched
			&& Mathf.IsEqualApprox(_owner.BodyHeight, crouched ? _owner.HumanCrouchHeight : _owner.StandHeight);
		_eyeSettled = still ? _eyeSettled + delta : 0;
		if (_eyeSettled < EyeSettleTime) return;
		_owner.TuneHumanEye(crouched, EyeHeight, (float)delta);
	}
}
