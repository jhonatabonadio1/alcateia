using Godot;

/// <summary>
/// A vinheta da Initech Games, a primeira tela do jogo (Jhon, 21/11: "tela
/// preta, aparece a logo da produtora com efeito profissional, transição e
/// som, e enfim o jogo").
///
/// Preto. O emblema vem de longe enquanto o som sobe e bate no impacto, com
/// o brilho dourado se abrindo, um risco de luz e um tremor curto; INITECH
/// sobe, GAMES abre do meio com as duas linhas; uma faixa de luz passa pela
/// logo; tudo some no preto e o menu entra. O menu carrega por trás
/// enquanto isso. Qualquer tecla ou clique pula a vinheta.
///
/// A arte é a logo do Jhon recortada em três partes (assets/ui/estudio:
/// emblema, INITECH, GAMES), nas posições da logo inteira; o som,
/// assets/audio/vinheta_initech.wav (sintetizado: tools/estudio/som.py),
/// com o impacto em <see cref="Impact"/>.
/// </summary>
public partial class StudioIntro : Control
{
	private const string Dir = "res://assets/ui/estudio/";
	private const string SoundPath = "res://assets/audio/vinheta_initech.wav";

	// O tempo da vinheta, em segundos (o som foi feito nesse tempo).
	private const float Impact = 1.0f;
	private const float FadeOut = 3.3f;
	private const float End = 3.95f;

	// As partes na logo inteira, em frações da arte original (1800 × 1632).
	private const float LogoAspect = 1800f / 1632f;
	private static readonly Rect2 EmblemRect = new(0.21667f, 0.02512f, 0.59000f, 0.68934f);
	private static readonly Rect2 NameRect = new(0.02278f, 0.73836f, 0.95500f, 0.15809f);
	private static readonly Rect2 GamesRect = new(0.03111f, 0.90993f, 0.94000f, 0.06556f);
	// O brilho do emblema (o emblema desfocado, em ouro), com a sobra do desfoque.
	private static readonly Rect2 GlowRect = new(0.13889f, -0.06066f, 0.74556f, 0.86091f);

	private static readonly Color Gold = new("fec803");

	private const string LayerShader = @"
shader_type canvas_item;
// A parte na logo inteira (x, y, largura, altura), para a faixa de luz
// atravessar a logo como uma peça só.
uniform vec4 part = vec4(0.0, 0.0, 1.0, 1.0);
uniform float sweep = -1.0;   // onde está a faixa de luz, de 0 a 1 na logo
uniform float reveal = 1.0;   // abre do meio para os lados
void fragment() {
	vec4 c = COLOR;
	vec2 p = part.xy + UV * part.zw;
	float band = p.x + (p.y - 0.5) * 0.45 - sweep;
	float light = exp(-band * band * 150.0);
	c.rgb += vec3(1.0, 0.95, 0.82) * light * 1.05 * c.a;
	c.a *= step(abs(UV.x - 0.5) * 2.0, reveal);
	COLOR = c;
}";

	private Control _logo;
	private TextureRect _emblem, _name, _games, _glow, _streak, _halo;
	private ShaderMaterial _emblemFx, _nameFx, _gamesFx;
	private ColorRect _fade;
	private AudioStreamPlayer _sound;
	private float _t;
	private bool _skipping, _leaving;

	public override void _Ready()
	{
		string[] args = OS.GetCmdlineUserArgs();
		// O servidor de partida troca esta cena sozinho (Inicio); robô, jogo
		// sem tela e quem pede --sem-vinheta vão direto ao menu.
		if (Inicio.IsServer(args)) return;
		if (DisplayServer.GetName() == "headless" || System.Array.Exists(args, a => a.StartsWith("--robo=") || a == "--sem-vinheta"))
		{
			Callable.From(() => GetTree().ChangeSceneToFile(GameMenu.MenuScene)).CallDeferred();
			return;
		}

		ResourceLoader.LoadThreadedRequest(GameMenu.MenuScene);
		Input.MouseMode = Input.MouseModeEnum.Hidden;
		Build();
		Resized += Layout;
		Layout();

		Settings.ApplyAudio();
		_sound = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(SoundPath), Bus = Sfx.BusMusic };
		AddChild(_sound);
		_sound.Play();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_skipping || _leaving) return;
		if (e is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true } or InputEventJoypadButton { Pressed: true })
		{
			_skipping = true;
			if (_t < FadeOut) _t = FadeOut;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (_logo == null) return;
		_t += (float)delta * (_skipping ? 2.2f : 1f);
		float t = _t;

		// Fundo: um brilho quente atrás do emblema, que acende no impacto.
		_halo.Modulate = new Color(1, 1, 1, t < Impact ? 0f : Mathf.Lerp(1f, 0.28f, Smooth(Impact, Impact + 1.4f, t)));

		// O emblema vem de longe e bate no impacto.
		float a = Clamp01((t - 0.45f) / (Impact - 0.45f));
		float s = t < Impact ? 1f + 0.18f * (1f - a) * (1f - a) : 1f;
		_emblem.Modulate = new Color(1, 1, 1, t < Impact ? Mathf.Pow(a, 1.5f) * 0.85f : 1f);
		_emblem.Scale = new Vector2(s, s);

		// O impacto: o emblema brilha e o brilho se abre, o risco de luz e um tremor.
		float k = t - Impact;
		float g = k is >= 0 and < 0.9f ? 1f - k / 0.9f : 0f;
		_glow.Modulate = new Color(1, 1, 1, g * g);
		_glow.Scale = Vector2.One * (1f + 0.16f * EaseOut(1f - g));
		float st = k is >= 0 and < 0.6f ? k / 0.6f : 1f;
		_streak.Modulate = new Color(1, 1, 1, k >= 0 ? 0.85f * (1f - st) : 0f);
		_streak.Scale = new Vector2(0.15f + 1.35f * EaseOut(st), 1f);
		Vector2 shake = k is >= 0 and < 0.28f
			? new Vector2(Mathf.Sin(k * 97f), Mathf.Cos(k * 83f)) * 7f * (1f - k / 0.28f)
			: Vector2.Zero;
		_logo.Position = _home + shake;

		// INITECH sobe; GAMES abre do meio.
		float n = EaseOut(Clamp01((t - 1.08f) / 0.47f));
		_name.Modulate = new Color(1, 1, 1, n);
		_name.Position = _nameHome + new Vector2(0, 26f * (1f - n) * _scale);
		float r = EaseOut(Clamp01((t - 1.3f) / 0.55f));
		_games.Modulate = new Color(1, 1, 1, Mathf.Min(1f, r * 1.6f));
		_gamesFx.SetShaderParameter("reveal", r);

		// A faixa de luz atravessa a logo inteira.
		float sweep = Mathf.Lerp(-0.3f, 1.3f, Smooth(1.45f, 2.3f, t));
		foreach (ShaderMaterial fx in new[] { _emblemFx, _nameFx, _gamesFx }) fx.SetShaderParameter("sweep", sweep);

		// Some no preto, e o menu entra.
		_fade.Color = new Color(0, 0, 0, Smooth(FadeOut, FadeOut + 0.55f, t));
		if (_sound != null && _skipping) _sound.VolumeDb = Mathf.Lerp(0f, -40f, Smooth(FadeOut, FadeOut + 0.55f, t));
		if (t >= End) Leave();
	}

	private void Leave()
	{
		if (_leaving) return;
		ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(GameMenu.MenuScene);
		if (status == ResourceLoader.ThreadLoadStatus.InProgress) return; // o menu ainda carrega: fica no preto
		_leaving = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		if (status == ResourceLoader.ThreadLoadStatus.Loaded)
			GetTree().ChangeSceneToPacked((PackedScene)ResourceLoader.LoadThreadedGet(GameMenu.MenuScene));
		else
			GetTree().ChangeSceneToFile(GameMenu.MenuScene);
	}

	// ------------------------------------------------------------------ montagem

	private Vector2 _home, _nameHome;
	private float _scale = 1f;

	private void Build()
	{
		var black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
		Fill(black);

		var add = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

		_halo = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = Ramp(new Color(Gold, 0.34f), new Color(Gold, 0.2f), new Color(Gold, 0.09f), new Color(Gold, 0.025f), new Color(Gold, 0f)),
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f),
				FillTo = new Vector2(1f, 0.5f),
				Width = 512,
				Height = 512
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Material = add,
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(_halo);

		_logo = new Control { MouseFilter = MouseFilterEnum.Ignore };
		AddChild(_logo);

		_glow = Part("emblema_brilho", GlowRect, null);
		_glow.Material = add;
		_emblem = Part("emblema", EmblemRect, _emblemFx = Fx(EmblemRect));
		_name = Part("initech", NameRect, _nameFx = Fx(NameRect));
		_games = Part("games", GamesRect, _gamesFx = Fx(GamesRect));
		_gamesFx.SetShaderParameter("reveal", 0f);
		_name.Modulate = _games.Modulate = new Color(1, 1, 1, 0);
		_emblem.Modulate = _glow.Modulate = new Color(1, 1, 1, 0);

		// O risco de luz do impacto: uma linha fina e uma faixa larga e fraca.
		var line = Ramp(new Color(1, 0.95f, 0.8f, 0f), new Color(1, 0.95f, 0.8f, 1f), new Color(1, 0.95f, 0.8f, 0f));
		_streak = new TextureRect
		{
			Texture = new GradientTexture2D { Gradient = line, Width = 512, Height = 4 },
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Material = add,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0)
		};
		AddChild(_streak);

		_fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore };
		Fill(_fade);
	}

	private TextureRect Part(string file, Rect2 at, ShaderMaterial fx)
	{
		var part = new TextureRect
		{
			Texture = GD.Load<Texture2D>(Dir + file + ".png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			// A arte é grande (1800 px) e aparece menor: com mipmaps não serrilha.
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			Material = fx,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = at.Position.X,
			AnchorTop = at.Position.Y,
			AnchorRight = at.End.X,
			AnchorBottom = at.End.Y
		};
		_logo.AddChild(part);
		return part;
	}

	private static ShaderMaterial Fx(Rect2 at)
	{
		_shader ??= new Shader { Code = LayerShader };
		var fx = new ShaderMaterial { Shader = _shader };
		fx.SetShaderParameter("part", new Vector4(at.Position.X, at.Position.Y, at.Size.X, at.Size.Y));
		return fx;
	}

	private static Shader _shader;

	/// <summary>A logo no meio da tela: 44% da altura, sem passar de 70% da largura.</summary>
	private void Layout()
	{
		if (_logo == null) return;
		Vector2 view = Size;
		float h = Mathf.Min(view.Y * 0.44f, view.X * 0.70f / LogoAspect);
		var size = new Vector2(h * LogoAspect, h);
		_scale = h / 600f;
		_logo.Size = size;
		_home = (view - size) / 2f;
		_logo.Position = _home;

		foreach (Node child in _logo.GetChildren())
			if (child is Control c) c.PivotOffset = c.Size / 2f;
		_nameHome = _name.Position;

		// O brilho do fundo e o risco de luz, centrados no emblema.
		Vector2 emblemCenter = _home + new Vector2((EmblemRect.Position.X + EmblemRect.Size.X / 2f) * size.X,
			(EmblemRect.Position.Y + EmblemRect.Size.Y / 2f) * size.Y);
		_halo.Size = new Vector2(h * 2.4f, h * 2.4f);
		_halo.Position = emblemCenter - _halo.Size / 2f;
		_streak.Size = new Vector2(view.X * 0.9f, Mathf.Max(2f, h * 0.008f));
		_streak.Position = new Vector2(view.X * 0.05f, emblemCenter.Y - _streak.Size.Y / 2f);
		_streak.PivotOffset = _streak.Size / 2f;
	}

	// ------------------------------------------------------------------ peças

	private void Fill(Control c)
	{
		c.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(c);
	}

	private static Gradient Ramp(params Color[] colors)
	{
		var g = new Gradient();
		float[] offsets = new float[colors.Length];
		for (int i = 0; i < colors.Length; i++) offsets[i] = colors.Length == 1 ? 0 : i / (float)(colors.Length - 1);
		g.Offsets = offsets;
		g.Colors = colors;
		return g;
	}

	private static float Clamp01(float x) => Mathf.Clamp(x, 0f, 1f);
	private static float EaseOut(float x) => 1f - Mathf.Pow(1f - x, 3f);
	private static float Smooth(float a, float b, float t) => Mathf.SmoothStep(a, b, t);
}
