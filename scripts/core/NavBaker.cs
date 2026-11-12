using Godot;

/// <summary>
/// Assa a navegação da cena em tempo de execução.
///
/// Existe porque assar no editor é GUI, e porque o mapa muda com
/// frequência enquanto o jogo está sendo desenhado — assar na
/// inicialização custa alguns milissegundos e elimina a chance de
/// alguém mexer na geometria e esquecer de reassar, que é o erro que
/// deixa os bots andando contra parede sem ninguém entender por quê.
///
/// Só colisores estáticos entram. Jogador e bots são corpos
/// cinemáticos e ficam de fora: incluí-los os transformaria em paredes
/// permanentes no mapa de navegação.
/// </summary>
public partial class NavBaker : Node3D
{
    [Export] public float AgentRadius = 0.5f;
    [Export] public float AgentHeight = 1.8f;
    [Export] public float AgentMaxClimb = 0.5f;
    [Export] public float AgentMaxSlope = 45f;

    /// <summary>Tamanho da célula. Menor acompanha melhor a geometria
    /// e custa mais para assar.</summary>
    [Export] public float CellSize = 0.25f;

    [Export] public bool Debug = true;

    public override void _Ready()
    {
        var region = new NavigationRegion3D();
        AddChild(region);

        var mesh = new NavigationMesh
        {
            AgentRadius = AgentRadius,
            AgentHeight = AgentHeight,
            AgentMaxClimb = AgentMaxClimb,
            AgentMaxSlope = AgentMaxSlope,
            CellSize = CellSize,
            GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders
        };

        var source = new NavigationMeshSourceGeometryData3D();

        NavigationServer3D.ParseSourceGeometryData(mesh, source, GetTree().CurrentScene);
        NavigationServer3D.BakeFromSourceGeometryData(mesh, source);

        region.NavigationMesh = mesh;

        if (Debug)
            GD.Print($"NavBaker: {mesh.GetPolygonCount()} polígonos de navegação");
    }
}
