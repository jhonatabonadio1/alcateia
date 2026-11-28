using Godot;
using System.Linq;

/// <summary>
/// Os mapas passando, na tela de login (27/11): a foto de cada mapa em
/// tela cheia, andando devagar, cortada na diagonal do lado do painel e
/// puxada para o azul da noite — atrás dela continua o fundo vivo. A cada
/// <see cref="Every"/> s a foto troca, e embaixo à direita vai o nome do
/// lugar, como numa placa.
///
/// É cenário: não recebe clique.
/// </summary>
public partial class MapReel : Control
{
	/// <summary>Quanto tempo cada mapa fica, em segundos.</summary>
	[Export] public double Every = 7.0;

	// O corte na diagonal e o tom: a foto some para a esquerda numa beira
	// inclinada, e a cor dela vai para a paleta dos menus.
	private const string Code = @"
shader_type canvas_item;
uniform float show = 1.0;
uniform vec4 night : source_color = vec4(0.05, 0.09, 0.15, 1.0);
void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float edge = UV.x - (1.0 - UV.y) * 0.16;
	float mask = smoothstep(0.02, 0.34, edge);
	// Um fio claro acompanha o corte.
	float line = smoothstep(0.012, 0.0, abs(edge - 0.02)) * 0.0;
	float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
	vec3 graded = mix(tex.rgb, night.rgb * (0.5 + lum * 1.9), 0.42);
	// O alto e o chão escurecem: a foto é fundo, não cartaz.
	graded *= 0.5 + 0.5 * smoothstep(1.0, 0.25, UV.y) * smoothstep(-0.2, 0.3, UV.y);
	COLOR = vec4(graded + line, mask * show * 0.9);
}";

	private MapInfo[] _maps;
	private int _index = -1;
	private double _held;
	private TextureRect _front;
	private VBoxContainer _plate;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
		_maps = Maps.All.Where(m => ResourceLoader.Exists(m.Picture)).ToArray();

		_plate = UiKit.Column(0);
		_plate.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		_plate.GrowHorizontal = GrowDirection.Begin;
		_plate.GrowVertical = GrowDirection.Begin;
		_plate.OffsetRight = -UiTokens.S6;
		_plate.OffsetBottom = -UiTokens.S6;
		_plate.MouseFilter = MouseFilterEnum.Ignore;
		_plate.ZIndex = 1;
		AddChild(_plate);

		Next();
	}

	public override void _Process(double delta)
	{
		if (_maps.Length < 2) return;
		_held += delta;
		if (_held >= Every) Next();
	}

	private void Next()
	{
		if (_maps.Length == 0) return;
		_held = 0;
		_index = (_index + 1) % _maps.Length;
		MapInfo map = _maps[_index];

		// A de antes some por baixo da nova.
		if (_front != null && IsInstanceValid(_front))
		{
			TextureRect old = _front;
			var oldMaterial = (ShaderMaterial)old.Material;
			Tween fade = old.CreateTween();
			fade.TweenMethod(Callable.From<float>(v => oldMaterial.SetShaderParameter("show", v)), 1f, 0f, 1.4f);
			fade.TweenCallback(Callable.From(old.QueueFree));
		}

		var material = new ShaderMaterial { Shader = new Shader { Code = Code } };
		material.SetShaderParameter("show", 0f);
		_front = new TextureRect
		{
			Texture = GD.Load<Texture2D>(map.Picture),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
			Material = material
		};
		UiKit.Fill(_front);
		AddChild(_front);
		MoveChild(_front, 0);

		TextureRect shown = _front;
		shown.PivotOffset = Size / 2f;
		Tween tween = shown.CreateTween().SetParallel();
		tween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("show", v)), 0f, 1f, 1.4f);
		// Chega mais perto o tempo todo em que fica.
		shown.Scale = Vector2.One * 1.02f;
		tween.TweenProperty(shown, "scale", Vector2.One * 1.1f, Every + 1.5);

		// A placa do lugar.
		UiKit.Clear(_plate);
		Label kicker = Deco.Kicker(Loc.F("Campo de caça {0:00} / {1:00}", _index + 1, _maps.Length), UiTokens.Accent);
		kicker.HorizontalAlignment = HorizontalAlignment.Right;
		_plate.AddChild(kicker);
		Label name = Deco.Big(map.Name, 84);
		name.HorizontalAlignment = HorizontalAlignment.Right;
		_plate.AddChild(name);
		Label size = UiKit.Caption(map.Size, UiTokens.TextSecondary);
		size.HorizontalAlignment = HorizontalAlignment.Right;
		_plate.AddChild(size);
		Motion.FadeIn(_plate, 0.9f, 0.3f);
	}
}
