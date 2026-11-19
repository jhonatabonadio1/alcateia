using Godot;
using System.Collections.Generic;

/// <summary>
/// Câmera de morte: quem te matou fica com a **silhueta vermelha** — só
/// para você, vista através das paredes — e a câmera vira para ele, com
/// um cartão sobre ele com a patente e o nome (decisão do Jhon). Dura até
/// você renascer.
///
/// A silhueta é um <see cref="GeometryInstance3D.MaterialOverlay"/> nas
/// malhas do matador: uma segunda passada vermelha, sem teste de
/// profundidade, mais forte na borda. Fica no seu cliente e mais nenhum,
/// porque é você quem põe e tira. A câmera é própria, fora do corpo: o
/// corpo morto não gira, e o morto não tem mouse.
/// </summary>
public partial class KillCam : Node3D
{
	/// <summary>Quanto tempo a câmera fica no próprio corpo antes de virar
	/// para o matador: o bastante para ver que morreu.</summary>
	[Export] public float Hold = 0.35f;

	/// <summary>Rapidez da virada (quanto do caminho por segundo).</summary>
	[Export] public float TurnRate = 5f;

	/// <summary>Segundos da câmera de abate (a visão de quem te matou).
	/// Curta: o tempo de renascer já é espera bastante. Espaço pula.</summary>
	[Export] public float KillTime = 2.6f;

	/// <summary>Velocidade da câmera livre (m/s); com Shift, o dobro.</summary>
	[Export] public float FreeSpeed = 9f;

	private enum Phase { Kill, Mate, Free }
	private Phase _phase = Phase.Kill;
	private Node3D _mate;
	private Label _hint;
	private float _yaw, _pitch;

	/// <summary>A câmera de abate está passando agora (a escolha de
	/// conjunto espera por ela).</summary>
	public static bool InKillPhase { get; private set; }

	/// <summary>Em que modo está: "abate", "aliado" ou "livre". Para os testes.</summary>
	public string Mode => _phase switch { Phase.Kill => "abate", Phase.Mate => "aliado", _ => "livre" };

	/// <summary>Quem a câmera segue no modo aliado. Para os testes.</summary>
	public Node3D Watching => _phase == Phase.Mate ? _mate : null;

	private Node3D _killer;
	private Node3D _victim;
	private Camera3D _camera;
	private Transform3D _from;
	private float _fov = 75f;
	private double _time;
	private double _remark;
	private readonly Dictionary<GeometryInstance3D, Material> _marked = new();

	private CanvasLayer _tagLayer;
	private Control _tag;

	public static KillCam Begin(Node parent, Node3D victim, Node3D killer, Camera3D current)
	{
		var cam = new KillCam
		{
			Name = "KillCam",
			_killer = killer,
			_victim = victim,
			_from = current?.GlobalTransform ?? killer?.GlobalTransform ?? victim.GlobalTransform,
			_fov = current?.Fov ?? 75f
		};
		parent.AddChild(cam);
		return cam;
	}

	public override void _Ready()
	{
		TopLevel = true;
		_camera = new Camera3D { Fov = _fov };
		AddChild(_camera);
		_camera.GlobalTransform = _from;
		_camera.MakeCurrent();
		Mark();
		BuildTag();
		BuildHint();
		if (_killer == null) EnterSpectate();
		else InKillPhase = true;
	}

	private void BuildHint()
	{
		_tagLayer ??= new CanvasLayer { Layer = 8 };
		if (_tagLayer.GetParent() == null) AddChild(_tagLayer);
		_hint = HudStyle.Outlined(UiKit.Caption("", UiTokens.TextPrimary));
		_hint.HorizontalAlignment = HorizontalAlignment.Center;
		_hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
		_hint.OffsetLeft = -360; _hint.OffsetRight = 360;
		_hint.OffsetTop = -150; _hint.OffsetBottom = -128;
		_hint.MouseFilter = Control.MouseFilterEnum.Ignore;
		_tagLayer.AddChild(_hint);
	}

	// ------------------------------------------------------------ espectador

	/// <summary>
	/// Depois da câmera de abate (ou de pular com Espaço), enquanto não
	/// renasce, o morto assiste (pedido do Jhon, 18/11): a câmera fica
	/// **por cima de um aliado vivo**, Espaço ou clique passam para o
	/// próximo, e **F solta a câmera** para andar pelo mapa (WASD e mouse;
	/// Shift corre). F de novo volta aos aliados. Sem aliado vivo, fica
	/// livre.
	/// </summary>
	private void EnterSpectate()
	{
		InKillPhase = false;
		if (_tag != null) _tag.Visible = false;
		_phase = Phase.Mate;
		_mate = NextMate(null);
		if (_mate == null) EnterFree();
	}

	private void EnterFree()
	{
		_phase = Phase.Free;
		Vector3 e = _camera.GlobalBasis.GetEuler();
		_pitch = e.X;
		_yaw = e.Y;
	}

	private Node3D NextMate(Node3D after)
	{
		if (_victim == null || !IsInstanceValid(_victim) || _victim.GetNodeOrNull<Health>("Health") is not Health mine) return null;

		var mates = new List<Node3D>();
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is Node3D body && body != _victim && body.GetNodeOrNull<Health>("Health") is Health health
					&& health.Team == mine.Team && !health.IsDead)
					mates.Add(body);
		if (mates.Count == 0) return null;
		int at = after == null ? -1 : mates.IndexOf(after);
		return mates[(at + 1) % mates.Count];
	}

	private void Spectate(double delta, bool blocked)
	{
		bool next = !blocked && (Input.IsActionJustPressed("jump") || Input.IsActionJustPressed("attack"));
		bool toggle = !blocked && Input.IsActionJustPressed("spectate_free");

		if (_phase == Phase.Mate)
		{
			bool gone = _mate == null || !IsInstanceValid(_mate) || (_mate.GetNodeOrNull<Health>("Health")?.IsDead ?? true);
			if (next || gone) _mate = NextMate(gone ? null : _mate);
			if (toggle || _mate == null) { EnterFree(); return; }

			// Por cima e atrás do aliado, olhando para onde ele vai.
			Vector3 forward = -_mate.GlobalBasis.Z; forward.Y = 0;
			forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
			Vector3 perch = _mate.GlobalPosition + Vector3.Up * 1.9f - forward * 3.4f;
			float weight = Mathf.Min(1f, (float)delta * 6f);
			_camera.GlobalPosition = _camera.GlobalPosition.DistanceTo(perch) > 25f ? perch : _camera.GlobalPosition.Lerp(perch, weight);
			Vector3 target = _mate.GlobalPosition + Vector3.Up * 0.7f + forward * 2f;
			if (_camera.GlobalPosition.DistanceSquaredTo(target) > 0.01f)
			{
				Basis look = Basis.LookingAt(target - _camera.GlobalPosition, Vector3.Up);
				_camera.GlobalBasis = new Basis(_camera.GlobalBasis.GetRotationQuaternion().Slerp(look.GetRotationQuaternion(), weight));
			}
			_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, weight);
			string name = _mate.GetNodeOrNull<Health>("Health")?.PublicName ?? "";
			_hint.Text = Loc.F("ASSISTINDO {0}  ·  ESPAÇO: PRÓXIMO  ·  F: CÂMERA LIVRE", name);
			return;
		}

		// Livre: WASD no plano do olhar, mouse vira.
		if (toggle && NextMate(null) is Node3D first)
		{
			_phase = Phase.Mate;
			_mate = first;
			return;
		}

		_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 6f));
		_camera.GlobalBasis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0));
		if (!blocked)
		{
			Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
			float lift = (Input.IsActionPressed("jump") ? 1f : 0f) - (Input.IsActionPressed("crouch") ? 1f : 0f);
			Vector3 step = _camera.GlobalBasis * new Vector3(move.X, 0, move.Y) + Vector3.Up * lift;
			_camera.GlobalPosition += step * FreeSpeed * (Input.IsActionPressed("walk") ? 2f : 1f) * (float)delta;
		}
		_hint.Text = "CÂMERA LIVRE  ·  WASD E MOUSE  ·  F: VOLTAR AOS ALIADOS";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_phase != Phase.Free || UiState.AnyOpen || @event is not InputEventMouseMotion motion) return;
		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;
		const float sense = 0.0022f;
		_yaw -= motion.Relative.X * sense;
		_pitch = Mathf.Clamp(_pitch - motion.Relative.Y * sense, Mathf.DegToRad(-88f), Mathf.DegToRad(88f));
	}

	/// <summary>O cartão sobre o matador: patente e nome.</summary>
	private void BuildTag()
	{
		if (_killer?.GetNodeOrNull<Health>("Health") is not Health health) return;

		_tagLayer = new CanvasLayer { Layer = 8 };
		AddChild(_tagLayer);

		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.85f), new Color(UiTokens.Danger, 0.8f), 1, 0, UiTokens.S2, UiTokens.S1));
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		panel.AddChild(row);
		row.AddChild(RankBadge.Make(DuelCard.RankOf(health.PublicName, false), 26));
		VBoxContainer text = UiKit.Column(0);
		text.AddChild(UiKit.Text(health.PublicName, null, UiTokens.Danger));
		text.AddChild(UiKit.Caption(Ranks.NameOf(DuelCard.RankOf(health.PublicName, false))));
		row.AddChild(text);

		_tag = UiTheme.Apply(panel);
		_tag.Visible = false;
		_tagLayer.AddChild(_tag);
	}

	/// <summary>A placa de quem te matou, no alto da tela, durante a
	/// câmera de abate.</summary>
	private void PlaceTag()
	{
		if (_tag == null) return;
		_tag.Visible = _phase == Phase.Kill;
		if (!_tag.Visible) return;
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		_tag.Position = new Vector2((screen.X - _tag.Size.X) * 0.5f, screen.Y * 0.14f);
	}

	public override void _Process(double delta)
	{
		_time += delta;
		bool blocked = UiState.AnyOpen;

		if (_phase != Phase.Kill)
		{
			Spectate(delta, blocked);
			return;
		}

		bool skip = !blocked && _time > 0.15 && Input.IsActionJustPressed("jump");
		if (_killer == null || !IsInstanceValid(_killer) || _time >= KillTime || skip)
		{
			EnterSpectate();
			return;
		}

		// O matador pode trocar de forma enquanto você espera: o corpo
		// novo ganha a silhueta também.
		_remark -= delta;
		if (_remark <= 0) { _remark = 0.5; Mark(); }

		PlaceTag();
		_hint.Text = "CÂMERA DE ABATE  ·  ESPAÇO PULA";
		if (_time < Hold) return;

		// A visão de quem te matou (pedido do Jhon, 18/11): a câmera vai
		// para o olho dele — um palmo à frente do rosto, para não ficar
		// dentro da cabeça — e olha para onde você caiu.
		float weight = Mathf.Min(1f, (float)delta * TurnRate * 1.6f);
		Vector3 eye = _killer.GlobalPosition + Vector3.Up * 0.72f;
		Vector3 fallen = _victim != null && IsInstanceValid(_victim) ? _victim.GlobalPosition + Vector3.Up * 0.1f : eye - _killer.GlobalBasis.Z * 5f;
		Vector3 toward = fallen - eye;
		if (toward.LengthSquared() < 0.01f) return;
		Vector3 seat = eye + toward.Normalized() * Mathf.Min(0.5f, toward.Length() * 0.4f);

		_camera.GlobalPosition = _camera.GlobalPosition.Lerp(seat, weight);
		Basis look = Basis.LookingAt(fallen - _camera.GlobalPosition, Vector3.Up);
		Quaternion turned = _camera.GlobalBasis.GetRotationQuaternion().Slerp(look.GetRotationQuaternion(), weight);
		_camera.GlobalBasis = new Basis(turned);
		_camera.Fov = Mathf.Lerp(_camera.Fov, 68f, weight);
	}

	public void Reclaim() => _camera?.MakeCurrent();

	/// <summary>Tira a silhueta e devolve a câmera ao corpo.</summary>
	public void Finish()
	{
		foreach (var (mesh, previous) in _marked)
			if (IsInstanceValid(mesh)) mesh.MaterialOverlay = previous;
		_marked.Clear();
		QueueFree();
	}

	public override void _ExitTree()
	{
		InKillPhase = false;
		foreach (var (mesh, previous) in _marked)
			if (IsInstanceValid(mesh)) mesh.MaterialOverlay = previous;
		_marked.Clear();
	}

	private void Mark()
	{
		if (_killer == null || !IsInstanceValid(_killer)) return;

		foreach (Node node in _killer.FindChildren("*", nameof(MeshInstance3D), true, false))
		{
			if (node is not MeshInstance3D mesh || _marked.ContainsKey(mesh)) continue;
			// O brilho do ombro já é vermelho e é um quadrado montado no
			// shader: com a silhueta por cima virava um quadrado vermelho.
			if (mesh.IsInGroup(EnemyMarks.MarkGroup)) continue;
			_marked[mesh] = mesh.MaterialOverlay;
			mesh.MaterialOverlay = Silhouette.Killer;
		}
	}
}
