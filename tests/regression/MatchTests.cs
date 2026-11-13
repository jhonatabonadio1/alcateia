using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Bots atiram (v0.26.9): viu o alvo, atira — inclusive indo
/// para a cobertura.</summary>
public sealed class BotsShootTest : RegressionTest
{
	public override string Name => "BotsAtiram";
	public override string Description => "numa partida, os bots trocam tiro";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		int shots = 0;
		await b.Until(() => b.Bots().Length >= 4, 5.0);
		foreach (var bot in b.Bots())
			bot.GetNode<Weapon>("Weapon").Fired += () => shots++;

		await b.Seconds(40);
		RegressionBench.Note($"{shots} tiros em 40 s, {b.Bots().Length} bots");
		b.AtLeast(60, shots, "tiros dos bots em 40 s");
	}
}

/// <summary>Bots lobo usam os especiais (v0.26.9): o salto carregado de
/// longe e o pulo com bote de perto.</summary>
public sealed class WolfBotsTest : RegressionTest
{
	public override string Name => "LobosUsamEspeciais";
	public override string Description => "bots lobo usam o salto carregado numa partida";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		int pounces = 0;
		await b.Until(() => b.Bots().Length >= 4, 5.0);
		foreach (var bot in b.Bots())
			bot.GetNode<Weapon>("Weapon").WolfMoveStarted += move =>
			{
				if (move == (int)WolfMove.Pounce) pounces++;
			};

		double end = b.Time + 90;
		while (b.Time < end)
		{
			foreach (var bot in b.Bots())
			{
				var form = bot.GetNode<FormController>("FormController");
				if (form.Current != Form.Human || bot.GetNode<Health>("Health").IsDead) continue;
				form.AddWp(5000);
				form.ToggleForm();
			}
			await b.Seconds(2);
		}

		RegressionBench.Note($"{pounces} saltos em 90 s");
		b.AtLeast(2, pounces, "saltos carregados dos bots lobo em 90 s");
	}
}

/// <summary>Humano agacha e atira (v0.26.10): o tronco agachado usa a
/// pose de mira de pé, fuzil no ombro.</summary>
public sealed class CrouchAimTest : RegressionTest
{
	public override string Name => "AgachadoMira";
	public override string Description => "o humano agachado fica com o fuzil no ombro e atira";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f);
		RegressionBench.Press("crouch");
		await b.Seconds(0.6);

		var tree = b.Player.GetNode("Human").FindChildren("*", "AnimationTree", true, false).OfType<AnimationTree>().First();
		var playback = (AnimationNodeStateMachinePlayback)tree.Get("parameters/Machine/playback");
		b.Check(playback.GetCurrentNode() == "Crouch", $"estado da animação: {playback.GetCurrentNode()}");
		b.Near(1.0, (float)tree.Get("parameters/Machine/Crouch/AimBlend/blend_amount"), 0.001, "tronco na pose de mira");

		int before = b.Weapon.PrimaryAmmo;
		RegressionBench.Press("attack");
		await b.Seconds(0.5);
		RegressionBench.ReleaseAll();
		b.Check(b.Weapon.PrimaryAmmo < before, "agachado, atirou");
	}
}

/// <summary>As cenas principais carregam e rodam sem erro (o
/// tools/testar.sh reprova qualquer ERROR no Output).</summary>
public abstract class SceneLoadsTest : RegressionTest
{
	public override async Task Run(RegressionBench b)
	{
		await b.Seconds(4);
		b.Check(b.Scene != null && GodotObject.IsInstanceValid(b.Scene), "a cena continua de pé");
	}
}

public sealed class MenuSceneTest : SceneLoadsTest
{
	public override string Name => "CenaMenu";
	public override string Description => "o lobby abre sem erro";
	public override string Scene => "res://scenes/menu/menu.tscn";
}

public sealed class MatchSceneTest : SceneLoadsTest
{
	public override string Name => "CenaPartida";
	public override string Description => "a partida abre e roda sem erro";
	public override string Scene => RegressionBench.Match;
}

public sealed class TrainingSceneTest : SceneLoadsTest
{
	public override string Name => "CenaTreino";
	public override string Description => "o treino abre e roda sem erro";
}
