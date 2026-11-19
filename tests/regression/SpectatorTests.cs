using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Morto: a câmera de abate (a visão de quem matou) passa e pode ser pulada
/// com Espaço; depois a câmera segue um aliado vivo, Espaço troca de aliado,
/// F solta a câmera livre e volta; renasceu, tudo se desfaz. E a bala de
/// raspão tem os seus sons.
/// </summary>
public sealed class SpectatorTest : RegressionTest
{
	public override string Name => "Espectador";
	public override string Description => "morto: câmera de abate que Espaço pula, depois assiste os aliados (Espaço troca, F câmera livre) até renascer";

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		var bots = b.Bots();
		b.Check(bots.Length >= 2, $"há bots no treino ({bots.Length})");
		if (bots.Length < 2) return;
		foreach (var bot in bots) RegressionBench.Freeze(bot);
		CharacterBody3D killer = bots[0], mate = bots[1];
		killer.GetNode<Health>("Health").Team = b.Health.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
		mate.GetNode<Health>("Health").Team = b.Health.Team;
		for (int i = 2; i < bots.Length; i++) bots[i].GetNode<Health>("Health").Team = killer.GetNode<Health>("Health").Team;
		b.Check(Sfx.Has("bala_raspao"), "o som da bala de raspão existe");

		// Espera longa para renascer, como na partida (no treino são 3 s).
		b.Health.TrainingRespawnDelay = 12f;

		// Anda um pouco antes de morrer, para a fita ter o que mostrar.
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.3);
		RegressionBench.Press("move_forward");
		await b.Seconds(1.6);
		RegressionBench.Release("move_forward");
		await b.Seconds(0.2);
		Vector3 fell = b.Player.GlobalPosition;

		b.Health.NoteAttacker(killer);
		b.Health.TakeDamage(99999f);
		await b.Frames(3);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null && cam.Mode == "abate" && KillCam.InKillPhase, "morreu: a câmera de abate começa");
		if (cam == null) return;
		b.Check(cam.Replaying && b.Player.Replaying, "com a fita gravada, é o replay do abate");

		// No meio do replay o corpo está de volta ao caminho que fez, e a
		// câmera está no olho de quem matou.
		await b.Seconds(0.9);
		b.AtLeast(1.0, RegressionBench.Flat(b.Player.GlobalPosition - fell), "no replay, o corpo volta para onde estava antes (m)");
		Camera3D replayView = b.GetViewport().GetCamera3D();
		b.AtMost(1.2, replayView.GlobalPosition.DistanceTo(killer.GlobalPosition + Vector3.Up * 0.72f), "a câmera do replay fica no olho do matador (m)");

		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "aliado" && !KillCam.InKillPhase, $"Espaço pula a câmera de abate (modo {cam.Mode})");
		b.Check(cam.Watching == mate, "e a câmera passa a seguir o aliado vivo");
		b.Check(!b.Player.Replaying, "pulou: o corpo deixa de ser reencenado");
		b.AtMost(0.3, RegressionBench.Flat(b.Player.GlobalPosition - fell), "e volta para onde caiu (m)");
		b.CloseLoadout();

		await b.Seconds(0.8);
		Camera3D view = b.GetViewport().GetCamera3D();
		b.AtMost(6.0, view.GlobalPosition.DistanceTo(mate.GlobalPosition), "a câmera fica por cima do aliado (m)");

		// Espaço troca a visão: de cima → primeira pessoa → livre.
		await b.Tap("jump");
		await b.Seconds(0.5);
		b.Check(cam.Mode == "primeira" && cam.Watching == mate, $"Espaço: primeira pessoa do aliado (modo {cam.Mode})");
		b.AtMost(0.6, view.GlobalPosition.DistanceTo(mate.GlobalPosition + Vector3.Up * 0.72f), "a câmera fica no olho do aliado (m)");
		await b.Tap("attack");
		await b.Frames(3);
		b.Check(cam.Watching == mate && cam.Mode == "primeira", "o clique troca de aliado (só há um: fica nele)");
		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "livre", "Espaço de novo: câmera livre");
		Vector3 before = view.GlobalPosition;
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		RegressionBench.Release("move_forward");
		b.AtLeast(2.5, view.GlobalPosition.DistanceTo(before), "livre, o W anda pelo mapa (m)");

		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "aliado", "Espaço de novo volta aos aliados, de cima");
		var menu = b.GetTree().Root.FindChildren("*", "", true, false).OfType<LoadoutMenu>().FirstOrDefault();
		b.Check(menu != null && !menu.IsOpen, "morto e assistindo, a escolha de conjunto não aparece");

		// Chegou a hora de renascer: aí sim a escolha aparece, e escolher renasce.
		b.Check(await b.Until(() => menu != null && menu.IsOpen, b.Health.RespawnWait + 1.0), "na hora de renascer, a escolha de conjunto abre");
		b.CloseLoadout();
		await b.Seconds(0.5);
		b.Check(!b.Health.IsDead, "escolheu: renasce");
		b.Check(!GodotObject.IsInstanceValid(cam) || cam.IsQueuedForDeletion(), "e a câmera de espectador some");
	}
}
