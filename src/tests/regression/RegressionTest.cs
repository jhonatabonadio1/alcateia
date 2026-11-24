using System.Threading.Tasks;

/// <summary>
/// Um teste de regressão: monta uma situação no jogo de verdade (física,
/// animação, bots), mede e compara com o comportamento aprovado. Roda
/// sem tela, em 60 quadros por segundo fixos, um teste por processo.
///
/// Os números aceitos aqui são os aprovados pelo Jhon. Mudou o
/// comportamento de propósito? Mude o teste no mesmo commit, dizendo por
/// quê.
/// </summary>
public abstract class RegressionTest
{
	/// <summary>Nome curto, sem espaço: é o que se passa em --teste=.</summary>
	public abstract string Name { get; }

	/// <summary>A regra que o teste prova, em português.</summary>
	public abstract string Description { get; }

	/// <summary>A cena em que o teste roda.</summary>
	public virtual string Scene => RegressionBench.Training;

	/// <summary>Antes de a cena carregar: o que precisa estar valendo
	/// quando ela nasce (a sala, o mapa).</summary>
	public virtual void Prepare() { }

	public abstract Task Run(RegressionBench bench);
}
