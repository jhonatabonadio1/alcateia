using System;
using System.IO;
using System.Text.Json.Nodes;
using Alcateia.Cliente.Core;
using Alcateia.Cliente.ViewModels;
using Alcateia.Cliente.Views;
using Alcateia.Pacote;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Alcateia.Cliente.Fotos;

/// <summary>
/// Monta cada tela do cliente com dados de mentira e grava um PNG dela.
///
///   fotos PASTA_DE_SAIDA
/// </summary>
public static class Program
{
	public static int Main(string[] args)
	{
		string outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "fotos");
		Directory.CreateDirectory(outDir);
		// Os ajustes de mentira não tocam nos do jogador.
		Environment.SetEnvironmentVariable("WOLFLEAGUE_LAUNCHER_DADOS", Path.Combine(outDir, ".dados"));

		AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();

		var latest = new Manifest
		{
			Product = "wolfleague", Platform = "windows", Version = "0.5.0-alpha.17", Exe = "WolfLeague.exe",
			Notes = "Amigos com tag: cada conta tem um Nome#0000, e é por ele que se adiciona.\n\n" +
				"Grupo: chame amigos para o grupo e entrem juntos nas salas, no mesmo time. Quem escolhe a sala é o líder.\n\n" +
				"Cliente de instalação: baixa o jogo, atualiza sozinho só com o que mudou, repara e desinstala."
		};
		latest.Blobs.Add(new BlobInfo("wolfleague.000.pack", 745_987_875));
		latest.Chunks.Add(new ChunkInfo(new string('a', 64), 1, 0, 0, 1, false));
		latest.Files.Add(new FileEntry("WolfLeague.exe", 1_480_000_000, new string('b', 64), false, Array.Empty<int>()));
		var old = new Manifest { Product = "wolfleague", Platform = "windows", Version = "0.5.0-alpha.16", Exe = "WolfLeague.exe" };
		old.Files.Add(new FileEntry("WolfLeague.exe", 1_470_000_000, new string('c', 64), false, Array.Empty<int>()));

		// --- a entrada
		var settings = new Settings();
		settings.Accounts.Add(new SavedAccount("x", 1, "LoboGuara", "0042", "t"));
		settings.Accounts.Add(new SavedAccount("x", 2, "Presa_Fina", "7310", "t"));
		Shoot(outDir, "1_entrada", new MainViewModel(settings) { PreviewOnly = true }, _ => { });

		// --- o jogo pronto, com amigos
		Shoot(outDir, "2_jogo_pronto", Logged(), vm => vm.GameService.Preview(GameState.Ready, latest, latest));

		// --- baixando uma atualização
		Shoot(outDir, "3_atualizando", Logged(), vm => vm.GameService.Preview(GameState.Working, latest, old, WorkKind.Update,
			new InstallProgress(InstallPhase.Downloading, 620L << 20, 1100L << 20, 41L << 20, 96L << 20, "game/WolfLeague.pck"), 11.5 * (1 << 20)));

		// --- não instalado
		Shoot(outDir, "4_instalar", Logged(friends: false), vm => vm.GameService.Preview(GameState.NotInstalled, latest, null));

		// --- configurações e a pergunta de desinstalar
		Shoot(outDir, "5_configuracoes", Logged(), vm =>
		{
			vm.GameService.Preview(GameState.Ready, latest, latest);
			vm.Page = Page.Settings;
		});
		Shoot(outDir, "6_desinstalar", Logged(), vm =>
		{
			vm.GameService.Preview(GameState.Ready, latest, latest);
			vm.Page = Page.Settings;
			vm.Game.Uninstall.Execute(null);
		});

		// --- ligando a verificação em duas etapas
		Shoot(outDir, "8_duas_etapas", Logged(), vm =>
		{
			vm.GameService.Preview(GameState.Ready, latest, latest);
			vm.Page = Page.Settings;
			vm.OpenTwoFactor();
			vm.TwoFactor.Show("JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP",
				"otpauth://totp/WolfLeague:LoboGuara?secret=JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP&issuer=WolfLeague&digits=6&period=30");
		});

		// --- novidades, com o aviso de versão nova do cliente e um toast
		Shoot(outDir, "7_novidades", Logged(), vm =>
		{
			vm.GameService.Preview(GameState.Ready, latest, latest);
			vm.Game.Tab = GameTab.News;
			vm.Toast(Tr.F("toast.tag_copied", "LoboGuara#0042"));
		});

		// --- a conversa com um amigo
		Shoot(outDir, "10_conversa", Logged(), vm =>
		{
			vm.GameService.Preview(GameState.Ready, latest, latest);
			vm.FriendsLink.PreviewTalk(1, 3, (false, "bora uma partida?"), (true, "bora! cria a sala"), (false, "sala 12, senha lobo"),
				(true, "entrando"), (false, "traz o Garra também"));
			foreach (var f in vm.FriendsLink.Friends)
				if (f.Id == 3) vm.Friends.OpenChat(f);
		});

		// --- os idiomas: a mesma tela em português e em espanhol (o padrão é inglês)
		foreach (string language in new[] { "pt_BR", "es" })
		{
			Tr.Set(language);
			Shoot(outDir, "9_" + language, Logged(), vm => vm.GameService.Preview(GameState.UpdateAvailable, latest, old));
		}
		Tr.Set(Tr.Default);

		Console.WriteLine("fotos em " + outDir);
		return 0;
	}

	private static MainViewModel Logged(bool friends = true)
	{
		// O idioma da foto: o que estiver posto (o MainViewModel põe o dos ajustes).
		var vm = new MainViewModel(new Settings { Language = Tr.Current });
		vm.Preview(new Account(1, "LoboGuara", "0042", 18400, 12, "t"));
		JsonObject list = friends ? Friends() : new JsonObject { ["t"] = "friends", ["friends"] = new JsonArray(), ["requests"] = new JsonArray(), ["sent"] = new JsonArray() };
		// Pelo texto, como chega do servidor (os números vêm sem tipo).
		vm.FriendsLink.Preview((JsonObject)JsonNode.Parse(list.ToJsonString()));
		vm.Friends.Rebuild();
		return vm;
	}

	private static JsonObject Friends()
	{
		static JsonObject P(long id, string name, string tag, string status = null, int? room = null) =>
			new() { ["id"] = id, ["name"] = name, ["tag"] = tag, ["xp"] = 1000, ["photo"] = "", ["status"] = status, ["room"] = room };

		return new JsonObject
		{
			["t"] = "friends",
			["friends"] = new JsonArray(P(2, "Presa_Fina", "7310", "room", 12), P(3, "Uivo", "0007", "playing", 4), P(4, "Garra_Rubra", "5521", "online"),
				P(5, "Sombra", "9034", "app"), P(6, "Fenrir_BR", "1188", "offline"), P(7, "Matilha", "4402", "offline")),
			["requests"] = new JsonArray(P(8, "Cacador99", "3141")),
			["sent"] = new JsonArray(P(9, "Lua_Cheia", "2718"))
		};
	}

	private static void Shoot(string outDir, string name, MainViewModel vm, Action<MainViewModel> prepare)
	{
		var window = new MainWindow { DataContext = vm, Width = 1200, Height = 740 };
		prepare(vm);
		window.Show();
		Dispatcher.UIThread.RunJobs();
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		Dispatcher.UIThread.RunJobs();

		using WriteableBitmap frame = window.CaptureRenderedFrame();
		string path = Path.Combine(outDir, name + ".png");
		frame.Save(path);
		window.Quitting = true;
		window.Close();
		Console.WriteLine(path);
	}
}
