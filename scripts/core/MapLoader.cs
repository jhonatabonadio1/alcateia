using Godot;
using System.Collections.Generic;

/// <summary>
/// Carrega um mapa vindo de um .glb e o deixa jogável.
///
/// O Godot importa .glb sozinho, mas o que vem é só geometria: sem
/// colisão, sem navegação, sem pontos de spawn. Normalmente isso se
/// resolve na janela de importação, que é GUI. Aqui é resolvido em
/// tempo de execução, o que também tem a vantagem de funcionar com
/// qualquer .glb que você trocar depois, sem reconfigurar nada.
///
/// Convenções por NOME de nó, lidas do arquivo:
///
///   *-noco*   não gera colisão (vidro, decoração, folhagem)
///   *-invis*  colide mas não aparece (parede invisível, teto de
///             bloqueio, limite do mapa)
///   spawn*    vira ponto de nascimento
///
/// Nomear no Blender é a forma mais barata de passar intenção do
/// modelo para o jogo sem inventar um formato de arquivo paralelo.
/// </summary>
public partial class MapLoader : Node3D
{
    [Export(PropertyHint.File, "*.glb,*.gltf,*.tscn")]
    public string MapPath = "";

    /// <summary>Gerar colisão de malha para tudo que não for marcado.</summary>
    [Export] public bool GenerateCollision = true;

    /// <summary>Assar navegação depois de montar a geometria.</summary>
    [Export] public bool BakeNavigation = true;

    /// <summary>Altura máxima que a navegação considera subível.</summary>
    [Export] public float AgentStepHeight = 0.5f;

    [Export] public float AgentRadius = 0.45f;
    [Export] public float AgentHeight = 1.8f;

    /// <summary>Inclinação máxima andável, em graus.</summary>
    [Export] public float AgentMaxSlope = 45f;

    [Export] public bool Debug = true;

    /// <summary>Pontos de nascimento encontrados no mapa.</summary>
    public List<Vector3> Spawns { get; } = new();

    private NavigationRegion3D _region;

    public override void _Ready()
    {
        if (string.IsNullOrEmpty(MapPath))
        {
            if (Debug) GD.Print("MapLoader: nenhum mapa definido.");
            return;
        }

        var scene = GD.Load<PackedScene>(MapPath);

        if (scene == null)
        {
            GD.PrintErr($"MapLoader: não consegui carregar {MapPath}");
            return;
        }

        Node3D map = scene.Instantiate<Node3D>();
        AddChild(map);

        int collisions = 0;
        Prepare(map, ref collisions);

        if (Debug)
            GD.Print($"MapLoader: {collisions} corpos de colisão, {Spawns.Count} spawns");

        if (BakeNavigation)
            Bake(map);
    }

    // ------------------------------------------------------------------

    /// <summary>Percorre a árvore do modelo aplicando as convenções de
    /// nome e gerando colisão.</summary>
    private void Prepare(Node node, ref int collisions)
    {
        // Os filhos são copiados antes: gerar colisão adiciona nós, e
        // percorrer a lista viva entraria em recursão infinita.
        var children = new List<Node>();
        foreach (Node child in node.GetChildren())
            children.Add(child);

        string name = node.Name.ToString().ToLowerInvariant();

        if (name.StartsWith("spawn") && node is Node3D marker)
            Spawns.Add(marker.GlobalPosition);

        if (node is MeshInstance3D mesh)
        {
            if (name.Contains("-invis"))
                mesh.Visible = false;

            bool skip = name.Contains("-noco");

            if (GenerateCollision && !skip && mesh.Mesh != null)
            {
                // Trimesh: colisão exata e estática. Correta para
                // cenário, inútil para corpo que se move — e cenário é
                // tudo que vem de um mapa.
                mesh.CreateTrimeshCollision();
                collisions++;
            }
        }

        foreach (Node child in children)
            Prepare(child, ref collisions);
    }

    /// <summary>
    /// Assa a navegação a partir da geometria já montada.
    ///
    /// Sem isto os bots continuam andando na direção do alvo e
    /// desviando com raycast, o que funciona em campo aberto e falha
    /// no primeiro corredor em L.
    /// </summary>
    private void Bake(Node3D map)
    {
        _region = new NavigationRegion3D();
        AddChild(_region);

        var mesh = new NavigationMesh
        {
            AgentRadius = AgentRadius,
            AgentHeight = AgentHeight,
            AgentMaxClimb = AgentStepHeight,
            AgentMaxSlope = AgentMaxSlope,
            GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders
        };

        _region.NavigationMesh = mesh;

        // A fonte é a árvore do mapa, não a cena inteira: incluir
        // jogador e bots na navegação os transformaria em paredes.
        var source = new NavigationMeshSourceGeometryData3D();
        NavigationServer3D.ParseSourceGeometryData(mesh, source, map);
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);

        _region.NavigationMesh = mesh;

        if (Debug)
            GD.Print($"MapLoader: navegação assada com {mesh.GetPolygonCount()} polígonos");
    }
}
