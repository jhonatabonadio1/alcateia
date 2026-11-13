using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O placar dos dois times, lado a lado. Usado no Tab durante a partida e
/// na tela de resultado.
///
/// Colunas (docs/UI.md, "Placar e fim de partida"):
///
/// | Coluna | Largura | Nota |
/// |---|---|---|
/// | Patente | 32 | só a insígnia |
/// | Nickname | flexível | o jogador local com fundo diferente |
/// | Abates, Mortes, Assist. | 56 | |
/// | Pontos | 64 | a coluna de ordenação |
/// | Forma | 48 | humano, lobo ou caveira (morto) |
/// | Ping | 44 | barras; bot mostra o ícone de bot |
///
/// Ordena por pontos. Morto fica com o nome esmaecido. Sequência de
/// abates (×N) aparece ao lado do nome: é pressão social, e o modo
/// funciona por causa dela.
/// </summary>
public partial class Scoreboard : HBoxContainer
{
	public const int BlockWidth = 620;

	// Um pouco acima das larguras do guia (32/48/56/32/40): o título da
	// coluna, em versalete espaçado, não cabe em 48 px e empurraria a
	// coluna para fora do alinhamento com as linhas.
	private const int RankW = 32, StatW = 56, PointsW = 64, FormW = 48, PingW = 44;

	private static readonly (string Title, int Width)[] Columns =
	{
		("", RankW), ("JOGADOR", 0), ("ABATES", StatW), ("MORTES", StatW), ("ASSIST.", StatW), ("PONTOS", PointsW), ("FORMA", FormW), ("PING", PingW)
	};

	/// <summary>Quem leva a coroa. Nulo durante a partida.</summary>
	public string Mvp { get; set; }

	private readonly Dictionary<int, VBoxContainer> _rows = new();
	private readonly Dictionary<int, List<RowView>> _views = new();
	private readonly Dictionary<int, Label> _scores = new();

	public Scoreboard()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeConstantOverride("separation", UiTokens.S4);
		Alignment = AlignmentMode.Center;
	}

	public override void _Ready()
	{
		foreach (int team in new[] { Teams.Blue, Teams.Orange })
			AddChild(Block(team));
	}

	/// <summary>Atualiza os números. As linhas são montadas uma vez e
	/// reaproveitadas: o Tab chama isto quatro vezes por segundo, e
	/// recriar dezenas de nós a cada chamada só gera lixo.</summary>
	public void Refresh(Match match)
	{
		if (match == null || _rows.Count == 0) return;

		foreach (int team in new[] { Teams.Blue, Teams.Orange })
		{
			_scores[team].Text = match.ScoreOf(team).ToString();

			List<CombatantStats> list = match.Combatants
				.Where(c => c.Team == team)
				.OrderByDescending(c => c.Points)
				.ThenByDescending(c => c.Kills)
				.ThenBy(c => c.Deaths)
				.ThenBy(c => c.Name)
				.ToList();

			List<RowView> views = _views[team];
			while (views.Count < list.Count)
			{
				var view = new RowView();
				_rows[team].AddChild(view.Root);
				views.Add(view);
			}

			for (int i = 0; i < views.Count; i++)
			{
				views[i].Root.Visible = i < list.Count;
				if (i < list.Count) views[i].Show(list[i], Mvp);
			}
		}
	}

	/// <summary>Uma linha do placar e seus nós, para atualizar sem
	/// recriar.</summary>
	private sealed class RowView
	{
		public readonly PanelContainer Root;
		private readonly StyleBoxFlat _plain = Highlight(null);
		private readonly StyleBoxFlat _local = Highlight(UiTokens.Accent);
		private readonly RankBadge _badge;
		private readonly Icon _crown;
		private readonly Label _name;
		private readonly PanelContainer _spree;
		private readonly Label _spreeText;
		private readonly Label _kills, _deaths, _assists, _points;
		private readonly Icon _form;
		private readonly PingBars _ping;
		private readonly Icon _bot;

		public RowView()
		{
			HBoxContainer line = Line();
			line.CustomMinimumSize = new Vector2(0, 30);

			var badgeBox = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			_badge = RankBadge.Make(0, 22);
			badgeBox.AddChild(_badge);
			Cell(badgeBox, RankW);
			line.AddChild(badgeBox);

			// Nome, coroa do MVP e sequência.
			HBoxContainer who = UiKit.Row(UiTokens.S1 + 2);
			who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			who.MouseFilter = MouseFilterEnum.Ignore;
			_crown = Icon.Make(IconKind.Crown, 16, UiTokens.Accent);
			who.AddChild(_crown);
			_name = UiKit.Truncated("");
			_name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			who.AddChild(_name);
			_spree = UiKit.Chip("×2", UiTokens.Warning);
			_spreeText = _spree.GetChild<Label>(0);
			who.AddChild(_spree);
			line.AddChild(who);

			_kills = Number(StatW, UiTokens.TextPrimary);
			_deaths = Number(StatW, UiTokens.TextSecondary);
			_assists = Number(StatW, UiTokens.TextSecondary);
			_points = Number(PointsW, UiTokens.TextPrimary);
			_points.AddThemeFontOverride("font", UiFonts.NumericDisplay);
			line.AddChild(_kills);
			line.AddChild(_deaths);
			line.AddChild(_assists);
			line.AddChild(_points);

			var form = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			_form = Icon.Make(IconKind.Human, 18, UiTokens.TextSecondary);
			form.AddChild(_form);
			Cell(form, FormW);
			line.AddChild(form);

			// Partida local: não há rede. O jogador mostra conexão cheia,
			// o bot mostra que é bot — inventar um ping seria mentir no
			// placar.
			var ping = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			_ping = PingBars.Make(0);
			_bot = Icon.Make(IconKind.Bot, 16, UiTokens.TextDisabled);
			_bot.TooltipText = "bot";
			ping.AddChild(_ping);
			ping.AddChild(_bot);
			Cell(ping, PingW);
			line.AddChild(ping);

			Root = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
			Root.AddThemeStyleboxOverride("panel", _plain);
			Root.AddChild(line);
		}

		public void Show(CombatantStats c, string mvp)
		{
			Root.AddThemeStyleboxOverride("panel", c.IsLocal ? _local : _plain);

			_badge.Rank = c.Rank;
			_crown.Visible = c.Name == mvp;

			_name.Text = c.Name;
			_name.AddThemeFontOverride("font", c.IsLocal ? UiFonts.Bold : UiFonts.Medium);
			_name.AddThemeColorOverride("font_color", c.IsLocal ? UiTokens.Accent : UiTokens.TextPrimary);
			_name.Modulate = c.IsDead ? new Color(1, 1, 1, 0.4f) : Colors.White;

			_spree.Visible = c.Spree >= 2;
			_spreeText.Text = $"×{c.Spree}";

			_kills.Text = c.Kills.ToString();
			_deaths.Text = c.Deaths.ToString();
			_assists.Text = c.Assists.ToString();
			_points.Text = c.Points.ToString();

			if (c.IsDead)
			{
				_form.Kind = IconKind.Skull;
				_form.Tint = UiTokens.TextDisabled;
			}
			else
			{
				_form.Kind = c.IsWolf ? IconKind.Wolf : IconKind.Human;
				_form.Tint = c.IsWolf ? UiTokens.TeamOrange : UiTokens.TextSecondary;
			}

			_ping.Visible = !c.IsBot;
			_bot.Visible = c.IsBot;
		}
	}

	// ------------------------------------------------------------------

	private Control Block(int team)
	{
		Color color = Teams.ColorOf(team);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(BlockWidth, 0), MouseFilter = MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Surface1, 0.94f), UiTokens.Border, 1, UiTokens.RadiusPanel, 0, 0));

		VBoxContainer column = UiKit.Column(0);
		panel.AddChild(column);

		// Cabeçalho: faixa na cor do time, nome e pontuação somada.
		var head = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		var headStyle = UiTheme.Box(new Color(color, 0.16f), default, 0, 0, UiTokens.S3, UiTokens.S2);
		headStyle.BorderWidthTop = 3;
		headStyle.BorderColor = color;
		headStyle.CornerRadiusTopLeft = headStyle.CornerRadiusTopRight = UiTokens.RadiusPanel;
		head.AddThemeStyleboxOverride("panel", headStyle);
		HBoxContainer headRow = UiKit.Row(UiTokens.S2);
		headRow.AddChild(UiKit.Text(Teams.NameOf(team), "Title", color));
		headRow.AddChild(UiKit.Spacer());
		Label score = UiKit.Text("0", "NumericLarge");
		_scores[team] = score;
		headRow.AddChild(score);
		head.AddChild(headRow);
		column.AddChild(head);

		MarginContainer body = UiKit.Margin(UiTokens.S2, UiTokens.S2, UiTokens.S2, UiTokens.S2);
		column.AddChild(body);
		VBoxContainer inner = UiKit.Column(2);
		body.AddChild(inner);

		HBoxContainer titles = Line();
		foreach (var (title, width) in Columns)
		{
			Label label = UiKit.Caption(title);
			Cell(label, width);
			if (width == FormW || width == PingW) label.HorizontalAlignment = HorizontalAlignment.Center;
			titles.AddChild(label);
		}
		var titleRow = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		titleRow.AddThemeStyleboxOverride("panel", Highlight(null));
		titleRow.AddChild(titles);
		inner.AddChild(titleRow);
		inner.AddChild(UiKit.Divider());

		var rows = UiKit.Column(2);
		_rows[team] = rows;
		_views[team] = new List<RowView>();
		inner.AddChild(rows);

		return panel;
	}

	private static HBoxContainer Line()
	{
		HBoxContainer line = UiKit.Row(UiTokens.S2);
		line.MouseFilter = MouseFilterEnum.Ignore;
		return line;
	}

	private static StyleBoxFlat Highlight(Color? highlight)
	{
		if (highlight is not Color h)
			return UiTheme.Box(Colors.Transparent, default, 0, 0, UiTokens.S2, 1);

		StyleBoxFlat style = UiTheme.Box(new Color(h, 0.12f), default, 0, UiTokens.RadiusSmall, UiTokens.S2, 1);
		style.BorderWidthLeft = 3;
		style.BorderColor = h;
		return style;
	}

	private static Label Number(int width, Color color)
	{
		Label label = UiKit.Text("0", "Numeric", color);
		Cell(label, width);
		return label;
	}

	private static void Cell(Control control, int width)
	{
		if (width <= 0)
		{
			control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			return;
		}

		control.CustomMinimumSize = new Vector2(width, 0);
		if (control is Label label) label.HorizontalAlignment = HorizontalAlignment.Right;
	}
}
