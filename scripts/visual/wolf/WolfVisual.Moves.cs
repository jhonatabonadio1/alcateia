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

		// Encolhido, ele respira (pedido do Jhon, 15/11): parado de todo
		// parecia travado. A respiração entra e sai em 0,2 s.
		// Andando carregado, o lobo anda agachado (Charge_Walk) no passo do
		// corpo; parou, volta à pose da carga em 0,15 s.
		bool walking = _weapon.IsChargingPounce && _speed > 0.4f;
		if (Has("charge_walk"))
		{
			_chargeWalk = Mathf.MoveToward(_chargeWalk, walking ? 1f : 0f, (float)delta / 0.15f);
			_tree.Set("parameters/ChargeWalk/blend_amount", _chargeWalk);
			_tree.Set("parameters/ChargeWalkSpeed/scale", Mathf.Clamp(_speed / ChargeWalkClipSpeed, 0f, 3f) * (_crawlRate < 0 ? -1f : 1f));
		}

		if (Has("breath_add"))
		{
			_chargeBreath = Mathf.MoveToward(_chargeBreath, hold && !walking ? 1f : 0f, (float)delta / 0.2f);
			_tree.Set("parameters/ChargeBreath/add_amount", _chargeBreath);
			_tree.Set("parameters/ChargeBreathSpeed/scale", ChargeBreathRate);
		}

		// No voo, o clipe acelera no ritmo do voo (o salto carregado voa
		// mais rápido que o clipe): pousa no quadro em que o corpo pousa.
		float rate = hold ? 0f
			: WolfMoves.Of(WolfMove.Pounce).Speed * (_weapon.IsPounceFlying ? _weapon.PounceFlightRate : 1f);
		if (hold == _pounceHeld && Mathf.IsEqualApprox(rate, _pounceRate)) return;

		_pounceHeld = hold;
		_pounceRate = rate;
		_tree.Set("parameters/PounceSpeed/scale", rate);
	}

	private float _pounceRate = 1f;

	/// <summary>Ritmo da respiração do lobo encolhido na carga do salto, em
	/// relação à do Idle_Pose (2 s por ciclo): 1,6 dá 1,25 s, de quem está
	/// tenso. A profundidade está no clipe (5,5× a do parado).</summary>
	[Export] public float ChargeBreathRate = 1.6f;

	/// <summary>A passada do Charge_Walk, em m/s: o pé de apoio anda 0,80
	/// m/s no arquivo, vezes 1,13 (a mesma razão dos outros andares). O
	/// corpo carregando anda a 0,91 (WolfChargeMoveFactor 0,234): o clipe
	/// toca a 1×.</summary>
	[Export] public float ChargeWalkClipSpeed = 0.91f;

	private float _chargeBreath;
	private float _chargeWalk;

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
