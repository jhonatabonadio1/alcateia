using System.Collections.Generic;
using Godot;

/// <summary>
/// Alisa o desenho de um personagem entre um passo da física e o seguinte.
///
/// A física anda 60 passos por segundo; a tela anda no ritmo dela — 120 Hz
/// num MacBook, ou 60 Hz com quadros que não caem certinho no passo. Sem
/// isto, o corpo é desenhado onde o último passo o deixou: num quadro anda
/// dois passos, no outro nenhum, e o boneco treme (Jhon, 13/11: "tremem,
/// ficam piscando").
///
/// Faz à mão o que a interpolação da física do Godot faria, mas só na
/// posição e só nas partes que se veem (a cabeça com as câmeras e a arma
/// na mão, o modelo humano, o lobo, a barra de vida). O corpo e a colisão
/// ficam onde a física diz, e a jogabilidade nunca vê o deslocamento: ele
/// sai no começo de cada passo e de cada quadro, e volta só no fim do
/// quadro, depois de todo mundo, para o desenho.
///
/// O giro não é alisado: o do jogador vem do mouse e tem de valer na hora.
/// A interpolação do Godot, ligada no projeto inteiro, atrasaria o mouse e
/// tudo o que se mexe no _Process (a arma na mão, a câmera da morte).
/// </summary>
public partial class VisualSmoothing : Node
{
	/// <summary>As partes que se veem. A colisão fica de fora.</summary>
	private static readonly string[] Parts =
		{ "Head", "MeshInstance3D", "Human", "WolfVisual", "Weapon", "HealthBar" };

	/// <summary>Andar mais que isto num passo é teleporte (nascer,
	/// renascer, a correção da rede): o desenho vai direto.</summary>
	public const float TeleportStep = 2f;

	private Node3D _body;
	private Health _health;
	private readonly List<Node3D> _parts = new();

	/// <summary>Peças soltas (<c>TopLevel</c>) que se desenham presas ao corpo: a arma
	/// na mão vista de fora (<see cref="HeldWeapon"/>). Postas no mundo, não seguem o
	/// deslocamento do modelo: a arma era desenhada onde a física está e as mãos um
	/// passo atrás — andando, até 5 cm; saindo de agachado para o pulo, o corpo sobe
	/// 17 cm num passo e a arma descolava 10 cm das mãos (Jhon, 24/11). Andam junto,
	/// no mundo.</summary>
	private readonly List<Node3D> _loose = new();
	private Vector3 _from, _applied, _appliedWorld, _appliedLift;

	/// <summary>A cabeça (as câmeras): o dono a põe no lugar depois de um degrau,
	/// e o <see cref="Lift"/> não entra nela.</summary>
	private Node3D _headPart;
	private bool _shifted;

	/// <summary>O que falta o desenho andar de uma correção da rede (no
	/// mundo), e a velocidade com que ele está indo até lá.
	///
	/// Uma mola amortecida na medida (26/11, "a tela fica tremendo com
	/// glitches"): o desenho sai devagar, acelera e chega sem passar. Era um
	/// decaimento de ~40 ms, que começava na velocidade máxima: 30 cm de
	/// correção faziam a câmera saltar a 6 m/s de um quadro para o outro, e
	/// para trás quando a correção era contra o movimento. Com a mola, 30 cm
	/// somem em ~0,3 s e a câmera nunca passa de ~1,3 m/s a mais.</summary>
	private Vector3 _correction, _correctionSpeed;

	/// <summary>A rigidez da mola da correção (rad/s): quanto maior, mais rápido
	/// o desenho alcança o corpo — e maior o tranco.</summary>
	private const float CorrectionSpring = 12f;

	/// <summary>
	/// O corpo foi corrigido pelo servidor (partida autoritativa, decisão
	/// 0011): andou <paramref name="moved"/> de uma vez. O desenho não pula
	/// junto — fica onde estava e escorrega até o corpo em uma fração de
	/// segundo, que é o que faz a correção passar despercebida.
	///
	/// O ponto de partida do passo anda junto (26/11): a correção chega fora do
	/// passo da física, e sem isto ela entrava também no deslocamento do passo
	/// que o desenho reparte pelo quadro — o desenho ia para trás até o tamanho
	/// da correção num quadro e voltava no seguinte (o tremor da tela).
	/// </summary>
	public void Absorb(Vector3 moved)
	{
		_correction -= moved;
		_from += moved;
	}

	/// <summary>
	/// O corpo subiu ou desceu um degrau de uma vez (<paramref name="moved"/>,
	/// na vertical), e o dono já cuida de levar o olho até lá devagar
	/// (<c>Player.EaseStep</c>). Esse pedaço do passo não é repartido pelo
	/// quadro. Sem isto a compensação contava duas vezes: o desenho ainda
	/// mostrava o corpo embaixo e a cabeça já tinha descido o degrau, e a
	/// câmera afundava o degrau inteiro (17 cm) num quadro e voltava no
	/// seguinte, a cada degrau (Jhon, 27/11: "ao subir escada com o humano,
	/// tá mexendo muito a câmera para cima e para baixo").
	/// </summary>
	public void Stepped(Vector3 moved) => _from += moved;

	/// <summary>
	/// Quanto o corpo desenhado fica acima (ou abaixo, negativo) do corpo da
	/// física, em metros: depois de um degrau o dono traz o olho devagar
	/// (<c>Player.EaseStep</c>), e o boneco vai junto (27/11) — numa escada de
	/// degraus sem chapa ele subia aos pulos, 13 cm de um quadro para o outro
	/// (medido), que é o "tremendo" que se via no lobo. A cabeça fica de fora:
	/// o dono já a põe no lugar.
	/// </summary>
	public float Lift { get; set; }

	/// <summary>O que o desenho do corpo ainda vai subir (ou descer) neste quadro
	/// por conta do <see cref="Lift"/>: zero se o deslocamento do quadro já está
	/// nas partes. Quem mede o corpo desenhado contra o chão no meio do quadro (as
	/// patas do lobo, <see cref="WolfFootPlant"/>) soma isto.</summary>
	public float PendingLift => _shifted ? 0f : Lift;

	/// <summary>Uma peça solta (<c>TopLevel</c>) presa ao desenho do corpo: anda com
	/// ele (veja <see cref="_loose"/>). Sai sozinha quando é liberada.</summary>
	public void Follow(Node3D loose)
	{
		if (loose != null && !_loose.Contains(loose)) _loose.Add(loose);
	}

	public override void _Ready()
	{
		_body = GetParent<Node3D>();
		_health = _body.GetNodeOrNull<Health>("Health");
		foreach (string name in Parts)
			if (_body.GetNodeOrNull<Node3D>(name) is Node3D part) _parts.Add(part);
		_headPart = _body.GetNodeOrNull<Node3D>("Head");

		_from = _body.GlobalPosition;

		// Depois de todos os _Process: o modelo, a arma e a câmera já
		// fizeram o quadro deles quando o deslocamento entra.
		ProcessPriority = int.MaxValue;

		GetTree().PhysicsFrame += OnPhysicsFrame;
		GetTree().ProcessFrame += Restore;
	}

	public override void _ExitTree()
	{
		GetTree().PhysicsFrame -= OnPhysicsFrame;
		GetTree().ProcessFrame -= Restore;
		Restore();
	}

	/// <summary>Começo do passo: tira o deslocamento e marca de onde o
	/// corpo parte.</summary>
	private void OnPhysicsFrame()
	{
		Restore();
		_from = _body.GlobalPosition;
	}

	public override void _Process(double delta)
	{
		Restore();
		if (_health != null && _health.IsDead)
		{
			_correction = _correctionSpeed = Vector3.Zero;
			return;
		}

		Vector3 behind = _correction;
		if (_correction != Vector3.Zero)
		{
			// Mola criticamente amortecida: a = −ω²x − 2ωv.
			float dt = (float)delta, w = CorrectionSpring;
			_correctionSpeed += (-w * w * _correction - 2f * w * _correctionSpeed) * dt;
			_correction += _correctionSpeed * dt;
			if (_correction.LengthSquared() < 1e-6f && _correctionSpeed.LengthSquared() < 1e-4f)
				_correction = _correctionSpeed = Vector3.Zero;
		}

		Vector3 step = _body.GlobalPosition - _from;
		if (step.LengthSquared() >= 1e-8f && step.Length() <= TeleportStep)
		{
			// Quanto do passo a tela já devia mostrar: o resto fica para trás.
			float fraction = Mathf.Clamp((float)Engine.GetPhysicsInterpolationFraction(), 0f, 1f);
			behind += step * (fraction - 1f);
		}
		Vector3 lift = Vector3.Up * Lift;
		if (behind.LengthSquared() < 1e-8f && Lift == 0f) return;

		_applied = _body.GlobalTransform.Basis.Inverse() * behind;
		_appliedWorld = behind;
		_appliedLift = _body.GlobalTransform.Basis.Inverse() * lift;
		foreach (Node3D part in _parts)
			if (IsInstanceValid(part)) part.Position += part == _headPart ? _applied : _applied + _appliedLift;
		_loose.RemoveAll(n => !IsInstanceValid(n));
		foreach (Node3D loose in _loose)
			if (loose.IsInsideTree()) loose.GlobalPosition += _appliedWorld + lift;
		_appliedWorld += lift;
		_shifted = true;
	}

	/// <summary>Devolve as partes ao lugar da física. Subtrai em vez de
	/// regravar a posição antiga: entre o desenho e o próximo passo o
	/// mouse pode ter girado a cabeça, e isso tem de ficar.</summary>
	private void Restore()
	{
		if (!_shifted) return;
		_shifted = false;

		foreach (Node3D part in _parts)
			if (IsInstanceValid(part)) part.Position -= part == _headPart ? _applied : _applied + _appliedLift;
		foreach (Node3D loose in _loose)
			if (IsInstanceValid(loose) && loose.IsInsideTree()) loose.GlobalPosition -= _appliedWorld;
	}
}
