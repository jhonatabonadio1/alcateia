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
	[Export] public float MouseSensitivity = 0.002f;

	/// <summary>Sensibilidade efetiva: a base daqui vezes a das opções.</summary>
	private float Sensitivity => MouseSensitivity * Settings.Values.Sensitivity;

	/// <summary>Distância da câmera na forma lobo.</summary>
	[Export] public float ThirdPersonDistance = 3.5f;

	private Camera3D _camera;        // primeira pessoa — humano
	private Camera3D _wolfCamera;    // terceira pessoa — lobo
	private SpringArm3D _springArm;
	private float _cameraKick;
	private double _sinceKick;
	private void OnFormChanged(int form) => ApplyCamera();

	/// <summary>A câmera de onde o jogador mira: a de terceira pessoa no
	/// lobo, a da cabeça no humano. Não é a câmera atual da tela — essa
	/// pode ser a da morte ou outra qualquer.</summary>
	public Camera3D AimCamera => _form.IsThirdPerson ? _wolfCamera : _camera;

	/// <summary>Recuo de câmera: o cano sobe a cada tiro e volta
	/// sozinho. Sem isso, atirar não tem peso nenhum.</summary>
	private void OnWeaponFired()
	{
		if (_form.IsWolf) return;

		float kick = Mathf.DegToRad(_weapon.Recoil * RecoilScale);
		float room = Mathf.DegToRad(RecoilMax) - _cameraKick;

		// Perto do teto o recuo para de crescer: a arma fica difícil,
		// não inútil.
		kick = Mathf.Min(kick, Mathf.Max(room, 0));
		if (kick <= 0) return;

		_cameraKick += kick;
		_sinceKick = 0;

		Vector3 rotation = _head.Rotation;
		rotation.X = Mathf.Clamp(
			rotation.X + kick, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
		_head.Rotation = rotation;

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

		// Volta linear, na velocidade do documento (°/s): o M16 desce os
		// 4,5° do teto em 0,6 s, o AK em 0,8 s, a M3 em 1,1 s.
		//
		// O defeito antigo: a volta SOMAVA ao ângulo da cabeça em vez de
		// subtrair, então a mira subia de novo ao parar de atirar.
		float rate = Mathf.DegToRad((_weapon.Held?.RecoilRecovery ?? 7f) * RecoilScale);
		float recover = Mathf.Min(rate * (float)delta, _cameraKick);

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

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority()) return;

		// Menu aberto: o mouse é do menu. Sem isto a câmera gira atrás
		// da tela de lotes enquanto o jogador tenta clicar num cartão.
		if (UiState.AnyOpen) return;

		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
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

		// Esc não solta mais o mouse aqui: abre o menu, e quem decide
		// o cursor é o UiState. Dois donos do mesmo cursor foi o defeito
		// que o UiState nasceu para resolver.
	}
}
