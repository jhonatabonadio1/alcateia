using Godot;
using System.Collections.Generic;

/// <summary>
/// O header persistente: a única parte da interface que nunca sai da
/// tela fora da partida. Uma faixa só, com divisórias verticais (a
/// sensação de cockpit), em três zonas:
///
/// - identidade à esquerda — insígnia, nome, patente e barra de XP, um
///   único bloco clicável que abre o perfil;
/// - navegação no meio — ícones das telas, com um « que recolhe a
///   fileira (numa interface densa, deixar esconder é o que impede a
///   tela de sufocar);
/// - moedas à direita — Presas e Sangue, com largura fixa e contagem
///   animada a cada mudança.
///
/// WP não aparece aqui: WP é da partida, não do lobby.
/// </summary>
public partial class LobbyHeader : PanelContainer
{
	private readonly record struct Nav(IconKind Icon, string Label, System.Action Go);

	private RankBadge _badge;
	private Label _name;
	private Label _rank;
	private ProgressBar _xp;
	private Label _xpText;
	private CurrencyLabel _presas;
	private CurrencyLabel _sangue;
	private HBoxContainer _navRow;
	private Button _collapse;
	private readonly Dictionary<IconKind, Button> _navButtons = new();
	private Label _missionBadge;
	private IconKind _active;
	private bool _collapsed;

	public override void _Ready()
	{
		ThemeTypeVariation = "Header";
		CustomMinimumSize = new Vector2(0, UiTokens.HeaderHeight);

		HBoxContainer row = UiKit.Row(0);
		AddChild(row);

		row.AddChild(Logo());
		row.AddChild(new VSeparator());
		row.AddChild(Identity());
		row.AddChild(new VSeparator());
		row.AddChild(UiKit.Spacer());
		row.AddChild(Navigation());
		row.AddChild(new VSeparator());
		row.AddChild(Currencies());
		row.AddChild(new VSeparator());
		row.AddChild(ExitButton());

		Profile.Changed += OnProfileChanged;
		OnProfileChanged();
	}

	public override void _ExitTree() => Profile.Changed -= OnProfileChanged;

	// ------------------------------------------------------------------ zonas

	private static Control Logo()
	{
		MarginContainer margin = UiKit.Margin(0, 0, UiTokens.S3, 0);
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		margin.AddChild(row);

		row.AddChild(Icon.Make(IconKind.Wolf, 28, UiTokens.Accent));
		Label word = UiKit.Text("ALCATEIA", "Subtitle");
		word.AddThemeFontOverride("font", UiFonts.Display);
		word.AddThemeFontSizeOverride("font_size", 26);
		word.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(word);
		return margin;
	}

	private Control Identity()
	{
		// Patente, nome e XP são um bloco só, clicável — não espalhe.
		var cluster = new Button
		{
			ThemeTypeVariation = "TertiaryButton",
			CustomMinimumSize = new Vector2(300, UiTokens.HeaderHeight - 8),
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = "Abrir o perfil"
		};
		cluster.Pressed += () => LobbyShell.Instance?.GoProfile();
		Sfx.Wire(cluster);

		MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S3, UiTokens.S1, UiTokens.S3, UiTokens.S1));
		margin.MouseFilter = MouseFilterEnum.Ignore;
		cluster.AddChild(margin);

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		row.MouseFilter = MouseFilterEnum.Ignore;
		margin.AddChild(row);

		_badge = RankBadge.Make(Profile.Rank, 40);
		_badge.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(_badge);

		VBoxContainer text = UiKit.Column(2);
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(text);

		_name = UiKit.Truncated("", "Subtitle");
		_name.AddThemeFontSizeOverride("font_size", 17);
		text.AddChild(_name);

		HBoxContainer line = UiKit.Row(UiTokens.S2);
		line.MouseFilter = MouseFilterEnum.Ignore;
		_rank = UiKit.Caption("");
		line.AddChild(_rank);
		line.AddChild(UiKit.Spacer());
		_xpText = UiKit.Text("", "Caption");
		_xpText.AddThemeFontOverride("font", UiFonts.Numeric);
		line.AddChild(_xpText);
		text.AddChild(line);

		_xp = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 4),
			ShowPercentage = false,
			MaxValue = 1,
			Step = 0.001,
			MouseFilter = MouseFilterEnum.Ignore
		};
		text.AddChild(_xp);

		return cluster;
	}

	private Control Navigation()
	{
		HBoxContainer outer = UiKit.Row(0);

		_navRow = UiKit.Row(0);
		outer.AddChild(_navRow);

		var items = new[]
		{
			new Nav(IconKind.Rooms, "Salas", () => LobbyShell.Instance?.GoHome()),
			new Nav(IconKind.Shop, "Loja", () => LobbyShell.Instance?.GoShop()),
			new Nav(IconKind.Inventory, "Inventário", () => LobbyShell.Instance?.GoInventory()),
			new Nav(IconKind.Missions, "Missões", () => LobbyShell.Instance?.GoMissions()),
			new Nav(IconKind.Profile, "Perfil", () => LobbyShell.Instance?.GoProfile()),
			new Nav(IconKind.Options, "Opções", () => LobbyShell.Instance?.GoOptions()),
		};

		foreach (Nav nav in items)
		{
			Button button = NavButton(nav);
			_navButtons[nav.Icon] = button;
			_navRow.AddChild(button);
		}

		_collapse = UiKit.IconButton(IconKind.Collapse, "Recolher a barra", ToggleCollapse, 18);
		_collapse.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		outer.AddChild(_collapse);
		return outer;
	}

	private Button NavButton(Nav nav)
	{
		var button = new Button
		{
			ThemeTypeVariation = "TertiaryButton",
			// Largura pelo rótulo: "INVENTÁRIO" não cabe no mesmo botão
			// que "LOJA", e rótulo que vaza encavala no vizinho.
			CustomMinimumSize = new Vector2(
				Mathf.Max(72, UiFonts.Label.GetStringSize(nav.Label.ToUpperInvariant(), HorizontalAlignment.Left, -1, UiTokens.Caption).X + UiTokens.S4),
				UiTokens.HeaderHeight - 8),
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = nav.Label
		};
		button.Pressed += nav.Go;
		Sfx.Wire(button);

		VBoxContainer stack = UiKit.Fill(UiKit.Column(2));
		stack.Alignment = BoxContainer.AlignmentMode.Center;
		stack.MouseFilter = MouseFilterEnum.Ignore;
		stack.Name = "Column";
		button.AddChild(stack);

		Icon icon = Icon.Make(nav.Icon, 22, UiTokens.TextSecondary);
		icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		icon.Name = "Icon";
		stack.AddChild(icon);

		Label label = UiKit.Caption(nav.Label);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.Name = "Label";
		stack.AddChild(label);

		if (nav.Icon == IconKind.Missions)
		{
			// Bolinha com o número de missões prontas para resgatar.
			_missionBadge = UiKit.Text("", "Caption", UiTokens.AccentText);
			_missionBadge.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTokens.Accent, default, 0, 8, 5, 0));
			_missionBadge.Position = new Vector2(46, 4);
			_missionBadge.Visible = false;
			button.AddChild(_missionBadge);
		}

		return button;
	}

	private Control Currencies()
	{
		MarginContainer margin = UiKit.Margin(UiTokens.S3, 0, UiTokens.S4, 0);
		VBoxContainer column = UiKit.Column(2);
		column.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		margin.AddChild(column);

		_presas = CurrencyLabel.Make(Currency.Presas, Profile.Presas, 92);
		_sangue = CurrencyLabel.Make(Currency.Sangue, Profile.Sangue, 92);
		column.AddChild(_presas);
		column.AddChild(_sangue);
		return margin;
	}

	/// <summary>
	/// Sair do jogo, no canto direito — onde a mão vai procurar. Pede
	/// confirmação: um clique errado no canto da tela não pode fechar o
	/// jogo. O perfil é salvo a cada mudança, então sair não perde nada.
	/// </summary>
	private Control ExitButton()
	{
		var button = new Button
		{
			ThemeTypeVariation = "TertiaryButton",
			CustomMinimumSize = new Vector2(72, UiTokens.HeaderHeight - 8),
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = "Sair do jogo"
		};
		button.Pressed += ConfirmExit;
		Sfx.Wire(button);

		VBoxContainer stack = UiKit.Fill(UiKit.Column(2));
		stack.Alignment = BoxContainer.AlignmentMode.Center;
		stack.MouseFilter = MouseFilterEnum.Ignore;
		button.AddChild(stack);

		Icon icon = Icon.Make(IconKind.Power, 22, UiTokens.TextSecondary);
		icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		stack.AddChild(icon);

		Label label = UiKit.Caption("Sair");
		label.HorizontalAlignment = HorizontalAlignment.Center;
		stack.AddChild(label);

		// Vermelho só no foco do mouse: em repouso é discreto como os outros.
		button.MouseEntered += () =>
		{
			icon.Tint = UiTokens.Danger;
			label.AddThemeColorOverride("font_color", UiTokens.Danger);
		};
		button.MouseExited += () =>
		{
			icon.Tint = UiTokens.TextSecondary;
			label.RemoveThemeColorOverride("font_color");
		};

		MarginContainer margin = UiKit.Margin(UiTokens.S1, 0, UiTokens.S2, 0);
		margin.AddChild(button);
		return margin;
	}

	private void ConfirmExit()
	{
		Label body = UiKit.Paragraph("Seu perfil, inventário e missões ficam salvos neste computador.");
		body.CustomMinimumSize = new Vector2(400, 0);

		LobbyShell.Instance?.Modal("Sair do jogo?", body,
			new Modal.Choice("Sair", ButtonKind.Danger, () =>
			{
				Profile.Save();
				Settings.Save();
				GetTree().Quit();
			}),
			new Modal.Choice("Ficar", ButtonKind.Primary, null));
	}

	// ------------------------------------------------------------------ estado

	public void SetActive(IconKind icon)
	{
		_active = icon;
		foreach (var (kind, button) in _navButtons)
		{
			bool on = kind == icon;
			Color color = on ? UiTokens.Accent : UiTokens.TextSecondary;
			button.GetNode<Icon>("Column/Icon").Tint = color;
			button.GetNode<Label>("Column/Label").AddThemeColorOverride("font_color", on ? UiTokens.TextPrimary : UiTokens.TextSecondary);

			var underline = UiTheme.Box(on ? new Color(1, 1, 1, 0.03f) : Colors.Transparent, UiTokens.Accent, 0, 0, 0, 0);
			underline.BorderWidthBottom = on ? 2 : 0;
			button.AddThemeStyleboxOverride("normal", underline);
		}
	}

	private void ToggleCollapse()
	{
		_collapsed = !_collapsed;
		foreach (var (kind, button) in _navButtons)
			button.Visible = !_collapsed || kind == _active;

		_collapse.TooltipText = _collapsed ? "Expandir a barra" : "Recolher a barra";
		_collapse.GetChild<Icon>(0).Kind = _collapsed ? IconKind.Expand : IconKind.Collapse;
	}

	private void OnProfileChanged()
	{
		if (_name == null) return;

		int rank = Profile.Rank;
		_badge.Rank = rank;
		_name.Text = string.IsNullOrEmpty(Profile.Title) ? Profile.PlayerName : $"{Profile.PlayerName}";
		_rank.Text = Ranks.NameOf(rank).ToUpperInvariant();
		_rank.AddThemeColorOverride("font_color", Ranks.ColorOf(rank));

		long into = Profile.Xp - Ranks.XpFor(rank);
		long span = Ranks.XpFor(rank + 1) - Ranks.XpFor(rank);
		_xp.Value = Ranks.Progress(Profile.Xp);
		_xpText.Text = rank >= Ranks.Count - 1 ? "MÁX" : $"{UiTokens.Thousands(into)} / {UiTokens.Thousands(span)} XP";

		_presas.SetValue(Profile.Presas);
		_sangue.SetValue(Profile.Sangue);

		int claimable = Profile.Missions.ClaimableCount;
		if (_missionBadge != null)
		{
			_missionBadge.Visible = claimable > 0;
			_missionBadge.Text = claimable.ToString();
		}
	}

	/// <summary>Atualiza o selo de missões (chamado após resgatar).</summary>
	public void RefreshBadges() => OnProfileChanged();
}
