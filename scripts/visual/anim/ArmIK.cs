using Godot;

/// <summary>
/// Um braço de dois ossos (braço e antebraço) levando o pulso a um ponto,
/// com o cotovelo para o lado pedido, e a mão girada como se quer. É o que
/// põe as mãos do corpo em terceira pessoa na arma, nos mesmos pontos da
/// primeira pessoa (<see cref="HeldWeapon"/>, 21/11).
///
/// Conta no mundo e escreve a rotação de cada osso em relação ao pai: o
/// tamanho dos ossos e a escala do modelo não entram. Os ossos são escritos
/// por <paramref name="write"/> (quem chama guarda o que mudou, para
/// desfazer no quadro seguinte se a animação não reescrever).
/// </summary>
public static class ArmIK
{
	/// <summary>
	/// Leva o pulso de <paramref name="hand"/> até <paramref name="target"/>
	/// (no mundo). Longe demais, o braço estica na direção dele; perto
	/// demais, dobra até onde dá. Devolve onde o pulso ficou.
	/// </summary>
	/// <param name="handRotation">A rotação da mão no mundo; nula, a mão
	/// fica como estava em relação ao antebraço.</param>
	/// <param name="pole">Um ponto para onde o cotovelo aponta.</param>
	/// <param name="twist">Quanto do giro da mão em torno do antebraço vai
	/// para o antebraço (0 a 1). Sem osso de torção no corpo, a mão girada
	/// sozinha torce o pulso como um papel de bala; repartido, o antebraço
	/// gira junto e o pulso fica reto.</param>
	public static Vector3 Reach(Skeleton3D skeleton, int upper, int lower, int hand, Vector3 target,
		Basis? handRotation, Vector3 pole, System.Action<int, Quaternion> write, float twist = 0f)
	{
		Transform3D frame = skeleton.GlobalTransform;
		Transform3D upperPose = frame * skeleton.GetBoneGlobalPose(upper);
		Transform3D lowerPose = frame * skeleton.GetBoneGlobalPose(lower);
		Transform3D handPose = frame * skeleton.GetBoneGlobalPose(hand);
		int parent = skeleton.GetBoneParent(upper);
		Basis parentRotation = (parent >= 0 ? frame * skeleton.GetBoneGlobalPose(parent) : frame).Basis.Orthonormalized();

		Vector3 a = upperPose.Origin, b = lowerPose.Origin, c = handPose.Origin;
		float upperLength = a.DistanceTo(b), lowerLength = b.DistanceTo(c);
		Vector3 toTarget = target - a;
		float distance = toTarget.Length();
		if (upperLength < 1e-4f || lowerLength < 1e-4f || distance < 1e-4f) return c;

		Vector3 direction = toTarget / distance;
		float reach = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 1e-3f, upperLength + lowerLength - 1e-3f);

		// O triângulo braço–antebraço–distância: o ângulo no ombro.
		float cos = Mathf.Clamp((upperLength * upperLength + reach * reach - lowerLength * lowerLength)
			/ (2f * upperLength * reach), -1f, 1f);
		float sin = Mathf.Sqrt(Mathf.Max(1f - cos * cos, 0f));

		// Para onde o cotovelo dobra: o polo, tirado o que está na linha do
		// braço; sem polo útil, para onde ele já dobrava.
		Vector3 bend = Across(pole - a, direction);
		if (bend.LengthSquared() < 1e-8f) bend = Across(b - a, direction);
		if (bend.LengthSquared() < 1e-8f) bend = Across(Vector3.Down, direction);
		if (bend.LengthSquared() < 1e-8f) bend = Across(Vector3.Forward, direction);
		bend = bend.Normalized();

		Vector3 elbow = a + direction * (upperLength * cos) + bend * (upperLength * sin);
		Vector3 wrist = a + direction * reach;

		// O braço gira no ombro até o cotovelo; o antebraço, no cotovelo até o pulso.
		Quaternion turnUpper = Arc(b - a, elbow - a);
		Vector3 handAfter = a + turnUpper * (c - a);
		Quaternion turnLower = Arc(handAfter - elbow, wrist - elbow);

		Basis upperRotation = new Basis(turnUpper) * upperPose.Basis.Orthonormalized();
		Basis lowerRotation = new Basis(turnLower * turnUpper) * lowerPose.Basis.Orthonormalized();
		Basis handRotationWorld = handRotation ?? new Basis(turnLower * turnUpper) * handPose.Basis.Orthonormalized();

		// O giro da mão em relação ao antebraço, em torno do eixo dele: uma
		// parte passa para o antebraço (o pulso não fica torcido).
		if (handRotation != null && twist > 0f)
		{
			Vector3 axis = (wrist - elbow).Normalized();
			Quaternion delta = (handRotationWorld.Orthonormalized() * lowerRotation.Inverse()).Orthonormalized().GetRotationQuaternion();
			if (delta.W < 0f) delta = -delta;
			Vector3 v = new Vector3(delta.X, delta.Y, delta.Z);
			Vector3 along = axis * v.Dot(axis);
			var spin = new Quaternion(along.X, along.Y, along.Z, delta.W);
			if (spin.LengthSquared() > 1e-8f)
			{
				spin = spin.Normalized();
				lowerRotation = new Basis(Quaternion.Identity.Slerp(spin, Mathf.Clamp(twist, 0f, 1f))) * lowerRotation;
			}
		}

		write(upper, (parentRotation.Transposed() * upperRotation).Orthonormalized().GetRotationQuaternion());
		write(lower, (upperRotation.Transposed() * lowerRotation).Orthonormalized().GetRotationQuaternion());
		write(hand, (lowerRotation.Transposed() * handRotationWorld.Orthonormalized()).Orthonormalized().GetRotationQuaternion());
		return wrist;
	}

	/// <summary>A parte de <paramref name="v"/> que não está na linha de <paramref name="axis"/>.</summary>
	private static Vector3 Across(Vector3 v, Vector3 axis) => v - axis * v.Dot(axis);

	/// <summary>O menor giro que leva a direção <paramref name="from"/> a <paramref name="to"/>.</summary>
	public static Quaternion Arc(Vector3 from, Vector3 to)
	{
		if (from.LengthSquared() < 1e-10f || to.LengthSquared() < 1e-10f) return Quaternion.Identity;
		Vector3 f = from.Normalized(), t = to.Normalized();
		float dot = Mathf.Clamp(f.Dot(t), -1f, 1f);
		if (dot > 0.99999f) return Quaternion.Identity;
		if (dot < -0.99999f)
		{
			Vector3 any = f.Cross(Vector3.Up);
			if (any.LengthSquared() < 1e-6f) any = f.Cross(Vector3.Right);
			return new Quaternion(any.Normalized(), Mathf.Pi);
		}
		return new Quaternion(f.Cross(t).Normalized(), Mathf.Acos(dot));
	}

	/// <summary>Base ortonormal com X em <paramref name="x"/> e Y o mais perto
	/// possível de <paramref name="y"/>.</summary>
	public static Basis Orient(Vector3 x, Vector3 y)
	{
		x = x.Normalized();
		Vector3 z = x.Cross(y).Normalized();
		return new Basis(x, z.Cross(x), z);
	}
}
