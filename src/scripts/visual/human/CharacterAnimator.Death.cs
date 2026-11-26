using Godot;
using static AnimationKit;

/// <summary>
/// Morrer e renascer: o clipe de morte, o corpo no chão, o desbotar e a
/// volta ao normal.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>
	/// Tempo que o corpo fica à mostra no chão antes de sumir.
	///
	/// Contado a partir do fim do clipe de morte, não da morte: o
	/// clipe tem três segundos e o corpo passa boa parte deles ainda
	/// caindo. Contar do começo fazia o corpo desbotar no meio da
	/// queda.
	/// </summary>
	[ExportGroup("Corpo morto")]
	[Export] public float CorpseHold = 1.2f;

	/// <summary>Duração do desaparecimento.</summary>
	[Export] public float CorpseFade = 1.2f;

	// O corpo no chão e o sumiço por igual (26/11): DeadBody. Até 26/11 o corpo
	// descia 12 cm fixos ao cair (CorpseDrop, feito para o X Bot: a Eve ficava 5 cm
	// no ar) e desbotava com o AlphaDepthPrePass, que mostra o de dentro.
	private DeadBody _deadBody;

	private bool _dead;
	private double _corpseTimer;
	private double _deathLength;
	private bool _hasDeath;

	private void OnDied()
	{
		StopReload();
		_dead = true;
		_corpseTimer = 0;
		if (_deadBody?.Model != _model)
		{
			_deadBody?.Reset();
			_deadBody = new DeadBody(_model, GetParentOrNull<CollisionObject3D>());
		}
		_deadBody.Hold = CorpseHold;
		_deadBody.Fade = CorpseFade;
		_deadBody.Begin();

		if (_hasDeath) _playback?.Travel("Death");
		else if (Debug) GD.Print("CharacterAnimator: no death clip.");
	}

	/// <summary>
	/// O corpo fica um instante no chão, depois some.
	///
	/// A pausa antes é o que faz a morte ser lida: sumir no mesmo
	/// quadro em que cai tira do jogador a chance de ver o que
	/// aconteceu, e quem matou perde a confirmação do abate.
	/// </summary>
	private void UpdateCorpse(double delta)
	{
		_corpseTimer += delta;
		_deadBody?.Step(delta, _corpseTimer, _deathLength);
	}

	/// <summary>O corpo tem algum material do sumiço agora (o teste
	/// confere que ninguém renasce assim).</summary>
	public bool HasFadeMaterials => _deadBody?.HasFadeMaterials ?? false;

	private void OnRespawned()
	{
		_dead = false;
		_corpseTimer = 0;
		_blend = Vector2.Zero;
		_crouched = false;
		_airborne = false;

		// Volta tudo: um corpo que renasce meio transparente e meio
		// enterrado é o mesmo defeito, só adiado. E volta o material de
		// antes, não só a opacidade: a cópia continuava transparente, e um
		// corpo transparente desenha o que tem por dentro por cima da pele —
		// depois de algumas mortes, os olhos e os dentes saltavam do rosto,
		// o cabelo sumia e as luvas pareciam rasgadas, nas duas máquinas
		// (Jhon, 22/11: "o servidor iniciou corretamente, depois que matei
		// algumas vezes ficou assim").
		_deadBody?.Reset();

		_playback?.Start("Locomotion");
	}
}
