using Godot;

/// <summary>
/// A marca do inimigo travado pelo salto do lobo (<see cref="Player.LockTarget"/>):
/// quatro cantos vermelhos em volta dele, um losango girando no meio e
/// "TRAVADO" com a distância. Só na tela do dono.
/// </summary>
public partial class LockMark : CanvasLayer
{
	private Player _player;
	private Control _canvas;
	private Vector2 _center;
	private Vector2 _half;
	private float _distance;
	private float _spin;
	private float _grow;
	private Node3D _last;

	public override void _Ready()
	{
		Layer = 6;
		_canvas = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_canvas.Draw += OnDraw;
		AddChild(_canvas);
	}

	public override void _Process(double delta)
	{
		_player ??= Groups.LocalPlayer(GetTree()) as Player;
		if (_player == null || !IsInstanceValid(_player))
		{
			_player = null;
			_canvas.Visible = false;
			return;
		}

		Node3D target = _player.LockTarget;
		Camera3D camera = GetViewport().GetCamera3D();
		bool show = target != null && IsInstanceValid(target) && camera != null
			&& !camera.IsPositionBehind(target.GlobalPosition);
		_canvas.Visible = show;
		if (!show)
		{
			_last = null;
			return;
		}

		// Travou agora: a marca nasce larga e fecha no alvo.
		if (target != _last) _grow = 1f;
		_last = target;
		_grow = Mathf.MoveToward(_grow, 0f, (float)delta * 5f);

		Vector2 head = camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 1.0f);
		Vector2 feet = camera.UnprojectPosition(target.GlobalPosition + Vector3.Down * 0.9f);
		float height = Mathf.Clamp(Mathf.Abs(feet.Y - head.Y), 40f, 520f);
		_center = (head + feet) / 2f;
		_half = new Vector2(height * 0.36f, height * 0.56f) * (1f + _grow * 0.6f);
		_distance = camera.GlobalPosition.DistanceTo(target.GlobalPosition);
		_spin += (float)delta * 2.4f;
		_canvas.QueueRedraw();
	}

	private void OnDraw()
	{
		Color color = UiTokens.Danger with { A = 1f - _grow * 0.6f };
		var outline = new Color(0, 0, 0, 0.55f);
		float arm = Mathf.Min(_half.X, _half.Y) * 0.45f;

		foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
		{
			Vector2 corner = _center + new Vector2(sx * _half.X, sy * _half.Y);
			Vector2[] bracket = { corner + new Vector2(0, -sy * arm), corner, corner + new Vector2(-sx * arm, 0) };
			_canvas.DrawPolyline(bracket, outline, 6f, true);
			_canvas.DrawPolyline(bracket, color, 3f, true);
		}

		// O losango no meio, girando: travado.
		float r = 9f;
		var diamond = new Vector2[5];
		for (int i = 0; i < 4; i++)
			diamond[i] = _center + Vector2.FromAngle(_spin + i * Mathf.Pi / 2f) * r;
		diamond[4] = diamond[0];
		_canvas.DrawPolyline(diamond, outline, 5f, true);
		_canvas.DrawPolyline(diamond, color, 2.5f, true);

		string text = $"TRAVADO  {_distance:0} M";
		Vector2 at = _center + new Vector2(-120f, _half.Y + 26f);
		_canvas.DrawStringOutline(UiFonts.Display, at, text, HorizontalAlignment.Center, 240f, 20, 5, outline);
		_canvas.DrawString(UiFonts.Display, at, text, HorizontalAlignment.Center, 240f, 20, color);
	}
}
