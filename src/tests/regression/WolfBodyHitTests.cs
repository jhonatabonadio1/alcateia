using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O corpo do lobo leva tiro onde se vê (defeitos anotados em 14 e 15/11,
/// conferidos em 25/11: "o pescoço e o peito curvado ficam fora da cápsula,
/// e o tiro passa direto"; "o peito agachado passa direto no tiro").
///
/// O teste pega a malha do lobo na pose daquela hora (a pele calculada aqui,
/// osso por osso), acha os pontos do pescoço e do tronco que ficam **fora da
/// cápsula de movimento** e atira em cada um, de fora, na direção em que ele
/// sai. Tem de acertar.
///
/// Medido em 25/11, com o lobo novo (o "lobo natural" do Meshy): os dois
/// defeitos já não acontecem. O tronco desse modelo é mais reto que o do
/// lobo de 14/11 e cabe quase todo na cápsula: em pé, 1 ponto do pescoço e
/// do tronco fica a mais de 4 cm fora dela; andando, 2; agachado, 5; de
/// quatro, nenhum — e os 8 levam o tiro (a cápsula logo atrás, ou a esfera
/// da cabeça). O teste fica para segurar isso quando o modelo ou as poses
/// mudarem.
/// </summary>
public sealed class WolfBodyHitTest : RegressionTest
{
	public override string Name => "CorpoDoLoboNoTiro";
	public override string Description => "o tiro acerta o pescoço e o tronco do lobo onde eles saem da cápsula (em pé, andando, agachado e de quatro)";

	private static readonly Vector3 Spot = new(-15, 1, 13);

	/// <summary>Os ossos do pescoço e do tronco: a malha que pesa neles.</summary>
	private static readonly string[] Torso = { "mixamorig_Spine", "mixamorig_Spine1", "mixamorig_Spine2", "mixamorig_Neck" };

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(1);
		await b.Seconds(0.3);
		var bot = (CharacterBody3D)b.Bots()[0];
		RegressionBench.Freeze(bot);
		Health health = b.Health;
		Weapon gun = bot.GetNode<Weapon>("Weapon");
		gun.ProcessMode = Node.ProcessModeEnum.Always;
		var camera = bot.GetNode<Camera3D>("Head/Camera3D");
		await b.BecomeWolf();
		await b.Seconds(health.SpawnProtection + 0.2);
		var shapeNode = b.Player.GetNode<CollisionShape3D>("CollisionShape3D");

		int total = 0, hitsTotal = 0;
		foreach (string pose in new[] { "parado", "andando", "agachado", "de quatro" })
		{
			await b.Place(Spot, 0f, settle: 0.5);
			if (pose == "andando")
			{
				RegressionBench.Press("move_forward");
				await b.Seconds(0.6);
			}
			else if (pose == "agachado")
			{
				RegressionBench.Press("crouch");
				await b.Seconds(0.8);
			}
			else if (pose == "de quatro")
			{
				await b.Tap("move_forward");
				await b.Seconds(0.08);
				RegressionBench.Press("move_forward");
				await b.Seconds(0.7);
			}
			// O corpo para na pose desta hora (a animação junto).
			RegressionBench.Freeze(b.Player);
			await b.Frames(2);

			// A cápsula de movimento agora (agachado, ela encolhe).
			var capsule = (CapsuleShape3D)shapeNode.Shape;
			Transform3D shape = shapeNode.GlobalTransform;
			Vector3 bottom = shape * new Vector3(0, -capsule.Height * 0.5f + capsule.Radius, 0);
			Vector3 top = shape * new Vector3(0, capsule.Height * 0.5f - capsule.Radius, 0);

			List<Vector3> outside = OutsideCapsule(b.Player, bottom, top, capsule.Radius);
			int hits = 0;
			var missed = new List<string>();
			foreach (Vector3 surface in outside)
			{
				// Do eixo da cápsula para fora, na horizontal: é de lá que se vê o ponto
				// saindo do corpo. O alvo é 3 cm para dentro da pele.
				Vector3 outward = Outward(surface, bottom, top);
				Vector3 point = surface - outward * 0.03f;
				if (await Shoot(b, bot, gun, camera, health, point, outward)) hits++;
				else missed.Add($"({RegressionBench.F(point.X - b.Player.GlobalPosition.X)} {RegressionBench.F(point.Y - bottom.Y + capsule.Radius)} {RegressionBench.F(point.Z - b.Player.GlobalPosition.Z)})");
			}

			total += outside.Count;
			hitsTotal += hits;
			RegressionBench.Note($"{pose}: {outside.Count} pontos do pescoço e do tronco fora da cápsula, {hits} levaram o tiro" + (missed.Count > 0 ? "; passaram (x, altura do pé da cápsula, z): " + string.Join(" ", missed) : ""));
			b.Check(hits == outside.Count, $"{pose}: todo ponto do corpo fora da cápsula leva tiro ({hits} de {outside.Count})");

			b.Player.ProcessMode = Node.ProcessModeEnum.Inherit;
			RegressionBench.ReleaseAll();
			await b.Seconds(0.25);
		}

		RegressionBench.Note($"no total, {hitsTotal} de {total} tiros em pontos fora da cápsula acertaram");
		bot.ProcessMode = Node.ProcessModeEnum.Inherit;
		await b.Seconds(1.0);
	}

	/// <summary>O bot, a 8 m na direção <paramref name="from"/>, atira no
	/// ponto. Verdadeiro se o lobo levou dano.</summary>
	private static async Task<bool> Shoot(RegressionBench b, CharacterBody3D bot, Weapon gun, Camera3D camera, Health health, Vector3 point, Vector3 from)
	{
		// Pente cheio a cada tiro: com o pente acabando, o bot recarregava e o
		// tiro daquela hora não saía (parecia o corpo deixando o tiro passar).
		gun.Equip(Arsenal.Get("ar15"), Arsenal.Get("deagle"), 1);
		gun.SelectSlot(Slot.Primary);
		bot.GlobalPosition = new Vector3(point.X, Spot.Y, point.Z) + from * 8f;
		Aim(bot, camera, point);
		await b.Frames(2);
		health.SetHp(health.MaxHp);
		float before = health.Hp + health.Shield;
		gun.TryAttack();
		await b.Frames(3);
		bool took = health.Hp + health.Shield < before;
		await b.Seconds(0.1);
		return took;
	}

	private static Vector3 Outward(Vector3 p, Vector3 bottom, Vector3 top)
	{
		Vector3 outward = p - Closest(p, bottom, top);
		outward.Y = 0;
		return outward.LengthSquared() < 1e-4f ? Vector3.Right : outward.Normalized();
	}

	/// <summary>Até 10 pontos da pele do pescoço e do tronco, na pose de
	/// agora, que ficam a mais de 4 cm fora da cápsula, os mais saídos
	/// primeiro e espalhados (a pelo menos 12 cm um do outro).</summary>
	private static List<Vector3> OutsideCapsule(Player player, Vector3 bottom, Vector3 top, float radius)
	{
		Skeleton3D skeleton = AnimationKit.FindSkeleton(player.GetNode<WolfVisual>("WolfVisual").Model);
		var torso = new HashSet<int>(Torso.Select(skeleton.FindBone).Where(i => i >= 0));
		var found = new List<(Vector3 Point, float Out)>();
		foreach (Node node in skeleton.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (node is not MeshInstance3D { Mesh: ArrayMesh mesh, Skin: Skin skin }) continue;
			for (int s = 0; s < mesh.GetSurfaceCount(); s++)
			{
				Godot.Collections.Array arrays = mesh.SurfaceGetArrays(s);
				if (arrays[(int)Mesh.ArrayType.Bones].VariantType == Variant.Type.Nil) continue;
				Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
				float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				int per = vertices.Length > 0 ? bones.Length / vertices.Length : 4;
				for (int v = 0; v < vertices.Length; v++)
				{
					Vector3 skinned = Vector3.Zero;
					int dominant = -1;
					float strongest = 0f;
					for (int k = 0; k < per; k++)
					{
						float w = weights[v * per + k];
						if (w <= 0f) continue;
						int bind = bones[v * per + k];
						int bone = skin.GetBindBone(bind);
						if (bone < 0) bone = skeleton.FindBone(skin.GetBindName(bind));
						skinned += (skeleton.GetBoneGlobalPose(bone) * (skin.GetBindPose(bind) * vertices[v])) * w;
						if (w > strongest) { strongest = w; dominant = bone; }
					}
					if (!torso.Contains(dominant)) continue;
					Vector3 world = skeleton.GlobalTransform * skinned;
					float outBy = world.DistanceTo(Closest(world, bottom, top)) - radius;
					if (outBy > 0.04f) found.Add((world, outBy));
				}
			}
		}

		var picked = new List<Vector3>();
		foreach (var (point, _) in found.OrderByDescending(f => f.Out))
		{
			if (picked.Any(p => p.DistanceTo(point) < 0.12f)) continue;
			picked.Add(point);
			if (picked.Count == 10) break;
		}
		return picked;
	}

	private static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b)
	{
		Vector3 ab = b - a;
		float t = ab.LengthSquared() < 1e-9f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
		return a + ab * t;
	}

	/// <summary>Vira o corpo e a cabeça para a câmera olhar o ponto.</summary>
	private static void Aim(CharacterBody3D shooter, Camera3D camera, Vector3 point)
	{
		Vector3 direction = (point - camera.GlobalPosition).Normalized();
		shooter.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
		shooter.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.Asin(direction.Y), 0, 0);
	}
}
