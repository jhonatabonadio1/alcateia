using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O crash do fim de partida (Jhon, 25/11: "deu um crash ao clicar em
/// 'Voltar à sala' após o fim da partida, em prod", e o do "jogar
/// novamente" de manhã). Morto, o jogador assiste um aliado em primeira
/// pessoa, e a câmera de morte esconde o corpo dele trocando a camada de
/// desenho. No Godot 4.7 (Forward+) a malha que perde a camada da luz dos
/// personagens (máscara restrita) continuava presa a ela; na troca de cena a
/// luz some, o Godot avisa "BUG, indexing did not unpair geometries from
/// light" e cai lendo a luz apagada (sinal 11). Toda troca de camada passa
/// agora por <see cref="RenderLayers.Set"/>.
///
/// Sem tela (<c>--headless</c>, como o testar.sh) o desenho é de mentira e
/// o aviso não sai: o teste percorre o caminho e reprova qualquer erro de
/// script. Com tela (o mesmo comando sem <c>--headless</c>) ele também pega
/// o aviso do renderizador e o crash; foi assim que ele foi conferido.
/// </summary>
public sealed class BackToRoomTest : RegressionTest
{
	public override string Name => "VoltarASalaSemCrash";
	public override string Description => "três partidas seguidas com bots, morto assistindo um aliado em primeira pessoa no fim: \"Voltar à sala\" (duas vezes) e \"Sair para o lobby\" trocam de cena sem erro";
	public override string Scene => "res://src/scenes/menu/menu.tscn";

	private const string Menu = "res://src/scenes/menu/menu.tscn";

	public override void Prepare()
	{
		// Uma sala offline, como a que o jogador cria no lobby: é para ela que
		// o "Voltar à sala" volta (a tela da sala, com a vitrine 3D).
		_room = RoomDirectory.Create(new RoomSettings { Map = "cidade", TeamSize = 4 });
		RoomDirectory.Current = _room;
		Arm();
	}

	private RoomListing _room;

	/// <summary>O que a tela da sala põe antes de abrir a partida (RoomScreen.Launch).</summary>
	private void Arm()
	{
		RoomConfig.Active = true;
		RoomConfig.Settings = _room.Settings.Clone();
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Teste", Team = Teams.Blue, IsHost = true });
		foreach (var (name, team) in RoomConfig.DefaultBots())
			RoomConfig.Members.Add(new RoomMember { Name = name, Team = team, IsBot = true });
		RoomConfig.ReturnTo = ReturnTarget.Room;
		_room.State = RoomState.Playing;
	}

	public override async Task Run(RegressionBench b)
	{
		SceneTree tree = b.GetTree();
		// Como no jogo: a partida é a cena atual (a câmera de morte nasce
		// nela, e a troca de cena a apaga junto). A bancada fica de fora.
		var before = new Node { Name = "Antes" };
		tree.Root.AddChild(before);
		tree.CurrentScene = before;
		b.Scene.QueueFree();
		// Sem tela, a carga em segundo plano da tela de carregamento não lê a
		// cena da partida do zero (falha no recurso C# do ruído do bot); no jogo
		// ela lê. Carregadas aqui antes, ela as pega prontas.
		GD.Load<PackedScene>(LoadingScreen.MatchScene);
		GD.Load<PackedScene>(Maps.Get(_room.Settings.Map).Scene);

		string[] exits = { "Voltar à sala", "Voltar à sala", "Sair para o lobby" };
		for (int round = 0; round < exits.Length; round++)
		{
			Arm();
			LoadingScreen.Open(tree);
			bool live = await b.Until(() => InTree(Match.Current) && Match.Current.Phase == MatchPhase.Live && !LoadingScreen.Busy, 60);
			b.Check(live, $"partida {round + 1}: começou");
			if (!live) return;
			await b.Seconds(2.0);

			// Morto, assistindo um aliado em primeira pessoa.
			Player player = tree.GetNodesInGroup("player").OfType<Player>().First();
			Health me = player.GetNode<Health>("Health");
			me.RespawnDelay = 60f;
			await b.Until(() => !me.IsProtected, 5.0);
			me.TakeDamage(99999f);
			bool watching = await b.Until(() => InTree(KillCam.Current) && InTree(KillCam.Current.Watching), 4.0);
			Node3D mate = watching ? KillCam.Current.Watching : null;
			bool hidden = mate != null && await b.Until(() => mate.FindChildren("*", nameof(VisualInstance3D), true, false)
				.OfType<VisualInstance3D>().Any(v => v is not Light3D && v.Layers == KillCam.WatchedLayer), 2.0);
			RegressionBench.Note($"partida {round + 1}: morto, câmera de morte '{(InTree(KillCam.Current) ? KillCam.Current.Mode : "nenhuma")}', assistindo {mate?.Name}");
			b.Check(watching && hidden, $"partida {round + 1}: assistindo um aliado em primeira pessoa, com o corpo dele na camada escondida");
			await b.Seconds(2.0);

			// O fim por placar, como no jogo.
			Match match = Match.Current;
			match.ScoreToWin = match.ScoreOf(me.Team) + 1;
			KillFeed.Report(new Kill(me.PublicName, me.Team, "Alguém", me.Team == Teams.Blue ? Teams.Orange : Teams.Blue, KillCause.Gun, "ar15"));
			ResultScreen screen = null;
			await b.Until(() => (screen = tree.Root.FindChildren("*", "", true, false).OfType<ResultScreen>().FirstOrDefault()) != null, 3.0);
			Button button = screen?.FindChildren("*", nameof(Button), true, false).OfType<Button>().FirstOrDefault(x => x.Text == exits[round]);
			b.Check(button is { Disabled: false }, $"partida {round + 1}: o resultado tem o botão '{exits[round]}', ativo");
			if (button == null) return;
			await b.Until(() => button.IsVisibleInTree(), 8.0);

			// O clique.
			button.EmitSignal(BaseButton.SignalName.Pressed);
			bool menu = await b.Until(() => tree.CurrentScene?.SceneFilePath == Menu && !InTree(Match.Current), 10.0);
			await b.Seconds(1.0);
			b.Check(menu, $"partida {round + 1}: '{exits[round]}' leva ao lobby");
			b.Near(1.0, Engine.TimeScale, 0.001, $"partida {round + 1}: o tempo volta ao normal fora da partida");
		}
	}

	/// <summary>O nó existe e está na árvore (os estáticos de uma partida
	/// apagada ainda apontam para ela).</summary>
	private static bool InTree(Node node) => node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}

/// <summary>
/// O <see cref="RenderLayers.Set"/> desfaz os pares com as luzes antes de
/// tirar uma camada: esconde a malha, troca, mostra de novo. Tirar camada
/// de uma malha à vista esconde e mostra (dois avisos de visibilidade); pôr
/// camada a mais, ou trocar a de uma malha escondida, não mexe na
/// visibilidade. E a malha termina à vista, na camada nova.
/// </summary>
public sealed class RenderLayersTest : RegressionTest
{
	public override string Name => "CamadaSemLuzPresa";
	public override string Description => "trocar a camada de desenho tira a malha das luzes antes (esconde e mostra) quando ela perde camada; pôr camada a mais não pisca";

	public override async Task Run(RegressionBench b)
	{
		var light = new OmniLight3D { LightCullMask = ViewModel.CharacterLayer, OmniRange = 8f, ShadowEnabled = false };
		b.Scene.AddChild(light);
		light.GlobalPosition = b.Player.GlobalPosition + new Vector3(0, 1.5f, -3f);
		var mesh = new MeshInstance3D { Mesh = new BoxMesh(), Layers = 1u };
		b.Scene.AddChild(mesh);
		mesh.GlobalPosition = light.GlobalPosition + Vector3.Right;
		int toggles = 0;
		mesh.VisibilityChanged += () => toggles++;
		await b.Frames(5);

		RenderLayers.Set(mesh, 1u | ViewModel.CharacterLayer);
		b.Check(toggles == 0 && mesh.Layers == (1u | ViewModel.CharacterLayer), $"pôr a camada dos personagens não pisca ({toggles} trocas de visibilidade)");
		await b.Frames(5);

		RenderLayers.Set(mesh, KillCam.WatchedLayer);
		b.Check(toggles == 2 && mesh.Visible && mesh.Layers == KillCam.WatchedLayer, $"tirar a camada esconde e mostra a malha no mesmo quadro ({toggles} trocas), e ela termina à vista na camada nova");

		mesh.Visible = false;
		toggles = 0;
		RenderLayers.Set(mesh, 1u);
		b.Check(toggles == 0 && !mesh.Visible && mesh.Layers == 1u, "escondida, a troca não a mostra");
		mesh.Visible = true;

		// Com tela, apagar a luz e mexer a malha era o "BUG, indexing did not
		// unpair geometries from light" e o sinal 11.
		light.QueueFree();
		await b.Frames(3);
		for (int i = 0; i < 10; i++)
		{
			mesh.GlobalPosition += new Vector3(0.1f, 0, 0);
			await b.Frames(1);
		}
		mesh.QueueFree();
		await b.Frames(2);
	}
}
