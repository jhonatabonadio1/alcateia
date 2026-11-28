using Godot;

/// <summary>
/// As câmeras do jogador: o olhar com o mouse, a primeira pessoa do
/// humano e a terceira do lobo, o recuo do tiro e a câmera da morte.
/// </summary>
public partial class Player
{
	/// <summary>
	/// Multiplicador do recuo da arma. O valor base vem de cada arma
	/// no lote; isto é o ajuste global de quanto o recuo pesa.
	/// </summary>
	[ExportGroup("Câmera e mira")]
	[Export] public float RecoilScale = 1f;

	/// <summary>Teto do recuo acumulado, em graus. Sem teto, segurar
	/// o gatilho aponta a arma para o céu e a arma vira inútil em vez
	/// de difícil.</summary>
	[Export] public float RecoilMax = 4.5f;

	/// <summary>
	/// Quanto tempo sem atirar até a mira começar a descer, em
	/// segundos. Maior que o intervalo de qualquer automática da
	/// primeira leva (0,08 s): enquanto o dedo está no gatilho a mira só
	/// sobe, e desce quando ele solta. É o mesmo atraso que o documento
	/// usa para a dispersão.
	/// </summary>
	[Export] public float RecoilReturnDelay = 0.12f;

	/// <summary>
	/// A parte proporcional da volta do coice, por segundo (a fixa é a
	/// <see cref="WeaponDefinition.RecoilRecovery"/> da arma). No Counter-Strike
	/// o recuo volta assim: o grosso logo, o fim assentando. Com 4, o teto de
	/// 4,5° do AK volta em ~0,36 s em vez de 0,82 (27/11): parar de atirar
	/// devolve a mira, e a rajada curta seguinte sai de onde se mirou.
	/// </summary>
	[Export] public float RecoilReturnExp = 4f;
	[Export] public float MouseSensitivity = 0.002f;

	/// <summary>Sensibilidade efetiva: a base daqui vezes a das opções.
	/// Na mira, cai na razão das aberturas: o mesmo movimento do mouse
	/// anda a mesma distância na tela, com a câmera fechada ou aberta.</summary>
	private float Sensitivity => MouseSensitivity * Settings.Values.Sensitivity * AimLookFactor;

	/// <summary>Abertura da câmera da cabeça fora da mira (a da cena).</summary>
	private float _baseFov = 75f;

	private float AimLookFactor =>
		_camera == null || _weapon == null || _weapon.AimBlend <= 0f ? 1f
		: Mathf.Tan(Mathf.DegToRad(_camera.Fov * 0.5f)) / Mathf.Tan(Mathf.DegToRad(_baseFov * 0.5f));

	/// <summary>A câmera fecha na mira e abre ao soltar.</summary>
	private void UpdateAimCamera()
	{
		if (_camera == null) return;
		_camera.Fov = _weapon.AimFovFor(_baseFov);
	}

	/// <summary>Distância da câmera na forma lobo (era 3,5; o Jhon achou
	/// longe demais, 15/11).</summary>
	[Export] public float ThirdPersonDistance = 2.5f;

	private Camera3D _camera;        // primeira pessoa — humano
	private Camera3D _wolfCamera;    // terceira pessoa — lobo
	private SpringArm3D _springArm;
	private float _cameraKick;
	private double _sinceKick;

	private void OnFormChanged(int form) => ApplyCamera();

	/// <summary>
	/// A câmera do lobo recua quando há parede atrás, mas não por gente
	/// (Jhon, 25/11: "o lobo vai pra frente e pra trás piscando"). Com o
	/// aliado atravessável (TeamCollision), o lobo passa por ele e o corpo do
	/// aliado fica entre o lobo e a câmera: o braço encolhia de 2,5 m para
	/// 0,1 m e voltava assim que ele saía da frente, um salto da câmera a
	/// cada um que cruzasse atrás. Gente se move e é fina; só o cenário
	/// segura a câmera. Quem avisa é o TeamCollision, a cada par.
	/// </summary>
	public void IgnoreForCamera(CharacterBody3D other)
	{
		_springArm ??= GetNodeOrNull<SpringArm3D>("Head/SpringArm3D");
		_springArm?.AddExcludedObject(other.GetRid());
	}

	/// <summary>A câmera de onde o jogador mira: a de terceira pessoa no
	/// lobo, a da cabeça no humano. Não é a câmera atual da tela — essa
	/// pode ser a da morte ou outra qualquer.</summary>
	public Camera3D AimCamera => _form.IsThirdPerson ? _wolfCamera : _camera;

	/// <summary>Recuo de câmera: o cano sobe a cada tiro e volta
	/// sozinho. Sem isso, atirar não tem peso nenhum.</summary>
	private void OnWeaponFired()
	{
		// Só arma de fogo dá coice. A faca também avisa Fired, e chutava a
		// câmera com o recuo do último tiro.
		if (_form.IsWolf || _weapon.Held == null) return;

		float kick = Mathf.DegToRad(_weapon.Recoil * RecoilScale);
		float room = Mathf.DegToRad(RecoilMax) - _cameraKick;

		// Perto do teto o recuo para de crescer: a arma fica difícil,
		// não inútil.
		kick = Mathf.Min(kick, Mathf.Max(room, 0));
		// No teto, atirando, a mira também não volta: a volta é de quem soltou o
		// gatilho. Antes o tiro no teto não contava como tiro, a volta começava
		// no meio da rajada e a mira subia e descia no alto do spray (27/11).
		_sinceKick = 0;
		if (kick > 0)
		{
			_cameraKick += kick;
			Vector3 rotation = _head.Rotation;
			rotation.X = Mathf.Clamp(
				rotation.X + kick, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
			_head.Rotation = rotation;
		}

		// No teto o lateral continua: é no alto do spray que ele desvia.
		// O lateral não volta sozinho, de propósito: é ele que o jogador
		// aprende a compensar com o mouse, e devolvê-lo automaticamente
		// tiraria o padrão que diferencia uma arma da outra.
		RotateY(-Mathf.DegToRad(_weapon.RecoilYaw * RecoilScale));
	}

	private void UpdateCameraKick(double delta)
	{
		if (_cameraKick <= 0) return;

		_sinceKick += delta;
		if (_sinceKick < RecoilReturnDelay) return;

		// A velocidade fixa do documento (°/s) mais uma parte proporcional ao
		// que falta (RecoilReturnExp): o teto de 4,5° desce em ~0,3 s na M4A4 e
		// ~0,36 s no AK (eram 0,6 e 0,8 s, só a fixa).
		//
		// O defeito antigo: a volta SOMAVA ao ângulo da cabeça em vez de
		// subtrair, então a mira subia de novo ao parar de atirar.
		float rate = Mathf.DegToRad((_weapon.Held?.RecoilRecovery ?? 7f) * RecoilScale);
		float recover = CombatRules.RecoilReturn(_cameraKick, rate, RecoilReturnExp, (float)delta);

		_cameraKick -= recover;
		_head.Rotation = new Vector3(
			Mathf.Clamp(_head.Rotation.X - recover, Mathf.DegToRad(-89f), Mathf.DegToRad(89f)),
			_head.Rotation.Y, _head.Rotation.Z);
	}

	private KillCam _killCam;

	private void ApplyCamera()
	{
		bool isMine = IsMultiplayerAuthority();

		// Morrer joga a câmera para trás. É a única hora em que o dono
		// vê o próprio corpo em primeira pessoa — e é justamente a
		// hora em que ver importa, porque mostra o que aconteceu em
		// vez de congelar a tela onde você já não está.
		bool third = _form.IsThirdPerson || (_health != null && _health.IsDead);

		_camera.Current = isMine && !third;
		_wolfCamera.Current = isMine && third;

		// Morto, quem manda na tela é a câmera de morte.
		if (_killCam != null && IsInstanceValid(_killCam) && _health != null && _health.IsDead)
			_killCam.Reclaim();

		// A arma na mão é do dono e só existe em primeira pessoa.
		Node3D view = _camera.GetNodeOrNull<Node3D>("ViewModel");
		if (view != null) view.Visible = isMine && !third;

		// Em primeira pessoa o dono não vê o próprio corpo; os outros
		// jogadores continuam vendo normalmente.
		//
		// Com modelo humano na cena a cápsula não volta nunca: ela era
		// o corpo provisório, e reacendê-la na morte poria uma cápsula
		// cinza em pé ao lado do boneco caído.
		if (isMine && GetNodeOrNull<Node3D>("Human") == null)
			_mesh.Visible = third;
	}

	/// <summary>Olhar com o mouse: o corpo gira no eixo Y, a cabeça no X.</summary>
	private void Look(InputEventMouseMotion motion)
	{
		RotateY(-motion.Relative.X * Sensitivity);

		float pitch = motion.Relative.Y * Sensitivity;

		// Puxar o mouse para baixo durante o recuo já é a volta,
		// feita à mão. Desconta do que falta voltar, senão a volta
		// automática soma em cima e a mira afunda abaixo do alvo.
		if (pitch > 0 && _cameraKick > 0)
			_cameraKick = Mathf.Max(_cameraKick - pitch, 0);

		Vector3 headRot = _head.Rotation;
		headRot.X = Mathf.Clamp(
			headRot.X - pitch,
			Mathf.DegToRad(-89f),
			Mathf.DegToRad(89f));
		_head.Rotation = headRot;
	}
}
