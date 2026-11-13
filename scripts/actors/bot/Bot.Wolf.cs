using Godot;
using System;

/// <summary>
/// O bot de lobo. Ele é um atirador: virar lobo é recurso de desespero,
/// porque o lobo é corpo-a-corpo e um bot que transforma cedo gruda no
/// jogador e nunca mais atira. Quando vira, usa as jogadas do jogador —
/// salto carregado, pulo com bote, rugido — na medida da dificuldade.
/// </summary>
public partial class Bot
{
	/// <summary>Chance por segundo de o lobo do bot usar um golpe especial
	/// quando dá (o salto carregado de 4 a 11 m, o pulo com bote de perto).
	/// Sobe com a dificuldade: no fácil o lobo quase só corre e arranha.</summary>
	[ExportGroup("Lobo")]
	[Export] public float WolfSpecialRate = 0.35f;

	/// <summary>Erro da carga do salto do bot, em fração da carga: quanto
	/// ele erra o alcance. Nunca zero — um salto que sempre cai na cabeça
	/// é injusto, não difícil.</summary>
	[Export] public float PounceError = 0.12f;
	[Export] public float WolfHealthThreshold = 0.4f;
	[Export] public float WolfRange = 14f;
	[Export] public float HumanRange = 20f;

	/// <summary>Tempo que falta do rugido. Rugir é compromisso, como
	/// para o jogador: o bot só volta a decidir depois do fim.</summary>
	private double _howlLeft;

	/// <summary>
	/// Rugido inteiro, parado, e o escudo cheio só no fim — a mesma regra
	/// do jogador, no mesmo tempo da animação (<see cref="WolfMoves"/>).
	/// </summary>
	private void RunHowl(double delta)
	{
		MoveTowards(Vector3.Zero, Speed, delta);

		if (!_form.IsWolf || _health.IsDead)
		{
			_howlLeft = 0;
			Action = BotAction.Engage;
			return;
		}

		_howlLeft -= delta;
		if (_howlLeft > 0) return;

		_health.RestoreShield(_health.MaxShield);
		_howlLeft = 0;
		Action = BotAction.Engage;
		_brainTimer = 0;
	}

	private double _specialRoll;
	private bool _wantDive;

	/// <summary>
	/// O lobo do bot com as jogadas do jogador, na medida da dificuldade:
	///
	/// - **Salto carregado** de 4 a 11 m, com o alvo à vista: carrega o
	///   que precisa para cair numa garra dele, errando
	///   <see cref="PounceError"/>.
	/// - **Pulo com bote** de 3 a 6 m: pula em direção ao alvo e mergulha
	///   no alto do pulo.
	///
	/// Sorteado uma vez por segundo com <see cref="WolfSpecialRate"/>: o
	/// lobo não vive de especial, ele chega correndo e arranha — o especial
	/// é a surpresa.
	/// </summary>
	private void WolfTactics(float distance, double delta)
	{
		if (_wantDive)
		{
			if (IsOnFloor()) { _wantDive = false; return; }
			if (Velocity.Y < 1.5f)
			{
				_wantDive = false;
				_weapon.TrySpecial();
				if (Debug) GD.Print($"{Name}: bote {(_weapon.IsDiving ? "saiu" : "não saiu")}");
			}
			return;
		}

		_specialRoll -= delta;
		if (_specialRoll > 0) return;
		_specialRoll = 0.5;

		if (!IsOnFloor() || _target == null || !CanSeeTarget()) return;
		if (!_rng.Chance(WolfSpecialRate * 0.5f)) return;

		if (distance >= 4f && distance <= 11f)
		{
			float power = _weapon.PounceChargeFor(distance) + _rng.Range(-PounceError, PounceError);
			bool went = _weapon.BotPounce(_target, power);
			if (Debug) GD.Print($"{Name}: salto a {distance:0.0} m, carga {power:0.00}{(went ? "" : " (não deu)")}");
		}
		else if (distance >= 3f && distance < 6f)
		{
			Vector3 to = _target.GlobalPosition - GlobalPosition;
			to.Y = 0;
			Vector3 hop = to.Normalized() * 3.2f;
			// Sai do chão já neste quadro: no próximo, o andar zeraria a
			// subida de quem ainda está no chão.
			Velocity = new Vector3(hop.X, JumpVelocity * (_form.Breed?.JumpMultiplier ?? 1f), hop.Z);
			MoveAndSlide();
			_wantDive = true;
		}
	}
}
