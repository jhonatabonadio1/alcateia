using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

/// <summary>
/// A cabeça de quem leva tiro, no modelo que se vê: a esfera de
/// <see cref="HeadHit"/> entre os ossos <c>mixamorig_Head</c> e
/// <c>mixamorig_HeadTop_End</c> do esqueleto visível (o humano em
/// <c>Human</c> ou o lobo em <c>WolfVisual</c>). Segue a animação: o lobo
/// curvado, o humano mirando, agachado.
///
/// Sem modelo (o servidor tira tudo que não colide; o boneco do treino é
/// uma cápsula), não há esfera, e a arma volta para a faixa de cima da
/// cápsula (<see cref="Weapon.HeadZone"/>).
/// </summary>
public static class HeadHitbox
{
	private const string HeadBone = "mixamorig_Head";
	private const string TopBone = "mixamorig_HeadTop_End";

	private sealed record Bones(Skeleton3D Skeleton, int Head, int Top);

	private sealed class Entry
	{
		public Bones[] Sets = System.Array.Empty<Bones>();
		public ulong BuiltAt;
	}

	/// <summary>Os esqueletos de cada corpo, achados uma vez. Some com o
	/// corpo.</summary>
	private static readonly ConditionalWeakTable<Node3D, Entry> Cache = new();

	/// <summary>A cabeça de <paramref name="body"/> agora. Falso: ele não tem
	/// modelo com cabeça à vista.</summary>
	public static bool TryGet(Node3D body, out Vector3 center, out float radius)
	{
		center = Vector3.Zero;
		radius = 0f;

		Entry entry = Cache.GetValue(body, _ => new Entry());
		if (Find(entry, out Bones bones) || (Stale(entry) && Rebuild(entry, body) && Find(entry, out bones)))
		{
			Skeleton3D skeleton = bones.Skeleton;
			Transform3D at = skeleton.GlobalTransform;
			var (c, r) = HeadHit.Sphere(
				ToNumerics(at * skeleton.GetBoneGlobalPose(bones.Head).Origin),
				ToNumerics(at * skeleton.GetBoneGlobalPose(bones.Top).Origin));
			center = new Vector3(c.X, c.Y, c.Z);
			radius = r;
			return true;
		}

		return false;
	}

	/// <summary>O corpo tem cabeça de modelo à vista agora. Quem tem não
	/// leva tiro na cabeça pela faixa da cápsula, só pela esfera.</summary>
	public static bool Has(Node3D body) => TryGet(body, out _, out _);

	private static bool Find(Entry entry, out Bones found)
	{
		foreach (Bones set in entry.Sets)
		{
			if (!GodotObject.IsInstanceValid(set.Skeleton) || !set.Skeleton.IsVisibleInTree()) continue;
			found = set;
			return true;
		}

		found = null;
		return false;
	}

	/// <summary>Nada à vista: o modelo pode ter nascido ou trocado depois
	/// da última busca (o lobo monta o dele no _Ready, e de novo ao trocar
	/// de time). Procura de novo, no máximo uma vez por segundo.</summary>
	private static bool Stale(Entry entry) =>
		entry.BuiltAt == 0 || Time.GetTicksMsec() - entry.BuiltAt >= 1000;

	private static bool Rebuild(Entry entry, Node3D body)
	{
		var sets = new List<Bones>();
		foreach (string root in new[] { "Human", "WolfVisual" })
		{
			if (body.GetNodeOrNull(root) is not Node node) continue;
			foreach (Skeleton3D skeleton in node.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>())
			{
				int head = skeleton.FindBone(HeadBone), top = skeleton.FindBone(TopBone);
				if (head >= 0 && top >= 0) sets.Add(new Bones(skeleton, head, top));
			}
		}

		entry.Sets = sets.ToArray();
		entry.BuiltAt = System.Math.Max(Time.GetTicksMsec(), 1);
		return entry.Sets.Length > 0;
	}

	private static System.Numerics.Vector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);
}
