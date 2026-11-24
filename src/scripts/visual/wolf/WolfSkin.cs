using Godot;
using System.Collections.Generic;

/// <summary>
/// O material do corpo do lobo (assets/efeitos/lobo.gdshader) no lugar do
/// que vem do arquivo: as mesmas texturas, mais o pelo que balança, o neon
/// que respira e acende, e o brilho de contorno no pelo (pedido do Jhon,
/// 18/11). Vale para qualquer lobo: o do time (lobo_azul/lobo_vermelho,
/// que trazem as máscaras na cor de vértice e a textura de emissão — veja
/// tools/lobo/polir.py) e o de um personagem (sem máscaras: só o material).
///
/// Um material por material de origem, dividido por todos os lobos que
/// usam o modelo; o que muda de lobo para lobo (o empurrão do pelo, o
/// neon aceso) é parâmetro de instância, posto pelo <see cref="WolfFx"/>.
/// </summary>
public static class WolfSkin
{
	public const string ShaderPath = "res://src/assets/efeitos/lobo.gdshader";

	private static readonly Dictionary<ulong, ShaderMaterial> Made = new();

	/// <summary>Troca o material de cada malha do modelo. Devolve as
	/// malhas trocadas.</summary>
	public static List<MeshInstance3D> Apply(Node model)
	{
		var done = new List<MeshInstance3D>();
		if (model == null || !ResourceLoader.Exists(ShaderPath)) return done;
		Shader shader = GD.Load<Shader>(ShaderPath);

		foreach (Node node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (node is not MeshInstance3D { Mesh: not null } mesh) continue;

			bool any = false;
			for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
			{
				if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source || source.AlbedoTexture == null) continue;

				bool masks = mesh.Mesh is ArrayMesh array
					&& (array.SurfaceGetFormat(surface) & Mesh.ArrayFormat.FormatColor) != 0;
				mesh.SetSurfaceOverrideMaterial(surface, MaterialFor(shader, source, masks));
				any = true;
			}

			if (any) done.Add(mesh);
		}

		return done;
	}

	private static ShaderMaterial MaterialFor(Shader shader, BaseMaterial3D source, bool masks)
	{
		ulong key = source.GetInstanceId() * 2 + (masks ? 1UL : 0UL);
		if (Made.TryGetValue(key, out ShaderMaterial made) && GodotObject.IsInstanceValid(made)) return made;

		made = new ShaderMaterial { Shader = shader };
		made.SetShaderParameter("albedo_tex", source.AlbedoTexture);
		made.SetShaderParameter("albedo", source.AlbedoColor);
		made.SetShaderParameter("use_normal", source.NormalEnabled && source.NormalTexture != null);
		if (source.NormalTexture != null) made.SetShaderParameter("normal_tex", source.NormalTexture);

		// O ORM do glTF: oclusão no R, rugosidade no G, metal no B.
		Texture2D orm = source.RoughnessTexture ?? source.MetallicTexture;
		made.SetShaderParameter("use_orm", orm != null);
		if (orm != null) made.SetShaderParameter("orm_tex", orm);
		made.SetShaderParameter("roughness", source.Roughness);
		made.SetShaderParameter("metallic", source.Metallic);

		if (source.EmissionEnabled && source.EmissionTexture != null)
			made.SetShaderParameter("emission_tex", source.EmissionTexture);
		made.SetShaderParameter("masks", masks ? 1f : 0f);

		Made[key] = made;
		return made;
	}
}
