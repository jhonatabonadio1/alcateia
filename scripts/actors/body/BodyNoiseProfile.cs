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

	/// <summary>Distância percorrida entre um passo e outro, em metros.</summary>
	[Export] public float StepDistance = 2.2f;
}
