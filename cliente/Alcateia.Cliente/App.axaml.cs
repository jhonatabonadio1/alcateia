using System;
using Alcateia.Cliente.ViewModels;
using Alcateia.Cliente.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Alcateia.Cliente;

/// <summary>
/// O aplicativo: a janela, o ícone da bandeja (abrir, sair) e o que fazer
/// quando alguém abre o cliente de novo com ele já aberto (mostrar a janela).
/// </summary>
public partial class App : Application
{
	private MainWindow _window;

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var vm = new MainViewModel();
			_window = new MainWindow { DataContext = vm };
			vm.Quit = () => Quit(desktop);
			_window.Closed += (_, _) => desktop.Shutdown();
			_window.HasTray = AddTray(desktop);

			Program.ShowRequested += () => Dispatcher.UIThread.Post(ShowWindow);

			// Aberto com --minimizado (junto com o sistema): fica na bandeja, sem janela.
			if (Program.StartHidden && _window.HasTray) _window.Attach();
			else _window.Show();
		}
		base.OnFrameworkInitializationCompleted();
	}

	private void ShowWindow()
	{
		if (_window == null) return;
		_window.Show();
		if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
		_window.Activate();
	}

	private void Quit(IClassicDesktopStyleApplicationLifetime desktop)
	{
		if (_window != null) _window.Quitting = true;
		desktop.Shutdown();
	}

	/// <summary>O ícone da bandeja. Falso se o sistema não tem uma.</summary>
	private bool AddTray(IClassicDesktopStyleApplicationLifetime desktop)
	{
		try
		{
			var open = new NativeMenuItem("Abrir o WolfLeague");
			open.Click += (_, _) => ShowWindow();
			var quit = new NativeMenuItem("Sair");
			quit.Click += (_, _) => Quit(desktop);
			var menu = new NativeMenu();
			menu.Items.Add(open);
			menu.Items.Add(new NativeMenuItemSeparator());
			menu.Items.Add(quit);

			var tray = new TrayIcon
			{
				Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://WolfLeagueLauncher/Assets/icone.ico"))),
				ToolTipText = "WolfLeague",
				Menu = menu
			};
			tray.Clicked += (_, _) => ShowWindow();
			TrayIcon.SetIcons(this, new TrayIcons { tray });
			return true;
		}
		catch (Exception e) when (e is PlatformNotSupportedException or InvalidOperationException or NotSupportedException)
		{
			return false;
		}
	}
}
