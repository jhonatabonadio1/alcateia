using Godot;
using System.Collections.Generic;

/// <summary>
/// As marcas do modo alvo do salto do lobo (<see cref="Player.TargetMode"/>):
/// um losango sobre cada inimigo que pode ser escolhido; cantos claros em
/// volta do destacado ("CLIQUE"); cantos vermelhos, um losango girando e
/// "ALVO" com a distância em volta do travado. Só na tela do dono.
/// </summary>
public partial class LockMark : CanvasLayer
{
	private Player _player;
	private Control _canvas;
	private float _spin;
	private float _grow;
	private Node3D _lastLocked;

	private readonly List<Vector2> _others = new();
	private bool _hasMain;
	private bool _locked;
	private Vector2 _center;
	private Vector2 _half;
	private float _distance;
	private bool _empty;

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

		Camera3D camera = GetViewport().GetCamera3D();
		_canvas.Visible = _player.TargetMode && camera != null;
		if (!_canvas.Visible)
		{
			_lastLocked = null;
			return;
		}

		Node3D locked = _player.LockTarget;
		Node3D main = locked ?? _player.TargetHover;
		_locked = locked != null;
		if (locked != _lastLocked && locked != null) _grow = 1f;
		_lastLocked = locked;
		_grow = Mathf.MoveToward(_grow, 0f, (float)delta * 5f);
		_spin += (float)delta * 2.4f;

		_others.Clear();
		foreach (Node3D body in _player.TargetCandidates)
		{
			if (body == main || !IsInstanceValid(body)) continue;
			Vector3 above = body.GlobalPosition + Vector3.Up * 1.35f;
			if (!camera.IsPositionBehind(above)) _others.Add(camera.UnprojectPosition(above));
		}

		_hasMain = main != null && IsInstanceValid(main) && !camera.IsPositionBehind(main.GlobalPosition);
		_empty = _player.TargetCandidates.Count == 0;
		if (_hasMain)
		{
			Vector2 head = camera.UnprojectPosition(main.GlobalPosition + Vector3.Up * 1.0f);
			Vector2 feet = camera.UnprojectPosition(main.GlobalPosition + Vector3.Down * 0.9f);
			float height = Mathf.Clamp(Mathf.Abs(feet.Y - head.Y), 40f, 520f);
			_center = (head + feet) / 2f;
			_half = new Vector2(height * 0.36f, height * 0.56f) * (1f + _grow * 0.6f);
			_distance = camera.GlobalPosition.DistanceTo(main.GlobalPosition);
		}
		_canvas.QueueRedraw();
	}

	private void OnDraw()
	{
		var outline = new Color(0, 0, 0, 0.55f);
		Font font = HudStyle.Slanted(UiFonts.Display);

		// Os outros escolhíveis: um losango pequeno sobre a cabeça.
		foreach (Vector2 at in _others)
			Diamond(at, 7f, 0f, new Color(1, 1, 1, 0.75f), outline);

		if (_empty)
		{
			Vector2 mid = _canvas.Size / 2f + new Vector2(-160f, 80f);
			const string none = "SEM ALVO · CLIQUE SALTA NA MIRA";
			_canvas.DrawStringOutline(font, mid, Loc.T(none), HorizontalAlignment.Center, 320f, 20, 5, outline);
			_canvas.DrawString(font, mid, Loc.T(none), HorizontalAlignment.Center, 320f, 20, UiTokens.TextSecondary);
		}

		if (!_hasMain) return;

		Color color = _locked ? UiTokens.Danger with { A = 1f - _grow * 0.6f } : UiTokens.TextPrimary;
		float arm = Mathf.Min(_half.X, _half.Y) * 0.45f;
		foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
		{
			Vector2 corner = _center + new Vector2(sx * _half.X, sy * _half.Y);
			Vector2[] bracket = { corner + new Vector2(0, -sy * arm), corner, corner + new Vector2(-sx * arm, 0) };
			_canvas.DrawPolyline(bracket, outline, 6f, true);
			_canvas.DrawPolyline(bracket, color, 3f, true);
		}

		if (_locked) Diamond(_center, 9f, _spin, color, outline);

		string text = _locked ? Loc.F("CLIQUE: SALTAR  {0:0} M", _distance) : Loc.T("CLIQUE: ALVO");
		Vector2 label = _center + new Vector2(-120f, _half.Y + 26f);
		_canvas.DrawStringOutline(font, label, text, HorizontalAlignment.Center, 240f, 20, 5, outline);
		_canvas.DrawString(font, label, text, HorizontalAlignment.Center, 240f, 20, color);
	}

	private void Diamond(Vector2 center, float r, float angle, Color color, Color outline)
	{
		var points = new Vector2[5];
		for (int i = 0; i < 4; i++)
			points[i] = center + Vector2.FromAngle(angle + i * Mathf.Pi / 2f) * r;
		points[4] = points[0];
		_canvas.DrawPolyline(points, outline, 5f, true);
		_canvas.DrawPolyline(points, color, 2.5f, true);
	}
}
