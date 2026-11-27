using System;

/// <summary>
/// A mira de cada jogador, vista do servidor (27/11, contra aimbot). O
/// acerto já é conferido contra o alvo (<c>HitCheck</c>); o que isso não
/// pega é a mira que não é de gente: o giro de dezenas de graus em dois ou
/// três passos que termina, sem nenhum ajuste, em cima do alvo — e o tiro
/// sai nesse passo. Uma pessoa faz isso uma vez ou outra (o tiro de
/// reflexo); um aimbot faz em quase todo acerto.
///
/// O servidor guarda para onde cada jogador olhava nos últimos comandos
/// (ele já os recebe, um por passo) e, a cada acerto de arma de fogo que
/// valeu, olha quanto a mira girou nos <see cref="SnapTicks"/> passos antes
/// do tiro. Não recusa nada: só conta. A conta vai no resultado da partida,
/// e quem decide é uma pessoa, no painel (<c>Plausibility</c>, no servidor
/// central) — acusar errado é pior do que deixar passar um.
///
/// C# puro, com teste de unidade.
/// </summary>
public sealed class AimAudit
{
	/// <summary>Em quantos passos antes do tiro o giro conta como "num instante" (0,05 s).</summary>
	public const int SnapTicks = 3;

	/// <summary>O giro, em graus, a partir do qual é um tranco de mira (20° em 0,05 s são 400°/s).</summary>
	public const float SnapDegrees = 20f;

	/// <summary>Acertos de arma de fogo a partir dos quais a proporção diz alguma coisa.</summary>
	public const int MinHits = 25;

	/// <summary>A parte dos acertos com tranco de mira a partir da qual vale olhar (por cento).</summary>
	public const int SnapShare = 40;

	private const int Window = 16;
	private readonly (uint Seq, float Yaw, float Pitch)[] _views = new (uint, float, float)[Window];
	private uint _lastHitSeq;

	/// <summary>Disparos de arma de fogo que acertaram (um por comando: os chumbos da escopeta contam um).</summary>
	public int Hits { get; private set; }

	/// <summary>Dos acertos, quantos vieram logo depois de um tranco de mira.</summary>
	public int Snaps { get; private set; }

	/// <summary>Para onde o jogador olhava neste comando.</summary>
	public void Record(uint seq, float yaw, float pitch) => _views[seq % Window] = (seq, yaw, pitch);

	/// <summary>Um acerto de arma de fogo que valeu, no comando <paramref name="seq"/>.</summary>
	public void Hit(uint seq)
	{
		if (seq == 0 || seq == _lastHitSeq) return;
		_lastHitSeq = seq;
		Hits++;
		if (Turned(seq) >= SnapDegrees) Snaps++;
	}

	/// <summary>Quanto a mira girou, em graus, nos <see cref="SnapTicks"/> passos antes deste comando (0 se não há registro).</summary>
	public float Turned(uint seq)
	{
		if (seq <= SnapTicks) return 0f;
		var now = _views[seq % Window];
		var before = _views[(seq - SnapTicks) % Window];
		if (now.Seq != seq || before.Seq != seq - SnapTicks) return 0f;
		float dot = System.Numerics.Vector3.Dot(HitRules.AimDirection(now.Yaw, now.Pitch), HitRules.AimDirection(before.Yaw, before.Pitch));
		return MathF.Acos(Math.Clamp(dot, -1f, 1f)) * 180f / MathF.PI;
	}

	/// <summary>Acertos bastantes, e quase todos depois de um tranco de mira.</summary>
	public bool Suspicious => Hits >= MinHits && Snaps * 100 >= Hits * SnapShare;
}
