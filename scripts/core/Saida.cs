using Godot;

/// <summary>
/// Deixa o jogo fechar limpo: esvazia o coletor de lixo do C# depois que
/// a cena já foi destruída e antes de o C# desligar.
///
/// Por que existe: cada personagem monta animações, árvores de animação
/// e materiais pelo C#. O lado C# de cada um desses objetos só solta a
/// referência quando o coletor passa e roda o finalizador. Se o jogo
/// fecha com finalizadores ainda na fila, eles rodam depois de o C# já
/// ter desligado, e o Godot aborta na saída ("script_bindings is not
/// empty", com uma lista de "Leaked unsafe reference"). Aparecia depois
/// de partidas longas, que é quando há mais objetos na fila.
///
/// Por que no PREDELETE: a raiz apaga os filhos do último para o
/// primeiro, então a cena da partida some antes deste nó, que é
/// autoload (primeiro filho). Aqui tudo o que a partida segurava já foi
/// solto.
///
/// Por que em laço: soltar um objeto solta os que ele segurava (a
/// biblioteca solta as animações, a máquina de estados solta os nós), e
/// cada camada só fica livre para o coletor na passada seguinte. Para
/// quando uma passada não acha mais nada, com um teto por segurança.
/// </summary>
public partial class Saida : Node
{
	private const int MaxPasses = 12;

	public override void _Notification(int what)
	{
		if (what == NotificationPredelete) Flush();
	}

	public static void Flush()
	{
		for (int i = 0; i < MaxPasses; i++)
		{
			long before = System.GC.GetTotalMemory(false);
			System.GC.Collect();
			System.GC.WaitForPendingFinalizers();
			if (i > 1 && System.GC.GetTotalMemory(false) >= before) break;
		}
	}
}
