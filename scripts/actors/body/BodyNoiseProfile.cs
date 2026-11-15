using Godot;

/// <summary>
/// Quanto barulho o corpo faz — os raios, em metros, até onde cada som é
/// ouvido. Um arquivo só para o jogador e para o bot
/// (resources/body/ruido.tres): ouvir e ser ouvido segue a mesma regra
/// dos dois lados.
///
/// Som é informação de jogo: o bot ouve o que o jogador ouviria. Agachar
/// é silencioso, e é isso que torna o agachamento uma decisão tática em
/// vez de um botão de encolher.
/// </summary>
[GlobalClass]
public partial class BodyNoiseProfile : Resource
{
	/// <summary>Raio do passo humano andando.</summary>
	[Export] public float StepHuman = 13f;

	/// <summary>Raio do passo do lobo. Mais alto: o corpo é maior e mais
	/// rápido, e isso precisa ter um custo.</summary>
	[Export] public float StepWolf = 20f;

	/// <summary>Raio do baque ao aterrissar.</summary>
	[Export] public float Land = 17f;

	/// <summary>Raio do rugido. Muito alto de propósito — entregar a
	/// posição é metade do preço de recuperar o escudo.</summary>
	[Export] public float Howl = 45f;

	/// <summary>
	/// A passada: quantos metros o corpo anda entre um pé no chão e o
	/// outro. O ruído do passo sai a cada passada, então o som cai junto
	/// com o pé da animação — e acelera e desacelera com o corpo sozinho.
	///
	/// Os números saem dos clipes (velocidade em que o pé não patina ×
	/// duração do ciclo ÷ 2 pisadas) e o teste PassoNoRitmo confere:
	/// humano correndo, 3,24 m/s × 0,733 s ÷ 2; lobo em pé, o andar do Jhon
	/// de frente, 1,45 × 1,433 ÷ 2. Corrida de quatro: uma batida por ciclo
	/// (o som sai quando as patas da frente e as de trás se juntam, pedido
	/// do Jhon), com o clipe a 1× e o corpo a 6,02 m/s: 6,02 × 0,6.
	///
	/// No jogo o lobo usa estes só sem o modelo: com ele, a passada em pé
	/// vem do andar que está tocando (frente, trás ou lado) e a corrida de
	/// quatro bate pelo clipe (<see cref="WolfVisual.SprintBeats"/>).
	/// Trocou o clipe ou a velocidade de andar, refaça a conta.
	/// </summary>
	[ExportGroup("Passada")]
	[Export] public float StrideHuman = 1.19f;
	[Export] public float StrideWolf = 1.04f;
	[Export] public float StrideWolfSprint = 3.61f;
}
