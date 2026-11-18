using Godot;
using System.Collections.Generic;

/// <summary>
/// Os braços animados: para as armas de fogo, o que o jogador vê é um
/// conjunto pronto — dois braços, a arma e as animações de um animador
/// (parado, sacar, tiro, recarga) —, convertido por
/// <c>tools/armas/blender/bracos.py</c> para <c>assets/armas/fp/</c>.
///
/// A ficha <c>bracos.json</c> diz que arma usa que conjunto e que trecho
/// de que clipe é cada animação. O conjunto já vem com o olho na origem:
/// entra na câmera sem ajuste.
///
/// Aqui só se escolhe o que tocar, seguindo a arma:
/// - **tiro**: o clipe do disparo, do começo, a cada tiro;
/// - **recarga**: o clipe tocado na velocidade que o faz terminar junto
///   com a recarga de verdade (<see cref="Weapon.ReloadDuration"/>) — o
///   de pente vazio quando o pente esvaziou; na escopeta, um ciclo por
///   cartucho;
/// - **golpe** e **golpe2** na faca, um de cada vez (a estocada e o corte
///   de lado), no tempo entre dois golpes;
/// - **sacar** ao trocar de arma, **andar** ao andar, **parado** no resto;
/// - **guardar** ao trocar para outra arma, quando o conjunto tem (o
///   AR-15): a arma desce antes de a outra subir.
///
/// O AR-15 recarrega com a animação do Jhon (16/11, 2,7 s, esticada para
/// o tempo da recarga). Conjunto sem animação de recarga recarrega por
/// código, por cima do parado — o conjunto inclina, o carregador cai e
/// volta, o ferrolho é puxado se o pente esvaziou (<see cref="RigPieces"/>).
///
/// A granada não tem conjunto próprio: vai na mão do braço da pistola, e
/// o arremesso é movimento de código. A faca sem "golpe" na ficha também
/// cai no golpe de código.
/// </summary>
public partial class ViewModel
{
	public const string RigSheetPath = "res://assets/armas/fp/bracos.json";

	/// <summary>Um trecho de um clipe: a animação "recarga" do M16 é do
	/// segundo 2,3 ao 4,5 da linha do tempo única dele.</summary>
	/// <summary><c>Contact</c>: no golpe, quanto depois do começo a lâmina
	/// chega no alvo (0 = o clipe não diz).</summary>
	private readonly record struct Clip(string Name, double From, double To, double Contact = 0)
	{
		public double Length => To - From;
	}

	/// <summary>Com os braços animados, o coice de código soma com o da
	/// animação: fica só uma fração.</summary>
	private const float RigKick = 0.3f;

	/// <summary>
	/// Os conjuntos foram animados para uma câmera de 50,5° na vertical
	/// (80° na horizontal); a do jogo é mais aberta. Para a arma aparecer
	/// do tamanho e no lugar em que o animador a pôs, o conjunto é esticado
	/// na largura e na altura da tela na razão entre as duas aberturas — é
	/// o mesmo que desenhá-lo com a câmera dele.
	/// </summary>
	[Export] public float RigFov = 50.5f;

	/// <summary>
	/// E encolhido na direção do olho: desenhado do mesmo tamanho na tela,
	/// mas mais perto — o cano fica a meio metro, não a um, e para de
	/// atravessar a parede quando se encosta nela.
	/// </summary>
	[Export] public float RigScale = 0.45f;

	/// <summary>Os conjuntos já carregados: trocar de arma não lê do disco.</summary>
	private static readonly Dictionary<string, PackedScene> RigScenes = new();

	private static Godot.Collections.Dictionary _rigSheet;

	private AnimationPlayer _rigPlayer;
	private readonly Dictionary<string, Clip> _rigClips = new();
	private string _rigPlaying = "";
	private double _rigBusy;
	private float _rigLastReload;

	/// <summary>Os braços animados estão em uso (arma de fogo com conjunto).</summary>
	public bool HasRig => _rigPlayer != null;

	/// <summary>A animação dos braços que está tocando ("parado", "tiro"...).
	/// Na recarga de código, "recarga".</summary>
	public string RigAnimation => _codeReload ? "recarga" : _rigPlaying;

	/// <summary>Quantas vezes uma animação dos braços começou do início.
	/// Parado ou andando sem parar, não pode crescer: o teste confere.</summary>
	public int RigStarts { get; private set; }

	private static Godot.Collections.Dictionary RigSheet()
	{
		if (_rigSheet != null) return _rigSheet;
		_rigSheet = ResourceLoader.Exists(RigSheetPath)
			? GD.Load<Json>(RigSheetPath).Data.AsGodotDictionary()
			: new Godot.Collections.Dictionary();
		return _rigSheet;
	}

	/// <summary>Carrega todos os conjuntos de uma vez, ao entrar na partida:
	/// a primeira troca de arma não engasga lendo o arquivo.</summary>
	private static void PreloadRigs()
	{
		foreach (Variant entry in RigSheet().Values)
			RigScene(entry.AsGodotDictionary()["arquivo"].AsString());
	}

	private static PackedScene RigScene(string path)
	{
		if (RigScenes.TryGetValue(path, out PackedScene scene) && scene != null) return scene;
		scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
		RigScenes[path] = scene;
		return scene;
	}

	/// <summary>A entrada da ficha para esta arma, ou null se ela não tem conjunto.</summary>
	private static Godot.Collections.Dictionary RigFor(string weaponId)
	{
		foreach (Variant entry in RigSheet().Values)
		{
			Godot.Collections.Dictionary rig = entry.AsGodotDictionary();
			if (rig["armas"].AsGodotArray().Contains(weaponId) && ResourceLoader.Exists(rig["arquivo"].AsString())) return rig;
		}
		return null;
	}

	private Node3D _rigObject;

	private void ForgetRig()
	{
		_pieces = null;
		ForgetRigHands();
		_rigSkeleton = null;
		_codeReload = false;
		_magazineBone = _boltBone = _gripBone = -1;
		_rigScene = null;
		_magazineNode = null;
		_rigObject = null;
		_rigPlayer = null;
		_rigClips.Clear();
		_rigPlaying = "";
		_rigBusy = 0;
		ForgetMuzzle();
	}

	/// <summary>Monta os braços animados desta arma. Falso se ela não tem
	/// conjunto — aí valem as mãos de sempre.</summary>
	private bool BuildRig(string id)
	{
		Godot.Collections.Dictionary rig = RigFor(id);
		if (rig == null) return false;

		PackedScene packed = RigScene(rig["arquivo"].AsString());
		if (packed == null) return false;
		var scene = packed.Instantiate<Node3D>();
		// A abertura de fora do zoom: o zoom fecha a câmera, e quem o
		// desfaz na arma é o _Process.
		// Cada conjunto pode ter sido animado para outra abertura (o AR-15,
		// para a da Unreal): a ficha diz.
		float rigFov = rig.ContainsKey("fov") ? (float)rig["fov"].AsDouble() : RigFov;
		float stretch = Mathf.Tan(Mathf.DegToRad(_baseFov * 0.5f)) / Mathf.Tan(Mathf.DegToRad(rigFov * 0.5f));
		scene.Scale = new Vector3(stretch, stretch, 1f) * RigScale;
		if (rig.ContainsKey("ajuste")) scene.Position = scene.Scale * ToVector(rig["ajuste"]);
		_held = new Node3D { Name = id };
		_held.AddChild(scene);
		AddChild(_held);
		_rigObject = scene.FindChild(id == Arsenal.GrenadeId ? "granada" : "faca", true, false) as Node3D;

		AttachRigHands(scene);
		foreach (Node node in scene.FindChildren("*", "MeshInstance3D", true, false))
			((MeshInstance3D)node).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		// A boca do cano e, um pouco atrás, a janela por onde sai a cápsula.
		Vector3 muzzle = ToVector(rig["cano"]);
		// Os pontos ficam no nó da arma (é lá que o resto do código os
		// procura), já na escala do conjunto.
		Vector3 eject = new(muzzle.X + 0.02f, muzzle.Y + 0.03f, muzzle.Z + Mathf.Min(0.55f, -muzzle.Z * 0.5f));
		_held.AddChild(new Marker3D { Name = WeaponModel.MuzzleName, Position = scene.Transform * muzzle });
		_held.AddChild(new Marker3D { Name = WeaponModel.EjectName, Position = scene.Transform * eject });
		AttachMuzzle(rig, scene);

		_rigPlayer = scene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		foreach (var (name, value) in rig["animacoes"].AsGodotDictionary())
		{
			Godot.Collections.Dictionary a = value.AsGodotDictionary();
			string clip = a["clipe"].AsString();
			if (_rigPlayer == null || !_rigPlayer.HasAnimation(clip)) continue;
			_rigClips[name.AsString()] = new Clip(clip, a["de"].AsDouble(), a["ate"].AsDouble(),
				a.ContainsKey("contato") ? a["contato"].AsDouble() : 0);

			// Parado, andar e correr se repetem sem emenda; o arquivo vem sem
			// laço, e sem ele o fim de cada volta dá um tranco.
			//
			// Linha do tempo única ("Scene", o M16 e a faca): o trecho de
			// parado não fecha em si mesmo (no M16 o fim fica a 10 cm do
			// começo, e a cada volta os braços pulavam). Vai e volta.
			if (clip == "Scene")
				_rigPlayer.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Pingpong;
			else if (name.AsString() is "parado" or "andar" or "correr" or "mira" or "mira_andar")
				_rigPlayer.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
		}

		if (_rigPlayer == null || !_rigClips.ContainsKey("parado"))
		{
			GD.PushWarning($"ViewModel: o conjunto de {id} não tem animação de parado.");
			RemoveChild(_held);
			_held.QueueFree();
			_held = null;
			ForgetRig();
			return false;
		}

		// Arma principal não tem animação de pegar (decisão do Jhon,
		// 13/11): sobe de baixo, como a pistola. Só a faca saca animado —
		// e o conjunto cuja ficha pede ("sacar": o AR-15, que o Jhon
		// animou para isso).
		bool asks = rig.ContainsKey("sacar") && rig["sacar"].AsBool();
		_rigKick = rig.ContainsKey("coice") ? (float)rig["coice"].AsDouble() : RigKick;
		_rigBob = rig.ContainsKey("balanco") && rig["balanco"].AsBool();
		_rigRaw = rig.ContainsKey("fiel") && rig["fiel"].AsBool();
		bool draws = _rigClips.ContainsKey("sacar") && (_weapon.HumanSlot != Slot.Primary || asks);
		if (!draws) _rigClips.Remove("sacar");
		_swingTail = null;
		double drawSeconds = draws ? Mathf.Min(_rigClips["sacar"].Length, RigDrawMax) : 0;
		PlayRig(draws ? "sacar" : "parado", drawSeconds);
		if (draws) _rigBusy = drawSeconds;

		FindPieces(scene);
		return true;
	}

	/// <summary>O saque animado mais longo que se toca, em segundos: a arma
	/// atira 0,2 s depois da troca, e o saque não pode ficar para trás.</summary>
	[Export] public float RigDrawMax = 0.4f;

	/// <summary>Quanto dura o guardar, antes de a outra arma subir, quando
	/// a arma não diz o dela (<see cref="WeaponDefinition.HolsterTime"/>).</summary>
	[Export] public float RigHolsterTime = 0.16f;

	/// <summary>O coice de código deste conjunto e se ele balança no passo
	/// mesmo tendo animação de andar (a ficha: "coice", "balanco").</summary>
	private float _rigKick = RigKick;
	private bool _rigBob;

	/// <summary>Conjunto fiel (a ficha: "fiel"): a animação como foi feita,
	/// sem nada de código por cima.</summary>
	private bool _rigRaw;

	private RigPieces _pieces;
	private Skeleton3D _rigSkeleton;
	private Basis _skeletonToRig;
	private int _magazineBone = -1, _boltBone = -1, _gripBone = -1;
	private Node3D _rigScene;
	private Node3D _magazineNode;
	private bool _codeReload;

	/// <summary>As peças que a recarga de código mexe: o carregador e o
	/// ferrolho, se o esqueleto do conjunto os tem (o AR-15 tem).</summary>
	private void FindPieces(Node3D scene)
	{
		if (_rigClips.ContainsKey("recarga") || _rigClips.ContainsKey("recarga_um")) return;
		var skeleton = scene.FindChildren("*", "Skeleton3D", true, false).Count > 0
			? (Skeleton3D)scene.FindChildren("*", "Skeleton3D", true, false)[0] : null;
		if (skeleton == null) return;

		_magazineBone = skeleton.FindBone("Magazine");
		_boltBone = skeleton.FindBone("Bolt");
		_gripBone = skeleton.FindBone("Grip");
		_rigScene = scene;
		if (_magazineBone < 0 && _boltBone < 0) return;

		_pieces = RigPieces.Attach(skeleton, scene);
		_rigSkeleton = skeleton;
		_skeletonToRig = RigPieces.Relative(scene, skeleton).Basis.Orthonormalized();
		_magazineNode = skeleton.FindChild("carregador", false, false) as Node3D;
	}

	/// <summary>Uma direção da tela (o conjunto entra sem girar: para baixo
	/// é −Y, para trás é +Z) no espaço do osso pai de <paramref name="bone"/>,
	/// na pose de agora — a arma está onde a animação a pôs.</summary>
	private Vector3 InParentOf(int bone, Vector3 rigDirection)
	{
		int parent = _rigSkeleton.GetBoneParent(bone);
		Basis pose = parent >= 0 ? _rigSkeleton.GetBoneGlobalPose(parent).Basis.Orthonormalized() : Basis.Identity;
		return (pose.Inverse() * (_skeletonToRig.Inverse() * rigDirection)).Normalized();
	}

	/// <summary>
	/// A recarga de código, para conjunto sem animação de recarga: inclina
	/// a arma (girando em torno dela, não do olho), o carregador cai e some,
	/// volta de baixo e encaixa; com o pente vazio, o ferrolho é puxado no
	/// fim. <paramref name="p"/> é o progresso da recarga de verdade.
	/// </summary>
	private Transform3D CodeReload(float p, bool on)
	{
		_codeReload = on && _pieces != null;
		if (!_codeReload)
		{
			if (_pieces != null)
			{
				_pieces.Offset(_magazineBone, Vector3.Zero);
				_pieces.Offset(_boltBone, Vector3.Zero);
				if (_magazineNode != null) _magazineNode.Visible = true;
			}
			return Transform3D.Identity;
		}

		float tilt = Span(p, 0f, 0.16f) * (1f - Span(p, 0.84f, 1f));
		float drop = Span(p, 0.2f, 0.36f) * (1f - Span(p, 0.58f, 0.74f));
		bool gone = p > 0.34f && p < 0.6f;
		float pull = _emptyReload ? Span(p, 0.78f, 0.84f) * (1f - Span(p, 0.86f, 0.9f)) : 0f;

		if (_magazineBone >= 0) _pieces.Offset(_magazineBone, InParentOf(_magazineBone, Vector3.Down) * 0.24f * drop);
		if (_boltBone >= 0) _pieces.Offset(_boltBone, InParentOf(_boltBone, Vector3.Back) * 0.05f * pull);
		if (_magazineNode != null) _magazineNode.Visible = !gone;

		// Gira em torno do punho (onde a mão direita segura), não do olho:
		// a arma inclina para mostrar o encaixe do carregador e desce um pouco.
		Vector3 pivot = _gripBone >= 0
			? _rigScene.Transform * (RigPieces.Relative(_rigScene, _rigSkeleton) * _rigSkeleton.GetBoneGlobalPose(_gripBone).Origin)
			: (_muzzle?.Position ?? new Vector3(0, -0.05f, -0.4f)) * 0.5f;
		var turn = new Basis(Basis.FromEuler(new Vector3(2f, 8f, 10f) * tilt * Mathf.DegToRad(1f)).GetRotationQuaternion());
		return new Transform3D(Basis.Identity, new Vector3(-0.01f, -0.04f, 0.015f) * tilt)
			* new Transform3D(Basis.Identity, pivot) * new Transform3D(turn, Vector3.Zero) * new Transform3D(Basis.Identity, -pivot);
	}

	/// <summary>Guardando a arma antes de a outra subir: quanto falta.</summary>
	private float _holster;

	/// <summary>Começa a guardar a arma, se o conjunto tem a animação. Falso:
	/// troca já.</summary>
	private bool StartHolster()
	{
		if (!HasRig || !_rigClips.ContainsKey("guardar") || _holster > 0f) return _holster > 0f;
		_codeReload = false;
		// O tempo é o da arma, o mesmo que ela espera para a próxima atirar.
		float holster = Arsenal.Get(_heldId)?.HolsterTime ?? 0f;
		if (holster <= 0f) holster = RigHolsterTime;
		PlayRig("guardar", holster, 0.05);
		_rigAiming = false;
		_rigBusy = holster;
		_holster = holster;
		return true;
	}

	private static Vector3 ToVector(Variant value)
	{
		var list = value.AsGodotArray();
		return new Vector3(list[0].AsSingle(), list[1].AsSingle(), list[2].AsSingle());
	}

	/// <summary>Toca uma animação do conjunto, do começo, em <paramref name="seconds"/>
	/// segundos (0 = no tempo dela).</summary>
	private void PlayRig(string name, double seconds, double blend = 0.08)
	{
		if (!_rigClips.TryGetValue(name, out Clip clip)) return;

		float speed = seconds > 0.01 ? (float)(clip.Length / seconds) : 1f;
		// Trecho do mesmo clipe não se mistura consigo mesmo: troca seca.
		bool sameClip = _rigPlayer.CurrentAnimation == clip.Name;
		_rigPlayer.PlaySection(clip.Name, clip.From, clip.To, sameClip ? 0 : blend, speed);
		_rigPlaying = name;
		RigStarts++;
	}

	private int _swing;

	/// <summary>Em quanto tempo a lâmina chega no alvo depois do clique. O
	/// dano da faca sai no clique; a armada do clipe (0,2 s) corre nisto
	/// para o golpe na tela bater com o golpe de verdade.</summary>
	[Export] public float RigSwingLead = 0.07f;

	/// <summary>O resto do golpe (a volta da lâmina), para tocar quando a
	/// armada acabar: o trecho e em quanto tempo.</summary>
	private Clip? _swingTail;
	private double _swingTailIn, _swingTailSeconds;

	/// <summary>O golpe da faca animado: alterna os golpes do conjunto.
	/// A lâmina chega em <see cref="RigSwingLead"/> e a volta toca inteira,
	/// no tempo do clipe — só encolhe se não couber no intervalo entre
	/// dois golpes. Falso se o conjunto não tem golpe — aí vale o golpe de
	/// código.</summary>
	private bool RigSwing()
	{
		if (!HasRig) return false;

		string name = _swing % 2 == 1 && _rigClips.ContainsKey("golpe2") ? "golpe2" : "golpe";
		if (!_rigClips.TryGetValue(name, out Clip clip)) return false;

		_swing++;
		double lead = clip.Contact > 0 ? Mathf.Min(RigSwingLead, clip.Contact) : 0;
		if (lead <= 0)
		{
			double seconds = Mathf.Min(clip.Length, _weapon.KnifeCooldown);
			PlayRig(name, seconds, 0.05);
			_rigBusy = seconds;
			_swingTail = null;
			return true;
		}

		// A armada, corrida; depois a volta.
		double tail = Mathf.Min(clip.Length - clip.Contact, _weapon.KnifeCooldown - lead);
		_rigClips["_armada"] = new Clip(clip.Name, clip.From, clip.From + clip.Contact);
		PlayRig("_armada", lead, 0.03);
		_rigPlaying = name;
		_swingTail = new Clip(clip.Name, clip.From + clip.Contact, clip.To);
		_swingTailIn = lead;
		_swingTailSeconds = tail;
		_rigBusy = lead + tail;
		return true;
	}

	private void RigFired()
	{
		if (!HasRig) return;

		// Na mira, o tiro da mira: a arma fica no olho.
		string name = _rigAiming && _rigClips.ContainsKey("mira_tiro") ? "mira_tiro" : "tiro";
		if (!_rigClips.TryGetValue(name, out Clip shot)) return;

		// Atirou no meio do caminho (subindo ou descendo): a arma chega um pouco mais devagar.
		bool moving = _rigPlaying is "mirar" or "desmirar" && _rigPlayer.IsPlaying();
		PlayRig(name, 0, moving ? 0.07 : 0.02);
		_rigBusy = shot.Length;
	}

	private void UpdateRig(float dt)
	{
		_rigBusy -= dt;
		FollowMuzzle();

		// A armada do golpe acabou: a volta da lâmina.
		if (_swingTail is Clip tail && (_swingTailIn -= dt) <= 0)
		{
			_swingTail = null;
			_rigPlayer.PlaySection(tail.Name, tail.From, tail.To, 0, (float)(tail.Length / Mathf.Max(_swingTailSeconds, 0.05)));
		}
		bool reloading = _weapon.IsReloading && _weapon.Held != null;

		// Faca e granada: o golpe e o arremesso são movimento de código
		// sobre o conjunto inteiro (o braço vai junto).
		Vector3 position = Vector3.Zero, rotation = Vector3.Zero;
		if (_heldId == Arsenal.GrenadeId)
		{
			bool winding = _weapon.IsGrenadeReady;
			if (!winding && _windup > 0.5f) _throw = 1f;
			_windup = Mathf.MoveToward(_windup, winding ? 1f : 0f, dt * (winding ? 4f : 10f));
			float w = Smooth(_windup), t = Mathf.Sin(_throw * Mathf.Pi);
			position += new Vector3(0.04f * w - 0.05f * t, 0.06f * w + 0.04f * t, 0.12f * w - 0.25f * t);
			rotation += new Vector3(30f * w - 40f * t, -12f * w, -14f * w);
			if (_rigObject != null) _rigObject.Visible = !(_throw > 0f && _throw < 0.75f);
		}
		Slash(ref position, ref rotation);
		bool codeReload = reloading && !_rigClips.ContainsKey("recarga") && !_rigClips.ContainsKey("recarga_um");
		_held.Transform = new Transform3D(Basis.FromEuler(rotation * Mathf.DegToRad(1f)), position)
			* CodeReload(_weapon.ReloadProgress, codeReload);

		if (reloading && !codeReload)
		{
			float p = _weapon.ReloadProgress;
			bool shells = _weapon.Held.ShellByShell;
			string name = shells && _rigClips.ContainsKey("recarga_um") ? "recarga_um"
				: _emptyReload && _rigClips.ContainsKey("recarga_vazia") ? "recarga_vazia"
				: "recarga";

			// Começou a recarga — ou, na escopeta, o cartucho seguinte.
			if (_rigPlaying != name || p < _rigLastReload - 0.2f)
				PlayRig(name, _weapon.ReloadDuration * (1f - p));
			_rigLastReload = p;
			_rigBusy = 0;
			// A recarga tira da mira: quando acabar, a arma está na cintura.
			_rigAiming = false;
			return;
		}

		_rigLastReload = 0f;
		if (UpdateRigAim()) return;
		if (_rigBusy > 0 && _rigPlayer.IsPlaying()) return;

		var flat = new Vector3(_body.Velocity.X, 0, _body.Velocity.Z);
		bool moving = _body.IsOnFloor() && flat.Length() > 0.5f;
		string rest = _rigAiming
			? moving && _rigClips.ContainsKey("mira_andar") ? "mira_andar" : "mira"
			: moving && _rigClips.ContainsKey("andar") ? "andar" : "parado";

		// Só troca entre parado e andar depois de o novo estado durar um
		// pouco. Descendo escada ou esbarrando numa quina o corpo perde o
		// chão por um quadro, e cada troca recomeçava a animação do
		// início: os braços ficavam reiniciando (Jhon, 13/11).
		bool resting = _rigPlaying is "parado" or "andar" or "mira" or "mira_andar";
		if (resting && _rigPlaying != rest && _rigPlayer.IsPlaying())
		{
			_rigRestHold += dt;
			if (_rigRestHold < RigRestDelay) return;
		}
		_rigRestHold = 0f;

		if (_rigPlaying != rest || !_rigPlayer.IsPlaying()) PlayRig(rest, 0, 0.2);
	}

	/// <summary>Quanto o corpo tem de ficar andando (ou parado) para os
	/// braços trocarem de animação, em segundos.</summary>
	private const float RigRestDelay = 0.18f;
	private float _rigRestHold;
}
