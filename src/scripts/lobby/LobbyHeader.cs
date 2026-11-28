using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// O header persistente: a única parte da interface que nunca sai da tela
/// fora da partida. Refeito em 27/11 (Jhon: "refaça a HUD dos menus usando
/// Valorant como inspiração; ao centro do header terá um JOGAR"):
///
/// - **no meio, a aba JOGAR**, uma placa de papel que desce do topo: é a
///   peça clara da tela, o que o jogador veio fazer. De um lado e do outro,
///   as abas de texto — Início e Inventário, Loja e Missões — com um
///   losango embaixo da que está aberta;
/// - **à esquerda**, a marca;
/// - **à direita**, as moedas (largura fixa, contagem animada), a
///   identidade (foto, nome, nível e XP: um bloco só, que abre o perfil) e
///   três ícones: opções, social e sair.
///
/// WP não aparece aqui: WP é da partida, não do lobby.
/// </summary>
public partial class LobbyHeader : PanelContainer
{
	private PhotoTile _photo;
	private LevelChip _chip;
	private Label _name;
	private ProgressBar _xp;
	private CurrencyLabel _presas;
	private CurrencyLabel _sangue;
	private readonly Dictionary<IconKind, NavTab> _tabs = new();
	private PlayTab _play;
	private Button _identity;
	private Label _socialBadge;

	/// <summary>Largura da aba JOGAR.</summary>
	private const int PlayWidth = 252;

	public override void _Ready()
	{
		ThemeTypeVariation = "Header";
		CustomMinimumSize = new Vector2(0, UiTokens.HeaderHeight);

		// Três zonas ancoradas: a do meio fica no meio da tela, tenham as
		// dos lados a largura que tiverem.
		var bar = new Control { CustomMinimumSize = new Vector2(0, UiTokens.HeaderHeight), MouseFilter = MouseFilterEnum.Ignore };
		AddChild(bar);

		Control left = Logo();
		left.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
		bar.AddChild(left);

		// A aba JOGAR no centro exato da tela; as abas de texto encostam nela
		// pelos dois lados (as da esquerda são mais largas que as da direita:
		// numa fileira só, a placa sairia do meio).
		HBoxContainer before = UiKit.Row(0), after = UiKit.Row(0);
		Navigation(before, after);
		foreach ((Control piece, GrowDirection grow, float from) in new (Control, GrowDirection, float)[]
			{ (before, GrowDirection.Begin, -PlayWidth / 2f), (_play, GrowDirection.Both, 0f), (after, GrowDirection.End, PlayWidth / 2f) })
		{
			piece.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
			piece.GrowHorizontal = grow;
			piece.OffsetLeft = piece.OffsetRight = from;
			piece.OffsetTop = 0;
			bar.AddChild(piece);
		}

		HBoxContainer right = UiKit.Row(UiTokens.S2);
		right.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
		right.GrowHorizontal = GrowDirection.Begin;
		right.AddChild(Currencies());
		right.AddChild(new VSeparator());
		right.AddChild(Identity());
		right.AddChild(new VSeparator());
		right.AddChild(Tools());
		bar.AddChild(right);

		Profile.Changed += OnProfileChanged;
		Social.Changed += OnSocialChanged;
		LobbyLink.Instance.Changed += OnLobbyChanged;
		OnProfileChanged();
		OnSocialChanged();
	}

	public override void _ExitTree()
	{
		Profile.Changed -= OnProfileChanged;
		Social.Changed -= OnSocialChanged;
		if (LobbyLink.Instance is LobbyLink link && IsInstanceValid(link)) link.Changed -= OnLobbyChanged;
	}

	// ------------------------------------------------------------------ zonas

	private static Control Logo()
	{
		MarginContainer margin = UiKit.Margin(UiTokens.S2, 0, 0, 0);
		margin.MouseFilter = MouseFilterEnum.Ignore;
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		margin.AddChild(row);

		row.AddChild(Icon.Make(IconKind.Wolf, 28, UiTokens.Accent));
		// WOLF claro, LEAGUE na cor da marca: duas palavras, um nome só.
		HBoxContainer name = UiKit.Row(0);
		foreach (var (part, color) in new[] { ("WOLF", UiTokens.TextPrimary), ("LEAGUE", UiTokens.Accent) })
		{
			Label word = UiKit.Text(part, "Subtitle", color);
			word.AddThemeFontOverride("font", UiFonts.Brand);
			word.AddThemeFontSizeOverride("font_size", 26);
			word.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			name.AddChild(word);
		}
		row.AddChild(name);
		return margin;
	}

	private void Navigation(HBoxContainer before, HBoxContainer after)
	{
		void Tab(HBoxContainer row, IconKind kind, string text, Action go)
		{
			NavTab tab = NavTab.Make(text, go);
			_tabs[kind] = tab;
			row.AddChild(tab);
		}

		Tab(before, IconKind.Home, "Início", () => LobbyShell.Instance?.GoHome());
		Tab(before, IconKind.Inventory, "Inventário", () => LobbyShell.Instance?.GoInventory());

		_play = new PlayTab { CustomMinimumSize = new Vector2(PlayWidth, UiTokens.HeaderHeight) };
		_play.Pressed += () => LobbyShell.Instance?.GoPlay();
		Sfx.Wire(_play);

		Tab(after, IconKind.Shop, "Loja", () => LobbyShell.Instance?.GoShop());
		Tab(after, IconKind.Missions, "Missões", () => LobbyShell.Instance?.GoMissions());
	}

	private Control Identity()
	{
		// Foto, nome, nível e XP são um bloco só, clicável — não espalhe.
		_identity = new Button
		{
			ThemeTypeVariation = "TertiaryButton",
			CustomMinimumSize = new Vector2(220, UiTokens.HeaderHeight - 8),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = "Abrir o perfil"
		};
		_identity.Pressed += () => LobbyShell.Instance?.GoProfile();
		Sfx.Wire(_identity);

		MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S2, UiTokens.S1, UiTokens.S2, UiTokens.S1));
		margin.MouseFilter = MouseFilterEnum.Ignore;
		_identity.AddChild(margin);

		HBoxContainer row = UiKit.Row(UiTokens.S2 + 2);
		row.MouseFilter = MouseFilterEnum.Ignore;
		margin.AddChild(row);

		_photo = PhotoTile.Make(Avatars.Selected, 44);
		row.AddChild(_photo);

		VBoxContainer text = UiKit.Column(UiTokens.S1);
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(text);

		HBoxContainer top = UiKit.Row(UiTokens.S2);
		top.MouseFilter = MouseFilterEnum.Ignore;
		_name = UiKit.Truncated("", "Subtitle");
		_name.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		_name.AddThemeFontSizeOverride("font_size", 20);
		top.AddChild(_name);
		_chip = LevelChip.Make(Profile.Rank, 18);
		_chip.MouseFilter = MouseFilterEnum.Ignore;
		top.AddChild(_chip);
		text.AddChild(top);

		_xp = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 3),
			ShowPercentage = false,
			MaxValue = 1,
			Step = 0.001,
			MouseFilter = MouseFilterEnum.Ignore
		};
		// Um fio reto, sem pílula: é um medidor, não um controle.
		_xp.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(UiTokens.Paper, 0.14f) });
		_xp.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = UiTokens.Accent });
		text.AddChild(_xp);

		return _identity;
	}

	private Control Currencies()
	{
		VBoxContainer column = UiKit.Column(2);
		column.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_presas = CurrencyLabel.Make(Currency.Presas, Profile.Presas, 84);
		_sangue = CurrencyLabel.Make(Currency.Sangue, Profile.Sangue, 84);
		column.AddChild(_presas);
		column.AddChild(_sangue);
		return column;
	}

	/// <summary>Opções, social e sair: ícones, no canto onde a mão procura.</summary>
	private Control Tools()
	{
		MarginContainer margin = UiKit.Margin(0, 0, UiTokens.S2, 0);
		margin.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		HBoxContainer row = UiKit.Row(0);
		margin.AddChild(row);

		_options = ToolButton(IconKind.Options, "Opções", () => LobbyShell.Instance?.GoOptions());
		row.AddChild(_options);

		Button social = ToolButton(IconKind.Users, "Amigos", () => LobbyShell.Instance?.ToggleSocial());
		// O número de pedidos e convites à espera.
		_socialBadge = UiKit.Text("", "Caption", UiTokens.AccentText);
		_socialBadge.AddThemeStyleboxOverride("normal", Badge());
		_socialBadge.Position = new Vector2(26, 8);
		_socialBadge.Visible = false;
		social.AddChild(_socialBadge);
		row.AddChild(social);

		// Sair pede confirmação: um clique errado no canto da tela não pode
		// fechar o jogo.
		Button exit = ToolButton(IconKind.Power, "Sair do jogo", ConfirmExit, UiTokens.Danger);
		row.AddChild(exit);
		return margin;
	}

	private Button _options;

	private static StyleBoxFlat Badge()
	{
		var box = new StyleBoxFlat { BgColor = UiTokens.Accent };
		box.ContentMarginLeft = box.ContentMarginRight = 5;
		return box;
	}

	private static Button ToolButton(IconKind kind, string tooltip, Action pressed, Color? hot = null)
	{
		var button = new Button
		{
			ThemeTypeVariation = "IconButton",
			CustomMinimumSize = new Vector2(44, 44),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = tooltip
		};
		button.Pressed += pressed;
		Sfx.Wire(button);

		Icon icon = Icon.Make(kind, 22, UiTokens.TextSecondary);
		icon.SetAnchorsPreset(LayoutPreset.TopLeft);
		icon.Position = new Vector2(11, 11);
		icon.Size = new Vector2(22, 22);
		icon.Name = "Icon";
		button.AddChild(icon);
		button.MouseEntered += () => icon.Tint = hot ?? UiTokens.TextPrimary;
		button.MouseExited += () => icon.Tint = button.HasMeta("on") ? UiTokens.Accent : UiTokens.TextSecondary;
		return button;
	}

	private void ConfirmExit()
	{
		Label body = UiKit.Paragraph("Seu perfil, inventário e missões ficam salvos na sua conta.");
		body.CustomMinimumSize = new Vector2(400, 0);

		LobbyShell.Instance?.Modal("Sair do jogo?", body,
			new Modal.Choice("Trocar de conta", ButtonKind.Secondary, () => LobbyShell.Instance?.Logout()),
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
		foreach (var (kind, tab) in _tabs) tab.Active = kind == icon;
		// As salas e a sala são a aba JOGAR.
		_play.Active = icon is IconKind.Play or IconKind.Rooms;

		bool options = icon == IconKind.Options;
		if (options) _options.SetMeta("on", true); else _options.RemoveMeta("on");
		_options.GetNode<Icon>("Icon").Tint = options ? UiTokens.Accent : UiTokens.TextSecondary;

		var on = UiTheme.Box(new Color(UiTokens.Paper, 0.05f), UiTokens.Accent, 0, 0, 0, 0);
		on.BorderWidthBottom = 2;
		if (icon == IconKind.Profile) _identity.AddThemeStyleboxOverride("normal", on);
		else _identity.RemoveThemeStyleboxOverride("normal");
	}

	private void OnLobbyChanged()
	{
		if (!IsInsideTree()) return;
		_play.Room = LobbyLink.InRoom ? LobbyLink.Instance.Room?["id"]?.GetValue<int>() ?? 0 : 0;
	}

	private void OnSocialChanged()
	{
		if (_socialBadge == null) return;
		int waiting = Social.Requests.Count + Social.Invites.Count;
		_socialBadge.Visible = waiting > 0;
		_socialBadge.Text = waiting.ToString();
	}

	private void OnProfileChanged()
	{
		if (_name == null) return;

		int rank = Profile.Rank;
		_photo.Photo = Avatars.Selected;
		_photo.Edge = Ranks.ColorOf(rank);
		_chip.Rank = rank;
		_name.Text = Profile.PlayerName;

		long into = Profile.RankXp - Ranks.XpFor(rank);
		long span = Ranks.XpFor(rank + 1) - Ranks.XpFor(rank);
		_xp.Value = Ranks.Progress(Profile.RankXp);
		_identity.TooltipText = rank >= Ranks.Count - 1
			? Loc.T("Abrir o perfil")
			: Loc.F("Abrir o perfil · {0} / {1} XP", UiTokens.Thousands(into), UiTokens.Thousands(span));

		_presas.SetValue(Profile.Presas);
		_sangue.SetValue(Profile.Sangue);
		// Carteira e patente são da conta: sem ela, é tudo zero.
		string hint = Profile.HasAccount ? "" : "Entre na sua conta para ter carteira, itens e patente";
		_presas.TooltipText = hint;
		_sangue.TooltipText = hint;

		if (_tabs.TryGetValue(IconKind.Missions, out NavTab missions))
			missions.Badge = Profile.Missions.ClaimableCount;
	}

	/// <summary>Atualiza o selo de missões (chamado após resgatar).</summary>
	public void RefreshBadges() => OnProfileChanged();
}
