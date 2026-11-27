using System;

/// <summary>O que a sala deixa um time ser (o <c>FormRule</c> do jogo, sem o Godot).</summary>
public enum FormLimit : byte
{
	/// <summary>Vira lobo quando quiser, no tempo do lobo e com a espera.</summary>
	Free,
	HumanOnly,
	WolfOnly,
	/// <summary>O servidor não sabe a regra deste time agora (os modos que trocam na metade): copia.</summary>
	Unchecked
}

/// <summary>
/// A forma que o jogo diz ter, conferida no servidor (27/11). A forma vem
/// no comando, e até aqui o servidor copiava: um jogo adulterado ficava
/// lobo a partida inteira, virava de novo sem esperar, pulava a
/// transformação (meio segundo em que se leva mais dano) ou virava lobo
/// num modo só de humanos. Aqui o servidor conta, em passos de comando do
/// próprio jogador (a rede junta e separa pacotes, o comando não): o tempo
/// de lobo, a espera depois dele e o tempo da transformação. O que não
/// cabe é recusado — o corpo do servidor fica humano, anda como humano (o
/// jogo é corrigido para lá) e a garra dele não vale (arma que não estava
/// na mão).
///
/// Com folga a favor do jogador: o relógio dele e o daqui não começam no
/// mesmo passo. As formas vão como número (0 humano, 1 virando lobo, 2
/// lobo, 3 voltando), o mesmo do comando. C# puro, com teste de unidade.
/// </summary>
public sealed class FormAudit
{
	public const byte Human = 0, ShiftingToWolf = 1, Wolf = 2, ShiftingToHuman = 3;

	/// <summary>Folga no tempo de lobo e na espera, em passos (0,75 s).</summary>
	public const int SlackTicks = 45;

	/// <summary>Folga no tempo da transformação, em passos.</summary>
	public const int ShiftSlackTicks = 8;

	private readonly int _limit, _cooldown, _shift;
	private int _wolfTicks, _coolLeft, _shiftTicks;
	private bool _wolf, _flagged;

	/// <summary>Quantas vezes a forma contada pelo jogo não coube nas regras.</summary>
	public int Refused { get; private set; }

	/// <summary>Passos de espera que ainda faltam para virar lobo de novo.</summary>
	public int CooldownLeft => _coolLeft;

	/// <param name="wolfSeconds">O tempo de lobo de cada vez (0: sem limite).</param>
	/// <param name="cooldownSeconds">A espera para virar de novo.</param>
	/// <param name="shiftSeconds">Quanto dura virar lobo.</param>
	public FormAudit(float wolfSeconds, float cooldownSeconds, float shiftSeconds, int tickRate = NetRates.TickRate)
	{
		_limit = (int)MathF.Ceiling(wolfSeconds * tickRate);
		_cooldown = (int)MathF.Floor(cooldownSeconds * tickRate);
		_shift = (int)MathF.Floor(shiftSeconds * tickRate);
	}

	/// <summary>Morreu ou renasceu: o tempo de lobo e a espera zeram (como no jogo).</summary>
	public void Reset()
	{
		_wolfTicks = _coolLeft = _shiftTicks = 0;
		_wolf = _flagged = false;
	}

	/// <summary>Um comando: a forma que o jogo conta e a que vale.</summary>
	public byte Step(byte claimed, FormLimit rule)
	{
		if (rule == FormLimit.Unchecked) return claimed;
		if (rule == FormLimit.WolfOnly) return claimed == ShiftingToWolf ? ShiftingToWolf : Wolf;

		bool wantsWolf = claimed is ShiftingToWolf or Wolf;
		if (rule == FormLimit.HumanOnly)
		{
			Flag(wantsWolf);
			return Human;
		}

		if (!wantsWolf)
		{
			if (_wolf) Leave();
			else if (_coolLeft > 0) _coolLeft--;
			_flagged = false;
			return claimed == ShiftingToHuman ? ShiftingToHuman : Human;
		}

		if (!_wolf)
		{
			// Ainda na espera: fica humano (e a espera segue contando).
			if (_coolLeft > SlackTicks)
			{
				_coolLeft--;
				Flag(true);
				return Human;
			}
			_wolf = true;
			_wolfTicks = _shiftTicks = 0;
			_coolLeft = 0;
		}

		if (claimed == ShiftingToWolf || _shiftTicks < _shift - ShiftSlackTicks)
		{
			// Lobo sem ter virado: ainda está virando.
			if (claimed == Wolf && _shiftTicks == 0) Flag(true);
			_shiftTicks++;
			return ShiftingToWolf;
		}

		_wolfTicks++;
		if (_limit > 0 && _wolfTicks > _limit + SlackTicks)
		{
			// O tempo de lobo acabou e o jogo não voltou: volta aqui.
			Leave();
			Flag(true);
			return Human;
		}
		return Wolf;
	}

	private void Leave()
	{
		_wolf = false;
		_wolfTicks = _shiftTicks = 0;
		_coolLeft = _limit > 0 ? _cooldown : 0;
	}

	/// <summary>Conta uma vez por episódio, não uma por passo.</summary>
	private void Flag(bool bad)
	{
		if (!bad) return;
		if (!_flagged) Refused++;
		_flagged = true;
	}
}
