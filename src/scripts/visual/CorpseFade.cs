using Godot;

/// <summary>O sumiço do corpo morto (provisório: só a marca, para o teste medir o antes).</summary>
public static class CorpseFade
{
	public const string Mark = "corpo_sumindo";

	/// <summary>O material é o da passada de profundidade do sumiço.</summary>
	public static bool IsFading(Material material) => material != null && material.HasMeta(Mark);
}
