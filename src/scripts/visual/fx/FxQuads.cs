using Godot;

/// <summary>
/// Partículas desenhadas de uma vez: cada uma é um quadradinho de frente
/// para a câmera, com a própria cor (as faíscas do choque, ShockBlast; a
/// poeira da corrida, SprintDust). Antes os dois montavam isto cada um, e o
/// tools/duplicados.py reprovava.
/// </summary>
public static class FxQuads
{
	/// <summary>Um MultiMesh de <paramref name="count"/> quadradinhos com a textura
	/// dada, sem luz, transparentes e virados para a câmera. Com
	/// <paramref name="additive"/>, a cor soma (brilha) em vez de cobrir.</summary>
	public static MultiMesh Cloud(Texture2D texture, int count, bool additive = false) => new()
	{
		TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
		UseColors = true,
		Mesh = new QuadMesh
		{
			Size = Vector2.One,
			Material = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoTexture = texture,
				VertexColorUseAsAlbedo = true,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
				BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
				BillboardKeepScale = true
			}
		},
		InstanceCount = count
	};
}
