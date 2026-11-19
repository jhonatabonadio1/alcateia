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
	public override string Description => "morto: 1 s no corpo e o espectador em primeira pessoa do aliado, de cima e livre dentro do mapa";

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

		// Desde 19/11 sem câmera de abate: passou o segundo no corpo, direto
		// no espectador.
		await b.Seconds(1.1);
		b.Check(cam.Mode == "primeira" && cam.Watching == mate, $"1 s depois: espectador, direto na primeira pessoa do aliado (modo {cam.Mode})");
		Hud mine = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().FirstOrDefault(h => h.Subject == null);
		b.Check(mine != null && !mine.Visible, "o HUD de quem joga sai da tela");
		b.AtMost(0.3, RegressionBench.Flat(b.Player.GlobalPosition - fell), "o corpo fica onde caiu (m)");
		b.Check(KillCam.Focus == mate.GetNode<Health>("Health").PublicName, "o feed marca o aliado");
		b.Check(cam.SubjectHud != null && cam.SubjectHud.Subject == mate && cam.SubjectHud.Visible, "na tela, o HUD do aliado");

		// Morto, o rádio não fala.
		Radio.Reset();
		b.Check(!Radio.Send(b.Player, 0), "morto, o rádio não fala");
		await b.Seconds(0.3);
		Camera3D view = b.GetViewport().GetCamera3D();
		b.AtMost(0.3, view.GlobalPosition.DistanceTo(mate.GlobalPosition + Vector3.Up * mate.GetNode<Node3D>("Head").Position.Y), "a câmera fica no olho do aliado (m)");
		b.Check(cam.FirstPerson && cam.PovWeapon != null, $"com a arma dele na mão ({cam.PovWeapon})");

		// O aliado vira lobo enquanto você o vê em primeira pessoa: na vista
		// de cima, aparece o lobo (e não o humano que estava escondido).
		var mateForm = mate.GetNode<FormController>("FormController");
		bool wasWolf = mateForm.ShowsWolf;
		mateForm.ProcessMode = Node.ProcessModeEnum.Always;
		mateForm.MirrorWp(mateForm.MaxWp);
		mateForm.ToggleForm();
		await b.Seconds(1.6);

		await b.Tap("jump");
		await b.Seconds(0.8);
		b.Check(cam.Mode == "cima", "Espaço: vista de cima do aliado");
		bool nowWolf = mateForm.ShowsWolf;
		var mateHuman = mate.GetNode<Node3D>("Human");
		var mateWolf = mate.GetNode<Node3D>("WolfVisual");
		b.Check(nowWolf != wasWolf, $"o aliado trocou de forma ({(nowWolf ? "lobo" : "humano")})");
		b.Check(mateHuman.Visible == !nowWolf && mateWolf.Visible == nowWolf, "na vista de cima, aparece a forma de agora dele");
		b.Check(mate.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>().All(v => (v.Layers & KillCam.WatchedLayer) == 0),
			"nada do corpo dele fica escondido da câmera");
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

		// Chegou a hora de renascer: aí sim a escolha aparece. Sem escolher,
		// o tempo acaba, vai o padrão e renasce — e o espectador sai
		// (19/11: "o modo espectador deve sair quando eu renascer").
		menu.ChooseTime = 1.5f;
		b.Check(await b.Until(() => menu != null && menu.IsOpen, b.Health.RespawnWait + 1.0), "na hora de renascer, a escolha de conjunto abre");
		b.Check(await b.Until(() => !b.Health.IsDead, 4.0), "não escolheu: o tempo acaba e renasce com o padrão");
		await b.Frames(3);
		b.Check(!KillCam.Active && KillCam.Eyes == null, "renasceu: o espectador sai");
		b.Check(b.GetViewport().GetCamera3D() == b.Player.GetNode<Camera3D>("Head/Camera3D"), $"a câmera volta a ser a sua ({b.GetViewport().GetCamera3D()?.GetPath()})");
		b.Check(mate.GetNodeOrNull("Head/Camera3D/ViewModel") == null, "a arma emprestada sai do olho do aliado");
		b.Check(mate.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>().All(v => (v.Layers & KillCam.WatchedLayer) == 0), "o corpo do aliado volta a aparecer");
		b.Check(!b.Health.IsDead, "escolheu: renasce");
		b.Check(!GodotObject.IsInstanceValid(cam) || cam.IsQueuedForDeletion(), "e a câmera de espectador some");
		b.Check(!KillCam.Active, "e o HUD de quem joga volta");
	}
}

/// <summary>
/// A câmera de abate é só da morte de quem joga (19/11: o "abatido por"
/// aparecia na tela quando qualquer um morria). No servidor da partida
/// os corpos dos outros (<see cref="Player.Driven"/>) também são "desta
/// máquina", e a morte deles abria a câmera na tela de quem hospeda.
/// </summary>
public sealed class OnlyMyKillCamTest : RegressionTest
{
	public override string Name => "AbateSoMeu";
	public override string Description => "a morte de outro corpo (bot ou jogador movido pela rede) não abre a câmera de abate";

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		var bots = b.Bots();
		bots[0].GetNode<Health>("Health").NoteAttacker(b.Player);
		bots[0].GetNode<Health>("Health").TakeDamage(99999f);
		await b.Seconds(1.3);
		b.Check(!KillCam.Active, "um bot morreu: nada de câmera de abate");

		var body = GD.Load<PackedScene>("res://scenes/player.tscn").Instantiate<Player>();
		body.Driven = true;
		body.GetNodeOrNull("MultiplayerSynchronizer")?.Free();
		body.RemoveFromGroup("player");
		body.GetNode<Health>("Health").Team = Teams.Orange;
		b.Scene.AddChild(body);
		body.GlobalPosition = new Vector3(0, -200, 0);
		await b.Frames(3);
		int helpers = b.GetTree().Root.FindChildren("*", nameof(TeamOutlines), true, false).Count;
		b.Check(helpers <= 1, $"o corpo da rede não monta a tela de quem joga ({helpers} bordas)");
		body.GetNode<Health>("Health").NoteAttacker(b.Player);
		body.GetNode<Health>("Health").TakeDamage(99999f);
		await b.Seconds(1.3);
		b.Check(!KillCam.Active, "um corpo da rede morreu: nada de câmera de abate");
		b.Check(!b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().Any(), "e nenhuma câmera de abate na cena");
		body.QueueFree();
	}
}

/// <summary>
/// A tela do aliado no espectador é a tela dele, viva (19/11: "deve
/// diminuir as balas, a vida, tudo que acontece na minha câmera"): ele
/// atira, o pente desce; apanha, a vida desce; recarrega, aparece
/// "RECARREGANDO" e o pente enche.
/// </summary>
public sealed class WatchedHudLiveTest : RegressionTest
{
	public override string Name => "TelaDoAliado";
	public override string Description => "no espectador, o HUD do aliado acompanha: tiro desce o pente, dano desce a vida, recarga enche";

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		var bots = b.Bots();
		foreach (var bot in bots) RegressionBench.Freeze(bot);
		CharacterBody3D mate = bots.FirstOrDefault(x => !x.GetNode<FormController>("FormController").IsWolf) ?? bots[0];
		mate.GetNode<Health>("Health").Team = b.Health.Team;
		foreach (var bot in bots) if (bot != mate) bot.GetNode<Health>("Health").Team = b.Health.Team == Teams.Orange ? Teams.Blue : Teams.Orange;
		b.Health.TrainingRespawnDelay = 20f;
		// Um abate de antes de morrer: o feed dele continua no espectador.
		Health other = bots.First(x => x != mate).GetNode<Health>("Health");
		KillFeed.Report(new Kill(mate.GetNode<Health>("Health").PublicName, b.Health.Team, other.PublicName, other.Team, KillCause.Gun, "ar15"));
		b.Health.TakeDamage(99999f);
		await b.Seconds(0.3);
		await b.Tap("jump");
		await b.Frames(4);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null && cam.Mode == "primeira" && cam.Watching == mate, $"espectador na tela do aliado ({cam?.Mode})");
		Hud hud = cam?.SubjectHud;
		if (hud == null) { b.Check(false, "há o HUD do aliado"); return; }
		b.Check(hud.FeedShown >= 1, $"o feed continua no espectador ({hud.FeedShown} linhas)");

		Weapon gun = mate.GetNode<Weapon>("Weapon");
		gun.ProcessMode = Node.ProcessModeEnum.Always;
		string mag0 = hud.ShownMag;
		for (int i = 0; i < 3; i++) { gun.TryAttack(); await b.Seconds(0.25); }
		b.Check(hud.ShownMag != mag0 && int.Parse(hud.ShownMag) < int.Parse(mag0), $"ele atirou: o pente desce ({mag0} → {hud.ShownMag})");

		string hp0 = hud.ShownHp;
		mate.GetNode<Health>("Health").TakeDamage(35f);
		await b.Frames(2);
		b.Check(int.Parse(hud.ShownHp) < int.Parse(hp0), $"ele apanhou: a vida desce ({hp0} → {hud.ShownHp})");

		gun.TryReload();
		await b.Frames(2);
		b.Check(hud.ShownReload == "RECARREGANDO", $"ele recarrega: aparece ({hud.ShownReload})");
		await b.Seconds(3.5);
		b.Check(hud.ShownMag == gun.MagSize.ToString(), $"e o pente enche ({hud.ShownMag})");
	}
}

/// <summary>
/// No Armazém, a câmera livre do espectador fica dentro do galpão (19/11:
/// "não posso sair pelas paredes, chão e teto"): voando para cima, para os
/// lados e para baixo, ela para no cenário.
/// </summary>
public sealed class SpectatorInsideTest : RegressionTest
{
	public override string Scene => RegressionBench.Match;
	public override string Name => "EspectadorNoArmazem";
	public override string Description => "a câmera livre do espectador não atravessa parede, chão nem teto do Armazém";

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.IsLive, 15.0);
		b.CloseLoadout();
		await b.Seconds(0.5);
		b.Health.TrainingRespawnDelay = 60f;
		b.Health.TakeDamage(99999f);
		await b.Seconds(0.3);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null, "morreu: há câmera");
		if (cam == null) return;
		for (int i = 0; i < 4 && cam.Mode != "livre"; i++) { await b.Tap("jump"); await b.Frames(3); }
		b.Check(cam.Mode == "livre", $"chegou na câmera livre ({cam.Mode})");
		Vector3 inside = b.Player.GlobalPosition + Vector3.Up * 1.2f;

		var space = b.Player.GetWorld3D().DirectSpaceState;
		Godot.Collections.Array<Rid> skip = new();
		foreach (string group in Groups.Fighters)
			foreach (Node node in b.GetTree().GetNodesInGroup(group))
				if (node is CollisionObject3D body) skip.Add(body.GetRid());

		foreach (var (name, yaw, pitch) in new[] { ("teto", 0f, 89f), ("parede", 0f, 0f), ("parede do lado", 90f, 0f), ("chão", 30f, -89f) })
		{
			cam.AimFree(yaw, pitch);
			RegressionBench.Press("move_forward");
			RegressionBench.Press("walk");
			await b.Seconds(6);
			RegressionBench.ReleaseAll();
			await b.Frames(2);
			Vector3 at = b.GetViewport().GetCamera3D().GlobalPosition;
			// Do ponto de dentro até a câmera não pode haver casca no meio.
			var q = PhysicsRayQueryParameters3D.Create(inside, at, 1);
			q.Exclude = skip;
			var hit = space.IntersectRay(q);
			string wall = hit.Count > 0 ? ((Node)hit["collider"]).Name : "";
			b.Check(!wall.StartsWith("casca") && at.Y > -0.1f, $"{name}: a câmera fica dentro do galpão ({at}, no caminho: {(wall == "" ? "nada" : wall)})");
		}
	}
}
