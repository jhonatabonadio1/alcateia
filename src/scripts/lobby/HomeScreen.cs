using Godot;
using System;

/// <summary>
/// A tela principal do jogo: é onde se cai ao entrar (pedido do Jhon,
/// 26/11). No centro, três quadrados — os jeitos de jogar: Jogo rápido e
/// Arcade (ainda não existem: "em breve") e Salas (o jogo de hoje: criar
/// ou entrar numa sala) — e, por baixo deles, a faixa do Treino, que abre
/// o campo de treinamento (o Setor Zero), sozinho e offline.
///
/// Cada cartão é a foto de um mapa com o nome por cima; o que ainda não
/// existe fica apagado, com o selo, e clicar nele não leva a lugar
/// nenhum. Loja, inventário, missões, perfil e opções continuam no
/// header, como em todas as telas.
/// </summary>
public partial class HomeScreen : LobbyScreen
{
	/// <summary>A cena do campo de treinamento.</summary>
	public const string TrainingScene = "res://src/scenes/treino_setor_zero.tscn";

	/// <summary>O lado de cada quadrado, em pixels (a tela de referência tem 1080 de altura).</summary>
	public const int Square = 300;

	/// <summary>A altura da faixa do Treino.</summary>
	public const int Strip = 132;

	public override IconKind NavIcon => IconKind.Play;

	protected override void Build()
	{
		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		VBoxContainer column = UiKit.Column(UiTokens.S4);
		center.AddChild(column);

		VBoxContainer heading = UiKit.Column(UiTokens.S1);
		Label title = UiKit.Text("Como você quer jogar?", "Title");
		title.HorizontalAlignment = HorizontalAlignment.Center;
		heading.AddChild(title);
		Label hint = UiKit.Text("Escolha um modo. A loja, o inventário e as missões estão no alto.", "Muted");
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		heading.AddChild(hint);
		column.AddChild(heading);

		HBoxContainer squares = UiKit.Row(UiTokens.S4);
		squares.Alignment = BoxContainer.AlignmentMode.Center;
		column.AddChild(squares);

		squares.AddChild(Card("JogoRapido", "Jogo rápido", "Entre numa partida em segundos, sem escolher sala.",
			"res://src/assets/ui/mapas/neon_foto.png", IconKind.Go, UiTokens.TeamBlue, soon: true, null, new Vector2(Square, Square)));
		squares.AddChild(Card("Arcade", "Arcade", "Modos diferentes, regras malucas, partidas curtas.",
			"res://src/assets/ui/mapas/cidade_foto.png", IconKind.Star, UiTokens.WolfOrange, soon: true, null, new Vector2(Square, Square)));
		squares.AddChild(Card("Salas", "Salas", "Crie uma sala ou entre na de alguém: é o jogo de hoje.",
			"res://src/assets/ui/mapas/armazem_estendido_foto.png", IconKind.Rooms, UiTokens.Accent, soon: false,
			() => Shell?.GoHome(), new Vector2(Square, Square)));

		column.AddChild(Card("Treino", "Treino", "O campo de treinamento: armas, lobo e robôs de alvo, sozinho e sem pressa. No campo, a tecla dos conjuntos troca as armas quando quiser.",
			"res://src/assets/ui/mapas/setor_zero_foto.png", IconKind.Bot, UiTokens.Success, soon: false,
			StartTraining, new Vector2(Square * 3 + UiTokens.S4 * 2, Strip)));
	}

	/// <summary>Um cartão de modo: a foto, o escurecido, o ícone, o nome e uma
	/// linha do que é. <paramref name="soon"/>: ainda não existe.</summary>
	private static Control Card(string name, string title, string about, string cover, IconKind icon, Color accent, bool soon, Action go, Vector2 size)
	{
		PanelContainer card = UiKit.Panel("Surface1");
		card.Name = name;
		card.CustomMinimumSize = size;
		card.ClipContents = true;

		var art = new TextureRect
		{
			Texture = ResourceLoader.Exists(cover) ? GD.Load<Texture2D>(cover) : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
			// O que ainda não existe fica sem cor.
			Modulate = soon ? new Color(0.5f, 0.52f, 0.56f) : Colors.White,
		};
		card.AddChild(art);

		// O escurecido por cima da foto: o texto se lê, e some um pouco com o cursor em cima.
		var shade = new ColorRect { Color = new Color(0.02f, 0.04f, 0.08f, soon ? 0.72f : 0.56f), MouseFilter = MouseFilterEnum.Ignore };
		card.AddChild(shade);

		MarginContainer margin = UiKit.Margin(UiTokens.S3);
		margin.MouseFilter = MouseFilterEnum.Ignore;
		card.AddChild(margin);
		bool wide = size.X > size.Y * 1.5f;
		BoxContainer body = wide ? UiKit.Row(UiTokens.S3) : UiKit.Column(UiTokens.S2);
		body.MouseFilter = MouseFilterEnum.Ignore;
		margin.AddChild(body);

		Icon mark = Icon.Make(icon, wide ? 56 : 40, soon ? UiTokens.TextDisabled : accent);
		mark.SizeFlagsVertical = wide ? SizeFlags.ShrinkCenter : SizeFlags.ShrinkBegin;
		body.AddChild(mark);
		if (!wide) body.AddChild(UiKit.Spacer());

		VBoxContainer text = UiKit.Column(UiTokens.S1);
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = wide ? SizeFlags.ShrinkCenter : SizeFlags.ShrinkEnd;
		HBoxContainer line = UiKit.Row(UiTokens.S2);
		line.MouseFilter = MouseFilterEnum.Ignore;
		Label label = UiKit.Text(title, "Title", soon ? UiTokens.TextSecondary : UiTokens.TextPrimary);
		line.AddChild(label);
		if (soon)
		{
			PanelContainer chip = UiKit.Chip("Em breve", UiTokens.Warning);
			chip.Name = "EmBreve";
			chip.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			line.AddChild(chip);
		}
		text.AddChild(line);
		Label small = UiKit.Paragraph(about);
		small.MouseFilter = MouseFilterEnum.Ignore;
		text.AddChild(small);
		body.AddChild(text);

		// A linha da cor do modo, embaixo.
		var bar = new ColorRect
		{
			Color = soon ? UiTokens.Border : accent,
			CustomMinimumSize = new Vector2(0, 4),
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		card.AddChild(bar);

		// Quem recebe o clique: um botão liso por cima de tudo.
		var hit = new Button
		{
			Name = "Entrar",
			Flat = true,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = soon ? CursorShape.Forbidden : CursorShape.PointingHand,
			TooltipText = soon ? Loc.T("Em breve") : "",
		};
		hit.SetMeta("em_breve", soon);
		card.AddChild(hit);
		float rest = shade.Color.A;
		void Lift(bool on)
		{
			if (soon) return;
			Tween tween = card.CreateTween();
			tween.TweenProperty(shade, "color:a", on ? rest - 0.3f : rest, UiTokens.Fast);
			tween.Parallel().TweenProperty(bar, "custom_minimum_size:y", on ? 8f : 4f, UiTokens.Fast);
		}
		hit.MouseEntered += () => Lift(true);
		hit.MouseExited += () => Lift(false);
		hit.FocusEntered += () => Lift(true);
		hit.FocusExited += () => Lift(false);
		hit.Pressed += () =>
		{
			if (soon || go == null)
			{
				Sfx.Ui("ui_negado");
				return;
			}
			Sfx.Ui("ui_clique");
			go();
		};
		return card;
	}

	/// <summary>Abre o campo de treinamento: sem sala, sem partida, sem
	/// servidor. Volta-se pelo Esc ("Sair do treinamento").</summary>
	private void StartTraining()
	{
		UiState.Reset();
		RoomConfig.Active = false;
		RoomConfig.ReturnTo = ReturnTarget.None;
		GetTree().ChangeSceneToFile(TrainingScene);
	}
}
