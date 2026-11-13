using Godot;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// O HUD reage aos sinais: a vida cai no número, o tiro tira do pente, e
/// virar lobo troca o painel de munição pelo das recargas. Lê os campos
/// privados do <see cref="Hud"/> por reflexão — o teste olha o que a tela
/// mostra, não como ela é montada.
/// </summary>
public sealed class HudTest : RegressionTest
{
	public override string Name => "Hud";
	public override string Description => "vida, pente e painel de forma acompanham o jogador";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		var huds = b.GetTree().Root.FindChildren("*", nameof(CanvasLayer), true, false).OfType<Hud>().ToList();
		b.Check(huds.Count == 1, $"um HUD na cena ({huds.Count})");
		if (huds.Count != 1) return;
		Hud h = huds[0];

		Label hp = Field<Label>(h, "_hpNumber");
		Label mag = Field<Label>(h, "_mag");
		Control human = Field<Control>(h, "_human");
		Control wolf = Field<Control>(h, "_wolf");

		await b.Seconds(3.5); // proteção de quem acabou de nascer
		b.Check(hp.Text == "200", $"vida cheia no número: {hp.Text}");
		b.Health.TakeDamage(60f);
		await b.PhysicsFrame();
		b.Check(hp.Text == "140", $"vida depois de 60 de dano: {hp.Text}");

		int before = int.Parse(mag.Text);
		await b.Tap("attack");
		await b.Seconds(0.2);
		int after = int.Parse(mag.Text);
		b.Check(after == before - 1, $"um tiro tira um do pente: {before} → {after}");

		b.Check(human.Visible && !wolf.Visible, "humano: painel de munição");
		await b.BecomeWolf();
		b.Check(wolf.Visible && !human.Visible, "lobo: painel das recargas");
	}

	private static T Field<T>(Hud hud, string name) where T : class =>
		typeof(Hud).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(hud) as T;
}

/// <summary>
/// A mira do jogador: o padrão é a de sempre, e as Opções do menu da
/// partida mudam a mira na hora — escolher "Ponto" some com os ajustes das
/// hastes (o ponto tem um tamanho só).
/// </summary>
public sealed class CrosshairOptionsTest : RegressionTest
{
	public override string Name => "MiraDoJogador";
	public override string Description => "Opções → Mira: o padrão é a mira de sempre e o menu da partida muda a mira na hora";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		CrosshairSettings saved = Settings.Values.Crosshair;
		try
		{
			Settings.Values.Crosshair = new CrosshairSettings();
			CrosshairSettings c = Settings.Values.Crosshair;
			b.Check(c.Style == CrosshairSettings.Shape.CrossDot && c.Dynamic && c.Outline, "padrão: cruz com ponto, abrindo ao atirar, com contorno");
			b.Check(c.Length == 8f && c.Thickness == 2f && c.Gap == 5f && c.Color == "ffffff", "padrão: as medidas da mira de sempre");

			var menu = b.GetTree().Root.FindChildren("*", nameof(CanvasLayer), true, false).OfType<GameMenu>().FirstOrDefault();
			b.Check(menu != null, "há o menu da partida");
			if (menu == null) return;

			Input.ParseInputEvent(new InputEventAction { Action = "menu", Pressed = true });
			await b.Frames(3);
			Button options = Find<Button>(menu, x => x.Text == "Opções" && x.IsVisibleInTree());
			b.Check(options != null, "o menu abriu, com Opções");
			options?.EmitSignal(BaseButton.SignalName.Pressed);
			await b.Frames(3);

			b.Check(Find<Crosshair>(menu, x => x.IsVisibleInTree()) != null, "as Opções da partida mostram a prévia da mira");

			// -- --fotos=pasta: guarda a tela das Opções da partida.
			string photos = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fotos="))?["--fotos=".Length..];
			if (photos != null)
			{
				await b.Frames(10);
				System.IO.Directory.CreateDirectory(photos);
				b.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(photos, "opcoes_partida.png"));
			}
			b.Check(Find<Label>(menu, x => x.Text == "Tamanho") != null, "na cruz, dá para mudar o tamanho das hastes");

			OptionButton style = Find<OptionButton>(menu, x => x.ItemCount == 3 && x.GetItemText(2) == "Ponto");
			b.Check(style != null, "há a escolha do estilo");
			style?.Select(2);
			style?.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
			await b.Frames(3);

			b.Check(Settings.Values.Crosshair.Style == CrosshairSettings.Shape.Dot, "escolher Ponto muda a mira");
			b.Check(Find<Label>(menu, x => x.Text == "Tamanho") == null, "e o ponto não tem tamanho para escolher");

			// Arquivo editado à mão com valores fora do menu: volta para dentro.
			var wild = new CrosshairSettings { Length = 400, Thickness = 0, Opacity = 3, Color = "não é cor", Style = (CrosshairSettings.Shape)9 };
			wild.Clamp();
			b.Check(wild.Length == 20 && wild.Thickness == 1 && wild.Opacity == 1 && wild.Color == "ffffff" && wild.Style == CrosshairSettings.Shape.CrossDot,
				"valores fora do menu voltam para dentro");

			Input.ParseInputEvent(new InputEventAction { Action = "menu", Pressed = true });
			await b.Frames(2);
			Input.ParseInputEvent(new InputEventAction { Action = "menu", Pressed = true });
			await b.Frames(2);
		}
		finally
		{
			Settings.Values.Crosshair = saved;
			Settings.Save();
		}
	}

	// Sem filtro de tipo no FindChildren: ele só conhece a classe do Godot,
	// não a do script (a Crosshair é um Control para ele).
	private static T Find<T>(Node root, System.Func<T, bool> test) where T : Node =>
		root.FindChildren("*", "", true, false).OfType<T>().FirstOrDefault(test);
}
