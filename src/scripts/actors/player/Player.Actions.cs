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

	/// <summary>Quanto o botão direito apertado no chão fica guardado, em
	/// segundos: junto com o pulo (ou um instante antes), o bote fica pedido
	/// para este pulo (Jhon, 20/11).</summary>
	[Export] public float LeapBuffer = 0.15f;

	private double _leapBuffered;

	/// <summary>O bote pedido no ar (ou guardado do chão) esperando o lobo
	/// subir até <c>Weapon.LeapMinHeight</c>. Sai assim que chegar; tocou o
	/// chão antes, cai fora.</summary>
	private bool _leapWaiting;

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
		// escudo (decisão do Jhon). Morrer ou deixar de ser lobo também, e
		// grudar numa parede (pedido do Jhon, 17/11): na parede, corre.
		if (_health.IsDead || !_form.IsWolf || !_intent.HowlHeld || _onWall)
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
		// Botão direito segurado, de humano: zoom com mira circular (Weapon.Aim).
		// Com menu aberto, solta a mira.
		_weapon.SetAiming(!UiState.AnyOpen && !_form.IsWolf && _intent.AimHeld);

		// Menu aberto ou partida acabada: o que estava sendo segurado é
		// largado sem disparar (o salto carregado não sai, o rugido é cortado).
		if (UiState.AnyOpen || Match.IsOver)
		{
			_weapon.CancelCharge();
			_howling = false;
			_howlProgress = 0;
			return;
		}

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

		// Especial armado: o clique salta (no alvo travado, se houver), não
		// arranha.
		if (_weapon.SpecialArmed)
		{
			if (_intent.Attack) _weapon.TryPounce();
		}
		// Correndo de quatro (W W) o lobo não arranha: tem de sair da
		// corrida para bater (regra do Jhon, 18/11: "se eu estiver andando
		// de quadrúpede não posso atacar, tenho que parar; não posso estar
		// no W W").
		//
		// No ar a regra não vale (Jhon, 23/11: "correndo de 4 e pulo, não
		// consigo dar ataque primário, só secundário"): o _sprinting segue
		// ligado no pulo com o W apertado (para voltar correndo ao cair), e
		// prendia a garra aérea junto. A corrida que proíbe é a do chão.
		//
		// Na parede também não arranha (Jhon, 24/11: "andar na parede com o lobo
		// não pode atacar, mas se sair da parede tem que responder aos ataques
		// imediatamente"): lá o botão é o de agarrar (Player.WallRun), e o clique
		// não fica guardado. A garrada em curso é cortada ao grudar
		// (Weapon.CutClaw), então saiu da parede, o clique arranha na hora.
		else if (firing && !(_form.IsWolf && ((_sprinting && IsOnFloor()) || _onWall)))
			_weapon.TryAttack();

		// A granada sai quando o botão solta.
		if (_intent.AttackReleased)
			_weapon.ReleaseGrenade();

		// Botão direito, de lobo: o bote, no ar. Quem move o corpo durante
		// ele é o passo de física, pela arma.
		//
		// No chão o botão direito fica guardado um instante: apertado junto
		// com o pulo (ou logo antes), vale para este pulo. No ar, o bote só
		// sai a partir de uma altura (Weapon.LeapMinHeight): apertado antes,
		// espera o lobo subir até lá e sai sozinho.
		if (_intent.Special)
		{
			// Humano com a faca: a estocada (Jhon, 24/11). Com arma de fogo o
			// botão direito é a mira (SetAiming, acima).
			if (!_form.IsWolf && _weapon.HumanSlot == Slot.Knife) _weapon.TryStab();
			else if (!_form.IsWolf) _weapon.TrySpecial();
			else if (IsOnFloor()) _leapBuffered = LeapBuffer;
			else _leapWaiting = true;
		}
		else if (_leapBuffered > 0)
		{
			_leapBuffered -= delta;
			if (!_form.IsWolf) _leapBuffered = 0;
			else if (!IsOnFloor())
			{
				_leapBuffered = 0;
				_leapWaiting = true;
			}
		}

		if (_leapWaiting)
		{
			if (!_form.IsWolf || IsOnFloor()) _leapWaiting = false;
			else
			{
				_weapon.TrySpecial();
				if (_weapon.IsDiving) _leapWaiting = false;
			}
		}

		// V, de humano com arma de fogo: a coronhada.
		if (_intent.Melee && !_form.IsWolf)
			_weapon.TryButt();

		// Q, de lobo: arma o especial (o lobo segue andando, com o modo alvo
		// aberto) ou desarma. Quem salta é o clique (acima).
		if (_intent.Pounce && _form.IsWolf)
			_weapon.ToggleSpecial();

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
