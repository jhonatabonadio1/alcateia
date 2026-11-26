using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A sola de cada pé (e de cada mão) do lobo como ela está na tela: os
/// vértices da pele levados pelo esqueleto na pose do quadro, separados pelo
/// osso que mais pesa neles. Para cada membro, o ponto mais baixo e o chão
/// de verdade embaixo dele (um raio na colisão do mapa). Serve aos testes do
/// pé no chão (o pé não entra nem flutua).
/// </summary>
public sealed class WolfFeetProbe
{
	public enum Limb { LeftFoot, RightFoot, LeftHand, RightHand }

	/// <summary>O ponto mais baixo de um membro e o chão embaixo dele:
	/// <see cref="Gap"/> positivo é ar entre a sola e o chão; negativo, a sola
	/// dentro do chão.</summary>
	public readonly struct Contact
	{
		public readonly Vector3 Lowest;
		public readonly float Floor;
		public readonly bool HasFloor;
		public Contact(Vector3 lowest, float floor, bool hasFloor) { Lowest = lowest; Floor = floor; HasFloor = hasFloor; }
		public float Gap => HasFloor ? Lowest.Y - Floor : float.NaN;
	}

	private sealed class Part
	{
		public MeshInstance3D Mesh;
		public Skin Skin;
		public Skeleton3D Skeleton;
		public Vector3[] Vertices;
		public int[] Bones;
		public float[] Weights;
		public int Per;
		public Limb?[] LimbOf;
	}

	private readonly List<Part> _parts = new();
	private readonly CharacterBody3D _body;

	public WolfFeetProbe(CharacterBody3D body, Node3D model)
	{
		_body = body;
		foreach (MeshInstance3D mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (mesh.Mesh is not ArrayMesh array || mesh.Skin == null) continue;
			Skeleton3D skeleton = mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton);
			if (skeleton == null) continue;
			for (int s = 0; s < array.GetSurfaceCount(); s++)
			{
				var arrays = array.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				var part = new Part
				{
					Mesh = mesh, Skin = mesh.Skin, Skeleton = skeleton,
					Vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(),
					Bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array(),
					Weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array(),
				};
				part.Per = part.Vertices.Length > 0 ? part.Bones.Length / part.Vertices.Length : 4;
				part.LimbOf = new Limb?[part.Vertices.Length];
				for (int v = 0; v < part.Vertices.Length; v++)
				{
					int dominant = -1;
					float strongest = 0f;
					for (int k = 0; k < part.Per; k++)
					{
						float w = part.Weights[v * part.Per + k];
						if (w > strongest) { strongest = w; dominant = part.Bones[v * part.Per + k]; }
					}
					if (dominant < 0) continue;
					int bone = part.Skin.GetBindBone(dominant);
					string name = bone >= 0 ? skeleton.GetBoneName(bone) : part.Skin.GetBindName(dominant).ToString();
					part.LimbOf[v] = LimbOfBone(name);
				}
				_parts.Add(part);
			}
		}
	}

	private static Limb? LimbOfBone(string name)
	{
		if (name.EndsWith("LeftFoot") || name.EndsWith("LeftToeBase")) return Limb.LeftFoot;
		if (name.EndsWith("RightFoot") || name.EndsWith("RightToeBase")) return Limb.RightFoot;
		if (name.EndsWith("LeftHand")) return Limb.LeftHand;
		if (name.EndsWith("RightHand")) return Limb.RightHand;
		return null;
	}

	/// <summary>O ponto mais baixo de cada membro agora, com o chão embaixo.</summary>
	public Dictionary<Limb, Contact> Measure()
	{
		var lowest = new Dictionary<Limb, Vector3>();
		foreach (Part part in _parts)
		{
			if (!part.Mesh.IsVisibleInTree()) continue;
			var binds = new Transform3D[part.Skin.GetBindCount()];
			for (int k = 0; k < binds.Length; k++)
			{
				int bone = part.Skin.GetBindBone(k);
				if (bone < 0) bone = part.Skeleton.FindBone(part.Skin.GetBindName(k));
				binds[k] = part.Skeleton.GlobalTransform * part.Skeleton.GetBoneGlobalPose(bone) * part.Skin.GetBindPose(k);
			}
			for (int v = 0; v < part.Vertices.Length; v++)
			{
				if (part.LimbOf[v] is not Limb limb) continue;
				Vector3 p = Vector3.Zero;
				for (int k = 0; k < part.Per; k++)
				{
					float w = part.Weights[v * part.Per + k];
					if (w > 0f) p += binds[part.Bones[v * part.Per + k]] * part.Vertices[v] * w;
				}
				if (!lowest.TryGetValue(limb, out Vector3 low) || p.Y < low.Y) lowest[limb] = p;
			}
		}

		var result = new Dictionary<Limb, Contact>();
		PhysicsDirectSpaceState3D space = _body.GetWorld3D().DirectSpaceState;
		foreach (var (limb, p) in lowest)
		{
			var query = PhysicsRayQueryParameters3D.Create(p + Vector3.Up * 0.6f, p + Vector3.Down * 1.5f, 1);
			query.Exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
			var hit = space.IntersectRay(query);
			result[limb] = hit.Count > 0 ? new Contact(p, ((Vector3)hit["position"]).Y, true) : new Contact(p, 0f, false);
		}
		return result;
	}
}
