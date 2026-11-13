using Godot;
using System.Collections.Generic;

/// <summary>
/// Superior direito: o feed de abates, com o ícone de cada arma ou golpe.
/// </summary>
public partial class Hud
{
	/// <summary>Quanto tempo uma linha fica no feed.</summary>
	[ExportGroup("Feed")]
	[Export] public float FeedLife = 5f;
	[Export] public int FeedLines = 5;

	private VBoxContainer _feed;

	private void OnKill(Kill kill)
	{
		if (_feed == null) return;

		string me = _health?.PublicName;
		bool mine = kill.Killer == me;
		bool died = kill.Victim == me;

		if (mine && kill.Victim != me) _hitMarker?.Flash(kill.Cause == KillCause.Headshot, kill: true);

		if (died) _deathBy.Text = KillFeed.HowDied(kill);

		// A linha em que você aparece leva borda dourada: é a que importa.
		var line = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
		line.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.66f),
			mine || died ? UiTokens.Accent : new Color(1, 1, 1, 0.06f), 1, 0, UiTokens.S2, 3));

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		line.AddChild(row);

		if (kill.KillerTeam != 0 && kill.Killer != kill.Victim)
			row.AddChild(TeamName(kill.Killer, kill.KillerTeam));

		row.AddChild(Glyph(kill));
		if (kill.Cause == KillCause.Headshot) row.AddChild(Icon.Make(IconKind.Headshot, 16, UiTokens.Danger.Lightened(0.2f)));
		row.AddChild(TeamName(kill.Victim, kill.VictimTeam));

		_feed.AddChild(line);
		Motion.FadeIn(line, UiTokens.Fast);

		// Só as cinco últimas: um feed que cresce vira parede de texto.
		while (_feed.GetChildCount() > FeedLines)
		{
			Node old = _feed.GetChild(0);
			_feed.RemoveChild(old);
			old.QueueFree();
		}

		Tween life = line.CreateTween();
		life.TweenInterval(FeedLife - 0.4f);
		life.TweenProperty(line, "modulate:a", 0f, 0.4f);
		life.TweenCallback(Callable.From(line.QueueFree));
	}

	private static Label TeamName(string name, int team)
	{
		Label label = UiKit.Text(name, null, Teams.ColorOf(team));
		label.AddThemeFontOverride("font", UiFonts.SemiBold);
		return label;
	}

	/// <summary>Ícone da arma no meio da linha: silhueta pequena para arma
	/// de fogo, garra para lobo, faca, granada, queda.</summary>
	private static Control Glyph(Kill kill)
	{
		switch (kill.Cause)
		{
			case KillCause.Wolf: return Icon.Make(IconKind.Claw, 18, UiTokens.TeamOrange);
			case KillCause.Knife: return Icon.Make(IconKind.Knife, 18, UiTokens.TextPrimary);
			case KillCause.Grenade: return Icon.Make(IconKind.Grenade, 18, UiTokens.TextPrimary);
			case KillCause.Fall: return Icon.Make(IconKind.Fall, 18, UiTokens.TextSecondary);
		}

		if (Catalog.Get(kill.Weapon) == null) return Icon.Make(IconKind.Gun, 18, UiTokens.TextPrimary);
		WeaponArt art = WeaponArt.Make(kill.Weapon, new Vector2(58, 22));
		art.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return art;
	}
}
