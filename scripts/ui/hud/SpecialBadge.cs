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

	public override void _Ready()
	{
		Icon claw = Icon.Make(IconKind.Claw, 15, Colors.White);
		AddChild(claw);
		claw.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		claw.Position = (CustomMinimumSize - new Vector2(15, 15)) / 2f;
	}

	public override void _Process(double delta)
	{
		_time += delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 c = Size / 2f;
		Color orange = UiTokens.WolfOrange;
		float breath = 0.5f + 0.5f * Mathf.Sin((float)_time * 4.2f);

		// Raios girando.
		var ray = new Vector2[3];
		for (int i = 0; i < Rays; i++)
		{
			float a = (float)_time * 0.9f + i * Mathf.Tau / Rays;
			float reach = Radius * (i % 2 == 0 ? 1.95f : 1.55f) + breath * 1.5f;
			ray[0] = c + Vector2.FromAngle(a - 0.13f) * (Radius * 0.9f);
			ray[1] = c + Vector2.FromAngle(a + 0.13f) * (Radius * 0.9f);
			ray[2] = c + Vector2.FromAngle(a) * reach;
			DrawColoredPolygon(ray, new Color(1f, 0.78f, 0.35f, i % 2 == 0 ? 0.7f : 0.45f));
		}

		// O halo.
		DrawCircle(c, Radius + 4f + breath * 2f, new Color(orange, 0.22f));
		DrawCircle(c, Radius + 2f, new Color(orange, 0.35f));

		// O disco e o aro.
		DrawCircle(c, Radius, orange);
		DrawCircle(c, Radius - 3f, orange.Lightened(0.12f));
		DrawArc(c, Radius - 1f, 0, Mathf.Tau, 32, new Color(1f, 0.97f, 0.9f), 1.6f, true);

		// Faíscas piscando.
		for (int i = 0; i < 4; i++)
		{
			float a = i * 1.7f + 0.6f;
			float blink = Mathf.Max(0f, Mathf.Sin((float)_time * 5f + i * 1.9f));
			Vector2 at = c + Vector2.FromAngle(a) * (Radius + 6f + (i % 2) * 3f);
			float s = 1.2f + 2.6f * blink;
			var white = new Color(1f, 0.96f, 0.8f, 0.35f + 0.65f * blink);
			DrawLine(at - new Vector2(s, 0), at + new Vector2(s, 0), white, 1.2f, true);
			DrawLine(at - new Vector2(0, s), at + new Vector2(0, s), white, 1.2f, true);
		}
	}
}
