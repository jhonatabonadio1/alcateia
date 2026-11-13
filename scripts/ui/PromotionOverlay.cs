using Godot;
using System;

/// <summary>
/// A tela de promoção: a insígnia nova grande, o nome da patente e a
/// recompensa em Presas. Momento de celebração — não se pula sozinho:
/// fecha no botão.
/// </summary>
public partial class PromotionOverlay : Control
{
	private int _rank;
	private Action _closed;

	public static PromotionOverlay Open(Node host, int rank, Action closed = null)
	{
		var overlay = new PromotionOverlay { _rank = rank, _closed = closed };
		UiKit.Fill(overlay);
		overlay.MouseFilter = MouseFilterEnum.Stop;
		overlay.ZIndex = 70;
		if (overlay.Theme == null) UiTheme.Apply(overlay);
		host.AddChild(overlay);
		return overlay;
	}

	public override void _Ready()
	{
		AddChild(UiKit.Fill(new ColorRect { Color = new Color(0, 0, 0, 0.78f) }));

		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		VBoxContainer column = UiKit.Column(UiTokens.S3);
		column.Alignment = BoxContainer.AlignmentMode.Center;
		center.AddChild(column);

		Label head = UiKit.Text("PROMOVIDO", "Display", UiTokens.Accent);
		head.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(head);

		RankBadge badge = RankBadge.Make(_rank, 160);
		badge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		column.AddChild(badge);

		Label name = UiKit.Text(Ranks.NameOf(_rank), "Title", Ranks.ColorOf(_rank));
		name.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(name);

		HBoxContainer reward = UiKit.Row(UiTokens.S2);
		reward.Alignment = BoxContainer.AlignmentMode.Center;
		reward.AddChild(UiKit.Text($"+{UiTokens.Thousands(Ranks.PromotionReward(_rank))}", "NumericLarge"));
		reward.AddChild(Icon.Make(IconKind.Presa, 22, CurrencyLabel.ColorOf(Currency.Presas)));
		column.AddChild(reward);

		string unlock = UnlockText(_rank);
		if (unlock != null)
		{
			Label line = UiKit.Text(unlock, "Muted");
			line.HorizontalAlignment = HorizontalAlignment.Center;
			column.AddChild(line);
		}

		Button ok = UiKit.Button("Continuar", ButtonKind.Primary, Close);
		ok.CustomMinimumSize = new Vector2(220, UiTokens.ButtonHeight);
		ok.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		column.AddChild(UiKit.Gap(UiTokens.S2));
		column.AddChild(ok);
		ok.CallDeferred(Control.MethodName.GrabFocus);

		badge.PivotOffset = badge.CustomMinimumSize / 2f;
		badge.Scale = new Vector2(0.4f, 0.4f);
		Tween tween = badge.CreateTween();
		tween.TweenProperty(badge, "scale", Vector2.One, 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		Motion.FadeIn(this, UiTokens.Normal);
		Sfx.Ui("vitoria");
	}

	/// <summary>O que a patente nova destrava na loja, se algo.</summary>
	private static string UnlockText(int rank)
	{
		var names = new System.Collections.Generic.List<string>();
		foreach (CatalogItem item in Catalog.All)
			if (item.MinRank == rank) names.Add(item.Name);
		return names.Count == 0 ? null : "Destravado: " + string.Join(", ", names);
	}

	private void Close()
	{
		_closed?.Invoke();
		QueueFree();
	}
}
