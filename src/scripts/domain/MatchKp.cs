using System;
using System.Collections.Generic;

/// <summary>
/// O KP de uma partida (o WP do código; docs/CONJUNTOS.md), em C# puro: o
/// jogo e o servidor de partida contam pelas mesmas regras.
///
/// **Morrer não tira KP** (Jhon, 26/11: "ao morrer, não pode perder KP. E
/// devem ganhar um pouco mais por partida"). Até 25/11 a morte tirava 100 e
/// o conjunto pago era cobrado de novo a cada renascer — quem comprava uma
/// arma e morria logo perdia tudo. Agora a arma paga com KP fica
/// **desbloqueada até o fim da partida**: renascer com ela não cobra de
/// novo, e um conjunto custa só a soma dos itens que ainda não foram pagos
/// nesta partida. O KP só desce quando o jogador escolhe pagar uma arma nova.
/// </summary>
public sealed class MatchKp
{
	/// <summary>Os ganhos (26/11, +25% sobre os de 25/11: 100, 70 e 35).</summary>
	public const int PerKill = 125, PerWolfKill = 88, PerAssist = 44;

	/// <summary>Renascer com o conjunto padrão (o de graça) rende isto: a rede
	/// de segurança de quem está sem KP.</summary>
	public const int DefaultBonus = 50;

	/// <summary>O teto do KP na partida (Jhon, 21/11).</summary>
	public const float Max = 10000f;

	private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

	/// <summary>O que já foi pago nesta partida (não se paga de novo).</summary>
	public IReadOnlyCollection<string> Unlocked => _unlocked;

	public bool IsUnlocked(string id) => string.IsNullOrEmpty(id) || _unlocked.Contains(id);

	/// <summary>Quanto falta pagar por estes itens: a soma dos que ainda não
	/// foram pagos nesta partida (cada item conta uma vez, mesmo repetido).</summary>
	public int CostOf(IEnumerable<string> items, Func<string, int> priceOf)
	{
		int cost = 0;
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (string id in items)
		{
			if (string.IsNullOrEmpty(id) || _unlocked.Contains(id) || !seen.Add(id)) continue;
			cost += Math.Max(priceOf(id), 0);
		}
		return cost;
	}

	/// <summary>Marca os itens como pagos (depois de debitar o <see cref="CostOf"/>).</summary>
	public void Unlock(IEnumerable<string> items)
	{
		foreach (string id in items)
			if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
	}

	/// <summary>Começa a partida de novo: nada pago.</summary>
	public void Reset() => _unlocked.Clear();

	/// <summary>
	/// O máximo de KP que um jogador pode ter juntado (o servidor de partida
	/// confere o gasto contra isto): os abates, as assistências e o bônus do
	/// padrão em cada vez que entrou (a primeira e cada renascer). É um teto:
	/// quem renasceu com um conjunto pago não ganhou aqueles 50 — a folga fica
	/// a favor do jogador, nunca contra.
	/// </summary>
	public static int MostEarned(int kills, int wolfKills, int assists, int deaths)
	{
		int wolf = Math.Clamp(wolfKills, 0, Math.Max(kills, 0));
		int human = Math.Max(kills, 0) - wolf;
		long total = (long)human * PerKill + (long)wolf * PerWolfKill
			+ (long)Math.Max(assists, 0) * PerAssist + (long)(Math.Max(deaths, 0) + 1) * DefaultBonus;
		return (int)Math.Min(total, (long)Max);
	}
}
