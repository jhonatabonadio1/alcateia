using Godot;

/// <summary>
/// Curvas de utilidade.
///
/// Uma consideração transforma um número do mundo — distância, vida,
/// munição — num valor entre 0 e 1 que diz "o quanto isto favorece a
/// ação". A curva é onde mora o comportamento: a mesma distância pode
/// favorecer avançar e desfavorecer atirar, e é a forma da curva que
/// decide qual vence.
///
/// Manter as curvas separadas da decisão é o que torna o sistema
/// ajustável sem reescrever lógica. Trocar uma curva muda a
/// personalidade do bot; trocar um `if` muda o programa.
/// </summary>
public static class Utility
{
	/// <summary>Normaliza um valor para 0..1 dentro de uma faixa.</summary>
	public static float Normalize(float value, float min, float max)
	{
		if (Mathf.IsEqualApprox(min, max)) return 0f;

		return Mathf.Clamp((value - min) / (max - min), 0f, 1f);
	}

	/// <summary>Cresce devagar e acelera no fim. Para o que só importa
	/// quando chega perto do extremo.</summary>
	public static float Quadratic(float x) => x * x;

	/// <summary>Cresce rápido e satura. Para o que já importa muito
	/// com pouco — ouvir um tiro, por exemplo.</summary>
	public static float Inverse(float x) => 1f - (1f - x) * (1f - x);

	/// <summary>Vale 1 no meio da faixa e cai nas pontas. Para
	/// distância preferida: nem colado, nem longe demais.</summary>
	public static float Band(float x, float center, float width)
	{
		float distance = Mathf.Abs(x - center) / Mathf.Max(width, 0.001f);

		return Mathf.Clamp(1f - distance, 0f, 1f);
	}

	/// <summary>
	/// Compensa a queda do produto quando há muitas considerações.
	///
	/// Multiplicar cinco valores de 0.8 dá 0.33, e uma ação com muitas
	/// condições boas perderia para uma ação com uma condição medíocre.
	/// A compensação puxa o resultado de volta na proporção do número
	/// de fatores. É a correção clássica de Dave Mark.
	/// </summary>
	public static float Compensate(float score, int factors)
	{
		if (factors <= 1) return score;

		float modification = 1f - 1f / factors;
		float makeUp = (1f - score) * modification;

		return score + makeUp * score;
	}
}
