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
			("sala", () => shell.GoRoom(RoomDirectory.Current ?? RoomDirectory.Create(new RoomSettings { Title = "Teste" }))),
			("loja", () => shell.GoShop()),
			("inventário", () => shell.GoInventory()),
			("perfil", shell.GoProfile),
			("missões", shell.GoMissions),
			("opções", shell.GoOptions),
			("online", shell.GoOnline),
			("créditos", shell.GoCredits),
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

/// <summary>
/// O que se muda no inventário fica (19/11: "as mudanças que eu fizer no
/// inventário devem persistir; não estão persistindo quando fecho e abro
/// o jogo"): o conjunto padrão com a AR-15 Ultimate e a granada, outro
/// conjunto com uma lendária, a classe de lobo — salvos e lidos de novo.
/// </summary>
public sealed class ProfilePersistsTest : RegressionTest
{
	public override string Name => "PerfilPersiste";
	public override string Description => "o conjunto padrão e os outros conjuntos ficam como o jogador deixou depois de salvar e abrir de novo";

	public override async Task Run(RegressionBench b)
	{
		string path = ProjectSettings.GlobalizePath("user://perfil.json");
		string backup = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
		try
		{
			foreach (string id in new[] { "ar15_ultimate", "ar15_ouro", Arsenal.GrenadeId })
				if (!Profile.Owns(id)) Profile.Inventory.Add(new OwnedItem { Id = id, AcquiredUtc = System.DateTime.UtcNow });
			Profile.Sets[0].Primary = "ar15_ultimate";
			Profile.Sets[0].Utilities.Clear();
			Profile.Sets[0].Utilities.Add(Arsenal.GrenadeId);
			Profile.Sets[1].Primary = "ar15_ouro";
			Profile.Sets[1].Name = "Lendário";
			Profile.LastUsed = 1;
			Profile.Save();

			Profile.Load();
			b.Check(Profile.Sets[0].Primary == "ar15_ultimate", $"o padrão guarda a AR-15 Ultimate ({Profile.Sets[0].Primary})");
			b.Check(Profile.Sets[0].Utilities.Contains(Arsenal.GrenadeId), "o padrão guarda a granada");
			b.Check(Profile.Sets[1].Primary == "ar15_ouro" && Profile.Sets[1].Name == "Lendário", $"o conjunto 2 guarda a lendária e o nome ({Profile.Sets[1].Primary}, {Profile.Sets[1].Name})");
			b.Check(Profile.LastUsed == 1, "e o último conjunto usado");

			// Arma que saiu do jogo volta para a padrão, sem quebrar o resto.
			Profile.Sets[2].Primary = "ak47";
			Profile.Save();
			Profile.Load();
			b.Check(Profile.Sets[2].Primary == Arsenal.DefaultPrimary, $"arma que não existe mais vira o AR-15 ({Profile.Sets[2].Primary})");
		}
		finally
		{
			if (backup != null) System.IO.File.WriteAllText(path, backup);
			else if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
			Profile.Load();
		}
		await b.Frames(1);
	}
}
