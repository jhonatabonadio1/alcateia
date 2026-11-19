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
/// No espectador o corpo visto é o aliado de verdade. No replay da câmera
/// de abate é a **cópia** de quem matou (<see cref="Bot.ReplayCopy"/>),
/// movida pela gravação (<see cref="ReplayTape"/>), que repete os
/// acontecimentos com o mesmo código do jogo — então tem o mesmo tiro, o
/// mesmo som e a mesma animação, sem ferir ninguém.
/// </summary>
public partial class KillCam
{
	/// <summary>Distância e folga da câmera de lobo (as do jogador:
	/// <see cref="Player.ThirdPersonDistance"/> e a mola da cena).</summary>
	private const float WolfDistance = 2.5f, WolfMargin = 0.2f;

	private const string BotScene = "res://scenes/bot.tscn";

	private Node3D _subject;
	private Hud _subjectHud;
	private ViewModel _subjectView;
	private Camera3D _subjectCamera;
	private bool _subjectFirst;
	private Bot _copy;

	private readonly List<(Node3D Owner, Node3D Node, bool Was)> _hidden = new();

	/// <summary>A visão está na primeira pessoa de alguém, com a arma na
	/// mão dele. Para os testes.</summary>
	public bool FirstPerson => _subjectFirst && _subjectView != null && IsInstanceValid(_subjectView);

	/// <summary>A arma na mão de quem se vê. Para os testes.</summary>
	public string PovWeapon => FirstPerson ? _subject?.GetNodeOrNull<Weapon>("Weapon")?.HeldId : null;

	/// <summary>O HUD de quem se vê. Para os testes.</summary>
	public Hud SubjectHud => _subjectHud;

	/// <summary>No replay, a cópia de quem matou. Para os testes.</summary>
	public Bot Copy => _copy;

	// ------------------------------------------------------------ quem se vê

	/// <summary>Passa a ver por este corpo (com o HUD dele). Null: ninguém.</summary>
	private void Watch(Node3D subject, bool replay = false)
	{
		if (subject == _subject) return;
		Unwatch();
		_subject = subject;
		if (subject == null) return;

		_subjectHud = new Hud { Name = "HudDeQuemSeVe", Subject = subject, ReplayFeed = replay };
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
	}

	/// <summary>A câmera da cabeça de quem se vê, com a arma na mão do jogo.</summary>
	private void EnterFirstPerson()
	{
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
			_subjectView = new ViewModel { Name = "VistaDeQuemSeVe" };
			_subjectCamera.AddChild(_subjectView);
		}
		_subjectCamera.MakeCurrent();
		_subjectFirst = true;
	}

	private void LeaveFirstPerson()
	{
		if (_subjectView != null && IsInstanceValid(_subjectView) && _subjectView.Name == "VistaDeQuemSeVe")
			_subjectView.QueueFree();
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
		float pitch = body.GetNodeOrNull<Node3D>("Head")?.Rotation.X ?? 0f;
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

		_camera.Near = 0.05f;
		_camera.GlobalTransform = new Transform3D(look, at);
		_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 8f));
	}

	// ------------------------------------------------------------ a cópia do replay

	/// <summary>Monta a cópia de quem matou: o mesmo time, o mesmo nome,
	/// fora dos grupos e sem colisão.</summary>
	private Bot MakeCopy(Node3D killer)
	{
		if (!ResourceLoader.Exists(BotScene)) return null;
		var copy = GD.Load<PackedScene>(BotScene).Instantiate<Bot>();
		Health health = killer.GetNodeOrNull<Health>("Health");
		copy.ReplayCopy = true;
		copy.Name = $"Replay_{killer.Name}";
		copy.Team = health?.Team ?? 0;
		copy.Callsign = health?.PublicName ?? "";
		killer.GetParent().AddChild(copy);
		copy.GlobalPosition = killer.GlobalPosition;
		if (copy.GetNodeOrNull<Node3D>("HealthBar") is Node3D bar) bar.Visible = false;
		return copy;
	}

	private void RemoveCopy()
	{
		if (_copy != null && IsInstanceValid(_copy))
		{
			if (_subject == _copy) Unwatch();
			_copy.QueueFree();
		}
		_copy = null;
	}

	/// <summary>Põe a cópia no instante gravado e repete o que aconteceu
	/// desde o quadro anterior.</summary>
	private void DriveCopy(ReplayTape tape, ReplayTape.Frame f, double from, double to)
	{
		if (_copy == null || !IsInstanceValid(_copy)) return;
		_copy.RemoteStatus(f.Hp, f.MaxHp, f.Shield, f.MaxShield, false, f.Primary, f.Secondary);
		_copy.RemoteSnapshot(f.At, f.Velocity, f.Yaw, f.Pitch, f.Grounded, (Form)f.Form, (Slot)f.Slot);
		_copy.GetNodeOrNull<FormController>("FormController")?.MirrorWp(f.Wp);
		Weapon weapon = _copy.GetNodeOrNull<Weapon>("Weapon");
		if (weapon == null) return;
		foreach (ReplayTape.Event e in tape.EventsBetween(_killer, from, to))
		{
			weapon.ReplayEvent(e.Kind, e.A, e.B);
			// O marcador de acerto toca para quem acertou: na câmera de
			// abate, quem acertou é ele.
			if (e.Kind == ReplayTape.Kind.Hit)
			{
				Sfx.Local(Sfx.First("marcador", "acerto"), -3f);
				if (e.A != 0) Sfx.Local("marcador_cabeca", 0f);
			}
		}
		weapon.ReplayMirror(f.Ammo, f.Charge, f.Grenades);

		if (_subjectHud != null && IsInstanceValid(_subjectHud))
			foreach (Kill kill in tape.KillsBetween(from, to))
				_subjectHud.ReplayKill(kill);
	}

	// ------------------------------------------------------------ esconder o corpo visto

	private static readonly string[] BodyParts = { "Human", "WolfVisual", "MeshInstance3D", "HealthBar", "RadioIcone" };

	private void HideBody(Node3D body)
	{
		if (body == null || !IsInstanceValid(body)) return;
		foreach (string part in BodyParts)
		{
			if (body.GetNodeOrNull<Node3D>(part) is not Node3D node) continue;
			bool known = false;
			foreach (var (_, n, _) in _hidden) if (n == node) { known = true; break; }
			if (!known) _hidden.Add((body, node, node.Visible));
			node.Visible = false;
		}
	}

	private void ShowBody(Node3D body)
	{
		for (int i = _hidden.Count - 1; i >= 0; i--)
		{
			var (owner, node, was) = _hidden[i];
			if (owner != body) continue;
			if (IsInstanceValid(node)) node.Visible = was;
			_hidden.RemoveAt(i);
		}
	}

	private void ShowBodies()
	{
		foreach (var (_, node, was) in _hidden)
			if (IsInstanceValid(node)) node.Visible = was;
		_hidden.Clear();
	}
}
