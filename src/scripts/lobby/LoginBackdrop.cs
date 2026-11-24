using Godot;
using System.Collections.Generic;

/// <summary>
/// O fundo da tela de login (pedido do Jhon, 20/11: "a tela de login
/// precisa ser mais elaborada; o fundo deve mexer, com efeito nas
/// partículas", com o login do Overwatch de referência).
///
/// Duas camadas. Embaixo, por shader, um **campo de polígonos** azul-noite:
/// uma malha de triângulos cuja altura ondula devagar, cada face acesa por
/// uma luz que gira, com o fio das arestas brilhando de leve; um clarão
/// frio no alto, a brasa quente embaixo (a cor do lobo) e a vinheta. Por
/// cima, as **partículas**, desenhadas aqui: brasas douradas que sobem
/// tremulando, poeira fria que flutua e umas manchas grandes e borradas
/// de luz (o "bokeh"), em três profundidades.
///
/// O fundo segue o mouse (paralaxe: o perto anda mais que o longe) e as
/// partículas se afastam do cursor; <see cref="Burst"/> solta um punhado
/// de faíscas (o clique em Entrar). O estado é estático: a tela de login
/// se refaz a cada mudança e o fundo continua de onde estava.
/// </summary>
public partial class LoginBackdrop : Control
{
	private const string Code = @"
shader_type canvas_item;
uniform float aspect = 1.777;
uniform vec2 parallax = vec2(0.0);
uniform vec4 deep : source_color = vec4(0.020, 0.035, 0.060, 1.0);
uniform vec4 steel : source_color = vec4(0.120, 0.230, 0.330, 1.0);
uniform vec4 cool : source_color = vec4(0.200, 0.480, 0.680, 1.0);
uniform vec4 warm : source_color = vec4(0.850, 0.420, 0.130, 1.0);
uniform vec4 gold : source_color = vec4(0.950, 0.720, 0.310, 1.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// A altura de um vértice da malha: ondula devagar.
float height(vec2 k) {
	vec2 q = k * 0.23;
	return vnoise(q + vec2(TIME * 0.045, TIME * 0.02)) * 2.2
		+ vnoise(q * 2.3 - vec2(TIME * 0.03, -TIME * 0.05)) * 0.9;
}

const float F2 = 0.36602540;
const float G2 = 0.21132487;
vec2 unskew(vec2 k) { return k - (k.x + k.y) * G2; }

void fragment() {
	vec2 uv = UV;
	vec2 wide = uv * vec2(aspect, 1.0);
	vec3 col = mix(vec3(0.040, 0.075, 0.120), deep.rgb, clamp(uv.y * 1.05, 0.0, 1.0));

	// A malha de triângulos (a grade do simplex), com paralaxe.
	vec2 p = (wide + parallax * 0.035) * 8.5 + vec2(3.7, 1.3);
	vec2 s = floor(p + (p.x + p.y) * F2);
	vec2 x0 = p - unskew(s);
	vec2 i1 = x0.x > x0.y ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
	vec2 k0 = s, k1 = s + i1, k2 = s + vec2(1.0);
	vec2 u0 = unskew(k0), u1 = unskew(k1), u2 = unskew(k2);
	float h0 = height(k0), h1 = height(k1), h2 = height(k2);
	vec3 n = normalize(cross(vec3(u1 - u0, h1 - h0), vec3(u2 - u0, h2 - h0)));
	if (n.z < 0.0) n = -n;

	// A luz gira devagar; cada face pega a sua parte.
	float t = TIME * 0.07;
	vec3 L = normalize(vec3(cos(t) * 0.7, sin(t) * 0.5 - 0.35, 0.62));
	float lit = clamp(dot(n, L), 0.0, 1.0);
	lit = pow(lit, 2.2);
	vec2 centroid = (u0 + u1 + u2) / 3.0;
	float grain = hash(k0 + i1 * 7.0) * 0.5 + 0.5;

	// Uma faixa de luz passa pela malha de tempos em tempos.
	float sweep = centroid.x * 0.55 - centroid.y * 0.25 - mod(TIME * 1.6, 46.0) + 8.0;
	float band = exp(-sweep * sweep * 0.08);

	// Onde fica o formulário, a malha apaga; nas beiras, aparece.
	float edge = smoothstep(0.18, 0.75, length((uv - vec2(0.5, 0.5)) * vec2(aspect * 0.55, 1.0)));
	float field = mix(0.2, 1.0, edge);
	vec3 face = mix(deep.rgb * 0.8, steel.rgb * 1.25, lit * grain) + cool.rgb * band * lit * 0.5;
	col = mix(col, face, 0.7 * field);

	// O fio das arestas (baricêntricas do triângulo).
	vec2 v0 = u1 - u0, v1 = u2 - u0, v2 = p - u0;
	float d00 = dot(v0, v0), d01 = dot(v0, v1), d11 = dot(v1, v1), d20 = dot(v2, v0), d21 = dot(v2, v1);
	float den = d00 * d11 - d01 * d01;
	float bv = (d11 * d20 - d01 * d21) / den, bw = (d00 * d21 - d01 * d20) / den;
	vec3 bary = vec3(1.0 - bv - bw, bv, bw);
	float e = min(min(bary.x, bary.y), bary.z);
	float line = 1.0 - smoothstep(0.0, fwidth(e) * 1.4, e);
	col += mix(cool.rgb, gold.rgb, band * 0.7) * line * (0.03 + band * 0.2 + lit * 0.05) * field;

	// O clarão frio do alto e a brasa de baixo, que respira.
	float sky = exp(-distance(wide, vec2(0.5 * aspect + parallax.x * 0.03, -0.3)) * 1.35);
	col += cool.rgb * sky * 0.5;
	float breathe = 0.85 + 0.15 * sin(TIME * 0.6);
	float ember = exp(-distance(wide, vec2(0.88 * aspect, 1.2)) * 1.55);
	col += warm.rgb * ember * 0.55 * breathe;
	float ember2 = exp(-distance(wide, vec2(0.08 * aspect, 1.25)) * 2.2);
	col += warm.rgb * ember2 * 0.18 * breathe;

	// Névoa que anda.
	float fog = vnoise(wide * 1.4 + vec2(TIME * 0.015, TIME * 0.007)) * 0.6
		+ vnoise(wide * 3.3 - vec2(TIME * 0.025, 0.0)) * 0.4;
	col += cool.rgb * (fog - 0.45) * 0.08;

	vec2 q = uv - 0.5;
	col *= 1.0 - dot(q, q) * 1.05;
	COLOR = vec4(max(col, vec3(0.0)), 1.0);
}";

	private enum Kind { Ember, Dust, Bokeh, Spark }

	private struct Mote
	{
		public Kind Kind;
		public Vector2 Pos;      // 0..1 na tela (o x vai além: sai e volta)
		public Vector2 Vel;      // px/s
		public Vector2 Push;     // o empurrão do cursor, que se desfaz
		public float Size, Depth, Phase, Life, Age;
	}

	private const int Embers = 110, Dust = 110, Bokeh = 16;

	private static readonly List<Mote> Motes = new();
	private static readonly RandomNumberGenerator Rng = new();
	private static Texture2D _soft;
	private static Vector2 _parallax;

	private ShaderMaterial _material;
	private Control _particles;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		UiKit.Fill(this);

		_material = new ShaderMaterial { Shader = new Shader { Code = Code } };
		AddChild(UiKit.Fill(new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Material = _material }));

		_particles = UiKit.Fill(new Control { MouseFilter = MouseFilterEnum.Ignore });
		_particles.Draw += DrawMotes;
		AddChild(_particles);

		_soft ??= SoftDot();
		if (Motes.Count == 0) Seed();
		Resized += () => _material.SetShaderParameter("aspect", Size.Y > 0 ? Size.X / Size.Y : 1.777f);
	}

	/// <summary>Um punhado de faíscas saindo de um ponto da tela (em pixels).</summary>
	public static void Burst(Vector2 at, Vector2 screen, int count = 28)
	{
		if (screen.X <= 0 || screen.Y <= 0) return;
		for (int i = 0; i < count; i++)
		{
			float angle = Rng.RandfRange(-Mathf.Pi, 0f) + Rng.RandfRange(-0.4f, 0.4f);
			float speed = Rng.RandfRange(80f, 340f);
			Motes.Add(new Mote
			{
				Kind = Kind.Spark,
				Pos = at / screen,
				Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
				Size = Rng.RandfRange(1.5f, 3.2f),
				Depth = 1f,
				Phase = Rng.Randf() * Mathf.Tau,
				Life = Rng.RandfRange(0.6f, 1.3f)
			});
		}
	}

	private static void Seed()
	{
		for (int i = 0; i < Embers; i++) Motes.Add(NewEmber(Rng.Randf()));
		for (int i = 0; i < Dust; i++) Motes.Add(NewDust());
		for (int i = 0; i < Bokeh; i++) Motes.Add(NewBokeh());
	}

	private static Mote NewEmber(float y)
	{
		float depth = Rng.RandfRange(0.3f, 1f);
		return new Mote
		{
			Kind = Kind.Ember,
			// Mais delas sobe pelos lados (onde a brasa está), poucas pelo meio.
			Pos = new Vector2(Rng.Randf() < 0.65f ? (Rng.Randf() < 0.6f ? Rng.RandfRange(0.62f, 1.05f) : Rng.RandfRange(-0.05f, 0.3f)) : Rng.Randf(), y),
			Vel = new Vector2(Rng.RandfRange(-8f, 14f), -Rng.RandfRange(18f, 55f)) * depth,
			Size = Rng.RandfRange(1.6f, 3.6f) * (0.5f + depth * 0.8f),
			Depth = depth,
			Phase = Rng.Randf() * Mathf.Tau,
			Life = 1f
		};
	}

	private static Mote NewDust()
	{
		float depth = Rng.RandfRange(0.2f, 0.9f);
		return new Mote
		{
			Kind = Kind.Dust,
			Pos = new Vector2(Rng.Randf(), Rng.Randf()),
			Vel = new Vector2(Rng.RandfRange(-6f, 6f), Rng.RandfRange(-5f, 3f)) * depth,
			Size = Rng.RandfRange(0.8f, 1.8f) * (0.6f + depth * 0.6f),
			Depth = depth,
			Phase = Rng.Randf() * Mathf.Tau,
			Life = 1f
		};
	}

	private static Mote NewBokeh()
	{
		float depth = Rng.RandfRange(0.15f, 0.6f);
		return new Mote
		{
			Kind = Kind.Bokeh,
			Pos = new Vector2(Rng.Randf(), Rng.Randf()),
			Vel = new Vector2(Rng.RandfRange(-4f, 4f), Rng.RandfRange(-6f, -1f)),
			Size = Rng.RandfRange(28f, 90f),
			Depth = depth,
			Phase = Rng.Randf() * Mathf.Tau,
			Life = 1f
		};
	}

	/// <summary>Um ponto de luz macio (o miolo forte, a borda some), para
	/// todas as partículas.</summary>
	private static Texture2D SoftDot()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(1, 1, 1, 1));
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		gradient.AddPoint(0.25f, new Color(1, 1, 1, 0.55f));
		gradient.AddPoint(0.55f, new Color(1, 1, 1, 0.12f));
		return new GradientTexture2D
		{
			Gradient = gradient, Width = 64, Height = 64,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
		};
	}

	public override void _Process(double delta)
	{
		float dt = Mathf.Min((float)delta, 0.05f);
		Vector2 size = Size;
		if (size.X <= 0 || size.Y <= 0) return;

		// A paralaxe segue o mouse, com atraso: o fundo "respira" junto.
		Vector2 mouse = GetLocalMousePosition();
		bool inside = new Rect2(Vector2.Zero, size).HasPoint(mouse);
		Vector2 aim = inside ? (mouse / size - new Vector2(0.5f, 0.5f)) * 2f : Vector2.Zero;
		_parallax = _parallax.Lerp(aim, 1f - Mathf.Exp(-dt * 2.2f));
		_material.SetShaderParameter("parallax", _parallax);

		float time = (float)Time.GetTicksMsec() / 1000f;
		for (int i = Motes.Count - 1; i >= 0; i--)
		{
			Mote m = Motes[i];
			Vector2 px = m.Pos * size + Offset(m, size);

			// O cursor empurra o que está perto (o perto mais que o longe).
			if (inside)
			{
				Vector2 away = px - mouse;
				float d = away.Length(), reach = 140f;
				if (d < reach && d > 0.5f)
				{
					float force = (1f - d / reach) * (1f - d / reach) * 900f * (0.4f + m.Depth * 0.6f);
					m.Push += away / d * force * dt;
				}
			}
			m.Push *= Mathf.Exp(-dt * 2.5f);

			Vector2 vel = m.Vel;
			if (m.Kind == Kind.Ember) vel.X += Mathf.Sin(time * 1.3f + m.Phase) * 10f * m.Depth;
			if (m.Kind == Kind.Spark) { m.Vel *= Mathf.Exp(-dt * 2.2f); m.Vel.Y += 160f * dt; }
			m.Pos += (vel + m.Push) * dt / size;
			m.Age += dt;

			bool gone = m.Kind == Kind.Spark ? m.Age > m.Life
				: m.Pos.Y < -0.08f || m.Pos.Y > 1.12f || m.Pos.X < -0.15f || m.Pos.X > 1.15f;
			if (gone)
			{
				switch (m.Kind)
				{
					case Kind.Spark: Motes.RemoveAt(i); continue;
					case Kind.Ember: m = NewEmber(Rng.RandfRange(1.02f, 1.08f)); break;
					case Kind.Dust:
						m = NewDust();
						m.Pos = new Vector2(m.Vel.X > 0 ? -0.05f : 1.05f, Rng.Randf());
						break;
					default:
						m = NewBokeh();
						m.Pos = new Vector2(Rng.Randf(), 1.1f);
						break;
				}
			}
			Motes[i] = m;
		}
		_particles.QueueRedraw();
	}

	private static Vector2 Offset(in Mote m, Vector2 size) => -_parallax * size * 0.022f * m.Depth;

	private void DrawMotes()
	{
		Vector2 size = Size;
		float time = (float)Time.GetTicksMsec() / 1000f;
		Color gold = new("f3b950"), ember = new("ff7a2a"), cold = new("9fd4ff");
		foreach (Mote m in Motes)
		{
			Vector2 at = m.Pos * size + Offset(m, size);
			float flicker = 0.65f + 0.35f * Mathf.Sin(time * (m.Kind == Kind.Ember ? 7f : 1.5f) + m.Phase);
			Color c;
			float r;
			switch (m.Kind)
			{
				case Kind.Ember:
				{
					// Sobe e esfria: do laranja ao dourado, e apaga perto do alto.
					float fade = Mathf.Clamp(m.Pos.Y * 1.4f, 0f, 1f);
					c = ember.Lerp(gold, 1f - m.Pos.Y);
					c.A = Mathf.Min(1f, (0.5f + 0.6f * m.Depth) * flicker * fade);
					r = m.Size;
					// O halo e o miolo.
					_particles.DrawTextureRect(_soft, new Rect2(at - Vector2.One * r * 5f, Vector2.One * r * 10f), false, new Color(c, c.A * 0.45f));
					_particles.DrawTextureRect(_soft, new Rect2(at - Vector2.One * r * 1.4f, Vector2.One * r * 2.8f), false, new Color(c.Lightened(0.4f), c.A));
					continue;
				}
				case Kind.Spark:
				{
					float left = 1f - m.Age / m.Life;
					c = gold.Lerp(ember, 1f - left);
					c.A = left;
					r = m.Size * (0.6f + left * 0.6f);
					_particles.DrawLine(at, at - m.Vel * 0.025f, new Color(c, c.A * 0.6f), r * 0.8f, true);
					_particles.DrawTextureRect(_soft, new Rect2(at - Vector2.One * r * 4f, Vector2.One * r * 8f), false, new Color(c, c.A * 0.5f));
					continue;
				}
				case Kind.Dust:
					c = new Color(cold, (0.14f + 0.3f * m.Depth) * flicker);
					r = m.Size * 2.2f;
					break;
				default:
					c = new Color(m.Phase > Mathf.Pi ? cold : gold, 0.045f + 0.035f * flicker);
					r = m.Size;
					break;
			}
			_particles.DrawTextureRect(_soft, new Rect2(at - Vector2.One * r, Vector2.One * r * 2f), false, c);
		}
	}
}
