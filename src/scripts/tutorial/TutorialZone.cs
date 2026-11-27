using Godot;
using System.Collections.Generic;

/// <summary>
/// Uma zona do campo de treino (o Setor Zero): uma caixa no mapa com um
/// nome (<see cref="Id"/>) que avisa quando o jogador entra e sai dela —
/// "chegou na marca", "passou o vão", "está sob a viga", "no trecho
/// silencioso". O mapa as traz prontas (src/tools/mapas/setor_zero.py);
/// quem escuta é o tutorial (<c>TutorialRunner</c>).
///
/// Como a <see cref="Ladder"/>, ela pergunta pela posição do centro do
/// corpo a cada passo de física, em vez de esperar o sinal da física:
/// é a mesma conta em qualquer máquina e não depende de camada de
/// colisão. Só o jogador desta máquina conta (o grupo
/// <see cref="Groups.Players"/>): o treino não tem bots.
///
/// <see cref="Reset"/>: o fundo de um vão. Quem cai nele volta para
/// <see cref="Back"/> (a marca antes do vão), parado, sem dano e sem
/// morrer — cair no treino não é derrota.
///
/// <see cref="Deadly"/>: o abismo. O Setor Zero fica no alto de uma
/// montanha (Jhon, 26/11: "o player pode pular e se matar, num
/// precipício infinito"): quem passa da beira e desce até a zona morre
/// de queda, humano ou lobo, com ou sem a proteção de quem acabou de
/// nascer — e o feed diz "queda".
/// </summary>
public partial class TutorialZone : Node3D
{
	/// <summary>O nome da zona no roteiro (assets/tutorial/roteiro.json).</summary>
	[Export] public string Id = "";

	/// <summary>Tamanho da caixa, em metros (largura, altura, fundura), centrada no nó.</summary>
	[Export] public Vector3 Size = Vector3.One;

	/// <summary>O fundo de um vão: quem entra volta para <see cref="Back"/>.</summary>
	[Export] public bool Reset = false;

	/// <summary>Para onde volta quem cai (um <c>Marker3D</c> do mapa).</summary>
	[Export] public NodePath Back;

	/// <summary>O abismo: quem entra morre de queda.</summary>
	[Export] public bool Deadly = false;

	/// <summary>Entrou na zona (o corpo do jogador).</summary>
	[Signal] public delegate void EnteredEventHandler(Node3D body);

	/// <summary>Saiu da zona.</summary>
	[Signal] public delegate void ExitedEventHandler(Node3D body);

	/// <summary>Caiu no vão e voltou para a marca. O HUD do treino escurece a
	/// tela por um instante; a regra não precisa de mais nada.</summary>
	[Signal] public delegate void SentBackEventHandler(Node3D body);

	private static readonly List<TutorialZone> _all = new();
	private readonly HashSet<ulong> _inside = new();

	public override void _Ready()
	{
		if (!_all.Contains(this)) _all.Add(this);
	}

	public override void _ExitTree() => _all.Remove(this);

	public bool Contains(Vector3 point)
	{
		Vector3 local = GlobalTransform.AffineInverse() * point;
		return Mathf.Abs(local.X) <= Size.X / 2f && Mathf.Abs(local.Y) <= Size.Y / 2f && Mathf.Abs(local.Z) <= Size.Z / 2f;
	}

	/// <summary>O jogador está dentro agora.</summary>
	public bool Holds(Node3D body) => body != null && _inside.Contains(body.GetInstanceId());

	/// <summary>A zona com este nome, ou nenhuma.</summary>
	public static TutorialZone Find(string id)
	{
		foreach (TutorialZone zone in _all)
			if (zone.IsInsideTree() && zone.Id == id)
				return zone;
		return null;
	}

	/// <summary>Todas as zonas do mapa carregado.</summary>
	public static IReadOnlyList<TutorialZone> All => _all;

	public override void _PhysicsProcess(double delta)
	{
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Players))
		{
			if (node is not Node3D body) continue;
			ulong id = body.GetInstanceId();
			bool now = Contains(body.GlobalPosition);
			bool was = _inside.Contains(id);

			if (now && !was)
			{
				_inside.Add(id);
				EmitSignal(SignalName.Entered, body);
				if (Reset) SendBack(body);
				if (Deadly) Kill(body);
			}
			else if (!now && was)
			{
				_inside.Remove(id);
				EmitSignal(SignalName.Exited, body);
			}
		}
	}

	private static void Kill(Node3D body)
	{
		var health = body.GetNodeOrNull<Health>("Health");
		if (health == null || health.IsDead) return;
		health.TakeOwnDamage(100000f, body.GlobalPosition + Vector3.Up, KillCause.Fall, "");
	}

	private void SendBack(Node3D body)
	{
		if (GetNodeOrNull<Node3D>(Back) is not Node3D back) return;

		body.GlobalPosition = back.GlobalPosition;
		if (body is CharacterBody3D character) character.Velocity = Vector3.Zero;
		_inside.Remove(body.GetInstanceId());
		EmitSignal(SignalName.SentBack, body);
	}
}
