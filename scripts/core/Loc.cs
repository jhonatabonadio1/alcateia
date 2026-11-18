using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Os textos do jogo num arquivo só (pedido do Jhon, 18/11):
/// <c>assets/i18n/textos.csv</c>, para trocar nomes e textos sem mexer no
/// código e para abrir outros idiomas.
///
/// **Como funciona.** A chave é o texto em português como está no código.
/// O arquivo tem uma coluna por idioma:
///
/// - <c>texto</c>: a chave (não se mexe);
/// - <c>pt_BR</c>: o que aparece em português — vazia, é a própria chave;
///   preenchida, troca o texto na tela (renomear uma arma, um botão);
/// - <c>en</c>, <c>es</c>…: a tradução — vazia, aparece em português. Outro
///   idioma é só mais uma coluna com o código dele;
/// - <c>onde</c>: de que arquivo veio (para quem traduz; o jogo ignora).
///
/// Na carga, cada coluna vira uma tradução do Godot
/// (<see cref="TranslationServer"/>). O que é <see cref="Control"/> com
/// texto fixo (rótulo, botão, aba, caixa) o Godot traduz sozinho na hora
/// de desenhar. O que o código monta (texto com número ou nome dentro) e o
/// que é desenhado na mão passam por <see cref="T"/> e <see cref="F"/>.
///
/// Quem gera e atualiza o arquivo é <c>tools/i18n/extrair.py</c>, que não
/// perde o que já foi traduzido.
/// </summary>
public static class Loc
{
	public const string FilePath = "res://assets/i18n/textos.csv";

	/// <summary>O idioma do código: a chave já é o texto.</summary>
	public const string Source = "pt_BR";

	/// <summary>Colunas que não são idioma.</summary>
	private static readonly HashSet<string> NotLanguages = new() { "texto", "onde", "nota" };

	private static readonly List<string> Found = new() { Source };
	private static readonly Dictionary<string, int> Filled = new();
	private static bool _loaded;

	/// <summary>Os idiomas do arquivo, na ordem das colunas (o português primeiro).</summary>
	public static IReadOnlyList<string> Languages { get { Load(); return Found; } }

	/// <summary>Quantos textos cada idioma tem traduzidos.</summary>
	public static int Count(string language) { Load(); return Filled.GetValueOrDefault(language); }

	/// <summary>Quantos textos o arquivo tem.</summary>
	public static int Total { get; private set; }

	/// <summary>O nome do idioma na lista de opções, no próprio idioma.</summary>
	public static string NameOf(string language) => language switch
	{
		"pt_BR" => "Português (Brasil)",
		"en" => "English",
		"es" => "Español",
		"fr" => "Français",
		"de" => "Deutsch",
		"it" => "Italiano",
		"ru" => "Русский",
		"ja" => "日本語",
		"zh" => "中文",
		_ => language
	};

	/// <summary>Lê o arquivo e registra os idiomas. Uma vez; chamar de novo não faz nada.</summary>
	public static void Load()
	{
		if (_loaded) return;
		_loaded = true;

		if (!FileAccess.FileExists(FilePath)) return;
		using FileAccess file = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
		if (file == null) return;

		string[] header = file.GetCsvLine();
		var translations = new Dictionary<int, Translation>();
		for (int column = 0; column < header.Length; column++)
		{
			string code = header[column].Trim();
			if (code == "" || NotLanguages.Contains(code)) continue;
			translations[column] = new Translation { Locale = code };
			if (!Found.Contains(code)) Found.Add(code);
			Filled[code] = 0;
		}

		while (!file.EofReached())
		{
			string[] row = file.GetCsvLine();
			if (row.Length == 0 || row[0] == "") continue;
			Total++;
			foreach (var (column, translation) in translations)
			{
				if (column >= row.Length || row[column] == "") continue;
				translation.AddMessage(row[0], row[column]);
				Filled[translation.Locale]++;
			}
		}

		foreach (Translation translation in translations.Values)
			TranslationServer.AddTranslation(translation);
	}

	/// <summary>Carrega e põe o idioma das opções. No começo do jogo.</summary>
	public static void Start()
	{
		Load();
		Apply(Settings.Values.Language);
	}

	/// <summary>Troca o idioma agora (e não grava: quem grava é a tela de opções).</summary>
	public static void Apply(string language)
	{
		Load();
		if (string.IsNullOrEmpty(language) || !Found.Contains(language)) language = Source;
		TranslationServer.SetLocale(language);
		Current = language;
		Changed?.Invoke();
	}

	public static string Current { get; private set; } = Source;

	/// <summary>O idioma mudou: quem montou texto em código se refaz.</summary>
	public static event Action Changed;

	/// <summary>O texto no idioma atual; sem tradução, o próprio texto.</summary>
	public static string T(string text) =>
		string.IsNullOrEmpty(text) ? text : TranslationServer.Translate(text).ToString();

	/// <summary>O texto no idioma atual, em maiúsculas. Para rótulo que o
	/// código põe em caixa alta: traduz ANTES (a chave é o texto como está
	/// escrito; em maiúsculas ela não seria achada).</summary>
	public static string Upper(string text) => T(text ?? "").ToUpperInvariant();

	/// <summary>Texto com valores dentro: <c>Loc.F("Exige a patente {0}", nome)</c>.
	/// A tradução pode mudar a ordem dos valores.</summary>
	public static string F(string format, params object[] args)
	{
		string translated = T(format);
		try
		{
			return string.Format(translated, args);
		}
		catch (FormatException)
		{
			// Tradução com as chaves erradas: o texto do código, que está certo.
			return string.Format(format, args);
		}
	}
}
