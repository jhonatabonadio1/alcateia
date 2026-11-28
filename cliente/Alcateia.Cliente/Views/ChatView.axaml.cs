using System;
using Alcateia.Cliente.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Alcateia.Cliente.Views;

/// <summary>A conversa: rola para o fim quando chega ou sai mensagem, e põe o cursor no campo ao abrir.</summary>
public partial class ChatView : UserControl
{
	private ChatViewModel _vm;

	public ChatView()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => Hook();
	}

	private void Hook()
	{
		if (_vm != null) _vm.Scrolled -= ToEnd;
		_vm = DataContext as ChatViewModel;
		if (_vm == null) return;
		_vm.Scrolled += ToEnd;
		ToEnd();
		Dispatcher.UIThread.Post(() => Input?.Focus());
	}

	private void ToEnd() => Dispatcher.UIThread.Post(() => Scroll?.ScrollToEnd(), DispatcherPriority.Background);
}
