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
	private const float TeleportStep = 2f;

	private Node3D _body;
	private Health _health;
	private readonly List<Node3D> _parts = new();
	private Vector3 _from, _applied;
	private bool _shifted;

	/// <summary>O que falta o desenho andar de uma correção da rede (no
	/// mundo). Some aos poucos: em ~0,1 s o desenho alcança o corpo.</summary>
	private Vector3 _correction;
	private const float CorrectionFade = 25f;

	/// <summary>
	/// O corpo foi corrigido pelo servidor (partida autoritativa, decisão
	/// 0011): andou <paramref name="moved"/> de uma vez. O desenho não pula
	/// junto — fica onde estava e escorrega até o corpo em uma fração de
	/// segundo, que é o que faz a correção passar despercebida.
	/// </summary>
	public void Absorb(Vector3 moved) => _correction -= moved;

	public override void _Ready()
	{
		_body = GetParent<Node3D>();
		_health = _body.GetNodeOrNull<Health>("Health");
		foreach (string name in Parts)
			if (_body.GetNodeOrNull<Node3D>(name) is Node3D part) _parts.Add(part);

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
			_correction = Vector3.Zero;
			return;
		}

		Vector3 behind = _correction;
		_correction *= Mathf.Exp(-CorrectionFade * (float)delta);
		if (_correction.LengthSquared() < 1e-6f) _correction = Vector3.Zero;

		Vector3 step = _body.GlobalPosition - _from;
		if (step.LengthSquared() >= 1e-8f && step.Length() <= TeleportStep)
		{
			// Quanto do passo a tela já devia mostrar: o resto fica para trás.
			float fraction = Mathf.Clamp((float)Engine.GetPhysicsInterpolationFraction(), 0f, 1f);
			behind += step * (fraction - 1f);
		}
		if (behind.LengthSquared() < 1e-8f) return;

		_applied = _body.GlobalTransform.Basis.Inverse() * behind;
		foreach (Node3D part in _parts)
			if (IsInstanceValid(part)) part.Position += _applied;
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
			if (IsInstanceValid(part)) part.Position -= _applied;
	}
}
