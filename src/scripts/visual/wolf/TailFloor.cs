using Godot;

/// <summary>
/// O chão do rabo do lobo morto (Jhon, 27/11: "o lobo, ao morrer, flutua";
/// "parece que o lobo encosta, porém o rabo dele o levanta").
///
/// O rabo é uma mola com gravidade (<see cref="SpringBoneSimulator3D"/>,
/// WolfVisual.Tail) e, com o lobo deitado, pende do quadril para dentro do
/// chão: 44 cm abaixo dele, medido. Este ajuste roda **depois** da mola, no
/// mesmo esqueleto: junta por junta, da raiz à ponta, a que ficaria abaixo
/// do chão é dobrada para cima até pousar nele, sem mudar o comprimento do
/// osso — o rabo fica deitado no chão.
///
/// Até 27/11 quem devia segurar o rabo era um plano de colisão da própria
/// mola (<c>SpringBoneCollisionPlane3D</c>). Ele não segurava: posto no chão,
/// meio metro acima ou quatro metros acima, o rabo pendia igual (medido). O
/// rabo só não aparecia dentro do chão porque o corpo inteiro subia para
/// tirá-lo de lá (<see cref="DeadBody"/>), e era isso o lobo flutuando.
///
/// Só existe com o lobo morto (WolfVisual.Corpse o põe e tira).
/// </summary>
public partial class TailFloor : SkeletonModifier3D
{
	/// <summary>A altura do chão, no mundo.</summary>
	public float FloorY { get; set; }

	/// <summary>Há chão medido debaixo do rabo (sem ele, não mexe em nada).</summary>
	public bool HasFloor { get; set; }

	/// <summary>
	/// O chão inclinado: a normal dele e um ponto dele, no mundo. Com a normal
	/// para cima (o padrão) o chão é plano, em <see cref="FloorY"/>. O corpo
	/// deitado numa escada ou num declive se inclina até o plano do chão
	/// debaixo dele (<see cref="DeadBody.GroundNormal"/>), e o rabo deita no
	/// mesmo plano: com o chão do rabo plano, na altura da raiz, ele ficava no ar
	/// do lado de baixo da escada e dentro dela do lado de cima.
	/// </summary>
	public Vector3 Normal { get; set; } = Vector3.Up;
	public Vector3 Point { get; set; }

	/// <summary>A altura do chão debaixo de <paramref name="at"/>, no mundo.</summary>
	private float FloorAt(Vector3 at) => Normal.Y > 0.999f ? FloorY
		: Point.Y - (Normal.X * (at.X - Point.X) + Normal.Z * (at.Z - Point.Z)) / Mathf.Max(Normal.Y, 0.3f);

	/// <summary>Quanto a junta fica acima do chão, em metros: a grossura do
	/// rabo com o pelo (com 4,5 cm o pelo entrava 6 cm no chão, medido).</summary>
	[Export] public float Margin = 0.115f;

	/// <summary>Os ossos, da raiz à ponta.</summary>
	public string[] Chain { get; set; } = System.Array.Empty<string>();

	public override void _ProcessModificationWithDelta(double delta)
	{
		Skeleton3D skeleton = GetSkeleton();
		if (skeleton == null || !HasFloor || Chain.Length < 2) return;

		Transform3D world = skeleton.GlobalTransform;
		Quaternion toWorld = world.Basis.GetRotationQuaternion();

		for (int i = 0; i + 1 < Chain.Length; i++)
		{
			int bone = skeleton.FindBone(Chain[i]), child = skeleton.FindBone(Chain[i + 1]);
			if (bone < 0 || child < 0) return;

			Transform3D pose = skeleton.GetBoneGlobalPose(bone);
			Vector3 joint = world * pose.Origin;
			Vector3 tip = world * skeleton.GetBoneGlobalPose(child).Origin;
			float height = FloorAt(tip) + Margin;
			if (tip.Y >= height) continue;

			// A ponta deste osso sobe até o chão, girando em volta da junta: o
			// que ela sobe, perde de alcance para o lado.
			Vector3 reach = tip - joint;
			float length = reach.Length();
			if (length < 1e-5f) continue;
			float rise = Mathf.Clamp(height - joint.Y, -length, length);
			Vector3 side = new(reach.X, 0f, reach.Z);
			// Pendurado a prumo não tem "lado": deita para trás do quadril.
			if (side.LengthSquared() < 1e-8f) side = world.Basis.Z;
			side = side.Normalized() * Mathf.Sqrt(Mathf.Max(length * length - rise * rise, 0f));
			Vector3 wanted = side + Vector3.Up * rise;

			var turn = new Quaternion(reach / length, wanted.Normalized());
			// O giro é no mundo; a pose do osso é no espaço do esqueleto.
			Quaternion local = toWorld.Inverse() * turn * toWorld;
			pose.Basis = new Basis(local) * pose.Basis;
			skeleton.SetBoneGlobalPose(bone, pose);
		}
	}
}
