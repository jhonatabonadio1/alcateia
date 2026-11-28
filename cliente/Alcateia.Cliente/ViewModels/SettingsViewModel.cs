using System;
using Alcateia.Cliente.Core;

namespace Alcateia.Cliente.ViewModels;

/// <summary>
/// As configurações do cliente: atualizar sozinho, o que fazer ao fechar a
/// janela e ao abrir o jogo, abrir junto com o Windows, e a manutenção do
/// jogo (a pasta, verificar e reparar, desinstalar).
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
			if (!Startup.Set(value)) _main.Toast("Não consegui mudar a abertura com o Windows.", error: true);
			Raise();
		}
	}

	public string VersionText => "Cliente " + AppInfo.Version;
	public string ServerText => AppInfo.ServerUrl;
	public string DownloadsText => string.IsNullOrEmpty(_main.GameService.Downloads) ? "ainda não informado pelo servidor" : _main.GameService.Downloads;

	internal void Refresh() => RaiseAll();
}
