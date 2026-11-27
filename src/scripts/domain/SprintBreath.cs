using System;

/// <summary>
/// O fôlego da corrida do humano (Jhon, 27/11: "coloque um tempo máximo que o
/// jogador pode correr: pode correr por X tempo e depois espera X tempo para
/// correr de novo, como se tivesse perdido o fôlego; veja algo que fica
/// equilibrado para ele não esperar muito para correr, cooldown bem rápido").
///
/// Um tanque em segundos de corrida: correndo, esvazia; sem correr, enche.
/// Esvaziou de todo: **sem fôlego** — não corre por <see cref="WindedWait"/>
/// segundos, mesmo segurando o Shift; depois corre com o que encheu. Parar
/// antes de esvaziar não tem castigo: o tanque volta a encher na hora.
///
/// Os números (decisão minha, 27/11, para o Jhon ajustar): <b>4 s</b> de
/// corrida (uns 20 m a 4,91 m/s), <b>1,5 s</b> sem fôlego, e o tanque enche
/// inteiro em <b>3 s</b> — ao fim da espera já há 2 s de corrida. Quem segura
/// o Shift sem parar corre 4 s, e depois 2 s a cada 3,5 s (57% do tempo): em
/// média uns 3,9 m/s, e o lobo a pé (4,33 m/s) volta a ser mais rápido que o
/// humano numa corrida longa — a mobilidade é do lobo (regra 9 do guia).
///
/// C# puro e determinístico (o passo de física): o corpo do servidor conta o
/// mesmo fôlego e corrige quem corresse além dele.
/// </summary>
public sealed class SprintBreath
{
	public float Capacity { get; }
	public float RefillTime { get; }
	public float WindedWait { get; }

	/// <summary>Segundos de corrida no tanque.</summary>
	public float Left { get; private set; }

	/// <summary>Segundos de espera sem fôlego que ainda faltam (0: tem fôlego).</summary>
	public float WindedLeft { get; private set; }

	public bool Winded => WindedLeft > 0f;

	/// <summary>O tanque, de 0 a 1.</summary>
	public float Fraction => Capacity > 0f ? Left / Capacity : 1f;

	public SprintBreath(float capacity = 4f, float refillTime = 3f, float windedWait = 1.5f)
	{
		Capacity = Math.Max(capacity, 0.1f);
		RefillTime = Math.Max(refillTime, 0.1f);
		WindedWait = Math.Max(windedWait, 0f);
		Left = Capacity;
	}

	/// <summary>Dá para começar ou seguir correndo agora?</summary>
	public bool CanSprint => !Winded && Left > 0f;

	/// <summary>Um passo: <paramref name="sprinting"/> se correu neste passo.</summary>
	public void Step(float dt, bool sprinting)
	{
		if (dt <= 0f) return;
		if (Winded)
		{
			WindedLeft = Math.Max(WindedLeft - dt, 0f);
			Left = Math.Min(Capacity, Left + dt * Capacity / RefillTime);
			return;
		}
		if (sprinting)
		{
			Left = Math.Max(Left - dt, 0f);
			if (Left <= 0f) WindedLeft = WindedWait;
		}
		else Left = Math.Min(Capacity, Left + dt * Capacity / RefillTime);
	}

	/// <summary>Morreu, renasceu: o fôlego volta cheio.</summary>
	public void Reset()
	{
		Left = Capacity;
		WindedLeft = 0f;
	}
}
