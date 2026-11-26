using Godot;
using System.Collections.Generic;

/// <summary>
/// O que o morto vê até renascer (pedidos do Jhon, 18 e 19/11, com o
/// Overwatch de referência):
///
/// 1. **Morte** (1 s): a câmera fica no próprio corpo caindo.
/// 2. **Espectador**: direto na **tela de um aliado vivo**: a arma na mão
///    dele, o HUD dele, o som dele (<c>KillCam.Pov.cs</c>). Clique esquerdo: próximo aliado; direito: o anterior.
///    **Espaço troca a visão**: a tela dele → de cima → câmera livre (WASD
///    e mouse, presa dentro do mapa, <see cref="MapBounds"/>) → de volta.
///
/// Até 19/11 havia entre os dois a câmera de abate (o replay pela tela de
/// quem te matou); saiu a pedido do Jhon. O HUD de quem joga sai da tela
/// enquanto isto existe; a escolha de
/// conjunto só aparece na hora de renascer (<see cref="LoadoutMenu"/>).
/// Morto, o rádio não fala.
///
/// Quem te matou continua com a **silhueta vermelha** (só para você,
/// através das paredes) enquanto esta câmera existe.
/// </summary>
public partial class KillCam : Node3D
{
	/// <summary>Segundos vendo o próprio corpo antes do espectador.</summary>
	[Export] public float DeathTime = 1f;

	/// <summary>Velocidade da câmera livre (m/s); com Shift, o dobro.</summary>
	[Export] public float FreeSpeed = 9f;

	private enum Phase { Death, First, Above, Free }
	private Phase _phase = Phase.Death;

	private Node3D _killer, _victim, _mate;
	private Camera3D _camera;
	private Transform3D _from;
	private float _fov = 75f;
	private double _time, _remark;
	private float _yaw, _pitch;
	private readonly Dictionary<GeometryInstance3D, Material> _marked = new();

	/// <summary>O nome de quem a câmera mostra agora. Null fora disso.</summary>
	public static string Focus { get; private set; }

	/// <summary>Há uma câmera de morte na tela: o HUD de quem joga sai.</summary>
	public static bool Active { get; private set; }

	/// <summary>A câmera de morte na tela agora (nula sem nenhuma).</summary>
	public static KillCam Current { get; private set; }

	/// <summary>Em que modo está: "morte", "abate", "primeira", "cima" ou
	/// "livre". Para os testes.</summary>
	public string Mode => _phase switch
	{
		Phase.Death => "morte", Phase.First => "primeira", Phase.Above => "cima", _ => "livre"
	};

	/// <summary>Quem te matou, como a câmera o tem (nulo: morte sem matador).
	/// Para os testes.</summary>
	public Node3D Killer => _killer;

	/// <summary>Quantas peças do corpo do matador estão com a silhueta vermelha
	/// agora, e o nome que o HUD mostra no segundo do corpo caindo. Para os testes.</summary>
	public int MarkedPieces => _marked.Count;
	public string WhoName => _whoName != null && _whoName.Visible ? _whoName.Text : "";

	/// <summary>Quem a câmera segue no espectador. Para os testes.</summary>
	public Node3D Watching => _phase is Phase.First or Phase.Above ? _mate : null;

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
		Current = this;
		Mark();
		BuildHud();
	}

	/// <summary>Volta a câmera para esta visão (depois de algo tê-la tirado).</summary>
	public void Reclaim()
	{
		if (_subjectFirst && _subjectCamera != null && IsInstanceValid(_subjectCamera)) _subjectCamera.MakeCurrent();
		else _camera?.MakeCurrent();
	}

	public void Finish()
	{
		Cleanup();
		// O QueueFree só apaga no fim do quadro: até lá esta câmera ainda
		// andaria, e renascer pela escolha de conjunto (que anda antes dela
		// na cena) a fazia voltar a assistir — HUD e arma do aliado de novo
		// por cima da sua tela, sem ninguém para tirar (19/11).
		SetProcess(false);
		SetProcessInput(false);
		SetProcessUnhandledInput(false);
		QueueFree();
	}

	public override void _ExitTree() => Cleanup();

	private bool _cleaned;

	private void Cleanup()
	{
		if (_cleaned) return;
		_cleaned = true;
		Unwatch();
		ShowBodies();
		Focus = null;
		Active = false;
		if (Current == this) Current = null;
		foreach (var (mesh, previous) in _marked)
			if (IsInstanceValid(mesh)) BodyOverlay.SetEdge(mesh, previous);
		_marked.Clear();
	}

	// ------------------------------------------------------------ as fases

	public override void _Process(double delta)
	{
		if (_cleaned) return;
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
				// Espaço já vai para o espectador; passou o segundo, também.
				if ((!blocked && Input.IsActionJustPressed("jump")) || _time >= DeathTime) EnterSpectate();
				break;

			default:
				Spectate(delta, blocked);
				break;
		}

		UpdateHud();
	}

	private void EnterSpectate()
	{
		Unwatch();
		ShowBodies();
		_time = 0;
		_phase = Phase.First;
		_mate = NextMate(null);
		if (_mate == null) EnterFree();
		else Watch(_mate);
	}

	private void EnterFree()
	{
		Unwatch();
		ShowBodies();
		_phase = Phase.Free;
		_camera.MakeCurrent();
		Vector3 e = _camera.GlobalBasis.GetEuler();
		_pitch = e.X;
		_yaw = e.Y;
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
			if (gone) _mate = NextMate(null);
			else if (next) _mate = NextMate(_mate, 1);
			else if (previous) _mate = NextMate(_mate, -1);
			if (_mate == null || (cycle && _phase == Phase.Above)) { EnterFree(); return; }
			if (cycle) _phase = Phase.Above;
			Watch(_mate);

			Focus = _mate.GetNodeOrNull<Health>("Health")?.PublicName;
			if (_phase == Phase.First)
			{
				ViewSubject(delta);
				return;
			}

			// De cima: por cima e atrás do aliado, olhando para onde ele vai.
			ViewSubject(delta, above: true);
			Vector3 forward = -_mate.GlobalBasis.Z; forward.Y = 0;
			forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
			float weight = Mathf.Min(1f, (float)delta * 6f);
			Vector3 perch = SafePerch(_mate, _mate.GlobalPosition + Vector3.Up * 1.9f - forward * 3.4f);
			_camera.Near = 0.05f;
			Vector3 glide = _camera.GlobalPosition.DistanceTo(perch) > 25f ? perch : _camera.GlobalPosition.Lerp(perch, weight);
			// Nunca fora do mapa nem atrás de parede (19/11: com o aliado
			// encostado na borda, a vista de cima saía do mapa).
			_camera.GlobalPosition = MapBounds.Clamp(GetTree(), glide);
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
		// (ou um clique) volta aos aliados, na tela deles.
		Focus = null;
		if ((cycle || next || previous) && NextMate(null) is Node3D)
		{
			_phase = Phase.First;
			_mate = NextMate(previous ? null : _mate, previous ? -1 : 1) ?? NextMate(null);
			Watch(_mate);
			return;
		}

		_camera.Near = 0.05f;
		_camera.Fov = Mathf.Lerp(_camera.Fov, 75f, Mathf.Min(1f, (float)delta * 6f));
		_camera.GlobalBasis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0));
		if (!blocked)
		{
			Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
			Vector3 step = _camera.GlobalBasis * new Vector3(move.X, 0, move.Y);
			_camera.GlobalPosition = Fly(_camera.GlobalPosition, step * FreeSpeed * (Input.IsActionPressed("sprint") ? 2f : 1f) * (float)delta);
		}
		_camera.GlobalPosition = MapBounds.Clamp(GetTree(), _camera.GlobalPosition);
	}

	/// <summary>Aponta a câmera livre (graus). Para os testes.</summary>
	public void AimFree(float yawDegrees, float pitchDegrees)
	{
		_yaw = Mathf.DegToRad(yawDegrees);
		_pitch = Mathf.DegToRad(pitchDegrees);
	}

	/// <summary>Raio da bola que a câmera livre é para o cenário.</summary>
	private const float FlyRadius = 0.3f;

	private SphereShape3D _flyShape;

	/// <summary>
	/// A câmera livre bate no cenário (19/11: "tenho que ficar dentro do
	/// Armazém, não posso sair pelas paredes, chão e teto"): anda como uma
	/// bola de 30 cm, eixo por eixo, então escorrega na parede em vez de
	/// parar seco. Só o cenário conta (camada 1), não os corpos.
	/// </summary>
	private Vector3 Fly(Vector3 from, Vector3 motion)
	{
		_flyShape ??= new SphereShape3D { Radius = FlyRadius };
		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
		// Os corpos também estão na camada 1: ficam de fora.
		var skip = new Godot.Collections.Array<Rid>();
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is CollisionObject3D body) skip.Add(body.GetRid());
		Vector3 at = from;
		foreach (Vector3 axis in new[] { Vector3.Right, Vector3.Up, Vector3.Back })
		{
			Vector3 part = axis * motion.Dot(axis);
			if (part.LengthSquared() < 1e-10f) continue;
			var query = new PhysicsShapeQueryParameters3D
			{
				Shape = _flyShape,
				Transform = new Transform3D(Basis.Identity, at),
				Motion = part,
				CollisionMask = 1,
				Exclude = skip
			};
			float[] cast = space.CastMotion(query);
			float safe = cast.Length > 0 ? cast[0] : 1f;
			at += part * safe;
		}
		return at;
	}

	/// <summary>O ponto da vista de cima, puxado para perto do aliado se
	/// houver parede no caminho.</summary>
	private Vector3 SafePerch(Node3D mate, Vector3 want)
	{
		Vector3 eye = mate.GlobalPosition + Vector3.Up * 1.5f;
		var query = PhysicsRayQueryParameters3D.Create(eye, want);
		var skip = new Godot.Collections.Array<Rid>();
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is CollisionObject3D fighter) skip.Add(fighter.GetRid());
		query.Exclude = skip;
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0) return want;
		Vector3 at = hit["position"].AsVector3();
		return at + (eye - at).Normalized() * WolfMargin;
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
			_marked[mesh] = BodyOverlay.Edge(mesh);
			BodyOverlay.SetEdge(mesh, Silhouette.Killer);
		}
	}
}
