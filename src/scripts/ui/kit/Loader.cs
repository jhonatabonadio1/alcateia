using Godot;

/// <summary>
/// O "carregando" do jogo (Jhon, 27/11: "efeitos de loading personalizados
/// e profissionais"): seis lâminas em volta de um hexágono, acendendo uma
/// atrás da outra com rastro, e um losango no meio que pulsa. Serve em
/// qualquer tamanho — 18 px ao lado de um texto, 96 px no meio da tela.
///
/// Desenhado aqui, sem imagem: a cor é a que a tela pedir
/// (<see cref="Tint"/>) e ele não pisca ao mudar de tamanho.
/// </summary>
public partial class Loader : Control
{
	/// <summary>A cor das lâminas acesas.</summary>
	public Color Tint { get; set; } = UiTokens.Accent;

	/// <summary>Voltas por segundo da luz.</summary>
	[Export] public float Speed = 0.9f;

	private double _time;

	public static Loader Make(float size, Color? tint = null) => new()
	{
		CustomMinimumSize = new Vector2(size, size),
		SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		SizeFlagsVertical = SizeFlags.ShrinkCenter,
		MouseFilter = MouseFilterEnum.Ignore,
		Tint = tint ?? UiTokens.Accent
	};

	public override void _Process(double delta)
	{
		if (!IsVisibleInTree()) return;
		_time += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float side = Mathf.Min(Size.X, Size.Y);
		if (side <= 2f) return;
		Vector2 c = Size / 2f;
		float r = side * 0.46f, inner = side * 0.3f;
		float head = (float)(_time * Speed) % 1f;

		var blade = new Vector2[4];
		for (int i = 0; i < 6; i++)
		{
			// Quanto esta lâmina está atrás da luz: 0 é a acesa, e o rastro cai até 1.
			float behind = Mathf.PosMod(head - i / 6f, 1f);
			float glow = Mathf.Pow(1f - behind, 2.4f);
			float a0 = Mathf.Tau * i / 6f - Mathf.Pi / 2f + 0.1f;
			float a1 = Mathf.Tau * (i + 1) / 6f - Mathf.Pi / 2f - 0.1f;
			// A acesa salta um pouco para fora.
			float push = side * 0.035f * glow;
			blade[0] = c + Dir(a0) * (inner + push);
			blade[1] = c + Dir(a0) * (r + push);
			blade[2] = c + Dir(a1) * (r + push);
			blade[3] = c + Dir(a1) * (inner + push);
			DrawColoredPolygon(blade, new Color(Tint, 0.14f + 0.86f * glow));
		}

		// O losango do meio: respira no ritmo da volta.
		float beat = 0.5f + 0.5f * Mathf.Sin((float)_time * Speed * Mathf.Tau);
		float d = side * (0.085f + 0.035f * beat);
		DrawColoredPolygon(new[]
		{
			c + new Vector2(0, -d), c + new Vector2(d, 0), c + new Vector2(0, d), c + new Vector2(-d, 0)
		}, new Color(Tint.Lerp(Colors.White, 0.5f), 0.55f + 0.45f * beat));
	}

	private static Vector2 Dir(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
}
