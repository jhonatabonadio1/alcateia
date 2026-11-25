using Godot;
using System.Threading.Tasks;

/// <summary>
/// Tiro na cabeça (bug do Jhon, 14/11: "mirei escopado exatamente na
/// cabeça do humano, e não deu dano"). Dois defeitos: o recuo subia a
/// câmera antes de o tiro sair, então todo tiro ia 1° acima da cruz; e a
/// cabeça era a faixa de cima da cápsula, que pegava só a metade de cima
/// da cabeça do humano e nada da do lobo, curvado para a frente, fora da
/// cápsula. Agora o tiro sai de onde a cruz está e a cabeça é a do modelo
/// (<see cref="HeadHitbox"/>).
/// </summary>
public sealed class HeadshotTest : RegressionTest
{
	public override string Name => "TiroNaCabeca";
	public override string Description => "o tiro sai onde a cruz está, da cintura e no zoom; a cabeça é a do modelo, no humano e no lobo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(1);
		await b.Seconds(0.3);
		var bot = (CharacterBody3D)b.Bots()[0];
		RegressionBench.Freeze(bot);
		bot.GlobalPosition = new Vector3(-15, 1, 13);
		bot.Rotation = Vector3.Zero;
		Health target = bot.GetNode<Health>("Health");

		// A proteção de quem nasceu passa (a vida conta com o bot parado).
		await b.Seconds(target.SpawnProtection + 0.3);

		Weapon w = b.Weapon;
		w.Equip(Arsenal.Get("ar15"), Arsenal.Get("deagle"), 1);
		w.SelectSlot(Slot.Primary);
		WeaponDefinition m16 = Arsenal.Get("ar15");
		float body = CombatRules.FirearmDamage(m16.Damage, 1, false, m16.HeadshotMultiplier);
		float head = CombatRules.FirearmDamage(m16.Damage, 1, true, m16.HeadshotMultiplier);

		bool confirmedHead = false;
		w.HitConfirmed += h => confirmedHead = h;
		var camera = b.Player.GetNode<Camera3D>("Head/Camera3D");

		foreach (float distance in new[] { 8f, 25f })
		{
			foreach (bool zoom in new[] { false, true })
			{
				string where = $"{distance:0} m{(zoom ? ", no zoom" : "")}";

				// Na cabeça que se vê, no peito, e um palmo acima da cabeça. O peito é
				// o meio da peça do peito (BodyHitbox, 25/11): até 25/11 era meio metro
				// abaixo da cabeça, que de lado cai no ar na frente do peito do humano
				// mirando — a cápsula de antes levava tiro ali, o corpo de agora não.
				foreach (var (aimAt, expected, isHead, what) in new (float, float, bool, string)[]
				{
					(0f, head, true, "na cabeça"),
					(float.NaN, body, false, "no peito"),
					(0.3f, 0f, false, "acima da cabeça"),
				})
				{
					await b.Place(bot.GlobalPosition + new Vector3(distance, 0, 0), -90f, settle: 0.4);
					if (zoom)
					{
						RegressionBench.Press("special");
						await b.Seconds(0.3);
					}

					b.Check(HeadHitbox.TryGet(bot, out Vector3 center, out _), "o bot tem cabeça de modelo");
					Aim(b.Player, camera, float.IsNaN(aimAt) ? Chest(bot) : center + Vector3.Up * aimAt);
					await b.Frames(2);

					target.SetHp(target.MaxHp);
					confirmedHead = false;
					RegressionBench.Press("attack");
					await b.Frames(1);
					RegressionBench.Release("attack");
					await b.Frames(3);

					float dealt = target.MaxHp - target.Hp;
					b.Near(expected, dealt, 0.5, $"{where}, {what}: dano");
					if (expected > 0) b.Check(confirmedHead == isHead, $"{where}, {what}: {(isHead ? "conta" : "não conta")} como cabeça");
					RegressionBench.ReleaseAll();
					await b.Seconds(0.15);
				}
			}
		}

		await WolfHead(b, bot);
	}

	/// <summary>O lobo tem a cabeça à frente da cápsula (o do Meshy, mais
	/// reto que o Mutant, a 0,43 m do eixo): o bot atira
	/// nela (no jogador, de lobo) e acerta a cabeça; no corpo, não.</summary>
	private static async Task WolfHead(RegressionBench b, CharacterBody3D bot)
	{
		await b.BecomeWolf();
		await b.Place(bot.GlobalPosition + new Vector3(10, 0, 0), 90f, settle: 0.6);
		RegressionBench.Freeze(b.Player);
		await b.Seconds(b.Health.SpawnProtection);

		Weapon gun = bot.GetNode<Weapon>("Weapon");
		gun.ProcessMode = Node.ProcessModeEnum.Always;
		bool confirmedHead = false;
		gun.HitConfirmed += h => confirmedHead = h;
		var camera = bot.GetNode<Camera3D>("Head/Camera3D");

		b.Check(HeadHitbox.TryGet(b.Player, out Vector3 center, out float radius), "o lobo tem cabeça de modelo");
		Vector3 flat = new Vector3(center.X - b.Player.GlobalPosition.X, 0, center.Z - b.Player.GlobalPosition.Z);
		float capsule = ((CapsuleShape3D)b.Player.GetNode<CollisionShape3D>("CollisionShape3D").Shape).Radius;
		RegressionBench.Note($"cabeça do lobo a {RegressionBench.F(flat.Length())} m do eixo da cápsula (cápsula de raio {RegressionBench.F(capsule)}, cabeça cabe numa bola de {RegressionBench.F(radius)})");
		// O meio da cabeça fica fora da cápsula (até 25/11 a conta usava o raio da
		// esfera da cabeça; a peça de agora é mais comprida, e a bola que a contém
		// não diz nada sobre onde ela está).
		b.AtLeast(capsule, flat.Length(), "a cabeça do lobo fica fora da cápsula");

		foreach (var (point, isHead, what) in new (Vector3, bool, string)[]
		{
			(center, true, "na cabeça"),
			// O corpo: o meio da peça do osso Spine (a barriga). Até 25/11 era o
			// centro da cápsula mais 10 cm, que de frente fica atrás do focinho
			// do lobo curvado — com a cabeça do tamanho da que se vê, o raio até
			// ali passa pelo queixo.
			(Chest(b.Player, "mixamorig_Spine"), false, "no corpo"),
		})
		{
			Aim(bot, camera, point);
			await b.Seconds(0.3);
			float before = b.Health.Hp + b.Health.Shield;
			confirmedHead = !isHead;
			gun.TryAttack();
			await b.Frames(3);
			b.Check(b.Health.Hp + b.Health.Shield < before, $"lobo, {what}: levou dano");
			b.Check(confirmedHead == isHead, $"lobo, {what}: {(isHead ? "conta" : "não conta")} como cabeça");
		}

		// Descongela e deixa os sons acabarem: som tocando em nó parado
		// fica preso na saída (o Output acusa recurso em uso).
		b.Player.ProcessMode = Node.ProcessModeEnum.Inherit;
		bot.ProcessMode = Node.ProcessModeEnum.Inherit;
		await b.Seconds(1.0);
	}

	/// <summary>O meio de uma parte do corpo: o das peças do osso (o peito é o Spine2).</summary>
	private static Vector3 Chest(Node3D body, string bone = "mixamorig_Spine2")
	{
		var parts = new System.Collections.Generic.List<BodyHitbox.Part>();
		BodyHitbox.TryGet(body, parts);
		Vector3 sum = Vector3.Zero;
		int n = 0;
		foreach (BodyHitbox.Part part in parts)
		{
			if (part.Bone != bone) continue;
			sum += new Vector3(part.Shape.Center.X, part.Shape.Center.Y, part.Shape.Center.Z);
			n++;
		}
		return n > 0 ? sum / n : body.GlobalPosition + Vector3.Up * 0.3f;
	}

	/// <summary>Vira o corpo e a cabeça para a câmera olhar o ponto.</summary>
	private static void Aim(CharacterBody3D shooter, Camera3D camera, Vector3 point)
	{
		Vector3 direction = (point - camera.GlobalPosition).Normalized();
		shooter.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
		shooter.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.Asin(direction.Y), 0, 0);
	}
}
