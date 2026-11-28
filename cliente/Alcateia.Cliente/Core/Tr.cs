using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace Alcateia.Cliente.Core;

/// <summary>
/// Os textos do launcher em vários idiomas (Jhon, 27/11: "coloque multi
/// idioma i18n no launcher. o padrão do jogo e do launcher será em inglês").
///
/// Um arquivo só, como o do jogo (src/assets/i18n/textos.csv):
/// <c>Assets/i18n/launcher.csv</c>, uma linha por texto e uma coluna por
/// idioma — <c>chave</c>, <c>en</c>, <c>pt_BR</c>, <c>es</c>. A chave é um
/// nome curto (<c>game.play</c>); o texto pode ter <c>{0}</c> para os valores
/// (<see cref="F"/>). Faltou a tradução: sai o inglês; faltou o inglês: a chave.
/// Outro idioma é só mais uma coluna (com o nome dele em <see cref="NameOf"/>).
///
/// Na tela, <c>{c:T game.play}</c> (<see cref="TExtension"/>) segue o idioma
/// na hora em que ele muda; no código, <see cref="T"/> e <see cref="F"/>, e
/// quem monta texto se refaz no <see cref="Changed"/>.
/// </summary>
public sealed class Tr : INotifyPropertyChanged
{
	public const string Default = "en";

	public static readonly Tr I = new();

	private readonly Dictionary<string, Dictionary<string, string>> _texts = new();
	private readonly List<string> _languages = new();
	private string _language = Default;

	private Tr() => Load();

	/// <summary>Os idiomas do arquivo, na ordem das colunas (o inglês primeiro).</summary>
	public static IReadOnlyList<string> Languages => I._languages;

	public static string Current => I._language;

	public static event Action Changed;
	public event PropertyChangedEventHandler PropertyChanged;

	/// <summary>O texto pela chave, no idioma atual (para a tela: <c>[game.play]</c>).</summary>
	public string this[string key] => Get(key);

	public static string T(string key) => I.Get(key);

	public static string F(string key, params object[] args)
	{
		string text = T(key);
		try
		{
			return string.Format(text, args);
		}
		catch (FormatException)
		{
			return text;
		}
	}

	/// <summary>Troca o idioma agora. Idioma que o arquivo não tem: inglês.</summary>
	public static void Set(string language)
	{
		if (string.IsNullOrEmpty(language) || !I._languages.Contains(language)) language = Default;
		if (I._language == language) return;
		I._language = language;
		I.PropertyChanged?.Invoke(I, new PropertyChangedEventArgs("Item[]"));
		Changed?.Invoke();
	}

	/// <summary>O nome do idioma na lista, no próprio idioma (os mesmos do jogo).</summary>
	public static string NameOf(string language) => language switch
	{
		"pt_BR" => "Português (Brasil)",
		"en" => "English",
		"es" => "Español",
		"fr" => "Français",
		"de" => "Deutsch",
		_ => language
	};

	private string Get(string key)
	{
		if (string.IsNullOrEmpty(key)) return key ?? "";
		if (!_texts.TryGetValue(key, out Dictionary<string, string> row)) return key;
		if (row.TryGetValue(_language, out string text) && text.Length > 0) return text;
		return row.TryGetValue(Default, out text) && text.Length > 0 ? text : key;
	}

	private void Load()
	{
		try
		{
			using Stream stream = AssetLoader.Open(new Uri("avares://WolfLeagueLauncher/Assets/i18n/launcher.csv"));
			using var reader = new StreamReader(stream, Encoding.UTF8);
			List<List<string>> rows = Csv(reader.ReadToEnd());
			if (rows.Count == 0) return;
			List<string> header = rows[0];
			for (int c = 1; c < header.Count; c++)
				if (header[c].Length > 0 && header[c] != "nota") _languages.Add(header[c]);
			for (int r = 1; r < rows.Count; r++)
			{
				List<string> row = rows[r];
				if (row.Count == 0 || row[0].Length == 0) continue;
				var texts = new Dictionary<string, string>();
				for (int c = 1; c < header.Count && c < row.Count; c++) texts[header[c]] = row[c];
				_texts[row[0]] = texts;
			}
		}
		catch (Exception e) when (e is IOException or InvalidOperationException or FileNotFoundException)
		{
			// Sem o arquivo, a tela mostra as chaves: feio, mas abre.
		}
	}

	/// <summary>CSV com aspas (texto com vírgula ou quebra de linha vai entre aspas; aspas dobradas).</summary>
	private static List<List<string>> Csv(string text)
	{
		var rows = new List<List<string>>();
		var row = new List<string>();
		var field = new StringBuilder();
		bool quoted = false;
		for (int i = 0; i < text.Length; i++)
		{
			char ch = text[i];
			if (quoted)
			{
				if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
				else if (ch == '"') quoted = false;
				else field.Append(ch);
			}
			else if (ch == '"') quoted = true;
			else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
			else if (ch == '\n' || ch == '\r')
			{
				if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
				row.Add(field.ToString());
				field.Clear();
				rows.Add(row);
				row = new List<string>();
			}
			else field.Append(ch);
		}
		if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
		return rows;
	}
}

/// <summary>Na tela: <c>Text="{c:T game.play}"</c> — o texto da chave, que troca junto com o idioma.</summary>
public sealed class TExtension : MarkupExtension
{
	public TExtension() { }
	public TExtension(string key) => Key = key;

	public string Key { get; set; }

	public override object ProvideValue(IServiceProvider serviceProvider) =>
		new Binding($"[{Key}]") { Source = Tr.I, Mode = BindingMode.OneWay };
}
