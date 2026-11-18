using Godot;

/// <summary>
/// O aviso de abate, logo abaixo da mira (pedido do Jhon, 17/11): uma
/// caveira e "ELIMINOU &lt;jogador&gt;", como em Overwatch. Só para quem
/// matou. A caveira nasce grande e encolhe; a linha fica uns segundos e
/// some. Abates seguidos empilham (até <see cref="Lines"/>).
///
/// O feed do canto continua contando o que acontece na partida; isto é a
/// confirmação para quem atirou, sem tirar o olho do meio da tela.
/// </summary>
public partial class KillBanner : VBoxContainer
{
	/// <summary>Quanto tempo uma linha fica na tela.</summary>
	[Export] public float Life = 2.6f;

	[Export] public int Lines = 3;

	private static readonly Color Red = new("ff4a4f");

	public KillBanner()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Alignment = AlignmentMode.Begin;
		AddThemeConstantOverride("separation", UiTokens.S1);
	}

	public void Add(string victim, bool headshot)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.Alignment = AlignmentMode.Center;
		row.MouseFilter = MouseFilterEnum.Ignore;

		// A caveira, com uma sombra escura por baixo (sem painel atrás,
		// vermelho some em parede vermelha).
		const int side = 38;
		var skull = new Control
		{
			CustomMinimumSize = new Vector2(side, side),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			PivotOffset = new Vector2(side, side) / 2f,
			MouseFilter = MouseFilterEnum.Ignore
		};
		foreach (var (color, offset) in new[] { (new Color(0, 0, 0, 0.6f), new Vector2(2, 2.5f)), (Red, Vector2.Zero) })
		{
			Icon layer = Icon.Make(IconKind.SkullSolid, side, color);
			layer.Position = offset;
			layer.Size = new Vector2(side, side);
			skull.AddChild(layer);
		}
		row.AddChild(skull);

		row.AddChild(HudStyle.Number(UiKit.Text("ELIMINOU", "Display"), 30, UiTokens.TextPrimary));
		row.AddChild(HudStyle.Number(UiKit.Text((victim ?? "").ToUpperInvariant(), "Display"), 30, Red));
		if (headshot)
		{
			Label head = HudStyle.Outlined(UiKit.Caption("na cabeça", UiTokens.Warning));
			head.SizeFlagsVertical = SizeFlags.ShrinkEnd;
			row.AddChild(head);
		}

		AddChild(row);
		while (GetChildCount() > Lines)
		{
			Node old = GetChild(0);
			RemoveChild(old);
			old.QueueFree();
		}

		// A caveira bate na tela: nasce grande e assenta.
		skull.Scale = new Vector2(2.1f, 2.1f);
		Tween pop = skull.CreateTween();
		pop.TweenProperty(skull, "scale", Vector2.One, 0.28f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		row.Modulate = new Color(1, 1, 1, 0);
		Tween life = row.CreateTween();
		life.TweenProperty(row, "modulate:a", 1f, 0.1f);
		life.TweenInterval(Mathf.Max(0.2f, Life - 0.5f));
		life.TweenProperty(row, "modulate:a", 0f, 0.4f);
		life.TweenCallback(Callable.From(row.QueueFree));
	}
}
