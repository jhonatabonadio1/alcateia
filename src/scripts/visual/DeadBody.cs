using Godot;
using System.Collections.Generic;

/// <summary>
/// O corpo de quem morreu, como se vê: assenta no chão e some por igual
/// (Jhon, 26/11: "corpo do morto afundando/ficando transparente, dá para ver o
/// interior dos personagens. Ele deve ficar transparente por igual, sem poder
/// ver o interior do corpo. E não deve afundar, deve ficar no limite do chão").
/// O mesmo para o humano (<see cref="CharacterAnimator"/>) e o lobo
/// (<see cref="WolfVisual"/>), o seu, o do bot e o boneco da rede.
///
/// **No chão.** O clipe de morte foi animado sobre um chão que não é o do mapa:
/// o lobo (o "mutant dying" do Mixamo) entrava 28 cm no chão no meio da queda e
/// terminava 23 cm enterrado; a Eve ficava 5 cm pairando (o 12 cm fixo que
/// descia todo humano, <c>CorpseDrop</c>, servia ao X Bot). Agora, a cada quadro,
/// o ponto mais baixo da pele (a pele deformada pelo esqueleto, numa amostra
/// dos vértices) é medido contra o chão logo abaixo dele: caindo, o corpo só
/// sobe o que entraria no chão; depois de cair, encosta nele.
///
/// **Deitado no chão que tem** (27/11; Jhon: "quando o lobo morre ou o humano,
/// ele tá ficando no ar"). O clipe deita o corpo na horizontal. Numa escada ou
/// num declive, medido: a ponta do corpo ficava 50 a 80 cm acima dos degraus.
/// Do meio da queda em diante o corpo se inclina até o plano do chão que há
/// debaixo dele — a altura do chão sob o quadril, a cabeça, as mãos e os pés
/// (<see cref="Tilt"/>), até <see cref="MaxTilt"/> graus —, e só então encosta.
/// Na beira de uma laje, o que fica para fora (o chão lá embaixo) não conta.
///
/// **Some por igual.** Cada superfície da pele vira duas passadas: primeiro a
/// profundidade do corpo inteiro, sem cor (<c>corpo_profundidade.gdshader</c>,
/// ou a do lobo, <c>lobo_profundidade.gdshader</c>, com o pelo balançando
/// igual); depois a cor, transparente, que só passa na pele de fora. O
/// "AlphaDepthPrePass" do Godot que se usava só escreve a profundidade onde o
/// alfa passa de 0,99: no primeiro quadro do sumiço ela sumia, e os olhos, os
/// dentes, o cabelo de baixo e o forro apareciam por cima da pele. O que não é
/// pele (a arma na mão, os discos dos olhos acesos) some pela transparência
/// da própria malha (<see cref="GeometryInstance3D.Transparency"/>).
/// </summary>
public sealed class DeadBody
{
	public const string DepthPath = "res://src/assets/efeitos/corpo_profundidade.gdshader";
	public const string WolfDepthPath = "res://src/assets/efeitos/lobo_profundidade.gdshader";
	public const string WolfFadePath = "res://src/assets/efeitos/lobo_sumindo.gdshader";

	/// <summary>A marca na passada de profundidade do sumiço (o teste confere).</summary>
	public const string Mark = "corpo_sumindo";

	/// <summary>O material é a passada de profundidade do sumiço.</summary>
	public static bool IsFading(Material material) => material != null && material.HasMeta(Mark);

	/// <summary>Tempo à mostra no chão antes de sumir, e o sumiço, em segundos.</summary>
	public float Hold = 1.2f, Fade = 1.2f;

	/// <summary>O quanto o corpo pode subir (m) para sair do chão, e descer
	/// para encostar nele: mais que isso é a medida errada (o chão de baixo de
	/// uma escada, uma borda), e o corpo fica onde o clipe o põe.</summary>
	public float MaxLift = 0.6f, MaxDrop = 0.4f;

	/// <summary>O quanto o corpo deitado se inclina para acompanhar o chão (graus),
	/// e a partir de que ponto do clipe de morte (0 a 1) ele começa a se inclinar
	/// e já está todo inclinado.</summary>
	public float MaxTilt = 42f, TiltFrom = 0.45f, TiltBy = 0.85f;

	/// <summary>Um ponto do corpo cujo chão difere mais que isto (m) do chão do meio
	/// dele está para fora de uma beira: não entra na conta da inclinação. (Numa
	/// escada de 33° o corpo deitado, de 1,7 m, tem até 1,1 m de desnível.)</summary>
	public float TiltLedge = 1.4f;

	/// <summary>E o ponto cujo chão fica mais que isto (m) acima do plano dos outros
	/// está em cima de uma mureta ou de um caixote: sai da conta do plano.</summary>
	public float TiltCurb = 0.08f;

	private readonly Node3D _model;
	private readonly CollisionObject3D _body;
	private float _baseY, _offset, _delta, _progress;
	private bool _begun, _fallen;
	private Transform3D _base;
	private Quaternion _tilt = Quaternion.Identity;
	private Vector3 _pivot;

	/// <summary>O plano do chão debaixo do corpo deitado (<see cref="Tilt"/>): um
	/// ponto dele e a normal. Falso antes de o corpo começar a deitar.</summary>
	public bool HasGround { get; private set; }
	public Vector3 GroundPoint { get; private set; }
	public Vector3 GroundNormal { get; private set; } = Vector3.Up;

	/// <summary>Quanto o quadril do corpo deitado fica, no mínimo, acima do plano
	/// do chão (m): é até onde o corpo desce para encostar num declive.</summary>
	public float HipsClear = 0.03f;

	/// <summary>Quanto o chão debaixo de um ponto do corpo deitado pode ficar abaixo
	/// do plano do chão dele (m): a altura de um degrau.</summary>
	public float LedgeSlack = 0.2f;

	/// <summary>E quanto pode ficar acima dele (m): mais que isto é uma mureta, um
	/// rodapé, um caixote ao lado — a ponta do corpo entra nele, o corpo não sobe.</summary>
	public float CurbSlack = 0.06f;
	private readonly List<int> _tiltBones = new();

	/// <summary>O esqueleto que avisa quando a pose do quadro ficou pronta: a
	/// medida é feita ali, e não no _Process do dono, que corre antes da
	/// animação (um quadro atrasado, o lobo caindo rápido entrava 5 cm).</summary>
	private Skeleton3D _listening;

	private readonly List<Sampled> _skin = new();
	private readonly List<(MeshInstance3D Mesh, int Surface, Material Before)> _surfaces = new();
	private readonly List<(BaseMaterial3D Material, float Alpha, float Emission)> _colors = new();
	private readonly List<ShaderMaterial> _wolfColors = new();
	private readonly List<(GeometryInstance3D Node, float Before)> _instances = new();

	/// <summary>Uma malha da pele e a amostra dos vértices dela (vértice, os
	/// ossos e os pesos), tirada uma vez por morte.</summary>
	private sealed class Sampled
	{
		public MeshInstance3D Mesh;
		public Skeleton3D Skeleton;
		public int[] BindBone;
		public Transform3D[] BindPose;
		public Transform3D[] Bind;
		public readonly List<Vector3> Vertex = new();
		public readonly List<int> Bones = new();
		public readonly List<float> Weights = new();
	}

	/// <summary>Ossos por vértice guardados (o Godot usa 4 ou 8).</summary>
	private const int Slots = 8;

	/// <param name="owner">O nó do corpo que o dono esconde quando não é a
	/// forma de agora (o humano de quem morreu lobo, e o contrário): escondido,
	/// nada é medido.</param>
	public DeadBody(Node3D model, CollisionObject3D body, Node3D owner)
	{
		_model = model;
		_body = body;
		_owner = owner;
	}

	private readonly Node3D _owner;

	/// <summary>O corpo desta forma está à mostra (o lobo morto não mede o humano escondido).</summary>
	private bool Shown => _model != null && _model.Visible && (_owner == null || _owner.Visible);

	/// <summary>O modelo deste corpo (o personagem pode trocar de modelo).</summary>
	public Node3D Model => _model;

	/// <summary>Algum material do sumiço está no corpo agora.</summary>
	public bool HasFadeMaterials => _surfaces.Count > 0 || _instances.Count > 0;

	/// <summary>Quanto o corpo foi levado para encostar no chão, em metros
	/// (positivo: para cima).</summary>
	public float Offset => _offset;

	/// <summary>Morreu: guarda o lugar do modelo e a amostra da pele.</summary>
	/// <param name="rest">O lugar do modelo de pé, se o dono o mexe em vida (o lobo:
	/// a pose da parede, a inclinação da corrida); sem ele, o da primeira morte.</param>
	public void Begin(Transform3D? rest = null)
	{
		if (_model == null) return;
		if (rest is Transform3D given)
		{
			_base = given;
			_baseY = given.Origin.Y;
		}
		else if (!_begun)
		{
			_baseY = _model.Position.Y;
			_base = _model.Transform;
		}
		_begun = true;
		_offset = 0f;
		_fallen = false;
		_progress = 0f;
		_tilt = Quaternion.Identity;
		HasGround = false;
		SampleSkin();
		FindTiltBones();
		Listen(_skin.Count > 0 ? _skin[0].Skeleton : null);
	}

	/// <summary>
	/// Um quadro do corpo morto. <paramref name="timer"/> é o tempo desde a
	/// morte; <paramref name="clip"/>, a duração do clipe de morte. Durante o
	/// clipe o corpo só sobe o que entraria no chão; depois, encosta nele, fica
	/// <see cref="Hold"/> à mostra e some em <see cref="Fade"/>.
	/// </summary>
	public void Step(double delta, double timer, double clip)
	{
		if (!_begun || !Shown) return;

		_fallen = timer >= clip;
		_progress = clip > 0.01 ? (float)(timer / clip) : 1f;
		_delta = (float)delta;
		if (_listening == null) OnPose();

		double t = (timer - clip - Hold) / Mathf.Max(Fade, 0.01f);
		float progress = t <= 0 ? 0f : Mathf.Clamp((float)t, 0f, 1f);
		if (progress <= 0f) return;

		SetOpacity(1f - progress);
		if (progress >= 1f)
		{
			_model.Visible = false;
			Listen(null);
		}
	}

	/// <summary>A pose do quadro está pronta: encosta o corpo no chão.</summary>
	private void OnPose()
	{
		// O esqueleto ainda avisa a pose enquanto a cena sai (a troca de cena do
		// "Voltar à sala"): fora da árvore não há chão nem posição no mundo para
		// medir (26/11: 30 erros "!is_inside_tree()" no VoltarASalaSemCrash).
		if (!_begun || !Shown || !GodotObject.IsInstanceValid(_model) || !_model.IsInsideTree()) return;
		Tilt(_delta);
		Place();
		FitFloor(_delta, _fallen);
		Place();
	}

	/// <summary>Põe o modelo: onde a cena o tem, inclinado em volta do meio do
	/// corpo, e subido (ou descido) o que falta para encostar no chão.</summary>
	private void Place()
	{
		if (_tilt.IsEqualApprox(Quaternion.Identity) || _model.GetParent() is not Node3D parent)
		{
			_model.Transform = new Transform3D(_base.Basis, new Vector3(_base.Origin.X, _baseY + _offset, _base.Origin.Z));
			return;
		}
		Transform3D flat = parent.GlobalTransform * _base;
		var tilted = new Transform3D(new Basis(_tilt) * flat.Basis, _pivot + _tilt * (flat.Origin - _pivot));
		tilted.Origin += Vector3.Up * _offset;
		_model.GlobalTransform = tilted;
	}

	/// <summary>Os ossos que dizem onde o corpo está deitado: o quadril, a cabeça,
	/// as mãos e os pés (os nomes do Mixamo, no humano e no lobo).</summary>
	private void FindTiltBones()
	{
		_tiltBones.Clear();
		if (_skin.Count == 0) return;
		Skeleton3D skeleton = _skin[0].Skeleton;
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
		{
			string name = skeleton.GetBoneName(i);
			if (name.EndsWith("Hips") || name.EndsWith("Head") || name.EndsWith("LeftHand") || name.EndsWith("RightHand")
				|| name.EndsWith("LeftFoot") || name.EndsWith("RightFoot"))
				_tiltBones.Add(i);
		}
	}

	/// <summary>O plano y = a·x + b·z + c que melhor passa pelos pontos (mínimos
	/// quadrados, em volta do meio deles). Falso se eles estão em linha: não
	/// dizem plano nenhum.</summary>
	private static bool FitPlane(System.ReadOnlySpan<Vector3> points, out Vector3 mean, out float a, out float b)
	{
		mean = Vector3.Zero;
		a = b = 0f;
		foreach (Vector3 point in points) mean += point;
		mean /= Mathf.Max(points.Length, 1);
		float sxx = 0, szz = 0, sxz = 0, sxy = 0, szy = 0;
		foreach (Vector3 point in points)
		{
			Vector3 d = point - mean;
			sxx += d.X * d.X; szz += d.Z * d.Z; sxz += d.X * d.Z; sxy += d.X * d.Y; szy += d.Z * d.Y;
		}
		float det = sxx * szz - sxz * sxz;
		if (det < 1e-4f) return false;
		a = (sxy * szz - szy * sxz) / det;
		b = (szy * sxx - sxy * sxz) / det;
		return true;
	}

	/// <summary>
	/// Inclina o corpo até o plano do chão debaixo dele: o plano que melhor passa
	/// pela altura do chão sob cada osso de <see cref="FindTiltBones"/> (mínimos
	/// quadrados), sem os que ficam para fora de uma beira. Entra aos poucos, do
	/// meio do clipe de morte ao fim dele: quem leva o tiro em pé ainda não deitou.
	/// </summary>
	private void Tilt(float delta)
	{
		float weight = Mathf.SmoothStep(TiltFrom, TiltBy, _progress);
		if (weight <= 0f || _tiltBones.Count < 3 || _skin.Count == 0) return;
		Skeleton3D skeleton = _skin[0].Skeleton;
		if (!GodotObject.IsInstanceValid(skeleton)) return;

		// O chão debaixo de cada osso; o do primeiro (o quadril) é a referência.
		Transform3D toWorld = skeleton.GlobalTransform;
		System.Span<Vector3> points = stackalloc Vector3[_tiltBones.Count];
		int count = 0;
		float reference = float.NaN;
		foreach (int bone in _tiltBones)
		{
			Vector3 at = toWorld * skeleton.GetBoneGlobalPose(bone).Origin;
			if (!Floor(at, out float floor)) continue;
			if (float.IsNaN(reference)) reference = floor;
			else if (Mathf.Abs(floor - reference) > TiltLedge) continue;
			points[count++] = new Vector3(at.X, floor, at.Z);
		}
		if (count < 3) return;

		if (!FitPlane(points[..count], out Vector3 mean, out float a, out float b)) return;
		// Um ponto bem acima do plano está em cima de outra coisa (a mureta do lado
		// da escada, um caixote): sem ele o plano é o do chão. Medido na escada do
		// Deserto, o lobo ficava 12 cm mais alto que no chão plano por causa de um.
		int kept = 0;
		for (int i = 0; i < count; i++)
		{
			Vector3 d = points[i] - mean;
			if (d.Y - (a * d.X + b * d.Z) <= TiltCurb) points[kept++] = points[i];
		}
		if (kept < count && kept >= 3 && FitPlane(points[..kept], out Vector3 mean2, out float a2, out float b2))
			(mean, a, b) = (mean2, a2, b2);
		Vector3 normal = new Vector3(-a, 1f, -b).Normalized();
		HasGround = true;
		GroundPoint = mean;
		GroundNormal = normal;

		float angle = Mathf.Min(Vector3.Up.AngleTo(normal), Mathf.DegToRad(MaxTilt)) * weight;
		Vector3 axis = Vector3.Up.Cross(normal);
		Quaternion target = axis.LengthSquared() < 1e-8f ? Quaternion.Identity : new Quaternion(axis.Normalized(), angle);
		_tilt = _tilt.Slerp(target, 1f - Mathf.Exp(-delta / 0.12f)).Normalized();
		_pivot = mean;
	}

	private void Listen(Skeleton3D skeleton)
	{
		if (_listening == skeleton) return;
		if (_listening != null && GodotObject.IsInstanceValid(_listening)) _listening.SkeletonUpdated -= OnPose;
		_listening = skeleton;
		if (_listening != null) _listening.SkeletonUpdated += OnPose;
	}

	/// <summary>Renasceu: a altura, os materiais e a transparência de antes.</summary>
	public void Reset()
	{
		Listen(null);
		Restore();
		if (_model != null && _begun)
		{
			_model.Transform = new Transform3D(_base.Basis, new Vector3(_base.Origin.X, _baseY, _base.Origin.Z));
			_model.Visible = true;
		}
		_offset = 0f;
		_tilt = Quaternion.Identity;
		_skin.Clear();
	}

	// ---------------------------------------------------------------- no chão

	private void SampleSkin()
	{
		_skin.Clear();
		foreach (Node node in _model.FindChildren("*", nameof(MeshInstance3D), true, false))
		{
			if (node is not MeshInstance3D { Mesh: Mesh mesh, Skin: Skin skin } instance) continue;
			Skeleton3D skeleton = instance.GetNodeOrNull<Skeleton3D>(instance.Skeleton);
			if (skeleton == null) continue;

			var sampled = new Sampled { Mesh = instance, Skeleton = skeleton };
			int binds = skin.GetBindCount();
			sampled.BindBone = new int[binds];
			sampled.BindPose = new Transform3D[binds];
			sampled.Bind = new Transform3D[binds];
			for (int k = 0; k < binds; k++)
			{
				int bone = skin.GetBindBone(k);
				sampled.BindBone[k] = bone >= 0 ? bone : skeleton.FindBone(skin.GetBindName(k));
				sampled.BindPose[k] = skin.GetBindPose(k);
			}

			for (int s = 0; s < mesh.GetSurfaceCount(); s++)
			{
				var arrays = mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				if (vertices.Length == 0) continue;
				int per = Mathf.Min(bones.Length / vertices.Length, Slots);
				// Uns 6000 por superfície: o bastante para achar o pé, a garra e o
				// rabo (o lobo é uma malha só), e barato para fazer a cada quadro.
				int stride = Mathf.Max(1, vertices.Length / 6000);
				for (int v = 0; v < vertices.Length; v += stride)
				{
					int stored = bones.Length / vertices.Length;
					if (Loose(sampled, bones, weights, v * stored, per)) continue;
					sampled.Vertex.Add(vertices[v]);
					for (int j = 0; j < Slots; j++)
					{
						sampled.Bones.Add(j < per ? bones[v * stored + j] : 0);
						sampled.Weights.Add(j < per ? weights[v * stored + j] : 0f);
					}
				}
			}
			if (sampled.Vertex.Count > 0) _skin.Add(sampled);
		}
	}

	/// <summary>
	/// Os ossos soltos, que não entram na medida do chão: os de mola (o rabo do
	/// lobo). A medida supõe que o ponto mais baixo desce e sobe junto com o
	/// corpo; a mola não — ela pende até o chão dela, seja qual for a altura
	/// do corpo. Com o rabo na conta o lobo morto flutuava (Jhon, 27/11:
	/// "parece que o lobo encosta, porém o rabo dele o levanta"): a ponta do
	/// rabo, pousada no chão, era sempre "o ponto que entraria no chão"; o
	/// corpo subia um centímetro para tirá-la dali, o rabo esticava e pousava
	/// de novo, e assim até o teto (<see cref="MaxLift"/>, 60 cm). Quem
	/// segura o rabo fora do chão é o plano dele (WolfVisual.Corpse).
	/// </summary>
	public System.Collections.Generic.ICollection<string> LooseBones { get; set; }

	/// <summary>O vértice é mais de um osso solto que do resto do corpo.</summary>
	private bool Loose(Sampled sampled, int[] bones, float[] weights, int first, int per)
	{
		if (LooseBones == null || LooseBones.Count == 0) return false;
		float loose = 0f, total = 0f;
		for (int j = 0; j < per; j++)
		{
			float w = weights[first + j];
			if (w <= 0f) continue;
			total += w;
			int bind = bones[first + j];
			int bone = bind >= 0 && bind < sampled.BindBone.Length ? sampled.BindBone[bind] : -1;
			if (bone >= 0 && LooseBones.Contains(sampled.Skeleton.GetBoneName(bone))) loose += w;
		}
		return total > 0f && loose > total * 0.5f;
	}

	/// <summary>A altura do plano do chão do corpo deitado em (x, z).</summary>
	private float PlaneAt(Vector3 at) =>
		GroundPoint.Y - (GroundNormal.X * (at.X - GroundPoint.X) + GroundNormal.Z * (at.Z - GroundPoint.Z)) / Mathf.Max(GroundNormal.Y, 0.3f);

	/// <summary>O ponto da pele mais perto do chão agora, no mundo: o mais baixo;
	/// ou, com o plano do chão conhecido (<see cref="Tilt"/>), o de menor altura
	/// acima dele — num declive o ponto mais baixo é a ponta de baixo do corpo, e
	/// quem entra no chão é a de cima. Falso sem pele.</summary>
	private bool Lowest(out Vector3 lowest)
	{
		lowest = Vector3.Zero;
		float low = float.MaxValue;
		bool plane = HasGround;
		foreach (Sampled s in _skin)
		{
			if (!GodotObject.IsInstanceValid(s.Mesh) || !GodotObject.IsInstanceValid(s.Skeleton)) continue;
			Transform3D skeleton = s.Skeleton.GlobalTransform;
			for (int k = 0; k < s.Bind.Length; k++)
				s.Bind[k] = s.BindBone[k] >= 0 ? skeleton * s.Skeleton.GetBoneGlobalPose(s.BindBone[k]) * s.BindPose[k] : skeleton;
			for (int v = 0; v < s.Vertex.Count; v++)
			{
				Vector3 p = Vector3.Zero;
				for (int j = 0; j < Slots; j++)
				{
					float w = s.Weights[v * Slots + j];
					if (w > 0f) p += s.Bind[s.Bones[v * Slots + j]] * s.Vertex[v] * w;
				}
				float height = plane ? p.Y - PlaneAt(p) : p.Y;
				if (height < low)
				{
					low = height;
					lowest = p;
				}
			}
		}
		return low < float.MaxValue;
	}

	private void FitFloor(float delta, bool fallen)
	{
		if (_skin.Count == 0 || !Lowest(out Vector3 low)) return;
		// O ponto mais baixo sem a correção de agora: a pose do clipe.
		float pose = low.Y - _offset;
		bool hit = Floor(new Vector3(low.X, pose, low.Z), out float floor);
		// Deitado num chão de plano conhecido, o chão do ponto não fica muito abaixo
		// dele (um degrau): o que pende para fora de uma beira não puxa o corpo para
		// o chão lá de baixo.
		// E nem muito acima (a mureta do lado da escada, o rodapé de uma parede): uma
		// ponta do corpo por cima de algo mais alto não levanta o corpo inteiro.
		if (HasGround) floor = hit ? Mathf.Clamp(floor, PlaneAt(low) - LedgeSlack, PlaneAt(low) + CurbSlack) : PlaneAt(low);
		else if (!hit) return;

		float target = Mathf.Clamp(floor - pose, -DropLimit(), MaxLift);
		if (!fallen)
		{
			// Caindo, o clipe manda: só sobe o que entraria no chão.
			_offset = Mathf.Max(target, 0f);
			return;
		}
		// Caído: encosta. Subir é na hora (nada entra no chão); descer, em
		// um quarto de segundo, para não dar um tranco.
		_offset = target > _offset ? target : Mathf.Lerp(_offset, target, 1f - Mathf.Exp(-delta / 0.08f));
	}

	/// <summary>
	/// O quanto o corpo pode descer para encostar. Deitado num chão de plano
	/// conhecido (<see cref="Tilt"/>): até o quadril ficar a <see cref="HipsClear"/>
	/// dele — num declive o corpo, girado em volta do meio, fica paralelo ao chão
	/// mas longe dele (na escada do Deserto, 35 cm; o limite fixo de
	/// <see cref="MaxDrop"/> o deixava no ar). Sem plano: <see cref="MaxDrop"/>.
	/// </summary>
	private float DropLimit()
	{
		if (!HasGround || _tiltBones.Count == 0 || _skin.Count == 0) return MaxDrop;
		Skeleton3D skeleton = _skin[0].Skeleton;
		if (!GodotObject.IsInstanceValid(skeleton)) return MaxDrop;
		Vector3 hips = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(_tiltBones[0]).Origin;
		float plane = GroundPoint.Y - (GroundNormal.X * (hips.X - GroundPoint.X) + GroundNormal.Z * (hips.Z - GroundPoint.Z)) / Mathf.Max(GroundNormal.Y, 0.3f);
		// (a altura do quadril sem a correção de agora, contra o plano)
		return Mathf.Max(MaxDrop, (hips.Y - _offset) - (plane + HipsClear));
	}

	private bool Floor(Vector3 at, out float floor) => FloorUnder(_model, _body, at, out floor);

	/// <summary>A altura do chão logo abaixo do ponto (o cenário, não os
	/// corpos), no mundo de <paramref name="node"/>.</summary>
	public static bool FloorUnder(Node3D node, CollisionObject3D self, Vector3 at, out float floor)
	{
		floor = 0f;
		World3D world = node?.GetWorld3D();
		if (world == null) return false;
		var exclude = new Godot.Collections.Array<Rid>();
		if (self != null) exclude.Add(self.GetRid());
		for (int tries = 0; tries < 4; tries++)
		{
			var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 0.8f, at + Vector3.Down * 2.5f, 1, exclude);
			var hit = world.DirectSpaceState.IntersectRay(query);
			if (hit.Count == 0) return false;
			if (hit["collider"].AsGodotObject() is CharacterBody3D other)
			{
				exclude.Add(other.GetRid());
				continue;
			}
			floor = ((Vector3)hit["position"]).Y;
			return true;
		}
		return false;
	}

	// ---------------------------------------------------------------- o sumiço

	private void SetOpacity(float opacity)
	{
		if (!HasFadeMaterials) Prepare();

		foreach (var (material, alpha, emission) in _colors)
		{
			Color color = material.AlbedoColor;
			color.A = alpha * opacity;
			material.AlbedoColor = color;
			// O brilho (as faixas acesas da roupa) apaga junto, um pouco antes.
			material.EmissionEnergyMultiplier = emission * opacity * opacity;
		}
		foreach (ShaderMaterial material in _wolfColors)
			material.SetShaderParameter("sumindo", opacity);
		foreach (var (node, before) in _instances)
			if (GodotObject.IsInstanceValid(node)) node.Transparency = 1f - (1f - before) * opacity;
	}

	private void Prepare()
	{
		Shader depth = GD.Load<Shader>(DepthPath);
		foreach (Node node in _model.FindChildren("*", nameof(GeometryInstance3D), true, false))
		{
			var geometry = (GeometryInstance3D)node;
			if (geometry is MeshInstance3D { Mesh: Mesh mesh, Skin: not null } skinned && geometry.MaterialOverride == null)
			{
				bool all = true;
				for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
					all &= PrepareSurface(skinned, surface, depth);
				if (all) continue;
			}
			// O que não é pele (a arma na mão, os olhos acesos): a malha inteira.
			_instances.Add((geometry, geometry.Transparency));
		}
	}

	/// <summary>A superfície vira profundidade antes, cor transparente depois.
	/// Falso se o material não é dos que se sabe dividir.</summary>
	private bool PrepareSurface(MeshInstance3D mesh, int surface, Shader depthShader)
	{
		Material active = mesh.GetActiveMaterial(surface);
		ShaderMaterial depth;
		Material color;

		if (active is ShaderMaterial wolf && wolf.Shader?.ResourcePath == WolfSkin.ShaderPath)
		{
			depth = Copy(wolf, GD.Load<Shader>(WolfDepthPath));
			var fade = Copy(wolf, GD.Load<Shader>(WolfFadePath));
			fade.SetShaderParameter("sumindo", 1f);
			_wolfColors.Add(fade);
			color = fade;
		}
		else if (active is BaseMaterial3D source)
		{
			var fade = (BaseMaterial3D)source.Duplicate();
			fade.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			_colors.Add((fade, fade.AlbedoColor.A, fade.EmissionEnergyMultiplier));
			color = fade;

			depth = new ShaderMaterial { Shader = depthShader };
			depth.SetShaderParameter("albedo", source.AlbedoColor);
			depth.SetShaderParameter("uv1_scale", source.Uv1Scale);
			depth.SetShaderParameter("uv1_offset", source.Uv1Offset);
			bool cut = source.Transparency != BaseMaterial3D.TransparencyEnum.Disabled && source.AlbedoTexture != null;
			depth.SetShaderParameter("use_cutout", cut);
			if (cut) depth.SetShaderParameter("albedo_tex", source.AlbedoTexture);
			depth.SetShaderParameter("cutout", source.Transparency == BaseMaterial3D.TransparencyEnum.AlphaScissor ? source.AlphaScissorThreshold : 0.5f);
		}
		else return false;

		// A profundidade antes da cor: os dois são transparentes, da mesma
		// malha e à mesma distância, e o Godot só os põe em ordem pela prioridade.
		color.RenderPriority = Mathf.Clamp(active.RenderPriority, (int)Material.RenderPriorityMin + 1, (int)Material.RenderPriorityMax);
		depth.RenderPriority = color.RenderPriority - 1;
		depth.NextPass = color;
		depth.SetMeta(Mark, true);

		_surfaces.Add((mesh, surface, mesh.GetSurfaceOverrideMaterial(surface)));
		mesh.SetSurfaceOverrideMaterial(surface, depth);
		return true;
	}

	/// <summary>Um material novo com o shader dado e os parâmetros do lobo.</summary>
	private static ShaderMaterial Copy(ShaderMaterial source, Shader shader)
	{
		var copy = new ShaderMaterial { Shader = shader };
		foreach (Godot.Collections.Dictionary uniform in source.Shader.GetShaderUniformList())
		{
			string name = uniform["name"].AsString();
			Variant value = source.GetShaderParameter(name);
			if (value.VariantType != Variant.Type.Nil) copy.SetShaderParameter(name, value);
		}
		return copy;
	}

	private void Restore()
	{
		foreach (var (mesh, surface, before) in _surfaces)
			if (GodotObject.IsInstanceValid(mesh)) mesh.SetSurfaceOverrideMaterial(surface, before);
		foreach (var (node, before) in _instances)
			if (GodotObject.IsInstanceValid(node)) node.Transparency = before;
		_surfaces.Clear();
		_colors.Clear();
		_wolfColors.Clear();
		_instances.Clear();
	}
}
