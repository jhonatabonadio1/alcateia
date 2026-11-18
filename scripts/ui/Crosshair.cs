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
/// No zoom (botão direito segurado, de humano) a cruz dá lugar a uma
/// mira circular, maior e mais detalhada: anel, quatro hastes com marcas
/// de distância, o ponto no meio e um círculo fino que mostra o cone de
/// verdade do próximo tiro, na escala da câmera fechada — o que cai
/// dentro dele pode ser atingido.
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
		float aim = Weapon?.AimEase ?? 0f;
		Paint(this, Size / 2f, Settings.Values.Crosshair, _spread * PixelsPerDegree, aim);
		if (aim > 0f) PaintScope(this, Size / 2f, Size.Y, Settings.Values.Crosshair, ScopeSpread(), aim);
	}

	/// <summary>O cone do próximo tiro em pixels, na câmera de agora.</summary>
	private float ScopeSpread()
	{
		Camera3D camera = GetViewport()?.GetCamera3D();
		float fov = camera?.Fov ?? 75f;
		return Size.Y * 0.5f * Mathf.Tan(Mathf.DegToRad(_spread)) / Mathf.Tan(Mathf.DegToRad(fov * 0.5f));
	}

	/// <summary>
	/// A mira circular do zoom. <paramref name="height"/>: a altura da tela
	/// (o anel acompanha o tamanho dela); <paramref name="cone"/>: o raio do
	/// cone do tiro em pixels; <paramref name="aim"/>: quanto do zoom já
	/// entrou (a mira aparece junto).
	/// </summary>
	public static void PaintScope(CanvasItem canvas, Vector2 center, float height, CrosshairSettings s, float cone, float aim)
	{
		Color tint = s.Tint;
		Color line = new(tint, tint.A * aim);
		Color soft = new(tint, tint.A * aim * 0.55f);
		Color shadow = new(0, 0, 0, 0.5f * s.Opacity * aim);

		float ring = Mathf.Round(height * 0.11f);
		float inner = Mathf.Round(ring * 0.5f);
		const float gap = 9f;

		// O anel, com uma sombra por fora e por dentro para ler em qualquer fundo.
		canvas.DrawArc(center, ring, 0, Mathf.Tau, 96, shadow, 3.5f, true);
		canvas.DrawArc(center, ring, 0, Mathf.Tau, 96, line, 1.5f, true);

		foreach (Vector2 dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
		{
			// Haste grossa do anel até o meio do caminho, fina daí até perto do centro.
			Vector2 side = new(dir.Y, dir.X);
			DrawBar(canvas, center + dir * (ring + 1f), center + dir * inner, 4f, shadow);
			DrawBar(canvas, center + dir * ring, center + dir * inner, 2.5f, line);
			DrawBar(canvas, center + dir * inner, center + dir * gap, 1f, line);

			// Marcas de distância na haste fina: uma a cada quarto do caminho.
			for (int i = 1; i <= 3; i++)
			{
				Vector2 at = center + dir * Mathf.Round(gap + (inner - gap) * i / 4f);
				float half = i == 2 ? 4f : 2.5f;
				DrawBar(canvas, at - side * half, at + side * half, 1f, soft);
			}
		}

		// O cone do próximo tiro: fecha parado e no zoom, abre andando e na rajada.
		float radius = Mathf.Clamp(cone, 3f, ring - 2f);
		canvas.DrawArc(center, radius, 0, Mathf.Tau, 48, soft, 1f, true);

		// O ponto, redondo (pedido do Jhon, 17/11: era um quadrado).
		canvas.DrawCircle(center, 2.2f, shadow, true, -1f, true);
		canvas.DrawCircle(center, 1.4f, line, true, -1f, true);
	}

	/// <summary>Desenha a mira em <paramref name="center"/>.
	/// <paramref name="opening"/>: quanto a dispersão abre as hastes, em
	/// pixels (ignorado com a abertura desligada). <paramref name="aim"/>:
	/// quanto está na mira (0 a 1) — as hastes somem, o ponto fica.</summary>
	public static void Paint(CanvasItem canvas, Vector2 center, CrosshairSettings s, float opening, float aim = 0f)
	{
		Color line = s.Tint;
		Color shadow = new(0, 0, 0, 0.55f * s.Opacity);
		Color fadedLine = new(line, line.A * (1f - aim));
		Color fadedShadow = new(shadow, shadow.A * (1f - aim));

		if (s.Style != CrosshairSettings.Shape.Dot && aim < 1f)
		{
			float gap = s.Gap + (s.Dynamic ? opening : 0f);
			foreach (Vector2 dir in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
			{
				Vector2 from = center + dir * gap;
				Vector2 to = center + dir * (gap + s.Length);
				if (s.Outline) DrawBar(canvas, from - dir, to + dir, s.Thickness + 2f, fadedShadow);
				DrawBar(canvas, from, to, s.Thickness, fadedLine);
			}
		}

		// Cruz sem ponto: na mira, o ponto aparece no lugar das hastes.
		if (s.Style == CrosshairSettings.Shape.Cross)
		{
			if (aim <= 0f) return;
			line = new Color(line, line.A * aim);
			shadow = new Color(shadow, shadow.A * aim);
		}

		// O ponto, redondo (pedido do Jhon, 17/11: era um quadrado): 2 px na
		// cruz (o de sempre), o tamanho fixo quando é só ele.
		float side = s.Style == CrosshairSettings.Shape.Dot ? CrosshairSettings.DotSize : 2f;
		float radius = side / 2f + 0.4f;
		if (s.Outline) canvas.DrawCircle(center, radius + 1f, shadow, true, -1f, true);
		canvas.DrawCircle(center, radius, line, true, -1f, true);
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
