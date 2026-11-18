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

/// <summary>Bots jogam granada (18/11): não jogavam nenhuma — a cena do
/// bot nem tinha a granada atribuída.</summary>
public sealed class BotsThrowTest : RegressionTest
{
	public override string Name => "BotsJogamGranada";
	public override string Description => "numa partida, os bots arremessam granada e ela explode sem erro";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		int thrown = 0;
		await b.Until(() => b.Bots().Length >= 4, 5.0);
		foreach (var bot in b.Bots())
			bot.GetNode<Weapon>("Weapon").Thrown += () => thrown++;

		bool any = await b.Until(() => thrown >= 2, 150.0);
		RegressionBench.Note($"{thrown} granadas até {RegressionBench.F(b.Time)} s");
		b.Check(any, "os bots arremessaram granada em até 150 s");

		// Deixa a última explodir: o clarão dela dava erro no Output.
		await b.Seconds(3.0);
	}
}

/// <summary>Bots viram lobo por conta própria e saem da ronda (18/11).
/// Medido antes: 1% do tempo de lobo e de 71 a 81% em ronda.</summary>
public sealed class BotsHuntTest : RegressionTest
{
	public override string Name => "BotsCacam";
	public override string Description => "numa partida, os bots viram lobo sozinhos, andam e não passam a partida em ronda";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => b.Bots().Length >= 4, 5.0);
		await b.Until(() => Match.IsLive, 15.0);
		var bots = b.Bots();
		int wolves = 0, samples = 0, patrol = 0, wolfSamples = 0;
		float walked = 0f;
		var last = bots.ToDictionary(bot => bot, bot => bot.GlobalPosition);
		foreach (var bot in bots)
			bot.GetNode<FormController>("FormController").FormChanged += form => { if (form == (int)Form.Wolf) wolves++; };

		double end = b.Time + 60;
		while (b.Time < end)
		{
			await b.Seconds(0.5);
			foreach (var bot in bots)
			{
				if (bot.GetNode<Health>("Health").IsDead) { last[bot] = bot.GlobalPosition; continue; }
				float step = RegressionBench.Flat(bot.GlobalPosition - last[bot]);
				if (step < 8f) walked += step;
				last[bot] = bot.GlobalPosition;
				samples++;
				if (((Bot)bot).Action == BotAction.Patrol) patrol++;
				if (bot.GetNode<FormController>("FormController").IsWolf) wolfSamples++;
			}
		}

		float perBot = walked / bots.Length;
		RegressionBench.Note($"60 s: {wolves} transformações, de lobo {100 * wolfSamples / Mathf.Max(samples, 1)}% do tempo, em ronda {100 * patrol / Mathf.Max(samples, 1)}%, {RegressionBench.F(perBot)} m por bot");
		b.AtLeast(3, wolves, "transformações em lobo por conta própria em 60 s");
		b.AtLeast(10, 100.0 * wolfSamples / Mathf.Max(samples, 1), "% do tempo de lobo");
		b.AtMost(60, 100.0 * patrol / Mathf.Max(samples, 1), "% do tempo em ronda");
		b.AtLeast(70, perBot, "metros andados por bot em 60 s");
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
