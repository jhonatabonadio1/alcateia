using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Shape = BodyHit.Shape;

/// <summary>
/// O corpo que leva tiro, no modelo que se vê (Jhon, 25/11: "ajuste o hitbox
/// dos personagens, deixe mais preciso, exigir mais mira para dar headshot").
/// Cada peça (cabeça, pescoço, tronco, braço, antebraço, mão, coxa, canela,
/// pé) é uma peça de <see cref="BodyHit"/> ajustada à pele que pesa no
/// osso dela, e segue o osso: o lobo curvado, o humano mirando, agachado.
///
/// O ajuste sai uma vez por modelo (o arquivo do personagem), na pose em que
/// ele está: a pele deformada pelo esqueleto, levada ao espaço de cada osso.
/// Fica guardado só em números (as peças), não em recurso do Godot.
///
/// O rabo do lobo não leva tiro (decisão de 25/11, junto com o rabo: ele
/// balança solto e daria acerto fantasma). Sem modelo à vista (o servidor tira
/// tudo que não colide; o boneco do treino é uma cápsula), não há peças, e a
/// arma volta para a cápsula de movimento e a faixa de cima dela
/// (<see cref="Weapon.HeadZone"/>).
/// </summary>
public static class BodyHitbox
{
	/// <summary>Uma peça no mundo: a forma e se ela é a cabeça.</summary>
	public readonly record struct Part(Shape Shape, bool Head, string Bone);

	/// <summary>Menos pontos que isto e a peça vai para o osso pai (a
	/// clavícula entra no tronco, um dedo solto entra na mão).</summary>
	private const int MinPoints = 16;

	/// <summary>Quanto do peso de um ponto da pele tem de ser da cabeça para ele
	/// entrar na peça da cabeça.</summary>
	private const float HeadWeight = 0.8f;

	private sealed record Fitted(int Bone, Shape Local, bool Head, string Name);

	private sealed record Rig(Skeleton3D Skeleton, Fitted[] Parts);

	private sealed class Entry
	{
		public Rig[] Rigs = System.Array.Empty<Rig>();
		public ulong BuiltAt;
	}

	/// <summary>Os esqueletos de cada corpo, achados uma vez. Some com o corpo.</summary>
	private static readonly ConditionalWeakTable<Node3D, Entry> Cache = new();

	/// <summary>As peças de cada modelo, pelo arquivo dele: números, não
	/// recursos (nada que o coletor do C# precise soltar na saída).</summary>
	private static readonly Dictionary<string, Fitted[]> Models = new();

	/// <summary>As peças de <paramref name="body"/> agora, no mundo. Falso: ele
	/// não tem modelo à vista.</summary>
	public static bool TryGet(Node3D body, List<Part> into)
	{
		into.Clear();
		Entry entry = Cache.GetValue(body, _ => new Entry());
		if (!Find(entry, out Rig rig) && !(Stale(entry) && Rebuild(entry, body) && Find(entry, out rig))) return false;

		Skeleton3D skeleton = rig.Skeleton;
		Transform3D at = skeleton.GlobalTransform;
		foreach (Fitted part in rig.Parts)
		{
			Transform3D bone = at * skeleton.GetBoneGlobalPose(part.Bone);
			Vector3 scale = bone.Basis.Scale;
			float s = (scale.X + scale.Y + scale.Z) / 3f;
			into.Add(new Part(part.Local.Moved(p => N(bone * G(p)), d => N(bone.Basis * G(d)), s), part.Head, part.Name));
		}
		return into.Count > 0;
	}

	/// <summary>A cabeça agora: o meio da peça e a bola que a contém. Falso
	/// sem modelo à vista.</summary>
	public static bool TryGetHead(Node3D body, out Vector3 center, out float reach)
	{
		center = Vector3.Zero;
		reach = 0f;
		var parts = new List<Part>();
		if (!TryGet(body, parts)) return false;
		foreach (Part part in parts)
		{
			if (!part.Head) continue;
			center = G(part.Shape.Center);
			reach = part.Shape.Reach;
			return true;
		}
		return false;
	}

	/// <summary>O corpo tem modelo à vista agora (leva tiro pelas peças).</summary>
	public static bool Has(Node3D body)
	{
		Entry entry = Cache.GetValue(body, _ => new Entry());
		return Find(entry, out _) || (Stale(entry) && Rebuild(entry, body) && Find(entry, out _));
	}

	private static bool Find(Entry entry, out Rig found)
	{
		foreach (Rig rig in entry.Rigs)
		{
			if (!GodotObject.IsInstanceValid(rig.Skeleton) || !rig.Skeleton.IsVisibleInTree()) continue;
			found = rig;
			return true;
		}
		found = null;
		return false;
	}

	/// <summary>Nada à vista: o modelo pode ter nascido ou trocado depois da
	/// última busca (o lobo monta o dele no _Ready, e de novo ao trocar de
	/// time). Procura de novo, no máximo uma vez por segundo.</summary>
	private static bool Stale(Entry entry) =>
		entry.BuiltAt == 0 || Time.GetTicksMsec() - entry.BuiltAt >= 1000;

	private static bool Rebuild(Entry entry, Node3D body)
	{
		var rigs = new List<Rig>();
		foreach (string root in new[] { "Human", "WolfVisual" })
		{
			if (body.GetNodeOrNull(root) is not Node node) continue;
			foreach (Skeleton3D skeleton in node.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>())
			{
				Fitted[] parts = PartsOf(skeleton);
				if (parts.Length > 0 && parts.Any(p => p.Head)) rigs.Add(new Rig(skeleton, parts));
			}
		}
		entry.Rigs = rigs.ToArray();
		entry.BuiltAt = System.Math.Max(Time.GetTicksMsec(), 1);
		return entry.Rigs.Length > 0;
	}

	/// <summary>As peças do modelo deste esqueleto: do guardado, ou ajustadas agora.</summary>
	private static Fitted[] PartsOf(Skeleton3D skeleton)
	{
		string key = ModelKey(skeleton);
		if (key != null && Models.TryGetValue(key, out Fitted[] known)) return known;
		Fitted[] parts = FitParts(skeleton);
		if (key != null && parts.Length > 0) Models[key] = parts;
		return parts;
	}

	/// <summary>O arquivo do modelo (a cena instanciada mais perto do
	/// esqueleto) mais o número de ossos.</summary>
	private static string ModelKey(Skeleton3D skeleton)
	{
		for (Node n = skeleton; n != null; n = n.GetParent())
			if (!string.IsNullOrEmpty(n.SceneFilePath)) return $"{n.SceneFilePath}#{skeleton.GetBoneCount()}";
		return null;
	}

	/// <summary>O grupo de cada osso: a cabeça leva tudo o que sai dela (topo,
	/// focinho, olhos, cabelo), a mão leva os dedos, o pé leva os dedos do pé;
	/// o rabo fica de fora (-1).</summary>
	private static int[] Groups(Skeleton3D skeleton)
	{
		int count = skeleton.GetBoneCount();
		var group = new int[count];
		for (int i = 0; i < count; i++)
		{
			group[i] = i;
			for (int up = i; up >= 0; up = skeleton.GetBoneParent(up))
			{
				string name = skeleton.GetBoneName(up);
				if (name.StartsWith("rabo")) { group[i] = -1; break; }
				if (name.EndsWith("Head") || name.EndsWith("Hand") || name.EndsWith("Foot")) { group[i] = up; break; }
			}
		}
		return group;
	}

	/// <summary>Ajusta as peças: a pele deformada pelo esqueleto, agora,
	/// cada ponto no osso que mais pesa nele (pelo grupo), levado ao espaço
	/// desse osso.</summary>
	private static Fitted[] FitParts(Skeleton3D skeleton)
	{
		int[] group = Groups(skeleton);
		var head = new bool[group.Length];
		for (int i = 0; i < group.Length; i++) head[i] = skeleton.GetBoneName(i).EndsWith("Head");
		var points = new Dictionary<int, List<System.Numerics.Vector3>>();
		var inverse = new Dictionary<int, Transform3D>();
		Transform3D at = skeleton.GlobalTransform;

		foreach (MeshInstance3D mesh in skeleton.GetParent().FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (mesh.Mesh == null || mesh.Skin is not Skin skin || mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) != skeleton) continue;
			var binds = new Transform3D[skin.GetBindCount()];
			var bindBone = new int[binds.Length];
			for (int k = 0; k < binds.Length; k++)
			{
				int bone = skin.GetBindBone(k);
				if (bone < 0) bone = skeleton.FindBone(skin.GetBindName(k));
				bindBone[k] = bone;
				binds[k] = bone < 0 ? Transform3D.Identity : at * skeleton.GetBoneGlobalPose(bone) * skin.GetBindPose(k);
			}

			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				var arrays = mesh.Mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				int per = verts.Length > 0 ? bones.Length / verts.Length : 0;
				if (per == 0) continue;
				for (int v = 0; v < verts.Length; v++)
				{
					Vector3 p = Vector3.Zero;
					int dominant = -1, body = -1;
					float strongest = 0f, strongestBody = 0f, onHead = 0f;
					for (int j = 0; j < per; j++)
					{
						float w = weights[v * per + j];
						if (w <= 0f) continue;
						int bind = bones[v * per + j];
						if (bind < 0 || bind >= binds.Length) continue;
						p += binds[bind] * verts[v] * w;
						int bone = bindBone[bind];
						bool isHead = bone >= 0 && group[bone] >= 0 && head[group[bone]];
						if (isHead) onHead += w;
						if (w > strongest) { strongest = w; dominant = bone; }
						if (!isHead && w > strongestBody) { strongestBody = w; body = bone; }
					}
					if (dominant < 0 || group[dominant] < 0) continue;
					int g = group[dominant];
					// A cabeça leva só a pele que é quase toda dela: a emenda com o
					// pescoço (no lobo, a juba que desce pelas costas) vai para o osso
					// do corpo que mais pesa nela. Sem isso, o tiro nas costas do lobo,
					// perto do pescoço, era headshot (25/11).
					if (head[g] && onHead < HeadWeight)
					{
						if (body < 0 || group[body] < 0) continue;
						g = group[body];
					}
					if (!inverse.TryGetValue(g, out Transform3D toBone))
						inverse[g] = toBone = (at * skeleton.GetBoneGlobalPose(g)).AffineInverse();
					if (!points.TryGetValue(g, out var list)) points[g] = list = new List<System.Numerics.Vector3>();
					list.Add(N(toBone * p));
				}
			}
		}

		// Peça com pontos de menos vai para o osso pai (no espaço dele).
		foreach (int g in points.Keys.OrderByDescending(b => Depth(skeleton, b)).ToList())
		{
			if (!points.TryGetValue(g, out var list) || list.Count >= MinPoints) continue;
			int parent = skeleton.GetBoneParent(g);
			while (parent >= 0 && group[parent] < 0) parent = skeleton.GetBoneParent(parent);
			points.Remove(g);
			if (parent < 0) continue;
			parent = group[parent];
			Transform3D from = at * skeleton.GetBoneGlobalPose(g);
			if (!inverse.TryGetValue(parent, out Transform3D toParent))
				inverse[parent] = toParent = (at * skeleton.GetBoneGlobalPose(parent)).AffineInverse();
			if (!points.TryGetValue(parent, out var target)) points[parent] = target = new List<System.Numerics.Vector3>();
			foreach (var q in list) target.Add(N(toParent * (from * G(q))));
		}

		var parts = new List<Fitted>();
		foreach (var (bone, list) in points)
		{
			if (list.Count < MinPoints) continue;
			string name = skeleton.GetBoneName(bone);
			bool isHead = head[bone];
			foreach (Shape piece in BodyHit.FitPieces(list, isHead ? BodyHit.HeadContains : BodyHit.Contains, !isHead))
				parts.Add(new Fitted(bone, piece, isHead, name));
		}
		return parts.ToArray();
	}

	private static int Depth(Skeleton3D skeleton, int bone)
	{
		int d = 0;
		for (int b = skeleton.GetBoneParent(bone); b >= 0; b = skeleton.GetBoneParent(b)) d++;
		return d;
	}

	private static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);
	private static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
