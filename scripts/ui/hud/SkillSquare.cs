using Godot;

/// <summary>
/// Um quadradinho de habilidade no canto do HUD (pedido do Jhon, 18/11:
/// "coloque o R como habilidade, para o jogador saber que existe isso;
/// pode pôr no lado direito em um quadradinho, igual Overwatch").
///
/// O ícone num quadrado escuro, a tecla numa plaquinha embaixo. Três
/// estados, sem texto:
///
/// - **pronto**: ícone claro, borda fraca;
/// - **recarregando**: ícone apagado e uma cortina escura que desce
///   conforme a recarga anda (<see cref="Set"/>, <c>ready</c> de 0 a 1);
/// - **em uso**: a cor quente enche de baixo para cima e a borda acende
///   (o uivo, enquanto se segura o R: <c>progress</c> de 0 a 1).
/// </summary>
public partial class SkillSquare : Control
{
	public const int Side = 52;
	private const int KeyHeight = 18;
	private const int Gap = 4;

	public IconKind Kind { get; set; } = IconKind.Claw;

	/// <summary>A tecla (ou o botão do mouse), curta: "R", "CLIQUE".</summary>
	public string Key { get; set; } = "";

	/// <summary>A cor do "em uso".</summary>
	public Color Hot { get; set; } = UiTokens.WolfOrange;

	/// <summary>De 0 (acabou de usar) a 1 (pronto). Para os testes.</summary>
	public float Ready { get; private set; } = 1f;

	/// <summary>De 0 a 1 enquanto está em uso. Para os testes.</summary>
	public float Progress { get; private set; }

	private Icon _icon;
	private Control _cover;

	public SkillSquare()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Side, Side + Gap + KeyHeight);
	}

	public override void _Ready()
	{
		_icon = Icon.Make(Kind, Side - 20, UiTokens.TextPrimary);
		_icon.Position = new Vector2(10, 10);
		_icon.Size = new Vector2(Side - 20, Side - 20);
		AddChild(_icon);

		// A cortina e a borda vão por cima do ícone.
		_cover = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_cover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_cover.Draw += DrawCover;
		AddChild(_cover);
	}

	/// <param name="ready">0 a 1: a recarga (1 = pronto).</param>
	/// <param name="progress">0 a 1: em uso agora.</param>
	public void Set(float ready, float progress = 0f)
	{
		ready = Mathf.Clamp(ready, 0f, 1f);
		progress = Mathf.Clamp(progress, 0f, 1f);
		if (Mathf.IsEqualApprox(ready, Ready) && Mathf.IsEqualApprox(progress, Progress)) return;
		Ready = ready;
		Progress = progress;
		if (_icon != null) _icon.Modulate = new Color(1, 1, 1, ready >= 1f || progress > 0f ? 1f : 0.4f);
		QueueRedraw();
		_cover?.QueueRedraw();
	}

	public override void _Draw()
	{
		var box = new Rect2(0, 0, Side, Side);
		DrawRect(box, new Color(0.03f, 0.04f, 0.06f, 0.74f));
		// Em uso: a cor enche de baixo para cima, por baixo do ícone.
		if (Progress > 0f)
			DrawRect(new Rect2(0, Side * (1f - Progress), Side, Side * Progress), new Color(Hot, 0.5f));

		// A tecla, numa plaquinha embaixo.
		Font font = UiFonts.Label;
		const int size = 12;
		string key = Loc.T(Key);
		float width = Mathf.Max(font.GetStringSize(key, HorizontalAlignment.Left, -1, size).X + 12f, 22f);
		var chip = new Rect2((Side - width) / 2f, Side + Gap, width, KeyHeight);
		DrawRect(chip, new Color(0.03f, 0.04f, 0.06f, 0.9f));
		DrawRect(chip, new Color(1, 1, 1, 0.28f), false, 1f);
		DrawString(font, new Vector2(chip.Position.X, chip.Position.Y + 13f), key, HorizontalAlignment.Center, width, size, UiTokens.TextPrimary);
	}

	private void DrawCover()
	{
		var box = new Rect2(0, 0, Side, Side);
		if (Ready < 1f && Progress <= 0f)
			_cover.DrawRect(new Rect2(0, 0, Side, Side * (1f - Ready)), new Color(0, 0, 0, 0.55f));
		bool lit = Progress > 0f;
		_cover.DrawRect(box, lit ? Hot : new Color(1, 1, 1, Ready >= 1f ? 0.38f : 0.18f), false, lit ? 2f : 1.5f);
	}
}
