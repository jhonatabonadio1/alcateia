using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Cada tela do lobby monta sem erro e com conteúdo — e se redesenha
/// (Refresh) sem erro. Guarda as peças que as telas dividem (UiKit,
/// LobbyScreen.Page/Split). O testar.sh reprova qualquer ERROR no Output.
/// </summary>
public sealed class LobbyScreensTest : RegressionTest
{
	public override string Name => "TelasDoLobby";
	public override string Description => "todas as telas do lobby montam e se redesenham sem erro";
	public override string Scene => "res://scenes/menu/menu.tscn";

	public override async Task Run(RegressionBench b)
	{
		await b.Seconds(1);
		LobbyShell shell = LobbyShell.Instance;
		b.Check(shell != null, "o lobby abriu");
		if (shell == null) return;

		var screens = new (string Name, Action Go)[]
		{
			("entrada", () => shell.Go(new WelcomeScreen())),
			("salas", shell.GoRooms),
			("sala", () => shell.GoRoom(RoomDirectory.Current ?? RoomDirectory.Create(new RoomSettings { Title = "Teste" }))),
			("loja", () => shell.GoShop()),
			("inventário", () => shell.GoInventory()),
			("perfil", shell.GoProfile),
			("missões", shell.GoMissions),
			("opções", shell.GoOptions),
		};

		foreach (var (name, go) in screens)
		{
			go();
			await b.Seconds(0.3);
			LobbyScreen screen = shell.Current;
			int controls = screen?.FindChildren("*", nameof(Control), true, false).Count ?? 0;
			b.Check(controls >= 5, $"{name}: montou ({controls} controles)");

			screen?.Refresh();
			await b.Seconds(0.2);
			b.Check(GodotObject.IsInstanceValid(screen) && screen.GetChildCount() > 0, $"{name}: redesenhou");
		}
	}
}
