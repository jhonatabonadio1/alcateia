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

    /// <summary>
    /// Deslocamento aplicado ao mapa inteiro.
    ///
    /// Modelo exportado de outra ferramenta raramente tem o chão em
    /// Y=0 — o Arrival tem o piso em 7,5. Em vez de corrigir no
    /// Blender e reexportar a cada troca de mapa, o jogo baixa o mapa
    /// na entrada. Com o piso em zero, o limite de queda, o minimapa e
    /// qualquer coordenada que apareça em log voltam a ser legíveis.
    /// </summary>
    [Export] public Vector3 MapOffset = Vector3.Zero;

    /// <summary>Tamanho da célula de navegação. Menor acompanha melhor
    /// a geometria e custa mais para assar.</summary>
    [Export] public float NavCellSize = 0.25f;

    /// <summary>
    /// Caixa dentro da qual a navegação é assada. Vazia significa tudo.
    ///
    /// Mapas de FPS costumam ter "cortinas": planos enormes descendo
    /// centenas de metros para o jogador não ver o vazio nas bordas. O
    /// Arrival tem as suas descendo até -287. Assar isso faria o
    /// Godot varrer uma caixa de 300 metros de altura célula por
    /// célula para encontrar um piso que está todo nos três primeiros.
    /// </summary>
    [Export] public Aabb NavBounds = new Aabb();

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

        map.Position = MapOffset;

        // A árvore precisa estar com a posição final antes de ler
        // GlobalPosition dos marcadores de spawn.
        map.ForceUpdateTransform();

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
        {
            Spawns.Add(marker.GlobalPosition);
            SpawnPoints.Register(marker.GlobalPosition);
        }

        if (node is MeshInstance3D mesh)
        {
            if (name.Contains("-invis"))
                mesh.Visible = false;

            bool skip = name.Contains("-noco");

            if (GenerateCollision && !skip && mesh.Mesh != null)
            {
                BuildCollision(mesh);
                collisions++;
            }
        }

        foreach (Node child in children)
            Prepare(child, ref collisions);
    }

    /// <summary>
    /// Gera colisão de malha para um objeto do mapa.
    ///
    /// Faz à mão o que <c>CreateTrimeshCollision()</c> faria, por um
    /// motivo só: aquele método não dá acesso à forma gerada, e a
    /// forma precisa de <c>BackfaceCollision</c>.
    ///
    /// Mapa extraído de jogo é feito de planos de uma face só, e o
    /// lado que cada plano considera "frente" é o que o modelador
    /// precisou para desenhar, não o lado onde o jogador anda. No
    /// Arrival, mais da metade das faces verticais na altura de jogo
    /// aponta para fora da arena. Com colisão de uma face, metade das
    /// paredes do mapa existe para os olhos e não existe para a
    /// física: o jogador vê parede, o raio atravessa, o lobo não
    /// gruda — e nada no log explica por quê, porque para o motor não
    /// havia nada ali.
    ///
    /// Colisão de dois lados custa pouco (5.213 triângulos no mapa
    /// inteiro) e é a resposta certa para geometria de uma face.
    /// </summary>
    private static void BuildCollision(MeshInstance3D mesh)
    {
        var shape = new ConcavePolygonShape3D
        {
            Data = mesh.Mesh.GetFaces(),
            BackfaceCollision = true
        };

        var body = new StaticBody3D { Name = $"{mesh.Name}_col" };
        body.AddChild(new CollisionShape3D { Shape = shape });

        // Filho do próprio objeto, com transformação local identidade:
        // é assim que os dados da forma, que estão em espaço local da
        // malha, caem no lugar certo do mundo.
        mesh.AddChild(body);
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
            CellSize = NavCellSize,
            GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders
        };

        if (NavBounds.Size != Vector3.Zero)
        {
            mesh.FilterBakingAabb = NavBounds;
            mesh.FilterBakingAabbOffset = Vector3.Zero;
        }

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
