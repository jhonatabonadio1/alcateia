using Godot;
using System.Collections.Generic;

/// <summary>
/// O que o morto vê até renascer (pedido do Jhon, 18/11, com o Overwatch
/// de referência):
///
/// 1. **Morte** (1 s): a câmera fica no próprio corpo caindo.
/// 2. **Câmera de abate**: o **replay** dos últimos segundos, **pelo olho
///    de quem te matou** — com a arma dele na mão, a mira, os tiros e o
///    HUD dele (a vida, o escudo, a munição), o nome dele marcado no feed,
///    faixas escuras em cima e embaixo, "CÂMERA DE ABATE" escrito e uma
///    música por trás. Se ele era lobo, a câmera é a de lobo do jogo (em
///    terceira pessoa, atrás dele), com o lobo reencenado. O seu corpo é
///    posto de pé e refaz o caminho até cair no mesmo lugar. Sem fita
///    bastante, mostra a visão dele ao vivo. **Espaço pula.**
/// 3. **Espectador**: direto na **primeira pessoa de um aliado vivo**, com
///    o HUD dele. Clique esquerdo: próximo aliado; direito: o anterior.
///    **Espaço troca a visão**: primeira pessoa → de cima → câmera livre
///    (WASD e mouse, presa dentro do mapa, <see cref="MapBounds"/>) → de
///    volta. Aliado lobo: a câmera de lobo.
///
/// A escolha de conjunto só aparece na hora de renascer
/// (<see cref="LoadoutMenu"/>). Morto, o rádio não fala.
///
/// Quem te matou continua com a **silhueta vermelha** (só para você,
/// através das paredes) enquanto esta câmera existe.
///
/// As partes: as fases e a entrada aqui; a visão de outro corpo (a arma
/// na mão, a câmera de lobo, o lobo reencenado) em
/// <c>KillCam.Pov.cs</c>; o HUD por cima em <c>KillCam.Hud.cs</c>.
/// </summary>
public partial class KillCam : Node3D
{
	/// <summary>Segundos vendo o próprio corpo antes da câmera de abate.</summary>
	[Export] public float DeathTime = 1f;

	/// <summary>Sem fita: segundos da visão ao vivo de quem matou.</summary>
	[Export] public float KillTime = 3f;

	/// <summary>Quantos segundos antes do abate o replay mostra, e o
	/// tempo no fim, vendo a queda.</summary>
	[Export] public float ReplayLength = 3.2f;
	[Export] public float ReplayTail = 0.9f;

	/// <summary>Velocidade da câmera livre (m/s); com Shift, o dobro.</summary>
	[Export] public float FreeSpeed = 9f;

	/// <summary>A música da câmera de abate (dB).</summary>
	[Export] public float MusicDb = -6f;

	private enum Phase { Death, Kill, First, Above, Free }
	private Phase _phase = Phase.Death;

	private Node3D _killer, _victim, _mate;
	private Camera3D _camera;
	private Transform3D _from;
	private float _fov = 75f;
	private double _time, _remark;
	private float _yaw, _pitch;
	private AudioStreamPlayer _music;
	private readonly Dictionary<GeometryInstance3D, Material> _marked = new();

	// O replay.
	private bool _replaying, _fell, _hasLast;
	private double _replayClock, _replayEnd;
	private Vector3 _lastVictimAt, _lastKillerAt;

	/// <summary>A câmera de abate está passando agora (a escolha de
	/// conjunto espera por ela).</summary>
	public static bool InKillPhase { get; private set; }

	/// <summary>Quem a câmera mostra agora (o matador, um aliado), para o
	/// HUD marcar o nome no feed. Null fora disso.</summary>
	public static string Focus { get; private set; }

	/// <summary>Há uma câmera de morte na tela: o HUD de quem joga esconde
	/// a vida, a arma e a mira dele (o HUD de quem se vê toma o lugar).</summary>
	public static bool Active { get; private set; }

	/// <summary>Em que modo está: "morte", "abate", "primeira", "cima" ou
	/// "livre". Para os testes.</summary>
	public string Mode => _phase switch
	{
		Phase.Death => "morte", Phase.Kill => "abate", Phase.First => "primeira", Phase.Above => "cima", _ => "livre"
	};

	/// <summary>Quem a câmera segue no espectador. Para os testes.</summary>
	public Node3D Watching => _phase is Phase.First or Phase.Above ? _mate : null;

	/// <summary>O replay gravado está passando (e não a visão ao vivo). Para os testes.</summary>
	public bool Replaying => _replaying;

	public static KillCam Begin(Node parent, Node3D victim, Node3D killer, Camera3D current)
	{
		var cam = new KillCam
		{
			Name = "KillCam",
			_killer = killer,
			_victim = victim,
			_from = current?.GlobalTransform ?? victim.GlobalTransform,
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
		Active = true;
		Mark();
		BuildHud();
		BuildPov();
	}

	public void Reclaim() => _camera?.MakeCurrent();

	public void Finish()
	{
		Cleanup();
		QueueFree();
	}

	public override void _ExitTree() => Cleanup();

	private void Cleanup()
	{
		StopReplay();
		StopMusic();
		ShowBodies();
		InKillPhase = false;
		Focus = null;
		Active = false;
		foreach (var (mesh, previous) in _marked)
			if (IsInstanceValid(mesh)) mesh.MaterialOverlay = previous;
		_marked.Clear();
	}

	// ------------------------------------------------------------ as fases

	public override void _Process(double delta)
	{
		_time += delta;
		bool blocked = UiState.AnyOpen;

		// O matador pode trocar de forma enquanto você espera: o corpo
		// novo ganha a silhueta também.
		_remark -= delta;
		if (_remark <= 0) { _remark = 0.5; Mark(); }

		switch (_phase)
		{
			case Phase.Death:
				Focus = null;
				// Espaço já vai para o espectador; passou o segundo, o abate.
				if (!blocked && Input.IsActionJustPressed("jump")) { EnterSpectate(); break; }
				if (_time >= DeathTime)
				{
					if (_killer != null && IsInstanceValid(_killer)) EnterKill();
					else EnterSpectate();
				}
				break;

			case Phase.Kill:
				if (!blocked && Input.IsActionJustPressed("jump")) { EnterSpectate(); break; }
				if (!KillStep(delta)) EnterSpectate();
				break;

			default:
				Spectate(delta, blocked);
				break;
		}

		UpdateHud();
	}

	private void EnterKill()
	{
		_phase = Phase.Kill;
		_time = 0;
		InKillPhase = true;
		Focus = _killer.GetNodeOrNull<Health>("Health")?.PublicName;
		_music = Sfx.Music(this, "musica_camera_abate", MusicDb);
		StartReplay();
	}

	/// <summary>Um quadro da câmera de abate. Falso quando acabou.</summary>
	private bool KillStep(double delta)
	{
		if (_killer == null || !IsInstanceValid(_killer)) return false;
		if (_replaying) return ReplayStep(delta);

		// Sem fita: a visão de quem matou, ao vivo, por KillTime.
		HideBody(_killer, !Pov(_killer, ReplayTape.Read(_killer), delta, LiveShots(_killer)));
		return _time < KillTime;
	}

	private void EnterSpectate()
	{
		StopReplay();
		StopMusic();
		InKillPhase = false;
		_time = 0;
		_phase = Phase.First;
		_mate = NextMate(null);
		if (_mate == null) EnterFree();
	}

	private void EnterFree()
	{
		ShowBodies();
		HidePov();
		_phase = Phase.Free;
		Vector3 e = _camera.GlobalBasis.GetEuler();
		_pitch = e.X;
		_yaw = e.Y;
	}

	private void StopMusic()
	{
		if (_music == null || !IsInstanceValid(_music)) return;
		AudioStreamPlayer leaving = _music;
		_music = null;
		// Sai em fade: a música é filha desta câmera, então vai para a cena.
		leaving.Reparent(GetTree().Root);
		Tween fade = leaving.CreateTween();
		fade.TweenProperty(leaving, "volume_db", -60f, 0.8);
		fade.TweenCallback(Callable.From(leaving.QueueFree));
	}

	// ------------------------------------------------------------ o replay

	/// <summary>Começa o replay se há fita dos dois. Sem fita, fica a visão
	/// ao vivo (<see cref="KillStep"/>).</summary>
	private void StartReplay()
	{
		ReplayTape tape = ReplayTape.Current;
		if (tape == null || _victim is not Player victim) return;

		double have = Mathf.Min(tape.Recorded(_killer), tape.Recorded(_victim));
		if (have < 0.8) return;

		// O fim é o instante da morte (a fita do morto parou ali).
		_replayEnd = tape.Now - (_time + DeathTime);
		_replayClock = _replayEnd - Mathf.Min(ReplayLength, have - DeathTime - 0.1);
		if (_replayClock >= _replayEnd - 0.3) return;
		_replaying = true;
		_fell = false;
		_hasLast = false;
		victim.ReplayHold(true);
	}

	private void StopReplay()
	{
		if (!_replaying) return;
		_replaying = false;
		RemoveGhost();
		if (_victim is Player victim && IsInstanceValid(victim)) victim.ReplayHold(false);
	}

	private bool ReplayStep(double delta)
	{
		ReplayTape tape = ReplayTape.Current;
		if (tape == null || _victim is not Player victim || !IsInstanceValid(victim)) return false;

		double before = _replayClock;
		_replayClock += delta;
		double t = System.Math.Min(_replayClock, _replayEnd);
		if (!tape.Sample(_killer, t, out ReplayTape.Frame killer) || !tape.Sample(_victim, t, out ReplayTape.Frame dead)) return false;

		if (!_fell)
		{
			Vector3 velocity = _hasLast && delta > 0 ? (dead.At - _lastVictimAt) / (float)delta : Vector3.Zero;
			_lastVictimAt = dead.At;
			victim.ReplayPose(dead.At, dead.Yaw, velocity);

			// O instante do abate: o corpo cai de novo, onde caiu.
			if (_replayClock >= _replayEnd)
			{
				_fell = true;
				victim.ReplayHold(false);
			}
		}

		float speed = _hasLast && delta > 0 ? new Vector2(killer.At.X - _lastKillerAt.X, killer.At.Z - _lastKillerAt.Z).Length() / (float)delta : 0f;
		_lastKillerAt = killer.At;
		_hasLast = true;
		int shots = before < _replayEnd ? tape.ShotsBetween(_killer, before, System.Math.Min(_replayClock, _replayEnd)) : 0;

		// O corpo de verdade de quem matou está em outro lugar agora: some
		// da tela; o lobo, que se vê de fora, é reencenado por uma cópia.
		HideBody(_killer, true);
		Pov(_killer, killer, delta, shots, speed, replay: true);
		return _replayClock < _replayEnd + ReplayTail;
	}

	// ------------------------------------------------------------ espectador

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

	private void Spectate(double delta, bool blocked)
	{
		bool next = !blocked && Input.IsActionJustPressed("attack");
		bool previous = !blocked && Input.IsActionJustPressed("special");
		bool cycle = !blocked && Input.IsActionJustPressed("jump");

		if (_phase is Phase.First or Phase.Above)
		{
			bool gone = _mate == null || !IsInstanceValid(_mate) || (_mate.GetNodeOrNull<Health>("Health")?.IsDead ?? true);
			Node3D before = _mate;
			if (gone) _mate = NextMate(null);
			else if (next) _mate = NextMate(_mate, 1);
			else if (previous) _mate = NextMate(_mate, -1);
			if (before != _mate && before != null && IsInstanceValid(before)) ShowBodies();
			if (_mate == null || (cycle && _phase == Phase.Above)) { EnterFree(); return; }
			if (cycle) { _phase = Phase.Above; ShowBodies(); HidePov(); }

			Focus = _mate.GetNodeOrNull<Health>("Health")?.PublicName;
			if (_phase == Phase.First)
			{
				HideBody(_mate, !Pov(_mate, ReplayTape.Read(_mate), delta, LiveShots(_mate)));
				return;
			}

			// De cima: por cima e atrás do aliado, olhando para onde ele vai.
			Vector3 forward = -_mate.GlobalBasis.Z; forward.Y = 0;
			forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
			float weight = Mathf.Min(1f, (float)delta * 6f);
			Vector3 perch = _mate.GlobalPosition + Vector3.Up * 1.9f - forward * 3.4f;
			_camera.Near = 0.05f;
			_camera.GlobalPosition = _camera.GlobalPosition.DistanceTo(perch) > 25f ? perch : _camera.GlobalPosition.Lerp(perch, weight);
			Vector3 target = _mate.GlobalPosition + Vector3.Up * 0.7f + forward * 2f;
			if (_camera.GlobalPosition.DistanceSquaredTo(target) > 0.01f)
			{
				Basis look = Basis.LookingAt(target - _camera.GlobalPosition, Vector3.Up);
				_camera.GlobalBasis = new Basis(_camera.GlobalBasis.GetRotationQuaternion().Slerp(look.GetRotationQuaternion(), weight));
			}
			_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, weight);
			return;
		}

		// Livre: WASD para onde se olha, mouse vira, dentro do mapa. Espaço
		// (ou um clique) volta aos aliados, em primeira pessoa.
		Focus = null;
		if ((cycle || next || previous) && NextMate(null) is Node3D)
		{
			_phase = Phase.First;
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
		_camera.GlobalPosition = MapBounds.Clamp(GetTree(), _camera.GlobalPosition);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_phase != Phase.Free || UiState.AnyOpen || @event is not InputEventMouseMotion motion) return;
		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;
		const float sense = 0.0022f;
		_yaw -= motion.Relative.X * sense;
		_pitch = Mathf.Clamp(_pitch - motion.Relative.Y * sense, Mathf.DegToRad(-88f), Mathf.DegToRad(88f));
	}

	// ------------------------------------------------------------ a silhueta do matador

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
