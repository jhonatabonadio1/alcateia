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
///
/// **E sem LOD.** O importador do Godot gerava versões simplificadas das
/// malhas dos personagens (meshes/generate_lods) e, a poucos metros, trocava
/// para elas: o cabelo virava casca oca, a jaqueta abria buracos pretos e o
/// rosto perdia a pele em volta dos olhos e da boca (Jhon, 21/11: "os
/// personagens estão com estes problemas de renderização"). O lobo já tinha
/// isso desligado; a Eve e o Barut não. Desligado no .import deles, e aqui,
/// por garantia, a malha nunca troca de detalhe.
/// </summary>
public static class SkinCulling
{
	public const float Margin = 2.5f;

	/// <summary>O maior "lod_bias" do Godot: a malha fica na versão cheia a
	/// qualquer distância.</summary>
	public const float FullDetail = 128f;

	public static void Loosen(Node model)
	{
		if (model == null) return;
		if (model is MeshInstance3D mesh)
		{
			if (mesh.ExtraCullMargin < Margin) mesh.ExtraCullMargin = Margin;
			mesh.LodBias = FullDetail;
		}
		foreach (Node child in model.GetChildren()) Loosen(child);
	}
}
