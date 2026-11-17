using Godot;
using System.Collections.Generic;

/// <summary>
/// Pontos de nascimento do mapa.
///
/// **Decisão do Jhon (17/11): cada um renasce sempre no mesmo ponto**,
/// o que recebeu na largada, dentro da base do time (<see cref="SetHome"/>,
/// chamado por quem distribui a largada). Renascer onde se morreu
/// devolveria o jogador para dentro do tiroteio que o matou; a base fixa
/// é a do WolfTeam. O risco conhecido é o inimigo acampar na base:
/// quem segura isso é a proteção de nascimento do <see cref="Health"/>.
///
/// Quem não tem ponto próprio (cena de treino, bot criado fora da
/// largada) cai na escolha antiga, por distância: entre os pontos
/// permitidos, nasce no mais longe do **inimigo** vivo mais próximo, com
/// alguns metros de ruído.
///
/// Os pontos são os filhos <c>Node3D</c> deste nó. Pode haver vários
/// nós destes na cena, um por time: <see cref="Team"/> diz para quem
/// cada conjunto serve, e 0 serve para todos. É como cada base do mapa
/// guarda os próprios spawns.
///
/// Marcadores com nome começando em <c>spawn</c> dentro do .glb do
/// mapa entram também, valendo para todos os times.
/// </summary>
public partial class SpawnPoints : Node3D
{
	/// <summary>Time que nasce aqui. 0 vale para qualquer um.</summary>
	[Export] public int Team = 0;

	/// <summary>Quanto de aleatoriedade entra na escolha, em metros.
	/// Sem nenhuma, o mesmo par de mortes gera sempre o mesmo spawn e
	/// o jogo fica previsível.</summary>
	[Export] public float Jitter = 6f;

	[Export] public bool Debug = true;

	private static readonly List<SpawnPoints> _all = new();

	/// <summary>O ponto de cada corpo, pelo id da instância.</summary>
	private static readonly Dictionary<ulong, Vector3> _home = new();

	/// <summary>Grava o ponto da largada de um corpo: é onde ele renasce
	/// a partida inteira.</summary>
	public static void SetHome(Node3D body, Vector3 at) => _home[body.GetInstanceId()] = at;

	private readonly List<Vector3> _points = new();

	public override void _Ready()
	{
		_all.Add(this);

		foreach (Node child in GetChildren())
			if (child is Node3D marker)
				_points.Add(marker.GlobalPosition);

		if (Debug)
			GD.Print($"SpawnPoints '{Name}': {_points.Count} pontos" +
					 (Team > 0 ? $" para o time {Teams.NameOf(Team)}" : " para qualquer time"));
	}

	public override void _ExitTree()
	{
		_all.Remove(this);
		// Acabou a partida (saíram os conjuntos do mapa): os pontos de
		// cada um não valem para a próxima.
		if (_all.Count == 0) _home.Clear();
	}

	/// <summary>Registra um ponto vindo de fora — do mapa, por exemplo.
	/// Vai para o primeiro conjunto sem time, que serve a todos.</summary>
	public static void Register(Vector3 point)
	{
		foreach (SpawnPoints set in _all)
		{
			if (set.Team != 0) continue;

			set._points.Add(point);
			return;
		}
	}

	/// <summary>Todos os pontos que servem a um time, na ordem da cena.
	/// Usado para distribuir a largada sem ninguém nascer em cima de
	/// ninguém.</summary>
	public static List<Vector3> PointsFor(int team)
	{
		var points = new List<Vector3>();
		foreach (SpawnPoints set in _all)
			if (set.Team == 0 || set.Team == team)
				points.AddRange(set._points);

		return points;
	}

	/// <summary>
	/// Escolhe onde nascer. Devolve <paramref name="fallback"/> quando
	/// não há ponto para o time de quem pergunta, o que mantém a cena de
	/// treino (um spawn fixo por ator) funcionando sem configuração.
	/// </summary>
	public static Vector3 Pick(Node3D asker, Vector3 fallback)
	{
		if (_home.TryGetValue(asker.GetInstanceId(), out Vector3 home)) return home;

		int team = asker.GetNodeOrNull<Health>("Health")?.Team ?? 0;

		var candidates = new List<Vector3>();
		foreach (SpawnPoints set in _all)
			if (set.Team == 0 || set.Team == team)
				candidates.AddRange(set._points);

		if (candidates.Count == 0) return fallback;

		var enemies = new List<Vector3>();
		Collect(Groups.Players, asker, team, enemies);
		Collect(Groups.Bots, asker, team, enemies);

		float jitter = _all.Count > 0 ? _all[0].Jitter : 6f;

		Vector3 best = candidates[0];
		float bestScore = float.MinValue;

		foreach (Vector3 point in candidates)
		{
			// Distância ao inimigo mais próximo: é esse o número que
			// decide se o spawn é seguro. A média não serve — nascer
			// longe de quatro e colado em um é nascer colado em um.
			float nearest = enemies.Count == 0 ? 0f : float.MaxValue;
			foreach (Vector3 enemy in enemies)
				nearest = Mathf.Min(nearest, point.DistanceTo(enemy));

			float score = nearest + MatchRandom.For("nascimento").Unit() * jitter;

			if (score <= bestScore) continue;

			bestScore = score;
			best = point;
		}

		return best;
	}

	/// <summary>Posições dos inimigos vivos de um grupo.</summary>
	private static void Collect(string group, Node asker, int team, List<Vector3> into)
	{
		if (Engine.GetMainLoop() is not SceneTree tree) return;

		foreach (Node node in tree.GetNodesInGroup(group))
		{
			if (node == asker || node is not Node3D body) continue;

			var health = node.GetNodeOrNull<Health>("Health");
			if (health == null || health.IsDead) continue;

			// Aliado não é ameaça. Com time 0 (cena sem times), todos
			// contam, como era antes de existirem bases.
			if (team != 0 && health.Team == team) continue;

			into.Add(body.GlobalPosition);
		}
	}
}
