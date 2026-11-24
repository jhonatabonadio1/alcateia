using Godot;
using System.Collections.Generic;

/// <summary>
/// A marca do tiro (pedido do Jhon, 20/11: "ao atirar, o local que
/// recebeu o tiro deve ficar marcado e depois sumir"). Cada bala que bate
/// no mapa — parede, chão, carro, caixa — deixa um furo com a lasca em
/// volta, que fica <see cref="Life"/> segundos e some esmaecendo.
///
/// É um decal (projeção), não um plano colado na superfície: ele se
/// deita no que estiver ali, em qualquer forma, e não fica boiando quando
/// a colisão é uma caixa um pouco à frente do desenho — se não há nada
/// que se veja ali, simplesmente não aparece. A projeção é rasa
/// (<see cref="Depth"/>) e só pega o que olha para o mesmo lado, para não
/// escorrer pela quina nem sujar quem passa colado à parede.
///
/// Texturas: <c>assets/efeitos/marca_tiro.png</c> e o relevo
/// <c>marca_tiro_n.png</c>, feitas por <c>tools/efeitos/marca_tiro.py</c>.
/// O decal é do Forward+ (o renderizador do jogo).
/// </summary>
public static class BulletMarks
{
	/// <summary>Quantas marcas ficam no mapa ao mesmo tempo; a mais velha sai primeiro.</summary>
	public const int MaxMarks = 96;

	/// <summary>Quanto tempo a marca fica inteira, em segundos, e quanto leva para sumir.</summary>
	public const float Life = 10f;
	public const float FadeTime = 2.5f;

	/// <summary>O tamanho do furo com a lasca (m) e a fundura da projeção (m).</summary>
	public const float Size = 0.17f;
	public const float Depth = 0.24f;

	private static readonly Queue<Decal> Marks = new();
	private static readonly RandomNumberGenerator Rng = new();
	private static Texture2D _albedo, _normal;

	/// <summary>Uma marca no ponto em que a bala bateu, deitada na superfície de normal <paramref name="normal"/>.</summary>
	public static void Add(Node world, Vector3 point, Vector3 normal, Node collider)
	{
		if (world is not Node3D world3D || !world3D.IsInsideTree()) return;
		// Corpo de gente não leva marca (o sangue é dele), nem a parede invisível do limite.
		if (collider is CharacterBody3D) return;
		if (collider is StaticBody3D body)
		{
			string name = body.Name;
			if (name.StartsWith("Barreira") || name.StartsWith("Limite")) return;
		}
		if (normal.LengthSquared() < 1e-6f) return;

		_albedo ??= GD.Load<Texture2D>("res://src/assets/efeitos/marca_tiro.png");
		_normal ??= GD.Load<Texture2D>("res://src/assets/efeitos/marca_tiro_n.png");

		float size = Size * Rng.RandfRange(0.8f, 1.2f);
		var mark = new Decal
		{
			Name = "MarcaDeTiro",
			Size = new Vector3(size, Depth, size),
			TextureAlbedo = _albedo,
			TextureNormal = _normal,
			AlbedoMix = 1f,
			// Só no que está de frente (a quina não estica a marca) e sem aparecer de longe à toa.
			NormalFade = 0.45f,
			UpperFade = 0.2f,
			LowerFade = 0.2f,
			DistanceFadeEnabled = true,
			DistanceFadeBegin = 45f,
			DistanceFadeLength = 12f
		};
		world3D.AddChild(mark);

		// O decal projeta no −Y dele: o Y vira a normal, com um giro sorteado em volta dela.
		Vector3 y = normal.Normalized();
		Vector3 x = y.Cross(Mathf.Abs(y.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized();
		Vector3 z = x.Cross(y);
		var basis = new Basis(x, y, z).Rotated(y, Rng.RandfRange(0f, Mathf.Tau));
		mark.GlobalTransform = new Transform3D(basis, point);

		Tween fade = mark.CreateTween();
		fade.TweenInterval(Life);
		fade.TweenProperty(mark, "modulate:a", 0f, FadeTime);
		fade.TweenCallback(Callable.From(mark.QueueFree));

		Marks.Enqueue(mark);
		while (Marks.Count > MaxMarks)
		{
			Decal old = Marks.Dequeue();
			if (GodotObject.IsInstanceValid(old)) old.QueueFree();
		}
	}
}
