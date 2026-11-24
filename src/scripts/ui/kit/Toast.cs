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
		toast.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface3, new Color(c, 0.6f), 1, UiTokens.RadiusSmall, UiTokens.S3, UiTokens.S2));

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		toast.AddChild(row);
		row.AddChild(Icon.Make(c == UiTokens.Danger ? IconKind.Warning : IconKind.Check, 16, c));
		row.AddChild(UiKit.Text(text, null, UiTokens.TextPrimary));

		toast.AnchorLeft = toast.AnchorRight = 0.5f;
		toast.AnchorTop = toast.AnchorBottom = 0f;
		toast.GrowHorizontal = GrowDirection.Both;
		toast.OffsetTop = UiTokens.HeaderHeight + UiTokens.S3;

		host.AddChild(toast);
		Motion.FadeIn(toast, UiTokens.Fast);

		Tween life = toast.CreateTween();
		life.TweenInterval(seconds);
		life.TweenProperty(toast, "modulate:a", 0f, UiTokens.Normal);
		life.TweenCallback(Callable.From(toast.QueueFree));
	}
}
