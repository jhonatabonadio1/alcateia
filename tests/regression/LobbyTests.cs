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
/// o jogo"): o conjunto padrão com uma arma comum e a granada, outro
/// conjunto com uma lendária, a classe de lobo — salvos e lidos de novo.
/// E o padrão só leva arma comum (19/11): o AR-15 que estiver nele vira a
/// M4A4 ao abrir.
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
			Profile.Sets[0].Primary = "m4a4";
			Profile.Sets[0].Utilities.Clear();
			Profile.Sets[0].Utilities.Add(Arsenal.GrenadeId);
			Profile.Sets[1].Primary = "ar15_ouro";
			Profile.Sets[1].Name = "Lendário";
			Profile.LastUsed = 1;
			Profile.Save();

			Profile.Load();
			b.Check(Profile.Sets[0].Primary == "m4a4", $"o padrão guarda a M4A4, arma comum ({Profile.Sets[0].Primary})");
			b.Check(Profile.Sets[0].Utilities.Contains(Arsenal.GrenadeId), "o padrão guarda a granada");
			b.Check(Profile.Sets[1].Primary == "ar15_ouro" && Profile.Sets[1].Name == "Lendário", $"o conjunto 2 guarda a lendária e o nome ({Profile.Sets[1].Primary}, {Profile.Sets[1].Name})");
			b.Check(Profile.LastUsed == 1, "e o último conjunto usado");

			// Arma que saiu do jogo volta para a padrão, sem quebrar o resto.
			Profile.Sets[2].Primary = "ak47";
			Profile.Save();
			Profile.Load();
			b.Check(Profile.Sets[2].Primary == Arsenal.DefaultPrimary, $"arma que não existe mais vira a M4A4 ({Profile.Sets[2].Primary})");

			// O padrão não aceita o AR-15 nem as lendárias: só arma comum, e de graça.
			b.Check(!LoadoutSet.AllowedInDefault("ar15") && !LoadoutSet.AllowedInDefault("ar15_ultimate"), "o AR-15 e as skins dele não vão no padrão");
			b.Check(LoadoutSet.AllowedInDefault("m4a4") && LoadoutSet.AllowedInDefault("deagle"), "as armas comuns vão no padrão");
			// AK-74 e SVD incomuns, SCAR-H épica: se compram e não vão no padrão (Jhon, 19/11).
			b.Check(!LoadoutSet.AllowedInDefault("ak74") && !LoadoutSet.AllowedInDefault("svd") && !LoadoutSet.AllowedInDefault("scar"), "AK-74, SVD e SCAR-H não vão no padrão");
			b.Check(Catalog.Get("ak74").Prices != null && Catalog.Get("svd").Prices != null && Catalog.Get("scar").Prices != null
				&& !Catalog.Get("ak74").Starter && !Catalog.Get("svd").Starter && !Catalog.Get("scar").Starter, "AK-74, SVD e SCAR-H estão à venda, não vêm com o perfil");
			b.Check(Catalog.Get("scar").Rarity == 3 && Catalog.Get("ak74").Rarity == 1 && Catalog.Get("svd").Rarity == 1, "SCAR-H épica, AK-74 e SVD incomuns");
			Profile.Sets[0].Primary = "ar15";
			Profile.Sets[1].Primary = "ar15";
			Profile.Save();
			Profile.Load();
			b.Check(Profile.Sets[0].Primary == Arsenal.DefaultPrimary, $"o AR-15 no padrão vira a M4A4 ({Profile.Sets[0].Primary})");
			b.Check(Profile.Sets[1].Primary == "ar15", $"nos outros conjuntos o AR-15 fica ({Profile.Sets[1].Primary})");
			Profile.Sets[0].Primary = "m4a4";
			b.Check(Profile.Sets[0].TotalCost() == 0, "o padrão com qualquer arma comum não custa WP");

			// A granada vem no padrão por padrão (19/11); quem a tira fica sem — ela não volta sozinha a cada abertura.
			b.Check(LoadoutSet.AllowedInDefault(Arsenal.GrenadeId) && Catalog.Get(Arsenal.GrenadeId).Starter, "a granada é de todo mundo e vai no padrão");
			Profile.Sets[0].Utilities.Clear();
			Profile.Save();
			Profile.Load();
			b.Check(Profile.Sets[0].GrenadeCount() == 0, "tirada do padrão, a granada não volta sozinha");
			System.IO.File.Delete(path);
			Profile.Load();
			b.Check(Profile.Sets[0].GrenadeCount() == 1, "perfil novo: a granada já vem no conjunto padrão");
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
