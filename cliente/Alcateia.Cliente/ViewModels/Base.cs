using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Alcateia.Cliente.ViewModels;

/// <summary>O mínimo para a tela acompanhar um objeto: avisa quando uma propriedade muda.</summary>
public abstract class Observable : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		Raise(name);
		return true;
	}

	protected void Raise([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	/// <summary>Tudo mudou: a tela relê todas as propriedades.</summary>
	protected void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
}

/// <summary>Um botão: o que faz e se está valendo.</summary>
public sealed class Command : ICommand
{
	private readonly Action<object> _run;
	private readonly Func<bool> _can;

	public Command(Action run, Func<bool> can = null) : this(_ => run(), can) { }

	public Command(Action<object> run, Func<bool> can = null)
	{
		_run = run;
		_can = can;
	}

	public event EventHandler CanExecuteChanged;
	public bool CanExecute(object parameter) => _can?.Invoke() ?? true;
	public void Execute(object parameter) => _run(parameter);
	public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
