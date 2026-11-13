using Godot;

/// <summary>
/// Mira com abertura dinâmica: as quatro hastes se afastam conforme a
/// dispersão da arma cresce (correr, atirar seguido) e voltam quando ela
/// recupera. Custa pouco e informa muito — o jogador vê, na mira, quando
/// o próximo tiro vai sair torto.
///
/// O desenho é do jogador (Opções → Mira, <see cref="CrosshairSettings"/>):
/// cruz com ou sem ponto, ou só o ponto; cor, tamanho, espessura,
/// espaço, contorno, e a abertura pode ser desligada. Mudou nas opções,
/// vale no quadro seguinte, no meio da partida.
///
/// Na forma de lobo não aparece: o lobo não mira, ele chega — e em
/// terceira pessoa o próprio lobo no meio da tela já diz para onde vai.
/// </summary>
public partial class Crosshair : Control
{
	/// <summary>Pixels de abertura por grau de dispersão.</summary>
	[Export] public float PixelsPerDegree = 9f;

	public Weapon Weapon { get; set; }
	public FormController Form { get; set; }

	/// <summary>Só para a prévia das opções: a dispersão a mostrar, em
	/// graus, no lugar da de uma arma.</summary>
	public float PreviewSpread { get; set; } = -1f;

	private float _spread;

	public Crosshair()
	{
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		// A abertura é visual e contínua: ler a dispersão a cada quadro é
		// o certo aqui (não existe "evento" de dispersão).
		float target = PreviewSpread >= 0f ? PreviewSpread : Weapon?.CurrentSpread ?? 0f;
		_spread = Mathf.Lerp(_spread, target, (float)Mathf.Min(1.0, delta * 18.0));
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Form != null && Form.IsWolf) return;
		Paint(this, Size / 2f, Settings.Values.Crosshair, _spread * PixelsPerDegree);
	}

	/// <summary>Desenha a mira em <paramref name="center"/>.
	/// <paramref name="opening"/>: quanto a dispersão abre as hastes, em
	/// pixels (ignorado com a abertura desligada).</summary>
	public static void Paint(CanvasItem canvas, Vector2 center, CrosshairSettings s, float opening)
	{
		Color line = s.Tint;
		Color shadow = new(0, 0, 0, 0.55f * s.Opacity);

		if (s.Style != CrosshairSettings.Shape.Dot)
		{
			float gap = s.Gap + (s.Dynamic ? opening : 0f);
			foreach (Vector2 dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
			{
				Vector2 from = center + dir * gap;
				Vector2 to = center + dir * (gap + s.Length);
				if (s.Outline) DrawBar(canvas, from - dir, to + dir, s.Thickness + 2f, shadow);
				DrawBar(canvas, from, to, s.Thickness, line);
			}
		}

		if (s.Style == CrosshairSettings.Shape.Cross) return;

		// O ponto: 2 px na cruz (o de sempre), o tamanho fixo quando é só ele.
		float side = s.Style == CrosshairSettings.Shape.Dot ? CrosshairSettings.DotSize : 2f;
		var dot = new Rect2(center - Vector2.One * side / 2f, Vector2.One * side);
		if (s.Outline) canvas.DrawRect(dot.Grow(1f), shadow);
		canvas.DrawRect(dot, line);
	}

	/// <summary>Uma haste como retângulo alinhado ao pixel: linha com
	/// espessura ímpar borra entre dois pixels.</summary>
	private static void DrawBar(CanvasItem canvas, Vector2 from, Vector2 to, float thickness, Color color)
	{
		Vector2 min = new(Mathf.Min(from.X, to.X), Mathf.Min(from.Y, to.Y));
		Vector2 max = new(Mathf.Max(from.X, to.X), Mathf.Max(from.Y, to.Y));
		if (Mathf.IsEqualApprox(from.X, to.X)) { min.X -= thickness / 2f; max.X += thickness / 2f; }
		else { min.Y -= thickness / 2f; max.Y += thickness / 2f; }
		canvas.DrawRect(new Rect2(min.Round(), (max - min).Round()), color);
	}
}
