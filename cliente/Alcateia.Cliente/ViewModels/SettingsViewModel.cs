using System;
using Alcateia.Cliente.Core;

namespace Alcateia.Cliente.ViewModels;

/// <summary>
/// As configurações do launcher: o idioma, atualizar os jogos sozinho, o que
/// fazer ao fechar a janela e ao abrir um jogo, abrir junto com o Windows. A
/// manutenção de cada jogo (pasta, reparar, desinstalar) fica no menu de
/// opções da página dele.
/// </summary>
public sealed class SettingsViewModel : Observable
{
	private readonly MainViewModel _main;

	public SettingsViewModel(MainViewModel main) => _main = main;

	private Settings S => _main.Settings;

	public bool AutoUpdate
	{
		get => S.AutoUpdate;
		set { S.AutoUpdate = value; S.Save(); Raise(); }
	}

	public bool CloseToTray
	{
		get => S.CloseToTray;
		set { S.CloseToTray = value; S.Save(); Raise(); }
	}

	public bool MinimizeOnPlay
	{
		get => S.MinimizeOnPlay;
		set { S.MinimizeOnPlay = value; S.Save(); Raise(); }
	}

	public bool AutoLogin
	{
		get => S.AutoLogin;
		set { S.AutoLogin = value; S.Save(); Raise(); }
	}

	/// <summary>Abrir com o sistema só existe no Windows (a chave Run do usuário).</summary>
	public bool CanStartWithSystem => AppInfo.IsWindows;

	public bool StartWithSystem
	{
		get => Startup.Enabled;
		set
		{
			if (!Startup.Set(value)) _main.Toast(Tr.T("toast.startup_failed"), error: true);
			Raise();
		}
	}

	public string VersionText => Tr.F("settings.version", AppInfo.Version);
	public string ServerText => AppInfo.ServerUrl;
	public string DownloadsText => string.IsNullOrEmpty(_main.GameService.Downloads) ? Tr.T("settings.downloads_unknown") : _main.GameService.Downloads;

	/// <summary>Os idiomas para escolher (os do arquivo), pelo nome no próprio idioma.</summary>
	public System.Collections.Generic.IReadOnlyList<LanguageOption> LanguageOptions { get; } =
		System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(Tr.Languages, l => new LanguageOption(l, Tr.NameOf(l))));

	public LanguageOption Language
	{
		get => System.Linq.Enumerable.FirstOrDefault(LanguageOptions, o => o.Code == Tr.Current);
		set
		{
			if (value == null || value.Code == Tr.Current) return;
			S.Language = value.Code;
			S.Save();
			Tr.Set(value.Code);
		}
	}

	internal void Refresh() => RaiseAll();
}

/// <summary>Um idioma na lista das configurações.</summary>
public sealed record LanguageOption(string Code, string Name)
{
	public override string ToString() => Name;
}
