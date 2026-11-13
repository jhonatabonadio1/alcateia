using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// A locomoção por quadro: o ponto da mistura sai da velocidade pela
/// passada de cada clipe (o clipe nunca acelera), a postura de pé ou
/// agachada, o pulo e a queda.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>
	/// Velocidade que corresponde ao clipe de caminhada.
	///
	/// O espaço de mistura tem a caminhada no raio 1 e a corrida no
	/// raio 2, então este número é a metade da velocidade máxima: a
	/// pé solto o personagem cai exatamente em cima do clipe de
	/// corrida, e qualquer coisa no meio mistura os dois.
	/// </summary>
	[ExportGroup("Locomoção")]
	[Export] public float WalkReference = 2.1f;

	/// <summary>
	/// A passada de cada clipe, em m/s a 1× — medida no quadril do X Bot.
	/// O ponto da mistura sai da velocidade por estes números, trecho a
	/// trecho: a 1,0 m/s para a frente é a caminhada pura, a 3,0 a corrida
	/// pura, e no meio a mistura anda o que o corpo anda. O clipe nunca
	/// acelera (decisão do Jhon): quem acompanha as pernas é o corpo.
	/// Antes a caminhada ficava em 2,1 m/s e a corrida em 4,2, e o pé
	/// patinava o dobro na caminhada.
	/// </summary>
	[Export] public float WalkClip = 1.0f;
	[Export] public float RunClip = 3.02f;
	[Export] public float BackWalkClip = 1.1f;
	[Export] public float BackRunClip = 2.59f;
	[Export] public float StrafeClip = 1.03f;

	/// <summary>
	/// Inverte o eixo lateral do espaço de mistura.
	///
	/// Os clipes de strafe do Mixamo vieram com o nome trocado e
	/// foram renomeados pela medição do deslocamento no arquivo. O
	/// que a medição não resolve é a conversão de eixo do importador,
	/// que pode espelhar Z. Se no jogo o strafe sair para o lado
	/// errado, é só marcar isto — os pares estão certos entre si.
	///
	/// Marcado: medido no pé apoiado, sem isto o strafe tocava ao
	/// contrário — o pé andava junto com o corpo, 4 m/s de patinada.
	/// </summary>
	[Export] public bool InvertStrafe = true;

	/// <summary>Suavização da entrada do espaço de mistura. Sem ela a
	/// troca de direção pisca entre clipes.</summary>
	[Export] public float BlendSmoothing = 9f;

	/// <summary>Velocidade em que o agachado está andando a pleno.</summary>
	[Export] public float CrouchWalkSpeed = 1.9f;

	/// <summary>Quanto da pose de mira em pé entra no tronco agachado: 1 é
	/// o fuzil no ombro, como de pé; 0, o fuzil baixo dos clipes
	/// agachados.</summary>
	[Export(PropertyHint.Range, "0,1")] public float CrouchAim = 1f;

	private Vector2 _blend;
	private double _jumpGrace;
	private bool _airborne;
	private bool _crouched;
	private float _crouchBlend;

	/// <summary>Velocidade para o raio da mistura: 0 parado, 1 na
	/// caminhada, 2 na corrida, em linha reta entre os clipes.</summary>
	private static float Axis(float speed, float walk, float run)
	{
		if (speed <= walk) return speed / Mathf.Max(walk, 0.01f);
		return 1f + (speed - walk) / Mathf.Max(run - walk, 0.01f);
	}

	/// <summary>
	/// Agachado é um estado à parte, não um ponto do espaço de
	/// mistura: a pose não é a caminhada mais devagar, é outra postura.
	///
	/// Sem o clipe de andar agachado, mover-se agachado mostra a pose
	/// parada. É feio e é honesto — melhor do que o corpo levantar
	/// sozinho porque o jogador andou.
	/// </summary>
	private void UpdateStance()
	{
		if (_airborne) return;

		bool crouching = _owner != null && _owner.IsCrouching;
		if (crouching == _crouched) return;

		_crouched = crouching;
		_playback?.Travel(crouching ? "Crouch" : "Locomotion");
	}

	/// <summary>Velocidade do agachado, de parado a andando.</summary>
	private void UpdateCrouchBlend(double delta)
	{
		if (!_crouched) return;

		Vector3 flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		float target = Mathf.Clamp(flat.Length() / Mathf.Max(CrouchWalkSpeed, 0.01f), 0f, 1f);

		_crouchBlend = Mathf.MoveToward(_crouchBlend, target, 4f * (float)delta);
		if (_crouchWalk) _tree.Set("parameters/Machine/Crouch/Pose/blend_position", _crouchBlend);
		if (_crouchAim) _tree.Set("parameters/Machine/Crouch/AimBlend/blend_amount", CrouchAim);
	}

	/// <summary>
	/// Entra e sai do ar sozinho.
	///
	/// Isto continua sendo leitura de estado, não decisão: quem
	/// decide que o corpo está no ar é a física. O animador só
	/// percebe a borda — e precisa percebê-la, porque cair de um
	/// telhado não passa por Jump() e sem isso o boneco desceria os
	/// dezessete metros do Arrival andando.
	/// </summary>
	private void UpdateAir(double delta)
	{
		if (_jumpGrace > 0) _jumpGrace -= delta;

		bool grounded = _body.IsOnFloor();

		if (!grounded && !_airborne)
		{
			_airborne = true;

			// Só vai para a queda se não estiver saltando: o salto
			// entrega para ela sozinho quando termina.
			if (_jumpGrace <= 0 && _playback?.GetCurrentNode().ToString() != "Jump")
				_playback?.Travel("Fall");
		}
		else if (grounded && _airborne)
		{
			_airborne = false;
			_crouched = false;

			// Pousa se houver clipe de pouso; o estado devolve para a
			// locomoção sozinho quando acaba.
			_playback?.Travel(_player.HasAnimation("alcateia/land") ? "Land" : "Locomotion");
		}
	}

	/// <summary>
	/// Dispara o estado do pulo. Chamado por quem pula, e não
	/// adivinhado aqui a partir de estar no ar — sair do chão também
	/// acontece ao cair de um telhado, e as duas coisas não são a
	/// mesma animação.
	///
	/// A carência existe porque o pulo e a saída do chão acontecem no
	/// mesmo quadro: sem ela, a detecção de ar via o corpo subindo e
	/// mandava para a queda antes de o pulo aparecer.
	/// </summary>
	public void Jump()
	{
		if (_dead) return;

		_jumpGrace = 0.35;
		_playback?.Travel("Jump");

		if (Debug && _playback == null)
			GD.Print($"{_body.Name}: Jump() chamado sem árvore montada");
	}
}
