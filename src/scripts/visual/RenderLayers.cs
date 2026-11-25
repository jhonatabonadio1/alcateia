using Godot;

/// <summary>
/// Trocar a camada de desenho (<see cref="VisualInstance3D.Layers"/>) de uma
/// malha que já está no mundo, sem deixar a malha presa a uma luz.
///
/// Por quê (o crash do "Voltar à sala" e do "jogar novamente", 25/11): no
/// Godot 4.7 com o Forward+, a malha e cada luz que a alcança ficam
/// emparelhadas, e o par só é desfeito se a máscara da luz ainda pegar a
/// camada da malha. Trocar a camada não desfaz os pares (o Forward+ não pede
/// isso), e a malha que **perde** a camada de uma luz de máscara restrita (a
/// luz dos personagens, que só acende a camada 18) fica presa a ela para
/// sempre: quando essa luz some (a troca de cena), o Godot avisa "BUG,
/// indexing did not unpair geometries from light" e a malha segura um
/// ponteiro para a luz apagada; na atualização seguinte dela o jogo cai
/// (sinal 11 lendo o 0x6c da luz que não existe mais, o que o crash.txt
/// mostra). Acontecia com o espectador em primeira pessoa
/// (<see cref="KillCam"/> põe o corpo de quem você assiste só na camada
/// escondida) e qualquer outra troca que tire camada.
///
/// O conserto: esconder a malha desfaz todos os pares com a camada antiga;
/// troca a camada; mostrar de novo refaz os pares com a nova. Tudo no mesmo
/// quadro, então nada pisca. Só pôr camada a mais é seguro sem isso (todo
/// par antigo continua valendo).
/// </summary>
public static class RenderLayers
{
	public static void Set(VisualInstance3D visual, uint layers)
	{
		uint before = visual.Layers;
		if (before == layers) return;

		bool losesLayer = (before & ~layers) != 0;
		if (!losesLayer || !visual.Visible || !visual.IsVisibleInTree())
		{
			// Fora de vista a malha não está emparelhada com luz nenhuma.
			visual.Layers = layers;
			return;
		}

		visual.Visible = false;
		visual.Layers = layers;
		visual.Visible = true;
	}
}
