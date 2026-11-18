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
		b.AtLeast(500, Loc.Total, "o arquivo de textos tem os textos do jogo");
		b.Check(Loc.Languages.Count >= 1 && Loc.Languages[0] == Loc.Source, "o português é o primeiro idioma");
		b.Check(Loc.T("Opções") == "Opções", "sem tradução, o texto é o do código");
		b.Check(Loc.F("{0} contra {1}", 3, 3) == "3 contra 3", "texto montado, em português");

		// Um idioma de teste, registrado na hora: é o que uma coluna nova do arquivo faz.
		var test = new Translation { Locale = "eo" };
		test.AddMessage("Opções", "Agordoj");
		test.AddMessage("{0} contra {1}", "{1} kontraŭ {0}");
		test.AddMessage("Faltam {0:0} WP", "Mankas {0:0} WP {9}");       // tradução com a chave errada
		TranslationServer.AddTranslation(test);
		string before = TranslationServer.GetLocale();
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

		TranslationServer.SetLocale(before);
		TranslationServer.RemoveTranslation(test);
		b.Check(Loc.T("Opções") == "Opções", "de volta ao português");
	}
}
