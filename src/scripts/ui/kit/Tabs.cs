using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Fileira de abas: texto em caixa alta, sublinhado de destaque na ativa.
/// O guia pede dois níveis no inventário e na loja (natureza do item,
/// depois subtipo); três seria demais.
/// </summary>
public partial class Tabs : HBoxContainer
{
	public event Action<int> Changed;

	public int Selected { get; private set; }

	private readonly List<Button> _buttons = new();
	private readonly ButtonGroup _group = new();

	public static Tabs Make(string[] labels, int selected = 0, Action<int> changed = null)
	{
		var tabs = new Tabs();
		tabs.AddThemeConstantOverride("separation", 0);
		if (changed != null) tabs.Changed += changed;

		for (int i = 0; i < labels.Length; i++)
		{
			int index = i;
			Button button = UiKit.Button(labels[i], ButtonKind.Tab);
			button.ToggleMode = true;
			button.ButtonGroup = tabs._group;
			button.Pressed += () => tabs.Select(index);
			tabs._buttons.Add(button);
			tabs.AddChild(button);
		}

		tabs.Select(selected, notify: false);
		return tabs;
	}

	public void Select(int index, bool notify = true)
	{
		if (_buttons.Count == 0) return;

		index = Mathf.Clamp(index, 0, _buttons.Count - 1);
		Selected = index;
		_buttons[index].SetPressedNoSignal(true);

		if (notify) Changed?.Invoke(index);
	}

	/// <summary>Número pequeno ao lado do nome da aba (ex.: missões
	/// prontas para resgatar).</summary>
	public void SetBadge(int index, string text)
	{
		if (index < 0 || index >= _buttons.Count) return;
		string baseText = _buttons[index].Text.Split("  ")[0];
		_buttons[index].Text = string.IsNullOrEmpty(text) ? baseText : $"{baseText}  {text}";
	}
}
