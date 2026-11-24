using Godot;
using System.Collections.Generic;

/// <summary>
/// Peças do conjunto animado mexidas por código, por cima da animação: o
/// carregador que cai e volta e o ferrolho puxado na recarga de código do
/// AR-15 (o pacote dele não tem animação de recarga).
///
/// É um modificador do esqueleto: roda depois de a animação pôr a pose,
/// então soma ao que ela pôs em vez de brigar com ela. Os deslocamentos
/// são em metros, no espaço do osso pai; quem pede não precisa saber que
/// os ossos do pacote estão em centímetros.
/// </summary>
public partial class RigPieces : SkeletonModifier3D
{
	private readonly Dictionary<int, Vector3> _offsets = new();
	private float _unitsPerMeter = 1f;

	/// <summary>Põe o modificador no esqueleto do conjunto. <paramref name="rigRoot"/>
	/// é a cena do conjunto, para saber em que unidade os ossos estão.</summary>
	public static RigPieces Attach(Skeleton3D skeleton, Node3D rigRoot)
	{
		var pieces = new RigPieces { Name = "PecasDeCodigo" };
		skeleton.AddChild(pieces);
		float scale = Relative(rigRoot, skeleton).Basis.Scale.X;
		pieces._unitsPerMeter = scale > 1e-6f ? 1f / scale : 1f;
		return pieces;
	}

	/// <summary>Desloca o osso <paramref name="bone"/> (metros, espaço do pai); zero tira.</summary>
	public void Offset(int bone, Vector3 meters)
	{
		if (bone < 0) return;
		if (meters.IsZeroApprox()) _offsets.Remove(bone);
		else _offsets[bone] = meters * _unitsPerMeter;
	}

	public override void _ProcessModificationWithDelta(double delta)
	{
		Skeleton3D skeleton = GetSkeleton();
		if (skeleton == null) return;
		foreach (var (bone, offset) in _offsets)
			skeleton.SetBonePosePosition(bone, skeleton.GetBonePosePosition(bone) + offset);
	}

	/// <summary>A transformação de <paramref name="node"/> no espaço de
	/// <paramref name="root"/>, sem precisar estar na árvore.</summary>
	public static Transform3D Relative(Node3D root, Node3D node)
	{
		Transform3D t = Transform3D.Identity;
		for (Node n = node; n != null && n != root; n = n.GetParent())
			if (n is Node3D n3) t = n3.Transform * t;
		return t;
	}
}
