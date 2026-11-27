using Godot;
using System.Collections.Generic;

/// <summary>
/// O conjunto da primeira pessoa (os braços, a arma e as animações do Jhon,
/// <c>assets/armas/fp/</c>) como referência para o corpo em terceira pessoa
/// (21/11, Jhon: "use o posicionamento das armas, tamanho, posição dos
/// dedos, punho, ombro das animações primeira pessoa do player").
///
/// Cada corpo que segura uma arma com conjunto tem uma cópia dele, com os
/// braços escondidos: a arma que aparece é a do conjunto (o mesmo modelo, no
/// tamanho de verdade, com o carregador e o ferrolho que a animação mexe), e
/// as mãos do corpo vão onde as mãos do conjunto estão — o punho girado como
/// lá e cada dedo dobrado como lá (<see cref="HeldWeapon"/>). O ombro e o
/// cotovelo continuam os do corpo: no conjunto eles são arrumados para a
/// câmera (o ombro fica acima do olho), e copiá-los entortaria o braço.
///
/// Toca a mesma ficha (<c>bracos.json</c>) que a primeira pessoa: parado,
/// andar, tiro, recarga (no tempo da recarga), os golpes da faca, armar e
/// arremessar a granada, sacar.
/// </summary>
public partial class HeldRig : Node3D
{
	private readonly record struct Clip(string Name, double From, double To, double Contact)
	{
		public double Length => To - From;
	}

	public Skeleton3D Skeleton { get; private set; }
	public AnimationPlayer Player { get; private set; }
	public string Playing { get; private set; } = "";

	/// <summary>Os ossos dos braços do conjunto: mão, antebraço; e os dedos,
	/// cada um da base para a ponta (polegar, indicador, médio, anelar,
	/// mínimo), nos dois lados.</summary>
	public int HandR = -1, HandL = -1, LowerR = -1, LowerL = -1;
	public readonly int[][] FingersR = new int[5][], FingersL = new int[5][];

	private readonly Dictionary<string, Clip> _clips = new();
	private double _busy;
	private int _muzzleBone = -1;
	private Vector3 _muzzlePoint;
	private Node3D _muzzle;
	private Node3D _grenade;
	private double _releaseIn = -1;

	private static readonly string[] FingerNames = { "thumb", "index", "middle", "ring", "pinky" };

	/// <summary>O conjunto da arma, pronto para a terceira pessoa; nulo se a
	/// arma não tem conjunto (vale a arma de sempre, <see cref="WeaponModel"/>).</summary>
	public static HeldRig Create(string weaponId, uint layers)
	{
		Godot.Collections.Dictionary sheet = ViewModel.RigFor(weaponId);
		if (sheet == null) return null;
		PackedScene packed = ViewModel.RigScene(sheet["arquivo"].AsString());
		if (packed == null) return null;

		var scene = packed.Instantiate<Node3D>();
		var rig = new HeldRig { Name = "Conjunto", TopLevel = true };
		rig.AddChild(scene);

		var skeletons = scene.FindChildren("*", "Skeleton3D", true, false);
		rig.Skeleton = skeletons.Count > 0 ? (Skeleton3D)skeletons[0] : null;
		rig.Player = scene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (rig.Skeleton == null || rig.Player == null)
		{
			rig.QueueFree();
			return null;
		}

		Skeleton3D sk = rig.Skeleton;
		rig._gun = sk.FindBone("ik_hand_gun");
		// Quem anda a animação é o corpo, no quadro dele (HeldWeapon): a
		// pose lida é a deste quadro, e o que se muda nela depois fica.
		rig.Player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
		rig.HandR = sk.FindBone("hand_r");
		rig.HandL = sk.FindBone("hand_l");
		rig.LowerR = sk.FindBone("lowerarm_r");
		rig.LowerL = sk.FindBone("lowerarm_l");
		for (int f = 0; f < 5; f++)
		{
			rig.FingersR[f] = Chain(sk, FingerNames[f], "r");
			rig.FingersL[f] = Chain(sk, FingerNames[f], "l");
		}
		if (rig.HandR < 0 || rig.HandL < 0 || rig.FingersR[1] == null || rig.FingersR[2] == null || rig.FingersR[4] == null
			|| rig.FingersL[1] == null || rig.FingersL[2] == null || rig.FingersL[4] == null)
		{
			rig.QueueFree();
			return null;
		}

		// Os braços do conjunto não aparecem: os do corpo vão no lugar deles.
		foreach (Node node in scene.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)node;
			string name = mesh.Name.ToString().ToLowerInvariant();
			if (name.Contains("braco") || name.Contains("arm") || name.Contains("mao") || name.Contains("hand")) mesh.Visible = false;
			if (name.Contains("granada")) rig._grenade = mesh;
		}
		SkinCulling.Loosen(scene);
		WeaponModel.SetLayers(scene, layers);

		foreach (var (name, value) in sheet["animacoes"].AsGodotDictionary())
		{
			Godot.Collections.Dictionary a = value.AsGodotDictionary();
			string clip = a["clipe"].AsString();
			if (!rig.Player.HasAnimation(clip)) continue;
			rig._clips[name.AsString()] = new Clip(clip, a["de"].AsDouble(), a["ate"].AsDouble(),
				a.ContainsKey("contato") ? a["contato"].AsDouble() : a.ContainsKey("soltar") ? a["soltar"].AsDouble() : 0);
			// Como na primeira pessoa (ViewModel.BuildRig): o descanso se repete.
			if (clip == "Scene") rig.Player.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Pingpong;
			else if (name.AsString() is "parado" or "andar" or "correr" or "mira" or "mira_andar" or "armado")
				rig.Player.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
		}
		if (!rig._clips.ContainsKey("parado"))
		{
			rig.QueueFree();
			return null;
		}

		// A boca do cano no osso da arma (o tiro dos outros sai dali).
		rig._muzzle = new Marker3D { Name = WeaponModel.MuzzleName };
		rig.AddChild(rig._muzzle);
		if (sheet.ContainsKey("cano_osso"))
		{
			Godot.Collections.Dictionary where = sheet["cano_osso"].AsGodotDictionary();
			rig._muzzleBone = sk.FindBone(where["osso"].AsString());
			var p = where["ponto"].AsGodotArray();
			rig._muzzlePoint = new Vector3(p[0].AsSingle(), p[1].AsSingle(), p[2].AsSingle());
		}
		// O meio da granada, no osso que a leva (a ficha: "centro").
		rig._bolt = sk.FindBone("Bolt");
		if (sheet.ContainsKey("centro") && rig._bolt >= 0)
		{
			var c = sheet["centro"].AsGodotArray();
			rig._center = new Vector3(c[0].AsSingle(), c[1].AsSingle(), c[2].AsSingle());
			rig.HasCenter = true;
		}
		if (rig._muzzleBone < 0 && sheet.ContainsKey("cano"))
		{
			var p = sheet["cano"].AsGodotArray();
			rig._muzzle.Position = new Vector3(p[0].AsSingle(), p[1].AsSingle(), p[2].AsSingle());
		}

		return rig;
	}

	/// <summary>
	/// Já na cena: lê a mão direita no começo do "parado" (a pose do Blender
	/// foi feita com o conjunto nesse instante, e o corpo põe o conjunto por
	/// ela) e começa a sacar.
	/// </summary>
	public void Start()
	{
		Clip idle = _clips["parado"];
		Player.PlaySection(idle.Name, idle.From, idle.To, 0, 1f);
		Player.Seek(idle.From, true);
		HandPoints(true, out RestWristR, out RestMiddleR, out RestIndexR, out RestPinkyR);

		bool draws = _clips.ContainsKey("sacar");
		double drawn = Play(draws ? "sacar" : "parado", draws ? Mathf.Min(_clips["sacar"].Length, DrawMax) : 0);
		if (draws) _busy = drawn;
	}

	/// <summary>A mão direita no começo do "parado", no espaço do conjunto:
	/// pulso e o começo do médio, do indicador e do mínimo.</summary>
	public Vector3 RestWristR, RestMiddleR, RestIndexR, RestPinkyR;

	/// <summary>Uma mão do conjunto agora, no espaço do conjunto.</summary>
	public void HandPoints(bool right, out Vector3 wrist, out Vector3 middle, out Vector3 index, out Vector3 pinky)
	{
		Transform3D toRig = GlobalTransform.AffineInverse() * Skeleton.GlobalTransform;
		int[][] fingers = right ? FingersR : FingersL;
		wrist = toRig * Skeleton.GetBoneGlobalPose(right ? HandR : HandL).Origin;
		middle = toRig * Skeleton.GetBoneGlobalPose(fingers[2][0]).Origin;
		index = toRig * Skeleton.GetBoneGlobalPose(fingers[1][0]).Origin;
		pinky = toRig * Skeleton.GetBoneGlobalPose(fingers[4][0]).Origin;
	}

	/// <summary>Os três ossos de um dedo do conjunto, da base para a ponta.</summary>
	private static int[] Chain(Skeleton3D sk, string finger, string side)
	{
		int a = sk.FindBone($"{finger}_01_{side}"), b = sk.FindBone($"{finger}_02_{side}"), c = sk.FindBone($"{finger}_03_{side}");
		return a < 0 || b < 0 || c < 0 ? null : new[] { a, b, c };
	}

	/// <summary>O saque mais longo, em segundos.</summary>
	private const double DrawMax = 0.4;

	public bool Has(string name) => _clips.ContainsKey(name);
	public bool Busy => _busy > 0;

	/// <summary>Toca uma animação da ficha, do começo, em <paramref name="seconds"/>
	/// segundos (0 = no tempo dela).</summary>
	/// <summary>Devolve quanto ela dura.</summary>
	public double Play(string name, double seconds = 0, double blend = 0.1)
	{
		if (!_clips.TryGetValue(name, out Clip clip)) return 0;
		float speed = seconds > 0.01 ? (float)(clip.Length / seconds) : 1f;
		bool sameClip = Player.CurrentAnimation == clip.Name;
		// A troca: a animação de antes continua andando e some aos poucos
		// (como o AnimationPlayer faz), e o corpo mistura as duas do mesmo
		// jeito (HeldWeapon.HoldByClips).
		if (!sameClip && blend > 0.001 && Playing != "")
		{
			PrevPlaying = Playing;
			PrevTime = ClipTime;
			_prevSpeed = Speed;
			_blendLeft = _blendTotal = blend;
		}
		else _blendLeft = 0;
		Player.PlaySection(clip.Name, clip.From, clip.To, sameClip ? 0 : blend, speed);
		Playing = name;
		Speed = speed;
		return seconds > 0.01 ? seconds : clip.Length;
	}

	/// <summary>A velocidade em que a animação de agora toca.</summary>
	public double Speed { get; private set; } = 1;

	/// <summary>Onde a animação de agora está (s, desde o começo dela).</summary>
	public double ClipTime => _clips.TryGetValue(Playing, out Clip clip) ? Player.CurrentAnimationPosition - clip.From : 0;

	/// <summary>A animação que está saindo (numa troca) e onde ela está.</summary>
	public string PrevPlaying { get; private set; } = "";
	public double PrevTime { get; private set; }
	private double _prevSpeed = 1, _blendLeft, _blendTotal;

	/// <summary>Quanto a animação de agora já entrou na troca (1 = inteira).</summary>
	public float BlendWeight => _blendLeft > 0 && _blendTotal > 0 ? Mathf.Clamp(1f - (float)(_blendLeft / _blendTotal), 0f, 1f) : 1f;

	/// <summary>Ocupado com uma animação que não é de descanso até o fim dela.</summary>
	public void Hold(double seconds) => _busy = seconds;

	public double Length(string name) => _clips.TryGetValue(name, out Clip clip) ? clip.Length : 0;

	// ------------------------------------------------------------ o que acontece com a arma

	private static readonly string[] SwingOrder = { "golpe", "golpe2", "golpe4", "golpe3" };
	private int _swing;

	/// <summary>Atirou (ou golpeou, com a faca).</summary>
	public void Fired(bool knife, float knifeCooldown)
	{
		if (!knife && _aimed && Has("mira_tiro"))
		{
			_busy = Play("mira_tiro", 0, 0.02);
			return;
		}
		if (knife)
		{
			for (int k = 0; k < SwingOrder.Length; k++)
			{
				string name = SwingOrder[(_swing + k) % SwingOrder.Length];
				if (!_clips.TryGetValue(name, out Clip clip)) continue;
				_swing += k + 1;
				_busy = Play(name, Mathf.Min(clip.Length, Mathf.Max(knifeCooldown, 0.2f) * 1.6), 0.05);
				return;
			}
			return;
		}
		if (Has("tiro")) _busy = Play("tiro", 0, 0.02);
	}

	/// <summary>A recarga começou: no tempo dela.</summary>
	public void Reload(bool empty, float seconds)
	{
		string name = empty && Has("recarga_vazia") ? "recarga_vazia" : "recarga";
		if (!Has(name)) return;
		// recarregando, sai da mira (a arma: Weapon.CanAim)
		_aimed = false;
		_busy = Play(name, seconds > 0.1f ? seconds : 0, 0.12);
	}

	/// <summary>A recarga foi interrompida: volta ao descanso.</summary>
	public void StopReload()
	{
		if (Playing is "recarga" or "recarga_vazia") _busy = 0;
	}

	/// <summary>A granada saiu: o lance, e a da mão some quando ela sai.</summary>
	public void Thrown()
	{
		if (!_clips.TryGetValue("arremesso", out Clip clip)) return;
		double seconds = clip.Length / 1.25;
		_busy = Play("arremesso", seconds, Playing == "armado" ? 0.03 : 0.1);
		_releaseIn = clip.Contact > 0.01 ? clip.Contact / 1.25 : seconds * 0.4;
	}

	/// <summary>O lance está tocando (a arma seguinte espera).</summary>
	public bool Throwing => Playing == "arremesso" && _busy > 0;

	/// <summary>
	/// O quadro: a granada armada, o descanso (parado ou andando) quando
	/// nada está tocando, e a boca do cano no lugar.
	/// </summary>
	/// <param name="sprintPace">Correndo (Shift): a velocidade em que o
	/// "correr" toca, a das pernas do corpo; 0 fora da corrida.</param>
	public void Tick(double delta, bool grenadeReady, bool moving, bool aiming = false, float sprintPace = 0f)
	{
		Decide(delta, grenadeReady, moving, aiming, sprintPace > 0f);

		// A arma volta para onde a animação a deixou (Carry muda) e a
		// animação anda.
		if (_carried)
		{
			Skeleton.SetBonePosePosition(_gun, _gunPose.Origin);
			Skeleton.SetBonePoseRotation(_gun, _gunPose.Basis.GetRotationQuaternion());
			Skeleton.SetBonePoseScale(_gun, _gunScale);
			_carried = false;
		}
		// A corrida anda no passo do corpo; o resto, no tempo do clipe.
		double pace = Playing == "correr" && sprintPace > 0f ? sprintPace : 1;
		if (Playing == "correr") Speed = pace;
		Player.Advance(delta * pace);
		if (_blendLeft > 0)
		{
			_blendLeft -= delta;
			PrevTime += delta * _prevSpeed;
		}
	}

	/// <summary>Na mira (a animação de mirar já tocou).</summary>
	public bool Aimed => _aimed;
	private bool _aimed;

	private void Decide(double delta, bool grenadeReady, bool moving, bool aiming, bool sprinting)
	{
		_busy -= delta;
		if (_releaseIn >= 0 && (_releaseIn -= delta) < 0 && _grenade != null) _grenade.Visible = false;

		if (grenadeReady && Has("armar"))
		{
			if (Playing != "armar" && Playing != "armado") _busy = Play("armar", 0, 0.06);
			else if (Playing == "armar" && _busy <= 0 && Has("armado")) Play("armado", 0, 0.15);
			return;
		}

		if (_busy > 0) return;

		// A mira (Jhon, 22/11: "mira escopada também, de cada arma"): sobe
		// com a animação de mirar do conjunto, fica na de mira (parado ou
		// andando) e desce com a de desmirar.
		bool aim = aiming && Has("mira");
		if (aim != _aimed)
		{
			_aimed = aim;
			string change = aim ? "mirar" : "desmirar";
			if (Has(change))
			{
				_busy = Play(change, 0, 0.06);
				return;
			}
		}
		// Correndo (Shift): a arma abaixada, como na primeira pessoa.
		bool run = sprinting && !aim && Has("correr");
		string rest = run ? "correr"
			: aim ? (moving && Has("mira_andar") ? "mira_andar" : "mira")
			: moving && Has("andar") ? "andar" : "parado";
		// Só troca entre parado e andar depois de o novo durar um pouco (um
		// degrau tira o corpo do chão por um quadro). A corrida entra na hora.
		if (!run && Playing is "parado" or "andar" or "mira" or "mira_andar" && Playing != rest)
		{
			_restHold += delta;
			if (_restHold < 0.18) return;
		}
		_restHold = 0;
		// Abaixar a arma para correr e trazê-la de volta leva o mesmo que na
		// primeira pessoa (ViewModel.RigSprintIn e RigSprintOut).
		if (Playing != rest || !Player.IsPlaying()) Play(rest, 0, run ? 0.20 : Playing == "correr" ? 0.28 : 0.2);
	}

	private double _restHold;

	/// <summary>O osso que leva a arma inteira (o carregador, o ferrolho e a
	/// granada são filhos dele).</summary>
	private int _gun = -1;
	private Transform3D _gunPose;
	private Vector3 _gunScale = Vector3.One;
	private bool _carried;

	/// <summary>
	/// Leva a arma junto com a mão do corpo: <paramref name="carry"/> (no
	/// mundo) é o que leva a mão do conjunto para a do corpo — a arma de uma
	/// mão só vai com a mão, e nunca sai dela. Pode crescer (a granada, na
	/// mão maior do personagem). Vale até o próximo quadro.
	/// </summary>
	public void Carry(Transform3D carry)
	{
		if (_gun < 0) return;
		if (!_carried)
		{
			_gunPose = new Transform3D(new Basis(Skeleton.GetBonePoseRotation(_gun)), Skeleton.GetBonePosePosition(_gun));
			_gunScale = Skeleton.GetBonePoseScale(_gun);
		}
		Transform3D skeleton = Skeleton.GlobalTransform;
		Transform3D global = Skeleton.GetBoneGlobalPose(_gun);
		Transform3D moved = skeleton.AffineInverse() * carry * skeleton * global;
		int parent = Skeleton.GetBoneParent(_gun);
		Transform3D local = parent >= 0 ? Skeleton.GetBoneGlobalPose(parent).AffineInverse() * moved : moved;
		Skeleton.SetBonePosePosition(_gun, local.Origin);
		Skeleton.SetBonePoseRotation(_gun, local.Basis.Orthonormalized().GetRotationQuaternion());
		Skeleton.SetBonePoseScale(_gun, local.Basis.Scale);
		_carried = true;
	}

	private int _bolt = -1;
	private Vector3 _center;

	/// <summary>A arma tem um meio para segurar (a granada).</summary>
	public bool HasCenter { get; private set; }

	/// <summary>O meio da granada agora, no mundo.</summary>
	public Vector3 Center() => Skeleton.GlobalTransform * (Skeleton.GetBoneGlobalPose(_bolt) * _center);

	/// <summary>Para onde o cano aponta agora (no mundo); a frente do conjunto
	/// se a arma não tem cano.</summary>
	public Vector3 GunForward()
	{
		if (_muzzleBone < 0) return -GlobalBasis.Z.Normalized();
		Transform3D bone = Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(_muzzleBone);
		Vector3 forward = bone * _muzzlePoint - bone.Origin;
		return forward.LengthSquared() > 1e-10f ? forward.Normalized() : -GlobalBasis.Z.Normalized();
	}

	/// <summary>A boca do cano onde a animação pôs a arma agora.</summary>
	public void FollowMuzzle()
	{
		if (_muzzleBone < 0) return;
		_muzzle.GlobalPosition = Skeleton.GlobalTransform * (Skeleton.GetBoneGlobalPose(_muzzleBone) * _muzzlePoint);
	}

	/// <summary>Uma posição do conjunto no mundo.</summary>
	public Vector3 At(int bone) => (Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(bone)).Origin;

	/// <summary>A direção de cada segmento de um dedo, no mundo: base → meio,
	/// meio → ponta-1, e a última falange (pelo eixo do osso que mais segue o
	/// segmento anterior — o conjunto não tem o osso da ponta).</summary>
	public Vector3[] FingerSegments(int[] chain)
	{
		Vector3 a = At(chain[0]), b = At(chain[1]), c = At(chain[2]);
		Vector3 last = c - b;
		Basis tip = (Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(chain[2])).Basis.Orthonormalized();
		Vector3 best = last;
		float bestDot = -2f;
		foreach (Vector3 axis in new[] { tip.X, -tip.X, tip.Y, -tip.Y, tip.Z, -tip.Z })
		{
			float dot = axis.Dot(last.Normalized());
			if (dot > bestDot) { bestDot = dot; best = axis; }
		}
		return new[] { b - a, c - b, best };
	}
}
