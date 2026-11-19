using Godot;
using System.Collections.Generic;

/// <summary>
/// A visão de outro corpo, para a câmera de abate e o espectador: a câmera
/// no olho dele, com a **arma dele na mão** — o mesmo conjunto animado de
/// primeira pessoa do jogo (<see cref="ViewModel"/>), com as mãos padrão,
/// parado, andando e atirando (o clarão no cano) — e a mira dele (o giro
/// e a inclinação da cabeça). Lobo não tem arma: a câmera é a de lobo do
/// jogo, atrás dele, como o jogador vê o próprio lobo; no replay, o lobo
/// é uma cópia que refaz o caminho (o corpo de verdade está em outro
/// lugar).
///
/// Em primeira pessoa o corpo visto some da tela (a câmera está dentro da
/// cabeça dele); volta ao sair.
/// </summary>
public partial class KillCam
{
	/// <summary>Distância e folga da câmera de lobo (as do jogador:
	/// <see cref="Player.ThirdPersonDistance"/> e a mola da cena).</summary>
	private const float WolfDistance = 2.5f, WolfMargin = 0.2f;

	private Node3D _povHolder, _povScene, _ghost;
	private Transform3D _ghostOffset;
	private AnimationPlayer _povPlayer;
	private string _povWeapon;
	private readonly Dictionary<string, (string Clip, double From, double To)> _povClips = new();
	private string _povPlaying = "";
	private double _povShotLeft;
	private OmniLight3D _povFlash;
	private Vector3 _povMuzzle;

	// O que a visão mostra agora, para o HUD por cima.
	private ReplayTape.Frame _shown;
	private Node3D _shownBody;
	private bool _shownFirst;

	private readonly List<(Node3D Node, bool Was)> _hidden = new();
	private readonly Dictionary<ulong, (Weapon Weapon, Weapon.FiredEventHandler Count)> _listeners = new();
	private readonly Dictionary<ulong, int> _liveShots = new();

	/// <summary>A visão está em primeira pessoa, com a arma na mão. Para os testes.</summary>
	public bool FirstPerson => _shownFirst && _povHolder != null && _povHolder.Visible;

	/// <summary>A arma na mão da visão. Para os testes.</summary>
	public string PovWeapon => _povHolder != null && _povHolder.Visible ? _povWeapon : null;

	/// <summary>No replay de um lobo, a cópia que refaz o caminho. Para os testes.</summary>
	public Node3D Ghost => _ghost;

	private void BuildPov()
	{
		_povHolder = new Node3D { Name = "ArmaVista", Visible = false };
		_camera.AddChild(_povHolder);
		_povFlash = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.5f), LightEnergy = 0f, OmniRange = 3.5f, ShadowEnabled = false };
		_povHolder.AddChild(_povFlash);
	}

	private void HidePov()
	{
		if (_povHolder != null) _povHolder.Visible = false;
		_shownFirst = false;
	}

	/// <summary>
	/// A visão do corpo num quadro. <paramref name="shots"/>: tiros dados
	/// desde o quadro anterior. <paramref name="speed"/>: a velocidade no
	/// chão (negativa: lê do corpo). Verdadeiro se a câmera ficou dentro da
	/// cabeça (quem chama esconde o corpo).
	/// </summary>
	private bool Pov(Node3D body, ReplayTape.Frame frame, double delta, int shots, float speed = -1f, bool replay = false)
	{
		_shown = frame;
		_shownBody = body;
		if (speed < 0f && body is CharacterBody3D moving) speed = new Vector2(moving.Velocity.X, moving.Velocity.Z).Length();

		float eyeHeight = body.GetNodeOrNull<Node3D>("Head")?.Position.Y ?? 0.62f;
		Vector3 eye = frame.At + Vector3.Up * eyeHeight;
		Basis look = Basis.FromEuler(new Vector3(frame.Pitch, frame.Yaw, 0));

		if (frame.Wolf)
		{
			HidePov();
			// A câmera de lobo do jogador: atrás e acima, na mola, sem
			// atravessar parede.
			Vector3 back = look.Z * WolfDistance;
			var query = PhysicsRayQueryParameters3D.Create(eye, eye + back);
			var skip = new Godot.Collections.Array<Rid>();
			foreach (string group in Groups.Fighters)
				foreach (Node node in GetTree().GetNodesInGroup(group))
					if (node is CollisionObject3D fighter) skip.Add(fighter.GetRid());
			query.Exclude = skip;
			var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
			Vector3 at = hit.Count > 0 ? hit["position"].AsVector3() - back.Normalized() * WolfMargin : eye + back;
			_camera.Near = 0.05f;
			_camera.GlobalTransform = new Transform3D(look, at);
			_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 8f));

			if (replay) PlaceGhost(body, frame, speed);
			else RemoveGhost();
			return false;
		}

		RemoveGhost();
		_shownFirst = true;
		_camera.Near = 0.015f;
		_camera.GlobalTransform = new Transform3D(look, eye);
		_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 8f));
		ShowWeapon(frame.Weapon, delta, shots, speed);
		return true;
	}

	// ------------------------------------------------------------ a arma na mão

	private void ShowWeapon(string id, double delta, int shots, float speed)
	{
		if (string.IsNullOrEmpty(id)) { _povHolder.Visible = false; return; }
		if (id != _povWeapon && !BuildWeapon(id)) { _povHolder.Visible = false; _povWeapon = id; return; }
		if (_povScene == null) { _povHolder.Visible = false; return; }
		_povHolder.Visible = true;

		if (_povShotLeft > 0) _povShotLeft -= delta;
		if (shots > 0 && _povClips.ContainsKey("tiro"))
		{
			PlayClip("tiro", restart: true);
			_povShotLeft = _povClips["tiro"].To - _povClips["tiro"].From;
			_povFlash.Position = _povMuzzle;
			_povFlash.LightEnergy = 4f;
		}
		_povFlash.LightEnergy = Mathf.MoveToward(_povFlash.LightEnergy, 0f, (float)delta * 60f);

		if (_povShotLeft <= 0) PlayClip(speed > 0.6f && _povClips.ContainsKey("andar") ? "andar" : "parado");

		// O trecho que não é o clipe inteiro volta ao começo ao passar do fim.
		if (_povPlayer != null && _povClips.TryGetValue(_povPlaying, out var c) && _povPlaying != "tiro"
			&& _povPlayer.CurrentAnimationPosition > c.To)
			_povPlayer.Seek(c.From, true);
	}

	private void PlayClip(string name, bool restart = false)
	{
		if (_povPlayer == null || !_povClips.TryGetValue(name, out var c)) return;
		if (!restart && _povPlaying == name && _povPlayer.IsPlaying()) return;
		_povPlaying = name;
		_povPlayer.Play(c.Clip, restart ? 0.02 : 0.12);
		_povPlayer.Seek(c.From, true);
	}

	/// <summary>Monta o conjunto de primeira pessoa da arma (o mesmo da
	/// mão do jogador, pela ficha <c>bracos.json</c>), com as mãos padrão.</summary>
	private bool BuildWeapon(string id)
	{
		_povScene?.QueueFree();
		_povScene = null;
		_povPlayer = null;
		_povClips.Clear();
		_povPlaying = "";
		_povWeapon = id;

		Godot.Collections.Dictionary rig = ViewModel.RigFor(id);
		if (rig == null) return false;
		PackedScene packed = ViewModel.RigScene(rig["arquivo"].AsString());
		if (packed == null) return false;

		var scene = packed.Instantiate<Node3D>();
		float rigFov = rig.ContainsKey("fov") ? (float)rig["fov"].AsDouble() : 50.5f;
		float stretch = Mathf.Tan(Mathf.DegToRad(75f * 0.5f)) / Mathf.Tan(Mathf.DegToRad(rigFov * 0.5f));
		scene.Scale = new Vector3(stretch, stretch, 1f) * 0.45f;
		if (rig.ContainsKey("ajuste")) scene.Position = scene.Scale * Vec(rig["ajuste"]);
		_povHolder.AddChild(scene);
		ViewModel.PutStandardHands(scene);
		foreach (Node node in scene.FindChildren("*", nameof(MeshInstance3D), true, false))
			((MeshInstance3D)node).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		_povMuzzle = scene.Transform * Vec(rig["cano"]);

		_povPlayer = scene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		foreach (var (name, value) in rig["animacoes"].AsGodotDictionary())
		{
			Godot.Collections.Dictionary a = value.AsGodotDictionary();
			string clip = a["clipe"].AsString();
			if (_povPlayer == null || !_povPlayer.HasAnimation(clip)) continue;
			_povClips[name.AsString()] = (clip, a["de"].AsDouble(), a["ate"].AsDouble());
			if (clip != "Scene" && name.AsString() is "parado" or "andar")
				_povPlayer.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
		}

		_povScene = scene;
		PlayClip("parado");
		return true;
	}

	private static Vector3 Vec(Variant value)
	{
		Godot.Collections.Array a = value.AsGodotArray();
		return new Vector3((float)a[0].AsDouble(), (float)a[1].AsDouble(), (float)a[2].AsDouble());
	}

	// ------------------------------------------------------------ o lobo do replay

	private void PlaceGhost(Node3D body, ReplayTape.Frame frame, float speed)
	{
		if (_ghost == null || !IsInstanceValid(_ghost))
		{
			if (body.GetNodeOrNull<WolfVisual>("WolfVisual") is not WolfVisual visual) return;
			_ghost = visual.MakeGhost(out _ghostOffset);
			if (_ghost == null) return;
			GetParent().AddChild(_ghost);
			// Sem a silhueta vermelha do matador: no replay ele é só o lobo.
			foreach (Node node in _ghost.FindChildren("*", nameof(GeometryInstance3D), true, false))
				((GeometryInstance3D)node).MaterialOverlay = null;
		}

		_ghost.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(0, frame.Yaw, 0)), frame.At) * _ghostOffset;
		WolfVisual.GhostStep(_ghost, speed);
	}

	private void RemoveGhost()
	{
		if (_ghost != null && IsInstanceValid(_ghost)) _ghost.QueueFree();
		_ghost = null;
	}

	// ------------------------------------------------------------ esconder o corpo visto

	private static readonly string[] BodyParts = { "Human", "WolfVisual", "MeshInstance3D", "HealthBar", "RadioIcone" };

	private void HideBody(Node3D body, bool hide)
	{
		if (!hide) { ShowBodies(); return; }
		if (body == null || !IsInstanceValid(body)) return;
		foreach (string part in BodyParts)
		{
			if (body.GetNodeOrNull<Node3D>(part) is not Node3D node) continue;
			bool known = false;
			foreach (var (n, _) in _hidden) if (n == node) { known = true; break; }
			if (!known) _hidden.Add((node, node.Visible));
			node.Visible = false;
		}
	}

	private void ShowBodies()
	{
		foreach (var (node, was) in _hidden)
			if (IsInstanceValid(node)) node.Visible = was;
		_hidden.Clear();
	}

	// ------------------------------------------------------------ os tiros ao vivo

	/// <summary>Tiros que o corpo deu desde a última pergunta.</summary>
	private int LiveShots(Node3D body)
	{
		ulong id = body.GetInstanceId();
		if (!_listeners.ContainsKey(id) && body.GetNodeOrNull<Weapon>("Weapon") is Weapon weapon)
		{
			Weapon.FiredEventHandler count = () => _liveShots[id] = _liveShots.GetValueOrDefault(id) + 1;
			weapon.Fired += count;
			_listeners[id] = (weapon, count);
		}

		int shots = _liveShots.GetValueOrDefault(id);
		_liveShots[id] = 0;
		return shots;
	}

	public override void _Notification(int what)
	{
		if (what != NotificationPredelete) return;
		foreach (var (weapon, count) in _listeners.Values)
			if (IsInstanceValid(weapon)) weapon.Fired -= count;
		_listeners.Clear();
	}
}
