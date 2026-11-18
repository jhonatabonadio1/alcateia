using Godot;
using System.Collections.Generic;

/// <summary>
/// Superior direito: o feed de abates. Cada linha diz quem, como e quem:
/// a foto da arma ou o golpe do lobo (garra, o especial, o bote) e as
/// marcas do abate — pelas costas, de cima, na cabeça, vingança — e a
/// sequência de quem matou (×2, ×3…). O seu abate em sequência ganha a
/// chamada grande no meio da tela (<see cref="KillBanner.Announce"/>).
/// </summary>
public partial class Hud
{
	/// <summary>Quanto tempo uma linha fica no feed.</summary>
	[ExportGroup("Feed")]
	[Export] public float FeedLife = 5f;
	[Export] public int FeedLines = 5;

	private VBoxContainer _feed;
	private readonly KillStory _story = new();

	private void OnKill(Kill kill)
	{
		if (_feed == null) return;

		string me = _health?.PublicName;
		bool mine = kill.Killer == me;
		bool died = kill.Victim == me;

		// Sequência (double, triple…) e vingança, de quem for.
		KillStory.Note note = _story.Add(kill.Killer, kill.Victim, Time.GetTicksMsec() / 1000.0);

		if (mine && kill.Victim != me)
		{
			_hitMarker?.Flash(kill.Cause == KillCause.Headshot, kill: true);
			// A caveira pulsa na mira (HitMarker); "ELIMINOU <jogador>" embaixo.
			_killBanner?.Add(kill.Victim, kill.Cause == KillCause.Headshot);
			Celebrate(kill, note);
		}

		if (died) _deathBy.Text = KillFeed.HowDied(kill);

		// Placa chapada (sem borrar o fundo). A linha em que você aparece
		// leva borda dourada: é a que importa.
		var line = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
		line.AddThemeStyleboxOverride("panel", HudStyle.Plate(mine || died ? UiTokens.Accent : new Color(1, 1, 1, 0.25f),
			padH: UiTokens.S3, padV: 3, accentWidth: mine || died ? 3 : 1));

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		line.AddChild(row);

		if (kill.KillerTeam != 0 && kill.Killer != kill.Victim)
		{
			row.AddChild(TeamName(kill.Killer, kill.KillerTeam));
			// A sequência de quem matou: ×2, ×3…
			if (note.Streak >= 2)
			{
				Label times = HudStyle.Number(UiKit.Text($"×{note.Streak}", "Numeric"), 17, UiTokens.Accent);
				times.TooltipText = KillStory.StreakName(note.Streak);
				row.AddChild(times);
			}
		}

		// Como foi: a arma ou o golpe, e as marcas do abate.
		row.AddChild(Glyph(kill));
		if (kill.FromBehind) row.AddChild(Mark(IconKind.Behind, UiTokens.TextPrimary));
		if (kill.FromAbove && kill.Wolf != WolfHit.Dive) row.AddChild(Mark(IconKind.FromAbove, UiTokens.TextPrimary));
		if (kill.Cause == KillCause.Headshot) row.AddChild(Mark(IconKind.Headshot, UiTokens.Danger.Lightened(0.2f)));
		if (note.Revenge) row.AddChild(Mark(IconKind.Revenge, UiTokens.Warning));
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

	/// <summary>A chamada grande e a fala do seu abate: TEAM KILL quando
	/// o time inimigo inteiro caiu nesta sequência; senão DOUBLE, TRIPLE…;
	/// e VINGANÇA quando a vítima era quem tinha te matado.</summary>
	private void Celebrate(Kill kill, KillStory.Note note)
	{
		int enemies = EnemiesAlive(out int total);
		if (note.Streak >= 2 && total >= 2 && enemies == 0 && note.Streak >= total)
		{
			_streakCall?.Announce(KillStory.TeamKillName, note.Streak + 1);
			Say(KillStory.TeamKillVoice);
		}
		else if (note.Streak >= 2)
		{
			_streakCall?.Announce(KillStory.StreakName(note.Streak), note.Streak);
			Say(KillStory.StreakVoice(note.Streak));
		}
		else if (note.Revenge)
		{
			_streakCall?.Announce("VINGANÇA", 1, UiTokens.Warning);
			Say(KillStory.RevengeVoice);
		}
	}

	/// <summary>Toca a fala da sequência. Cada uma tem o seu arquivo
	/// (voz_double_kill…) quando houver gravação; enquanto não há, todas
	/// caem na fala genérica que existe (voz_multi_kill). docs/AUDIO.md.</summary>
	private static void Say(string voice)
	{
		if (string.IsNullOrEmpty(voice)) return;
		WorldAudio.Say(Sfx.Has(voice) ? voice : voice == KillStory.RevengeVoice ? "" : KillStory.FallbackVoice);
	}

	/// <summary>Quantos inimigos estão de pé, e quantos há no outro time.</summary>
	private int EnemiesAlive(out int total)
	{
		total = 0;
		int alive = 0;
		foreach (string group in Groups.Fighters)
			foreach (Node fighter in GetTree().GetNodesInGroup(group))
			{
				if (fighter.GetNodeOrNull<Health>("Health") is not Health health || health.Team == _health.Team) continue;
				total++;
				if (!health.IsDead) alive++;
			}
		return alive;
	}

	private static Icon Mark(IconKind kind, Color color) => Icon.Make(kind, 16, color);

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
			// O lobo: garra, o especial (o salto) ou o bote, que vem de cima.
			case KillCause.Wolf:
				return Icon.Make(kill.Wolf switch
				{
					WolfHit.Pounce => IconKind.Pounce,
					WolfHit.Dive => IconKind.FromAbove,
					_ => IconKind.Claw
				}, 18, UiTokens.TeamOrange);
			case KillCause.Knife: return Icon.Make(IconKind.Knife, 18, UiTokens.TextPrimary);
			case KillCause.Grenade: return Icon.Make(IconKind.Grenade, 18, UiTokens.TextPrimary);
			case KillCause.Fall: return Icon.Make(IconKind.Fall, 18, UiTokens.TextSecondary);
		}

		if (Catalog.Get(kill.Weapon) == null) return Icon.Make(IconKind.Gun, 18, UiTokens.TextPrimary);
		WeaponArt art = WeaponArt.Make(kill.Weapon, new Vector2(70, 26));
		art.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		art.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return art;
	}
}
