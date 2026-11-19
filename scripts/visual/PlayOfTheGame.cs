using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A jogada da partida (pedido do Jhon, 19/11: "vamos criar a play of the
/// game, no final da partida"). Acabou a partida, antes da tela de
/// resultado: os melhores segundos da partida pela tela de quem fez — a
/// arma na mão, o HUD dele, o feed dele —, com "JOGADA DA PARTIDA", o nome
/// e o que foi ("ABATE TRIPLO") por cima. Espaço pula.
///
/// O lance é escolhido durante a partida pela fita (<see cref="ReplayTape.Best"/>):
/// abates seguidos do mesmo jogador pesam mais; tiro na cabeça, de cima,
/// pelas costas e o especial do lobo somam. Cada corpo que estava lá vira
/// uma cópia (<see cref="Bot.ReplayCopy"/>) movida pela fita, como na
/// câmera de abate; os corpos de verdade somem da câmera enquanto isso.
/// </summary>
public partial class PlayOfTheGame : Node3D
{
	private const string BotScene = "res://scenes/bot.tscn";

	/// <summary>Segundos de título antes de o lance começar.</summary>
	[Export] public double Intro = 1.2;

	private ReplayTape.Clip _clip;
	private Action _done;
	private readonly Dictionary<ulong, Bot> _copies = new();
	private readonly List<Node3D> _hidden = new();
	private Bot _hero;
	private Hud _hud;
	private ViewModel _view;
	private Camera3D _eye, _wolfEye;
	private double _clock;
	private int _killIndex;
	private bool _finished;
	private CanvasLayer _layer;
	private Label _title, _name, _what, _hint;

	/// <summary>A jogada está passando. Para os testes e o HUD.</summary>
	public static bool Showing { get; private set; }

	/// <summary>Começa a jogada, se a partida teve uma; <paramref name="done"/>
	/// roda no fim (ou já, sem jogada). Devolve se começou.</summary>
	public static bool TryStart(Node host, Action done)
	{
		ReplayTape.Current?.CutNow();
		ReplayTape.Clip clip = ReplayTape.Current?.Best;
		if (clip == null || !clip.Tracks.ContainsKey(clip.Hero) || !ResourceLoader.Exists(BotScene))
		{
			done?.Invoke();
			return false;
		}
		Node parent = host.GetTree().CurrentScene ?? host;
		var potg = new PlayOfTheGame { Name = "JogadaDaPartida", _clip = clip, _done = done };
		parent.AddChild(potg);
		return true;
	}

	public override void _Ready()
	{
		TopLevel = true;
		Showing = true;
		// A câmera de abate de quem estava morto sai: agora é a jogada.
		foreach (Node node in GetTree().Root.FindChildren("*", nameof(KillCam), true, false))
			if (node is KillCam cam) cam.Finish();

		BuildCopies();
		HideRealBodies();
		BuildView();
		BuildOverlay();
		Drive(_clip.From);
	}

	public override void _ExitTree()
	{
		Showing = false;
		Cleanup();
	}

	private void Cleanup()
	{
		foreach (Node3D body in _hidden)
			if (IsInstanceValid(body)) KillCam.Mark(body, false);
		_hidden.Clear();
		foreach (Bot copy in _copies.Values)
			if (IsInstanceValid(copy)) copy.QueueFree();
		_copies.Clear();
		if (_hud != null && IsInstanceValid(_hud)) _hud.QueueFree();
	}

	// ------------------------------------------------------------ montar

	private void BuildCopies()
	{
		var scene = GD.Load<PackedScene>(BotScene);
		Node parent = GetParent();
		foreach (var (id, track) in _clip.Tracks)
		{
			var copy = scene.Instantiate<Bot>();
			copy.ReplayCopy = true;
			copy.Name = $"Jogada_{_copies.Count}";
			copy.Team = track.Team;
			copy.Callsign = track.Name;
			parent.AddChild(copy);
			if (track.Sample(_clip.From, out ReplayTape.Frame f)) copy.GlobalPosition = f.At;
			_copies[id] = copy;
			if (id == _clip.Hero) _hero = copy;
		}
	}

	private void HideRealBodies()
	{
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is Node3D body)
				{
					KillCam.Mark(body, true);
					_hidden.Add(body);
				}
	}

	private void BuildView()
	{
		_eye = _hero.GetNodeOrNull<Camera3D>("Head/Camera3D");
		if (_eye != null)
		{
			_eye.Fov = 75f;
			_eye.CullMask &= ~KillCam.WatchedLayer;
			_view = new ViewModel { Name = "ViewModel" };
			_eye.AddChild(_view);
			_eye.MakeCurrent();
		}
		_wolfEye = new Camera3D { Fov = 75f, Near = 0.05f };
		_wolfEye.CullMask &= ~KillCam.WatchedLayer;
		AddChild(_wolfEye);

		_hud = new Hud { Name = "HudDaJogada", Subject = _hero, ReplayFeed = true };
		GetParent().AddChild(_hud);
	}

	private void BuildOverlay()
	{
		_layer = new CanvasLayer { Layer = 12 };
		AddChild(_layer);
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_layer.AddChild(root);

		var top = new ColorRect { Color = new Color(0.01f, 0.012f, 0.02f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		top.OffsetBottom = 18;
		root.AddChild(top);

		VBoxContainer box = UiKit.Column(0);
		box.MouseFilter = Control.MouseFilterEnum.Ignore;
		box.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		box.OffsetBottom = -205; box.OffsetLeft = -420; box.OffsetRight = 420;
		box.GrowVertical = Control.GrowDirection.Begin;
		box.Alignment = BoxContainer.AlignmentMode.End;
		root.AddChild(box);

		_title = HudStyle.Number(UiKit.Text("JOGADA DA PARTIDA", "Display"), 56, UiTokens.TextPrimary);
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(_title);
		_name = HudStyle.Number(UiKit.Text(_clip.HeroName.ToUpper(), "Display"), 40, Teams.ColorOf(_clip.HeroTeam).Lightened(0.25f));
		_name.HorizontalAlignment = HorizontalAlignment.Center;
		_name.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
		box.AddChild(_name);
		_what = HudStyle.Outlined(UiKit.Caption(_clip.Label ?? "", UiTokens.Accent));
		_what.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(_what);
		_hint = HudStyle.Outlined(UiKit.Caption("ESPAÇO: PULAR", UiTokens.TextSecondary));
		_hint.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(_hint);

		Motion.FadeIn(box, UiTokens.Normal);
	}

	// ------------------------------------------------------------ passar

	public override void _Process(double delta)
	{
		if (_finished) return;
		if (Input.IsActionJustPressed("jump")) { Finish(); return; }

		_clock += delta;
		double t = _clip.From + Math.Max(0, _clock - Intro);
		double before = _clip.From + Math.Max(0, _clock - delta - Intro);
		Drive(t, before);

		// O título fica no começo e volta discreto: só o nome e o que foi.
		float fade = Mathf.Clamp((float)(_clock - Intro - 1.5) / 0.6f, 0f, 1f);
		_title.Modulate = new Color(1, 1, 1, 1f - 0.65f * fade);

		if (t >= _clip.To + 0.3) Finish();
	}

	private void Drive(double t, double before = double.NaN)
	{
		foreach (var (id, copy) in _copies)
		{
			if (!IsInstanceValid(copy) || !_clip.Tracks[id].Sample(t, out ReplayTape.Frame f)) continue;
			copy.RemoteStatus(f.Hp, f.MaxHp, f.Shield, f.MaxShield, f.Dead, f.Primary, f.Secondary);
			if (f.Dead) continue;
			copy.RemoteSnapshot(f.At, f.Velocity, f.Yaw, f.Pitch, f.Grounded, (Form)f.Form, (Slot)f.Slot);
			copy.GetNodeOrNull<FormController>("FormController")?.MirrorWp(f.Wp);
			Weapon weapon = copy.GetNodeOrNull<Weapon>("Weapon");
			if (weapon == null) continue;
			if (!double.IsNaN(before))
				foreach (ReplayTape.Event e in _clip.Tracks[id].Events)
					if (e.Time > before && e.Time <= t) weapon.ReplayEvent(e.Kind, e.A, e.B);
			weapon.ReplayMirror(f.Ammo, f.Charge, f.Grenades);
		}

		while (_killIndex < _clip.Kills.Count && _clip.Kills[_killIndex].Time <= t)
		{
			if (_hud != null && IsInstanceValid(_hud)) _hud.ReplayKill(_clip.Kills[_killIndex].Kill);
			_killIndex++;
		}

		Look();
	}

	/// <summary>A tela de quem fez: primeira pessoa de humano (com o corpo
	/// dele fora da câmera dele); de lobo, a câmera de lobo, atrás.</summary>
	private void Look()
	{
		if (_hero == null || !IsInstanceValid(_hero)) return;
		bool wolf = _hero.GetNodeOrNull<FormController>("FormController")?.ShowsWolf ?? false;
		if (!wolf && _eye != null)
		{
			KillCam.Mark(_hero, true);
			if (!_eye.Current) _eye.MakeCurrent();
			return;
		}

		KillCam.Mark(_hero, false);
		float pitch = Mathf.Clamp(_hero.GetNodeOrNull<Node3D>("Head")?.Rotation.X ?? 0f, Mathf.DegToRad(-45f), Mathf.DegToRad(30f));
		Vector3 eye = _hero.GlobalPosition + Vector3.Up * (_hero.GetNodeOrNull<Node3D>("Head")?.Position.Y ?? 0.62f);
		Basis look = Basis.FromEuler(new Vector3(pitch, _hero.GlobalRotation.Y, 0));
		Vector3 back = look.Z * 2.5f;
		var query = PhysicsRayQueryParameters3D.Create(eye, eye + back);
		var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
		Vector3 at = hit.Count > 0 ? hit["position"].AsVector3() - back.Normalized() * 0.2f : eye + back;
		_wolfEye.GlobalTransform = new Transform3D(look, MapBounds.Clamp(GetTree(), at));
		if (!_wolfEye.Current) _wolfEye.MakeCurrent();
	}

	private void Finish()
	{
		if (_finished) return;
		_finished = true;
		Showing = false;
		Action done = _done;
		_done = null;
		Cleanup();
		QueueFree();
		done?.Invoke();
	}
}
