using Godot;
using System;

/// <summary>
/// Movimento da interface (guia, item 6): nada aparece ou some de uma
/// vez. Transições de 120 a 200 ms com curva de saída; número que muda,
/// conta; painel que abre, desliza.
///
/// Tudo via Tween criado no próprio controle: se o controle sai da
/// árvore, o tween morre junto e não sobra animação órfã.
/// </summary>
public static class Motion
{
	public static Tween FadeIn(Control control, float duration = UiTokens.Normal, float delay = 0f)
	{
		control.Modulate = new Color(control.Modulate, 0f);
		Tween tween = control.CreateTween();
		tween.TweenProperty(control, "modulate:a", 1f, duration)
			.SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		return tween;
	}

	/// <summary>Entra deslizando de <paramref name="offset"/> e aparecendo.
	/// Só serve para controles posicionados à mão (fora de container),
	/// porque container reposiciona os filhos.</summary>
	public static Tween SlideIn(Control control, Vector2 offset, float duration = UiTokens.Normal, float delay = 0f)
	{
		Vector2 target = control.Position;
		control.Position = target + offset;
		control.Modulate = new Color(control.Modulate, 0f);

		Tween tween = control.CreateTween().SetParallel();
		tween.TweenProperty(control, "position", target, duration)
			.SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(control, "modulate:a", 1f, duration)
			.SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		return tween;
	}

	/// <summary>
	/// A entrada de uma peça que mora num container (27/11): aparece e sobe
	/// (ou vem de lado) até o lugar dela. O container é quem põe a peça no
	/// lugar, então a posição só é lida um quadro depois, com o layout
	/// feito; no fim o container arruma de novo, para o caso de a tela ter
	/// mudado de tamanho no meio do caminho.
	/// </summary>
	public static void Rise(Control control, float delay = 0f, Vector2? from = null, float duration = 0.34f)
	{
		Vector2 offset = from ?? new Vector2(0, 22);
		control.Modulate = new Color(control.Modulate, 0f);
		Tween wait = control.CreateTween();
		wait.TweenInterval(Mathf.Max(delay, 0.02f));
		wait.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree()) return;
			Vector2 target = control.Position;
			control.Position = target + offset;
			Tween tween = control.CreateTween().SetParallel();
			tween.TweenProperty(control, "position", target, duration)
				.SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
			tween.TweenProperty(control, "modulate:a", 1f, duration * 0.8f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			tween.Chain().TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(control) && control.GetParent() is Container box) box.QueueSort();
			}));
		}));
	}

	/// <summary>Entrada em cascata: cada filho um passo depois do anterior.</summary>
	public static void Cascade(Control parent, float start = 0f, float step = UiTokens.Step, Vector2? from = null)
	{
		int i = 0;
		foreach (Node child in parent.GetChildren())
			if (child is Control control && control.Visible)
				Rise(control, start + step * i++, from);
	}

	/// <summary>A entrada de uma tela inteira (fora de container): vem de
	/// lado e aparece.</summary>
	public static Tween Enter(Control screen, float duration = UiTokens.Screen)
	{
		screen.Position = new Vector2(36, 0);
		screen.Modulate = new Color(screen.Modulate, 0f);
		Tween tween = screen.CreateTween().SetParallel();
		tween.TweenProperty(screen, "position", Vector2.Zero, duration)
			.SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(screen, "modulate:a", 1f, duration * 0.7f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		return tween;
	}

	/// <summary>Um "pulo" de escala, para chamar atenção a uma mudança
	/// (WP ganho, acerto). Usa o centro do controle como pivô.</summary>
	public static Tween Pop(Control control, float amount = 1.18f, float duration = 0.22f)
	{
		control.PivotOffset = control.Size / 2f;
		control.Scale = new Vector2(amount, amount);
		Tween tween = control.CreateTween();
		tween.TweenProperty(control, "scale", Vector2.One, duration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		return tween;
	}

	/// <summary>Conta de <paramref name="from"/> até <paramref name="to"/>,
	/// chamando <paramref name="show"/> a cada passo.</summary>
	public static Tween Count(Node owner, double from, double to, Action<double> show, float duration = UiTokens.CountUp)
	{
		Tween tween = owner.CreateTween();
		tween.TweenMethod(Callable.From<double>(value => show(value)), from, to, duration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		return tween;
	}

	/// <summary>Pisca uma cor sobre o texto e volta à original: verde
	/// para ganho, vermelho para gasto.</summary>
	public static Tween Flash(Label label, Color flash, Color rest, float duration = 0.45f)
	{
		label.AddThemeColorOverride("font_color", flash);
		Tween tween = label.CreateTween();
		tween.TweenMethod(Callable.From<Color>(c => label.AddThemeColorOverride("font_color", c)),
			flash, rest, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		return tween;
	}

	/// <summary>Pulso contínuo de opacidade, para avisos críticos
	/// (vida baixa, canalização). Devolve o tween para quem quiser parar.</summary>
	public static Tween Pulse(CanvasItem item, float low = 0.45f, float period = 0.8f)
	{
		Tween tween = item.CreateTween().SetLoops();
		tween.TweenProperty(item, "modulate:a", low, period / 2f).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(item, "modulate:a", 1f, period / 2f).SetTrans(Tween.TransitionType.Sine);
		return tween;
	}
}
