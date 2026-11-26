using Godot;

/// <summary>
/// A câmera de terceira pessoa (a do lobo) sem tremer nas escadas (alpha
/// fechado, 26/11: "tremor nas escadas no modo quadrúpede").
///
/// O <see cref="SpringArm3D"/> põe a câmera, a cada passo da física, na
/// distância livre até a primeira coisa atrás do lobo. Numa escada de
/// degraus (a Obra, a Neon) o braço bate na quina de um degrau, no corrimão,
/// na parede, e solta no passo seguinte: medido de quatro subindo e
/// descendo, a câmera entrava e saía de 15 a 18 vezes por escada, até 35 cm
/// de um quadro para o outro.
///
/// Aqui a distância da câmera segue a do braço de dois jeitos: entra rápido
/// (<see cref="InSpeed"/>) e nunca fica mais de <see cref="Slack"/> além do
/// que o braço deixa (a margem do braço é 20 cm: a câmera não entra na
/// parede), e sai devagar (<see cref="OutSpeed"/>). O braço que encolhe e
/// estica a cada degrau vira uma câmera que encosta e volta sem pressa.
/// Fica no braço; só o lobo do dono usa (é a única câmera de terceira pessoa).
/// </summary>
public partial class CameraArmEase : Node
{
	/// <summary>Quão rápido a câmera entra quando algo fica entre ela e o lobo, em m/s.</summary>
	[Export] public float InSpeed = 12f;

	/// <summary>Quão rápido ela volta para longe quando o caminho abre, em m/s.</summary>
	[Export] public float OutSpeed = 2.5f;

	/// <summary>O máximo que a câmera fica além da distância livre do braço, em
	/// metros: menor que a margem do braço (0,2), para não entrar na parede.</summary>
	[Export] public float Slack = 0.15f;

	private SpringArm3D _arm;
	private Camera3D _camera;
	private float _distance = -1f;

	/// <summary>A distância em que a câmera foi desenhada no último quadro (para os testes).</summary>
	public float Distance => _distance;

	public static CameraArmEase For(SpringArm3D arm, Camera3D camera)
	{
		if (arm == null || camera == null) return null;
		var ease = new CameraArmEase { Name = "CameraArmEase", _arm = arm, _camera = camera };
		arm.AddChild(ease);
		return ease;
	}

	public override void _Process(double delta)
	{
		if (_arm == null || _camera == null || !IsInstanceValid(_arm) || !IsInstanceValid(_camera)) return;
		float free = _arm.GetHitLength();
		float dt = (float)delta;
		if (_distance < 0f || !_camera.Current) _distance = free;
		else if (free < _distance)
			_distance = Mathf.Min(Mathf.MoveToward(_distance, free, InSpeed * dt), free + Slack);
		else
			_distance = Mathf.MoveToward(_distance, free, OutSpeed * dt);

		// O braço pôs a câmera em (0, 0, livre) no passo da física; aqui ela vai
		// para a distância alisada, no mesmo eixo.
		Vector3 position = _camera.Position;
		position.Z = _distance;
		_camera.Position = position;
	}
}
