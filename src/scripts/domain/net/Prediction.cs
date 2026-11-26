using System.Numerics;

/// <summary>
/// O que o jogo previu em cada passo do próprio jogador, para comparar com
/// o que o servidor diz depois (<see cref="Reconcile"/>).
///
/// A correção é por deslocamento: se no passo N o servidor diz que o
/// corpo estava um palmo à esquerda do previsto, o corpo de agora vai um
/// palmo à esquerda, e os passos previstos depois de N também (para a
/// próxima comparação partir do lugar certo). O desenho não pula: quem
/// chama esconde a correção e a solta aos poucos.
///
/// Diferença menor que <see cref="Tolerance"/> é ruído (o ângulo em 2
/// bytes, a física em outra máquina) e fica como está.
/// </summary>
public sealed class Prediction
{
	public const int Capacity = 128;

	/// <summary>Abaixo disto (metros) não corrige.</summary>
	public const float Tolerance = 0.03f;

	/// <summary>Acima disto (metros) a correção não é suave: o corpo vai.</summary>
	public const float SnapDistance = 2f;

	private readonly (uint Seq, Vector3 Position, Vector3 Velocity)[] _ring = new (uint, Vector3, Vector3)[Capacity];

	public int Corrections { get; private set; }
	public float LastError { get; private set; }

	/// <summary>A soma e a maior das correções feitas, em metros (para medir).</summary>
	public float ErrorSum { get; private set; }
	public float ErrorMax { get; private set; }

	public void Record(uint seq, Vector3 position, Vector3 velocity) =>
		_ring[seq % Capacity] = (seq, position, velocity);

	/// <summary>
	/// O servidor simulou até o comando <paramref name="seq"/> e o corpo
	/// ficou em <paramref name="server"/>. Devolve o quanto o corpo de agora
	/// deve andar para ficar certo (zero se não precisa).
	/// </summary>
	public Vector3 Reconcile(uint seq, uint newest, Vector3 server)
	{
		var entry = _ring[seq % Capacity];
		if (entry.Seq != seq) return Vector3.Zero;

		Vector3 error = server - entry.Position;
		LastError = error.Length();
		if (LastError < Tolerance) return Vector3.Zero;

		Corrections++;
		ErrorSum += LastError;
		if (LastError > ErrorMax) ErrorMax = LastError;
		for (uint s = seq; s <= newest && newest - s < Capacity; s++)
		{
			ref var e = ref _ring[s % Capacity];
			if (e.Seq == s) e.Position += error;
		}
		return error;
	}

	public void Clear() => System.Array.Clear(_ring);
}
