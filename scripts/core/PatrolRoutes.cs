using Godot;
using System.Collections.Generic;

/// <summary>
/// Rotas de ronda do mapa.
///
/// Antes a patrulha era um ponto aleatório dentro de um raio. Isso
/// produz bot que anda em linha reta para o meio de um prédio e fica
/// raspando a parede, porque o destino não tem relação nenhuma com a
/// forma do mapa — é só um número dentro de um círculo.
///
/// Rota é diferente: é um caminho que alguém já sabe que existe. O bot
/// escolhe uma ao acaso, percorre, e escolhe outra. O resultado é que
/// ele aparece onde um jogador apareceria, e some pelos mesmos lugares.
///
/// Cada filho deste nó é uma rota; cada filho da rota é um ponto, na
/// ordem. Mapa novo traz as suas, e sem nenhuma o bot volta a vagar no
/// raio — a ronda nunca depende disto existir.
/// </summary>
public partial class PatrolRoutes : Node3D
{
    [Export] public bool Debug = true;

    private static PatrolRoutes _instance;

    private readonly List<List<Vector3>> _routes = new();

    public static int RouteCount => _instance?._routes.Count ?? 0;

    public override void _Ready()
    {
        _instance = this;

        foreach (Node child in GetChildren())
        {
            var points = new List<Vector3>();

            foreach (Node point in child.GetChildren())
                if (point is Node3D marker)
                    points.Add(marker.GlobalPosition);

            if (points.Count >= 2) _routes.Add(points);
        }

        if (Debug)
            GD.Print($"PatrolRoutes: {_routes.Count} rotas");
    }

    public override void _ExitTree()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Quantos pontos tem a rota. Zero se ela não existe.</summary>
    public static int Length(int route) =>
        _instance != null && route >= 0 && route < _instance._routes.Count
            ? _instance._routes[route].Count
            : 0;

    public static Vector3 Point(int route, int index) =>
        _instance._routes[route][index];

    /// <summary>
    /// A rota que começa mais perto de quem pergunta, entre algumas
    /// sorteadas.
    ///
    /// Sorteio puro manda o bot atravessar o mapa até o começo de uma
    /// rota do outro lado, o que é uma caminhada sem propósito que o
    /// jogador vê. Sortear poucas e ficar com a mais próxima mantém o
    /// acaso e tira a travessia boba.
    /// </summary>
    public static int Nearest(Vector3 from, int samples = 3)
    {
        if (RouteCount == 0) return -1;

        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < samples; i++)
        {
            int route = (int)(GD.Randi() % (uint)RouteCount);
            float distance = ClosestPoint(route, from, out _);

            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = route;
        }

        return best;
    }

    /// <summary>Distância até o ponto mais próximo da rota, e qual é.</summary>
    public static float ClosestPoint(int route, Vector3 from, out int index)
    {
        index = 0;
        float best = float.MaxValue;

        List<Vector3> points = _instance._routes[route];

        for (int i = 0; i < points.Count; i++)
        {
            float distance = points[i].DistanceTo(from);
            if (distance >= best) continue;

            best = distance;
            index = i;
        }

        return best;
    }
}
