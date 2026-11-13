using Godot;

/// <summary>
/// Menu de dificuldade. Abre no início e com Tab a qualquer momento.
///
/// Pausa a árvore e libera o cursor enquanto está aberto; devolve a
/// captura do mouse ao fechar. Peça descartável — vira menu de
/// verdade quando houver fluxo de partida.
/// </summary>
public partial class DifficultyMenu : CanvasLayer
{
	private Control _root;
	private bool _open;

	public override void _Ready()
	{
		// Precisa continuar processando com a árvore pausada, senão o
		// próprio menu congela junto com o jogo.
		ProcessMode = ProcessModeEnum.Always;

		Build();
		Open();
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("menu")) return;

		if (_open) Close();
		else Open();

		GetViewport().SetInputAsHandled();
	}

	private void Build()
	{
		_root = new Control
		{
			AnchorRight = 1f,
			AnchorBottom = 1f,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		AddChild(_root);

		var backdrop = new ColorRect
		{
			AnchorRight = 1f,
			AnchorBottom = 1f,
			Color = new Color(0.04f, 0.04f, 0.06f, 0.88f)
		};
		_root.AddChild(backdrop);

		var column = new VBoxContainer
		{
			AnchorLeft = 0.5f, AnchorRight = 0.5f,
			AnchorTop = 0.5f, AnchorBottom = 0.5f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			CustomMinimumSize = new Vector2(340, 0)
		};
		column.AddThemeConstantOverride("separation", 10);
		_root.AddChild(column);

		var title = new Label
		{
			Text = "NÍVEL DOS BOTS",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 26);
		column.AddChild(title);

		var hint = new Label
		{
			Text = "Tab abre e fecha este menu",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(1, 1, 1, 0.45f)
		};
		hint.AddThemeFontSizeOverride("font_size", 12);
		column.AddChild(hint);

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

		AddOption(column, BotLevel.Easy);
		AddOption(column, BotLevel.Medium);
		AddOption(column, BotLevel.Hard);
	}

	private void AddOption(Node parent, BotLevel level)
	{
		var button = new Button
		{
			Text = Difficulty.Label(level),
			CustomMinimumSize = new Vector2(0, 42)
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		button.Pressed += () => Choose(level);
		parent.AddChild(button);

		var note = new Label
		{
			Text = Difficulty.Summary(level),
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = new Color(1, 1, 1, 0.5f)
		};
		note.AddThemeFontSizeOverride("font_size", 12);
		parent.AddChild(note);
	}

	private void Choose(BotLevel level)
	{
		Difficulty.Set(level);
		Close();
	}

	private void Open()
	{
		_open = true;
		_root.Visible = true;

		GetTree().Paused = true;
		UiState.Push();
	}

	private void Close()
	{
		_open = false;
		_root.Visible = false;

		GetTree().Paused = false;
		UiState.Pop();
	}
}
