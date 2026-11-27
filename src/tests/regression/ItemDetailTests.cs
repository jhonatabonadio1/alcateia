using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O painel de detalhe da loja e do inventário cabe na altura da tela
/// (Jhon, 26/11: "a interface tem que evitar scroll ao máximo" — numa janela
/// baixa o Comprar ficava para fora da tela, sem como chegar nele).
/// </summary>
public sealed class ItemDetailFitTest : RegressionTest
{
	public override string Name => "DetalheCabeNaTela";
	public override string Description => "o painel de detalhe do item cabe na altura da tela: numa janela alta mostra tudo sem rolar, com a vitrine inteira; numa baixa a vitrine encolhe, os atributos rolam só se preciso, e as ações (o Comprar) ficam sempre à vista";

	/// <summary>Um painel de detalhe numa janela de <paramref name="height"/> de altura, com o topo onde a loja o põe.</summary>
	private static async Task<(SubViewport View, ItemDetail Detail, Control Buy)> Open(RegressionBench b, int height)
	{
		var view = new SubViewport { Size = new Vector2I(1280, height), Disable3D = true };
		b.Scene.AddChild(view);
		var page = new MarginContainer();
		page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		page.AddThemeConstantOverride("margin_top", 96);                // o header do lobby e a faixa de cima da loja
		page.AddThemeConstantOverride("margin_bottom", 16);
		view.AddChild(page);
		HBoxContainer split = UiKit.Row(UiTokens.S4);
		page.AddChild(split);
		split.AddChild(UiKit.Spacer());

		CatalogItem[] weapons = Catalog.All.Where(i => i.Weapon != null).ToArray();
		// As ações da loja: o título da duração, os botões, o preço com o botão de comprar, e a linha do que falta.
		VBoxContainer actions = UiKit.Column(UiTokens.S2);
		actions.AddChild(UiKit.Caption("Duração"));
		actions.AddChild(new Control { CustomMinimumSize = new Vector2(0, 36) });
		var buy = new Button { Name = "Botao", Text = "Comprar", CustomMinimumSize = new Vector2(176, UiTokens.ButtonHeight), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
		actions.AddChild(buy);
		actions.AddChild(UiKit.Text("Faltam 1.000 de Cash", "Caption"));
		ItemDetail detail = ItemDetail.Make(weapons[0], weapons.Length > 1 ? weapons[1] : null, actions);
		split.AddChild(detail);
		await b.Frames(8);
		return (view, detail, buy);
	}

	public override async Task Run(RegressionBench b)
	{
		// Janela alta: tudo à vista, sem rolar, a vitrine do tamanho de sempre.
		var (tall, big, buyBig) = await Open(b, 1080);
		float foot = buyBig.GlobalPosition.Y + buyBig.Size.Y;
		RegressionBench.Note($"janela de 1080: o painel vai de {RegressionBench.F(big.GlobalPosition.Y)} a {RegressionBench.F(big.GlobalPosition.Y + big.Size.Y)}; os atributos pedem {RegressionBench.F(big.BodyWanted)}");
		b.Check(big.GlobalPosition.Y + big.Size.Y <= 1080f - ItemDetail.BottomGap + 1f, "numa janela alta o painel acaba antes do pé da tela");
		b.Check(!big.BodyScrolls, "os atributos aparecem inteiros, sem rolar");
		b.Check(foot <= 1080f, "o botão de comprar está na tela");
		b.Check(big.FindChild("Barras", true, false) is GridContainer { Columns: 2 }, "as barras de atributo vêm em duas colunas");
		tall.QueueFree();

		// Janelas baixas: o painel nunca passa do pé da tela, e o botão continua à vista.
		foreach (int height in new[] { 900, 768, 640 })
		{
			var (view, detail, buy) = await Open(b, height);
			float bottom = detail.GlobalPosition.Y + detail.Size.Y, button = buy.GlobalPosition.Y + buy.Size.Y;
			RegressionBench.Note($"janela de {height}: painel até {RegressionBench.F(bottom)}, botão até {RegressionBench.F(button)}, atributos {(detail.BodyScrolls ? "rolando" : "inteiros")}");
			b.Check(bottom <= height + 0.5f, $"numa janela de {height} de altura o painel não passa do pé da tela ({RegressionBench.F(bottom)})");
			b.Check(button <= height + 0.5f && buy.GlobalPosition.Y >= 0f, $"e o botão de comprar fica à vista ({RegressionBench.F(button)})");
			view.QueueFree();
		}
		await b.Frames(2);
	}
}
