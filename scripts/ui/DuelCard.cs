using Godot;
using System.Collections.Generic;

/// <summary>
/// O placar do duelo: toda vez que você mata ou morre, um cartão no
/// centro de baixo mostra a sua patente e a do outro, e quantas vezes cada
/// um já matou o outro nesta partida (decisão do Jhon). É a rivalidade que
/// a partida já contém, posta na tela no instante em que ela muda.
///
/// Você à esquerda, o outro à direita; no meio, o que aconteceu, a arma e
/// a distância. Some sozinho em alguns segundos.
/// </summary>
public partial class DuelCard : CanvasLayer
{
	/// <summary>Quanto tempo o cartão fica, em segundos.</summary>
	[Export] public float ShowTime = 4f;

	private readonly Dictionary<(string, string), int> _kills = new();
	private Control _root;
	private PanelContainer _card;
	private double _left;

	public override void _Ready()
	{
		Layer = 10;
		KillFeed.Reported += OnKill;
		// Uma faixa na largura da tela, presa embaixo, que centraliza o
		// cartão sozinha — posição calculada à mão antes do layout saía na
		// borda esquerda.
		var dock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		dock.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		dock.GrowVertical = Control.GrowDirection.Begin;
		dock.OffsetTop = -150;
		dock.OffsetBottom = -150;
		_root = UiTheme.Apply(dock);
		AddChild(_root);
	}

	public override void _ExitTree() => KillFeed.Reported -= OnKill;

	public override void _Process(double delta)
	{
		if (_card == null) return;
		_left -= delta;
		if (_left > 0) return;

		Tween fade = _card.CreateTween();
		fade.TweenProperty(_card, "modulate:a", 0f, UiTokens.Normal);
		fade.TweenCallback(Callable.From(_card.QueueFree));
		_card = null;
	}

	private void OnKill(Kill kill)
	{
		if (string.IsNullOrEmpty(kill.Killer) || kill.Killer == kill.Victim) return;

		var key = (kill.Killer, kill.Victim);
		_kills[key] = _kills.GetValueOrDefault(key) + 1;

		Health local = Groups.LocalPlayer(GetTree())?.GetNodeOrNull<Health>("Health");
		if (local == null) return;

		string me = local.PublicName;
		if (kill.Killer != me && kill.Victim != me) return;

		bool won = kill.Killer == me;
		string other = won ? kill.Victim : kill.Killer;
		Show(me, other, won, kill.Weapon, DistanceBetween(kill.Killer, kill.Victim));
	}

	private void Show(string me, string other, bool won, string weapon, float distance)
	{
		_card?.QueueFree();

		_card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		Color edge = won ? UiTokens.Success : UiTokens.Danger;
		_card.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.86f), new Color(edge, 0.7f), 1, 0, UiTokens.S4, UiTokens.S3));

		HBoxContainer row = UiKit.Row(UiTokens.S5);
		_card.AddChild(row);

		row.AddChild(Side(me, RankOf(me, true), _kills.GetValueOrDefault((me, other)), UiTokens.TeamBlue, HorizontalAlignment.Left));

		VBoxContainer middle = UiKit.Column(UiTokens.S1);
		middle.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		Label what = UiKit.Text(won ? "VOCÊ ABATEU" : "ABATIDO POR", "Subtitle", edge);
		what.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(what);
		string detail = (string.IsNullOrEmpty(weapon) ? "" : weapon.ToUpperInvariant())
			+ (distance >= 0 ? $"{(string.IsNullOrEmpty(weapon) ? "" : "  ·  ")}{distance:0} m" : "");
		Label how = UiKit.Text(detail, "Muted");
		how.HorizontalAlignment = HorizontalAlignment.Center;
		middle.AddChild(how);
		row.AddChild(middle);

		row.AddChild(Side(other, RankOf(other, false), _kills.GetValueOrDefault((other, me)), UiTokens.TeamOrange, HorizontalAlignment.Right));

		_root.AddChild(_card);
		_left = ShowTime;
		Motion.FadeIn(_card, UiTokens.Normal);
	}

	/// <summary>Um lado do cartão: patente, nome e quantas vezes este
	/// matou o outro.</summary>
	private static Control Side(string name, int rank, int wins, Color team, HorizontalAlignment align)
	{
		HBoxContainer side = UiKit.Row(UiTokens.S3);

		VBoxContainer text = UiKit.Column(0);
		Label who = UiKit.Text(name, "Subtitle", team);
		who.HorizontalAlignment = align;
		text.AddChild(who);
		Label patent = UiKit.Text(Ranks.NameOf(rank), "Muted");
		patent.HorizontalAlignment = align;
		text.AddChild(patent);
		Label score = UiKit.Text($"{wins}", "NumericLarge", UiTokens.TextPrimary);
		score.AddThemeFontSizeOverride("font_size", 36);
		score.HorizontalAlignment = align;
		text.AddChild(score);
		Label caption = UiKit.Caption(wins == 1 ? "ABATE" : "ABATES");
		caption.HorizontalAlignment = align;
		text.AddChild(caption);

		RankBadge badge = RankBadge.Make(rank, 44);
		if (align == HorizontalAlignment.Left)
		{
			side.AddChild(badge);
			side.AddChild(text);
		}
		else
		{
			side.AddChild(text);
			side.AddChild(badge);
		}

		return side;
	}

	public static int RankOf(string name, bool local)
	{
		if (local) return Profile.Rank;
		return Match.Current?.StatsOf(name)?.Rank ?? 0;
	}

	private float DistanceBetween(string a, string b)
	{
		Node3D first = Find(a), second = Find(b);
		return first != null && second != null ? first.GlobalPosition.DistanceTo(second.GlobalPosition) : -1f;
	}

	private Node3D Find(string name)
	{
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is Node3D body && body.GetNodeOrNull<Health>("Health")?.PublicName == name)
					return body;
		return null;
	}
}
