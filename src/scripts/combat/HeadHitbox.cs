using Godot;

/// <summary>
/// A cabeça de quem leva tiro, no modelo que se vê: a peça da cabeça de
/// <see cref="BodyHitbox"/> (a cápsula ajustada à pele da cabeça, 25/11).
/// Até 25/11 era uma esfera de raio 0,55 vezes a distância entre os ossos
/// <c>mixamorig_Head</c> e <c>mixamorig_HeadTop_End</c>: 12 cm no humano,
/// que passava da cabeça pelos lados e por baixo (Jhon: "tá muito fácil dar
/// headshot").
///
/// Fica para quem só quer onde a cabeça está (a mira dos testes): o meio da
/// peça e a bola que a contém.
/// </summary>
public static class HeadHitbox
{
	/// <summary>A cabeça de <paramref name="body"/> agora: o meio da peça e a
	/// bola que a contém. Falso: ele não tem modelo com cabeça à vista.</summary>
	public static bool TryGet(Node3D body, out Vector3 center, out float radius) =>
		BodyHitbox.TryGetHead(body, out center, out radius);

	/// <summary>O corpo tem modelo à vista. Quem tem não leva tiro na cabeça
	/// pela faixa da cápsula, só pela peça.</summary>
	public static bool Has(Node3D body) => BodyHitbox.Has(body);
}
