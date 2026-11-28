using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// As patas do lobo no chão, a cada quadro, depois da animação (alpha
/// fechado, 26/11: "os pés do lobo atravessam o chão, precisa corrigir para
/// ele tocar o chão perfeitamente").
///
/// Para cada pata (os dois pés e as duas mãos, que viram patas de quatro e
/// engatinhando): a sola como ela está na tela — pontos da pele de baixo do
/// pé (e das garras) e da mão, guardados no espaço do osso — e o chão de
/// verdade embaixo dela (um raio na colisão do mapa). Dentro do chão, a pata
/// sobe até encostar; um pouco acima dele (o pé de apoio flutuando numa rampa,
/// num degrau ou pela diferença de proporção dos clipes), desce até encostar
/// (<see cref="FootPlant.Lift"/>). Quem move é um IK de dois ossos (coxa e
/// canela, braço e antebraço; <see cref="FootPlant.Solve"/>), que dobra o
/// joelho ou o cotovelo mantendo o plano da dobra; a sola mantém o ângulo da
/// animação.
///
/// É só o desenho: não mexe na cápsula, na física nem no que vai pela rede.
/// As peças de acerto (<see cref="BodyHitbox"/>) leem os ossos depois disto,
/// então seguem a pata onde ela aparece.
/// </summary>
public partial class WolfFootPlant : SkeletonModifier3D
{
	/// <summary>Até quantos metros acima do chão a pata de apoio desce para
	/// encostar: o pé de trás numa rampa, o de baixo num degrau, o agachado que
	/// a proporção do clipe deixa no ar. A pata que dá o passo não é puxada (ela
	/// não está parada: <see cref="StillSpeed"/>).</summary>
	[Export] public float ContactBand = 0.15f;

	/// <summary>Até que velocidade vertical da sola, em relação ao corpo (m/s), a
	/// pata conta como apoiando: a de apoio fica parada na vertical (o passo a
	/// leva para trás); a que dá o passo sobe e desce bem mais rápido.</summary>
	[Export] public float StillSpeed = 0.3f;

	/// <summary>Quanto tempo o apoio leva para entrar e sair, em segundos (sem
	/// isto, a pata pulava no instante em que parava ou saía).</summary>
	[Export] public float ContactTime = 0.06f;

	/// <summary>O maior ajuste, em metros, para cima ou para baixo: uma pata
	/// sobre a quina de um degrau alto não vira contorcionismo.</summary>
	[Export] public float MaxLift = 0.3f;

	/// <summary>Quanto acima da sola começa o raio que procura o chão, em
	/// metros (a sola enterrada num degrau ainda acha o degrau por cima).</summary>
	[Export] public float RayUp = 0.5f;

	/// <summary>Quanto abaixo da sola o raio procura o chão.</summary>
	[Export] public float RayDown = 0.6f;

	/// <summary>Até quantos metros de uma amostra de chão um ponto da sola conta
	/// o chão dela (o mais alto entre as amostras perto): a quina de um degrau.</summary>
	[Export] public float EdgeReach = 0.12f;

	/// <summary>Pontos de sola guardados por pata (o resto da pele não conta).
	/// Com menos, a garra mais baixa podia ficar de fora e entrar 2 a 4 cm.</summary>
	private const int MaxPoints = 360;

	/// <summary>Além desta distância da câmera, em metros, a pata não é ajustada:
	/// de longe 2 cm de garra no chão não se veem, e cada lobo custa uns 1.400
	/// pontos de pele por quadro.</summary>
	[Export] public float PlantDistance = 40f;

	private sealed class Limb
	{
		public string Name;
		public int Root, Mid, End;
		/// <summary>Cada ponto da sola: os ossos que pesam nele, o peso e o ponto
		/// no espaço de cada osso (a pele como o Godot a desenha).</summary>
		public readonly List<(int[] Bones, float[] Weights, Vector3[] Locals)> Sole = new();
		/// <summary>O quanto a pata se moveu no último quadro (para os testes).</summary>
		public float Lift;
		/// <summary>A altura da sola sobre a base do corpo no quadro anterior
		/// (NaN: ainda nenhum) e o apoio, suavizado.</summary>
		public float LastHeight = float.NaN;
		public float Contact;
		/// <summary>O menor vão da sola ao chão no último quadro (para os testes).</summary>
		public float Gap;
		public Vector3[] World = System.Array.Empty<Vector3>();
		public readonly (Vector3 At, Vector3 Normal)[] Samples = new (Vector3, Vector3)[4];
	}

	private readonly List<Limb> _limbs = new();
	private readonly Dictionary<int, Transform3D> _poses = new();
	private CharacterBody3D _body;
	private Node3D _model;

	/// <summary>Liga ou desliga o apoio (no ar, na parede, morto, o corpo não
	/// está apoiado no chão do mapa).</summary>
	public bool Grounded = true;

	/// <summary>O ajuste vertical de cada pata no último quadro, em metros
	/// (positivo: subiu). Para os testes.</summary>
	public IReadOnlyDictionary<string, float> LastLift => _limbs.ToDictionary(l => l.Name, l => l.Lift);

	/// <summary>Monta as patas a partir da pele do modelo.</summary>
	public static WolfFootPlant For(Node3D model, Skeleton3D skeleton, CharacterBody3D body)
	{
		var plant = new WolfFootPlant { Name = "Patas", _body = body, _model = model };
		foreach (string side in new[] { "Left", "Right" })
		{
			plant.AddLimb(skeleton, $"{side}Foot", $"mixamorig_{side}UpLeg", $"mixamorig_{side}Leg", $"mixamorig_{side}Foot",
				new[] { $"mixamorig_{side}Foot", $"mixamorig_{side}ToeBase" }, bottomOnly: false, model);
			plant.AddLimb(skeleton, $"{side}Hand", $"mixamorig_{side}Arm", $"mixamorig_{side}ForeArm", $"mixamorig_{side}Hand",
				new[] { $"mixamorig_{side}Hand" }, bottomOnly: false, model);
		}
		plant._limbs.RemoveAll(l => l.Sole.Count == 0);
		skeleton.AddChild(plant);
		// Antes do rabo e de quem mais ler as patas.
		skeleton.MoveChild(plant, 0);
		return plant;
	}

	private void AddLimb(Skeleton3D skeleton, string name, string root, string mid, string end, string[] soleBones, bool bottomOnly, Node3D model)
	{
		var limb = new Limb { Name = name, Root = skeleton.FindBone(root), Mid = skeleton.FindBone(mid), End = skeleton.FindBone(end) };
		if (limb.Root < 0 || limb.Mid < 0 || limb.End < 0) return;
		var bones = new HashSet<int>(soleBones.Select(skeleton.FindBone).Where(b => b >= 0));

		// Os pontos da pele que pesam mais nesses ossos, no espaço do osso, e
		// a altura deles no repouso (no espaço do modelo, em que y é para cima).
		Transform3D toModel = model.GlobalTransform.AffineInverse() * skeleton.GlobalTransform;
		var found = new List<(int[] Bones, float[] Weights, Vector3[] Locals, float Height)>();
		foreach (MeshInstance3D mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (mesh.Mesh is not ArrayMesh array || mesh.Skin is not Skin skin) continue;
			if (mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) != skeleton) continue;
			for (int s = 0; s < array.GetSurfaceCount(); s++)
			{
				var arrays = array.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] boneIds = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				int per = vertices.Length > 0 ? boneIds.Length / vertices.Length : 4;
				for (int v = 0; v < vertices.Length; v++)
				{
					int dominant = -1;
					float strongest = 0f;
					var ids = new List<int>();
					var ws = new List<float>();
					var locals = new List<Vector3>();
					Vector3 rest = Vector3.Zero;
					for (int k = 0; k < per; k++)
					{
						float w = weights[v * per + k];
						if (w <= 0f) continue;
						int bind = boneIds[v * per + k];
						int bone = skin.GetBindBone(bind);
						if (bone < 0) bone = skeleton.FindBone(skin.GetBindName(bind));
						Vector3 local = skin.GetBindPose(bind) * vertices[v];
						ids.Add(bone);
						ws.Add(w);
						locals.Add(local);
						rest += (skeleton.GetBoneGlobalRest(bone) * local) * w;
						if (w > strongest) { strongest = w; dominant = bone; }
					}
					if (dominant < 0 || !bones.Contains(dominant)) continue;
					found.Add((ids.ToArray(), ws.ToArray(), locals.ToArray(), (toModel * rest).Y));
				}
			}
		}
		if (found.Count == 0) return;

		// O pé: só a parte de baixo (a sola e as garras, até 4 cm acima da mais
		// baixa no repouso, em pé). A mão: toda, porque no repouso ela está
		// pendurada e a palma não aponta para o chão.
		IEnumerable<(int[] Bones, float[] Weights, Vector3[] Locals, float Height)> chosen = found;
		if (bottomOnly)
		{
			float low = found.Min(f => f.Height);
			chosen = found.Where(f => f.Height <= low + 0.04f / Mathf.Max(model.Scale.Y, 1e-4f));
		}
		var list = chosen.ToList();
		int step = Mathf.Max(1, list.Count / MaxPoints);
		for (int i = 0; i < list.Count; i += step) limb.Sole.Add((list[i].Bones, list[i].Weights, list[i].Locals));
		_limbs.Add(limb);
	}

	private VisualSmoothing _smoothing;

	public override void _ProcessModificationWithDelta(double delta)
	{
		Skeleton3D skeleton = GetSkeleton();
		if (skeleton == null || _body == null || !IsInstanceValid(_body)) return;
		foreach (Limb limb in _limbs) limb.Lift = 0f;
		if (!Grounded)
		{
			foreach (Limb limb in _limbs) { limb.LastHeight = float.NaN; limb.Contact = 0f; }
			return;
		}
		Camera3D camera = GetViewport()?.GetCamera3D();
		if (camera != null && camera.GlobalPosition.DistanceSquaredTo(_body.GlobalPosition) > PlantDistance * PlantDistance)
		{
			foreach (Limb limb in _limbs) { limb.LastHeight = float.NaN; limb.Contact = 0f; }
			return;
		}
		float dt = Mathf.Max((float)delta, 1e-4f);
		float baseY = _model != null && IsInstanceValid(_model) ? _model.GlobalPosition.Y : _body.GlobalPosition.Y;
		// Depois de um degrau o corpo desenhado sobe devagar atrás do da física
		// (VisualSmoothing.Lift, 27/11), e esse deslocamento entra no fim do quadro,
		// depois desta conta: a pata era posta no chão e o corpo inteiro descia em
		// seguida — 6 cm dentro do degrau (PesDoLoboNoChao). A sola é medida onde vai
		// ser desenhada.
		_smoothing ??= _body.GetNodeOrNull<VisualSmoothing>("VisualSmoothing");
		float pending = _smoothing?.PendingLift ?? 0f;
		float blend = 1f - Mathf.Exp(-dt / Mathf.Max(ContactTime, 1e-3f));

		Transform3D toWorld = skeleton.GlobalTransform;
		Transform3D toSkeleton = toWorld.AffineInverse();
		PhysicsDirectSpaceState3D space = _body.GetWorld3D()?.DirectSpaceState;
		if (space == null) return;

		foreach (Limb limb in _limbs)
		{
			// A sola agora, no mundo, e a mais baixa.
			if (limb.World.Length != limb.Sole.Count) limb.World = new Vector3[limb.Sole.Count];
			Vector3 lowest = Vector3.Zero, center = Vector3.Zero;
			float lowY = float.MaxValue;
			_poses.Clear();
			for (int i = 0; i < limb.Sole.Count; i++)
			{
				var (bones, weights, locals) = limb.Sole[i];
				Vector3 skinned = Vector3.Zero;
				for (int k = 0; k < bones.Length; k++)
				{
					if (!_poses.TryGetValue(bones[k], out Transform3D pose)) _poses[bones[k]] = pose = skeleton.GetBoneGlobalPose(bones[k]);
					skinned += (pose * locals[k]) * weights[k];
				}
				Vector3 p = limb.World[i] = toWorld * skinned;
				center += p;
				if (p.Y < lowY) { lowY = p.Y; lowest = p; }
			}
			center /= limb.Sole.Count;

			// Apoiando? A sola parada na vertical, em relação ao corpo.
			float height = lowY - baseY;
			float speed = float.IsNaN(limb.LastHeight) ? 0f : (height - limb.LastHeight) / dt;
			limb.LastHeight = height;
			limb.Contact = Mathf.Lerp(limb.Contact, FootPlant.Stance(speed, StillSpeed), blend);

			// O chão em quatro pontos da pata: as duas pontas (calcanhar e garras, no
			// sentido em que ela é mais comprida), o meio e a mais baixa. Numa rampa ou na
			// quina de um degrau o chão muda ao longo do pé: só embaixo do ponto
			// mais baixo, a ponta das garras entrava 6 cm no degrau.
			Vector3 far = Farthest(limb.World, center), back = Farthest(limb.World, far);
			int samples = 0;
			foreach (Vector3 at in new[] { lowest, far, back, center })
			{
				if (!Floor(space, at, out Vector3 hit, out Vector3 normal)) continue;
				limb.Samples[samples++] = (hit, normal);
			}
			if (samples == 0) continue;

			// O vão de cada ponto da sola ao chão embaixo dele: o plano do chão de
			// cada amostra, no ponto (exato numa rampa, também de lado). Perto de
			// uma quina (amostras a até EdgeReach), vale o chão mais alto: a garra
			// que passa rente à quina de um degrau não entra nela. O menor vão manda.
			float gap = float.MaxValue, reach = EdgeReach * EdgeReach;
			foreach (Vector3 p in limb.World)
			{
				float floorY = float.MinValue, best = float.MaxValue, nearestY = 0f;
				for (int k = 0; k < samples; k++)
				{
					var (point, n) = limb.Samples[k];
					float d = (p.X - point.X) * (p.X - point.X) + (p.Z - point.Z) * (p.Z - point.Z);
					float y = n.Y > 0.3f ? point.Y - (n.X * (p.X - point.X) + n.Z * (p.Z - point.Z)) / n.Y : point.Y;
					if (d <= reach) floorY = Mathf.Max(floorY, y);
					if (d < best) { best = d; nearestY = y; }
				}
				if (floorY == float.MinValue) floorY = nearestY;
				gap = Mathf.Min(gap, p.Y + pending - floorY);
			}
			limb.Gap = gap;

			float lift = Mathf.Clamp(FootPlant.Lift(gap, limb.Contact, ContactBand), -MaxLift, MaxLift);
			if (Mathf.Abs(lift) < 0.001f) continue;
			limb.Lift = lift;
			Move(skeleton, limb, toSkeleton.Basis * new Vector3(0, lift, 0));
		}
	}

	/// <summary>A chapa lisa, só de colisão, que os mapas põem por cima dos degraus
	/// de uma escada (Montagem.rampa): "pr0", "er3"... no grupo Colisao.</summary>
	private static bool IsStairPlate(Node node) =>
		node is StaticBody3D && node.GetParent()?.Name == "Colisao"
		&& System.Text.RegularExpressions.Regex.IsMatch(node.Name, "^[a-z]r\\d+$");

	/// <summary>O ponto mais longe de <paramref name="from"/> na horizontal.</summary>
	private static Vector3 Farthest(Vector3[] points, Vector3 from)
	{
		Vector3 best = from;
		float far = -1f;
		foreach (Vector3 p in points)
		{
			float d = (p.X - from.X) * (p.X - from.X) + (p.Z - from.Z) * (p.Z - from.Z);
			if (d > far) { far = d; best = p; }
		}
		return best;
	}

	/// <summary>O chão embaixo de um ponto: a colisão do mapa, sem contar
	/// corpos (o próprio, o de um bot do lado).</summary>
	private bool Floor(PhysicsDirectSpaceState3D space, Vector3 at, out Vector3 point, out Vector3 normal)
	{
		point = at;
		normal = Vector3.Up;
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * RayUp, at + Vector3.Down * RayDown, _body.CollisionMask);
		var exclude = new Godot.Collections.Array<Rid> { _body.GetRid() };
		for (int tries = 0; tries < 4; tries++)
		{
			query.Exclude = exclude;
			var hit = space.IntersectRay(query);
			if (hit.Count == 0) return false;
			// Corpos não contam, e a chapa invisível das escadas também não: a pata
			// pisa no degrau que se vê, embaixo dela (Montagem.rampa: "<x>r<n>" em Colisao).
			if (hit["collider"].AsGodotObject() is CharacterBody3D or RigidBody3D || IsStairPlate(hit["collider"].AsGodotObject() as Node))
			{
				exclude.Add((Rid)hit["rid"]);
				continue;
			}
			point = (Vector3)hit["position"];
			normal = (Vector3)hit["normal"];
			return true;
		}
		return false;
	}

	/// <summary>Leva a ponta da pata (tornozelo, pulso) pelo deslocamento
	/// dado, no espaço do esqueleto, dobrando a coxa e a canela; a sola fica
	/// no ângulo em que estava.</summary>
	private static void Move(Skeleton3D skeleton, Limb limb, Vector3 offset)
	{
		Transform3D root = skeleton.GetBoneGlobalPose(limb.Root);
		Transform3D mid = skeleton.GetBoneGlobalPose(limb.Mid);
		Transform3D end = skeleton.GetBoneGlobalPose(limb.End);

		var (newMid, newEnd) = FootPlant.Solve(N(root.Origin), N(mid.Origin), N(end.Origin), N(end.Origin + offset));
		Vector3 m2 = G(newMid), e2 = G(newEnd);

		// A coxa gira até o joelho novo; a canela, até a ponta nova.
		Quaternion first = G(FootPlant.Arc(N(mid.Origin - root.Origin), N(m2 - root.Origin)));
		Vector3 endAfterFirst = root.Origin + first * (end.Origin - root.Origin);
		Vector3 midAfterFirst = root.Origin + first * (mid.Origin - root.Origin);
		Quaternion second = G(FootPlant.Arc(N(endAfterFirst - midAfterFirst), N(e2 - m2)));

		skeleton.SetBoneGlobalPose(limb.Root, new Transform3D(Rotate(first, root.Basis), root.Origin));
		skeleton.SetBoneGlobalPose(limb.Mid, new Transform3D(Rotate(second * first, mid.Basis), m2));
		// A sola mantém o ângulo da animação.
		skeleton.SetBoneGlobalPose(limb.End, new Transform3D(end.Basis, e2));
	}

	private static Basis Rotate(Quaternion q, Basis basis) => new Basis(q) * basis;

	private static System.Numerics.Vector3 N(Vector3 v) => new(v.X, v.Y, v.Z);
	private static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
	private static Quaternion G(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
}
