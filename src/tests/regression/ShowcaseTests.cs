using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A vitrine (<see cref="CharacterPreview"/>): o personagem fica **em cima**
/// do pedestal, sem a pele passar do disco — nem para baixo do tampo nem
/// para fora da borda (19/11: "pé atravessando o limite"). Mede a pele
/// deformada pelo esqueleto, vértice a vértice, ao longo do parado.
/// </summary>
public sealed class ShowcaseFeetTest : RegressionTest
{
	public override string Name => "VitrineNoPedestal";
	public override string Description => "na vitrine, os pés do humano e do lobo (azul e vermelho) ficam em cima do disco, sem afundar e sem sair da borda";

	public override async Task Run(RegressionBench b)
	{
		string dir = OS.GetEnvironment("FOTO_DIR");
		foreach ((string human, string wolf, string name) in new[]
		{
			("", "", "padrão"),
			("res://src/assets/personagens/barut/humano.glb", "res://src/assets/personagens/barut/lobo_vermelho.glb", "Barut"),
		})
		{
			foreach (bool isWolf in new[] { false, true })
			{
				var layer = new CanvasLayer { Layer = 50 };
				b.AddChild(layer);
				var preview = new CharacterPreview { HumanModel = human, WolfModel = wolf, Size = new Vector2(420, 640) };
				layer.AddChild(preview);
				preview.Wolf = isWolf;
				await b.Frames(3);
				string who = $"{name}, {(isWolf ? "lobo" : "humano")}";

				// O modelo à vista, dentro do mundo da vitrine.
				Node3D model = preview.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
					.Where(n => n.GetParent() is Node3D p && p.GetParent() is SubViewport && n.Visible && n.FindChildren("*", "MeshInstance3D", true, false).Count > 0)
					.FirstOrDefault();
				b.Check(model != null, $"{who}: o modelo está na vitrine");
				if (model == null) { layer.QueueFree(); continue; }

				float low = float.MaxValue, far = 0f;
				for (int i = 0; i < 24; i++)
				{
					await b.Seconds(0.25);
					(float y, float r) = SkinPoints.Lowest(model);
					low = Mathf.Min(low, y);
					far = Mathf.Max(far, r);
					if (i == 0 && !string.IsNullOrEmpty(dir))
					{
						await b.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
						var vp = preview.GetChild<SubViewport>(0);
						vp.GetTexture().GetImage().SavePng($"{dir}/{name}_{(isWolf ? "lobo" : "humano")}.png");
					}
				}
				RegressionBench.Note($"{who}: o ponto mais baixo da pele fica a {RegressionBench.F(low * 100)} cm do tampo (negativo = dentro do disco); o mais longe do eixo, a {RegressionBench.F(far * 100)} cm (o disco tem 85)");
				b.AtLeast(-0.005, low, $"{who}: a pele não afunda no tampo do pedestal (m)");
				b.AtMost(0.02, low, $"{who}: e encosta nele, não flutua (m)");
				b.AtMost(0.83, far, $"{who}: nada passa da borda do disco (m do eixo)");
				layer.QueueFree();
				await b.Frames(2);
			}
		}
	}
}
