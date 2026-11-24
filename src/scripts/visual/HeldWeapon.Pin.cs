using Godot;
using System.Collections.Generic;

/// <summary>
/// O pino da granada visto de fora (Jhon, 23/11: "o gatilho da granada deve cair no chão também").
/// No armar a mão esquerda puxa o pino com o indicador na argola (o Blender põe a ponta do dedo
/// onde a de lá está: <c>Copia.lance</c>); quando ele sai (<see cref="HoldClip.PinOut"/>), o pino e
/// a argola vão presos na mão esquerda do corpo — na primeira pessoa eles seguem a mão de lá, que
/// some embaixo da câmera, e de fora ficavam soltos no ar —; quando ela o larga
/// (<see cref="HoldClip.PinDrop"/>), somem da granada e uma cópia cai no chão pela física, como o
/// pente da recarga (<see cref="DropMagazine"/>). Voltam na próxima granada.
///
/// O pino e a argola não são malhas à parte: a granada é uma malha só, o corpo no osso Bolt, o
/// pino no Trigger e a argola no Magazine. A cópia que cai é montada com os triângulos desses dois
/// ossos, na pose do instante em que ela os larga; na granada eles somem encolhidos a nada.
/// </summary>
public partial class HeldWeapon
{
	private HeldRig _pinOf;
	private MeshInstance3D _pinMesh;
	private int _pinBone = -1, _ringBone = -1;
	// 0: na granada (a animação manda); 1: na mão esquerda do corpo; 2: largado (escondido)
	private int _pinState;
	private Transform3D _pinInHand, _ringInHand;
	private Vector3 _pinScale = Vector3.One, _ringScale = Vector3.One;
	private Vector3 _pinBefore, _pinVelocity;
	private bool _pinTracked;

	/// <summary>Quanto tempo o pino que caiu fica no chão (s).</summary>
	[Export] public float DroppedPinLife = 8f;

	private void PullPin(HoldClip clip, double at)
	{
		if (_pinOf != _rig)
		{
			ShowPin();
			_pinOf = _rig;
			_pinMesh = null;
			_pinBone = _ringBone = -1;
			_pinState = 0;
			if (_rig != null && Arsenal.IsGrenade(_id))
			{
				foreach (Node node in _rig.FindChildren("*", "MeshInstance3D", true, false))
					if (node.Name.ToString().ToLowerInvariant().Contains("granada")) _pinMesh = (MeshInstance3D)node;
				_pinBone = _rig.Skeleton.FindBone("Trigger");
				_ringBone = _rig.Skeleton.FindBone("Magazine");
			}
		}
		if (_pinMesh == null || _pinBone < 0 || _ringBone < 0 || _leftHand < 0) return;

		bool pulling = clip != null && clip.PullsPin && _rig.Playing == clip.Name;
		if (!pulling || at < clip.PinOut)
		{
			// fora do armar (ou antes de o pino sair): a animação manda, e o pino está na granada
			ShowPin();
			return;
		}
		Skeleton3D rig = _rig.Skeleton;
		Transform3D hand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_leftHand);
		if (_pinState == 0)
		{
			// saiu agora: daqui em diante vai preso na mão, onde estava em relação a ela
			_pinScale = rig.GetBonePoseScale(_pinBone);
			_ringScale = rig.GetBonePoseScale(_ringBone);
			_pinInHand = hand.AffineInverse() * Global(rig, _pinBone);
			_ringInHand = hand.AffineInverse() * Global(rig, _ringBone);
			_pinState = 1;
			_pinTracked = false;
		}
		if (_pinState == 1)
		{
			Transform3D pin = hand * _pinInHand;
			Transform3D ring = hand * _ringInHand;
			float dt = (float)GetProcessDeltaTime();
			if (_pinTracked && dt > 1e-4f) _pinVelocity = (pin.Origin - _pinBefore) / dt;
			_pinBefore = pin.Origin;
			_pinTracked = true;
			if (at < clip.PinDrop)
			{
				Place(rig, _pinBone, pin);
				Place(rig, _ringBone, ring);
				return;
			}
			Place(rig, _pinBone, pin);
			Place(rig, _ringBone, ring);
			DropPin();
			_pinState = 2;
		}
		// largado: some da granada até a próxima
		rig.SetBonePoseScale(_pinBone, Vector3.One * 1e-4f);
		rig.SetBonePoseScale(_ringBone, Vector3.One * 1e-4f);
	}

	/// <summary>O pino de volta na granada (a escala dele: a animação repõe a posição e a rotação,
	/// mas não a escala).</summary>
	private void ShowPin()
	{
		if (_pinState == 2 && _pinOf != null && IsInstanceValid(_pinOf) && _pinBone >= 0 && _ringBone >= 0)
		{
			_pinOf.Skeleton.SetBonePoseScale(_pinBone, _pinScale);
			_pinOf.Skeleton.SetBonePoseScale(_ringBone, _ringScale);
		}
		_pinState = 0;
	}

	private static Transform3D Global(Skeleton3D skeleton, int bone) =>
		skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone);

	/// <summary>Põe o osso no lugar dado (no mundo).</summary>
	private static void Place(Skeleton3D skeleton, int bone, Transform3D target)
	{
		int parent = skeleton.GetBoneParent(bone);
		Transform3D parentGlobal = skeleton.GlobalTransform * (parent >= 0 ? skeleton.GetBoneGlobalPose(parent) : Transform3D.Identity);
		Transform3D local = parentGlobal.AffineInverse() * target;
		skeleton.SetBonePosePosition(bone, local.Origin);
		skeleton.SetBonePoseRotation(bone, local.Basis.Orthonormalized().GetRotationQuaternion());
		skeleton.SetBonePoseScale(bone, local.Basis.Scale);
	}

	/// <summary>Uma cópia do pino e da argola cai de onde a mão os largou: os triângulos dos dois
	/// ossos, na pose de agora, num corpo físico que bate no cenário e some depois de
	/// <see cref="DroppedPinLife"/>.</summary>
	private void DropPin()
	{
		Mesh source = _pinMesh.Mesh;
		Skin skin = _pinMesh.Skin;
		if (source == null || skin == null) return;
		Skeleton3D rig = _rig.Skeleton;
		// de cada ligação da pele, o osso e a pose de ligação; só as do pino e da argola entram
		var place = new Dictionary<int, Transform3D>();
		for (int i = 0; i < skin.GetBindCount(); i++)
		{
			string name = skin.GetBindName(i);
			int bone = name != "" ? rig.FindBone(name) : skin.GetBindBone(i);
			if (bone == _pinBone || bone == _ringBone)
				place[i] = Global(rig, bone) * skin.GetBindPose(i);
		}
		if (place.Count == 0) return;

		var points = new List<Vector3>();
		var normals = new List<Vector3>();
		var uvs = new List<Vector2>();
		for (int s = 0; s < source.GetSurfaceCount(); s++)
		{
			Godot.Collections.Array arrays = source.SurfaceGetArrays(s);
			Vector3[] vertex = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
			Vector3[] normal = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
			Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
			int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
			int[] index = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
			if (vertex.Length == 0 || bones.Length < vertex.Length) continue;
			int per = bones.Length / vertex.Length;
			int count = index.Length > 0 ? index.Length : vertex.Length;
			for (int t = 0; t + 2 < count; t += 3)
			{
				int a = index.Length > 0 ? index[t] : t, b = index.Length > 0 ? index[t + 1] : t + 1, c = index.Length > 0 ? index[t + 2] : t + 2;
				if (!place.ContainsKey(bones[a * per]) || !place.ContainsKey(bones[b * per]) || !place.ContainsKey(bones[c * per])) continue;
				foreach (int v in new[] { a, b, c })
				{
					Transform3D at = place[bones[v * per]];
					points.Add(at * vertex[v]);
					normals.Add(normal.Length > v ? (at.Basis * normal[v]).Normalized() : Vector3.Up);
					uvs.Add(uv.Length > v ? uv[v] : Vector2.Zero);
				}
			}
		}
		if (points.Count < 3) return;

		// o corpo no meio da peça, girado como o pino; a malha em volta dele, sem escala
		Vector3 center = Vector3.Zero;
		foreach (Vector3 p in points) center += p;
		center /= points.Count;
		Basis turn = Global(rig, _pinBone).Basis.Orthonormalized();
		Basis back = turn.Inverse();
		var local = new Vector3[points.Count];
		var localNormal = new Vector3[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			local[i] = back * (points[i] - center);
			localNormal[i] = (back * normals[i]).Normalized();
		}
		var surface = new Godot.Collections.Array();
		surface.Resize((int)Mesh.ArrayType.Max);
		surface[(int)Mesh.ArrayType.Vertex] = local;
		surface[(int)Mesh.ArrayType.Normal] = localNormal;
		surface[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
		var loose = new ArrayMesh();
		loose.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surface);

		var body = new RigidBody3D
		{
			CollisionLayer = 0,
			CollisionMask = 1,
			Mass = 0.05f,
			ContinuousCd = true,
			PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.3f },
		};
		var mesh = new MeshInstance3D { Mesh = loose, Layers = _pinMesh.Layers };
		mesh.SetSurfaceOverrideMaterial(0, _pinMesh.GetActiveMaterial(0));
		body.AddChild(mesh);
		Shape3D hull = loose.CreateConvexShape(true, true);
		body.AddChild(new CollisionShape3D { Shape = hull ?? new SphereShape3D { Radius = 0.02f } });
		Node world = GetTree().CurrentScene ?? GetTree().Root;
		world.AddChild(body);
		body.GlobalTransform = new Transform3D(turn, center);
		// largado da mão: leva um pouco da velocidade dela, como o pente
		Vector3 thrown = (_pinVelocity - _body.Velocity) * MagazineThrow;
		body.LinearVelocity = _body.Velocity + thrown.LimitLength(MagazineMaxSpeed);
		GetTree().CreateTimer(DroppedPinLife).Timeout += () =>
		{
			if (IsInstanceValid(body)) body.QueueFree();
		};
	}
}
