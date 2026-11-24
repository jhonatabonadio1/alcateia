using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O jogo abre no login (pedido do Jhon, 20/11): sem entrar sozinho, com
/// as contas salvas para clicar, e as abas de entrar e criar conta. Com um
/// servidor no ar (<c>ALCATEIA_API</c>), faz o caminho inteiro pela tela:
/// cria uma conta, "fecha o jogo", entra clicando na conta salva, erra a
/// senha e esquece o acesso. Sem servidor, confere só a tela.
/// </summary>
public sealed class LoginTest : RegressionTest
{
	public override string Name => "Login";
	public override string Description => "o jogo abre no login; conta salva só entra com clique; entrar, criar conta e esquecer pela tela";
	public override string Scene => "res://src/scenes/menu/menu.tscn";

	public override async Task Run(RegressionBench b)
	{
		string cfg = ProjectSettings.GlobalizePath("user://online.cfg");
		string backup = System.IO.File.Exists(cfg) ? System.IO.File.ReadAllText(cfg) : null;
		try
		{
			await b.Seconds(1);
			LobbyShell shell = LobbyShell.Instance;
			b.Check(shell?.Current is LoginScreen, $"o jogo abre na tela de login ({shell?.Current?.GetType().Name})");
			if (shell == null) return;
			b.Check(!Backend.LoggedIn, "e sem entrar sozinho");
			b.Check(Buttons(shell).Any(x => x.Text == "Entrar") && TabRow(shell)?.GetChildCount() == 2, "com as abas Entrar e Criar conta");

			if (!(await Backend.Health()).Ok)
			{
				GD.Print("Login: sem servidor no ar (ALCATEIA_API); o caminho pela tela fica para o teste de ponta a ponta.");
				return;
			}

			// Cria a conta pela tela.
			string name = "Login" + (Time.GetTicksMsec() % 100000);
			const string password = "senha-do-teste-1";
			TabRow(shell).Select(1);
			await b.Frames(3);
			LineEdit[] fields = Fields(shell);
			b.Check(fields.Length == 3, $"criar conta: nome, senha e repetir a senha; o servidor é fixo ({fields.Length} campos)");
			fields[0].Text = name;
			fields[1].Text = password;
			fields[2].Text = "outra-senha-123";
			Click(shell, "Criar conta");
			await b.Frames(3);
			b.Check(shell.Current is LoginScreen && !Backend.LoggedIn, "senhas diferentes: não cria");

			fields = Fields(shell);
			fields[1].Text = password;
			fields[2].Text = password;
			Click(shell, "Criar conta");
			bool created = await RealUntil(b, () => shell.Current is OnlineScreen && Backend.LoggedIn, 5);
			if (!created) GD.Print("Login: na tela: " + string.Join(" | ", Labels(shell).Select(l => l.Text).Where(t => t.Trim().Length > 0)));
			b.Check(created, "criou a conta e entrou nas salas");
			b.Check(Backend.Saved.FirstOrDefault()?.Name == name, "e o acesso ficou salvo");

			// "Fecha o jogo e abre de novo": a conta aparece, mas só entra com o clique.
			Backend.EndSession();
			await b.Frames(3);
			b.Check(shell.Current is LoginScreen, "sem sessão, volta ao login");
			await b.Seconds(0.5);
			b.Check(!Backend.LoggedIn, "e não entra sozinho com o acesso salvo");
			Button account = Buttons(shell).FirstOrDefault(x => x.HasMeta("conta") && (string)x.GetMeta("conta") == name);
			b.Check(account != null, "a conta salva aparece para clicar");
			account?.EmitSignal(BaseButton.SignalName.Pressed);
			b.Check(await RealUntil(b, () => shell.Current is OnlineScreen && Backend.LoggedIn && Backend.AccountName == name, 5), "um clique na conta: entrou");

			// Senha errada: fica no login, com o recado.
			Backend.EndSession();
			await b.Frames(3);
			fields = Fields(shell);
			fields[0].Text = name;
			fields[1].Text = "senha-errada-123";
			Click(shell, "Entrar");
			await RealUntil(b, () => Labels(shell).Any(l => l.Text.Contains("enha") && !l.Text.Contains("Falando")), 5);
			b.Check(shell.Current is LoginScreen && !Backend.LoggedIn, "senha errada: não entra");

			// Esquecer o acesso.
			Button forget = Buttons(shell).FirstOrDefault(x => x.TooltipText.StartsWith("Esquecer"));
			forget?.EmitSignal(BaseButton.SignalName.Pressed);
			await b.Frames(3);
			b.Check(Backend.Saved.All(a => a.Name != name), "esquecer: o acesso sai da lista");
			b.Check(!Buttons(shell).Any(x => x.HasMeta("conta") && (string)x.GetMeta("conta") == name), "e da tela");
		}
		finally
		{
			Backend.EndSession();
			if (backup != null) System.IO.File.WriteAllText(cfg, backup);
		}
	}

	/// <summary>Espera em tempo de verdade: com --fixed-fps o tempo do jogo
	/// corre mais rápido que o relógio, e o servidor responde no relógio.</summary>
	private static async Task<bool> RealUntil(RegressionBench b, Func<bool> condition, double seconds)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		while (watch.Elapsed.TotalSeconds < seconds)
		{
			if (condition()) return true;
			await b.PhysicsFrame();
		}
		return condition();
	}

	private static Tabs TabRow(LobbyShell shell) => shell.Current?.FindChildren("*", "HBoxContainer", true, false).OfType<Tabs>().FirstOrDefault();
	private static LineEdit[] Fields(LobbyShell shell) => shell.Current.FindChildren("*", nameof(LineEdit), true, false).OfType<LineEdit>().ToArray();
	private static Button[] Buttons(LobbyShell shell) => shell.Current?.FindChildren("*", nameof(Button), true, false).OfType<Button>().ToArray() ?? Array.Empty<Button>();
	private static Label[] Labels(LobbyShell shell) => shell.Current?.FindChildren("*", nameof(Label), true, false).OfType<Label>().ToArray() ?? Array.Empty<Label>();

	private static void Click(LobbyShell shell, string text) =>
		Buttons(shell).LastOrDefault(x => x.Text == text && !x.ToggleMode)?.EmitSignal(BaseButton.SignalName.Pressed);
}
