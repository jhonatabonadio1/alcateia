using Godot;
using System;
using System.Linq;

/// <summary>
/// A tela de fim de partida. Uma função só: fazer o jogador querer jogar
/// de novo. Três tempos (docs/UI.md, "Fim de partida"):
///
/// 1. **Veredito** — Vitória, Derrota ou Empate, grande, com o placar
///    final. Um segundo e meio sozinho na tela.
/// 2. **Placar completo**, com o MVP no topo.
/// 3. **Recompensa animada** — as linhas de ganho entrando uma a uma, a
///    barra de patente enchendo, as Presas contando para cima e, se
///    houver, o aviso de promoção. É o momento mais importante da tela:
///    não pula nem acelera.
///
/// Rodapé: Jogar de novo (primário, já sob o cursor), Voltar à sala, Sair
/// para o lobby.
///
/// Só lê o que o <see cref="Match"/> já pagou em <see cref="Match.Reward"/>:
/// a conta é feita uma vez, antes desta tela existir.
/// </summary>
public partial class ResultScreen : CanvasLayer
{
	/// <summary>Quanto tempo o veredito fica sozinho.</summary>
	[Export] public float VerdictAlone = 1.5f;

	/// <summary>Intervalo entre uma linha de recompensa e a próxima.</summary>
	[Export] public float LineStep = 0.28f;

	/// <summary>Duração da barra de patente enchendo.</summary>
	[Export] public float XpFill = 1.8f;

	private Match _match;
	private int _localTeam;

	private Control _root;
	private Control _hero;
	private Control _page;
	private Control _overlay;
	private Button _again;

	private RankBadge _badge;
	private Label _rankName;
	private ProgressBar _xpBar;
	private Label _xpText;
	private Label _xpNext;
	private Label _xpGain;
	private VBoxContainer _lines;
	private CurrencyLabel _presas;
	private Label _presasGain;
	private VBoxContainer _missions;
	private int _shownRank = -1;

	public static ResultScreen Open(Node host, Match match, int localTeam)
	{
		var screen = new ResultScreen { _match = match, _localTeam = localTeam };
		host.AddChild(screen);
		return screen;
	}

	public override void _Ready()
	{
		// Acima do menu de conjuntos (9) e do Esc (10).
		Layer = 11;
		UiState.Push();

		_root = UiTheme.Apply(UiKit.Fill(new Control()));
		AddChild(_root);

		var dim = UiKit.Fill(new ColorRect { Color = new Color(UiTokens.Bg, 0.94f), MouseFilter = Control.MouseFilterEnum.Stop });
		_root.AddChild(dim);

		_hero = BuildHero();
		_root.AddChild(_hero);
		_page = BuildPage();
		_page.Visible = false;
		_root.AddChild(_page);

		_overlay = UiKit.Fill(new Control { MouseFilter = Control.MouseFilterEnum.Ignore });
		_root.AddChild(_overlay);

		Motion.FadeIn(_hero, UiTokens.Normal);
		Motion.Pop(_hero.GetChild<Control>(0), 1.08f, 0.35f);

		Tween flow = CreateTween();
		flow.TweenInterval(VerdictAlone);
		flow.TweenProperty(_hero, "modulate:a", 0f, 0.25f);
		flow.TweenCallback(Callable.From(ShowPage));
	}

	// Sem Pop ao sair: esta tela só some com a troca de cena, e quem
	// troca (Match.Restart, Match.Leave) já zera o UiState antes.

	// ================================================================== tempos

	private void ShowPage()
	{
		_hero.Visible = false;
		_page.Visible = true;
		Motion.FadeIn(_page, UiTokens.Normal);

		// O primário nasce sob o cursor: jogar de novo é um clique, sem
		// procurar o botão.
		_again.GrabFocus();
		Callable.From(WarpToAgain).CallDeferred();

		PlayRewards();
	}

	private void WarpToAgain()
	{
		if (!IsInstanceValid(_again)) return;
		Rect2 rect = _again.GetGlobalRect();
		GetViewport().WarpMouse(rect.GetCenter());
	}

	private void PlayRewards()
	{
		RewardSummary reward = _match.Reward;
		if (reward == null) return;

		ShowXp(reward.XpBefore);
		_presas.SetValue(reward.PresasBefore, animate: false);

		Tween t = CreateTween();
		t.TweenInterval(0.35f);

		foreach (RewardLine line in reward.Lines)
		{
			Control row = RewardRow(line);
			row.Modulate = new Color(1, 1, 1, 0);
			_lines.AddChild(row);
			t.TweenCallback(Callable.From(() =>
			{
				Motion.SlideIn(row, new Vector2(-16, 0), UiTokens.Normal);
				Sfx.Ui("ui_passar", -6f);
			}));
			t.TweenInterval(LineStep);
		}

		long xpGain = reward.XpAfter - reward.XpBefore;
		long presasGain = reward.PresasAfter - reward.PresasBefore;
		t.TweenCallback(Callable.From(() =>
		{
			_xpGain.Text = $"+{UiTokens.Thousands(xpGain)} XP";
			_presasGain.Text = $"+{UiTokens.Thousands(presasGain)}";
			Motion.Pop(_xpGain, 1.2f);
			Motion.Pop(_presasGain, 1.2f);
		}));

		// Barra e Presas juntas, no mesmo tempo: são a mesma notícia.
		t.TweenMethod(Callable.From<double>(x => ShowXp((long)x)), (double)reward.XpBefore, (double)reward.XpAfter, XpFill)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.Parallel().TweenMethod(Callable.From<double>(x => _presas.SetValue((long)x, animate: false)),
				(double)reward.PresasBefore, (double)reward.PresasAfter, XpFill)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		t.TweenCallback(Callable.From(() =>
		{
			if (reward.MissionsCompleted.Count > 0)
			{
				_missions.Visible = true;
				Motion.FadeIn(_missions, UiTokens.Normal);
			}
			Promote(0);
		}));
	}

	/// <summary>Uma promoção de cada vez, a próxima quando a anterior
	/// fecha.</summary>
	private void Promote(int index)
	{
		var promotions = _match.Reward.Promotions;
		if (index >= promotions.Count)
		{
			_again.GrabFocus();
			return;
		}

		PromotionOverlay.Open(_overlay, promotions[index], () => Promote(index + 1));
	}

	private void ShowXp(long xp)
	{
		int rank = Ranks.RankFor(xp);
		if (rank != _shownRank)
		{
			bool promoted = _shownRank >= 0 && rank > _shownRank;
			_shownRank = rank;
			_badge.Rank = rank;
			_rankName.Text = Ranks.NameOf(rank);
			_rankName.AddThemeColorOverride("font_color", Ranks.ColorOf(rank));
			if (promoted) Motion.Pop(_badge, 1.3f, 0.3f);
		}

		_xpBar.Value = Ranks.Progress(xp);

		if (rank >= Ranks.Count - 1)
		{
			_xpText.Text = $"{UiTokens.Thousands(xp)} XP";
			_xpNext.Text = "patente máxima";
			return;
		}

		long from = Ranks.XpFor(rank);
		long to = Ranks.XpFor(rank + 1);
		_xpText.Text = $"{UiTokens.Thousands(xp - from)} / {UiTokens.Thousands(to - from)} XP";
		_xpNext.Text = $"próxima: {Ranks.NameOf(rank + 1)}";
	}

	// ================================================================== veredito

	private string Verdict(out Color color)
	{
		int winner = _match.Winner;
		if (winner == 0)
		{
			color = UiTokens.TextPrimary;
			return "EMPATE";
		}

		bool won = winner == _localTeam;
		color = won ? UiTokens.Accent : UiTokens.Danger;
		return won ? "VITÓRIA" : "DERROTA";
	}

	private string How => _match.EndedByScore ? $"primeiro a {_match.ScoreToWin}" : "fim do tempo";

	private Control BuildHero()
	{
		var center = UiKit.Fill(new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore });

		VBoxContainer column = UiKit.Column(UiTokens.S2);
		column.Alignment = BoxContainer.AlignmentMode.Center;
		center.AddChild(column);

		string verdict = Verdict(out Color color);
		Label title = UiKit.Text(verdict, "Display", color);
		title.AddThemeFontSizeOverride("font_size", 132);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.PivotOffset = new Vector2(300, 80);
		column.AddChild(title);

		HBoxContainer score = ScoreLine(56);
		score.Alignment = BoxContainer.AlignmentMode.Center;
		column.AddChild(score);

		Label how = UiKit.Text(How, "Muted");
		how.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(how);

		return center;
	}

	/// <summary>AZUL 40 × 32 LARANJA, cada lado na cor do time.</summary>
	private HBoxContainer ScoreLine(int size)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);

		Label blueName = UiKit.Text(Teams.NameOf(Teams.Blue), "Subtitle", Teams.ColorOf(Teams.Blue));
		blueName.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(blueName);

		foreach (string part in new[] { _match.ScoreOf(Teams.Blue).ToString(), "×", _match.ScoreOf(Teams.Orange).ToString() })
		{
			Label n = UiKit.Text(part, "NumericLarge", part == "×" ? UiTokens.TextSecondary : UiTokens.TextPrimary);
			n.AddThemeFontSizeOverride("font_size", part == "×" ? size * 2 / 3 : size);
			n.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(n);
		}

		Label orangeName = UiKit.Text(Teams.NameOf(Teams.Orange), "Subtitle", Teams.ColorOf(Teams.Orange));
		orangeName.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(orangeName);
		return row;
	}

	// ================================================================== página

	private Control BuildPage()
	{
		MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S6, UiTokens.S5, UiTokens.S6, UiTokens.S5));
		VBoxContainer page = UiKit.Column(UiTokens.S4);
		margin.AddChild(page);

		// Cabeçalho: o veredito continua, menor.
		HBoxContainer head = UiKit.Row(UiTokens.S4);
		string verdict = Verdict(out Color color);
		Label title = UiKit.Text(verdict, "Display", color);
		title.AddThemeFontSizeOverride("font_size", 64);
		head.AddChild(title);
		HBoxContainer score = ScoreLine(40);
		score.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		head.AddChild(score);
		Label how = UiKit.Text(How, "Muted");
		how.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		head.AddChild(how);
		head.AddChild(UiKit.Spacer());

		MapInfo map = Maps.Get(RoomConfig.Rules.Map);
		Label where = UiKit.Caption($"{map.Name}  ·  {RoomSettings.ModeName(RoomConfig.Rules.Mode)}");
		where.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		head.AddChild(where);
		page.AddChild(head);

		HBoxContainer body = UiKit.Row(UiTokens.S4);
		body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		page.AddChild(body);

		VBoxContainer left = UiKit.Column(UiTokens.S3);
		left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		body.AddChild(left);

		CombatantStats mvp = _match.Mvp;
		left.AddChild(MvpCard(mvp));

		var board = new Scoreboard { Mvp = mvp?.Name, Alignment = BoxContainer.AlignmentMode.Begin };
		left.AddChild(board);
		Callable.From(() => board.Refresh(_match)).CallDeferred();

		body.AddChild(RewardPanel());

		page.AddChild(Footer());
		return margin;
	}

	private Control MvpCard(CombatantStats mvp)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
		card.CustomMinimumSize = new Vector2(Scoreboard.BlockWidth * 2 + UiTokens.S4, 0);
		card.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface2, mvp != null ? UiTokens.Accent : UiTokens.Border, 1,
			UiTokens.RadiusPanel, UiTokens.S4, UiTokens.S3));

		HBoxContainer row = UiKit.Row(UiTokens.S3);
		card.AddChild(row);

		if (mvp == null)
		{
			row.AddChild(Icon.Make(IconKind.Crown, 24, UiTokens.TextDisabled));
			row.AddChild(UiKit.Text("Sem MVP: ninguém pontuou nesta partida.", "Muted"));
			return card;
		}

		row.AddChild(Icon.Make(IconKind.Crown, 32, UiTokens.Accent));
		row.AddChild(RankBadge.Make(mvp.Rank, 40));

		VBoxContainer who = UiKit.Column(0);
		who.AddChild(UiKit.Caption("MVP DA PARTIDA", UiTokens.Accent));
		Label name = UiKit.Text(mvp.Name, "Title", Teams.ColorOf(mvp.Team));
		who.AddChild(name);
		row.AddChild(who);
		if (mvp.IsLocal) row.AddChild(UiKit.Chip("VOCÊ", UiTokens.Accent, solid: true));
		row.AddChild(UiKit.Spacer());

		foreach (var (value, label) in new[] { (mvp.Points, "pontos"), (mvp.Kills, "abates"), (mvp.Assists, "assist."), (mvp.BestSpree, "melhor sequência") })
		{
			VBoxContainer stat = UiKit.Column(0);
			Label n = UiKit.Text(value.ToString(), "NumericLarge");
			n.HorizontalAlignment = HorizontalAlignment.Right;
			stat.AddChild(n);
			Label l = UiKit.Caption(label);
			l.HorizontalAlignment = HorizontalAlignment.Right;
			stat.AddChild(l);
			row.AddChild(stat);
		}

		return card;
	}

	private Control RewardPanel()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column);
		panel.CustomMinimumSize = new Vector2(460, 0);
		panel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

		column.AddChild(UiKit.Caption("RECOMPENSA"));

		if (_match.Reward == null)
		{
			column.AddChild(UiKit.Paragraph("Esta partida não rendeu XP nem Presas."));
			_badge = RankBadge.Make(0);
			_rankName = new Label();
			_xpBar = new ProgressBar();
			_xpText = _xpNext = _xpGain = _presasGain = new Label();
			_lines = _missions = new VBoxContainer();
			_presas = CurrencyLabel.Make(Currency.Presas, Profile.Presas);
			return panel;
		}

		// Patente e barra.
		HBoxContainer rank = UiKit.Row(UiTokens.S3);
		_badge = RankBadge.Make(Ranks.RankFor(_match.Reward.XpBefore), 48);
		_badge.PivotOffset = new Vector2(24, 24);
		rank.AddChild(_badge);
		VBoxContainer rankText = UiKit.Column(0);
		rankText.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		_rankName = UiKit.Text("", "Subtitle");
		rankText.AddChild(_rankName);
		_xpNext = UiKit.Caption("");
		rankText.AddChild(_xpNext);
		rank.AddChild(rankText);
		rank.AddChild(UiKit.Spacer());
		_xpGain = UiKit.Text("", "NumericLarge", UiTokens.Accent);
		_xpGain.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		_xpGain.PivotOffset = new Vector2(60, 16);
		rank.AddChild(_xpGain);
		column.AddChild(rank);

		_xpBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 10),
			MaxValue = 1,
			Step = 0.0005,
			ShowPercentage = false
		};
		_xpBar.AddThemeStyleboxOverride("fill", UiTheme.Box(UiTokens.Accent, default, 0, UiTokens.RadiusSmall, 0, 0));
		column.AddChild(_xpBar);
		_xpText = UiKit.Text("", "Numeric", UiTokens.TextSecondary);
		_xpText.HorizontalAlignment = HorizontalAlignment.Right;
		column.AddChild(_xpText);

		column.AddChild(UiKit.Divider());

		_lines = UiKit.Column(UiTokens.S2);
		column.AddChild(_lines);

		column.AddChild(UiKit.Divider());

		HBoxContainer presas = UiKit.Row(UiTokens.S2);
		presas.AddChild(UiKit.Text("Presas", "Subtitle"));
		presas.AddChild(UiKit.Spacer());
		_presasGain = UiKit.Text("", "Numeric", UiTokens.Success);
		_presasGain.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		_presasGain.PivotOffset = new Vector2(30, 10);
		presas.AddChild(_presasGain);
		_presas = CurrencyLabel.Make(Currency.Presas, _match.Reward.PresasBefore, 110, large: true);
		presas.AddChild(_presas);
		column.AddChild(presas);

		_missions = UiKit.Column(UiTokens.S2);
		_missions.Visible = false;
		if (_match.Reward.MissionsCompleted.Count > 0)
		{
			_missions.AddChild(UiKit.Divider());
			_missions.AddChild(UiKit.Caption("MISSÕES CONCLUÍDAS", UiTokens.Accent));
			foreach (MissionDef mission in _match.Reward.MissionsCompleted)
			{
				HBoxContainer m = UiKit.Row(UiTokens.S2);
				m.AddChild(Icon.Make(IconKind.Check, 14, UiTokens.Success));
				Label text = UiKit.Paragraph(mission.Text, null);
				text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				m.AddChild(text);
				_missions.AddChild(m);
			}
			_missions.AddChild(UiKit.Paragraph("Resgate a recompensa em Missões, no lobby.", "Caption"));
		}
		column.AddChild(_missions);

		return panel;
	}

	private static Control RewardRow(RewardLine line)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		Label label = UiKit.Text(line.Label, "Muted");
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(label);

		Label xp = UiKit.Text(line.Xp > 0 ? $"+{UiTokens.Thousands(line.Xp)} XP" : "", "Numeric", UiTokens.TextPrimary);
		xp.CustomMinimumSize = new Vector2(100, 0);
		xp.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(xp);

		Label presas = UiKit.Text(line.Presas > 0 ? $"+{UiTokens.Thousands(line.Presas)}" : "", "Numeric", CurrencyLabel.ColorOf(Currency.Presas));
		presas.CustomMinimumSize = new Vector2(64, 0);
		presas.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(presas);
		row.AddChild(Icon.Make(IconKind.Presa, 13, CurrencyLabel.ColorOf(Currency.Presas)));
		return row;
	}

	private Control Footer()
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		row.AddChild(UiKit.Spacer());

		Button lobby = UiKit.Button("Sair para o lobby", ButtonKind.Tertiary, () => Leave(ReturnTarget.RoomList));
		row.AddChild(lobby);

		Button room = UiKit.Button("Voltar à sala", ButtonKind.Secondary, () => Leave(ReturnTarget.Room));
		room.Disabled = !RoomConfig.Active || RoomDirectory.Current == null;
		if (room.Disabled) room.TooltipText = "Partida aberta direto no editor: não há sala.";
		row.AddChild(room);

		_again = UiKit.Button("Jogar de novo", ButtonKind.Primary, Again);
		_again.CustomMinimumSize = new Vector2(220, 52);
		row.AddChild(_again);

		foreach (Button b in new[] { lobby, room })
			b.CustomMinimumSize = new Vector2(180, 52);

		return row;
	}

	private void Again() => _match.Restart();

	private void Leave(ReturnTarget target) => _match.Leave(target);
}
