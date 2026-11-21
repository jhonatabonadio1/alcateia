using Godot;

/// <summary>
/// O que precisa valer antes de qualquer cena: hoje, a semente do sorteio
/// (<see cref="GameRandom"/>). Autoload, para rodar antes do _Ready dos
/// bots e das armas, que pedem o seu fluxo ao nascer.
///
/// <c>-- --seed=N</c> fixa a semente: a partida sorteia igual (a mira dos
/// bots, a dispersão, as rotas), e um defeito que dependeu da sorte volta.
/// Sem isso a semente vem do relógio, e a partida diz qual foi no Output.
///
/// <c>-- --servidor</c> faz deste processo um servidor de partida
/// (<see cref="MatchServer"/>) em vez do jogo; o servidor exportado (o
/// preset "Servidor (Linux)", com a marca <c>dedicated_server</c>) já
/// nasce servidor, sem precisar do argumento; <c>-- --robo=criar</c> ou
/// <c>--robo=entrar</c> põe um robô para jogar uma partida online sozinho
/// (<see cref="OnlineRobot"/>, usado por tools/testar_online.sh).
/// </summary>
public partial class Inicio : Node
{
	public override void _EnterTree()
	{
		// Os textos, antes de qualquer tela montar.
		Loc.Start();

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--seed=") && ulong.TryParse(arg["--seed=".Length..], out ulong seed))
				MatchRandom.Begin(seed);
		}
	}

	public override void _Ready()
	{
		// O nome do jogo na janela. O nome do projeto no Godot continua
		// "alcateia": trocar ele mudaria a pasta onde o perfil é salvo.
		DisplayServer.WindowSetTitle(Credits.GameName);

		// A suavização vale para a janela inteira, desde a primeira cena
		// (também quando a partida é aberta direto do editor).
		Settings.ApplyVideo(GetTree().Root);

		// A qualidade: as sombras agora, e os efeitos de cada mapa quando
		// o ambiente dele entra na cena.
		Settings.ApplyQuality(GetTree());
		GetTree().NodeAdded += node =>
		{
			if (node is WorldEnvironment world) Settings.ApplyQuality(world.Environment);
		};

		string[] args = OS.GetCmdlineUserArgs();
		if (IsServer(args)) Callable.From(BecomeServer).CallDeferred();

		string robot = System.Array.Find(args, a => a.StartsWith("--robo="));
		if (robot != null) AddChild(new OnlineRobot { Name = "Robo", Role = robot["--robo=".Length..] });
	}

	/// <summary>Este processo é um servidor de partida: o exportado para
	/// servidor (docs/SERVIDOR_DEDICADO.md) ou o jogo com <c>-- --servidor</c>.</summary>
	public static bool IsServer(string[] args) =>
		OS.HasFeature("dedicated_server") || System.Array.IndexOf(args, "--servidor") >= 0;

	/// <summary>Troca o menu pelo servidor de partida.</summary>
	private void BecomeServer()
	{
		SceneTree tree = GetTree();
		tree.CurrentScene?.QueueFree();

		var server = new MatchServer { Name = "ServidorDePartida" };
		tree.Root.AddChild(server);
		tree.CurrentScene = server;
	}
}
