/// <summary>
/// A fila de comandos de um jogador, no servidor.
///
/// Os comandos chegam fora de ordem, repetidos (cada pacote leva os
/// últimos quatro) e às vezes não chegam. O servidor consome um por
/// passo, na ordem: é o que mantém o corpo dele andando no mesmo ritmo
/// que o do jogo.
///
/// - Começa com <see cref="StartDepth"/> comandos guardados: a folga que
///   absorve o pacote que atrasa um pouco.
/// - Faltou o comando do passo: repete o último, segurando o que estava
///   segurado (sem repetir pulo nem tiro) — <see cref="Starved"/> conta.
///   Se o que faltou chegar depois, vale (o servidor fica um passo atrás).
/// - Um buraco com comandos depois dele, e já esperou demais: pula o
///   buraco (<see cref="Skipped"/>).
/// - Fila funda demais (o jogo andou mais rápido): o servidor consome dois
///   no mesmo passo até voltar (<see cref="HighWater"/>).
/// </summary>
public sealed class CommandBuffer
{
	public const int Capacity = 128;
	public const int StartDepth = 2;
	public const int HighWater = 4;

	/// <summary>Esperando o comando que falta, quantos passos ainda se
	/// repete o último antes de pular o buraco.</summary>
	public const int GapPatience = 2;

	private readonly InputCommand[] _ring = new InputCommand[Capacity];
	private readonly bool[] _present = new bool[Capacity];
	private uint _highest;
	private InputCommand _last;
	private bool _hasLast;
	private int _waited;

	/// <summary>O último comando simulado (o servidor conta isto de volta
	/// ao jogo, que compara onde achou que estava).</summary>
	public uint LastProcessed { get; private set; }

	public bool Started { get; private set; }

	public int Starved { get; private set; }
	public int Skipped { get; private set; }
	public int Received { get; private set; }

	/// <summary>Quantos passos o jogo está à frente do que já foi simulado.</summary>
	public int Depth => _highest > LastProcessed ? (int)(_highest - LastProcessed) : 0;

	/// <summary>Quantos comandos esta fila quer consumir neste passo.</summary>
	public int WantedThisTick => !Started ? 0 : Depth > HighWater ? 2 : 1;

	public void Push(in InputCommand c)
	{
		if (c.Seq == 0) return;
		if (Started && c.Seq <= LastProcessed) return;

		// Muito à frente: o jogo recomeçou a contagem (reconectou). Recomeça junto.
		if (Started && c.Seq > LastProcessed + Capacity)
		{
			Reset();
		}

		int i = (int)(c.Seq % Capacity);
		if (_present[i] && _ring[i].Seq == c.Seq) return;

		_ring[i] = c;
		_present[i] = true;
		Received++;
		if (c.Seq > _highest) _highest = c.Seq;

		if (!Started && CountFrom(Lowest()) >= StartDepth)
		{
			Started = true;
			LastProcessed = Lowest() - 1;
		}
	}

	/// <summary>O próximo comando a simular. Falso antes de começar.
	/// <paramref name="repeated"/>: não chegou, é o último segurado.</summary>
	public bool Next(out InputCommand command, out bool repeated)
	{
		command = default;
		repeated = false;
		if (!Started) return false;

		uint want = LastProcessed + 1;
		if (Take(want, out command))
			return true;

		// Buraco: espera um pouco (repetindo o último); depois pula para o
		// próximo que chegou.
		if (_highest > want && ++_waited > GapPatience)
		{
			uint next = want + 1;
			while (next <= _highest && !Has(next)) next++;
			Skipped += (int)(next - want);
			LastProcessed = next - 1;
			_waited = 0;
			if (Take(next, out command)) return true;
		}

		if (!_hasLast) return false;
		command = _last.Held();
		repeated = true;
		Starved++;
		return true;
	}

	private bool Take(uint seq, out InputCommand command)
	{
		command = default;
		if (!Has(seq)) return false;

		int i = (int)(seq % Capacity);
		command = _ring[i];
		_present[i] = false;
		LastProcessed = seq;
		_last = command;
		_hasLast = true;
		_waited = 0;
		return true;
	}

	private bool Has(uint seq)
	{
		int i = (int)(seq % Capacity);
		return _present[i] && _ring[i].Seq == seq;
	}

	private uint Lowest()
	{
		uint lowest = uint.MaxValue;
		for (int i = 0; i < Capacity; i++)
			if (_present[i] && _ring[i].Seq < lowest) lowest = _ring[i].Seq;
		return lowest;
	}

	private int CountFrom(uint seq)
	{
		int n = 0;
		while (Has(seq + (uint)n)) n++;
		return n;
	}

	private void Reset()
	{
		System.Array.Clear(_present);
		_highest = 0;
		LastProcessed = 0;
		Started = false;
		_hasLast = false;
		_waited = 0;
	}
}
