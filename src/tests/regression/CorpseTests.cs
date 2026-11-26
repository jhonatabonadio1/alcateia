using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O corpo do morto (Jhon, 26/11: "corpo do morto afundando/ficando
/// transparente, dá para ver o interior dos personagens. Ele deve ficar
/// transparente por igual, sem poder ver o interior do corpo. E não deve
/// afundar, deve ficar no limite do chão").
///
/// Para a Eve e o Barut humanos e os dois lobos, como boneco (o corpo de
/// outro jogador ou bot que se vê): mata, e a cada quadro da morte inteira,
/// até sumir, mede a pele deformada pelo esqueleto contra o chão logo abaixo
/// dela. Não pode entrar no chão nem ficar pairando ao assentar. No sumiço,
/// toda superfície do corpo some junto e com a profundidade escrita antes da
/// cor (<see cref="CorpseFade"/>): sem isso o Godot não ordena os pedaços de
/// um mesmo corpo, e o de dentro (olhos, dentes, forro) aparece.
/// </summary>
public sealed class CorpseGroundTest : RegressionTest
{
	public override string Name => "CorpoMortoNoChao";
	public override string Description => "o corpo do morto (humano e lobo, dos dois times) fica no limite do chão e some por igual, sem mostrar o de dentro";
	public override string Scene => RegressionBench.Match;

	/// <summary>Quanto a pele pode entrar no chão (a sola, a junta do
	/// piso) e quanto pode pairar acima dele, depois de assentado.</summary>
	private const float Sink = 0.03f, Float = 0.04f;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		var space = b.Player.GetWorld3D().DirectSpaceState;

		foreach (bool wolf in new[] { false, true })
			foreach (int team in new[] { Teams.Blue, Teams.Orange })
			{
				string who = $"{(team == Teams.Blue ? "azul" : "laranja")} {(wolf ? "lobo" : "humano")}";
				var bot = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
				bot.Name = "Morto";
				bot.Team = team;
				bot.Remote = true;
				b.GetTree().CurrentScene.AddChild(bot);
				await b.Frames(3);
				bot.GlobalPosition = b.Player.GlobalPosition + new Vector3(3, 0.2f, 0);
				bot.CollisionLayer = 0;
				if (wolf)
				{
					bot.GetNode<FormController>("FormController").RemoteSet(Form.Wolf);
					await b.Seconds(1.2);
				}
				await b.Seconds(0.5);

				Node3D body = wolf ? bot.GetNode<Node3D>("WolfVisual") : bot.GetNode<Node3D>("Human");
				var all = body.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToList();
				RegressionBench.Note($"{who}: {all.Count} malhas; com pele {all.Count(m => m.Skin != null)}; visíveis {all.Count(m => m.IsVisibleInTree())}; corpo visível {body.IsVisibleInTree()}, bot {bot.Visible}");
				var health = bot.GetNode<Health>("Health");
				health.TakeDamage(99999f);
				b.Check(health.IsDead, $"{who}: morreu");

				float deepest = float.MaxValue, deepestAt = 0f, rest = 0f, fadeStart = -1f, gone = -1f;
				int frames = 0, fadeFrames = 0, fadeBad = 0, fadeSurfaces = 0;
				var reasons = new HashSet<string>();
				double t = 0;
				while (t < 9.0)
				{
					await b.Frames(1);
					t += 1.0 / 60.0;
					var meshes = SkinnedMeshes(body);
					if (meshes.Count == 0)
					{
						gone = (float)t;
						break;
					}
					var (low, at) = Lowest(meshes);
					var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 0.6f, at + Vector3.Down * 3f, 1));
					if (hit.Count == 0) continue;
					float gap = low - ((Vector3)hit["position"]).Y;
					if (gap < deepest) { deepest = gap; deepestAt = (float)t; }
					rest = gap;
					frames++;

					// O sumiço: toda superfície do corpo some junto, com a profundidade antes.
					var (surfaces, faded, bad) = Fade(meshes, reasons);
					if (faded > 0)
					{
						if (fadeStart < 0) fadeStart = (float)t;
						fadeFrames++;
						fadeSurfaces = surfaces;
						if (bad > 0 || faded < surfaces) fadeBad++;
					}
				}

				RegressionBench.Note($"{who}: {frames} quadros; a pele no máximo {RegressionBench.F(-deepest * 100)} cm dentro do chão (em {RegressionBench.F(deepestAt)} s); " +
					$"no último quadro, {RegressionBench.F(rest * 100)} cm acima; sumiço de {RegressionBench.F(fadeStart)} s a {RegressionBench.F(gone)} s, {fadeFrames} quadros, {fadeSurfaces} superfícies, " +
					$"{fadeBad} quadros com o de dentro à mostra{(reasons.Count > 0 ? " (" + string.Join("; ", reasons) + ")" : "")}");
				b.Check(frames > 30, $"{who}: o corpo foi medido durante a morte ({frames} quadros)");
				b.AtLeast(-Sink, deepest, $"{who}: a pele não entra no chão (m, no pior quadro)");
				b.AtMost(Float, rest, $"{who}: assentado, encosta no chão, não paira (m)");
				b.Check(fadeFrames > 10, $"{who}: o corpo some aos poucos ({fadeFrames} quadros de sumiço)");
				b.AtMost(0, fadeBad, $"{who}: no sumiço, todo o corpo some por igual, com a profundidade antes da cor");

				bot.QueueFree();
				await b.Frames(2);
			}
	}

	/// <summary>As malhas visíveis da pele (com esqueleto): a arma na mão e
	/// os discos dos olhos não são corpo.</summary>
	private static List<MeshInstance3D> SkinnedMeshes(Node3D root) =>
		root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
			.Where(m => Shown(m, root) && m.Mesh != null && m.Skin != null && m.GetNodeOrNull<Skeleton3D>(m.Skeleton) != null)
			.ToList();

	/// <summary>À mostra dentro do corpo (do nó do corpo para baixo): o boneco
	/// inteiro fica escondido enquanto ninguém o vê, e isso não é o sumiço.</summary>
	private static bool Shown(Node3D node, Node3D root)
	{
		for (Node n = node; n != null; n = n.GetParent())
		{
			if (n is Node3D n3 && !n3.Visible) return false;
			if (n == root) return true;
		}
		return true;
	}

	/// <summary>O ponto mais baixo da pele deformada (um vértice a cada três).</summary>
	private static (float Low, Vector3 At) Lowest(List<MeshInstance3D> meshes)
	{
		float low = float.MaxValue;
		Vector3 at = Vector3.Zero;
		foreach (MeshInstance3D mesh in meshes)
		{
			Skeleton3D skeleton = mesh.GetNode<Skeleton3D>(mesh.Skeleton);
			Skin skin = mesh.Skin;
			var binds = new Transform3D[skin.GetBindCount()];
			for (int k = 0; k < binds.Length; k++)
			{
				int bone = skin.GetBindBone(k);
				if (bone < 0) bone = skeleton.FindBone(skin.GetBindName(k));
				binds[k] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone) * skin.GetBindPose(k);
			}
			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				var arrays = mesh.Mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				int per = verts.Length > 0 ? bones.Length / verts.Length : 4;
				for (int v = 0; v < verts.Length; v += 3)
				{
					Vector3 p = Vector3.Zero;
					for (int j = 0; j < per; j++)
					{
						float w = weights[v * per + j];
						if (w > 0f) p += binds[bones[v * per + j]] * verts[v] * w;
					}
					if (p.Y < low) { low = p.Y; at = p; }
				}
			}
		}
		return (low, at);
	}

	/// <summary>Quantas superfícies da pele há, quantas estão sumindo, e
	/// quantas sumindo sem a profundidade escrita antes da cor (o de dentro
	/// aparece).</summary>
	private static (int Surfaces, int Faded, int Bad) Fade(List<MeshInstance3D> meshes, HashSet<string> reasons)
	{
		int surfaces = 0, faded = 0, bad = 0;
		foreach (MeshInstance3D mesh in meshes)
			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				surfaces++;
				Material m = mesh.GetActiveMaterial(s);
				if (CorpseFade.IsFading(m))
				{
					faded++;
					continue;
				}
				// Transparente sem a passada de profundidade que valha: o Godot
				// desenha os pedaços do mesmo corpo fora de ordem.
				if (m is BaseMaterial3D bm && bm.Transparency != BaseMaterial3D.TransparencyEnum.Disabled && bm.AlbedoColor.A < 0.99f)
				{
					faded++;
					bad++;
					reasons.Add($"{bm.Transparency} com alfa abaixo de 0,99: a passada de profundidade do Godot descarta tudo abaixo disso");
				}
			}
		return (surfaces, faded, bad);
	}
}
