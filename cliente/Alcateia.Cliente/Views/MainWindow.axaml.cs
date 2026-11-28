using System;
using System.Linq;
using System.Threading.Tasks;
using Alcateia.Cliente.ViewModels;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Alcateia.Cliente.Views;

/// <summary>
/// A janela: mostra o <see cref="MainViewModel"/> e empresta a ele o que só
/// uma janela tem — a área de transferência, o seletor de pasta, sair da
/// frente — e o relógio que confere se há versão nova.
/// </summary>
public partial class MainWindow : Window
{
	/// <summary>De quanto em quanto tempo se pergunta por versão nova, com o cliente aberto.</summary>
	private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(20);

	private readonly DispatcherTimer _timer = new() { Interval = CheckEvery };

	/// <summary>Fechar de verdade (o "Sair" da bandeja), e não ir para a bandeja.</summary>
	public bool Quitting { get; set; }

	/// <summary>Há bandeja para onde ir? (Sem ela, fechar a janela fecha o cliente.)</summary>
	public bool HasTray { get; set; }

	public MainWindow()
	{
		InitializeComponent();
		_timer.Tick += (_, _) =>
		{
			if (DataContext is MainViewModel vm) _ = vm.Recheck();
		};
	}

	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		Attach();
	}

	private bool _attached;

	/// <summary>Liga os ganchos e começa (uma vez, mesmo que a janela nasça escondida).</summary>
	public void Attach()
	{
		if (_attached || DataContext is not MainViewModel vm) return;
		_attached = true;
		vm.CopyText = async text =>
		{
			if (Clipboard != null) await Clipboard.SetTextAsync(text);
		};
		vm.PickFolder = async title =>
		{
			var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
			return picked.FirstOrDefault()?.TryGetLocalPath();
		};
		vm.HideWindow = () => WindowState = WindowState.Minimized;
		vm.WindowAway = () => !IsVisible || WindowState == WindowState.Minimized;
		_timer.Start();
		_ = vm.Start();
	}

	protected override void OnClosing(WindowClosingEventArgs e)
	{
		// Fechar a janela deixa o cliente na bandeja: os amigos seguem vendo
		// você e o download segue. Sair de verdade é pelo menu da bandeja.
		if (!Quitting && HasTray && DataContext is MainViewModel { Settings.CloseToTray: true } vm)
		{
			e.Cancel = true;
			Hide();
			// Foi para a bandeja: se há versão nova do cliente esperando, é a hora.
			vm.RestartIfIdle();
			return;
		}
		base.OnClosing(e);
	}
}
