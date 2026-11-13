using Godot;

/// <summary>
/// O que precisa valer antes de qualquer cena: hoje, a semente do sorteio
/// (<see cref="GameRandom"/>). Autoload, para rodar antes do _Ready dos
/// bots e das armas, que pedem o seu fluxo ao nascer.
///
/// <c>-- --seed=N</c> fixa a semente: a partida sorteia igual (a mira dos
/// bots, a dispersão, as rotas), e um defeito que dependeu da sorte volta.
/// Sem isso a semente vem do relógio, e a partida diz qual foi no Output.
/// </summary>
public partial class Inicio : Node
{
	public override void _EnterTree()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--seed=") && ulong.TryParse(arg["--seed=".Length..], out ulong seed))
				MatchRandom.Begin(seed);
		}
	}
}
