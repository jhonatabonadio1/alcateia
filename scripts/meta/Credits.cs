/// <summary>
/// Quem fez o quê: o que aparece na tela de créditos.
///
/// É aqui que entra o crédito de todo material de terceiros. Licença
/// CC BY (e parecidas) **exige** a linha — sem ela o jogo não pode ser
/// publicado. CC0 e domínio público não exigem, mas entram do mesmo
/// jeito: é o certo, e ajuda quem for procurar a origem depois.
///
/// A origem detalhada de cada som está em assets/audio/CREDITOS.md; a da
/// arte que vier de pacote, em docs/PACOTES.md.
/// </summary>
public static class Credits
{
	/// <summary>Uma linha: o quê, de quem, sob qual licença.</summary>
	public readonly record struct Line(string What, string Who, string License = "", bool Required = false);

	/// <summary>Um bloco da tela.</summary>
	public readonly record struct Section(string Title, Line[] Lines);

	public static readonly Section[] All =
	{
		new("Alcateia", new[]
		{
			new Line("Criação, design e programação", "Jhonata Bonadio"),
			new Line("Mapas, armas, mãos e texturas", "feitos para o jogo, gerados por código (tools/)")
		}),
		new("Sons", new[]
		{
			new Line("Passos, pulos, lobo, vozes, vinhetas, música do menu, ambiente do hangar e do metrô (modificados)",
				"Oculus Audio Pack 1 — Meta Platforms", "CC BY 4.0", Required: true),
			new Line("Tiros e recargas", "Free Firearm Sound Library", "CC0"),
			new Line("Interface, acertos, faca, explosão", "Kenney (kenney.nl)", "CC0"),
			new Line("Vento da Estação Polar e da Vila", "sintetizado para o jogo")
		}),
		new("Personagens e animação", new[]
		{
			new Line("Modelos e animações do humano e do lobo", "Mixamo — Adobe", "licença de uso do Mixamo")
		}),
		new("Letras", new[]
		{
			new Line("Barlow e Barlow Condensed", "Jeremy Tribby", "SIL Open Font License 1.1", Required: true)
		}),
		new("Tecnologia", new[]
		{
			new Line("Motor do jogo", "Godot Engine — Juan Linietsky, Ariel Manzur e colaboradores", "MIT", Required: true),
			new Line("Física", "Jolt Physics — Jorrit Rouwe", "MIT", Required: true),
			new Line("Servidor", ".NET — .NET Foundation e colaboradores", "MIT"),
			new Line("Banco de dados", "PostgreSQL Global Development Group", "PostgreSQL License")
		})
	};
}
