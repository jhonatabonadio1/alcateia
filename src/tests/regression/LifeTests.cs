using Godot;
using System.Threading.Tasks;

/// <summary>Morrer de lobo e renascer: volta humano, com vida cheia
/// (decisões do Jhon, v0.26.8 e v0.26.10). O WP não cai: até 25/11 a morte
/// tirava 100; decisão do Jhon, 26/11: "ao morrer, não pode perder KP".</summary>
public sealed class RespawnTest : RegressionTest
{
	public override string Name => "RenasceHumano";
	public override string Description => "quem morre de lobo renasce humano, de vida cheia, e não perde WP";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		float wp = b.Form.Wp;
		b.Health.TakeDamage(99999f);
		await b.Seconds(0.3);

		b.Check(b.Health.IsDead, "morreu");
		b.Near(wp, b.Form.Wp, 0.01, "WP depois de morrer (não cai)");

		await b.Seconds(b.Health.RespawnWait + 0.2);
		b.Check(b.Health.TryRespawn(), "renasceu");
		await b.Seconds(0.3);

		b.Check(b.Form.Current == Form.Human, $"forma ao renascer: {b.Form.Current}");
		b.Check(!b.Player.GetNode<Node3D>("WolfVisual").Visible, "o lobo some");
		b.Near(b.Health.MaxHp, b.Health.Hp, 0.01, "vida cheia");
		b.Near(0, b.Health.MaxShield, 0.01, "sem escudo de lobo");
		b.Check(!b.Form.IsThirdPerson, "câmera em primeira pessoa");
	}
}

/// <summary>A garra do Bruto contra humano: o dano da classe × 1,6.</summary>
public sealed class ClawDamageTest : RegressionTest
{
	public override string Name => "DanoDaGarra";
	public override string Description => "a garra do Bruto tira 75 × 1,6 = 120 de um humano";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var target = b.Bots()[0];
		RegressionBench.Freeze(target);
		var health = target.GetNode<Health>("Health");

		await b.BecomeWolf();
		await b.Place(new Vector3(0, 1, 2), 0f);
		target.GlobalPosition = new Vector3(0, 1, 0.6f);
		await b.Seconds(3.5); // proteção de quem acabou de nascer

		float before = health.Hp;
		await b.Tap("attack");
		await b.Seconds(0.6);
		b.Near(120f, before - health.Hp, 0.5, "dano da primeira garrada");
	}
}

/// <summary>
/// Quem morre e renasce volta com os materiais de antes (Jhon, 22/11:
/// depois de algumas mortes os personagens ficavam com os olhos e os dentes
/// saltando do rosto, sem cabelo e com as luvas rasgadas). Ao sumir, o
/// corpo troca os materiais por cópias transparentes; ao renascer, só a
/// opacidade voltava, e o material transparente desenhava o que tem por
/// dentro do corpo por cima da pele.
/// </summary>
public sealed class RespawnMaterialsTest : RegressionTest
{
	public override string Name => "RenasceInteiro";
	public override string Description => "quem some ao morrer renasce com os materiais de antes (nenhum transparente), nos dois personagens";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		foreach (int team in new[] { Teams.Orange, Teams.Blue })
		{
			var bot = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
			bot.Name = "Morto";
			bot.Team = team;
			bot.Remote = true;
			b.GetTree().CurrentScene.AddChild(bot);
			await b.Frames(3);
			bot.GlobalPosition = b.Player.GlobalPosition + new Vector3(3, 0, 0);
			bot.CollisionLayer = 0;

			var animator = bot.GetNode<CharacterAnimator>("Human");
			var before = Surfaces(animator);
			var health = bot.GetNode<Health>("Health");

			health.TakeDamage(99999f);
			await b.Seconds(4.5);
			b.Check(health.IsDead, $"time {team}: morreu");
			b.Check(animator.HasFadeMaterials, $"time {team}: o corpo sumiu com os materiais transparentes");

			await b.Seconds(Mathf.Max(0f, health.RespawnWait - 4.3f) + 0.2);
			b.Check(health.TryRespawn(), $"time {team}: renasceu");
			await b.Frames(3);

			var after = Surfaces(animator);
			int changed = 0, transparent = 0;
			foreach (var (key, (material, transparency)) in after)
			{
				// O que nasceu depois (a marca de inimigo) não é do corpo.
				if (!before.TryGetValue(key, out var old)) continue;
				if (old.Material != material) changed++;
				if (transparency == BaseMaterial3D.TransparencyEnum.Alpha || transparency == BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass)
					if (old.Transparency != transparency) transparent++;
			}
			RegressionBench.Note($"time {team}: {after.Count} superfícies; trocadas {changed}, transparentes a mais {transparent}");
			b.Check(!animator.HasFadeMaterials, $"time {team}: nenhum material do sumiço ficou");
			b.AtMost(0, changed, $"time {team}: cada superfície com o material de antes");
			b.AtMost(0, transparent, $"time {team}: nenhuma superfície transparente que não era");

			bot.QueueFree();
			await b.Frames(2);
		}
	}

	private static System.Collections.Generic.Dictionary<string, (Material Material, BaseMaterial3D.TransparencyEnum Transparency)> Surfaces(Node root)
	{
		var result = new System.Collections.Generic.Dictionary<string, (Material, BaseMaterial3D.TransparencyEnum)>();
		foreach (Node node in root.FindChildren("*", nameof(MeshInstance3D), true, false))
		{
			var mesh = (MeshInstance3D)node;
			if (mesh.Mesh == null || node is HeldRig || IsWeapon(mesh)) continue;
			for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
			{
				Material active = mesh.GetActiveMaterial(s);
				var mode = active is BaseMaterial3D bm ? bm.Transparency : BaseMaterial3D.TransparencyEnum.Disabled;
				result[$"{mesh.GetPath()}#{s}"] = (active, mode);
			}
		}
		return result;
	}

	private static bool IsWeapon(Node node)
	{
		for (Node up = node; up != null; up = up.GetParent())
			if (up is HeldRig || up is HeldWeapon) return true;
		return false;
	}
}
