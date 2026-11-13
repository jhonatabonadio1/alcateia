/// <summary>
/// Quem fez o quê: o que aparece na tela de créditos.
///
/// É aqui que entra o crédito de todo material de terceiros. Licença
/// CC BY (e parecidas) **exige** a linha — sem ela o jogo não pode ser
/// publicado. CC0 e domínio público não exigem, mas entram do mesmo
/// jeito: é o certo, e ajuda quem for procurar a origem depois.
///
/// A origem detalhada de cada som está em assets/audio/CREDITOS.md; a da
/// arte que vem de pacote, em docs/PACOTES.md; as licenças das letras dos
/// letreiros, em tools/mapas/fontes/licencas/.
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
			new Line("Mapas, armas e mãos", "feitos para o jogo, por código e no Blender (tools/)")
		}),
		new("Cenário", new[]
		{
			new Line("Texturas dos mapas, céus e objetos de cenário (barris, caixotes, estantes, pedras…)",
				"Poly Haven (polyhaven.com)", "CC0"),
			new Line("Letreiros: Overpass", "The Overpass Project Authors (Red Hat)", "SIL Open Font License 1.1", Required: true),
			new Line("Letreiros: Saira Stencil One e Archivo Black", "Omnibus-Type", "SIL Open Font License 1.1", Required: true),
			new Line("Letreiros: Bebas Neue", "Dharma Type", "SIL Open Font License 1.1", Required: true),
			new Line("Letreiros: Alfa Slab One", "JM Solé", "SIL Open Font License 1.1", Required: true),
			new Line("Letreiros: Stardos Stencil", "Vernon Adams", "SIL Open Font License 1.1", Required: true),
			new Line("Letreiros: Share Tech Mono", "Carrois Type Design", "SIL Open Font License 1.1", Required: true),
			new Line("Letreiros: Permanent Marker", "Font Diner", "Apache 2.0", Required: true)
		}),
		new("Sons", new[]
		{
			new Line("Passos, pulos, lobo, vozes, vinhetas, música do menu, ambiente do hangar e do metrô (modificados)",
				"Oculus Audio Pack 1 — Meta Platforms", "CC BY 4.0", Required: true),
			new Line("Tiros e recargas", "Free Firearm Sound Library", "CC0"),
			new Line("Interface, acertos, faca, explosão", "Kenney (kenney.nl)", "CC0"),
			new Line("Vento da Estação Polar e da Vila", "sintetizado para o jogo")
		}),
		new("Braços e armas em primeira pessoa", new[]
		{
			new Line("Animações dos braços (fuzis, submetralhadora, pistola, escopeta)", "Cransh (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Braços e mãos", "bumstrum, DJMaesen e teenjust500 (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("AK-74M", "creationwasteland (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Fuzil compacto com luneta", "doomsentinel (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("M16A2 com braços e animações", "user77 (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Submetralhadora", "eNse7en (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Pistola", "raimeiyonke (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Escopeta semiautomática", "haoliu95 (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Faca com braços e animações", "DJMaesen / bumstrum (Sketchfab)", "CC BY 4.0", Required: true),
			new Line("Granada de choque M84", "Coozy (Sketchfab)", "CC BY 4.0", Required: true)
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
