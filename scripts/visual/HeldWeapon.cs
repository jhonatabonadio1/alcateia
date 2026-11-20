using Godot;

/// <summary>
/// A arma na mão do corpo em terceira pessoa — a que os outros veem.
///
/// O modelo vem de <see cref="WeaponModel"/> e troca junto com o slot:
/// quem está de AK aparece de AK, quem puxou a faca aparece de faca. O
/// lobo não segura nada (o corpo humano inteiro some).
///
/// **Como a arma fica na mão sem calibrar osso por osso.** As animações
/// são de fuzil: a mão direita no punho, a esquerda embaixo do cano. Cada
/// arma diz, no armas.json ("maos"), onde fica a palma direita (o punho) e
/// a esquerda (o guarda-mão) — os mesmos pontos da primeira pessoa. A arma
/// entra com o punho no centro da palma direita e girada para o
/// guarda-mão cair na linha da palma esquerda (19/11: antes o cano ia do
/// pulso de uma mão ao da outra, e a arma ficava baixa e torta, longe da
/// mão de apoio). Funciona com qualquer clipe e qualquer esqueleto, sem
/// número de rotação por osso.
///
/// Na recarga a mão esquerda sai para buscar o carregador, e apontar
/// para ela viraria a arma para baixo. Então, enquanto a arma está bem
/// segura, guarda-se onde ela fica *em relação à mão direita*; na
/// recarga ela só acompanha a mão direita com essa relação congelada.
/// </summary>
public partial class HeldWeapon : Node3D
{
	private Skeleton3D _skeleton;
	private int _rightHand = -1;
	private int _leftHand = -1;
	private int _rightMiddle = -1, _leftMiddle = -1, _rightIndex = -1, _leftIndex = -1, _rightPinky = -1, _leftPinky = -1;
	private Weapon _weapon;
	private FormController _form;
	private Health _health;
	private CharacterBody3D _body;
	private uint _layers = 1;

	private Node3D _model;
	private Node3D _magazine;
	private string _id = "";
	private Transform3D _inHand = Transform3D.Identity;
	private bool _calibrated;

	/// <summary>Liga a arma ao esqueleto do modelo. Chamado pelo
	/// <see cref="CharacterAnimator"/> depois que o modelo carrega.</summary>
	public void Attach(Node3D model, CharacterBody3D body, uint layers)
	{
		_skeleton = AnimationKit.FindSkeleton(model);
		_body = body;
		_layers = layers;
		_weapon = body.GetNodeOrNull<Weapon>("Weapon");
		_form = body.GetNodeOrNull<FormController>("FormController");
		_health = body.GetNodeOrNull<Health>("Health");

		if (_skeleton == null || _weapon == null) return;

		for (int i = 0; i < _skeleton.GetBoneCount(); i++)
		{
			string bone = _skeleton.GetBoneName(i);
			if (bone.EndsWith("RightHand")) _rightHand = i;
			else if (bone.EndsWith("LeftHand")) _leftHand = i;
			else if (bone.EndsWith("RightHandMiddle1")) _rightMiddle = i;
			else if (bone.EndsWith("LeftHandMiddle1")) _leftMiddle = i;
			else if (bone.EndsWith("RightHandIndex1")) _rightIndex = i;
			else if (bone.EndsWith("LeftHandIndex1")) _leftIndex = i;
			else if (bone.EndsWith("RightHandPinky1")) _rightPinky = i;
			else if (bone.EndsWith("LeftHandPinky1")) _leftPinky = i;
		}

		FindBodyBones();

		if (_rightHand < 0)
		{
			GD.PrintErr("HeldWeapon: esqueleto sem mão direita.");
			return;
		}

		_weapon.SlotChanged += _ => Rebuild();
		_weapon.PickedUp += _ => Rebuild();
		_weapon.Dropped += _ => Rebuild();
		if (_form != null) _form.FormChanged += _ => Rebuild();
		if (_health != null)
		{
			_health.Died += _ => Rebuild();
			_health.Respawned += Rebuild;
		}

		// Depois da árvore de animação, para pegar a pose deste quadro.
		ProcessPriority = 100;
		Rebuild();
	}

	private string WantedId()
	{
		if (_weapon == null || (_form != null && _form.ShowsWolf) || (_health != null && _health.IsDead))
			return "";

		return _weapon.HeldId;
	}

	private void Rebuild()
	{
		string id = WantedId();
		if (id == _id && (_model != null || id == "")) return;

		_model?.QueueFree();
		_model = null;
		_magazine = null;
		_id = id;
		_calibrated = false;

		if (id == "") return;

		ReadHands();
		_model = WeaponModel.Build(id);
		_model.TopLevel = true;
		AddChild(_model);
		ReadShape();
		WeaponModel.SetLayers(_model, _layers);
		_magazine = _model.GetNodeOrNull<Node3D>(WeaponModel.MagazineName);
	}

	public override void _Process(double delta)
	{
		if (_model == null || _skeleton == null || !IsVisibleInTree()) return;

		// No fim do arremesso a mão direita ainda está no ar e o fuzil já é a
		// arma da vez: ele só volta quando o braço termina o gesto (antes
		// balançava pendurado na mão que jogou a granada).
		bool throwing = _id != Arsenal.GrenadeId && GetParent() is CharacterAnimator animator && animator.ThrowingGrenade;
		_model.Visible = !throwing;
		if (throwing) return;

		Transform3D skeleton = _skeleton.GlobalTransform;
		Transform3D hand = skeleton * _skeleton.GetBoneGlobalPose(_rightHand);
		Vector3 right = PalmOf(skeleton, _rightHand, _rightMiddle, _rightIndex, _rightPinky, true);

		bool reloading = _weapon.IsReloading;
		bool aimed = !reloading && _leftHand >= 0 && _id != Arsenal.KnifeId && _id != Arsenal.GrenadeId;

		Transform3D placed;

		if (aimed)
		{
			Vector3 left = PalmOf(skeleton, _leftHand, _leftMiddle, _leftIndex, _leftPinky, false);
			Vector3 axis = left - right;

			// Mãos juntas demais não dão direção; fica a frente do corpo.
			Vector3 toward = axis.Length() > 0.1f ? axis.Normalized() : -_body.GlobalTransform.Basis.Z;
			placed = OnPalms(right, toward);
			placed = OutOfBody(skeleton, placed);
			placed = AwayFromHead(skeleton, placed);

			_inHand = hand.AffineInverse() * placed;
			_calibrated = true;
		}
		else if (_calibrated)
		{
			placed = hand * _inHand;
			placed.Basis = placed.Basis.Orthonormalized();
		}
		else
		{
			placed = OnPalms(right, -_body.GlobalTransform.Basis.Z);
		}

		_model.GlobalTransform = placed;
		if (aimed) ClearHead(skeleton, placed);

		// O carregador some no meio da recarga, como na primeira pessoa.
		if (_magazine != null)
		{
			float p = _weapon.ReloadProgress;
			_magazine.Visible = !(reloading && p > 0.3f && p < 0.65f);
		}
	}

	// ------------------------------------------------------------ sem atravessar o corpo
	//
	// 19/11, Jhon: "armas não podem atravessar o personagem". Os clipes são
	// do X Bot; num corpo de ombros mais altos e braços mais curtos (o Barut)
	// o fuzil chega mais perto, e a coronha entrava no peito e a cabeça
	// inclinada do clipe, na arma. Duas correções, a cada quadro:
	//  * a coronha não entra no tronco: a arma desliza para a frente ao longo
	//    do cano até a ponta de trás ficar fora (a mão escorrega um pouco no
	//    punho, o que não se vê);
	//  * a arma não entra na cabeça: primeiro a arma se afasta dela, de lado
	//    e para baixo, alguns centímetros (a mão envolve o punho, não se
	//    nota); o que faltar, a cabeça e o pescoço sobem um pouco (a mira vem
	//    do corpo, não da cabeça do modelo).

	/// <summary>Raio do tronco (da coluna ao pescoço) que a coronha respeita.</summary>
	[Export] public float TorsoRadius = 0.12f;

	/// <summary>Quanto o alto da arma fica longe do centro da cabeça.</summary>
	[Export] public float HeadClearance = 0.105f;

	/// <summary>Até quanto a arma desliza para a frente, em metros.</summary>
	[Export] public float MaxSlide = 0.14f;

	/// <summary>Até quanto a cabeça sobe, em graus.</summary>
	[Export] public float MaxHeadLift = 12f;

	/// <summary>Até quanto a arma se afasta da cabeça, em metros.</summary>
	[Export] public float MaxShift = 0.045f;

	private int _spine = -1, _neck = -1, _head = -1, _top = -1;
	private Vector3 _butt, _topBack, _topFront;

	private void FindBodyBones()
	{
		for (int i = 0; i < _skeleton.GetBoneCount(); i++)
		{
			string bone = _skeleton.GetBoneName(i);
			if (bone.EndsWith("Spine1")) _spine = i;
			else if (bone.EndsWith("Neck")) _neck = i;
			else if (bone.EndsWith("_Head") || bone == "Head") _head = i;
			else if (bone.EndsWith("HeadTop_End")) _top = i;
		}
	}

	/// <summary>A ponta de trás da coronha e a linha do alto da arma, no
	/// espaço do modelo dela.</summary>
	private void ReadShape()
	{
		Aabb box = WeaponModel.Bounds(_model);
		Vector3 c = box.GetCenter();
		_butt = new Vector3(c.X, c.Y, box.End.Z);
		float top = box.End.Y - box.Size.Y * 0.15f;
		_topBack = new Vector3(c.X, top, box.End.Z);
		_topFront = new Vector3(c.X, top, box.Position.Z);
	}

	private Vector3 Bone(Transform3D skeleton, int bone) => (skeleton * _skeleton.GetBoneGlobalPose(bone)).Origin;

	private Transform3D OutOfBody(Transform3D skeleton, Transform3D placed)
	{
		if (_spine < 0 || _neck < 0) return placed;
		Vector3 a = Bone(skeleton, _spine), b = Bone(skeleton, _neck);
		Vector3 forward = -placed.Basis.Z.Normalized();
		float slid = 0f;
		for (int i = 0; i < 4 && slid < MaxSlide; i++)
		{
			Vector3 butt = placed * _butt;
			float d = butt.DistanceTo(Geometry3D.GetClosestPointToSegment(butt, a, b));
			if (d >= TorsoRadius) break;
			float step = Mathf.Min(TorsoRadius - d + 0.005f, MaxSlide - slid);
			placed.Origin += forward * step;
			slid += step;
		}
		return placed;
	}

	/// <summary>Afasta a arma do centro da cabeça, na perpendicular do cano,
	/// até <see cref="HeadClearance"/> ou <see cref="MaxShift"/>.</summary>
	private Transform3D AwayFromHead(Transform3D skeleton, Transform3D placed)
	{
		if (_head < 0 || _top < 0) return placed;
		Vector3 center = (Bone(skeleton, _head) + Bone(skeleton, _top)) * 0.5f;
		Vector3 back = placed * _topBack, front = placed * _topFront;
		Vector3 near = Geometry3D.GetClosestPointToSegment(center, back, front);
		Vector3 away = near - center;
		float d = away.Length();
		if (d >= HeadClearance || d < 1e-4f) return placed;
		// Só na perpendicular do cano: para a frente ou para trás a mão sairia do punho.
		Vector3 barrel = (front - back).Normalized();
		away -= barrel * away.Dot(barrel);
		if (away.LengthSquared() < 1e-8f) return placed;
		placed.Origin += away.Normalized() * Mathf.Min(HeadClearance - d, MaxShift);
		return placed;
	}

	/// <summary>Sobe a cabeça (metade no pescoço, metade nela) até o alto da
	/// arma passar a <see cref="HeadClearance"/> do centro dela.</summary>
	private void ClearHead(Transform3D skeleton, Transform3D placed)
	{
		if (_head < 0 || _top < 0 || _neck < 0) return;
		Vector3 back = placed * _topBack, front = placed * _topFront;
		Vector3 lateral = _body.GlobalTransform.Basis.X.Normalized();
		float lifted = 0f;
		for (int i = 0; i < 6 && lifted < MaxHeadLift; i++)
		{
			Vector3 center = (Bone(skeleton, _head) + Bone(skeleton, _top)) * 0.5f;
			float d = center.DistanceTo(Geometry3D.GetClosestPointToSegment(center, back, front));
			if (d >= HeadClearance) break;
			float step = Mathf.Min(Mathf.RadToDeg((HeadClearance - d) / 0.2f) + 1f, MaxHeadLift - lifted);
			Lift(skeleton, _neck, lateral, step * 0.5f);
			Lift(skeleton, _head, lateral, step * 0.5f);
			lifted += step;
		}
	}

	/// <summary>Gira o osso para trás, em volta do eixo lateral do corpo
	/// (o alto vai para as costas), por cima da pose deste quadro.</summary>
	private void Lift(Transform3D skeleton, int bone, Vector3 lateral, float degrees)
	{
		Basis global = (skeleton.Basis * _skeleton.GetBoneGlobalPose(bone).Basis).Orthonormalized();
		Vector3 axis = (global.Inverse() * lateral).Normalized();
		// Corpo de frente para −Z: girar em volta da lateral (+X do corpo) por
		// um ângulo positivo leva o alto para trás (+Z).
		Quaternion pose = _skeleton.GetBonePoseRotation(bone);
		_skeleton.SetBonePoseRotation(bone, (pose * new Quaternion(axis, Mathf.DegToRad(degrees))).Normalized());
	}

	/// <summary>Pontos da arma (no espaço do modelo dela) onde vão as palmas:
	/// o punho e o guarda-mão. Do armas.json; sem eles, o punho na origem e o
	/// cano em −Z.</summary>
	private Vector3 _gripPoint, _supportPoint;

	private void ReadHands()
	{
		_gripPoint = WeaponModel.Hand(_id, true).Transform * WeaponModel.Palm;
		_supportPoint = WeaponModel.Hand(_id, false).Transform * WeaponModel.Palm;
		// Pistola (e o que não tem mão de apoio à frente): o cano é a frente.
		if (WeaponModel.IsPistol(_id) || _supportPoint.Z > _gripPoint.Z - 0.08f)
			_supportPoint = _gripPoint + new Vector3(0, 0, -0.3f);
	}

	/// <summary>
	/// A arma com o punho em <paramref name="palm"/> e o guarda-mão na
	/// direção <paramref name="toward"/> (a da palma esquerda). O alto da
	/// arma fica para cima — ou, com o cano quase na vertical (a recarga
	/// de alguns clipes), para as costas do corpo.
	/// </summary>
	private Transform3D OnPalms(Vector3 palm, Vector3 toward)
	{
		Vector3 up = Vector3.Up;
		if (Mathf.Abs(toward.Dot(up)) > 0.95f) up = _body.GlobalTransform.Basis.Z;

		// No modelo: do punho ao guarda-mão, e o alto da arma (+Y).
		Vector3 along = (_supportPoint - _gripPoint).Normalized();
		Basis model = Frame(along, Vector3.Up);
		Basis world = Frame(toward, up);
		Basis basis = world * model.Transposed();
		return new Transform3D(basis, palm - basis * _gripPoint);
	}

	/// <summary>Base ortonormal com X ao longo de <paramref name="along"/> e Y o
	/// mais perto possível de <paramref name="up"/>.</summary>
	private static Basis Frame(Vector3 along, Vector3 up)
	{
		Vector3 x = along.Normalized();
		Vector3 z = x.Cross(up).Normalized();
		Vector3 y = z.Cross(x);
		return new Basis(x, y, z);
	}

	/// <summary>
	/// O centro da palma de uma mão, no mundo: do pulso (o osso da mão) até
	/// o começo do dedo médio, um pouco mais da metade, e para dentro da mão.
	/// Sem os dedos no esqueleto, o pulso mais um palmo ao longo da mão.
	/// </summary>
	private Vector3 PalmOf(Transform3D skeleton, int wrist, int middle, int index, int pinky, bool right)
	{
		Vector3 w = (skeleton * _skeleton.GetBoneGlobalPose(wrist)).Origin;
		if (middle < 0)
			return (skeleton * _skeleton.GetBoneGlobalPose(wrist)) * new Vector3(0, PalmReach, 0);

		Vector3 m = (skeleton * _skeleton.GetBoneGlobalPose(middle)).Origin;
		Vector3 palm = w.Lerp(m, 0.55f);
		if (index >= 0 && pinky >= 0)
		{
			Vector3 across = (skeleton * _skeleton.GetBoneGlobalPose(index)).Origin
				- (skeleton * _skeleton.GetBoneGlobalPose(pinky)).Origin;
			Vector3 inside = (right ? across.Cross(m - w) : (m - w).Cross(across)).Normalized();
			palm += inside * PalmDepth;
		}
		return palm;
	}

	/// <summary>Quanto o centro da palma fica para dentro da mão, saindo do
	/// plano dos ossos.</summary>
	[Export] public float PalmDepth = 0.02f;

	/// <summary>Do pulso ao centro da palma, quando o esqueleto não tem dedos.</summary>
	[Export] public float PalmReach = 0.08f;
}
