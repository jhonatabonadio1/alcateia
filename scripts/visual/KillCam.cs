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

	/// <summary>Quantos segundos antes do abate o replay mostra, e o
	/// tempo parado no fim, vendo a queda.</summary>
	[Export] public float ReplayLength = 3f;
	[Export] public float ReplayTail = 0.5f;

	private bool _replaying, _fell;
	private double _replayClock, _replayEnd;
	private Vector3 _lastVictimAt;
	private bool _hasLast;

	/// <summary>O replay gravado está passando (e não a visão ao vivo). Para os testes.</summary>
	public bool Replaying => _replaying;

	/// <summary>Velocidade da câmera livre (m/s); com Shift, o dobro.</summary>
	[Export] public float FreeSpeed = 9f;

	private enum Phase { Kill, Mate, First, Free }
	private Phase _phase = Phase.Kill;
	private Node3D _mate;
	private Label _hint;
	private float _yaw, _pitch;

	/// <summary>A câmera de abate está passando agora (a escolha de
	/// conjunto espera por ela).</summary>
	public static bool InKillPhase { get; private set; }

	/// <summary>Em que modo está: "abate", "aliado" ou "livre". Para os testes.</summary>
	public string Mode => _phase switch { Phase.Kill => "abate", Phase.Mate => "aliado", Phase.First => "primeira", _ => "livre" };

	/// <summary>Quem a câmera segue no modo aliado. Para os testes.</summary>
	public Node3D Watching => _phase is Phase.Mate or Phase.First ? _mate : null;

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
		else
		{
			InKillPhase = true;
			StartReplay();
		}
	}

	// ------------------------------------------------------------ o replay

	/// <summary>
	/// O replay do abate (pedido do Jhon, 18/11: "pode gravar a partida"):
	/// os últimos <see cref="ReplayLength"/> segundos, **do olho de quem te
	/// matou**. A fita (<see cref="ReplayTape"/>) dá o caminho dele e o
	/// seu; a câmera anda pelo caminho dele olhando para você, e o seu
	/// corpo é posto de pé e refaz o que fez até cair de novo no mesmo
	/// lugar. Os outros corpos aparecem onde estão agora — só os dois do
	/// abate são reencenados. Sem fita bastante (morreu logo ao nascer),
	/// fica a visão ao vivo do matador.
	/// </summary>
	private void StartReplay()
	{
		ReplayTape tape = ReplayTape.Current;
		if (tape == null || _victim is not Player victim) return;

		double have = Mathf.Min(tape.Recorded(_killer), tape.Recorded(_victim));
		if (have < 0.8) return;

		_replayEnd = tape.Now;
		_replayClock = _replayEnd - Mathf.Min(ReplayLength, have);
		_replaying = true;
		_fell = false;
		_hasLast = false;
		victim.ReplayHold(true);
		_camera.Near = 0.3f;
	}

	private void StopReplay()
	{
		if (!_replaying) return;
		_replaying = false;
		if (_victim is Player victim && IsInstanceValid(victim)) victim.ReplayHold(false);
		if (_camera != null) _camera.Near = 0.05f;
	}

	/// <summary>Um quadro do replay. Falso quando acabou.</summary>
	private bool ReplayStep(double delta)
	{
		ReplayTape tape = ReplayTape.Current;
		if (tape == null || _victim is not Player victim || !IsInstanceValid(victim)) return false;

		_replayClock += delta;
		double t = System.Math.Min(_replayClock, _replayEnd);
		if (!tape.Sample(_killer, t, out Vector3 killerAt, out _) || !tape.Sample(_victim, t, out Vector3 victimAt, out float victimYaw)) return false;

		if (!_fell)
		{
			Vector3 velocity = _hasLast && delta > 0 ? (victimAt - _lastVictimAt) / (float)delta : Vector3.Zero;
			_lastVictimAt = victimAt;
			_hasLast = true;
			victim.ReplayPose(victimAt, victimYaw, velocity);

			// O instante do abate: o corpo cai de novo, onde caiu.
			if (_replayClock >= _replayEnd)
			{
				_fell = true;
				victim.ReplayHold(false);
			}
		}

		Vector3 eye = killerAt + Vector3.Up * 0.72f;
		Vector3 target = (_fell ? victim.GlobalPosition : victimAt) + Vector3.Up * 0.3f;
		_camera.GlobalPosition = eye;
		if (eye.DistanceSquaredTo(target) > 0.04f)
		{
			Basis look = Basis.LookingAt(target - eye, Vector3.Up);
			float weight = _hasLast ? Mathf.Min(1f, (float)delta * 14f) : 1f;
			_camera.GlobalBasis = new Basis(_camera.GlobalBasis.GetRotationQuaternion().Slerp(look.GetRotationQuaternion(), weight));
		}
		_camera.Fov = 70f;
		return _replayClock < _replayEnd + ReplayTail;
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
	/// Depois do replay do abate (ou de pular com Espaço), enquanto não
	/// renasce, o morto assiste (pedido do Jhon, 18/11): a câmera segue um
	/// aliado vivo — de cima ou em primeira pessoa — ou anda solta pelo
	/// mapa (<see cref="Spectate"/>). Sem aliado vivo, fica livre.
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

	private Node3D NextMate(Node3D after, int step = 1)
	{
		if (_victim == null || !IsInstanceValid(_victim) || _victim.GetNodeOrNull<Health>("Health") is not Health mine) return null;

		var mates = new List<Node3D>();
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is Node3D body && body != _victim && body.GetNodeOrNull<Health>("Health") is Health health
					&& health.Team == mine.Team && !health.IsDead)
					mates.Add(body);
		if (mates.Count == 0) return null;
		int at = after == null ? (step > 0 ? -1 : 0) : mates.IndexOf(after);
		return mates[((at + step) % mates.Count + mates.Count) % mates.Count];
	}

	/// <summary>
	/// O espectador, quadro a quadro. **Clique esquerdo: próximo aliado;
	/// direito: o anterior. Espaço troca a visão**: de cima → primeira
	/// pessoa → câmera livre → de cima (pedido do Jhon, 18/11). O F
	/// também solta e prende a câmera livre.
	/// </summary>
	private void Spectate(double delta, bool blocked)
	{
		bool next = !blocked && Input.IsActionJustPressed("attack");
		bool previous = !blocked && Input.IsActionJustPressed("special");
		bool cycle = !blocked && Input.IsActionJustPressed("jump");
		bool toggle = !blocked && Input.IsActionJustPressed("spectate_free");

		if (_phase is Phase.Mate or Phase.First)
		{
			bool gone = _mate == null || !IsInstanceValid(_mate) || (_mate.GetNodeOrNull<Health>("Health")?.IsDead ?? true);
			if (gone) _mate = NextMate(null);
			else if (next) _mate = NextMate(_mate, 1);
			else if (previous) _mate = NextMate(_mate, -1);
			if (toggle || _mate == null || (cycle && _phase == Phase.First)) { EnterFree(); return; }
			if (cycle) _phase = Phase.First;

			Vector3 forward = -_mate.GlobalBasis.Z; forward.Y = 0;
			forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
			string name = _mate.GetNodeOrNull<Health>("Health")?.PublicName ?? "";
			float weight = Mathf.Min(1f, (float)delta * 6f);

			if (_phase == Phase.First)
			{
				// No olho do aliado, olhando para onde ele vira.
				Vector3 eye = _mate.GlobalPosition + Vector3.Up * 0.72f + forward * 0.1f;
				_camera.GlobalPosition = _camera.GlobalPosition.DistanceTo(eye) > 6f ? eye : _camera.GlobalPosition.Lerp(eye, Mathf.Min(1f, (float)delta * 22f));
				Basis ahead = Basis.LookingAt(forward, Vector3.Up);
				_camera.GlobalBasis = new Basis(_camera.GlobalBasis.GetRotationQuaternion().Slerp(ahead.GetRotationQuaternion(), Mathf.Min(1f, (float)delta * 14f)));
				_camera.Near = 0.3f;
				_camera.Fov = Mathf.Lerp(_camera.Fov, 80f, weight);
				_hint.Text = Loc.F("ASSISTINDO {0}  ·  PRIMEIRA PESSOA  ·  CLIQUES: TROCAR ALIADO  ·  ESPAÇO: VISÃO", name);
				return;
			}

			// Por cima e atrás do aliado, olhando para onde ele vai.
			_camera.Near = 0.05f;
			Vector3 perch = _mate.GlobalPosition + Vector3.Up * 1.9f - forward * 3.4f;
			_camera.GlobalPosition = _camera.GlobalPosition.DistanceTo(perch) > 25f ? perch : _camera.GlobalPosition.Lerp(perch, weight);
			Vector3 target = _mate.GlobalPosition + Vector3.Up * 0.7f + forward * 2f;
			if (_camera.GlobalPosition.DistanceSquaredTo(target) > 0.01f)
			{
				Basis look = Basis.LookingAt(target - _camera.GlobalPosition, Vector3.Up);
				_camera.GlobalBasis = new Basis(_camera.GlobalBasis.GetRotationQuaternion().Slerp(look.GetRotationQuaternion(), weight));
			}
			_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, weight);
			_hint.Text = Loc.F("ASSISTINDO {0}  ·  DE CIMA  ·  CLIQUES: TROCAR ALIADO  ·  ESPAÇO: VISÃO", name);
			return;
		}

		// Livre: WASD para onde se olha, mouse vira. Espaço (ou um clique)
		// volta aos aliados.
		if ((cycle || toggle || next || previous) && NextMate(null) is Node3D)
		{
			_phase = Phase.Mate;
			_mate = NextMate(previous ? null : _mate, previous ? -1 : 1) ?? NextMate(null);
			return;
		}

		_camera.Near = 0.05f;
		_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 6f));
		_camera.GlobalBasis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0));
		if (!blocked)
		{
			Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
			Vector3 step = _camera.GlobalBasis * new Vector3(move.X, 0, move.Y);
			_camera.GlobalPosition += step * FreeSpeed * (Input.IsActionPressed("walk") ? 2f : 1f) * (float)delta;
		}
		_hint.Text = "CÂMERA LIVRE  ·  WASD E MOUSE  ·  ESPAÇO: VOLTAR AOS ALIADOS";
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
		if (_killer == null || !IsInstanceValid(_killer) || (!_replaying && _time >= KillTime) || skip)
		{
			StopReplay();
			EnterSpectate();
			return;
		}

		if (_replaying)
		{
			PlaceTag();
			_hint.Text = "REPLAY DO ABATE  ·  ESPAÇO PULA";
			if (_time < Hold) return;
			if (!ReplayStep(delta))
			{
				StopReplay();
				EnterSpectate();
			}
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
		StopReplay();
		foreach (var (mesh, previous) in _marked)
			if (IsInstanceValid(mesh)) mesh.MaterialOverlay = previous;
		_marked.Clear();
		QueueFree();
	}

	public override void _ExitTree()
	{
		InKillPhase = false;
		StopReplay();
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
