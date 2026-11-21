using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O placar do Tab (Jhon, 21/11: "mude a tab para ter outro estilo,
/// capriche, deixe-a mais profissional, coloque um blur"). Por cima do
/// mapa borrado (quem põe o borrão é o MatchHud), duas faixas — os
/// inimigos em cima, o seu time embaixo — com um cartão por jogador:
///
/// - um filete na cor do time em cima do cartão;
/// - o retrato (foto + borda do nível + placa e estrelas,
///   <see cref="RankBadge"/>), com folga em cima para a chama;
/// - uma linha de estado: EM CHAMAS, a sequência de abates ou ABATIDO;
/// - forma (humano, lobo, caveira), nome e conexão;
/// - abates, assistências e mortes, cada um com o seu rótulo.
///
/// O seu cartão tem a borda em ouro; quem está em chamas
/// (<see cref="Rage"/>) tem a borda azul acesa e o fogo
/// (<see cref="FireAura"/>) em volta do retrato. Na base, os seus números.
///
/// A tela de resultado continua com a tabela (<see cref="Scoreboard"/>).
/// </summary>
public partial class TabBoard : VBoxContainer
{
	private const int CardW = 204, Portrait = 124, FlameRoom = 38;
	private static readonly Color Ice = new("7fc4ff");
	private static readonly Color CardBg = new(0.04f, 0.065f, 0.1f, 0.74f);
	private static readonly Color CardEdge = new(0.75f, 0.88f, 1f, 0.13f);

	/// <summary>Quem leva a coroa. Nulo durante a partida.</summary>
	public string Mvp { get; set; }

	private readonly Dictionary<bool, HBoxContainer> _rows = new();
	private readonly Dictionary<bool, List<Card>> _cards = new();
	private readonly Dictionary<bool, Label> _scores = new();
	private readonly Dictionary<bool, Label> _names = new();
	private readonly Dictionary<bool, ColorRect> _marks = new();
	private readonly Dictionary<string, Label> _mine = new();
	private Label _myName;
	private LevelChip _myLevel;

	public TabBoard()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeConstantOverride("separation", UiTokens.S3);
		Alignment = AlignmentMode.Center;
	}

	public override void _Ready()
	{
		AddChild(Lane(enemy: true));
		AddChild(Versus());
		AddChild(Lane(enemy: false));
		AddChild(MyStats());
	}

	public void Refresh(Match match)
	{
		if (match == null || _rows.Count == 0) return;

		CombatantStats me = match.Combatants.FirstOrDefault(c => c.IsLocal);
		int myTeam = me?.Team ?? Teams.Blue;
		int enemyTeam = myTeam == Teams.Blue ? Teams.Orange : Teams.Blue;

		foreach (bool enemy in new[] { true, false })
		{
			int team = enemy ? enemyTeam : myTeam;
			Color color = Teams.ColorOf(team);
			_marks[enemy].Color = color;
			_names[enemy].Text = Teams.NameOf(team);
			_names[enemy].AddThemeColorOverride("font_color", color.Lightened(0.15f));
			_scores[enemy].Text = match.ScoreOf(team).ToString();

			List<CombatantStats> list = match.Combatants.Where(c => c.Team == team)
				.OrderByDescending(c => c.Points).ThenByDescending(c => c.Kills).ThenBy(c => c.Deaths).ThenBy(c => c.Name).ToList();
			List<Card> cards = _cards[enemy];
			while (cards.Count < list.Count)
			{
				var card = new Card();
				_rows[enemy].AddChild(card.Root);
				cards.Add(card);
			}
			for (int i = 0; i < cards.Count; i++)
			{
				cards[i].Root.Visible = i < list.Count;
				if (i < list.Count) cards[i].Show(list[i], color, Mvp);
			}
		}

		if (me == null) return;
		_myName.Text = me.Name;
		_myLevel.Rank = me.Rank;
		_mine["kills"].Text = me.Kills.ToString();
		_mine["assists"].Text = me.Assists.ToString();
		_mine["deaths"].Text = me.Deaths.ToString();
		_mine["points"].Text = UiTokens.Thousands(me.Points);
		_mine["spree"].Text = me.BestSpree.ToString();
		int fire = (int)me.Rage.FireSeconds;
		_mine["fire"].Text = $"{fire / 60}:{fire % 60:00}";
		_mine["fire"].AddThemeColorOverride("font_color", me.Rage.OnFire ? Ice : UiTokens.TextPrimary);
	}

	// ------------------------------------------------------------------ partes

	/// <summary>Uma faixa: o cabeçalho do time (marca, nome, fio, placar) e os cartões.</summary>
	private Control Lane(bool enemy)
	{
		VBoxContainer lane = UiKit.Column(UiTokens.S2);
		lane.MouseFilter = MouseFilterEnum.Ignore;
		lane.CustomMinimumSize = new Vector2(4 * (CardW + UiTokens.S3), 0);

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.MouseFilter = MouseFilterEnum.Ignore;

		var mark = new ColorRect { CustomMinimumSize = new Vector2(6, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		_marks[enemy] = mark;
		head.AddChild(mark);

		Label name = UiKit.Text("", "Title");
		name.AddThemeFontSizeOverride("font_size", 34);
		_names[enemy] = name;
		head.AddChild(name);

		Label tag = UiKit.Caption(enemy ? "INIMIGOS" : "SEU TIME", UiTokens.TextSecondary);
		tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		head.AddChild(tag);

		head.AddChild(new ColorRect
		{
			Color = new Color(1, 1, 1, 0.14f), CustomMinimumSize = new Vector2(0, 1), SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore
		});

		Label score = UiKit.Text("0", "NumericLarge");
		score.AddThemeFontSizeOverride("font_size", 40);
		_scores[enemy] = score;
		head.AddChild(score);
		lane.AddChild(head);

		var row = new HBoxContainer { Alignment = AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", UiTokens.S3);
		_rows[enemy] = row;
		_cards[enemy] = new List<Card>();
		lane.AddChild(row);
		return lane;
	}

	private static Control Versus()
	{
		HBoxContainer line = UiKit.Row(UiTokens.S3);
		line.Alignment = AlignmentMode.Center;
		line.MouseFilter = MouseFilterEnum.Ignore;
		line.AddChild(Rule());
		line.AddChild(UiKit.Text("VS", "Title", UiTokens.TextSecondary));
		line.AddChild(Rule());
		return line;

		static Control Rule() => new ColorRect
		{
			Color = new Color(1, 1, 1, 0.14f), CustomMinimumSize = new Vector2(0, 1), SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore
		};
	}

	/// <summary>Os seus números da partida: nome e nível à esquerda, as fichas à direita.</summary>
	private Control MyStats()
	{
		var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		panel.AddThemeStyleboxOverride("panel", Box(CardBg, CardEdge, 1, padH: UiTokens.S4, padV: UiTokens.S2));

		HBoxContainer row = UiKit.Row(UiTokens.S5);
		row.MouseFilter = MouseFilterEnum.Ignore;
		panel.AddChild(row);

		VBoxContainer who = UiKit.Column(2);
		who.MouseFilter = MouseFilterEnum.Ignore;
		who.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_myName = UiKit.Text("", "Subtitle", UiTokens.Accent);
		_myName.AddThemeFontSizeOverride("font_size", 20);
		_myLevel = LevelChip.Make(Ranks.None, 20);
		_myLevel.MouseFilter = MouseFilterEnum.Ignore;
		_myLevel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		who.AddChild(_myName);
		who.AddChild(_myLevel);
		row.AddChild(who);

		row.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.12f), CustomMinimumSize = new Vector2(1, 34), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });

		foreach (var (key, title) in new[] { ("kills", "ABATES"), ("assists", "ASSISTÊNCIAS"), ("deaths", "MORTES"), ("points", "PONTOS"), ("spree", "MELHOR SEQUÊNCIA"), ("fire", "EM CHAMAS") })
		{
			VBoxContainer tile = UiKit.Column(0);
			tile.MouseFilter = MouseFilterEnum.Ignore;
			Label value = UiKit.Text("0", "NumericLarge");
			value.HorizontalAlignment = HorizontalAlignment.Center;
			Label caption = UiKit.Caption(title);
			caption.HorizontalAlignment = HorizontalAlignment.Center;
			tile.AddChild(value);
			tile.AddChild(caption);
			row.AddChild(tile);
			_mine[key] = value;
		}
		return panel;
	}

	/// <summary>A caixa facetada dos cartões: canto de baixo à direita cortado,
	/// como as placas do HUD; <paramref name="glow"/> acende um halo em volta.</summary>
	private static StyleBoxFlat Box(Color bg, Color border, int width, Color? glow = null, int padH = 0, int padV = 0)
	{
		var box = new StyleBoxFlat { BgColor = bg, BorderColor = border, CornerDetail = 1, AntiAliasing = true };
		box.SetBorderWidthAll(width);
		box.CornerRadiusBottomRight = 14;
		box.ContentMarginLeft = box.ContentMarginRight = padH;
		box.ContentMarginTop = box.ContentMarginBottom = padV;
		if (glow is Color g)
		{
			box.ShadowColor = g;
			box.ShadowSize = 14;
		}
		return box;
	}

	/// <summary>O cartão de um jogador.</summary>
	private sealed class Card
	{
		public readonly PanelContainer Root;

		private readonly StyleBoxFlat _plain = Box(CardBg, CardEdge, 1);
		private readonly StyleBoxFlat _local = Box(new Color(0.1f, 0.085f, 0.04f, 0.78f), new Color(UiTokens.Accent, 0.9f), 2);
		private readonly StyleBoxFlat _burning = Box(new Color(0.03f, 0.07f, 0.14f, 0.8f), new Color(Ice, 0.95f), 2, new Color(0.2f, 0.55f, 1f, 0.45f));

		private readonly ColorRect _strip;
		private readonly RankBadge _badge;
		private readonly FireAura _fire;
		private readonly Label _status, _name, _kills, _assists, _deaths;
		private readonly Icon _crown, _form, _bot, _botFace;
		private readonly PingBars _ping;

		public Card()
		{
			Root = new PanelContainer { CustomMinimumSize = new Vector2(CardW, 0), MouseFilter = MouseFilterEnum.Ignore };
			Root.AddThemeStyleboxOverride("panel", _plain);

			VBoxContainer outer = UiKit.Column(0);
			outer.MouseFilter = MouseFilterEnum.Ignore;
			Root.AddChild(outer);

			_strip = new ColorRect { CustomMinimumSize = new Vector2(0, 3), MouseFilter = MouseFilterEnum.Ignore };
			outer.AddChild(_strip);

			MarginContainer pad = UiKit.Margin(UiTokens.S2, 0, UiTokens.S2, UiTokens.S2);
			pad.MouseFilter = MouseFilterEnum.Ignore;
			outer.AddChild(pad);
			VBoxContainer column = UiKit.Column(UiTokens.S1);
			column.MouseFilter = MouseFilterEnum.Ignore;
			pad.AddChild(column);

			// A folga para a chama subir sem sair do cartão.
			column.AddChild(new Control { CustomMinimumSize = new Vector2(0, FlameRoom), MouseFilter = MouseFilterEnum.Ignore });

			var portrait = new CenterContainer { CustomMinimumSize = new Vector2(0, Portrait + 36), MouseFilter = MouseFilterEnum.Ignore };
			// bot não tem retrato nem nível: o ícone dele no lugar
			_botFace = Icon.Make(IconKind.Bot, 64, new Color(UiTokens.TextDisabled, 0.7f));
			portrait.AddChild(_botFace);
			_badge = RankBadge.Make(0, Portrait);
			_badge.MouseFilter = MouseFilterEnum.Ignore;
			portrait.AddChild(_badge);
			_fire = FireAura.On(_badge, Portrait * RankBadge.Grow * RankBadge.Window);
			column.AddChild(portrait);

			_status = UiKit.Caption("");
			_status.HorizontalAlignment = HorizontalAlignment.Center;
			_status.AddThemeFontSizeOverride("font_size", 12);
			_status.CustomMinimumSize = new Vector2(0, 16);
			column.AddChild(_status);

			HBoxContainer who = UiKit.Row(UiTokens.S1);
			who.MouseFilter = MouseFilterEnum.Ignore;
			_form = Icon.Make(IconKind.Human, 20, UiTokens.TextSecondary);
			_crown = Icon.Make(IconKind.Crown, 16, UiTokens.Accent);
			_name = UiKit.Truncated("", "Subtitle");
			_name.AddThemeFontSizeOverride("font_size", 19);
			_name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_ping = PingBars.Make(0);
			_bot = Icon.Make(IconKind.Bot, 14, UiTokens.TextDisabled);
			who.AddChild(_form);
			who.AddChild(_crown);
			who.AddChild(_name);
			who.AddChild(_ping);
			who.AddChild(_bot);
			column.AddChild(who);

			column.AddChild(new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });

			HBoxContainer stats = UiKit.Row(0);
			stats.MouseFilter = MouseFilterEnum.Ignore;
			_kills = Stat(stats, "ABATES", UiTokens.TextPrimary);
			_assists = Stat(stats, "ASSIST.", UiTokens.TextSecondary);
			_deaths = Stat(stats, "MORTES", UiTokens.TextSecondary);
			column.AddChild(stats);
		}

		private static Label Stat(HBoxContainer row, string title, Color color)
		{
			VBoxContainer tile = UiKit.Column(0);
			tile.MouseFilter = MouseFilterEnum.Ignore;
			tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			Label value = UiKit.Text("0", "Numeric", color);
			value.AddThemeFontOverride("font", UiFonts.NumericDisplay);
			value.AddThemeFontSizeOverride("font_size", 26);
			value.HorizontalAlignment = HorizontalAlignment.Center;
			Label caption = UiKit.Caption(title);
			caption.AddThemeFontSizeOverride("font_size", 11);
			caption.HorizontalAlignment = HorizontalAlignment.Center;
			tile.AddChild(value);
			tile.AddChild(caption);
			row.AddChild(tile);
			return value;
		}

		public void Show(CombatantStats c, Color team, string mvp)
		{
			bool burning = c.Rage.OnFire;
			Root.AddThemeStyleboxOverride("panel", burning ? _burning : c.IsLocal ? _local : _plain);
			_strip.Color = c.IsDead ? new Color(team, 0.35f) : team;

			_badge.Rank = c.Rank;
			_badge.Avatar = c.Photo;
			_botFace.Visible = c.Rank < 0;
			_botFace.Modulate = c.IsDead ? new Color(1, 1, 1, 0.4f) : Colors.White;
			_badge.Modulate = c.IsDead ? new Color(0.5f, 0.52f, 0.58f, 0.8f) : Colors.White;
			_fire.Intensity = burning ? c.Rage.Level : 0f;

			if (burning) Status("EM CHAMAS", Ice);
			else if (c.IsDead) Status("ABATIDO", UiTokens.TextDisabled);
			else if (c.Spree >= 2) Status(Loc.F("SEQUÊNCIA ×{0}", c.Spree), UiTokens.Warning);
			else Status("", UiTokens.TextDisabled);

			_crown.Visible = c.Name == mvp;
			_name.Text = c.Name;
			_name.AddThemeColorOverride("font_color", c.IsLocal ? UiTokens.Accent : c.IsDead ? UiTokens.TextDisabled : UiTokens.TextPrimary);

			if (c.IsDead)
			{
				_form.Kind = IconKind.Skull;
				_form.Tint = UiTokens.TextDisabled;
			}
			else
			{
				_form.Kind = c.IsWolf ? IconKind.Wolf : IconKind.Human;
				_form.Tint = c.IsWolf ? UiTokens.WolfOrange : UiTokens.TextSecondary;
			}

			_ping.Visible = !c.IsBot;
			_bot.Visible = c.IsBot;

			_kills.Text = c.Kills.ToString();
			_assists.Text = c.Assists.ToString();
			_deaths.Text = c.Deaths.ToString();
		}

		private void Status(string text, Color color)
		{
			_status.Text = text;
			_status.AddThemeColorOverride("font_color", color);
		}
	}
}
