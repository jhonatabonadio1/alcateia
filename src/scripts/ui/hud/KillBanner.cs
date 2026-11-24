using Godot;

/// <summary>
/// Os avisos de abate em texto (pedidos do Jhon, 17 e 18/11):
///
/// - **"ELIMINOU &lt;jogador&gt;"**, embaixo, logo acima do especial, só
///   para quem matou. A linha fica uns segundos e some; abates seguidos
///   empilham (até <see cref="Lines"/>). A caveira não fica aqui: ela
///   pulsa no centro da mira (<see cref="HitMarker"/>).
/// - **A chamada da sequência** (<see cref="Announce"/>: DOUBLE KILL,
///   TEAM KILL, VINGANÇA), que o Hud põe numa segunda instância, no alto,
///   abaixo da barra do placar.
///
/// O feed do canto continua contando o que acontece na partida; isto é a
/// confirmação para quem atirou.
/// </summary>
public partial class KillBanner : VBoxContainer
{
	/// <summary>Quanto tempo uma linha fica na tela.</summary>
	[Export] public float Life = 2.6f;

	[Export] public int Lines = 3;

	private static readonly Color Red = new("ff4a4f");
	private static readonly Color Gold = new("ffc94d");

	private Control _call;

	/// <summary>A chamada grande, acima das linhas: DOUBLE KILL, TEAM
	/// KILL, VINGANÇA. Uma por vez; a nova troca a anterior. Quanto maior
	/// a sequência, maior a letra.</summary>
	public void Announce(string title, int weight = 2, Color? color = null)
	{
		if (string.IsNullOrEmpty(title)) return;
		if (_call != null && IsInstanceValid(_call)) _call.QueueFree();

		int size = Mathf.Min(40 + weight * 5, 78);
		Label call = HudStyle.Number(UiKit.Text(title, "Display"), size, color ?? Gold);
		call.HorizontalAlignment = HorizontalAlignment.Center;
		call.AddThemeConstantOverride("outline_size", 10);
		call.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		AddChild(call);
		MoveChild(call, 0);
		_call = call;

		// Entra estourando e assenta; fica um pouco e some.
		call.Modulate = new Color(1, 1, 1, 0);
		Tween life = call.CreateTween();
		life.TweenProperty(call, "modulate:a", 1f, 0.08f);
		life.TweenInterval(1.7f);
		life.TweenProperty(call, "modulate:a", 0f, 0.45f);
		life.TweenCallback(Callable.From(call.QueueFree));

		Callable.From(() =>
		{
			if (!IsInstanceValid(call)) return;
			call.PivotOffset = call.Size / 2f;
			call.Scale = new Vector2(1.7f, 1.7f);
			Tween pop = call.CreateTween();
			pop.TweenProperty(call, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}).CallDeferred();
	}

	public KillBanner()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Alignment = AlignmentMode.End;
		AddThemeConstantOverride("separation", UiTokens.S1);
	}

	public void Add(string victim, bool headshot)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.Alignment = AlignmentMode.Center;
		row.MouseFilter = MouseFilterEnum.Ignore;

		row.AddChild(HudStyle.Number(UiKit.Text("ELIMINOU", "Display"), 30, UiTokens.TextPrimary));
		row.AddChild(HudStyle.Number(UiKit.Text((victim ?? "").ToUpperInvariant(), "Display"), 30, Red));
		if (headshot)
		{
			Label head = HudStyle.Outlined(UiKit.Caption("na cabeça", UiTokens.Warning));
			head.SizeFlagsVertical = SizeFlags.ShrinkEnd;
			row.AddChild(head);
		}

		AddChild(row);
		int first = _call != null && IsInstanceValid(_call) ? 1 : 0;
		while (GetChildCount() - first > Lines)
		{
			Node old = GetChild(first);
			RemoveChild(old);
			old.QueueFree();
		}

		row.Modulate = new Color(1, 1, 1, 0);
		Tween life = row.CreateTween();
		life.TweenProperty(row, "modulate:a", 1f, 0.1f);
		life.TweenInterval(Mathf.Max(0.2f, Life - 0.5f));
		life.TweenProperty(row, "modulate:a", 0f, 0.4f);
		life.TweenCallback(Callable.From(row.QueueFree));
	}
}
