using Godot;

/// <summary>
/// As malhas de um corpo animado não somem na beira da tela.
///
/// A caixa com que o Godot decide se desenha uma malha com esqueleto é a
/// da pose de descanso (os braços abertos; no lobo, que veio em
/// centímetros, 2 cm em volta dos pés). O corpo é feito de várias malhas —
/// cabelo, cabeça, luvas, tronco, calça, botas — e, com a câmera perto e
/// de cima (o espectador), cada uma saía da conta num momento diferente:
/// pedaços do personagem sumindo e voltando (Jhon, 20/11). A folga de
/// 2,5 m cobre qualquer pose e custa nada: um personagem a menos de 2,5 m
/// da tela é desenhado.
/// </summary>
public static class SkinCulling
{
	public const float Margin = 2.5f;

	public static void Loosen(Node model)
	{
		if (model == null) return;
		if (model is MeshInstance3D mesh && mesh.ExtraCullMargin < Margin) mesh.ExtraCullMargin = Margin;
		foreach (Node child in model.GetChildren()) Loosen(child);
	}
}
