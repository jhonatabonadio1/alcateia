using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>As nuvens do céu (pedido do Jhon, 26/11: o shader de nuvens do
/// Setor Zero também nos mapas de dia e de noite): todo mapa a céu aberto
/// declara o clima dele, o Armazém (fechado) não, e as camadas entram no
/// mapa sem repetir as de quem já as traz na cena.</summary>
public sealed class SkyCloudsTest : RegressionTest
{
	public override string Name => "NuvensNosMapas";
	public override string Description => "cada mapa a céu aberto ganha as camadas de nuvens do clima dele ao carregar; o Armazém não; o Setor Zero fica com as da cena";

	public override async Task Run(RegressionBench b)
	{
		string[] open = { "cidade", "deserto", "estacao", "obra", "neon" };
		foreach (string id in open)
		{
			MapInfo info = Maps.Get(id);
			b.Check(SkyClouds.Climates.ContainsKey(info.Clouds), $"{id}: declara um clima de nuvens que existe ('{info.Clouds}')");
		}
		b.Check(Maps.Get("neon").Clouds == "noite" && Maps.Get("estacao").Clouds == "tarde" && Maps.Get("deserto").Clouds == "dia_seco",
			"o Neon é de noite, a Estação de fim de tarde, o Deserto de céu limpo");
		b.Check(string.IsNullOrEmpty(Maps.Get("armazem_estendido").Clouds), "o Armazém é fechado: sem nuvens");
		b.Check(string.IsNullOrEmpty(Maps.Training.Clouds), "o Setor Zero não declara clima: traz as nuvens na cena");
		b.Check(Maps.All.All(m => string.IsNullOrEmpty(m.Clouds) || SkyClouds.Climates.ContainsKey(m.Clouds)), "nenhum mapa da lista com clima desconhecido");

		// As camadas entram num mapa qualquer: um nó Nuvens, uma malha por camada, sem sombra, com o material do shader.
		foreach (var (climate, layers) in SkyClouds.Climates)
		{
			var map = new Node3D { Name = "Mapa" };
			b.Scene.AddChild(map);
			Node3D clouds = SkyClouds.AddTo(map, climate);
			b.Check(clouds != null && clouds.Name == "Nuvens" && clouds.GetChildCount() == layers.Length, $"{climate}: {layers.Length} camadas");
			bool ok = clouds != null && clouds.GetChildren().OfType<MeshInstance3D>().All(mesh =>
				mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off
				&& mesh.Mesh is PlaneMesh plane && plane.Material is ShaderMaterial material
				&& material.Shader != null && material.GetShaderParameter("brilho").AsSingle() > 0.5f
				&& mesh.Position.Y > 400f);
			b.Check(ok, $"{climate}: planos altos, sem sombra, com o shader de nuvens");
			// De novo no mesmo mapa: não repete.
			b.Check(SkyClouds.AddTo(map, climate) == null && map.GetChildren().Count(c => c.Name.ToString().StartsWith("Nuvens")) == 1, $"{climate}: não repete");
			map.QueueFree();
		}

		var none = new Node3D();
		b.Scene.AddChild(none);
		b.Check(SkyClouds.AddTo(none, "") == null && none.GetChildCount() == 0, "sem clima, nada entra");
		none.QueueFree();
		await b.PhysicsFrame();
	}
}

/// <summary>O Setor Zero carregado pelo MapSlot fica com as nuvens da cena dele (as do céu e o mar de nuvens do precipício).</summary>
public sealed class SetorZeroCloudsTest : SetorZeroTest
{
	public override string Name => "SetorZeroNuvens";
	public override string Description => "o Setor Zero tem as seis camadas de nuvens da cena dele (quatro no precipício, duas no céu), e o MapSlot não põe outras";

	public override async Task Run(RegressionBench b)
	{
		Node map = b.Scene.GetNode("Map/Setor Zero");
		Node clouds = map.GetNodeOrNull("Nuvens");
		b.Check(clouds != null && clouds.GetChildCount() == 6, $"seis camadas ({clouds?.GetChildCount()})");
		b.Check(map.GetChildren().Count(c => c.Name.ToString().StartsWith("Nuvens")) == 1, "um nó de nuvens só");
		var layers = clouds.GetChildren().OfType<MeshInstance3D>().ToArray();
		b.Check(layers.Count(l => l.Position.Y < 0) == 4 && layers.Count(l => l.Position.Y > 400) == 2, "quatro abaixo do campo (o precipício) e duas no céu");
		b.Check(layers.All(l => l.Mesh is PlaneMesh p && p.Material is ShaderMaterial), "todas com o shader de nuvens");
		await b.PhysicsFrame();
	}
}
