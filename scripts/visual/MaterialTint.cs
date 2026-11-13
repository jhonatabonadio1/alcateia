using Godot;
using System;

/// <summary>
/// Pinta um modelo: em cada superfície de cada malha da árvore, troca o
/// material por uma cópia com a cor pedida. Cópia, porque o material
/// original é o mesmo para todo mundo que usa o modelo — pintar o
/// original pintaria o time inteiro, dos dois lados.
///
/// O humano (cor sólida do time, juntas mais escuras) e o lobo (a textura
/// tingida pela cor do time) decidem a cor; o resto é igual.
/// </summary>
public static class MaterialTint
{
	public static void Paint(Node root, Func<MeshInstance3D, BaseMaterial3D, Color> color)
	{
		if (root is MeshInstance3D mesh && mesh.Mesh != null)
		{
			for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
			{
				if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source) continue;

				var copy = (BaseMaterial3D)source.Duplicate();
				copy.AlbedoColor = color(mesh, source);
				mesh.SetSurfaceOverrideMaterial(surface, copy);
			}
		}

		foreach (Node child in root.GetChildren())
			Paint(child, color);
	}
}
