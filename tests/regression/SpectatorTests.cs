using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Morto: 1 s no próprio corpo; depois a câmera de abate — o replay pelo
/// olho de quem matou, em primeira pessoa com a arma dele, o HUD dele e o
/// nome no alto —; Espaço vai para o espectador, direto na primeira pessoa
/// de um aliado; Espaço troca para de cima e para a câmera livre, que não
/// sai do mapa; morto, o rádio não fala; a escolha de conjunto só aparece
/// na hora de renascer.
/// </summary>
public sealed class SpectatorTest : RegressionTest
{
	public override string Name => "Espectador";
	public override string Description => "morto: 1 s no corpo, replay do abate em primeira pessoa (Espaço pula), espectador em primeira pessoa, de cima e livre dentro do mapa";

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		var bots = b.Bots();
		b.Check(bots.Length >= 2, $"há bots no treino ({bots.Length})");
		if (bots.Length < 2) return;
		foreach (var bot in bots) RegressionBench.Freeze(bot);
		// O aliado é um bot humano (o lobo é visto de fora, pela câmera de lobo).
		CharacterBody3D mate = bots.FirstOrDefault(x => !x.GetNode<FormController>("FormController").IsWolf) ?? bots[1];
		CharacterBody3D killer = bots.First(x => x != mate);
		killer.GetNode<Health>("Health").Team = b.Health.Team == Teams.Blue ? Teams.Orange : Teams.Blue;
		mate.GetNode<Health>("Health").Team = b.Health.Team;
		for (int i = 2; i < bots.Length; i++) bots[i].GetNode<Health>("Health").Team = killer.GetNode<Health>("Health").Team;

		// Espera longa para renascer, como na partida (no treino são 3 s).
		b.Health.TrainingRespawnDelay = 14f;

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
		b.Check(cam != null && cam.Mode == "morte", "morreu: primeiro, o próprio corpo");
		if (cam == null) return;
		b.Check(KillCam.Active, "o HUD de quem joga sai (o de quem se vê toma o lugar)");

		await b.Seconds(1.1);
		b.Check(cam.Mode == "abate" && KillCam.InKillPhase, $"1 s depois: a câmera de abate (modo {cam.Mode})");
		b.Check(cam.Replaying && b.Player.Replaying, "com a fita gravada, é o replay");
		b.Check(KillCam.Focus == killer.GetNode<Health>("Health").PublicName, "o feed marca o nome de quem matou");
		b.Check(cam.Copy != null && cam.Copy.ReplayCopy && !cam.Copy.IsInGroup(Groups.Bots), "o replay é uma cópia de quem matou, fora da partida");
		b.Check(cam.Copy != null && cam.Copy.GetNode<Health>("Health").PublicName == killer.GetNode<Health>("Health").PublicName, "a cópia tem o nome dele");
		Hud mine = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().FirstOrDefault(h => h.Subject == null);
		b.Check(mine != null && !mine.Visible, "o HUD de quem joga sai da tela");
		b.Check(cam.SubjectHud != null && cam.SubjectHud.Subject == cam.Copy && cam.SubjectHud.Visible, "na tela, o HUD do jogo ligado a ele");

		await b.Seconds(0.6);
		if (killer.GetNode<FormController>("FormController").ShowsWolf)
		{
			// Quem matou era lobo: a câmera de lobo, atrás da cópia que refaz o caminho.
			b.Check(!cam.FirstPerson, "quem matou era lobo: câmera de lobo");
			Camera3D wolfView = b.GetViewport().GetCamera3D();
			b.AtMost(3.2, wolfView.GlobalPosition.DistanceTo(cam.Copy.GlobalPosition + Vector3.Up * 0.6f), "a câmera de lobo fica atrás da cópia (m)");
		}
		else
			b.Check(cam.FirstPerson && cam.PovWeapon == killer.GetNode<Weapon>("Weapon").HeldId, $"em primeira pessoa, com a arma dele na mão ({cam.PovWeapon})");
		b.AtLeast(0.6, RegressionBench.Flat(b.Player.GlobalPosition - fell), "no replay, o corpo volta para onde estava antes (m)");

		// Morto, o rádio não fala.
		Radio.Reset();
		b.Check(!Radio.Send(b.Player, 0), "morto, o rádio não fala");

		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "primeira" && cam.Watching == mate, $"Espaço: espectador, direto na primeira pessoa do aliado (modo {cam.Mode})");
		b.Check(!b.Player.Replaying, "saiu do replay: o corpo deixa de ser reencenado");
		b.AtMost(0.3, RegressionBench.Flat(b.Player.GlobalPosition - fell), "e volta para onde caiu (m)");
		b.Check(KillCam.Focus == mate.GetNode<Health>("Health").PublicName, "o feed passa a marcar o aliado");
		b.Check(cam.Copy == null, "a cópia do replay sai");
		b.Check(cam.SubjectHud != null && cam.SubjectHud.Subject == mate, "na tela, o HUD do aliado");
		await b.Seconds(0.3);
		Camera3D view = b.GetViewport().GetCamera3D();
		b.AtMost(0.3, view.GlobalPosition.DistanceTo(mate.GlobalPosition + Vector3.Up * mate.GetNode<Node3D>("Head").Position.Y), "a câmera fica no olho do aliado (m)");
		b.Check(cam.FirstPerson && cam.PovWeapon != null, $"com a arma dele na mão ({cam.PovWeapon})");

		await b.Tap("jump");
		await b.Seconds(0.8);
		b.Check(cam.Mode == "cima", "Espaço: vista de cima do aliado");
		view = b.GetViewport().GetCamera3D();
		b.AtMost(6.0, view.GlobalPosition.DistanceTo(mate.GlobalPosition), "a câmera fica por cima do aliado (m)");

		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "livre", "Espaço de novo: câmera livre");
		Vector3 before = view.GlobalPosition;
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		b.AtLeast(2.5, view.GlobalPosition.DistanceTo(before), "livre, o W anda pelo mapa (m)");
		// Segura o W até bater na borda: a câmera não sai do mapa.
		RegressionBench.Press("walk");
		await b.Seconds(8);
		RegressionBench.ReleaseAll();
		Aabb box = MapBounds.Of(b.GetTree());
		b.Check(box.Grow(0.05f).HasPoint(view.GlobalPosition), $"a câmera livre não sai do mapa ({view.GlobalPosition})");

		await b.Tap("jump");
		await b.Frames(3);
		b.Check(cam.Mode == "primeira", "Espaço de novo volta aos aliados, em primeira pessoa");
		var menu = b.GetTree().Root.FindChildren("*", "", true, false).OfType<LoadoutMenu>().FirstOrDefault();
		b.Check(menu != null && !menu.IsOpen, "morto e assistindo, a escolha de conjunto não aparece");

		// Chegou a hora de renascer: aí sim a escolha aparece, e escolher renasce.
		b.Check(await b.Until(() => menu != null && menu.IsOpen, b.Health.RespawnWait + 1.0), "na hora de renascer, a escolha de conjunto abre");
		b.CloseLoadout();
		await b.Seconds(0.5);
		b.Check(!b.Health.IsDead, "escolheu: renasce");
		b.Check(!GodotObject.IsInstanceValid(cam) || cam.IsQueuedForDeletion(), "e a câmera de espectador some");
		b.Check(!KillCam.Active, "e o HUD de quem joga volta");
	}
}
