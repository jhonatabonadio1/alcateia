using Godot;

/// <summary>
/// O especial do lobo (o salto do Q), no inferior centro, como a suprema
/// de Overwatch (pedido do Jhon, 17/11): um disco. Enchendo é escuro, com
/// um aro que fecha no sentido horário e a carga em % no meio (desde
/// 18/11 o especial é carga, não tempo: <see cref="Weapon.SpecialCharge"/>).
/// Cheio, **acende**: o disco fica laranja cheio, com o ícone branco, um
/// halo que respira e raios girando devagar em volta (shader aditivo), e
/// no instante em que enche solta uma onda e dá um pulinho.
///
/// A tecla fica numa ficha embaixo, com o nome.
///
/// Quem manda o estado é o Hud, uma vez por quadro: <see cref="SetCharge"/>.
/// </summary>
public partial class AbilityTile : Control
{
	private const float Side = 96f;
	private const float Radius = 42f;
	private const float GlowSide = 240f;
	private const float CaptionHeight = 42f;

	/// <summary>A cor do disco aceso.</summary>
	public Color Hot { get; set; } = UiTokens.WolfOrange;

	/// <summary>A cor do halo.</summary>
	public Color Glow { get; set; } = new(1f, 0.6f, 0.18f);

	private string _title = "";

	/// <summary>O nome embaixo do disco.</summary>
	public string Title
	{
		get => _title;
		set
		{
			if (value == _title) return;
			_title = value;
			_cover?.QueueRedraw();
		}
	}

	public string Key { get; set; } = "";
	public IconKind Kind { get; set; } = IconKind.Claw;

	// O brilho do especial pronto: halo que respira e eletricidade em
	// volta do disco — arcos tortos que estalam e raios curtos saltando
	// para fora, redesenhados várias vezes por segundo (pedido do Jhon,
	// 18/11: o raio do botão mais profissional; antes eram dois leques
	// girando).
	internal const string GlowCode = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 glow : source_color = vec4(1.0, 0.6, 0.18, 1.0);
uniform float power = 0.0;
uniform float burst = 0.0;
uniform float disc = 0.175;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec2 p = UV - 0.5;
	float r = length(p);
	vec2 dir = p / max(r, 0.0001);
	float pulse = 0.5 + 0.5 * sin(TIME * 3.0);
	float d = max(r - disc, 0.0);
	float outside = smoothstep(disc - 0.012, disc, r);
	float fade = 1.0 - smoothstep(0.38, 0.5, r);

	// Halo: cai com a distância da borda do disco, e respira.
	float halo = exp(-d * (13.0 - 4.0 * pulse)) * (0.55 + 0.2 * pulse);
	// Um clarão largo e fraco, que é o que se vê de canto de olho.
	float bloom = exp(-d * 5.0) * (0.2 + 0.08 * pulse);

	// Eletricidade. O desenho muda 16 vezes por segundo.
	float tick = floor(TIME * 16.0);
	float core = 0.0;
	float aura = 0.0;
	// Arcos em volta do disco: anéis tortos, acesos só num trecho de cada vez.
	for (int i = 0; i < 3; i++) {
		float fi = float(i);
		vec2 seed = vec2(tick * 3.7 + fi * 17.0, tick * 1.3 - fi * 9.0);
		float wobble = (vnoise(dir * (2.6 + fi * 1.4) + seed) - 0.5) * 0.11
			+ (vnoise(dir * 9.0 + seed * 1.7) - 0.5) * 0.03;
		float ring = disc + 0.03 + 0.035 * fi + wobble;
		float dist = abs(r - ring);
		float lit = smoothstep(0.5, 0.78, vnoise(dir * 1.6 + seed * 0.61 + 40.0));
		core += smoothstep(0.007, 0.0, dist) * lit;
		aura += exp(-dist * 70.0) * lit;
	}
	// Raios curtos saltando para fora.
	float ang = atan(p.y, p.x);
	for (int k = 0; k < 4; k++) {
		float fk = float(k);
		float a0 = hash(vec2(tick, fk * 7.3)) * 6.2832 - 3.1416;
		float da = ang - a0;
		da = atan(sin(da), cos(da));
		float reach = 0.07 + 0.11 * hash(vec2(tick + 5.0, fk));
		float along = (r - disc) / reach;
		float bend = (vnoise(vec2(r * 46.0, tick + fk * 13.0)) - 0.5) * 0.05 * along;
		float side = abs(da * r - bend);
		float on = step(0.0, along) * (1.0 - smoothstep(0.75, 1.0, along));
		core += smoothstep(0.006, 0.0, side) * on * (1.0 - along * 0.6);
		aura += exp(-side * 80.0) * on * (1.0 - along * 0.5);
	}
	core = clamp(core, 0.0, 1.0);
	aura = clamp(aura, 0.0, 1.0);

	float v = (halo + bloom + aura * 0.55) * outside * power * fade;
	float hot = core * outside * power * fade;
	// Por dentro do disco, um sopro de luz.
	float inner = (1.0 - outside) * (0.08 + 0.1 * pulse) * power;
	// A onda de quando fica pronto.
	float ring2 = disc + (1.0 - burst) * 0.25;
	float wave = smoothstep(0.035, 0.0, abs(r - ring2)) * burst * fade;
	vec3 col = mix(glow.rgb, vec3(1.0, 0.97, 0.9), clamp(v * 0.5 + hot + wave, 0.0, 1.0));
	COLOR = vec4(col, clamp(v + hot + inner + wave, 0.0, 1.0));
}";

	private static Shader _glowShader;

	/// <summary>O shader do brilho, para quem desenha o mesmo disco (o selo do feed).</summary>
	internal static Shader GlowShader => _glowShader ??= new Shader { Code = GlowCode };

	private Icon _icon;
	private Control _cover;
	private ShaderMaterial _glow;
	private float _charge = 1f;
	private int _percent = 100;
	private float _power;
	private float _burst;
	private bool _cooling;

	private static Vector2 Center => new(Side / 2f, Side / 2f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Side, Side + CaptionHeight);

		// O brilho: maior que o disco, por baixo do ícone, somando luz.
		_glowShader ??= new Shader { Code = GlowCode };
		_glow = new ShaderMaterial { Shader = _glowShader };
		_glow.SetShaderParameter("glow", Glow);
		_glow.SetShaderParameter("disc", Radius / GlowSide);
		var light = new ColorRect
		{
			Color = Colors.White,
			Material = _glow,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = Center - new Vector2(GlowSide, GlowSide) / 2f,
			Size = new Vector2(GlowSide, GlowSide)
		};
		AddChild(light);

		_icon = Icon.Make(Kind, 46, Colors.White);
		_icon.Position = Center - new Vector2(23, 23);
		_icon.Size = new Vector2(46, 46);
		AddChild(_icon);

		// Por cima de tudo: aro, número, tecla e nome.
		_cover = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Position = Vector2.Zero,
			Size = new Vector2(Side, Side + CaptionHeight)
		};
		_cover.Draw += DrawCover;
		AddChild(_cover);

		_power = 1f;
		_glow.SetShaderParameter("power", _power);
	}

	public override void _Process(double delta)
	{
		if (_glow == null) return;

		float target = _cooling ? 0f : 1f;
		if (!Mathf.IsEqualApprox(_power, target))
		{
			_power = Mathf.MoveToward(_power, target, (float)delta * 5f);
			_glow.SetShaderParameter("power", _power);
		}

		if (_burst > 0)
		{
			_burst = Mathf.Max(0, _burst - (float)delta * 1.8f);
			_glow.SetShaderParameter("burst", _burst);
		}
	}

	/// <summary>A carga, de 0 a 1. Cheia, o disco acende (e, no instante
	/// em que enche, solta a onda e dá o pulinho).</summary>
	public void SetCharge(float charge)
	{
		if (_cover == null) return;

		charge = Mathf.Clamp(charge, 0f, 1f);
		bool cooling = charge < 1f;
		if (_cooling && !cooling)
		{
			_burst = 1f;
			Motion.Pop(this, 1.2f, 0.3f);
		}

		// Redesenha quando o número muda, não a cada quadro: sem abate, a
		// carga anda 1% a cada 2,4 s.
		int percent = Mathf.FloorToInt(charge * 100f);
		bool changed = cooling != _cooling || percent != _percent;
		_cooling = cooling;
		_charge = charge;
		_percent = percent;
		_icon.Visible = !cooling;
		if (changed)
		{
			_cover.QueueRedraw();
			QueueRedraw();
		}
	}

	// O disco: por baixo do brilho e do ícone.
	public override void _Draw()
	{
		if (_cooling)
		{
			DrawCircle(Center, Radius, new Color(0.02f, 0.03f, 0.05f, 0.6f));
			return;
		}

		DrawCircle(Center, Radius, Hot);

		// Uma face mais clara em cima, à esquerda: o disco é facetado,
		// não uma bola lisa.
		const int steps = 20;
		var face = new Vector2[steps + 1];
		for (int i = 0; i <= steps; i++)
		{
			float angle = Mathf.DegToRad(150f + 180f * i / steps);
			face[i] = Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Radius - 0.5f);
		}
		DrawColoredPolygon(face, Hot.Lightened(0.16f));
	}

	private void DrawCover()
	{
		const float top = -Mathf.Pi / 2f;

		if (_cooling)
		{
			// O aro fecha no sentido horário, a partir do topo.
			float done = _charge;
			_cover.DrawArc(Center, Radius - 2.5f, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.2f), 5f, true);
			if (done > 0.003f)
				_cover.DrawArc(Center, Radius - 2.5f, top, top + Mathf.Tau * done, Mathf.Max(4, Mathf.CeilToInt(64 * done)),
					UiTokens.TextPrimary, 5f, true);

			// A carga, com o "%" menor ao lado do número.
			string number = _percent.ToString();
			Font font = HudStyle.Slanted(UiFonts.NumericDisplay);
			const int fontSize = 36;
			const int signSize = 18;
			Vector2 text = font.GetStringSize(number, HorizontalAlignment.Left, -1, fontSize);
			Vector2 sign = font.GetStringSize("%", HorizontalAlignment.Left, -1, signSize);
			var at = new Vector2(Center.X - (text.X + sign.X) / 2f, Center.Y + fontSize * 0.36f);
			var signAt = at + new Vector2(text.X + 1f, 0f);
			var shadow = new Color(0, 0, 0, 0.7f);
			_cover.DrawString(font, at + new Vector2(1.5f, 1.5f), number, HorizontalAlignment.Left, -1, fontSize, shadow);
			_cover.DrawString(font, at, number, HorizontalAlignment.Left, -1, fontSize, UiTokens.TextPrimary);
			_cover.DrawString(font, signAt + new Vector2(1f, 1f), "%", HorizontalAlignment.Left, -1, signSize, shadow);
			_cover.DrawString(font, signAt, "%", HorizontalAlignment.Left, -1, signSize, UiTokens.TextSecondary);
		}
		else
		{
			_cover.DrawArc(Center, Radius - 1.5f, 0, Mathf.Tau, 64, new Color(1f, 0.97f, 0.9f), 3f, true);
		}

		// A tecla, numa ficha que morde a beira de baixo do disco.
		Font keyFont = UiFonts.Bold;
		const int keySize = 13;
		Vector2 key = keyFont.GetStringSize(Key, HorizontalAlignment.Center, -1, keySize);
		var chip = new Rect2(Center.X - key.X / 2f - 7, Side - 9, key.X + 14, 19);
		_cover.DrawRect(chip, new Color(0.03f, 0.04f, 0.06f, 0.92f));
		_cover.DrawRect(chip, _cooling ? new Color(1, 1, 1, 0.25f) : Hot, false, 1.5f);
		_cover.DrawString(keyFont, new Vector2(chip.Position.X + 7, chip.Position.Y + 14), Key,
			HorizontalAlignment.Left, -1, keySize, _cooling ? UiTokens.TextSecondary : Colors.White);

		// O nome, embaixo.
		Font titleFont = HudStyle.Slanted(UiFonts.DisplaySemi);
		const int titleSize = 15;
		string title = Loc.T(Title);
		Vector2 size = titleFont.GetStringSize(title, HorizontalAlignment.Center, -1, titleSize);
		var where = new Vector2(Center.X - size.X / 2f, Side + CaptionHeight - 6);
		_cover.DrawString(titleFont, where + new Vector2(1, 1), title, HorizontalAlignment.Left, -1, titleSize, new Color(0, 0, 0, 0.8f));
		_cover.DrawString(titleFont, where, title, HorizontalAlignment.Left, -1, titleSize,
			_cooling ? UiTokens.TextSecondary : UiTokens.TextPrimary);
	}
}
