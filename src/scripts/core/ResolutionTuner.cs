using Godot;
using System;

/// <summary>
/// A resolução dinâmica (Jhon, 21/11: o jogo lento na MX110; "o que mais o
/// Overwatch faz"). A cada meio segundo olha quanto a placa levou para
/// desenhar o quadro e, se passou do tempo da meta de FPS
/// (<see cref="Settings.FrameTarget"/>), baixa a resolução do 3D; se sobrou
/// bastante, sobe, até o teto (<see cref="Settings.Data.RenderScale"/>).
/// O HUD não muda, nem a luz. Em máquina que aguenta, fica no teto.
///
/// Mede o tempo da placa, não o do quadro: com o V-Sync ou o limite de FPS
/// o quadro sempre "leva" o tempo da meta, e quando o atraso é do processador
/// baixar a resolução não ajudaria. Na compatibilidade, que não mede a
/// placa, usa o tempo do quadro.
/// </summary>
public partial class ResolutionTuner : Node
{
	public const float Floor = 0.5f;
	private const double Window = 0.5;
	/// <summary>Depois de baixar, espera antes de subir, para não ficar
	/// indo e vindo.</summary>
	private const double HoldAfterDrop = 2.0;

	/// <summary>A média da última meia janela, em ms: o que o painel de
	/// desempenho mostra.</summary>
	public static float GpuMs { get; private set; }
	public static float FrameMs { get; private set; }

	private double _gpu, _frame, _timer, _hold;
	private int _frames;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		RenderingServer.ViewportSetMeasureRenderTime(GetTree().Root.GetViewportRid(), true);
	}

	public override void _Process(double delta)
	{
		Viewport root = GetTree().Root;
		_gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(root.GetViewportRid());
		_frame += delta * 1000.0;
		_frames++;
		_timer += delta;
		_hold -= delta;
		if (_timer < Window) return;

		GpuMs = (float)(_gpu / _frames);
		FrameMs = (float)(_frame / _frames);
		_gpu = _frame = _timer = 0;
		_frames = 0;

		if (!Settings.Values.DynamicResolution) return;

		float max = Math.Clamp(Settings.Values.RenderScale, Floor, 1f);
		float budget = 1000f / Math.Max(Settings.FrameTarget, 1);
		float load = GpuMs > 0.01f ? GpuMs : FrameMs;
		float scale = root.Scaling3DScale;

		if (load > budget * 0.95f)
		{
			scale -= load > budget * 1.3f ? 0.1f : 0.05f;
			_hold = HoldAfterDrop;
		}
		else if (load < budget * 0.75f && _hold <= 0)
			scale += 0.05f;

		scale = Math.Clamp(scale, Floor, max);
		if (Math.Abs(scale - root.Scaling3DScale) > 0.001f) root.Scaling3DScale = scale;
	}
}
