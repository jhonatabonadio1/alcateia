using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O especial com mais dano e em área (Jhon, 26/11: "Ultimate precisa de
/// mais poder de dano e em área, afetando mais pessoas ao redor e mais dano
/// ao alvo principal"). O lobo salta num inimigo travado com outros em volta:
/// o principal leva o dano cheio; quem está até <see cref="Weapon.PounceAreaRadius"/>
/// leva uma fração que cai com a distância; atrás de parede e fora da área,
/// nada. Era 2,6 × a garra só no principal e 30% só a 1,8 m.
/// </summary>
public sealed class SpecialAreaTest : RegressionTest
{
	public override string Name => "EspecialEmArea";
	public override string Description => "o especial tira mais do alvo principal e fere quem está em volta, menos quanto mais longe; parede protege, fora da área nada";

	public override async Task Run(RegressionBench b)
	{
		// Cinco bots novos, da cena do bot (os do campo de treino podem estar de
		// lobo), todos humanos e parados.
		b.RemoveBots();
		await b.Frames(2);
		var scene = GD.Load<PackedScene>("res://src/scenes/bot.tscn");
		for (int i = 0; i < 5; i++)
		{
			var extra = scene.Instantiate<CharacterBody3D>();
			extra.Name = $"BotArea{i}";
			b.Scene.AddChild(extra);
		}
		await b.Frames(2);
		CharacterBody3D[] bots = b.Bots().Where(x => !x.IsQueuedForDeletion()).Take(5).ToArray();
		b.Check(bots.Length == 5, $"cinco bots para a cena ({bots.Length})");
		if (bots.Length < 5) return;
		Health mine = b.Health;
		var names = new[] { "principal", "perto", "na borda", "atras da parede", "fora da area" };
		var spots = new[]
		{
			new Vector3(-3f, 1, -3.5f),     // o travado
			new Vector3(-2.3f, 1, -1.5f),   // ~2 m de onde o lobo cai
			new Vector3(-0.5f, 1, -1.0f),   // ~3,1 m
			new Vector3(-5.6f, 1, -3.5f),   // ~3,3 m, atrás de uma parede
			new Vector3(-2.3f, 1, 2.0f)     // ~5,5 m
		};

		// A parede entre onde o lobo cai e o de trás.
		var wall = new StaticBody3D { CollisionLayer = 1, Position = new Vector3(-4.3f, 1.5f, -3.5f) };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.3f, 3f, 1.6f) } });
		b.Scene.AddChild(wall);

		var healths = new Health[5];
		var taken = new float[5];
		for (int i = 0; i < 5; i++)
		{
			healths[i] = bots[i].GetNode<Health>("Health");
			RegressionBench.Freeze(bots[i]);
			bots[i].GlobalPosition = spots[i];
		}
		await b.BecomeWolf();
		await b.Until(() => healths.All(h => !h.IsProtected), 6.0);
		for (int i = 0; i < 5; i++)
		{
			bots[i].GlobalPosition = spots[i];
			// Todos do outro time e humanos, com vida de sobra para medir o golpe inteiro.
			healths[i].Team = mine.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
			healths[i].SetPool(2000f, 0f, true);
			int k = i;
			healths[i].Damaged += (amount, _) => taken[k] += amount;
		}

		Weapon w = b.Weapon;
		w.SpecialFree = false;
		w.FillSpecial();
		mine.SetHp(mine.MaxHp);
		await b.Place(new Vector3(4, 1, -3.5f), 90f);
		for (int i = 0; i < 5; i++) bots[i].GlobalPosition = spots[i];
		b.Check(!mine.IsDead && b.Form.IsWolf, "o lobo está vivo e de lobo");
		await b.Tap("pounce");
		await b.Frames(3);
		b.Check(b.Player.LockTarget == bots[0], "travou no principal");
		await b.Tap("attack");
		b.Check(await b.Until(() => w.IsPounceFlying, 1.0), "o clique salta");
		await b.Until(() => !w.IsPounceFlying, 3.0);
		await b.Seconds(1.0);

		Vector3 landed = b.Player.GlobalPosition;
		float attack = b.Form.Breed.AttackDamage;
		float main = attack * w.HeavyDamageBonus * w.ClawVsHumanBonus;
		RegressionBench.Note($"lobo com garra {attack:0}: principal esperado {main:0} (× {w.HeavyDamageBonus} × {w.ClawVsHumanBonus} contra humano); área {RegressionBench.F(w.PounceAreaRadius)} m, {w.PounceSplash:0.##} até {RegressionBench.F(w.PounceSplashInner)} m, {w.PounceSplashFar:0.##} na borda");
		for (int i = 0; i < 5; i++)
			RegressionBench.Note($"{names[i]}: a {RegressionBench.F(bots[i].GlobalPosition.DistanceTo(landed))} m de onde o lobo caiu, levou {taken[i]:0} ({taken[i] / main * 100:0}% do principal)");

		b.Near(main, taken[0], 1.0, "o principal leva o dano cheio");
		b.Check(main >= 200f, $"o principal humano morre de vida cheia ({main:0} de 200)");
		float near = taken[1] / main, edge = taken[2] / main;
		b.AtLeast(0.45, near, "quem está perto leva boa parte (fração do principal)");
		b.Check(edge > 0f && edge < near, $"na borda leva menos que perto ({edge:0.00} < {near:0.00})");
		b.Near(0, taken[3], 0.01, "atrás da parede, nada");
		b.Near(0, taken[4], 0.01, "fora da área, nada");
		wall.QueueFree();
	}
}
