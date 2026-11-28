using Godot;

/// <summary>
/// Aviso curto no topo da tela, que some sozinho: "Comprado",
/// "Missão resgatada", "Nome inválido". Para o que o jogador precisa
/// saber mas não precisa confirmar.
/// </summary>
public partial class Toast : PanelContainer
{
	public static void Push(Node host, string text, Color? color = null, float seconds = 2.4f)
	{
		var toast = new Toast { ThemeTypeVariation = "Surface3", ZIndex = 60, MouseFilter = MouseFilterEnum.Ignore };
		Color c = color ?? UiTokens.TextPrimary;
		// Uma placa escura com o fio da cor do aviso à esquerda.
		var box = new StyleBoxFlat { BgColor = new Color(0.03f, 0.05f, 0.075f, 0.96f), BorderColor = c };
		box.BorderWidthLeft = 3;
		box.ContentMarginLeft = UiTokens.S3;
		box.ContentMarginRight = UiTokens.S3;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2 + 2;
		box.ShadowColor = new Color(0, 0, 0, 0.45f);
		box.ShadowSize = 16;
		toast.AddThemeStyleboxOverride("panel", box);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		toast.AddChild(row);
		row.AddChild(Icon.Make(c == UiTokens.Danger ? IconKind.Warning : IconKind.Check, 16, c));
		row.AddChild(UiKit.Text(text, null, UiTokens.TextPrimary));

		toast.AnchorLeft = toast.AnchorRight = 0.5f;
		toast.AnchorTop = toast.AnchorBottom = 0f;
		toast.GrowHorizontal = GrowDirection.Both;
		toast.OffsetTop = UiTokens.HeaderHeight + UiTokens.S3 + 4;

		// Os que já estão na tela descem: o novo entra em cima.
		foreach (Node other in host.GetChildren())
			if (other is Toast older && !older.IsQueuedForDeletion())
				older.CreateTween().TweenProperty(older, "offset_top", older.OffsetTop + 52f, 0.18f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		host.AddChild(toast);
		// Desce do header e aparece.
		toast.Modulate = new Color(1, 1, 1, 0);
		float rest = toast.OffsetTop;
		toast.OffsetTop = rest - 18f;
		Tween enter = toast.CreateTween().SetParallel();
		enter.TweenProperty(toast, "offset_top", rest, 0.24f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		enter.TweenProperty(toast, "modulate:a", 1f, 0.16f);

		Tween life = toast.CreateTween();
		life.TweenInterval(seconds);
		life.TweenProperty(toast, "modulate:a", 0f, UiTokens.Normal);
		life.TweenCallback(Callable.From(toast.QueueFree));
	}
}
