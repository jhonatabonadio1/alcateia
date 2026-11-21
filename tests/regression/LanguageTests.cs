using System.Linq;
using Godot;
using System.Threading.Tasks;

/// <summary>
/// Os textos num arquivo só (assets/i18n/textos.csv, <see cref="Loc"/>):
/// o arquivo carrega, a coluna pt_BR troca um texto sem mexer no código,
/// um idioma novo traduz rótulos (os fixos sozinhos, os montados pelo
/// Loc), e o que não tem tradução aparece em português.
/// </summary>
public sealed class LanguageTests : RegressionTest
{
	public override string Name => "Idiomas";
	public override string Description => "o arquivo de textos carrega, traduz e cai no português quando falta";

	public override async Task Run(RegressionBench b)
	{
		Loc.Load();
		string before = TranslationServer.GetLocale();
		Loc.Apply(Loc.Source);
		b.AtLeast(500, Loc.Total, "o arquivo de textos tem os textos do jogo");
		b.Check(Loc.Languages.Count >= 1 && Loc.Languages[0] == Loc.Source, "o português é o primeiro idioma");
		b.Check(Loc.T("Opções") == "Opções", "sem tradução, o texto é o do código");
		b.Check(Loc.F("{0} contra {1}", 3, 3) == "3 contra 3", "texto montado, em português");

		// Um idioma de teste, registrado na hora: é o que uma coluna nova do arquivo faz.
		Translation test = Loc.AddLanguage("eo", new System.Collections.Generic.Dictionary<string, string>
		{
			["Opções"] = "Agordoj",
			["{0} contra {1}"] = "{1} kontraŭ {0}",
			["Faltam {0:0} WP"] = "Mankas {0:0} WP {9}"        // tradução com a chave errada
		});
		TranslationServer.SetLocale("eo");

		b.Check(Loc.T("Opções") == "Agordoj", "com o idioma, o texto traduz");
		b.Check(Loc.Upper("Opções") == "AGORDOJ", "caixa alta traduz antes de subir");
		b.Check(Loc.F("{0} contra {1}", 2, 5) == "5 kontraŭ 2", "a tradução pode trocar a ordem dos valores");
		b.Check(Loc.F("Faltam {0:0} WP", 12f) == "Faltam 12 WP", "tradução com chave errada cai no texto do código");
		b.Check(Loc.T("Créditos") == "Créditos", "o que falta no idioma aparece em português");

		// Rótulo comum: o Godot traduz sozinho ao desenhar.
		var label = new Label { Text = "Opções" };
		b.AddChild(label);
		await b.Seconds(0.1);
		b.Check(label.Tr(label.Text) == "Agordoj", "rótulo com texto fixo é traduzido pelo Godot");
		label.QueueFree();

		TranslationServer.SetLocale(Loc.Source);
		TranslationServer.RemoveTranslation(test);
		b.Check(Loc.T("Opções") == "Opções", "de volta ao português");

		// Os idiomas do arquivo: inglês e espanhol preenchidos, e o português
		// não cai no inglês (o idioma reserva do Godot).
		b.Check(System.Linq.Enumerable.Contains(Loc.Languages, "en") && System.Linq.Enumerable.Contains(Loc.Languages, "es"), "o arquivo tem inglês e espanhol");
		// Texto novo entra sem tradução (e aparece em português) até alguém
		// traduzir: o que se cobra é que o grosso esteja traduzido.
		b.AtLeast(Loc.Total * 0.9, Loc.Count("en"), "nove em cada dez textos têm inglês");
		b.AtLeast(Loc.Total * 0.9, Loc.Count("es"), "nove em cada dez textos têm espanhol");
		b.Check(Loc.T("Créditos") == "Créditos", "em português, o texto é o português (não o inglês)");
		TranslationServer.SetLocale("en");
		b.Check(Loc.T("Créditos") == "Credits", "em inglês, traduz");
		// O que não é texto de tela fixo também (Jhon, 21/11): mapa, item,
		// arma, personagem e patente saem no idioma do jogo.
		b.Check(Maps.Get("neon").Name == "Neon District" && Maps.Get("neon").Description.StartsWith("Large"), $"mapa: nome e descrição ({Maps.Get("neon").Name})");
		b.Check(Catalog.Get("scar")?.Type == "Heavy weapon", $"item: o tipo ({Catalog.Get("scar")?.Type})");
		b.Check(Arsenal.NameOf("choque_neon") == "Neon Grenade", $"arma: o nome ({Arsenal.NameOf("choque_neon")})");
		b.Check(Catalog.All.Any(i => i.Kind == ItemKind.Character && i.Description.StartsWith("From the")), "personagem: a descrição");
		b.Check(Ranks.NameOf(5).StartsWith("Level ") && Ranks.FamilyName(Ranks.RankFor(long.MaxValue / 4)) != "", $"patente: {Ranks.NameOf(5)}");
		TranslationServer.SetLocale("es");
		b.Check(Loc.T("Voltar às opções") == "Volver a opciones", "em espanhol, traduz");
		TranslationServer.SetLocale(before);
	}
}
