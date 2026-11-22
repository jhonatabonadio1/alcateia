using Godot;
using System;

/// <summary>
/// O painel de desempenho (Jhon, 21/11: "opção para exibir o fps, ping e
/// informações pertinentes para debug, mas não muita coisa, só os
/// principais"). Uma linha só no alto, à esquerda, por cima de tudo, do menu à
/// partida: FPS e tempo do quadro, tempo da placa e resolução do 3D, e, na
/// partida online, o ping e a perda de pacotes. Liga em Opções → Vídeo
/// (<see cref="Settings.Data.ShowPerf"/>). Atualiza 4 vezes por segundo. Em inglês em qualquer idioma (pedido do Jhon, 21/11: informação de debug é em inglês).
/// </summary>
public partial class PerfOverlay : CanvasLayer
{
	private const double Every = 0.25;
	private Label _text;
	private PanelContainer _box;
	private double _timer;

	public override void _Ready()
	{
		Layer = 120;
		ProcessMode = ProcessModeEnum.Always;

		_box = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_box.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.55f),
			ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 2, ContentMarginBottom = 2,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4
		});
		// No canto de cima à esquerda, numa linha só, acima do minimapa.
		_box.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_box.GrowHorizontal = Control.GrowDirection.End;
		_box.OffsetTop = 4;
		_box.OffsetLeft = 4;

		_text = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = false
		};
		_text.AddThemeFontSizeOverride("font_size", 13);
		_text.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f));
		if (UiFonts.Numeric != null) _text.AddThemeFontOverride("font", UiFonts.Numeric);
		_box.AddChild(_text);
		AddChild(_box);

		Settings.Changed += Refresh;
		Refresh();
	}

	public override void _ExitTree() => Settings.Changed -= Refresh;

	private void Refresh()
	{
		Visible = Settings.Values.ShowPerf;
		_timer = Every;
	}

	public override void _Process(double delta)
	{
		if (!Visible) return;
		_timer += delta;
		if (_timer < Every) return;
		_timer = 0;

		int fps = (int)Engine.GetFramesPerSecond();
		int scale = (int)Math.Round(GetTree().Root.Scaling3DScale * 100);
		string line = $"{fps} FPS  {ResolutionTuner.FrameMs:0.0} ms   GPU {ResolutionTuner.GpuMs:0.0} ms   3D {scale}%";

		NetMatch net = NetMatch.Current;
		if (net != null && IsInstanceValid(net) && net.PingMs >= 0)
		{
			float loss = Math.Max(net.PacketLoss, 0f) * 100f;
			line += $"   Ping {net.PingMs} ms   loss {loss:0}%";
		}

		// Cor de alerta: abaixo de 30 FPS ou ping acima de 120 ms.
		bool bad = fps < 30 || (net != null && IsInstanceValid(net) && net.PingMs > 120);
		_text.AddThemeColorOverride("font_color", bad ? new Color(1f, 0.55f, 0.45f) : new Color(0.92f, 0.95f, 1f));
		_text.Text = line;
	}
}
