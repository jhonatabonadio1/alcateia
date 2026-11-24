using Godot;
using System.Collections.Generic;

/// <summary>
/// Ver pelos olhos de outro corpo, com **tudo o que a tela dele tem**
/// (pedido do Jhon, 19/11: "o tiro, o barulho, as recargas, tudo,
/// absolutamente tudo que tem na minha câmera; o mesmo HUD da minha"):
///
/// - a câmera é a dele: a da cabeça, em primeira pessoa, com a **arma na
///   mão do jogo** (<see cref="ViewModel"/>, o mesmo nó do jogador, ligado
///   à arma e à forma dele — recuo, clarão, recarga, coronhada, troca de
///   arma, tudo); de lobo, a câmera de lobo do jogo, atrás dele;
/// - o HUD é o do jogo (<see cref="Hud"/>, ligado à vida, à arma e à forma
///   dele): vida, escudo, munição, especial, virar lobo, mira, marcador de
///   acerto, feed com ele como "você", minimapa dele;
/// - o som sai da arma dele, que está ali.
///
/// O corpo visto é o aliado de verdade, e os sinais da arma e da vida
/// dele chegam direto ao HUD e à arma na mão.
/// </summary>
public partial class KillCam
{
	/// <summary>Distância e folga da câmera de lobo (as do jogador:
	/// <see cref="Player.ThirdPersonDistance"/> e a mola da cena).</summary>
	private const float WolfDistance = 2.5f, WolfMargin = 0.2f;

	/// <summary>Marca da arma na mão posta por esta visão (e não a do jogador).</summary>
	private const string LentView = "vista_emprestada";

	private Node3D _subject;
	private Hud _subjectHud;
	private ViewModel _subjectView;
	private Camera3D _subjectCamera;
	private bool _subjectFirst;

	/// <summary>De quem são os olhos da tela agora (a primeira pessoa de
	/// outro corpo). Null fora disso. O som dele vira o "seu"
	/// (<see cref="WorldAudio"/>).</summary>
	public static Node3D Eyes { get; private set; }

	/// <summary>A visão está na primeira pessoa de alguém, com a arma na
	/// mão dele. Para os testes.</summary>
	public bool FirstPerson => _subjectFirst && _subjectView != null && IsInstanceValid(_subjectView);

	/// <summary>A arma na mão de quem se vê. Para os testes.</summary>
	public string PovWeapon => FirstPerson ? _subject?.GetNodeOrNull<Weapon>("Weapon")?.HeldId : null;

	/// <summary>O HUD de quem se vê. Para os testes.</summary>
	public Hud SubjectHud => _subjectHud;

	// ------------------------------------------------------------ quem se vê

	/// <summary>Passa a ver por este corpo (com o HUD dele). Null: ninguém.</summary>
	private void Watch(Node3D subject)
	{
		if (subject == _subject || _cleaned) return;
		Unwatch();
		_subject = subject;
		if (subject == null) return;

		_subjectHud = new Hud { Name = "HudDeQuemSeVe", Subject = subject };
		GetParent().AddChild(_subjectHud);
	}

	private void Unwatch()
	{
		LeaveFirstPerson();
		if (_subjectHud != null && IsInstanceValid(_subjectHud)) _subjectHud.QueueFree();
		_subjectHud = null;
		if (_subject != null && IsInstanceValid(_subject)) ShowBody(_subject);
		_subject = null;
	}

	/// <summary>
	/// Um quadro da visão de quem se vê. <paramref name="above"/>: a vista
	/// de cima (no espectador), em vez da primeira pessoa.
	/// </summary>
	private void ViewSubject(double delta, bool above = false)
	{
		if (_subject == null || !IsInstanceValid(_subject)) return;
		bool wolf = _subject.GetNodeOrNull<FormController>("FormController")?.ShowsWolf ?? false;

		if (above || wolf)
		{
			LeaveFirstPerson();
			ShowBody(_subject);
			if (!_camera.Current) _camera.MakeCurrent();
			if (wolf && !above) WolfCamera(_subject, delta);
			return;
		}

		EnterFirstPerson();
		HideBody(_subject);
		StepKick(delta);
	}

	/// <summary>A câmera da cabeça de quem se vê, com a arma na mão do jogo.</summary>
	private void EnterFirstPerson()
	{
		if (_cleaned) return;
		if (_subjectFirst && _subjectCamera != null && IsInstanceValid(_subjectCamera))
		{
			if (!_subjectCamera.Current) _subjectCamera.MakeCurrent();
			return;
		}

		_subjectCamera = _subject.GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (_subjectCamera == null) return;
		_subjectCamera.Fov = 75f;
		if (_subjectCamera.GetNodeOrNull<ViewModel>("ViewModel") is ViewModel own) _subjectView = own;
		else
		{
			// Com o nome de sempre: a arma dele acha a ponta do cano por ele
			// (o tiro sai da arma na tela).
			// Anda sempre, mesmo que o corpo dele esteja parado (no fim da
			// partida, nos testes): sem isso a arma não subia para a mão.
			_subjectView = new ViewModel { Name = "ViewModel", ProcessMode = ProcessModeEnum.Always };
			_subjectView.SetMeta(LentView, true);
			_subjectCamera.AddChild(_subjectView);
		}
		_subjectCamera.MakeCurrent();
		_subjectFirst = true;
		Eyes = _subject;
		Weapon gun = _subject.GetNodeOrNull<Weapon>("Weapon");
		if (gun != null)
		{
			_kickWeapon = gun;
			gun.Fired += OnSubjectFired;
		}
	}

	// O tranco da câmera no tiro, como na sua (Player.Camera): o corpo de
	// outro não tem isso — a cabeça dele só segue a mira.
	private const float KickScale = 0.55f, KickMax = 4f, KickRecover = 9f;
	private Weapon _kickWeapon;
	private float _kick;

	private void OnSubjectFired()
	{
		if (_kickWeapon == null || !IsInstanceValid(_kickWeapon)) return;
		_kick = Mathf.Min(_kick + Mathf.DegToRad(_kickWeapon.Recoil * KickScale), Mathf.DegToRad(KickMax));
	}

	private void StepKick(double delta)
	{
		if (_subjectCamera == null || !IsInstanceValid(_subjectCamera)) return;
		_kick = Mathf.MoveToward(_kick, 0f, _kick * KickRecover * (float)delta + 0.0005f);
		_subjectCamera.Rotation = new Vector3(_kick, _subjectCamera.Rotation.Y, _subjectCamera.Rotation.Z);
	}

	private void LeaveFirstPerson()
	{
		if (_kickWeapon != null && IsInstanceValid(_kickWeapon)) _kickWeapon.Fired -= OnSubjectFired;
		_kickWeapon = null;
		if (_subjectCamera != null && IsInstanceValid(_subjectCamera))
			_subjectCamera.Rotation = new Vector3(0, _subjectCamera.Rotation.Y, _subjectCamera.Rotation.Z);
		_kick = 0;
		Eyes = null;
		if (_subjectView != null && IsInstanceValid(_subjectView) && _subjectView.HasMeta(LentView))
		{
			// Sai já do lugar: o nome fica livre e a arma dele para de
			// achar a ponta do cano nela.
			_subjectView.GetParent()?.RemoveChild(_subjectView);
			_subjectView.QueueFree();
		}
		_subjectView = null;
		_subjectFirst = false;
		_subjectCamera = null;
		if (_camera != null && IsInstanceValid(_camera) && IsInsideTree() && !_camera.Current) _camera.MakeCurrent();
	}

	/// <summary>A câmera de lobo do jogador: atrás e acima, na mola, sem
	/// atravessar parede.</summary>
	private void WolfCamera(Node3D body, double delta)
	{
		float eyeHeight = body.GetNodeOrNull<Node3D>("Head")?.Position.Y ?? 0.62f;
		// A inclinação da cabeça, no limite da câmera de lobo do jogador:
		// o bot mira para cima e para baixo bem mais que isso, e a câmera
		// ia parar olhando o teto.
		float pitch = Mathf.Clamp(body.GetNodeOrNull<Node3D>("Head")?.Rotation.X ?? 0f, Mathf.DegToRad(-45f), Mathf.DegToRad(30f));
		Vector3 eye = body.GlobalPosition + Vector3.Up * eyeHeight;
		Basis look = Basis.FromEuler(new Vector3(pitch, body.GlobalRotation.Y, 0));
		Vector3 back = look.Z * WolfDistance;

		var query = PhysicsRayQueryParameters3D.Create(eye, eye + back);
		var skip = new Godot.Collections.Array<Rid>();
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is CollisionObject3D fighter) skip.Add(fighter.GetRid());
		query.Exclude = skip;
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		Vector3 at = hit.Count > 0 ? hit["position"].AsVector3() - back.Normalized() * WolfMargin : eye + back;
		at = MapBounds.Clamp(GetTree(), at);

		_camera.Near = 0.05f;
		_camera.GlobalTransform = new Transform3D(look, at);
		_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 8f));
	}

	// ------------------------------------------------------------ esconder o corpo visto

	/// <summary>
	/// A camada de desenho dos corpos que a câmera não mostra (quem se vê
	/// em primeira pessoa; no replay, o corpo de verdade de quem matou). As
	/// câmeras desta visão não desenham essa camada; o resto do mundo, sim
	/// (a sombra continua).
	///
	/// Até 19/11 escondia trocando o <c>Visible</c> das partes e voltava o
	/// que estava antes — e se o corpo virou lobo no meio, voltava o humano
	/// à vista e o lobo escondido (o "lobo bugado" do espectador).
	/// </summary>
	public const uint WatchedLayer = 1u << 20;

	private static readonly string[] BodyParts = { "Human", "WolfVisual", "WolfFx", "MeshInstance3D", "HealthBar", "RadioIcone" };
	private readonly HashSet<Node3D> _hidden = new();
	private int _rehide;

	private void HideBody(Node3D body)
	{
		if (body == null || !IsInstanceValid(body)) return;
		bool fresh = _hidden.Add(body);
		CullWatched(_camera);
		CullWatched(_subjectCamera);
		// Partes novas aparecem (a arma trocada, o lobo): confere de novo
		// a cada poucos quadros.
		if (!fresh && --_rehide > 0) return;
		_rehide = 6;
		foreach (Node3D hidden in _hidden) Mark(hidden, true);
	}

	private void ShowBody(Node3D body)
	{
		if (body == null || !_hidden.Remove(body)) return;
		if (IsInstanceValid(body)) Mark(body, false);
	}

	private void ShowBodies()
	{
		foreach (Node3D body in _hidden)
			if (IsInstanceValid(body)) Mark(body, false);
		_hidden.Clear();
	}

	private static void CullWatched(Camera3D camera)
	{
		if (camera != null && IsInstanceValid(camera)) camera.CullMask &= ~WatchedLayer;
	}

	internal static void Mark(Node3D body, bool hide)
	{
		foreach (string part in BodyParts)
			if (body.GetNodeOrNull(part) is Node node) MarkTree(node, hide);
	}

	private const string LayersBefore = "camadas_antes_da_camera";

	/// <summary>A câmera desenha o que tiver qualquer camada dela: para
	/// sumir, o corpo fica **só** na camada escondida, e a de antes fica
	/// guardada para voltar.</summary>
	private static void MarkTree(Node node, bool hide)
	{
		if (node is VisualInstance3D visual && node is not Light3D)
		{
			if (hide && !visual.HasMeta(LayersBefore))
			{
				visual.SetMeta(LayersBefore, (long)visual.Layers);
				visual.Layers = WatchedLayer;
			}
			else if (!hide && visual.HasMeta(LayersBefore))
			{
				visual.Layers = (uint)visual.GetMeta(LayersBefore).AsInt64();
				visual.RemoveMeta(LayersBefore);
			}
		}
		foreach (Node child in node.GetChildren()) MarkTree(child, hide);
	}
}
