using System;
using System.Numerics;

/// <summary>Por que o servidor recusou um acerto (ou <see cref="Ok"/>).</summary>
public enum HitVerdict : byte
{
	Ok,
	/// <summary>A partida não está valendo, alvo aliado com fogo amigo desligado, ou o juiz não aceita.</summary>
	NotAllowed,
	/// <summary>Quem atirou não está no mundo, ou estava morto quando atirou.</summary>
	ShooterGone,
	/// <summary>O alvo não está no mundo, já morreu, ou acabou de renascer (protegido).</summary>
	TargetGone,
	/// <summary>O tiro diz que saiu de um lugar longe de onde o atirador estava.</summary>
	BadOrigin,
	/// <summary>Longe demais para a arma.</summary>
	OutOfRange,
	/// <summary>Parede no caminho: nenhuma parte do alvo estava à vista.</summary>
	Blocked,
	/// <summary>O alvo estava fora da mira de quem atirou.</summary>
	BadAim,
	/// <summary>Mais dano por segundo do que a arma dá.</summary>
	TooFast,
	/// <summary>Arma que não estava na mão.</summary>
	WrongWeapon
}

/// <summary>
/// As contas do acerto conferido no servidor (decisão 0016): a mira, o
/// alcance, a volta no tempo e o ritmo. C# puro, sem Godot — a parte que
/// precisa do mundo (a parede no caminho) fica em <c>HitCheck</c>.
///
/// Tudo aqui tem folga a favor de quem atira: um acerto honesto recusado
/// é pior para o jogo do que um trapaceiro que passa raspando. O que a
/// folga não deixa passar é o que faz a trapaça valer a pena: atirar
/// através da parede, de qualquer distância, sem mirar, ou mais rápido e
/// mais forte do que a arma.
/// </summary>
public static class HitRules
{
	/// <summary>O tiro sai do olho de quem atira; aceita-se até tanto de
	/// distância entre o que o jogo diz e onde o servidor tem o corpo.</summary>
	public const float OriginSlack = 1.5f;

	/// <summary>Folga no alcance da arma, em metros.</summary>
	public const float RangeSlack = 2f;

	/// <summary>Folga na mira, em graus, além da dispersão da arma e do
	/// tamanho do alvo: o comando e o tiro saem no mesmo passo, mas a
	/// cabeça pode girar rápido entre dois passos.</summary>
	public const float AimSlackDegrees = 8f;

	/// <summary>Meia largura do corpo, para o tamanho do alvo na mira.</summary>
	public const float BodyRadius = 0.45f;

	/// <summary>Passos de folga em volta do instante que o jogo diz que via.</summary>
	public const int RewindSlackTicks = 3;

	/// <summary>Até quantos passos no passado o servidor volta.</summary>
	public static int MaxRewindTicks => (int)Math.Ceiling(NetRates.MaxRewind * NetRates.TickRate);

	/// <summary>Para onde aponta quem olha com estes ângulos (os do
	/// Godot: guinada em Y, -Z é a frente; arfagem positiva olha para cima).</summary>
	public static Vector3 AimDirection(float yaw, float pitch)
	{
		float cp = MathF.Cos(pitch);
		return new Vector3(-MathF.Sin(yaw) * cp, MathF.Sin(pitch), -MathF.Cos(yaw) * cp);
	}

	/// <summary>O ângulo, em graus, entre a mira e a direção do olho até o ponto.</summary>
	public static float AimErrorDegrees(Vector3 eye, float yaw, float pitch, Vector3 point)
	{
		Vector3 to = point - eye;
		float length = to.Length();
		if (length < 0.001f) return 0f;
		float dot = Math.Clamp(Vector3.Dot(AimDirection(yaw, pitch), to / length), -1f, 1f);
		return MathF.Acos(dot) * 180f / MathF.PI;
	}

	/// <summary>Quanto um corpo de raio <paramref name="radius"/> ocupa da
	/// mira a <paramref name="distance"/> metros, em graus (do centro à borda).</summary>
	public static float AngularRadiusDegrees(float distance, float radius = BodyRadius) =>
		distance <= radius ? 90f : MathF.Atan(radius / distance) * 180f / MathF.PI;

	/// <summary>A mira cabe: o erro é menor que o tamanho do alvo, mais a
	/// dispersão da arma, mais a folga.</summary>
	public static bool AimFits(float errorDegrees, float distance, float spreadDegrees) =>
		errorDegrees <= AngularRadiusDegrees(distance) + Math.Max(spreadDegrees, 0f) + AimSlackDegrees;

	/// <summary>A distância de um ponto ao segmento entre <paramref name="a"/> e <paramref name="b"/>.</summary>
	public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
	{
		Vector3 ab = b - a;
		float lengthSquared = ab.LengthSquared();
		if (lengthSquared < 1e-6f) return Vector3.Distance(point, a);
		float t = Math.Clamp(Vector3.Dot(point - a, ab) / lengthSquared, 0f, 1f);
		return Vector3.Distance(point, a + ab * t);
	}

	/// <summary>O passo em que o servidor procura o alvo: o que o jogo diz
	/// que via, mas nunca no futuro do tiro nem mais longe que
	/// <see cref="NetRates.MaxRewind"/> no passado.</summary>
	public static double RewindTick(double viewTick, uint fireTick)
	{
		double oldest = Math.Max(0, (double)fireTick - MaxRewindTicks);
		if (double.IsNaN(viewTick)) return oldest;
		return Math.Clamp(viewTick, oldest, fireTick);
	}

	/// <summary>
	/// Os pontos do alvo que o servidor procura à vista: cabeça, peito,
	/// meio, pernas e os dois lados do peito (de quem atira, os ombros).
	/// <paramref name="center"/> é o meio da cápsula; <paramref name="height"/>
	/// a altura dela (em pé ou agachado). Devolve quantos escreveu (6).
	/// </summary>
	public static int TargetPoints(Vector3 center, float height, Vector3 from, Span<Vector3> points)
	{
		float half = Math.Max(height, 0.6f) * 0.5f;
		Vector3 up = Vector3.UnitY;
		Vector3 across = Vector3.Cross(up, center - from);
		across = across.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(across);

		Vector3 chest = center + up * (half * 0.35f);
		points[0] = HeadPoint(center, height);
		points[1] = chest;
		points[2] = center;
		points[3] = center - up * (half - 0.3f);
		points[4] = chest + across * 0.3f;
		points[5] = chest - across * 0.3f;
		return 6;
	}

	/// <summary>Onde fica a cabeça de um corpo com o meio em <paramref name="center"/>.</summary>
	public static Vector3 HeadPoint(Vector3 center, float height) =>
		center + Vector3.UnitY * (Math.Max(height, 0.6f) * 0.5f - 0.15f);
}

/// <summary>
/// O ritmo de dano de quem atira: um balde que enche com o dano por
/// segundo que a arma dá e esvazia a cada acerto. Mais dano do que cabe no
/// balde é atirar mais rápido (ou mais forte) do que a arma deixa.
/// </summary>
public sealed class DamageBudget
{
	private double _level, _at = double.NaN;
	private float _rate, _burst;

	/// <summary>Dano por segundo que a arma em uso dá, no máximo, e quanto
	/// cabe de uma vez (uns disparos de folga).</summary>
	public void Configure(float perSecond, float burst)
	{
		_rate = Math.Max(perSecond, 0f);
		// Trocou de arma: o balde fica com o que tinha, até o novo tamanho.
		if (double.IsNaN(_at)) _level = burst;
		_burst = Math.Max(burst, 0f);
		_level = Math.Min(_level, _burst);
	}

	/// <summary>Gasta <paramref name="amount"/>. Falso (sem gastar) se não cabe.</summary>
	public bool Spend(float amount, double now)
	{
		if (!double.IsNaN(_at) && now > _at) _level = Math.Min(_burst, _level + (now - _at) * _rate);
		_at = double.IsNaN(_at) ? now : Math.Max(_at, now);
		if (amount > _level + 0.001) return false;
		_level -= amount;
		return true;
	}

	public double Level => _level;
}
