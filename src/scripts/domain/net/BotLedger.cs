using System;
using System.Collections.Generic;

/// <summary>
/// A conta do dano de cada bot, no servidor (27/11). O bot roda na máquina
/// do dono da sala (decisão 0008): o servidor confere o acerto num bot e o
/// manda ao dono, e era o dono quem dizia quando o bot morria — um jogo
/// adulterado deixava os bots do próprio time imortais. Aqui o servidor soma
/// o dano que ele mesmo aceitou em cada bot; quando a soma passa do que
/// corpo nenhum aguenta (duas vezes a vida e o escudo que o dono diz que o
/// bot tem, com teto, mais uma folga), **o servidor decide a morte** e o que
/// o dono conta do bot deixa de valer até ele renascer.
///
/// A folga é larga de propósito: o servidor não tem o corpo do bot (a
/// redução de dano do especial, o escudo que o rugido devolve), e matar um
/// bot honesto antes da hora é um defeito que todo mundo vê. Um bot honesto
/// morre na máquina do dono muito antes de chegar aqui. C# puro, com teste.
/// </summary>
public sealed class BotLedger
{
	/// <summary>A vida mais o escudo que se aceita o dono contar (o humano tem 200, o lobo 200 + 100).</summary>
	public const float MaxPool = 500f;

	/// <summary>Quantas vezes a vida contada o bot aguenta antes de o servidor matar, e a folga fixa (o escudo do rugido).</summary>
	public const float Times = 2f, Slack = 300f;

	/// <summary>Sem levar dano por este tempo, a conta zera (a vida volta sozinha depois de 15 s).</summary>
	public const double Forget = 15.0;

	/// <summary>Morto pelo servidor, por quanto tempo o que o dono conta do bot não vale, no máximo (a espera de renascer).</summary>
	public const double ForcedFor = 15.0;

	private sealed class Entry
	{
		public float Taken, Pool = 300f;
		public double LastHit = double.NegativeInfinity, ForcedAt = double.NegativeInfinity;
		public bool Forced, ReportedDead;
	}

	private readonly Dictionary<long, Entry> _bots = new();

	/// <summary>Bots que o servidor matou porque o dono não matou.</summary>
	public int Forced { get; private set; }

	private Entry Of(long bot) => _bots.TryGetValue(bot, out Entry e) ? e : _bots[bot] = new Entry();

	/// <summary>O que o dono conta do bot (o estado dele): a vida máxima e se está morto.</summary>
	public void Report(long bot, float maxHp, float maxShield, bool dead, double now)
	{
		Entry e = Of(bot);
		if (float.IsFinite(maxHp) && float.IsFinite(maxShield)) e.Pool = Math.Clamp(maxHp + maxShield, 100f, MaxPool);
		if (dead)
		{
			e.Taken = 0;
			e.ReportedDead = true;
		}
		else if (e.Forced && (e.ReportedDead || now - e.ForcedAt >= ForcedFor))
		{
			// Renasceu (o dono contou a morte e agora conta vivo), ou a espera passou.
			e.Forced = false;
			e.ReportedDead = false;
			e.Taken = 0;
		}
		else if (!e.Forced) e.ReportedDead = false;
	}

	/// <summary>Um acerto que o servidor aceitou neste bot. Verdadeiro: passou do que ele aguenta — o servidor mata.</summary>
	public bool Hit(long bot, float amount, double now)
	{
		Entry e = Of(bot);
		if (e.Forced) return false;
		if (now - e.LastHit > Forget) e.Taken = 0;
		e.LastHit = now;
		e.Taken += Math.Max(amount, 0f);
		if (e.Taken <= e.Pool * Times + Slack) return false;

		e.Forced = true;
		e.ForcedAt = now;
		e.ReportedDead = false;
		e.Taken = 0;
		Forced++;
		return true;
	}

	/// <summary>O servidor matou este bot e o dono ainda não o fez renascer: o que o dono conta dele não vale.</summary>
	public bool IsForcedDead(long bot) => _bots.TryGetValue(bot, out Entry e) && e.Forced;
}
