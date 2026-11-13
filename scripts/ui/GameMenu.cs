using Godot;
using System;

/// <summary>
/// Menu do Esc dentro da partida: continuar, opções, controles,
/// reiniciar, sair para a sala ou sair do jogo.
///
/// **Não pausa.** A partida continua correndo, os bots continuam
/// atirando e o relógio continua descendo. É assim em jogo multijogador
/// — não existe pausa quando há outras pessoas na sala — e treinar com
/// a regra certa desde agora evita o hábito de abrir o menu para fugir
/// de uma luta.
///
/// O corpo do jogador fica parado enquanto o menu está aberto (quem
/// segura isso é o Player, lendo o UiState): o mouse é do menu, e uma
/// tecla esquecida não pode levar o personagem andando sozinho.
///
/// Sair ou reiniciar no meio é abandonar: não rende XP nem Presas. Por
/// isso os dois pedem confirmação.
/// </summary>
public partial class GameMenu : CanvasLayer
{
	public const string MenuScene = "res://scenes/menu/menu.tscn";

	private Control _root;
	private Control _main;
	private Control _sub;
	private Modal _confirm;
	private Button _continue;
	private bool _open;

	public override void _Ready()
	{
		Layer = 10;
		ProcessMode = ProcessModeEnum.Always;
		Build();
		_root.Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!@event.IsActionPressed("menu")) return;

		// No fim da partida quem manda é a tela de resultado.
		if (Match.Current?.Phase == MatchPhase.Ended) return;

		GetViewport().SetInputAsHandled();

		// Esc volta um nível: confirmação, depois subtela, depois fecha.
		if (_confirm != null && IsInstanceValid(_confirm)) _confirm.Close();
		else if (_sub != null) Back();
		else if (_open) Close();
		else Open();
	}

	private void Open()
	{
		_open = true;
		_root.Visible = true;
		_main.Visible = true;
		UiState.Push();
		Motion.FadeIn(_root, UiTokens.Fast);
		_continue.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Close()
	{
		if (!_open) return;

		Back();
		_open = false;
		_root.Visible = false;
		UiState.Pop();
	}

	// ------------------------------------------------------------------ ações

	private void Abandon(string title, Action then)
	{
		bool live = Match.Current != null && Match.Current.Phase != MatchPhase.Ended;
		if (!live)
		{
			then();
			return;
		}

		Label body = UiKit.Paragraph("A partida continua sem você e não rende XP nem Presas. Missões também não contam.");
		body.CustomMinimumSize = new Vector2(400, 0);

		_confirm = Modal.Open(_root, title, body,
			new Modal.Choice("Abandonar", ButtonKind.Danger, then),
			new Modal.Choice("Ficar", ButtonKind.Primary, null));
		_confirm.Closed += () => _confirm = null;
	}

	private void Restart()
	{
		if (Match.Current != null)
		{
			Match.Current.Restart();
			return;
		}

		// O contador de menus sobrevive à troca de cena; zerar antes
		// evita que a cena nova nasça achando que há menus abertos.
		UiState.Reset();
		GetTree().ReloadCurrentScene();
	}

	private void BackToRoom()
	{
		if (Match.Current != null)
		{
			Match.Current.Leave(ReturnTarget.Room);
			return;
		}

		UiState.Reset();
		RoomConfig.ReturnTo = RoomConfig.Active ? ReturnTarget.Room : ReturnTarget.RoomList;
		GetTree().ChangeSceneToFile(MenuScene);
	}

	// ------------------------------------------------------------------ subtelas

	private void ShowSub(string title, Control content)
	{
		Back();
		_main.Visible = false;

		var center = UiKit.Fill(new CenterContainer());
		PanelContainer panel = UiKit.Panel("Modal");
		panel.CustomMinimumSize = new Vector2(620, 0);
		center.AddChild(panel);

		VBoxContainer column = UiKit.Column(UiTokens.S3);
		panel.AddChild(column);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Text(title, "Title"));
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.Caption("Esc volta"));
		column.AddChild(head);
		column.AddChild(content);

		Button back = UiKit.Button("Voltar", ButtonKind.Secondary, Back);
		back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
		column.AddChild(back);

		_sub = center;
		_root.AddChild(center);
		Motion.FadeIn(panel, UiTokens.Fast);
		back.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Back()
	{
		if (_sub == null) return;
		_sub.QueueFree();
		_sub = null;
		_main.Visible = true;
		_continue.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Options()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S3);
		column.AddChild(OptionsScreen.AudioPanel());
		column.AddChild(OptionsScreen.MousePanel());
		ShowSub("Opções", column);
	}

	private void Controls()
	{
		ShowSub("Controles", ControlsTable.Build());
	}

	// ------------------------------------------------------------------ montagem

	private void Build()
	{
		_root = UiTheme.Apply(UiKit.Fill(new ColorRect { Color = new Color(UiTokens.Bg, 0.6f) }));
		AddChild(_root);

		var center = UiKit.Fill(new CenterContainer());
		_root.AddChild(center);
		_main = center;

		PanelContainer panel = UiKit.Panel("Modal");
		panel.CustomMinimumSize = new Vector2(380, 0);
		center.AddChild(panel);

		VBoxContainer stack = UiKit.Column(UiTokens.S2);
		panel.AddChild(stack);

		stack.AddChild(UiKit.Text("Partida", "Title"));
		HBoxContainer note = UiKit.Row(UiTokens.S1);
		note.AddChild(Icon.Make(IconKind.Warning, 14, UiTokens.Warning));
		note.AddChild(UiKit.Caption("a partida continua rodando", UiTokens.Warning));
		stack.AddChild(note);
		stack.AddChild(UiKit.Gap(UiTokens.S1));

		_continue = Item(stack, "Continuar", ButtonKind.Primary, Close);
		Item(stack, "Opções", ButtonKind.Secondary, Options);
		Item(stack, "Controles", ButtonKind.Secondary, Controls);

		stack.AddChild(UiKit.Divider());

		Item(stack, "Reiniciar partida", ButtonKind.Secondary, () => Abandon("Reiniciar a partida?", Restart));
		Item(stack, "Sair para a sala", ButtonKind.Secondary, () => Abandon("Sair para a sala?", BackToRoom));
		Item(stack, "Sair do jogo", ButtonKind.Danger, () => Abandon("Sair do jogo?", () => GetTree().Quit()));
	}

	private static Button Item(VBoxContainer stack, string text, ButtonKind kind, Action action)
	{
		Button button = UiKit.Button(text, kind, action);
		button.CustomMinimumSize = new Vector2(0, 44);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		stack.AddChild(button);
		return button;
	}
}
