using Godot;
using System.Threading.Tasks;

/// <summary>
/// O golpe do lobo no meio do cenário (18/11, Jhon: "dei o especial, dei
/// lock num inimigo, mas não deu dano algum; isso não pode acontecer").
///
/// A área do golpe era uma consulta de física com teto de contatos (8 na
/// garrada, 48 no salto) e pegava cenário e gente na mesma lista: perto
/// de um contêiner, cheio de peças de colisão, a lista enchia de cenário
/// e o inimigo ficava de fora. Aqui o inimigo fica cercado de 80 peças
/// sólidas dentro da área — a garrada e o especial têm de ferir do mesmo
/// jeito.
/// </summary>
public sealed class ClutterHitTest : RegressionTest
{
	public override string Name => "GolpeNoCenario";
	public override string Description => "garrada e especial ferem o inimigo mesmo cercado de 80 peças de cenário";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var enemy = b.Bots()[0];
		RegressionBench.Freeze(enemy);
		Health enemyHp = enemy.GetNode<Health>("Health");
		await b.BecomeWolf();
		Weapon w = b.Weapon;
		await b.Place(new Vector3(4, 1, -3.5f), 90f);
		enemy.GlobalPosition = b.Player.GlobalPosition + b.Player.GlobalBasis * new Vector3(0, 0, -1.4f);

		// O entulho: caixinhas sólidas no chão, em volta do inimigo, dentro da área dos golpes.
		var clutter = new Node3D { Name = "Entulho" };
		b.Scene.AddChild(clutter);
		void Fill(Vector3 around)
		{
			foreach (Node old in clutter.GetChildren()) old.QueueFree();
			for (int i = 0; i < 80; i++)
			{
				var piece = new StaticBody3D();
				piece.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.1f, 0.1f, 0.1f) } });
				clutter.AddChild(piece);
				float angle = i * 2.399963f, radius = 0.5f + 0.9f * (i % 10) / 10f;
				piece.GlobalPosition = new Vector3(around.X + Mathf.Cos(angle) * radius, around.Y - 0.8f, around.Z + Mathf.Sin(angle) * radius);
			}
		}

		Fill(enemy.GlobalPosition);
		await b.Seconds(1.2);   // a proteção de quem nasceu acaba
		b.Check(!enemyHp.IsProtected, "o alvo já pode levar dano");

		// ---- a garrada
		float before = enemyHp.Hp + enemyHp.Shield;
		await b.Tap("attack");
		await b.Seconds(0.6);
		float clawed = before - (enemyHp.Hp + enemyHp.Shield);
		RegressionBench.Note($"garrada no entulho: tirou {RegressionBench.F(clawed)}");
		b.Check(clawed > 10f || enemyHp.IsDead, "a garrada fere o inimigo cercado de cenário");
		await b.Until(() => !w.IsStriking, 2.0);

		// ---- o especial, travado, a 6 m
		if (enemyHp.IsDead) await b.Until(() => !enemyHp.IsDead, 6.0);
		await b.Place(new Vector3(4, 1, -3.5f), 90f);
		enemy.GlobalPosition = b.Player.GlobalPosition + b.Player.GlobalBasis * new Vector3(0, 0, -6f);
		Fill(enemy.GlobalPosition);
		await b.Until(() => !enemyHp.IsProtected, 5.0);
		await b.Seconds(0.2);
		w.SpecialFree = false;
		w.FillSpecial();
		await b.Tap("pounce");
		await b.Frames(4);
		b.Check(b.Player.LockTarget == enemy, "armado, trava no inimigo");
		before = enemyHp.Hp + enemyHp.Shield;
		await b.Tap("attack");
		b.Check(await b.Until(() => w.IsPounceFlying, 1.0), "o clique salta");
		await b.Until(() => !w.IsRooted, 3.0);
		float pounced = before - (enemyHp.Hp + enemyHp.Shield);
		RegressionBench.Note($"especial no entulho: tirou {RegressionBench.F(pounced)} (morto {enemyHp.IsDead})");
		b.Check(pounced > 50f || enemyHp.IsDead, "o especial fere o alvo travado cercado de cenário");

		clutter.QueueFree();
	}
}
