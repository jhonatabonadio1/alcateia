using Godot;
using System.Collections.Generic;

/// <summary>
/// O que o morto vê até renascer:
///
/// 1. **Onde morreu** (até renascer): a câmera fica onde estava na hora da
///    morte, com o próprio corpo caindo à frente (Jhon, 27/11: "tire o modo
///    espectador flutuante, se morrer, fica onde morreu. Com isso, usuários
///    não irão ver para fora do mapa").
/// 2. **A tela de um aliado vivo**, só se pedir (clique esquerdo; o direito,
///    o anterior): a arma na mão dele, o HUD dele, o som dele
///    (<c>KillCam.Pov.cs</c>). Espaço volta para onde morreu.
///
/// Até 27/11, passado 1 s, a câmera ia sozinha para a tela de um aliado, e
/// o Espaço trocava para a vista de cima e para a câmera livre (WASD e mouse,
/// voando pelo mapa); as duas saíram. Até 19/11 havia ainda a câmera de abate
/// (o replay pela tela de quem te matou). O HUD de quem joga sai da tela
/// enquanto isto existe; a escolha de conjunto só aparece na hora de
/// renascer (<see cref="LoadoutMenu"/>). Morto, o rádio não fala.
///
/// Quem te matou continua com a **silhueta vermelha** (só para você,
/// através das paredes) enquanto esta câmera existe.
/// </summary>
public partial class KillCam : Node3D
{
	private enum Phase { Death, First }
	private Phase _phase = Phase.Death;

	private Node3D _killer, _victim, _mate;
	private Camera3D _camera;
	private Transform3D _from;
	private float _fov = 75f;
	private double _remark;
	private readonly Dictionary<GeometryInstance3D, Material> _marked = new();

	/// <summary>O nome de quem a câmera mostra agora. Null fora disso.</summary>
	public static string Focus { get; private set; }

	/// <summary>Há uma câmera de morte na tela: o HUD de quem joga sai.</summary>
	public static bool Active { get; private set; }

	/// <summary>A câmera de morte na tela agora (nula sem nenhuma).</summary>
	public static KillCam Current { get; private set; }

	/// <summary>Em que modo está: "morte" (onde morreu) ou "primeira" (na
	/// tela de um aliado). Para os testes.</summary>
	public string Mode => _phase == Phase.Death ? "morte" : "primeira";

	/// <summary>Quem te matou, como a câmera o tem (nulo: morte sem matador).
	/// Para os testes.</summary>
	public Node3D Killer => _killer;

	/// <summary>Quantas peças do corpo do matador estão com a silhueta vermelha
	/// agora, e o nome que o HUD mostra no segundo do corpo caindo. Para os testes.</summary>
	public int MarkedPieces => _marked.Count;
	public string WhoName => _whoName != null && _whoName.Visible ? _whoName.Text : "";

	/// <summary>Quem a câmera segue no espectador. Para os testes.</summary>
	public Node3D Watching => _phase == Phase.First ? _mate : null;

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
		bool blocked = UiState.AnyOpen || MatchChat.Typing;

		// O matador pode trocar de forma enquanto você espera: o corpo
		// novo ganha a silhueta também.
		_remark -= delta;
		if (_remark <= 0) { _remark = 0.5; Mark(); }

		bool next = !blocked && Input.IsActionJustPressed("attack");
		bool previous = !blocked && Input.IsActionJustPressed("special");
		bool back = !blocked && Input.IsActionJustPressed("jump");

		if (_phase == Phase.Death)
		{
			// Onde morreu: a câmera não sai do lugar. Um clique vai para a tela de um aliado.
			Focus = null;
			if ((next || previous) && NextMate(null, previous ? -1 : 1) is Node3D mate) Spectate(mate);
		}
		else
		{
			bool gone = _mate == null || !IsInstanceValid(_mate) || (_mate.GetNodeOrNull<Health>("Health")?.IsDead ?? true);
			if (gone) _mate = NextMate(null);
			else if (next) _mate = NextMate(_mate, 1);
			else if (previous) _mate = NextMate(_mate, -1);
			if (_mate == null || back) ReturnToBody();
			else
			{
				Watch(_mate);
				Focus = _mate.GetNodeOrNull<Health>("Health")?.PublicName;
				ViewSubject(delta);
			}
		}

		UpdateHud();
	}

	/// <summary>Na tela de <paramref name="mate"/>.</summary>
	private void Spectate(Node3D mate)
	{
		_phase = Phase.First;
		_mate = mate;
		Watch(_mate);
	}

	/// <summary>De volta para onde morreu, a câmera como estava na hora.</summary>
	private void ReturnToBody()
	{
		Unwatch();
		ShowBodies();
		_phase = Phase.Death;
		_mate = null;
		Focus = null;
		_camera.Fov = _fov;
		_camera.GlobalTransform = _from;
		_camera.MakeCurrent();
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

	/// <summary>Há aliado vivo para assistir (o HUD só oferece o clique se houver).</summary>
	private bool HasMate => NextMate(null) != null;

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
