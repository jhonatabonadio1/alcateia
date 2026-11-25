using Godot;
using System.Collections.Generic;

/// <summary>
/// A pose da vitrine: de pé, posturado, braços soltos ao lado do corpo com
/// os cotovelos um pouco abertos, punhos fechados, pés afastados (19/11,
/// Jhon: "na vitrine, eles podem ficar numa pose parada, pose heroica,
/// posturados — sem ser em posição de ataque").
///
/// Não vem de clipe: cada osso recebe a direção que deve ter no espaço do
/// modelo (de frente para +Z, esquerda em +X, como os do Mixamo), e o giro
/// sai do arco mais curto entre a direção de repouso e essa. Serve para
/// qualquer corpo com os nomes de osso do Mixamo e repouso em T, seja qual
/// for o eixo de cada osso.
/// </summary>
public static class HeroPose
{
	/// <summary>Para onde cada osso aponta (até o filho principal dele).</summary>
	private static readonly (string Bone, string Child, Vector3 Direction)[] Left =
	{
		("Shoulder", "Arm", new Vector3(1f, -0.06f, -0.10f)),
		("Arm", "ForeArm", new Vector3(0.34f, -1f, -0.10f)),
		("ForeArm", "Hand", new Vector3(0.16f, -1f, 0.20f)),
		("Hand", "HandMiddle1", new Vector3(0.06f, -1f, 0.26f)),
		("UpLeg", "Leg", new Vector3(0.11f, -1f, 0.02f)),
		("Leg", "Foot", new Vector3(0.05f, -1f, -0.03f))
	};

	private static readonly string[] Fingers = { "Index", "Middle", "Ring", "Pinky" };

	/// <summary>Um osso de dedo, com o nome do Mixamo ("mixamorig_RightHandIndex1") ou o do Manny ("index_01_r").</summary>
	private static int FingerBone(Skeleton3D body, string side, string finger, int k)
	{
		int bone = body.FindBone($"mixamorig_{side}Hand{finger}{k}");
		return bone >= 0 ? bone : body.FindBone($"{finger.ToLowerInvariant()}_0{k}_{(side == "Right" ? 'r' : 'l')}");
	}

	/// <summary>Quanto cada falange dobra para a palma, somado, em graus.</summary>
	private static readonly float[] Curl = { 55f, 120f, 170f };

	/// <summary>A animação de um quadro só, em laço, com as trilhas em
	/// <paramref name="bodyPath"/> (o esqueleto a partir da raiz do tocador).</summary>
	public static Animation Make(Skeleton3D body, Node bodyRoot, string bodyPath)
	{
		var animation = new Animation { Length = 1f, LoopMode = Animation.LoopModeEnum.Linear };
		if (body == null) return animation;

		Quaternion frame = AnimationKit.ChainTo(body, bodyRoot).Basis.Orthonormalized().GetRotationQuaternion();
		int count = body.GetBoneCount();

		// Repouso de cada osso no espaço do modelo.
		var rest = new Quaternion[count];
		var origin = new Vector3[count];
		for (int i = 0; i < count; i++)
		{
			Transform3D global = body.GetBoneGlobalRest(i);
			rest[i] = (frame * global.Basis.Orthonormalized().GetRotationQuaternion()).Normalized();
			origin[i] = frame * global.Origin;
		}

		// A direção pedida, por osso.
		var wanted = new Dictionary<int, (int Child, Vector3 Direction)>();
		foreach (var (bone, child, direction) in Left)
		{
			Add(body, wanted, $"mixamorig_Left{bone}", $"mixamorig_Left{child}", direction);
			Add(body, wanted, $"mixamorig_Right{bone}", $"mixamorig_Right{child}", new Vector3(-direction.X, direction.Y, direction.Z));
		}
		// Os pés ficam como no repouso, mesmo com a perna aberta.
		foreach (string side in new[] { "Left", "Right" })
		{
			int foot = body.FindBone($"mixamorig_{side}Foot"), toe = body.FindBone($"mixamorig_{side}ToeBase");
			if (foot >= 0 && toe >= 0) wanted[foot] = (toe, (origin[toe] - origin[foot]).Normalized());
		}

		var posed = new Quaternion[count];
		var done = new bool[count];
		Quaternion Global(int bone)
		{
			if (done[bone]) return posed[bone];
			done[bone] = true;
			int parent = body.GetBoneParent(bone);
			Quaternion inherited = parent < 0 ? rest[bone]
				: (Global(parent) * rest[parent].Inverse() * rest[bone]).Normalized();
			if (wanted.TryGetValue(bone, out var want))
			{
				// O osso já veio girado pelo pai; do jeito que ficou, até a direção pedida.
				Vector3 now = (inherited * rest[bone].Inverse()) * (origin[want.Child] - origin[bone]).Normalized();
				inherited = (Arc(now, want.Direction.Normalized()) * inherited).Normalized();
			}
			return posed[bone] = inherited;
		}

		// Punhos: cada falange dobra para a palma (em T, a palma olha para baixo).
		foreach (string side in new[] { "Left", "Right" })
		{
			int hand = body.FindBone($"mixamorig_{side}Hand"), middle = FingerBone(body, side, "Middle", 1);
			if (hand < 0 || middle < 0) continue;
			Quaternion turn = (Global(hand) * rest[hand].Inverse()).Normalized();
			Vector3 along = turn * (origin[middle] - origin[hand]).Normalized();
			Vector3 palm = turn * Vector3.Down;
			foreach (string finger in Fingers)
			{
				for (int k = 1; k <= 3; k++)
				{
					int a = FingerBone(body, side, finger, k), b = FingerBone(body, side, finger, k + 1);
					if (a < 0 || b < 0) continue;
					float angle = Mathf.DegToRad(Curl[k - 1]);
					wanted[a] = (b, along * Mathf.Cos(angle) + palm * Mathf.Sin(angle));
				}
			}
			// O polegar fecha por cima dos dedos.
			for (int k = 1; k <= 3; k++)
			{
				int a = FingerBone(body, side, "Thumb", k), b = FingerBone(body, side, "Thumb", k + 1);
				if (a < 0 || b < 0) continue;
				Vector3 rested = turn * (origin[b] - origin[a]).Normalized();
				wanted[a] = (b, (rested + palm * (0.35f * k) + along * 0.2f).Normalized());
			}
		}

		for (int bone = 0; bone < count; bone++)
		{
			int parent = body.GetBoneParent(bone);
			Quaternion local = parent < 0 ? frame.Inverse() * Global(bone) : Global(parent).Inverse() * Global(bone);
			int track = animation.AddTrack(Animation.TrackType.Rotation3D);
			animation.TrackSetPath(track, $"{bodyPath}:{body.GetBoneName(bone)}");
			animation.RotationTrackInsertKey(track, 0, local.Normalized());
		}

		return animation;
	}

	private static void Add(Skeleton3D body, Dictionary<int, (int, Vector3)> wanted, string bone, string child, Vector3 direction)
	{
		int a = body.FindBone(bone), b = body.FindBone(child);
		if (a >= 0 && b >= 0) wanted[a] = (b, direction);
	}

	private static Quaternion Arc(Vector3 from, Vector3 to) => AnimationKit.Arc(from, to);
}
