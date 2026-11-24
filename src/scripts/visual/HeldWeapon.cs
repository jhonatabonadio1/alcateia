using Godot;
using System.Collections.Generic;

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
///
/// **Pistola, faca e granada: as mãos vão até a arma** (21/11, Jhon: "as
/// armas, granadas, pistola, faca não estão posicionadas corretamente nas
/// mãos dos personagens… o boneco precisa segurar igual é na primeira
/// pessoa"). Os clipes são todos de fuzil: a pistola saía de lado, entre
/// as duas mãos, e a faca de pé, com a lâmina para cima. Agora essas três
/// ficam numa pose de mira (na frente dos olhos, para onde a cabeça olha) e
/// os braços vão até elas (<see cref="ArmIK"/>), com a palma e os dedos
/// nos pontos do armas.json — os da primeira pessoa. Na pistola as duas
/// mãos; na faca e na granada a direita, e a esquerda em guarda. A faca
/// estoca e puxa na diagonal, alternando, como a da primeira pessoa.
///
/// **O tronco segue a mira** (<see cref="AimWithBody"/>): o corpo se inclina
/// para cima e para baixo com a cabeça, e o fuzil vai junto — antes o
/// boneco apontava sempre reto, olhasse para onde olhasse.
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
			else if (bone.EndsWith("RightHandMiddle1") || bone == "middle_01_r") _rightMiddle = i;
			else if (bone.EndsWith("LeftHandMiddle1") || bone == "middle_01_l") _leftMiddle = i;
			else if (bone.EndsWith("RightHandIndex1") || bone == "index_01_r") _rightIndex = i;
			else if (bone.EndsWith("LeftHandIndex1") || bone == "index_01_l") _leftIndex = i;
			else if (bone.EndsWith("RightHandPinky1") || bone == "pinky_01_r") _rightPinky = i;
			else if (bone.EndsWith("LeftHandPinky1") || bone == "pinky_01_l") _leftPinky = i;
		}

		FindBodyBones();
		_poses = HoldPoses.For(PosesPath);

		if (_rightHand < 0)
		{
			GD.PrintErr("HeldWeapon: skeleton has no right hand.");
			return;
		}

		_weapon.SlotChanged += _ => Rebuild();
		_weapon.Fired += OnFired;
		_weapon.Stabbed += OnStabbed;
		_weapon.Thrown += () => _rig?.Thrown();
		_weapon.AmmoChanged += OnAmmo;
		FindFingers();
		_bodyHead = body.GetNodeOrNull<Node3D>("Head");
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

	/// <summary>A arma é solta (<c>TopLevel</c>, posta no mundo a cada quadro): o alisamento do
	/// desenho do corpo (<see cref="VisualSmoothing"/>) a leva junto com o modelo, senão as mãos
	/// ficam um passo da física atrás dela.
	/// Pedido no quadro (não ao montar a arma): o corpo monta o alisamento depois do modelo.</summary>
	private void FollowBody()
	{
		if (_model == null || _following == _model) return;
		if (_body?.GetNodeOrNull<VisualSmoothing>("VisualSmoothing") is not VisualSmoothing smoothing) return;
		smoothing.Follow(_model);
		_following = _model;
	}

	private Node3D _following;

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

		// O lance da granada termina antes de a arma seguinte aparecer (como
		// na primeira pessoa). Morreu ou virou lobo: sai já.
		if (_rig != null && _rig.Throwing && id != "")
		{
			_rebuildAfterThrow = true;
			return;
		}
		_rebuildAfterThrow = false;

		_model?.QueueFree();
		_model = null;
		_rig = null;
		_clipSet = null;
		_rigReloading = false;
		_magazine = null;
		_id = id;
		_calibrated = false;

		if (id == "") return;

		// A arma com conjunto de primeira pessoa vem dele: o mesmo modelo, a
		// mesma animação, as mãos e os dedos no lugar (HeldRig).
		_rig = UseFirstPersonRig ? HeldRig.Create(id, _layers) : null;
		if (_rig != null)
		{
			_model = _rig;
			AddChild(_rig);
			_rig.Start();
			_clipSet = UseClips ? HoldClips.For(HoldClips.PathFor(ClipsModel, id)) : null;
			MapClipBones();
			_clipFade = 0f;
			return;
		}
		_clipSet = null;

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
		if (_skeleton == null) return;

		// O que este nó mexeu nos ossos no quadro passado e a animação não
		// reescreveu volta ao que era: nada se soma de um quadro para o outro.
		RestorePose();
		if (_model == null || !IsVisibleInTree()) return;
		FollowBody();
		_stab += delta;

		Transform3D skeleton = _skeleton.GlobalTransform;
		if (_rebuildAfterThrow && (_rig == null || !_rig.Throwing)) Rebuild();

		// As animações feitas no Blender a partir das da primeira pessoa
		// (22/11): o corpo da cintura para cima e a arma, quadro a quadro.
		if (_rig != null && _clipSet != null)
		{
			HoldByClips(delta);
			return;
		}

		// A pose do Blender para esta arma (o corpo da cintura para cima) e o
		// conjunto da primeira pessoa posto pela mão direita dela.
		if (_rig != null && PoseFor(_id) is HoldPose pose)
		{
			HoldByPose(delta, pose);
			return;
		}

		BendToAim(skeleton);

		// A arma do conjunto de primeira pessoa: as mãos vão até ela.
		if (_rig != null)
		{
			HoldByRig(delta);
			return;
		}

		// No fim do arremesso a mão direita ainda está no ar e o fuzil já é a
		// arma da vez: ele só volta quando o braço termina o gesto (antes
		// balançava pendurado na mão que jogou a granada).
		bool throwing = !Arsenal.IsGrenade(_id) && GetParent() is CharacterAnimator animator && animator.ThrowingGrenade;
		_model.Visible = !throwing;
		if (throwing) return;

		// Pistola, faca e granada: as mãos vão até a arma.
		if (HandsOnWeapon && HoldByHands(skeleton)) return;
		Transform3D hand = skeleton * _skeleton.GetBoneGlobalPose(_rightHand);
		Vector3 right = PalmOf(skeleton, _rightHand, _rightMiddle, _rightIndex, _rightPinky, true);

		bool reloading = _weapon.IsReloading;
		bool aimed = !reloading && _leftHand >= 0 && !Arsenal.IsKnife(_id) && !Arsenal.IsGrenade(_id);

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

		// O carregador some no meio da recarga, como na primeira pessoa.
		if (_magazine != null)
		{
			float p = _weapon.ReloadProgress;
			_magazine.Visible = !(reloading && p > 0.3f && p < 0.65f);
		}
	}

	// ------------------------------------------------------------ como na primeira pessoa (21/11)

	/// <summary>O tronco inclina com a mira (para cima e para baixo).</summary>
	[ExportGroup("Terceira pessoa")]
	[Export] public bool AimWithBody = true;

	/// <summary>Quanto da inclinação da cabeça o tronco acompanha.</summary>
	[Export(PropertyHint.Range, "0,1")] public float AimPitchShare = 0.85f;

	/// <summary>Até quantos graus o tronco inclina.</summary>
	[Export] public float AimPitchLimit = 55f;

	/// <summary>Pistola, faca e granada nas mãos pela pose de mira (desligado,
	/// voltam a ir só na mão direita do clipe de fuzil).</summary>
	[Export] public bool HandsOnWeapon = true;

	/// <summary>Onde a palma direita fica, em metros, a partir do meio da
	/// cabeça e no sentido da mira (x à direita, y para cima, −z à frente).
	/// Pistola: braços esticados, na linha dos olhos, um pouco abaixo.</summary>
	[Export] public Vector3 PistolHold = new(0.05f, -0.17f, -0.42f);

	/// <summary>A faca, baixa e à frente, com a lâmina para a frente.</summary>
	[Export] public Vector3 KnifeHold = new(0.2f, -0.34f, -0.3f);

	/// <summary>A granada na mão, antes de armar.</summary>
	[Export] public Vector3 GrenadeHold = new(0.2f, -0.38f, -0.19f);

	/// <summary>A mão esquerda em guarda, quando só a direita segura.</summary>
	[Export] public Vector3 GuardHold = new(-0.14f, -0.4f, -0.22f);

	/// <summary>O giro da faca em torno do lado, em graus: a lâmina (que no
	/// modelo é +Y) vai para a frente e um pouco para cima.</summary>
	[Export] public float KnifeTilt = -62f;

	/// <summary>O golpe da faca: quanto dura e até onde vai a estocada.</summary>
	[Export] public float StabTime = 0.3f;
	[Export] public float StabReach = 0.34f;

	private Node3D _bodyHead;
	private readonly System.Collections.Generic.List<int> _spines = new();
	private int _rightUpper = -1, _rightLower = -1, _leftUpper = -1, _leftLower = -1;
	private double _stab = 99;
	private int _stabKind = 1;

	/// <summary>O golpe da faca: a próxima é a outra (estocada, puxada).</summary>
	private void OnFired()
	{
		_rig?.Fired(Arsenal.IsKnife(_id), _weapon.KnifeCooldown);
		if (!Arsenal.IsKnife(_id)) return;
		_stab = 0;
		_stabKind = (_stabKind + 1) % 2;
	}

	/// <summary>A estocada da faca (botão direito, 24/11): o conjunto toca o
	/// clipe "estocada" e o corpo segue o dele (animacoes/faca.json). Sem o
	/// clipe, o golpe de código.</summary>
	private void OnStabbed()
	{
		if (_rig == null || !_rig.Has("estocada"))
		{
			OnFired();
			return;
		}
		_rig.Hold(_rig.Play("estocada", 0, 0.05));
	}

	// ---- os ossos mexidos aqui: o que eram antes e o que ficou

	private readonly System.Collections.Generic.Dictionary<int, (Quaternion Before, Quaternion Written)> _posed = new();

	private void RestorePose()
	{
		foreach (var (bone, pose) in _posed)
			if (_skeleton.GetBonePoseRotation(bone).IsEqualApprox(pose.Written))
				_skeleton.SetBonePoseRotation(bone, pose.Before);
		_posed.Clear();
		foreach (var (bone, pose) in _posedPosition)
			if (_skeleton.GetBonePosePosition(bone).IsEqualApprox(pose.Written))
				_skeleton.SetBonePosePosition(bone, pose.Before);
		_posedPosition.Clear();
	}

	private readonly Dictionary<int, (Vector3 Before, Vector3 Written)> _posedPosition = new();

	private void WritePosition(int bone, Vector3 position)
	{
		Vector3 before = _posedPosition.TryGetValue(bone, out var was) ? was.Before : _skeleton.GetBonePosePosition(bone);
		_skeleton.SetBonePosePosition(bone, position);
		_posedPosition[bone] = (before, position);
	}

	private void Write(int bone, Quaternion rotation)
	{
		Quaternion before = _posed.TryGetValue(bone, out var was) ? was.Before : _skeleton.GetBonePoseRotation(bone);
		_skeleton.SetBonePoseRotation(bone, rotation);
		_posed[bone] = (before, rotation);
	}

	/// <summary>
	/// O tronco inclina com a cabeça, repartido pelos ossos da coluna: o
	/// peito, os ombros, os braços e a arma sobem e descem juntos. Cada osso
	/// gira em torno do lado do corpo, no espaço do pai.
	/// </summary>
	private void BendToAim(Transform3D skeleton)
	{
		if (!AimWithBody || _spines.Count == 0 || _bodyHead == null) return;
		if (_health != null && _health.IsDead) return;

		float limit = Mathf.DegToRad(AimPitchLimit);
		float pitch = Mathf.Clamp(_bodyHead.Rotation.X * AimPitchShare, -limit, limit);
		if (Mathf.Abs(pitch) < 0.002f) return;

		Vector3 side = (skeleton.Basis.Inverse() * _body.GlobalBasis.X).Normalized();
		float share = pitch / _spines.Count;

		// O pai de cada um como estava antes de qualquer giro: giros em torno
		// do mesmo eixo se somam na ordem que for.
		var parents = new Basis[_spines.Count];
		for (int i = 0; i < _spines.Count; i++)
		{
			int parent = _skeleton.GetBoneParent(_spines[i]);
			parents[i] = parent >= 0 ? _skeleton.GetBoneGlobalPose(parent).Basis.Orthonormalized() : Basis.Identity;
		}

		for (int i = 0; i < _spines.Count; i++)
		{
			int bone = _spines[i];
			Vector3 axis = (parents[i].Transposed() * side).Normalized();
			Write(bone, new Quaternion(axis, share) * _skeleton.GetBonePoseRotation(bone));
		}
	}

	/// <summary>O meio da cabeça e o sentido da mira: a frente do corpo,
	/// inclinada como a cabeça.</summary>
	private Transform3D AimFrame(Transform3D skeleton)
	{
		Vector3 eye = _head >= 0 && _top >= 0 ? (Bone(skeleton, _head) + Bone(skeleton, _top)) * 0.5f
			: _head >= 0 ? Bone(skeleton, _head) : _body.GlobalPosition + Vector3.Up * 0.6f;
		Basis yaw = _body.GlobalBasis.Orthonormalized();
		float pitch = Mathf.Clamp(_bodyHead?.Rotation.X ?? 0f, -1.45f, 1.45f);
		return new Transform3D(new Basis(yaw.X, pitch) * yaw, eye);
	}

	/// <summary>A arma com a palma direita em <paramref name="hold"/> (no
	/// sentido da mira) e girada por <paramref name="turn"/>.</summary>
	private Transform3D HoldAt(Transform3D aim, Vector3 hold, Basis turn)
	{
		Basis basis = aim.Basis * turn;
		return new Transform3D(basis, aim * hold - basis * _gripPoint);
	}

	/// <summary>Pistola, faca ou granada nas mãos. Falso para o resto (o fuzil
	/// fica como sempre, na linha das mãos do clipe).</summary>
	private bool HoldByHands(Transform3D skeleton)
	{
		bool pistol = WeaponModel.IsPistol(_id), knife = Arsenal.IsKnife(_id), grenade = Arsenal.IsGrenade(_id);
		if (!pistol && !knife && !grenade) return false;
		if (_rightUpper < 0 || _rightLower < 0 || _rightHand < 0) return false;
		// Armando e lançando a granada, o braço é o do clipe.
		if (grenade && GetParent() is CharacterAnimator { ThrowingGrenade: true }) return false;

		Transform3D aim = AimFrame(skeleton);
		Transform3D placed = pistol ? HoldAt(aim, PistolHold, Basis.Identity)
			: knife ? HoldAt(aim, KnifeHold + StabOffset(), new Basis(Vector3.Right, Mathf.DegToRad(KnifeTilt)))
			: HoldAt(aim, GrenadeHold, Basis.Identity);

		if (!Grip(placed, true, out Vector3 missed)) return false;
		// Longe demais para o braço: a arma vem até a mão, não fica no ar.
		placed.Origin += missed;

		if (pistol && !_weapon.IsReloading && _leftUpper >= 0 && _leftLower >= 0 && _leftHand >= 0)
			Grip(placed, false, out _);
		else if (!pistol && _leftUpper >= 0 && _leftLower >= 0 && _leftHand >= 0)
			ArmIK.Reach(_skeleton, _leftUpper, _leftLower, _leftHand, aim * GuardHold, null, Elbow(false), Write);

		_model.GlobalTransform = placed;
		_calibrated = false;

		// O carregador da pistola some no meio da recarga, como no fuzil.
		if (_magazine != null)
		{
			float p = _weapon.ReloadProgress;
			_magazine.Visible = !(_weapon.IsReloading && p > 0.3f && p < 0.65f);
		}
		return true;
	}

	/// <summary>
	/// Uma mão na arma: a palma no ponto do armas.json, os dedos e a palma
	/// virados como lá (a mão da primeira pessoa tem os dedos em −Z e a palma
	/// em −Y). <paramref name="missed"/> é quanto o pulso ficou longe do
	/// ponto (braço curto demais).
	/// </summary>
	private bool Grip(Transform3D weapon, bool right, out Vector3 missed)
	{
		missed = Vector3.Zero;
		Transform3D skeleton = _skeleton.GlobalTransform;
		if (!HandFrame(skeleton, right, out Vector3 fingers, out Vector3 palm, out Vector3 offset)) return false;

		Transform3D place = weapon * WeaponModel.Hand(_id, right).Transform;
		Vector3 point = place * WeaponModel.Palm;
		Basis rotation = ArmIK.Orient(-place.Basis.Z, -place.Basis.Y) * ArmIK.Orient(fingers, palm).Transposed();
		Vector3 target = point - rotation * offset;

		Vector3 reached = right
			? ArmIK.Reach(_skeleton, _rightUpper, _rightLower, _rightHand, target, rotation, Elbow(true), Write)
			: ArmIK.Reach(_skeleton, _leftUpper, _leftLower, _leftHand, target, rotation, Elbow(false), Write);
		missed = reached - target;
		return true;
	}

	/// <summary>Para onde o cotovelo aponta: para baixo, para fora e um
	/// pouco para trás.</summary>
	private Vector3 Elbow(bool right)
	{
		int shoulder = right ? _rightUpper : _leftUpper;
		Basis body = _body.GlobalBasis.Orthonormalized();
		Vector3 at = Bone(_skeleton.GlobalTransform, shoulder);
		return at + Vector3.Down * 0.4f + body.X * (right ? 0.3f : -0.3f) + body.Z * 0.12f;
	}

	/// <summary>Os dedos, a palma e o centro da palma da mão do corpo, na
	/// rotação do osso da mão (não mudam com a pose: são os começos dos dedos).</summary>
	private bool HandFrame(Transform3D skeleton, bool right, out Vector3 fingers, out Vector3 palm, out Vector3 offset)
	{
		fingers = palm = offset = Vector3.Zero;
		int wrist = right ? _rightHand : _leftHand;
		int middle = right ? _rightMiddle : _leftMiddle, index = right ? _rightIndex : _leftIndex, pinky = right ? _rightPinky : _leftPinky;
		if (wrist < 0 || middle < 0 || index < 0 || pinky < 0) return false;

		Transform3D pose = skeleton * _skeleton.GetBoneGlobalPose(wrist);
		Basis back = pose.Basis.Orthonormalized().Transposed();
		Vector3 along = Bone(skeleton, middle) - pose.Origin;
		Vector3 across = Bone(skeleton, index) - Bone(skeleton, pinky);
		Vector3 inside = right ? across.Cross(along) : along.Cross(across);
		if (along.LengthSquared() < 1e-8f || inside.LengthSquared() < 1e-10f) return false;

		fingers = (back * along).Normalized();
		palm = (back * inside).Normalized();
		offset = back * (PalmOf(skeleton, wrist, middle, index, pinky, right) - pose.Origin);
		return true;
	}

	/// <summary>
	/// O golpe da faca, no sentido da mira: a estocada vai à frente e volta;
	/// a puxada desce na diagonal, de cima à direita para baixo à esquerda —
	/// os dois golpes da primeira pessoa.
	/// </summary>
	private Vector3 StabOffset()
	{
		if (_stab >= StabTime) return Vector3.Zero;
		float u = (float)(_stab / StabTime);
		float out_ = Mathf.Sin(Mathf.Pi * u);
		if (_stabKind == 0) return new Vector3(-0.04f, 0.06f, -StabReach) * out_;
		return new Vector3(Mathf.Lerp(0.15f, -0.28f, u), Mathf.Lerp(0.18f, -0.12f, u), -0.18f) * out_;
	}

	// ------------------------------------------------------------ o conjunto da primeira pessoa (21/11)

	/// <summary>Usar o conjunto de primeira pessoa como referência (desligado,
	/// volta a arma de sempre, nas mãos do clipe de fuzil).</summary>
	[ExportGroup("Terceira pessoa: conjunto")]
	[Export] public bool UseFirstPersonRig = true;

	/// <summary>
	/// Onde o conjunto fica em relação ao olho, em metros (x à direita, y
	/// para cima, −z à frente). Na primeira pessoa a arma fica colada no olho
	/// (a câmera não vê o ombro nem a cabeça); no corpo, o conjunto desce e
	/// vai para a frente até a arma encostar no ombro sem entrar no rosto.
	/// </summary>
	[Export] public Vector3 RigRifle = new(0f, -0.1f, 0f);
	[Export] public Vector3 RigPistol = new(0f, -0.12f, -0.16f);
	[Export] public Vector3 RigKnife = new(0f, -0.16f, -0.02f);
	[Export] public Vector3 RigGrenade = new(0f, -0.15f, -0.08f);

	/// <summary>Quanto do giro da mão vai para o antebraço (o pulso não torce).</summary>
	[Export(PropertyHint.Range, "0,1")] public float ForearmTwist = 0.5f;

	/// <summary>Que parte da mão do corpo encosta onde a do conjunto encosta:
	/// 0 é o pulso, 1 o começo dos dedos. Perto dos dedos, a mão maior do
	/// personagem fecha na mesma empunhadura.</summary>
	[Export(PropertyHint.Range, "0,1")] public float KnuckleAlign = 0.8f;

	private HeldRig _rig;
	private bool _rigReloading, _rebuildAfterThrow;

	/// <summary>Os braços do corpo seguem o conjunto (a recarga e a granada do
	/// clipe de fuzil não tocam por cima: CharacterAnimator).</summary>
	public bool ArmsFromRig => _rig != null;

	/// <summary>Os dedos do corpo: [dedo][1..4] (polegar, indicador, médio, anelar, mínimo).</summary>
	private readonly int[][] _fingersR = new int[5][], _fingersL = new int[5][];
	private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

	private void FindFingers()
	{
		for (int f = 0; f < 5; f++)
		{
			_fingersR[f] = FingerChain("Right", Fingers[f]);
			_fingersL[f] = FingerChain("Left", Fingers[f]);
		}
	}

	private int[] FingerChain(string side, string finger)
	{
		var chain = new int[4];
		for (int n = 0; n < 4; n++)
		{
			chain[n] = -1;
		{
			// os dedos têm os nomes do Mixamo ("RightHandIndex1") ou os do Manny ("index_01_r")
			string manny = $"{finger.ToLowerInvariant()}_0{n + 1}_{(side == "Right" ? 'r' : 'l')}";
			for (int i = 0; i < _skeleton.GetBoneCount(); i++)
			{
				string name = _skeleton.GetBoneName(i);
				if (name == manny || name.EndsWith($"{side}Hand{finger}{n + 1}")) { chain[n] = i; break; }
			}
		}
		}
		return chain[0] < 0 || chain[1] < 0 || chain[2] < 0 ? null : chain;
	}

	private void OnAmmo(int inMag, int magSize, bool reloading)
	{
		if (_rig == null) return;
		if (reloading && !_rigReloading)
		{
			_rigReloading = true;
			bool empty = inMag <= 0;
			float seconds = !_weapon.Puppet && _weapon.ReloadDuration > 0.2f ? _weapon.ReloadDuration
				: _weapon.Held is WeaponDefinition def ? (empty ? def.ReloadEmpty : def.ReloadTactical) : 0f;
			_rig.Reload(empty, seconds);
		}
		else if (!reloading && _rigReloading)
		{
			_rigReloading = false;
			if (inMag < magSize) _rig.StopReload();
		}
	}

	/// <summary>
	/// A arma do conjunto, onde o conjunto põe, e o corpo segurando: o
	/// conjunto vai para a frente do olho (na pose da mira), as mãos vão onde
	/// as dele estão — o punho girado como lá, cada dedo fechado até a ponta
	/// chegar onde a de lá chega — e o cotovelo e o ombro são os do corpo.
	///
	/// O punho não entorta (Jhon, 21/11: "mas não ficar torto"): o cotovelo
	/// vai para trás da mão, na linha dela, e o que sobrar de dobra além de
	/// <see cref="WristBend"/> a mão desfaz. A arma de uma mão só (a pistola,
	/// a faca, a granada) vai junto com a mão: ela nunca sai da mão. A
	/// granada cresce com a mão do personagem (a dele é maior que a do
	/// conjunto) e encosta na palma. A mão que não segura nada (a esquerda,
	/// com a granada ou com a faca, quando a do conjunto está fora da tela)
	/// fica solta ao lado do corpo.
	/// </summary>
	private void HoldByRig(double delta)
	{
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		_rig.Tick(delta, _weapon.IsGrenadeReady, _body.IsOnFloor() && flat.Length() > 0.5f);

		Transform3D skeleton = _skeleton.GlobalTransform;
		Transform3D aim = AimFrame(skeleton);
		bool knife = Arsenal.IsKnife(_id), grenade = Arsenal.IsGrenade(_id), pistol = WeaponModel.IsPistol(_id);
		bool oneHand = knife || grenade || pistol;
		Vector3 offset = knife ? RigKnife : grenade ? RigGrenade : pistol ? RigPistol : RigRifle;
		_rig.GlobalTransform = new Transform3D(aim.Basis, aim * offset);

		if (_rightUpper < 0 || _rightLower < 0 || _rightHand < 0) return;
		if (!RigTarget(true, out Vector3 rightWrist, out Basis rightTurn, out HandShape rightShape)) return;
		Vector3 leftWrist = Vector3.Zero;
		Basis leftTurn = Basis.Identity;
		HandShape leftShape = default;
		bool left = _leftUpper >= 0 && _leftLower >= 0 && _leftHand >= 0 && RigTarget(false, out leftWrist, out leftTurn, out leftShape);

		// A mão esquerda solta: a do conjunto está fora da tela (abaixo do
		// quadro da primeira pessoa), e lá ela não foi feita para ser vista.
		// Com a faca, a esquerda fica sempre solta: a guarda da primeira
		// pessoa (a mão aberta na frente da tela), no corpo, virava uma garra.
		float loose = 0f;
		if (left && knife) loose = 1f;
		else if (left && grenade)
		{
			Vector3 shoulder = Bone(skeleton, _leftUpper);
			float below = aim.Basis.Y.Dot(shoulder - leftWrist);
			loose = Mathf.Clamp((below - LooseFrom) / 0.12f, 0f, 1f);
		}

		Vector3 shift = Shortfall(_rightUpper, _rightLower, _rightHand, rightWrist);
		if (left && loose < 1f && !oneHand) shift += Shortfall(_leftUpper, _leftLower, _leftHand, leftWrist + shift);
		if (shift.LengthSquared() > 1e-8f)
		{
			_rig.GlobalPosition += shift;
			rightWrist += shift;
			leftWrist += shift;
		}

		// A direita: na arma (de duas mãos) ou com ela (de uma mão).
		Basis rightFinal = Arm(true, rightWrist, rightTurn, rightShape, oneHand ? WristBend : 180f);
		Transform3D carry = Transform3D.Identity;
		float ball = 0f;
		Vector3 ballAt = Vector3.Zero;
		if (oneHand)
		{
			float grow = grenade ? Mathf.Clamp(rightShape.Ratio * GrenadeGrow, 1f, 1.8f) : 1f;
			carry = rightShape.Carry(rightWrist, rightFinal, grow);
			// A granada na palma: o meio dela a um raio da palma, e os dedos
			// fecham em volta (Jhon, 21/11: "granada não encostando na mão").
			if (grenade && _rig.HasCenter && Palm(true, out Vector3 palm, out Vector3 inside))
			{
				ball = GrenadeRadius * grow;
				ballAt = palm + inside * ball;
				carry = new Transform3D(Basis.Identity, ballAt - carry * _rig.Center()) * carry;
			}
			_rig.Carry(carry);
		}

		if (left)
		{
			if (pistol)
			{
				// As duas mãos na pistola: a esquerda vai com ela.
				leftWrist = carry * leftWrist;
				leftTurn = carry.Basis.Orthonormalized() * leftTurn;
				Arm(false, leftWrist, leftTurn, leftShape, 180f);
			}
			else
			{
				if (loose > 0f)
				{
					Loose(out Vector3 restWrist, out Basis restTurn);
					leftWrist = leftWrist.Lerp(restWrist, loose);
					leftTurn = new Basis(leftTurn.GetRotationQuaternion().Slerp(restTurn.GetRotationQuaternion(), loose));
				}
				Arm(false, leftWrist, leftTurn, leftShape, oneHand ? WristBend : 180f);
			}
		}

		// Na pistola, os polegares vão para a frente, deitados na arma (o do
		// personagem, mais comprido, ficava em pé em cima do ferrolho).
		Vector3 thumbs = pistol ? _rig.GunForward() : Vector3.Zero;
		RigFingers(true, oneHand ? carry : Transform3D.Identity, 1f, 0f, thumbs, ballAt, ball);
		if (left) RigFingers(false, pistol ? carry : Transform3D.Identity, 1f - loose, oneHand ? 0f : SupportCurl, thumbs);
		_rig.FollowMuzzle();
		_calibrated = false;
	}

	// ------------------------------------------------------------ as animações do Blender (22/11, à tarde)

	/// <summary>O modelo do personagem (as animações ficam na pasta
	/// <c>animacoes/</c> ao lado dele); o CharacterAnimator põe antes do
	/// Attach.</summary>
	public string ClipsModel;

	/// <summary>Usar as animações do Blender quando o personagem tem (desligado,
	/// volta a pose de 22/11 de manhã).</summary>
	[Export] public bool UseClips = true;

	private HoldClipSet _clipSet;
	private int[] _clipBones = System.Array.Empty<int>();
	private int _spine2 = -1;
	private float _clipFade;

	/// <summary>As animações da arma na mão agora (null: não tem).</summary>
	public HoldClipSet Clips => _clipSet;

	private int[] _clipPosBones = System.Array.Empty<int>();

	private void MapClipBones()
	{
		if (_clipSet == null) return;
		_clipPosBones = new int[_clipSet.PosNames.Length];
		for (int i = 0; i < _clipPosBones.Length; i++)
			_clipPosBones[i] = _boneByName.TryGetValue(_clipSet.PosNames[i], out int pb) ? pb : -1;
		_clipBones = new int[_clipSet.BoneNames.Length];
		for (int i = 0; i < _clipBones.Length; i++)
			_clipBones[i] = _boneByName.TryGetValue(_clipSet.BoneNames[i], out int bone) ? bone : -1;
		_spine2 = _boneByName.TryGetValue("Spine2", out int chest) ? chest : -1;
	}

	/// <summary>
	/// A arma com as animações feitas no Blender a partir das da primeira
	/// pessoa (Jhon, 22/11: "vamos copiar exatamente as animações dos
	/// braços, tronco e etc e vamos adaptá-las nos personagens"): a
	/// animação de terceira pessoa toca junto com a da arma (o mesmo clipe,
	/// no mesmo instante, misturando na troca como a da arma mistura), o
	/// peito inclina para a mira e o conjunto vai onde o clipe diz, em
	/// relação ao peito. A mão na arma, o dedo no gatilho, o carregador na
	/// mão na recarga e a mira no olho vêm do clipe.
	/// </summary>
	private void HoldByClips(double delta)
	{
		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		bool moving = _body.IsOnFloor() && flat.Length() > 0.5f;
		bool aiming = _weapon.IsAiming && !_weapon.IsReloading;
		_rig.Tick(delta, _weapon.IsGrenadeReady, moving, aiming);

		HoldClip now = _clipSet.Get(_rig.Playing) ?? _clipSet.Get("parado");
		if (now == null || _spine2 < 0) return;
		double at = _rig.Playing == now.Name ? _rig.ClipTime : 0;
		float w = _rig.BlendWeight;
		HoldClip before = w < 1f ? _clipSet.Get(_rig.PrevPlaying) : null;
		_clipFade = Mathf.Min(1f, _clipFade + (float)delta / Mathf.Max(PoseBlendTime, 0.01f));
		float fade = _clipFade * _clipFade * (3f - 2f * _clipFade);

		Transform3D rig = now.RigAt(at);
		if (before != null) rig = HoldClips.Blend(before.RigAt(_rig.PrevTime), rig, w);
		// A arma do tamanho da mão do personagem: cresce em volta da origem
		// do conjunto (o olho da primeira pessoa), como no Blender.
		if (!Mathf.IsEqualApprox(_clipSet.Scale, 1f))
			rig *= new Transform3D(Basis.Identity.Scaled(Vector3.One * _clipSet.Scale), Vector3.Zero);

		// Na troca de arma o corpo leva PoseBlendTime para chegar à pose do clipe, e a arma nova ia
		// direto para o fim do caminho: a faca e a granada apareciam fora da mão, perto da coxa, e
		// depois a mão ia até elas (Jhon, 24/11: "ela surge fora da mão e depois se desloca para
		// mão"). Enquanto o corpo chega, a arma vai presa na mão direita, onde o clipe a põe nela.
		Transform3D placed;
		if (fade < 1f && _rightHand >= 0)
		{
			PoseClip(now, at, before, w, 1f);
			Transform3D inHand = _skeleton.GetBoneGlobalPose(_rightHand).AffineInverse() * _skeleton.GetBoneGlobalPose(_spine2) * rig;
			RestorePose();
			PoseClip(now, at, before, w, fade);
			placed = _skeleton.GetBoneGlobalPose(_rightHand) * inHand;
		}
		else
		{
			PoseClip(now, at, before, w, fade);
			placed = _skeleton.GetBoneGlobalPose(_spine2) * rig;
		}
		_rig.GlobalTransform = _skeleton.GlobalTransform * placed;
		// No saque a arma nova sai do bolso, da bainha ou de trás, onde o clipe começa: ela só
		// aparece quando a mão chega lá (Jhon, 24/11: "ela precisa sair do bolso"). Antes, presa
		// na mão, a granada descia do peito até o bolso para depois subir.
		_rig.Visible = !(now.Name == "sacar" && fade < DrawShowAt);
		_rig.FollowMuzzle();
		_calibrated = false;
		GrowGrenade();
		SwapMagazine(now, at);
		PullPin(now, at);
	}

	/// <summary>Na troca de arma, quanto do caminho até a pose do saque (0 a 1) a mão anda antes de
	/// a arma nova aparecer nela.</summary>
	[Export(PropertyHint.Range, "0,1")] public float DrawShowAt = 0.85f;

	/// <summary>Os ossos do clipe no corpo, misturados com o clipe de antes (<paramref name="w"/>) e
	/// com a pose de agora do corpo (<paramref name="fade"/>, a troca de arma); o peito como no
	/// Blender e inclinado para a mira.</summary>
	private void PoseClip(HoldClip now, double at, HoldClip before, float w, float fade)
	{
		for (int i = 0; i < _clipBones.Length; i++)
		{
			int bone = _clipBones[i];
			if (bone < 0) continue;
			Quaternion q = now.Bone(i, at);
			if (before != null) q = before.Bone(i, _rig.PrevTime).Slerp(q, w);
			if (fade < 1f) q = _skeleton.GetBonePoseRotation(bone).Slerp(q, fade);
			Write(bone, q);
		}

		// A posição de alguns ossos da mão (a palma de lá fecha deslizando os nós)
		for (int i = 0; i < _clipPosBones.Length; i++)
		{
			int bone = _clipPosBones[i];
			if (bone < 0 || now.PosCount == 0) continue;
			Vector3 p = now.Position(i, at);
			if (before != null && before.PosCount > 0) p = before.Position(i, _rig.PrevTime).Lerp(p, w);
			if (fade < 1f) p = _skeleton.GetBonePosePosition(bone).Lerp(p, fade);
			WritePosition(bone, p);
		}

		ChestAsBaked(fade);
		PitchChest();
	}

	/// <summary>Quantas vezes a granada na mão é maior vista de fora que a da primeira pessoa. Foi 1,4
	/// (Jhon, 23/11: "aumente o tamanho da granada"); desde 24/11 é 1, a mesma da primeira pessoa
	/// ("deixe a granada da terceira pessoa do mesmo tamanho da primeira"): 1,40 da de verdade
	/// (tools/armas/conjuntos.py). Só a da mão em terceira pessoa. Os dedos do personagem abrem para ela e o dedo que puxa
	/// o pino entra na argola crescida no Blender (GRANADA_MAO_ABRE, GRANADA_CRESCE: os dois
	/// números têm de ser iguais).</summary>
	[Export(PropertyHint.Range, "1,2")] public float GrenadeHeldGrow = 1f;

	// a pose do Grip antes de crescer e a que se escreveu: a animação não repõe a escala dele, e
	// crescer em cima do quadro de antes fazia a granada crescer sem parar até sumir
	private Skeleton3D _grownOf;
	private Vector3 _gripPos, _gripScale, _grownPos, _grownScale;
	private Quaternion _gripRot, _grownRot;

	/// <summary>Cresce a granada do conjunto em volta do centro dela: o osso Grip leva o corpo,
	/// o pino e a alavanca. Sempre a partir da pose sem crescer (a da animação, ou a guardada
	/// se a animação não repôs o osso).</summary>
	private void GrowGrenade()
	{
		// O conjunto já cresce com a mão do personagem (a escala do arquivo: o Barut 1,3); a granada cresce
		// só o que falta para ficar do tamanho de GrenadeHeldGrow nos dois.
		float grow = GrenadeHeldGrow / Mathf.Max(1f, _clipSet?.Scale ?? 1f);
		if (!Arsenal.IsGrenade(_id) || Mathf.IsEqualApprox(grow, 1f) || grow < 1f || !_rig.HasCenter) return;
		Skeleton3D sk = _rig.Skeleton;
		int grip = sk.FindBone("Grip");
		if (grip < 0) return;
		// cada uma por si: a animação repõe a posição e a rotação, mas não a escala
		if (_grownOf == sk)
		{
			if (sk.GetBonePosePosition(grip).IsEqualApprox(_grownPos)) sk.SetBonePosePosition(grip, _gripPos);
			if (sk.GetBonePoseRotation(grip).IsEqualApprox(_grownRot)) sk.SetBonePoseRotation(grip, _gripRot);
			if (sk.GetBonePoseScale(grip).IsEqualApprox(_grownScale)) sk.SetBonePoseScale(grip, _gripScale);
		}
		_grownOf = sk;
		_gripPos = sk.GetBonePosePosition(grip);
		_gripRot = sk.GetBonePoseRotation(grip);
		_gripScale = sk.GetBonePoseScale(grip);
		Transform3D G = sk.GlobalTransform * sk.GetBoneGlobalPose(grip);
		Vector3 center = _rig.Center();
		Vector3 local = G.AffineInverse() * center;
		Basis grown = G.Basis.Scaled(Vector3.One * grow);
		var target = new Transform3D(grown, center - grown * local);
		int parent = sk.GetBoneParent(grip);
		Transform3D parentGlobal = sk.GlobalTransform * (parent >= 0 ? sk.GetBoneGlobalPose(parent) : Transform3D.Identity);
		Transform3D pose = parentGlobal.AffineInverse() * target;
		sk.SetBonePosePosition(grip, pose.Origin);
		sk.SetBonePoseRotation(grip, pose.Basis.Orthonormalized().GetRotationQuaternion());
		sk.SetBonePoseScale(grip, pose.Basis.Scale);
		_grownPos = sk.GetBonePosePosition(grip);
		_grownRot = sk.GetBonePoseRotation(grip);
		_grownScale = sk.GetBonePoseScale(grip);
	}

	// ------------------------------------------------------------ a troca de pente vista de fora

	private MeshInstance3D _rigMag;
	private int _rigMagBone = -1;
	private HeldRig _rigMagOf;
	// a mão do corpo para a do conjunto, medida com as duas juntas (parado ou mirando)
	private Transform3D _bodyToRigHand = Transform3D.Identity;
	private bool _knowsHand, _magDropped;
	// o pente no quadro de antes (para a velocidade e o giro com que ele sai da mão)
	private Transform3D _magBefore = Transform3D.Identity;
	private bool _magTracked;
	private Vector3 _magVelocity, _magSpin;

	/// <summary>Em quanto tempo (s) o pente passa da animação para a mão do corpo ao ser
	/// agarrado (a mão do corpo está com a de lá, e os dois lugares são o mesmo).</summary>
	[Export] public float MagazineHandOver = 0.08f;

	/// <summary>Quanto tempo o pente que caiu fica no chão (s).</summary>
	[Export] public float DroppedMagazineLife = 8f;

	/// <summary>Quanto da velocidade e do giro da mão o pente leva ao ser solto (0 = cai parado,
	/// só a gravidade; 1 = tudo), e o teto dos dois (m/s e rad/s).</summary>
	[Export(PropertyHint.Range, "0,1")] public float MagazineThrow = 0.35f;
	[Export] public float MagazineMaxSpeed = 3f;
	[Export] public float MagazineMaxSpin = 8f;

	private ArrayMesh _looseMag;
	private Mesh _looseMagOf;

	/// <summary>A malha do pente sem os dados da pele (só o que se desenha), montada uma vez por
	/// arma: a do conjunto, solta num corpo físico, o motor não desenha.</summary>
	private ArrayMesh LooseMagazine()
	{
		Mesh source = _rigMag?.Mesh;
		if (source == null) return null;
		if (_looseMag != null && _looseMagOf == source) return _looseMag;
		var loose = new ArrayMesh();
		for (int s = 0; s < source.GetSurfaceCount(); s++)
		{
			Godot.Collections.Array from = source.SurfaceGetArrays(s);
			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			foreach (Mesh.ArrayType kind in new[] { Mesh.ArrayType.Vertex, Mesh.ArrayType.Normal, Mesh.ArrayType.Tangent,
				Mesh.ArrayType.Color, Mesh.ArrayType.TexUV, Mesh.ArrayType.TexUV2, Mesh.ArrayType.Index })
				arrays[(int)kind] = from[(int)kind];
			loose.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		}
		_looseMag = loose;
		_looseMagOf = source;
		return loose;
	}

	/// <summary>
	/// A recarga vista de fora troca o pente (Jhon, 23/11: "a Eve tira o pente, ele cai no
	/// chão (velocidade da gravidade), tira outro pente do bolso; o pente precisa estar nas
	/// mãos, orientado pela pegada da mão, veja a referência na recarga em primeira pessoa").
	/// Na primeira pessoa a mão some embaixo da câmera e volta com o mesmo pente; de fora, a
	/// animação do Blender (<c>Copia.trocar_pente</c>) leva a mão ao bolso, e os tempos vêm no
	/// clipe (<see cref="HoldClip.MagGrab"/>). Com o pente na mão, ele vai preso na mão
	/// esquerda do corpo pela pegada de lá, quadro a quadro: onde o pente do conjunto está em
	/// relação à mão do conjunto agora, levado para a mão do corpo. Ao soltar, some, e uma
	/// cópia cai em queda livre; quando a mão pega o novo no bolso, reaparece na mão, e no
	/// encaixe volta para o lugar da animação. O boneco de outro jogador não sabe que
	/// recarrega (só toca a animação que chegou): basta o conjunto estar tocando a recarga.
	/// </summary>
	private void SwapMagazine(HoldClip clip, double at)
	{
		if (_rigMagOf != _rig)
		{
			_rigMagOf = _rig;
			_rigMag = _rig?.FindChild("carregador", true, false) as MeshInstance3D;
			_rigMagBone = _rig?.Skeleton != null ? _rig.Skeleton.FindBone("Magazine") : -1;
			_knowsHand = false;
			_magDropped = false;
		}
		if (_rigMag == null || _rigMagBone < 0 || _leftHand < 0 || _rig.HandL < 0) return;
		Skeleton3D rigSkeleton = _rig.Skeleton;
		Transform3D rigHand = rigSkeleton.GlobalTransform * rigSkeleton.GetBoneGlobalPose(_rig.HandL);
		Transform3D bodyHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_leftHand);
		bool swapping = clip != null && clip.SwapsMagazine && _rig.Playing == clip.Name;
		if (!swapping)
		{
			// parado, andando ou mirando, já sem mistura: a mão do corpo está na do conjunto
			if (_rig.BlendWeight >= 1f && _rig.Playing is "parado" or "andar" or "mira" or "mira_andar")
			{
				_bodyToRigHand = bodyHand.AffineInverse() * rigHand;
				_knowsHand = true;
			}
			_rigMag.Visible = true;
			_magDropped = false;
			_magTracked = false;
			return;
		}
		if (!_knowsHand) return;

		// o pente na mão do corpo, seguro como a mão de lá o segura neste quadro
		Transform3D animated = rigSkeleton.GlobalTransform * rigSkeleton.GetBoneGlobalPose(_rigMagBone);
		Transform3D carried = bodyHand * _bodyToRigHand * rigHand.AffineInverse() * animated;
		if (at < clip.MagGrab)
		{
			_rigMag.Visible = true;
			_magDropped = false;
			_magTracked = false;
			return;
		}
		if (at >= clip.MagDrop && at < clip.MagPick)
		{
			if (!_magDropped)
			{
				_magDropped = true;
				// Da mão, se ela o tirou: sai com a velocidade e o giro que tinha nela (um objeto
				// largado). Direto da arma, se foi ejetado (a recarga de pente vazio da pistola:
				// a mão de lá nem o segura): só a velocidade do corpo, e a gravidade faz o resto.
				bool fromHand = clip.MagDrop > clip.MagGrab && _magTracked;
				DropMagazine(clip.MagDrop > clip.MagGrab ? carried : animated,
					fromHand ? _magVelocity : _body.Velocity, fromHand ? _magSpin : Vector3.Zero);
			}
			_rigMag.Visible = false;
			_magTracked = false;
			return;
		}
		_rigMag.Visible = true;
		if (at >= clip.MagSeat) return;
		// Ao agarrar, passa da animação para a mão em MagazineHandOver. Do bolso ao encaixe fica
		// só na mão: na primeira pessoa o pente está a metros dali até dois quadros antes de
		// encaixar (é como ela esconde a troca), e no encaixe a mão do corpo chega na pose de
		// lá, onde os dois lugares são o mesmo.
		float hand = at < clip.MagDrop ? Mathf.Clamp((float)((at - clip.MagGrab) / MagazineHandOver), 0f, 1f) : 1f;
		hand = hand * hand * (3f - 2f * hand);
		Transform3D target = hand >= 1f ? carried : animated.InterpolateWith(carried, hand);
		// a velocidade e o giro do pente na mão, de um quadro para o outro (no mundo)
		float dt = (float)GetProcessDeltaTime();
		if (_magTracked && dt > 1e-4f)
		{
			Quaternion turned = (target.Basis.Orthonormalized() * _magBefore.Basis.Orthonormalized().Inverse()).GetRotationQuaternion().Normalized();
			float angle = turned.GetAngle();
			if (angle > Mathf.Pi) angle -= Mathf.Tau;
			_magSpin = Mathf.Abs(angle) > 1e-5f ? turned.GetAxis().Normalized() * (angle / dt) : Vector3.Zero;
			_magVelocity = (target.Origin - _magBefore.Origin) / dt;
		}
		_magBefore = target;
		_magTracked = true;
		int parent = rigSkeleton.GetBoneParent(_rigMagBone);
		Transform3D parentGlobal = rigSkeleton.GlobalTransform * (parent >= 0 ? rigSkeleton.GetBoneGlobalPose(parent) : Transform3D.Identity);
		Transform3D local = parentGlobal.AffineInverse() * target;
		rigSkeleton.SetBonePosePosition(_rigMagBone, local.Origin);
		rigSkeleton.SetBonePoseRotation(_rigMagBone, local.Basis.Orthonormalized().GetRotationQuaternion());
	}

	/// <summary>Uma cópia do pente cai de onde foi solto e obedece à física até parar no chão
	/// (Jhon, 23/11: "o pente precisa cair 100% no chão e respeitar a física"): sai com a
	/// velocidade e o giro dados, a gravidade faz o resto, e a forma que bate é a do próprio
	/// pente (o casco convexo da malha: com a caixa em volta dela, um cubo, ele parava de pé no
	/// ar, a 7 cm do chão). Só de enfeite: bate no cenário, não fere ninguém, e some depois de
	/// <see cref="DroppedMagazineLife"/>.</summary>
	private void DropMagazine(Transform3D magazineBone, Vector3 velocity, Vector3 spin)
	{
		if (_rigMag.Mesh == null || _rigMag.Skin == null) return;
		ArrayMesh loose = LooseMagazine();
		if (loose == null) return;
		Skin skin = _rigMag.Skin;
		Transform3D bind = Transform3D.Identity;
		for (int i = 0; i < skin.GetBindCount(); i++)
			if (skin.GetBindName(i) == "Magazine" || skin.GetBindBone(i) == _rigMagBone)
				bind = skin.GetBindPose(i);
		// A malha solta, sem a pele: o osso vezes a pose de ligação. O corpo rígido fica no
		// meio do pente (a origem da malha é a do conjunto, longe dele), sem escala; a malha e
		// a forma de colisão vão juntas, com a escala e o desvio.
		Transform3D place = magazineBone * bind;
		Aabb box = _rigMag.Mesh.GetAabb();
		Vector3 scale = place.Basis.Scale;
		Vector3 offset = -(box.GetCenter() * scale);
		var body = new RigidBody3D
		{
			CollisionLayer = 0,
			CollisionMask = 1,
			Mass = 0.25f,
			ContinuousCd = true,
			PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.9f, Bounce = 0.25f },
		};
		var mesh = new MeshInstance3D { Mesh = loose, Layers = _rigMag.Layers, Scale = scale, Position = offset };
		for (int s = 0; s < loose.GetSurfaceCount(); s++)
			mesh.SetSurfaceOverrideMaterial(s, _rigMag.GetActiveMaterial(s));
		body.AddChild(mesh);
		Shape3D hull = loose.CreateConvexShape(true, true);
		body.AddChild(hull != null
			? new CollisionShape3D { Shape = hull, Scale = scale, Position = offset }
			: new CollisionShape3D { Shape = new BoxShape3D { Size = (box.Size * scale).Max(new Vector3(0.01f, 0.01f, 0.01f)) } });
		Node world = GetTree().CurrentScene ?? GetTree().Root;
		world.AddChild(body);
		Vector3 center = place * box.GetCenter();
		body.GlobalTransform = new Transform3D(place.Basis.Orthonormalized(), center);
		// a velocidade dada é a da origem do osso: a do meio do pente soma o giro
		// (com teto: um quadro ruim não pode arremessar o pente). Da velocidade da mão em relação
		// ao corpo ele leva só MagazineThrow: lá a mão arranca o pente a mais de 2 m/s, e com tudo
		// isso ele voava mais de um metro para o lado.
		Vector3 thrown = (velocity - _body.Velocity + spin.Cross(center - magazineBone.Origin)) * MagazineThrow;
		body.LinearVelocity = _body.Velocity + thrown.LimitLength(MagazineMaxSpeed);
		body.AngularVelocity = (spin * MagazineThrow).LimitLength(MagazineMaxSpin);
		GetTree().CreateTimer(DroppedMagazineLife).Timeout += () =>
		{
			if (IsInstanceValid(body)) body.QueueFree();
		};
	}

	/// <summary>
	/// O peito como no Blender, em relação ao corpo. Lá o quadril estava em
	/// repouso; aqui ele vem da animação de andar (que gira e balança), e as
	/// vértebras desfazem a diferença — senão o tronco, as mãos e a arma
	/// apontam para onde o quadril gira, e não para a mira.
	/// </summary>
	private void ChestAsBaked(float fade)
	{
		if (_spines.Count == 0) return;
		int hips = _skeleton.GetBoneParent(_spines[0]);
		if (hips < 0) return;
		Basis body = _skeleton.GlobalBasis.Orthonormalized();
		Basis rest = _skeleton.GetBoneGlobalRest(hips).Basis.Orthonormalized();
		Basis pose = _skeleton.GetBoneGlobalPose(hips).Basis.Orthonormalized();
		Quaternion delta = (body * rest * pose.Transposed() * body.Transposed()).Orthonormalized().GetRotationQuaternion().Normalized();
		if (fade < 1f) delta = Quaternion.Identity.Slerp(delta, fade);
		Quaternion part = Quaternion.Identity.Slerp(delta, 1f / _spines.Count);
		foreach (int bone in _spines) TurnBone(bone, part, body);
	}

	/// <summary>O peito inclina para a mira (para cima e para baixo), todo
	/// nas vértebras: a cabeça e a arma vão juntas, e mirando o olho continua
	/// na linha da mira.</summary>
	private void PitchChest()
	{
		if (_spines.Count == 0) return;
		float limit = Mathf.DegToRad(AimPitchLimit);
		float pitch = Mathf.Clamp(_bodyHead?.Rotation.X ?? 0f, -limit, limit);
		if (Mathf.Abs(pitch) < 0.002f) return;
		Basis body = _skeleton.GlobalBasis.Orthonormalized();
		Vector3 side = _body.GlobalBasis.X.Normalized();
		var part = new Quaternion(side, pitch / _spines.Count);
		foreach (int bone in _spines) TurnBone(bone, part, body);
	}

	// ------------------------------------------------------------ a pose do Blender (22/11)

	/// <summary>O arquivo de poses do personagem (<c>poses_armas.json</c>, ao
	/// lado do modelo); o CharacterAnimator põe antes do Attach.</summary>
	public string PosesPath;

	/// <summary>Usar as poses do Blender quando o personagem tem (desligado,
	/// volta o conjunto posto na frente do olho, de 21/11).</summary>
	[Export] public bool UsePoses = true;

	/// <summary>Quanto tempo leva a troca de uma pose para a outra (s).</summary>
	[Export] public float PoseBlendTime = 0.18f;

	private Dictionary<string, HoldPose> _poses;
	private readonly Dictionary<string, int> _boneByName = new();
	private double _poseClock = GD.Randf() * 3.0;
	private HoldPose _poseNow, _posePrevious;
	private float _poseBlend = 1f;

	private HoldPose PoseFor(string id)
	{
		if (!UsePoses || _poses == null || string.IsNullOrEmpty(id)) return null;
		return _poses.TryGetValue(HoldPoses.KindOf(id), out HoldPose pose) ? pose : null;
	}

	/// <summary>
	/// A arma com a pose do Blender (Jhon, 22/11: "use o blender para isso,
	/// não ficou bom"): o corpo da cintura para cima vai para a pose feita
	/// lá para este tipo de arma (o tronco, a cabeça, os braços e os dedos),
	/// o peito vira para a mira, e o conjunto da primeira pessoa é posto pela
	/// mão direita da pose — na pose ela segura a arma como a da primeira
	/// pessoa segura. Daí em diante só o que a animação da primeira pessoa
	/// mexe (sacar, tiro, recarga, golpe, armar, arremessar) move as mãos: a
	/// direita segue a de lá, a arma vai com ela, e a esquerda vai na arma
	/// (fuzil, pistola) ou fica na pose (faca, granada).
	/// </summary>
	private void HoldByPose(double delta, HoldPose pose)
	{
		_poseClock += delta;
		if (pose != _poseNow)
		{
			_posePrevious = _poseNow;
			_poseNow = pose;
			_poseBlend = 0f;
		}
		_poseBlend = Mathf.Min(1f, _poseBlend + (float)delta / Mathf.Max(PoseBlendTime, 0.01f));

		ApplyHoldPose(pose, _posePrevious, _poseBlend, _poseClock);
		AimChest(pose, _posePrevious, _poseBlend, _poseClock);

		_rig.Tick(delta, _weapon.IsGrenadeReady, false);
		if (_rightUpper < 0 || _rightLower < 0 || _rightHand < 0) return;
		if (!BodyHand(true, out Vector3 fingers, out Vector3 palm, out Vector3 knuckles)) return;

		// O conjunto pela mão direita da pose: a mão dele, no começo do
		// "parado", cai nela (o começo dos dedos no mesmo ponto, os dedos e a
		// palma para o mesmo lado). A granada cresce junto.
		Transform3D skeleton = _skeleton.GlobalTransform;
		Basis handRot = (skeleton * _skeleton.GetBoneGlobalPose(_rightHand)).Basis.Orthonormalized();
		Vector3 handAt = Bone(skeleton, _rightHand);
		var posed = new Transform3D(ArmIK.Orient(handRot * fingers, handRot * palm), handAt + handRot * (knuckles * KnuckleAlign));
		Transform3D rest = RigFrame(_rig.RestWristR, _rig.RestMiddleR, _rig.RestIndexR, _rig.RestPinkyR, true);
		float grow = pose.Grow > 0.01f ? pose.Grow : 1f;
		_rig.GlobalTransform = posed * new Transform3D(Basis.Identity.Scaled(Vector3.One * grow), Vector3.Zero) * rest.AffineInverse();

		// Os cotovelos da pose (o polo do braço).
		Vector3 elbowR = Bone(skeleton, _rightLower);
		Vector3 elbowL = _leftLower >= 0 ? Bone(skeleton, _leftLower) : Vector3.Zero;

		// A direita vai onde a mão da primeira pessoa está agora (parada, é
		// a própria pose; sacando, atirando, golpeando, ela se mexe).
		_rig.HandPoints(true, out Vector3 w, out Vector3 m, out Vector3 i, out Vector3 p);
		Transform3D now = _rig.GlobalTransform * RigFrame(w, m, i, p, true);
		var target = new Transform3D(now.Basis.Orthonormalized(), now.Origin);
		Basis turn = target.Basis * ArmIK.Orient(fingers, palm).Transposed();
		Vector3 wrist = target.Origin - turn * (knuckles * KnuckleAlign);
		bool oneHand = Arsenal.IsKnife(_id) || Arsenal.IsGrenade(_id) || WeaponModel.IsPistol(_id);
		var shape = new HandShape(fingers, palm, knuckles, Vector3.Zero, Vector3.Zero, Vector3.Zero) { AlignAt = KnuckleAlign };
		Basis final = Arm(true, wrist, turn, shape, oneHand ? WristBend : 180f, elbowR);

		// A arma vai com a mão direita (se a mão não chegou ou o punho
		// desentortou, a arma acompanha). A granada fica na palma.
		Basis finalRot = final.Orthonormalized();
		var reached = new Transform3D(ArmIK.Orient(finalRot * fingers, finalRot * palm),
			Bone(_skeleton.GlobalTransform, _rightHand) + finalRot * (knuckles * KnuckleAlign));
		Transform3D carry = reached * target.AffineInverse();
		if (pose.Ball > 0f && _rig.HasCenter && Palm(true, out Vector3 palmAt, out Vector3 inside))
		{
			Vector3 socket = palmAt + inside * pose.Ball;
			carry = new Transform3D(Basis.Identity, socket - carry * _rig.Center()) * carry;
		}
		_rig.Carry(carry);

		// A esquerda na arma (fuzil, pistola): onde a da primeira pessoa
		// está, levada com a arma; um pouco para trás no guarda-mão se o
		// braço não alcançava (o Blender disse quanto). Na faca e na granada
		// ela fica na pose.
		if (pose.Left == "arma" && _leftUpper >= 0 && _leftLower >= 0 && _leftHand >= 0
			&& BodyHand(false, out Vector3 leftFingers, out Vector3 leftPalm, out Vector3 leftKnuckles))
		{
			_rig.HandPoints(false, out w, out m, out i, out p);
			Transform3D left = carry * (_rig.GlobalTransform * RigFrame(w, m, i, p, false));
			Basis leftBasis = left.Basis.Orthonormalized();
			Vector3 leftPoint = left.Origin - _rig.GunForward() * pose.Slide;
			Basis leftTurn = leftBasis * ArmIK.Orient(leftFingers, leftPalm).Transposed();
			Vector3 leftWrist = leftPoint - leftTurn * (leftKnuckles * KnuckleAlign);
			var leftShape = new HandShape(leftFingers, leftPalm, leftKnuckles, Vector3.Zero, Vector3.Zero, Vector3.Zero) { AlignAt = KnuckleAlign };
			Arm(false, leftWrist, leftTurn, leftShape, 180f, elbowL);
		}

		_rig.FollowMuzzle();
		_calibrated = false;
	}

	/// <summary>Uma mão do conjunto como base: os dedos, a palma e o ponto no
	/// começo dos dedos (no espaço em que os pontos vieram).</summary>
	private Transform3D RigFrame(Vector3 wrist, Vector3 middle, Vector3 index, Vector3 pinky, bool right)
	{
		Vector3 along = middle - wrist, across = index - pinky;
		Vector3 inside = right ? across.Cross(along) : along.Cross(across);
		return new Transform3D(ArmIK.Orient(along, inside), wrist.Lerp(middle, KnuckleAlign));
	}

	/// <summary>Os ossos da cintura para cima na pose (passando da anterior
	/// para esta em <see cref="PoseBlendTime"/>).</summary>
	private void ApplyHoldPose(HoldPose pose, HoldPose previous, float blend, double t)
	{
		foreach (var (name, keys) in pose.Bones)
		{
			if (!_boneByName.TryGetValue(name, out int bone)) continue;
			Quaternion q = pose.Sample(keys, t);
			if (blend < 1f)
			{
				Quaternion from = previous != null && previous.Bones.TryGetValue(name, out var old)
					? previous.Sample(old, t) : _skeleton.GetBonePoseRotation(bone);
				q = from.Slerp(q, blend * blend * (3f - 2f * blend));
			}
			Write(bone, q);
		}
	}

	/// <summary>
	/// O peito como na pose, em relação ao corpo, e virado para a mira: as
	/// pernas e o quadril vêm da animação de andar (que gira o quadril), e as
	/// vértebras desfazem a diferença. A cabeça leva o resto da inclinação.
	/// </summary>
	private void AimChest(HoldPose pose, HoldPose previous, float blend, double t)
	{
		if (_spines.Count == 0) return;
		int chest = _spines[^1];
		Basis body = _skeleton.GlobalBasis.Orthonormalized();
		float limit = Mathf.DegToRad(AimPitchLimit);
		float pitch = Mathf.Clamp(_bodyHead?.Rotation.X ?? 0f, -limit, limit);
		Vector3 side = _body.GlobalBasis.X.Normalized();

		Quaternion model = pose.Sample(pose.Chest, t);
		if (blend < 1f && previous != null) model = previous.Sample(previous.Chest, t).Slerp(model, blend);
		Quaternion want = (new Quaternion(side, pitch * AimPitchShare) * (body * new Basis(model)).GetRotationQuaternion()).Normalized();
		Quaternion now = (body * _skeleton.GetBoneGlobalPose(chest).Basis.Orthonormalized()).GetRotationQuaternion();
		Quaternion delta = (want * now.Inverse()).Normalized();
		Quaternion part = Quaternion.Identity.Slerp(delta, 1f / _spines.Count);
		foreach (int bone in _spines) TurnBone(bone, part, body);

		// A cabeça: o resto da inclinação da mira, metade no pescoço.
		float rest = pitch * (1f - AimPitchShare);
		if (Mathf.Abs(rest) > 0.002f)
		{
			var nod = new Quaternion(side, rest * 0.5f);
			if (_neck >= 0) TurnBone(_neck, nod, body);
			if (_head >= 0) TurnBone(_head, nod, body);
		}
	}

	/// <summary>Gira um osso (e o que vem depois) no mundo, em torno dele.</summary>
	private void TurnBone(int bone, Quaternion world, Basis body)
	{
		int parent = _skeleton.GetBoneParent(bone);
		Basis local = body.Transposed() * new Basis(world) * body;
		Basis global = local * _skeleton.GetBoneGlobalPose(bone).Basis.Orthonormalized();
		Basis parentGlobal = parent >= 0 ? _skeleton.GetBoneGlobalPose(parent).Basis.Orthonormalized() : Basis.Identity;
		Write(bone, (parentGlobal.Transposed() * global).Orthonormalized().GetRotationQuaternion());
	}

	/// <summary>A dobra do punho que fica (graus): o resto o cotovelo e a mão
	/// desfazem.</summary>
	[Export(PropertyHint.Range, "0,90")] public float WristBend = 28f;

	/// <summary>A granada na mão do personagem, do tamanho da mão dele (1 = a
	/// mão dele é para a granada o que a do conjunto é para a dela).</summary>
	[Export(PropertyHint.Range, "0.5,2")] public float GrenadeGrow = 1f;

	/// <summary>Quanto abaixo do ombro (m) a mão esquerda do conjunto começa a
	/// ser deixada solta ao lado do corpo.</summary>
	[Export] public float LooseFrom = 0.3f;

	/// <summary>A forma de uma mão do corpo e da mão do conjunto, para levar
	/// a arma de uma mão da do conjunto para a do corpo.</summary>
	private readonly record struct HandShape(Vector3 Fingers, Vector3 Palm, Vector3 Knuckles,
		Vector3 RigWrist, Vector3 RigAlong, Vector3 RigInside)
	{
		/// <summary>A mão do corpo é quantas vezes a do conjunto.</summary>
		public float Ratio => RigAlong.Length() > 1e-4f ? Knuckles.Length() / RigAlong.Length() : 1f;

		/// <summary>O que leva a mão do conjunto (e o que ela segura) para a
		/// mão do corpo, já no lugar: <paramref name="grow"/> vezes maior.</summary>
		public Transform3D Carry(Vector3 wrist, Basis rotation, float grow)
		{
			Basis rotationOrtho = rotation.Orthonormalized();
			Vector3 along = rotationOrtho * Fingers, inside = rotationOrtho * Palm;
			var body = new Transform3D(ArmIK.Orient(along, inside), wrist + rotationOrtho * (Knuckles * AlignAt));
			var rig = new Transform3D(ArmIK.Orient(RigAlong, RigInside), RigWrist + RigAlong * AlignAt);
			return body * new Transform3D(Basis.Identity.Scaled(Vector3.One * grow), Vector3.Zero) * rig.AffineInverse();
		}

		public float AlignAt { get; init; }
	}

	/// <summary>Onde o pulso do corpo vai e como a mão gira para a mão dele
	/// ficar como a do conjunto: os dedos para o mesmo lado, a palma virada
	/// para o mesmo lado, o começo dos dedos no mesmo ponto.</summary>
	private bool RigTarget(bool right, out Vector3 wrist, out Basis turn, out HandShape shape)
	{
		wrist = Vector3.Zero;
		turn = Basis.Identity;
		shape = default;
		int hand = right ? _rig.HandR : _rig.HandL;
		int[][] fp = right ? _rig.FingersR : _rig.FingersL;
		if (hand < 0 || fp[1] == null || fp[2] == null || fp[4] == null) return false;
		if (!BodyHand(right, out Vector3 fingers, out Vector3 palm, out Vector3 knuckles)) return false;

		Vector3 w = _rig.At(hand), m = _rig.At(fp[2][0]);
		Vector3 along = m - w, across = _rig.At(fp[1][0]) - _rig.At(fp[4][0]);
		Vector3 inside = right ? across.Cross(along) : along.Cross(across);
		if (along.LengthSquared() < 1e-8f || inside.LengthSquared() < 1e-10f) return false;

		turn = ArmIK.Orient(along, inside) * ArmIK.Orient(fingers, palm).Transposed();
		wrist = w.Lerp(m, KnuckleAlign) - turn * (knuckles * KnuckleAlign);
		shape = new HandShape(fingers, palm, knuckles, w, along, inside) { AlignAt = KnuckleAlign };
		return true;
	}

	/// <summary>
	/// Um braço do corpo até o pulso, a mão girada como pedido, sem o punho
	/// entortar: o cotovelo aponta para trás da mão (o antebraço na linha
	/// dela) e, se ainda sobrar dobra além de <paramref name="maxBend"/>
	/// graus, a mão volta até lá. Devolve a rotação que a mão ficou.
	/// </summary>
	private Basis Arm(bool right, Vector3 wrist, Basis turn, HandShape shape, float maxBend, Vector3? posedElbow = null)
	{
		int upper = right ? _rightUpper : _leftUpper, lower = right ? _rightLower : _leftLower, hand = right ? _rightHand : _leftHand;
		Transform3D skeleton = _skeleton.GlobalTransform;
		float forearm = Bone(skeleton, lower).DistanceTo(Bone(skeleton, hand));
		Vector3 along = (turn.Orthonormalized() * shape.Fingers).Normalized();

		// Atrás da mão, na linha dela, puxado para baixo e para fora como o
		// cotovelo de quem segura uma arma.
		Vector3 behind = wrist - along * forearm;
		// Com pose, o cotovelo da pose (o Blender já escolheu o que deixa o
		// punho reto).
		Vector3 pole = posedElbow ?? Elbow(right).Lerp(behind + Vector3.Down * 0.06f, ElbowFollowsHand);
		ArmIK.Reach(_skeleton, upper, lower, hand, wrist, turn, pole, Write, ForearmTwist);

		if (maxBend >= 179f) return turn;
		Vector3 elbow = Bone(_skeleton.GlobalTransform, lower);
		Vector3 forearmAxis = (wrist - elbow).Normalized();
		float bend = Mathf.RadToDeg(along.AngleTo(forearmAxis));
		if (bend <= maxBend + 0.5f) return turn;

		Quaternion back = ArmIK.Arc(along, forearmAxis);
		Basis straighter = new Basis(Quaternion.Identity.Slerp(back, (bend - maxBend) / bend)) * turn.Orthonormalized();
		ArmIK.Reach(_skeleton, upper, lower, hand, wrist, straighter, pole, Write, ForearmTwist);
		return straighter;
	}

	/// <summary>O quanto o cotovelo vai para trás da mão (0 = o cotovelo
	/// de sempre, para baixo e para fora).</summary>
	[Export(PropertyHint.Range, "0,1")] public float ElbowFollowsHand = 0.6f;

	/// <summary>A mão esquerda solta, ao lado do corpo: um pouco à frente da
	/// coxa, os dedos para baixo, a palma para dentro.</summary>
	private void Loose(out Vector3 wrist, out Basis turn)
	{
		Transform3D skeleton = _skeleton.GlobalTransform;
		Basis body = _body.GlobalBasis.Orthonormalized();
		Vector3 shoulder = Bone(skeleton, _leftUpper);
		float arm = shoulder.DistanceTo(Bone(skeleton, _leftLower)) + Bone(skeleton, _leftLower).DistanceTo(Bone(skeleton, _leftHand));
		// O corpo olha para −Z: a esquerda é −X.
		wrist = shoulder + Vector3.Down * arm * 0.9f - body.Z * 0.1f - body.X * 0.05f;
		turn = Basis.Identity;
		if (!BodyHand(false, out Vector3 fingers, out Vector3 palm, out _)) return;
		Vector3 along = (Vector3.Down - body.Z * 0.25f).Normalized();
		turn = ArmIK.Orient(along, body.X) * ArmIK.Orient(fingers, palm).Transposed();
	}

	/// <summary>A mão do corpo, na rotação do osso dela: para onde vão os
	/// dedos, para onde a palma olha, e o começo do dedo médio (do pulso).</summary>
	private bool BodyHand(bool right, out Vector3 fingers, out Vector3 palm, out Vector3 middle)
	{
		fingers = palm = middle = Vector3.Zero;
		int wrist = right ? _rightHand : _leftHand;
		int mid = right ? _rightMiddle : _leftMiddle, index = right ? _rightIndex : _leftIndex, pinky = right ? _rightPinky : _leftPinky;
		if (wrist < 0 || mid < 0 || index < 0 || pinky < 0) return false;

		Transform3D skeleton = _skeleton.GlobalTransform;
		Transform3D pose = skeleton * _skeleton.GetBoneGlobalPose(wrist);
		Basis back = pose.Basis.Orthonormalized().Transposed();
		Vector3 along = Bone(skeleton, mid) - pose.Origin;
		Vector3 across = Bone(skeleton, index) - Bone(skeleton, pinky);
		Vector3 inside = right ? across.Cross(along) : along.Cross(across);
		if (along.LengthSquared() < 1e-8f || inside.LengthSquared() < 1e-10f) return false;

		fingers = (back * along).Normalized();
		palm = (back * inside).Normalized();
		middle = back * along;
		return true;
	}

	/// <summary>Quanto o conjunto tem de chegar perto para este braço
	/// alcançar o pulso (zero se alcança).</summary>
	private Vector3 Shortfall(int upper, int lower, int hand, Vector3 wrist)
	{
		Transform3D skeleton = _skeleton.GlobalTransform;
		Vector3 a = Bone(skeleton, upper), b = Bone(skeleton, lower), c = Bone(skeleton, hand);
		float reach = (a.DistanceTo(b) + b.DistanceTo(c)) * 0.97f;
		Vector3 to = wrist - a;
		float d = to.Length();
		return d > reach && d > 1e-4f ? -to / d * (d - reach) : Vector3.Zero;
	}

	/// <summary>
	/// Os dedos do corpo fechados como os do conjunto: cada falange dobra o
	/// quanto a de lá dobra, no plano em que os dedos do personagem fecham
	/// (o afastamento entre os dedos fica o do personagem — copiado do
	/// conjunto, a mão maior virava uma garra). O polegar copia a direção de
	/// cada falange de lá. <paramref name="carry"/> leva a mão do conjunto
	/// para onde a arma de uma mão foi; <paramref name="extra"/> fecha mais
	/// (a mão de apoio no guarda-mão: os dedos do personagem, mais longos,
	/// passavam por cima); <paramref name="weight"/> 0 deixa como estão.
	/// </summary>
	private void RigFingers(bool right, Transform3D carry, float weight, float extra = 0f, Vector3 thumbForward = default,
		Vector3 ball = default, float ballRadius = 0f)
	{
		if (weight <= 0.01f) return;
		int[][] fp = right ? _rig.FingersR : _rig.FingersL;
		int[][] body = right ? _fingersR : _fingersL;
		int rigHand = right ? _rig.HandR : _rig.HandL, hand = right ? _rightHand : _leftHand;
		if (fp[1] == null || fp[2] == null || fp[4] == null || hand < 0) return;
		if (!BodyHand(right, out Vector3 bodyFingers, out Vector3 bodyPalm, out _)) return;

		// A mão do conjunto (levada) e a do corpo: para onde os dedos vão,
		// para onde a palma olha, e o eixo em que um dedo fecha.
		Basis turn = carry.Basis.Orthonormalized();
		Vector3 w = carry * _rig.At(rigHand), m = carry * _rig.At(fp[2][0]);
		Vector3 rigAlong = (m - w).Normalized(), across = carry * _rig.At(fp[1][0]) - carry * _rig.At(fp[4][0]);
		Vector3 rigInside = (right ? across.Cross(rigAlong) : rigAlong.Cross(across)).Normalized();
		Vector3 rigCurl = rigAlong.Cross(rigInside).Normalized();

		Transform3D skeleton = _skeleton.GlobalTransform;
		Basis handNow = (skeleton * _skeleton.GetBoneGlobalPose(hand)).Basis.Orthonormalized();
		Basis handRest = _skeleton.GetBoneGlobalRest(hand).Basis.Orthonormalized();
		Vector3 along = (handNow * bodyFingers).Normalized(), inside = (handNow * bodyPalm).Normalized();
		Vector3 curl = along.Cross(inside).Normalized();

		for (int f = 0; f < 5; f++)
		{
			if (fp[f] == null || body[f] == null || body[f][3] < 0) continue;
			Vector3[] seg = _rig.FingerSegments(fp[f]);
			var want = new Vector3[3];
			for (int k = 0; k < 3; k++)
			{
				Vector3 d = (turn * seg[k]).Normalized();
				if (f == 0)
				{
					want[k] = k > 0 && thumbForward != Vector3.Zero ? d.Slerp(thumbForward.Normalized(), ThumbForward).Normalized() : d;
					continue;
				}
				// A falange do personagem na pose de descanso (dedos quase retos),
				// na mão de agora, dobrada até o ângulo da de lá.
				Vector3 restA = _skeleton.GetBoneGlobalRest(body[f][k]).Origin, restB = _skeleton.GetBoneGlobalRest(body[f][k + 1]).Origin;
				Vector3 rest = (handNow * (handRest.Transposed() * (skeleton.Basis.Orthonormalized() * (restB - restA)))).Normalized();
				float target = Bent(d, rigAlong, rigCurl) + extra * (k + 1) / 3f;
				want[k] = Turned(rest, curl, target - Bent(rest, along, curl));
			}

			// Em volta de uma bola (a granada): o dedo fecha ou abre até a
			// ponta encostar nela, sem nenhuma junta entrar.
			if (ballRadius > 0f)
			{
				if (f > 0) Wrap(body[f], want, curl, ball, ballRadius);
				else
				{
					// O polegar: no plano dele ou no da mão, o que encostar melhor.
					Vector3 own = ThumbAxis(want, curl);
					Vector3[] tryOwn = (Vector3[])want.Clone(), tryHand = (Vector3[])want.Clone();
					// E deitado sobre a bola, para a frente da mão (não em pé).
					Vector3 lean = (along + inside).Normalized();
					float a = Wrap(body[f], tryOwn, own, ball, ballRadius, lean), c = Wrap(body[f], tryHand, curl, ball, ballRadius, lean);
					System.Array.Copy(a <= c ? tryOwn : tryHand, want, 3);
				}
			}

			for (int k = 0; k < 3; k++)
			{
				int bone = body[f][k], child = body[f][k + 1];
				int parent = _skeleton.GetBoneParent(bone);
				if (parent < 0) break;
				skeleton = _skeleton.GlobalTransform;
				Transform3D pose = skeleton * _skeleton.GetBoneGlobalPose(bone);
				Vector3 now = Bone(skeleton, child) - pose.Origin;
				Vector3 aim = weight < 1f ? now.Normalized().Slerp(want[k], weight) : want[k];
				Quaternion turnBone = ArmIK.Arc(now, aim);
				Basis parentRotation = (skeleton * _skeleton.GetBoneGlobalPose(parent)).Basis.Orthonormalized();
				Basis next = new Basis(turnBone) * pose.Basis.Orthonormalized();
				Write(bone, (parentRotation.Transposed() * next).Orthonormalized().GetRotationQuaternion());
			}
		}
	}

	/// <summary>O eixo em que o polegar dobra: o plano das falanges dele.</summary>
	private static Vector3 ThumbAxis(Vector3[] d, Vector3 fallback)
	{
		Vector3 own = d[0].Cross(d[1]) + d[1].Cross(d[2]);
		return own.Length() > 0.1f ? own.Normalized() : fallback;
	}

	/// <summary>Fecha (ou abre) um dedo em volta de uma bola: a ponta
	/// encostada nela (mais a grossura do dedo), nenhuma junta dentro.
	/// Devolve o quanto ficou longe disso.</summary>
	private float Wrap(int[] chain, Vector3[] want, Vector3 axis, Vector3 ball, float radius, Vector3 lean = default)
	{
		Transform3D skeleton = _skeleton.GlobalTransform;
		Vector3 k0 = Bone(skeleton, chain[0]);
		float l0 = k0.DistanceTo(Bone(skeleton, chain[1]));
		float l1 = Bone(skeleton, chain[1]).DistanceTo(Bone(skeleton, chain[2]));
		float l2 = Bone(skeleton, chain[2]).DistanceTo(Bone(skeleton, chain[3]));
		float skin = radius + FingerThickness;

		float best = 0f, bestCost = float.MaxValue;
		for (float a = -0.8f; a <= 1.8f; a += 0.04f)
		{
			Vector3 p1 = k0 + Turned(want[0], axis, a / 3f) * l0;
			Vector3 p2 = p1 + Turned(want[1], axis, a * 2f / 3f) * l1;
			Vector3 p3 = p2 + Turned(want[2], axis, a) * l2;
			float cost = Mathf.Abs(p3.DistanceTo(ball) - skin);
			foreach (Vector3 p in new[] { p1, p2, p3 })
				cost += Mathf.Max(0f, skin - p.DistanceTo(ball)) * 4f;
			if (lean != Vector3.Zero) cost += (1f - (p3 - k0).Normalized().Dot(lean)) * 0.03f;
			if (cost < bestCost) { bestCost = cost; best = a; }
		}
		for (int k = 0; k < 3; k++) want[k] = Turned(want[k], axis, best * (k + 1) / 3f);
		return bestCost;
	}

	/// <summary>O meio da palma do corpo e para onde ela olha.</summary>
	private bool Palm(bool right, out Vector3 palm, out Vector3 inside)
	{
		palm = inside = Vector3.Zero;
		int wrist = right ? _rightHand : _leftHand, middle = right ? _rightMiddle : _leftMiddle;
		if (!BodyHand(right, out _, out Vector3 palmLocal, out _)) return false;
		Transform3D skeleton = _skeleton.GlobalTransform;
		Transform3D pose = skeleton * _skeleton.GetBoneGlobalPose(wrist);
		palm = pose.Origin.Lerp(Bone(skeleton, middle), 0.6f);
		inside = (pose.Basis.Orthonormalized() * palmLocal).Normalized();
		return true;
	}

	/// <summary>O raio da granada do conjunto (m), e a grossura de um dedo
	/// do personagem até o osso.</summary>
	[Export] public float GrenadeRadius = 0.036f;
	[Export] public float FingerThickness = 0.012f;

	/// <summary>O quanto uma falange dobra (radianos): o ângulo dela, a
	/// partir da direção da mão, em torno do eixo em que os dedos fecham.</summary>
	private static float Bent(Vector3 direction, Vector3 along, Vector3 curl)
	{
		Vector3 flat = direction - curl * direction.Dot(curl);
		return Mathf.Atan2(curl.Dot(along.Cross(flat)), along.Dot(flat));
	}

	/// <summary>O quanto os polegares deitam para a frente na pistola (0 a 1).</summary>
	[Export(PropertyHint.Range, "0,1")] public float ThumbForward = 0.6f;

	/// <summary>Quanto a mão de apoio fecha a mais no guarda-mão (radianos).</summary>
	[Export(PropertyHint.Range, "0,2")] public float SupportCurl = 0.7f;

	private static Vector3 Turned(Vector3 v, Vector3 axis, float angle) => Mathf.Abs(angle) < 1e-5f ? v : v.Rotated(axis, angle);

	// ------------------------------------------------------------ sem atravessar o corpo
	//
	// 19/11, Jhon: "armas não podem atravessar o personagem". Os clipes são
	// do X Bot; num corpo de ombros mais altos e braços mais curtos (o Barut)
	// o fuzil chega mais perto, e a coronha entrava no peito e a cabeça
	// inclinada do clipe, na arma. Duas correções, a cada quadro:
	//  * a coronha não entra no tronco: a arma desliza para a frente ao longo
	//    do cano até a ponta de trás ficar fora (a mão escorrega um pouco no
	//    punho, o que não se vê);
	//  * a arma não entra na cabeça: ela se afasta dela, de lado e para baixo,
	//    alguns centímetros (a mão envolve o punho, não se nota). A cabeça já
	//    vem um pouco mais erguida nos clipes desses corpos
	//    (CharacterAnimator.FittedHeadLift).
	//
	// Não se gira osso aqui: girar a cabeça por fora da árvore de animação,
	// a cada quadro, somava giro em cima de giro nos bots que correm, e o
	// Barut aparecia de rosto virado para trás (19/11, Jhon: "sem rosto").

	/// <summary>Raio do tronco (da coluna ao pescoço) que a coronha respeita.</summary>
	[Export] public float TorsoRadius = 0.12f;

	/// <summary>Quanto o alto da arma fica longe do centro da cabeça.</summary>
	[Export] public float HeadClearance = 0.105f;

	/// <summary>Até quanto a arma desliza para a frente, em metros.</summary>
	[Export] public float MaxSlide = 0.14f;

	/// <summary>Até quanto a arma se afasta da cabeça, em metros.</summary>
	[Export] public float MaxShift = 0.045f;

	private int _spine = -1, _neck = -1, _head = -1, _top = -1;
	private Vector3 _butt, _topBack, _topFront;

	private void FindBodyBones()
	{
		for (int i = 0; i < _skeleton.GetBoneCount(); i++)
		{
			string bone = _skeleton.GetBoneName(i);
			_boneByName[bone.StartsWith("mixamorig_") ? bone["mixamorig_".Length..] : bone] = i;
			if (bone.EndsWith("Spine1")) _spine = i;
			if (bone.EndsWith("Spine") || bone.EndsWith("Spine1") || bone.EndsWith("Spine2")) _spines.Add(i);
			if (bone.EndsWith("RightArm")) _rightUpper = i;
			else if (bone.EndsWith("RightForeArm")) _rightLower = i;
			else if (bone.EndsWith("LeftArm")) _leftUpper = i;
			else if (bone.EndsWith("LeftForeArm")) _leftLower = i;
			if (bone.EndsWith("Neck")) _neck = i;
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
