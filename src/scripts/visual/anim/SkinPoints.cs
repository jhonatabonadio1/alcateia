using Godot;
using System.Linq;

/// <summary>
/// A pele de um modelo como ela está na tela: os vértices levados pelo
/// esqueleto, na pose do quadro. Serve para assentar um modelo no chão (a
/// vitrine) e para os testes medirem se algo atravessa.
/// </summary>
public static class SkinPoints
{
	/// <summary>O ponto mais baixo (y no mundo) e o mais longe do eixo y, na
	/// horizontal, de toda a pele **deformada pelo esqueleto** do modelo
	/// (vértice a vértice, um sim um não), na pose em que ele está agora. Com
	/// <paramref name="bone"/>, só a pele que um osso aceito puxa mais (pelo
	/// nome dele: a sola de um pé, por exemplo).</summary>
	public static (float Low, float Far) Lowest(Node3D model, System.Func<string, bool> bone = null)
	{
		float low = float.MaxValue, far = 0f;
		foreach (MeshInstance3D mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (!mesh.IsVisibleInTree() || mesh.Mesh == null) continue;
			Skeleton3D skeleton = mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton);
			Skin skin = mesh.Skin;
			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				var arrays = mesh.Mesh.SurfaceGetArrays(s);
				var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				bool skinned = skeleton != null && skin != null && bones != null && weights != null && bones.Length >= verts.Length * 4;
				int per = skinned ? bones.Length / verts.Length : 0;
				if (bone != null && !skinned) continue;
				var binds = new Transform3D[skinned ? skin.GetBindCount() : 0];
				var accepted = new bool[binds.Length];
				for (int k = 0; k < binds.Length; k++)
				{
					int b = skin.GetBindBone(k);
					if (b < 0) b = skeleton.FindBone(skin.GetBindName(k));
					binds[k] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(b) * skin.GetBindPose(k);
					accepted[k] = bone == null || bone(skeleton.GetBoneName(b));
				}
				for (int v = 0; v < verts.Length; v += 2)
				{
					Vector3 p;
					if (skinned)
					{
						p = Vector3.Zero;
						int most = -1;
						float heaviest = 0f;
						for (int j = 0; j < per; j++)
						{
							float w = weights[v * per + j];
							if (w <= 0f) continue;
							p += binds[bones[v * per + j]] * verts[v] * w;
							if (w > heaviest) { heaviest = w; most = bones[v * per + j]; }
						}
						if (bone != null && (most < 0 || !accepted[most])) continue;
					}
					else p = mesh.GlobalTransform * verts[v];
					low = Mathf.Min(low, p.Y);
					far = Mathf.Max(far, new Vector2(p.X, p.Z).Length());
				}
			}
		}
		return (low, far);
	}
}
