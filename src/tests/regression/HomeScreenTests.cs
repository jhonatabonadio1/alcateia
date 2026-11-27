using Godot;
using System.Linq;
using System.Threading.Tasks;

/// <summary>A tela principal do jogo (pedido do Jhon, 26/11): quatro jeitos de
/// jogar — Jogo rápido e Arcade em breve, Salas e Treino valendo.</summary>
public sealed class HomeScreenTest : RegressionTest
{
	public override string Name => "TelaPrincipal";
	public override string Description => "a tela principal tem três quadrados no centro (Jogo rápido e Arcade em breve, Salas) e a faixa do Treino; os de em breve não levam a lugar nenhum";

	public override async Task Run(RegressionBench b)
	{
		var layer = new CanvasLayer();
		b.Scene.AddChild(layer);
		var screen = new HomeScreen();
		layer.AddChild(screen);
		await b.Frames(4);

		Control Card(string name) => screen.FindChild(name, true, false) as Control;
		string[] names = { "JogoRapido", "Arcade", "Salas", "Treino" };
		b.Check(names.All(n => Card(n) != null), "os quatro modos estão na tela");

		Control[] squares = { Card("JogoRapido"), Card("Arcade"), Card("Salas") };
		b.Check(squares.All(c => Mathf.Abs(c.Size.X - c.Size.Y) < 2f && c.Size.X >= HomeScreen.Square - 1), $"três quadrados ({squares[0].Size})");
		b.Check(squares.Select(c => c.GlobalPosition.Y).Distinct().Count() == 1 && squares[0].GlobalPosition.X < squares[1].GlobalPosition.X && squares[1].GlobalPosition.X < squares[2].GlobalPosition.X,
			"lado a lado, na ordem: Jogo rápido, Arcade, Salas");
		Vector2 view = screen.GetViewportRect().Size;
		float middle = (squares[0].GlobalPosition.X + squares[2].GlobalPosition.X + squares[2].Size.X) / 2f;
		b.Near(view.X / 2f, middle, 3f, "no centro da tela");
		Control strip = Card("Treino");
		b.Check(strip.GlobalPosition.Y > squares[0].GlobalPosition.Y + squares[0].Size.Y && strip.Size.X > strip.Size.Y * 3f, "a faixa do Treino fica por baixo, larga");

		bool Soon(string name) => Card(name).FindChild("EmBreve", true, false) != null && Card(name).GetNode<Button>("Entrar").GetMeta("em_breve").AsBool();
		b.Check(Soon("JogoRapido") && Soon("Arcade"), "Jogo rápido e Arcade: em breve");
		b.Check(!Soon("Salas") && !Soon("Treino"), "Salas e Treino: valendo");

		// Clicar no que ainda não existe não muda de cena nem quebra.
		Node before = b.GetTree().CurrentScene;
		Card("JogoRapido").GetNode<Button>("Entrar").EmitSignal(BaseButton.SignalName.Pressed);
		Card("Arcade").GetNode<Button>("Entrar").EmitSignal(BaseButton.SignalName.Pressed);
		await b.Frames(3);
		b.Check(b.GetTree().CurrentScene == before && GodotObject.IsInstanceValid(screen), "os de em breve não levam a lugar nenhum");
		b.Check(ResourceLoader.Exists(HomeScreen.TrainingScene), "a cena do campo de treinamento existe");
		layer.QueueFree();
	}
}
