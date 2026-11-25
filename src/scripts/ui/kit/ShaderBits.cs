/// <summary>
/// Pedaços de shader que mais de uma tela usa, para entrar no código de cada
/// uma (o fundo do login e o do menu dividem o ruído). Antes cada uma tinha a
/// sua cópia, e o tools/duplicados.py reprovava.
/// </summary>
public static class ShaderBits
{
	/// <summary>Ruído de valor em 2D: <c>hash(p)</c> e <c>vnoise(p)</c>, de 0 a 1.</summary>
	public const string ValueNoise = @"
float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
";
}
