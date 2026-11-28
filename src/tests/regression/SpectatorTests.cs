using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Morto: a câmera fica onde morreu até renascer (Jhon, 27/11: "tire o modo
/// espectador flutuante, se morrer, fica onde morreu"; até então, passado 1 s,
/// ela ia sozinha para a tela de um aliado, e o Espaço trocava para a vista de
/// cima e para a câmera livre). Um clique vai para a primeira pessoa de um
/// aliado, o Espaço volta; morto, o rádio não fala; a escolha de conjunto só
/// aparece na hora de renascer.
/// </summary>
public sealed class SpectatorTest : RegressionTest
{
	public override string Name => "Espectador";
	public override string Description => "morto: a câmera fica onde morreu; um clique vai para a primeira pessoa do aliado e o Espaço volta";

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

		// Desde 27/11 a câmera fica onde morreu: nem o tempo, nem o Espaço, nem
		// o WASD a tiram de lá.
		Camera3D view = b.GetViewport().GetCamera3D();
		Vector3 died = view.GlobalPosition;
		await b.Seconds(1.1);
		await b.Tap("jump");
		RegressionBench.Press("move_forward");
		await b.Seconds(0.5);
		RegressionBench.ReleaseAll();
		view = b.GetViewport().GetCamera3D();
		b.Check(cam.Mode == "morte" && cam.Watching == null, $"1,6 s depois, com Espaço e W: ainda onde morreu (modo {cam.Mode})");
		b.AtMost(0.01, view.GlobalPosition.DistanceTo(died), "a câmera não sai do lugar (m)");
		Hud mine = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().FirstOrDefault(h => h.Subject == null);
		b.Check(mine != null && !mine.Visible, "o HUD de quem joga sai da tela");
		b.AtMost(0.3, RegressionBench.Flat(b.Player.GlobalPosition - fell), "o corpo fica onde caiu (m)");

		// Um clique: a primeira pessoa do aliado.
		await b.Tap("attack");
		await b.Frames(3);
		b.Check(cam.Mode == "primeira" && cam.Watching == mate, $"clique: a primeira pessoa do aliado (modo {cam.Mode})");
		b.Check(KillCam.Focus == mate.GetNode<Health>("Health").PublicName, "o feed marca o aliado");
		b.Check(cam.SubjectHud != null && cam.SubjectHud.Subject == mate && cam.SubjectHud.Visible, "na tela, o HUD do aliado");

		// Morto, o rádio não fala.
		Radio.Reset();
		b.Check(!Radio.Send(b.Player, 0), "morto, o rádio não fala");
		await b.Seconds(0.3);
		view = b.GetViewport().GetCamera3D();
		b.AtMost(0.3, view.GlobalPosition.DistanceTo(mate.GlobalPosition + Vector3.Up * mate.GetNode<Node3D>("Head").Position.Y), "a câmera fica no olho do aliado (m)");
		b.Check(cam.FirstPerson && cam.PovWeapon != null, $"com a arma dele na mão ({cam.PovWeapon})");

		// Espaço: de volta para onde morreu, com o corpo do aliado à mostra de novo.
		await b.Tap("jump");
		await b.Frames(3);
		view = b.GetViewport().GetCamera3D();
		b.Check(cam.Mode == "morte" && cam.Watching == null, $"Espaço: de volta para onde morreu ({cam.Mode})");
		b.AtMost(0.01, view.GlobalPosition.DistanceTo(died), "a câmera volta ao lugar da morte (m)");
		b.Check(mate.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>().All(v => (v.Layers & KillCam.WatchedLayer) == 0),
			"nada do corpo do aliado fica escondido da câmera");
		b.Check(mate.GetNodeOrNull("Head/Camera3D/ViewModel") == null, "a arma emprestada sai do olho do aliado");

		// Para o fim do teste: de novo na tela do aliado, que tem de sair ao renascer.
		await b.Tap("attack");
		await b.Frames(3);
		var menu = b.GetTree().Root.FindChildren("*", "", true, false).OfType<LoadoutMenu>().FirstOrDefault();
		b.Check(menu != null && !menu.IsOpen, "morto e assistindo, a escolha de conjunto não aparece");

		// Chegou a hora de renascer: aí sim a escolha aparece. Sem escolher,
		// o tempo acaba, vai o padrão e renasce — e o espectador sai
		// (19/11: "o modo espectador deve sair quando eu renascer").
		menu.ChooseTime = 1.5f;
		// Na partida a câmera de espectador é a última da cena: anda DEPOIS
		// da escolha de conjunto no mesmo quadro (19/11: ainda espectador
		// depois de renascer, "mistura a câmera de espectador com a minha").
		cam.ProcessPriority = 1000;
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
		await b.Seconds(0.5);
		b.Check(!b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().Any(h => h.Subject != null), "nenhum HUD de outro fica na tela");
		b.Check(mate.GetNodeOrNull("Head/Camera3D/ViewModel") == null && b.GetViewport().GetCamera3D() == b.Player.GetNode<Camera3D>("Head/Camera3D"),
			"meio segundo depois, a câmera ainda é a sua e a arma emprestada não voltou");
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

		var body = GD.Load<PackedScene>("res://src/scenes/player.tscn").Instantiate<Player>();
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
		// (desde 27/11 a câmera fica onde morreu; o clique vai para a tela do aliado)
		await b.Tap("attack");
		await b.Frames(4);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null && cam.Mode == "primeira" && cam.Watching == mate, $"espectador na tela do aliado ({cam?.Mode})");
		Hud hud = cam?.SubjectHud;
		if (hud == null) { b.Check(false, "há o HUD do aliado"); return; }
		Hud mine = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().FirstOrDefault(h => h.Subject == null);
		b.Check(mine != null && mine.FeedOnScreen && mine.FeedShown >= 1, $"o feed continua na tela no espectador ({mine?.FeedShown} linhas)");
		b.Check(!hud.FeedOnScreen, "e só um: o HUD do aliado não repete o feed");



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
/// No Armazém, morto, a câmera fica onde morreu, dentro do galpão. Até 27/11
/// este teste voava a câmera livre contra o teto, as paredes e o chão (19/11:
/// "não posso sair pelas paredes, chão e teto"); a câmera livre saiu (Jhon,
/// 27/11: "se morrer, fica onde morreu. Com isso, usuários não irão ver para
/// fora do mapa"), e o que se confere agora é que nada a tira do lugar.
/// </summary>
public sealed class SpectatorInsideTest : RegressionTest
{
	public override string Scene => RegressionBench.Match;
	public override string Name => "EspectadorNoArmazem";
	public override string Description => "morto no Armazém, a câmera fica onde morreu, dentro do galpão: o tempo, o Espaço, o WASD e o mouse não a tiram de lá";

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.IsLive, 15.0);
		b.CloseLoadout();
		await b.Seconds(0.5);
		b.Health.TrainingRespawnDelay = 60f;
		b.Health.TakeDamage(99999f);
		await b.Frames(3);
		KillCam cam = b.GetTree().Root.FindChildren("*", "", true, false).OfType<KillCam>().FirstOrDefault();
		b.Check(cam != null, "morreu: há câmera");
		if (cam == null) return;
		Vector3 died = b.GetViewport().GetCamera3D().GlobalPosition;
		Aabb box = MapBounds.Of(b.GetTree());

		for (int i = 0; i < 4; i++)
		{
			await b.Tap("jump");
			Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(400, -300) });
			RegressionBench.Press("move_forward");
			RegressionBench.Press("sprint");
			await b.Seconds(1.5);
			RegressionBench.ReleaseAll();
		}
		await b.Frames(2);
		Camera3D view = b.GetViewport().GetCamera3D();
		b.Check(cam.Mode == "morte", $"continua onde morreu ({cam.Mode})");
		b.AtMost(0.01, view.GlobalPosition.DistanceTo(died), "a câmera não sai do lugar (m)");
		b.Check(box.HasPoint(view.GlobalPosition), $"e fica dentro do mapa ({view.GlobalPosition})");
	}
}

/// <summary>
/// Morto, o feed de abates continua na tela o tempo todo (pedido do Jhon,
/// 21/11): na câmera de morte, no espectador e com a escolha de conjunto
/// aberta por cima — e abate novo aparece.
/// </summary>
public sealed class DeadFeedTest : RegressionTest
{
	public override string Name => "FeedMorto";
	public override string Description => "morto, com a escolha de conjunto aberta, o feed de abates continua na tela e recebe abate novo";

	public override async Task Run(RegressionBench b)
	{
		b.CloseLoadout();
		var bots = b.Bots();
		foreach (var bot in bots) RegressionBench.Freeze(bot);
		Health a = bots[0].GetNode<Health>("Health"), c = bots[^1].GetNode<Health>("Health");
		b.Health.TrainingRespawnDelay = 3f;
		b.Health.TakeDamage(99999f);

		Hud mine = b.GetTree().Root.FindChildren("*", "", true, false).OfType<Hud>().FirstOrDefault(h => h.Subject == null);
		var loadout = b.GetTree().Root.FindChildren("*", "", true, false).OfType<LoadoutMenu>().FirstOrDefault();
		if (mine == null || loadout == null) { b.Check(false, "há o HUD e a escolha de conjunto"); return; }

		await b.Seconds(1.0);
		int before = mine.FeedShown;
		KillFeed.Report(new Kill(a.PublicName, a.Team, c.PublicName, c.Team, KillCause.Gun, "ar15"));
		await b.Frames(3);
		b.Check(mine.FeedOnScreen && mine.FeedShown > before, $"na câmera de morte, o abate novo aparece ({before} → {mine.FeedShown})");

		b.Check(await b.Until(() => loadout.IsOpen, 8), "a escolha de conjunto abriu");
		before = mine.FeedShown;
		KillFeed.Report(new Kill(c.PublicName, c.Team, a.PublicName, a.Team, KillCause.Headshot, "ar15"));
		await b.Frames(3);
		b.Check(mine.FeedOnScreen && mine.FeedShown > before, $"com a escolha aberta por cima, o feed continua e recebe o abate ({before} → {mine.FeedShown})");
		b.CloseLoadout();
	}
}
