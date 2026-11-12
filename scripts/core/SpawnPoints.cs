using Godot;
using System.Collections.Generic;

/// <summary>
/// Pontos de nascimento do mapa.
///
/// Renascer onde se morreu devolve o jogador para dentro do tiroteio
/// que o matou. Renascer sempre no mesmo canto é pior: em um mapa de
/// 80 metros, quem decora o ponto de spawn ganha de graça.
///
/// A escolha é por distância: entre os pontos disponíveis, nasce no
/// mais longe de quem está vivo. Não é a regra mais sofisticada que
/// existe — jogos grandes pesam também linha de visão e tempo desde a
/// última morte ali — mas é a que resolve 90% do problema com 20
/// linhas, e é honesta: ninguém nasce de costas para um inimigo a
/// cinco metros.
///
/// Os pontos são os filhos <c>Node3D</c> deste nó. Marcadores com nome
/// começando em <c>spawn</c> dentro do .glb do mapa entram também, o
/// que permite definir spawns no Blender sem tocar em código.
/// </summary>
public partial class SpawnPoints : Node3D
{
    /// <summary>Quanto de aleatoriedade entra na escolha, em metros.
    /// Sem nenhuma, o mesmo par de mortes gera sempre o mesmo spawn e
    /// o jogo fica previsível.</summary>
    [Export] public float Jitter = 6f;

    [Export] public bool Debug = true;

    private static SpawnPoints _instance;

    private readonly List<Vector3> _points = new();

    public override void _Ready()
    {
        _instance = this;

        foreach (Node child in GetChildren())
            if (child is Node3D marker)
                _points.Add(marker.GlobalPosition);

        if (Debug)
            GD.Print($"SpawnPoints: {_points.Count} pontos");
    }

    public override void _ExitTree()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Registra um ponto vindo de fora — do mapa, por exemplo.</summary>
    public static void Register(Vector3 point)
    {
        _instance?._points.Add(point);
    }

    /// <summary>
    /// Escolhe onde nascer. Devolve <paramref name="fallback"/> quando
    /// não há pontos definidos, o que mantém a cena de treino (um
    /// spawn fixo por ator) funcionando sem configuração.
    /// </summary>
    public static Vector3 Pick(Node3D asker, Vector3 fallback)
    {
        if (_instance == null || _instance._points.Count == 0)
            return fallback;

        return _instance.Choose(asker, fallback);
    }

    private Vector3 Choose(Node3D asker, Vector3 fallback)
    {
        var others = new List<Vector3>();
        Collect("player", asker, others);
        Collect("bot", asker, others);

        Vector3 best = fallback;
        float bestScore = float.MinValue;

        foreach (Vector3 point in _points)
        {
            // Distância ao inimigo mais próximo: é esse o número que
            // decide se o spawn é seguro. A média não serve — nascer
            // longe de quatro e colado em um é nascer colado em um.
            float nearest = float.MaxValue;
            foreach (Vector3 other in others)
                nearest = Mathf.Min(nearest, point.DistanceTo(other));

            if (others.Count == 0) nearest = 0;

            float score = nearest + (float)GD.Randf() * Jitter;

            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    /// <summary>Posições dos vivos de um grupo, menos quem perguntou.</summary>
    private static void Collect(string group, Node asker, List<Vector3> into)
    {
        foreach (Node node in Engine.GetMainLoop() is SceneTree tree
            ? tree.GetNodesInGroup(group)
            : new Godot.Collections.Array<Node>())
        {
            if (node == asker || node is not Node3D body) continue;

            // Um morto não ameaça ninguém, e contá-lo empurraria o
            // spawn para longe de um corpo que vai desaparecer.
            var health = node.GetNodeOrNull<Health>("Health");
            if (health != null && health.IsDead) continue;

            into.Add(body.GlobalPosition);
        }
    }
}
