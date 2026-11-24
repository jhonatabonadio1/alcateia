using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// As peças finas do Neon colidem na forma delas, não numa caixa do tamanho
/// total (Jhon, 24/11: "postes, placas e coisas pequenas estão com áreas de
/// colisão muito grandes: não consegui andar em regiões que pode andar, não
/// consegui atirar em oponentes nessas regiões"). O poste com braço era uma
/// parede invisível de 1,5 × 4 m; agora o tiro que passa ao lado dele segue,
/// e o que acerta o poste para nele (src/tools/mapas/colisao_exata.gd).
/// </summary>
public sealed class NeonThinCollisionTest : RegressionTest
{
	public override string Name => "NeonColisaoFina";
	public override string Description => "no Neon, postes, placas, mesas e bancos colidem na forma deles: o tiro ao lado do poste passa, no poste para";

	/// <summary>As peças que trocaram a caixa pela forma (colisao_exata.gd).</summary>
	private static readonly string[] Thin =
	{
		"SM_Floor_Light", "SM_Props_0", "SM_Props_BarProps", "SM_Props_Furniture", "SM_Props_CyberIce", "SM_Props_Screen",
		"SM_Props_Cafe", "SM_Props_Bench", "SM_Nature_Flowerbed", "SM_Nature_Rock", "SM_Nature_Log"
	};

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var map = GD.Load<PackedScene>("res://src/scenes/maps/neon.tscn").Instantiate<Node3D>();
		map.Position = new Vector3(3000, 0, 0);
		b.AddChild(map);
		await b.PhysicsFrame();
		await b.PhysicsFrame();

		StaticBody3D[] bodies = map.GetNode("Colisao").GetChildren().OfType<StaticBody3D>().ToArray();
		StaticBody3D[] thin = bodies.Where(body => Thin.Any(t => PieceOf(body).StartsWith(t))).ToArray();
		int boxes = thin.Count(body => body.GetNodeOrNull<CollisionShape3D>("Colisao")?.Shape is BoxShape3D);
		b.Check(thin.Length > 150, $"achou as peças finas ({thin.Length})");
		b.Check(boxes == 0, $"nenhuma delas com caixa ({boxes})");

		// Os postes: um tiro na altura do peito que passa a 0,5 m do pé do poste,
		// para os dois lados e nos dois eixos, não bate nele; um no poste, bate.
		PhysicsDirectSpaceState3D space = map.GetWorld3D().DirectSpaceState;
		StaticBody3D[] posts = bodies.Where(body => PieceOf(body).StartsWith("SM_Floor_Light")).ToArray();
		int beside = 0, besideHit = 0, center = 0, centerHit = 0;
		foreach (StaticBody3D post in posts)
		{
			Vector3 foot = FootOf(post);
			foreach (Vector3 axis in new[] { Vector3.Right, Vector3.Back })
			{
				Vector3 side = axis.Cross(Vector3.Up);
				foreach (float offset in new[] { -0.5f, 0.5f })
				{
					beside++;
					if (Hits(space, foot + Vector3.Up * 1.2f + side * offset, axis, post)) besideHit++;
				}
				center++;
				if (Hits(space, foot + Vector3.Up * 1.2f, axis, post)) centerHit++;
			}
		}
		RegressionBench.Note($"postes: {posts.Length}; tiros a 0,5 m do poste que bateram nele: {besideHit} de {beside}; no poste: {centerHit} de {center}");
		b.Check(posts.Length > 40, $"os postes de luz ({posts.Length})");
		b.Check(besideHit == 0, $"o tiro que passa a meio metro do poste segue ({besideHit} de {beside} bateram)");
		b.Check(centerHit >= center * 0.9, $"o tiro no poste para nele ({centerHit} de {center})");

		// As placas de anúncio, que o gerador deixou sem colisão (24/11: "essas
		// placas estão sem colisão, então estou passando por elas"): um tiro no
		// meio de cada uma, de frente e de lado, para nela.
		MeshInstance3D[] ads = map.FindChildren("SM_Props_Ads*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
		int adShots = 0, adHits = 0;
		foreach (MeshInstance3D ad in ads)
		{
			Vector3 middle = ad.GlobalTransform * ad.GetAabb().GetCenter();
			foreach (Vector3 axis in new[] { Vector3.Right, Vector3.Back })
			{
				adShots++;
				var query = PhysicsRayQueryParameters3D.Create(middle - axis * 1.5f, middle + axis * 1.5f);
				var hit = space.IntersectRay(query);
				if (hit.Count > 0 && hit["collider"].Obj is StaticBody3D body && body.Name.ToString().Contains("SM_Props_Ads")) adHits++;
			}
		}
		RegressionBench.Note($"placas: {ads.Length}; tiros nelas que pararam: {adHits} de {adShots}");
		b.Check(ads.Length > 0 && adHits >= adShots * 0.9, $"a placa de anúncio segura o tiro ({adHits} de {adShots})");

		// As paredes invisíveis de dentro do mapa saíram (tapavam portas, salas
		// e pátios); as do limite ficam.
		b.Check(!bodies.Any(body => body.Name.ToString().StartsWith("Barreira")), "nenhuma barreira invisível dentro do mapa");
		b.Check(bodies.Count(body => body.Name.ToString().StartsWith("Limite")) >= 4, "as paredes do limite ficam");

		map.QueueFree();
		await b.PhysicsFrame();
	}

	private static string PieceOf(Node body)
	{
		string name = body.Name;
		int cut = name.IndexOf('_');
		return name.StartsWith("pc") && cut > 0 ? name[(cut + 1)..] : name;
	}

	/// <summary>O pé do poste: o meio da forma no chão (a forma vai do pé ao topo).</summary>
	private static Vector3 FootOf(StaticBody3D post)
	{
		var shape = post.GetNode<CollisionShape3D>("Colisao");
		Vector3[] faces = shape.Shape is ConcavePolygonShape3D cp ? cp.GetFaces() : ((BoxShape3D)shape.Shape).GetDebugMesh().GetFaces();
		Transform3D t = shape.GlobalTransform;
		// os pontos baixos (até 0,5 m do chão) são o pé do poste
		Vector3[] world = faces.Select(p => t * p).ToArray();
		float floor = world.Min(p => p.Y);
		Vector3[] low = world.Where(p => p.Y < floor + 0.5f).ToArray();
		Vector3 mid = low.Aggregate(Vector3.Zero, (a, p) => a + p) / low.Length;
		return new Vector3(mid.X, floor, mid.Z);
	}

	/// <summary>Um raio de 3 m que atravessa o ponto no eixo dado: bateu em <paramref name="target"/>?</summary>
	private static bool Hits(PhysicsDirectSpaceState3D space, Vector3 through, Vector3 axis, CollisionObject3D target)
	{
		var query = PhysicsRayQueryParameters3D.Create(through - axis * 1.5f, through + axis * 1.5f);
		query.HitBackFaces = true;
		var hit = space.IntersectRay(query);
		return hit.Count > 0 && hit["collider"].Obj == target;
	}
}
