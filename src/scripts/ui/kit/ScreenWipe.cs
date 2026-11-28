using Godot;
using System;

/// <summary>
/// A cortina das trocas grandes (Jhon, 27/11: "quero animações de
/// transições"): lâminas na diagonal atravessam a tela — um fio de papel na
/// frente, uma faixa de ouro atrás dele e o corpo escuro —, cobrem tudo, a
/// tela troca por baixo e elas saem pelo outro lado.
///
/// É para mudança de lugar (do login para o jogo, do menu para a partida),
/// não para trocar de aba: aba só desliza (<see cref="Motion.Enter"/>). Uma
/// cortina a cada clique cansaria.
///
/// Uso: <c>ScreenWipe.Play(camada, () => trocar())</c>. A troca acontece
/// com a tela coberta.
/// </summary>
public partial class ScreenWipe : ColorRect
{
	/// <summary>Tempo para cobrir, e depois para descobrir.</summary>
	public const float In = 0.26f, Out = 0.34f;

	private const string Code = @"
shader_type canvas_item;
uniform float cover = 0.0;   // 0..1: a frente das lâminas atravessando
uniform float reveal = 0.0;  // 0..1: a cauda atravessando
uniform float aspect = 1.777;
uniform vec4 body : source_color = vec4(0.031, 0.047, 0.071, 1.0);
uniform vec4 gold : source_color = vec4(0.953, 0.725, 0.314, 1.0);
uniform vec4 paper : source_color = vec4(0.925, 0.902, 0.855, 1.0);

void fragment() {
	// A diagonal: o alto chega antes.
	float d = (UV.x * aspect + (1.0 - UV.y) * 0.42) / (aspect + 0.42);
	float front = mix(-0.2, 1.25, cover);
	float back = mix(-0.25, 1.2, reveal);
	// As três lâminas, cada uma um pouco atrás da outra.
	float a_paper = step(d, front) * step(back, d);
	float a_gold = step(d, front - 0.012) * step(back + 0.012, d);
	float a_body = step(d, front - 0.07) * step(back + 0.07, d);
	vec3 col = paper.rgb;
	col = mix(col, gold.rgb, a_gold);
	// O corpo: escuro, com riscos finos na diagonal que andam com ele.
	float lines = smoothstep(0.92, 1.0, fract((UV.x * aspect - UV.y * 0.42) * 26.0 + cover * 3.0));
	vec3 dark = body.rgb + gold.rgb * lines * 0.035;
	col = mix(col, dark, a_body);
	COLOR = vec4(col, a_paper);
}";

	private ShaderMaterial _material;

	/// <summary>Cobre a tela, chama <paramref name="swap"/> e descobre.
	/// Enquanto roda, nada embaixo recebe clique.</summary>
	public static ScreenWipe Play(Control host, Action swap)
	{
		var wipe = new ScreenWipe { Color = Colors.White, MouseFilter = MouseFilterEnum.Stop, ZIndex = 90 };
		wipe.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		wipe._material = new ShaderMaterial { Shader = new Shader { Code = Code } };
		wipe._material.SetShaderParameter("gold", UiTokens.Accent);
		wipe._material.SetShaderParameter("paper", UiTokens.Paper);
		wipe.Material = wipe._material;
		host.AddChild(wipe);
		wipe._material.SetShaderParameter("aspect", wipe.Size.Y > 0 ? wipe.Size.X / wipe.Size.Y : 1.777f);

		Tween tween = wipe.CreateTween();
		tween.TweenMethod(Callable.From<float>(v => wipe._material.SetShaderParameter("cover", v)), 0f, 1f, In)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		tween.TweenCallback(Callable.From(() => swap?.Invoke()));
		tween.TweenInterval(0.06f);
		tween.TweenMethod(Callable.From<float>(v => wipe._material.SetShaderParameter("reveal", v)), 0f, 1f, Out)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		tween.TweenCallback(Callable.From(wipe.QueueFree));
		return wipe;
	}
}
