using Godot;

/// <summary>
/// O que o jogador faz com as mãos (ou garras): trocar de arma, atacar,
/// o salto e o bote do lobo, recarregar, soltar a arma e rugir.
/// </summary>
public partial class Player
{
	/// <summary>
	/// Duração do rugido: a animação inteira do "roaring", na velocidade
	/// de <see cref="WolfMoves"/>. Segura-se R do começo ao fim, e só
	/// então o escudo volta.
	///
	/// É isso que torna rugir uma aposta: dois segundos sem atacar no
	/// meio de uma luta, com o som entregando a posição. O lobo continua
	/// andando e pulando enquanto ruge (decisão do Jhon) — parar no ar
	/// no meio do pulo parecia travar.
	/// </summary>
	public static float HowlDuration => WolfMoves.Duration(WolfMove.Howl);
	private bool _howling;
	private double _howlProgress;
	private bool IsHowling => _howling;

	/// <summary>Especial apertado junto com o pulo: vira bote no quadro
	/// seguinte.</summary>
	private bool _diveAfterJump;

	/// <summary>
	/// Rugido: segurar R do começo ao fim da animação devolve o escudo
	/// cheio. Soltar no meio corta o rugido sem escudo. Como o escudo
	/// escorre sozinho, rugir é decidir ficar exposto agora para
	/// sobreviver depois — e o som entrega a posição, que é a outra
	/// metade do preço.
	///
	/// Morrer ou deixar de ser lobo no meio também corta sem pagar.
	/// </summary>
	private void UpdateHowl(double delta)
	{
		if (!_howling)
		{
			bool starts = _intent.Howl
				&& _form.CanHowl
				&& !_weapon.IsStriking
				&& !_health.IsDead
				&& !UiState.AnyOpen;

			if (!starts) return;

			_howling = true;
			_howlProgress = 0;
		}

		// Segurar é o compromisso: soltar R no meio corta o rugido sem
		// escudo (decisão do Jhon). Morrer ou deixar de ser lobo também.
		if (_health.IsDead || !_form.IsWolf || !_intent.HowlHeld)
		{
			_howling = false;
			_howlProgress = 0;
			return;
		}

		_howlProgress += delta;
		if (_howlProgress < HowlDuration) return;

		_health.RestoreShield(_health.MaxShield);
		_howling = false;
		_howlProgress = 0;
	}

	/// <summary>Fração do rugido já concluída, para o HUD mostrar.</summary>
	public float HowlProgress =>
		_howling ? (float)(_howlProgress / HowlDuration) : 0f;

	private void HandleActions(double delta)
	{
		if (UiState.AnyOpen) return;

		// Rugindo, o lobo não faz mais nada até o rugido acabar.
		if (_howling)
		{
			UpdateHowl(delta);
			return;
		}

		if (_intent.Slot is Slot slot) _weapon.SelectSlot(slot);

		// Só a arma principal é automática. A secundária e a garrada
		// exigem um clique por golpe.
		bool firing = _weapon.IsAutomatic
			? _intent.AttackHeld
			: _intent.Attack;

		if (firing)
			_weapon.TryAttack();

		// A granada sai quando o botão solta.
		if (_intent.AttackReleased)
			_weapon.ReleaseGrenade();

		// Botão direito: salto no chão, bote no ar. Quem move o corpo
		// durante os dois é o passo de física, pela arma.
		//
		// Pulo e especial no mesmo quadro: quem apertou os dois quer o
		// bote, não o salto do chão. O pulo sai agora e o bote no quadro
		// seguinte, já no ar.
		//
		// No chão, segurar carrega o salto e soltar salta (decisão do
		// Jhon). Apertar e soltar no mesmo quadro é o salto mais curto.
		if (_diveAfterJump)
		{
			_diveAfterJump = false;
			if (!IsOnFloor()) _weapon.TrySpecial();
		}
		else if (_intent.Special)
		{
			if (_form.IsWolf && IsOnFloor() && _intent.Jump)
				_diveAfterJump = true;
			else
				_weapon.TrySpecial();
		}

		if (_intent.SpecialReleased)
			_weapon.ReleaseCharge();

		// R faz o que a forma permite: ruge de lobo, recarrega de
		// humano. Uma tecla, um gesto, dois corpos.
		if (_intent.Howl && !_form.IsWolf)
			_weapon.TryReload();

		// G: solta a arma da mão para frente e passa para a próxima.
		if (_intent.Drop)
			_weapon.TryDrop();

		UpdateHowl(delta);
	}
}
