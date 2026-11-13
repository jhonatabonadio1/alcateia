using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// Os golpes na tela: cada sinal da arma dispara o clipe no tempo do
/// WolfMoves — garra, golpe pesado, salto (com a carga segurando a pose),
/// bote e a aterrissagem dele — e o rugido da cintura para cima.
/// </summary>
public partial class WolfVisual
{
	private bool _howling;

	private bool IsHowling() =>
		_player != null ? _player.HowlProgress > 0f
		: _bot != null && _bot.Action == BotAction.Howl;

	/// <summary>Dispara um golpe por cima do corpo.</summary>
	private void Fire(string shot, float speed)
	{
		if (_tree == null || !Has(shot)) return;

		string name = Cap(shot);
		_tree.Set($"parameters/{name}Speed/scale", speed);
		_tree.Set($"parameters/{name}/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	/// <summary>
	/// Golpe do lobo, com o clipe e a velocidade de
	/// <see cref="WolfMoves"/> — os mesmos que a arma usa para o dano
	/// sair no instante em que a garra chega. Quem alterna os braços e
	/// decide quando pode bater é a arma; aqui só se toca.
	/// </summary>
	private void OnMove(int move)
	{
		if (!_form.IsWolf) return;

		var (clip, speed) = WolfMoves.Of((WolfMove)move);

		// A queda do bote acompanha o mergulho: o impacto da animação cai
		// no quadro em que o lobo deve encostar.
		if ((WolfMove)move == WolfMove.Leap)
			speed = WolfMoves.DiveSpeed(_weapon.DiveEta);

		Fire(clip, speed);
		if ((WolfMove)move == WolfMove.Pounce) _pounceClock = 0;
	}

	/// <summary>Tempo desde que o salto começou a tocar.</summary>
	private double _pounceClock = 99;
	private bool _pounceHeld;

	/// <summary>
	/// Carregando o salto, o clipe para no fim do agachar — o lobo fica
	/// encolhido, pronto — e segue quando o jogador solta. A arma conta o
	/// resto do golpe a partir dali, então o salto e o dano continuam no
	/// quadro certo.
	/// </summary>
	private void UpdateCharge(double delta)
	{
		_pounceClock += delta;
		if (!Has("pounce")) return;

		bool hold = _weapon.IsChargingPounce && _pounceClock >= WolfMoves.StepOf(WolfMove.Pounce).Delay;
		if (hold == _pounceHeld) return;

		_pounceHeld = hold;
		_tree.Set("parameters/PounceSpeed/scale", hold ? 0f : WolfMoves.Of(WolfMove.Pounce).Speed);
	}

	/// <summary>O bote encostou: a aterrissagem entra por cima da queda,
	/// no mesmo quadro do dano — mesmo que tenha encostado antes do
	/// previsto (alguém no caminho). A queda não é cortada: a
	/// aterrissagem precisa dela embaixo para entrar a partir da pose do
	/// mergulho, e não da pose parada.</summary>
	private void OnDiveLanded()
	{
		if (!_form.IsWolf || _tree == null) return;

		var (clip, speed) = WolfMoves.Land;
		Fire(clip, speed);
	}
}
