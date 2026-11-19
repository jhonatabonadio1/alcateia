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

		b.Health.NoteAttacker(killer);
		b.Health.TakeDamage(99999f);
		await b.Frames(3);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null && cam.Mode == "abate" && KillCam.InKillPhase, "morreu: a câmera de abate começa");
		if (cam == null) return;

		await b.Seconds(0.3);
		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "aliado" && !KillCam.InKillPhase, $"Espaço pula a câmera de abate (modo {cam.Mode})");
		b.Check(cam.Watching == mate, "e a câmera passa a seguir o aliado vivo");
		b.CloseLoadout();

		await b.Seconds(0.8);
		Camera3D view = b.GetViewport().GetCamera3D();
		b.AtMost(6.0, view.GlobalPosition.DistanceTo(mate.GlobalPosition), "a câmera fica por cima do aliado (m)");

		Input.ActionPress("spectate_free");
		await b.Frames(2);
		Input.ActionRelease("spectate_free");
		await b.Frames(2);
		b.Check(cam.Mode == "livre", "F solta a câmera livre");
		Vector3 before = view.GlobalPosition;
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		RegressionBench.Release("move_forward");
		b.AtLeast(2.5, view.GlobalPosition.DistanceTo(before), "livre, o W anda pelo mapa (m)");

		Input.ActionPress("spectate_free");
		await b.Frames(2);
		Input.ActionRelease("spectate_free");
		await b.Frames(2);
		b.Check(cam.Mode == "aliado", "F de novo volta aos aliados");

		await b.Seconds(b.Health.RespawnWait + 0.3);
		b.Health.TryRespawn();
		await b.Seconds(0.4);
		b.Check(!b.Health.IsDead && !GodotObject.IsInstanceValid(cam) || cam.IsQueuedForDeletion(), "renasceu: a câmera de espectador some");
	}
}
