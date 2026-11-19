using Godot;

/// <summary>
/// O selo do especial do lobo no feed de abates (pedido do Jhon, 18/11:
/// "se eu matei com o especial do lobo, deve aparecer o mesmo ícone que
/// aparece para o usuário, com brilhos e raios no fundo alaranjado, ao
/// invés desse mais simples — para todos saberem que foi com o especial").
///
/// É o disco do especial pronto (<see cref="AbilityTile"/>) em miniatura:
/// o disco laranja com o aro claro e a garra, raios girando atrás, um halo
/// que respira e faíscas piscando em volta.
/// </summary>
public partial class SpecialBadge : Control
{
	private const float Radius = 12f;
	private const int Rays = 10;

	private double _time;

	public SpecialBadge()
	{
		CustomMinimumSize = new Vector2(46, 30);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>Lado do quadro do brilho: o disco ocupa a mesma fração
	/// dele que no botão do especial.</summary>
	private const float GlowSide = 76f;

	public override void _Ready()
	{
		// O mesmo brilho do botão do especial pronto (AbilityTile): halo,
		// arcos elétricos e raios saltando — em miniatura.
		var glow = new ShaderMaterial { Shader = AbilityTile.GlowShader };
		glow.SetShaderParameter("glow", UiTokens.WolfOrange);
		glow.SetShaderParameter("disc", Radius / GlowSide);
		glow.SetShaderParameter("power", 1f);
		glow.SetShaderParameter("burst", 0f);
		AddChild(new ColorRect
		{
			Color = Colors.White,
			Material = glow,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = CustomMinimumSize / 2f - new Vector2(GlowSide, GlowSide) / 2f,
			Size = new Vector2(GlowSide, GlowSide),
			ShowBehindParent = true
		});

		_disc = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = CustomMinimumSize };
		_disc.Draw += DrawDisc;
		AddChild(_disc);

		Icon claw = Icon.Make(IconKind.Claw, 15, Colors.White);
		AddChild(claw);
		claw.Position = (CustomMinimumSize - new Vector2(15, 15)) / 2f;
	}

	private Control _disc;

	public override void _Process(double delta)
	{
		_time += delta;
		_disc?.QueueRedraw();
	}

	private void DrawDisc()
	{
		Vector2 c = _disc.Size / 2f;
		Color orange = UiTokens.WolfOrange;
		float breath = 0.5f + 0.5f * Mathf.Sin((float)_time * 4.2f);

		// Raios girando atrás do disco.
		var ray = new Vector2[3];
		for (int i = 0; i < Rays; i++)
		{
			float a = (float)_time * 0.9f + i * Mathf.Tau / Rays;
			float reach = Radius * (i % 2 == 0 ? 1.95f : 1.55f) + breath * 1.5f;
			ray[0] = c + Vector2.FromAngle(a - 0.13f) * (Radius * 0.9f);
			ray[1] = c + Vector2.FromAngle(a + 0.13f) * (Radius * 0.9f);
			ray[2] = c + Vector2.FromAngle(a) * reach;
			_disc.DrawColoredPolygon(ray, new Color(1f, 0.78f, 0.35f, i % 2 == 0 ? 0.55f : 0.35f));
		}

		// O disco e o aro claro.
		_disc.DrawCircle(c, Radius, orange);
		_disc.DrawCircle(c, Radius - 3f, orange.Lightened(0.12f));
		_disc.DrawArc(c, Radius - 1f, 0, Mathf.Tau, 32, new Color(1f, 0.97f, 0.9f), 1.6f, true);
	}
}
