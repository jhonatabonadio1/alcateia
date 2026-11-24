using Godot;
using System.Threading.Tasks;

/// <summary>
/// O tiro atravessa o time (19/11, Jhon: "a bala deve atravessar o meu
/// time, pq to atirando no inimigo com alguém do meu time na frente"). Com
/// o fogo amigo desligado, o raio parava no aliado da frente: não feria
/// ninguém. Agora passa por ele e acerta quem está atrás.
/// </summary>
public sealed class AllyPassTest : RegressionTest
{
	public override string Name => "TiroAtravessaAliado";
	public override string Description => "com o fogo amigo desligado, o tiro passa pelo aliado da frente e acerta o inimigo atrás dele";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(2);
		await b.Seconds(0.3);
		var ally = b.Bots()[0];
		var enemy = b.Bots()[1];
		Health me = b.Health;
		Health allyHealth = ally.GetNode<Health>("Health");
		Health enemyHealth = enemy.GetNode<Health>("Health");
		allyHealth.Team = me.Team;
		enemyHealth.Team = me.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
		b.Check(!RoomConfig.Rules.FriendlyFire, "fogo amigo desligado (o padrão)");

		foreach (var bot in new[] { ally, enemy })
		{
			RegressionBench.Freeze(bot);
			bot.Rotation = Vector3.Zero;
		}
		Vector3 start = new(-15, 1, 13);
		await b.Place(start, -90f, settle: 0.4);
		// Na mesma linha: eu, o aliado a 4 m, o inimigo a 10 m.
		ally.GlobalPosition = start + new Vector3(4, 0, 0);
		enemy.GlobalPosition = start + new Vector3(10, 0, 0);
		await b.Seconds(enemyHealth.SpawnProtection + 0.3);

		Weapon w = b.Weapon;
		w.Equip(Arsenal.Get("ar15"), Arsenal.Get(Arsenal.Secondaries[0]), 1);
		w.SelectSlot(Slot.Primary);
		await b.Seconds(1.0);   // sacar a arma
		var camera = b.Player.GetNode<Camera3D>("Head/Camera3D");

		// No peito do inimigo, que o aliado tampa por inteiro.
		Vector3 chest = enemy.GlobalPosition + Vector3.Up * 0.2f;
		Vector3 direction = (chest - camera.GlobalPosition).Normalized();
		b.Player.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
		b.Player.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.Asin(direction.Y), 0, 0);
		await b.Frames(2);

		var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, chest);
		query.Exclude = new Godot.Collections.Array<Rid> { b.Player.GetRid() };
		var first = b.Player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		b.Check(first.Count > 0 && first["collider"].Obj == ally, "o aliado está mesmo na frente");

		float allyTaken = 0, enemyTaken = 0;
		allyHealth.Damaged += (amount, _) => allyTaken += amount;
		enemyHealth.Damaged += (amount, _) => enemyTaken += amount;
		RegressionBench.Press("attack");
		await b.Frames(1);
		RegressionBench.Release("attack");
		await b.Frames(3);
		RegressionBench.ReleaseAll();

		RegressionBench.Note($"aliado levou {allyTaken:0}, inimigo levou {enemyTaken:0}");
		b.Near(0, allyTaken, 0.01, "o aliado não leva nada");
		b.AtLeast(1, enemyTaken, "o inimigo atrás dele leva o tiro");

		// Com o fogo amigo ligado, o aliado volta a segurar (e levar) o tiro.
		RoomConfig.Rules.FriendlyFire = true;
		try
		{
			allyTaken = enemyTaken = 0;
			await b.Seconds(0.5);
			// O recuo do primeiro tiro levantou a mira: de volta ao peito.
			b.Player.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
			b.Player.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.Asin(direction.Y), 0, 0);
			await b.Frames(2);
			RegressionBench.Press("attack");
			await b.Frames(1);
			RegressionBench.Release("attack");
			await b.Frames(3);
			RegressionBench.ReleaseAll();
			b.Check(allyTaken > 0 && enemyTaken == 0,
				"fogo amigo ligado: o aliado da frente leva o tiro");
		}
		finally { RoomConfig.Rules.FriendlyFire = false; }
	}
}
