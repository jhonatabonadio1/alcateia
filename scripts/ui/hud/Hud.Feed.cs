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
		line.SetMeta("quem", new Godot.Collections.Array { kill.Killer ?? "", kill.Victim ?? "" });
		line.SetMeta("minha", mine || died);
		Shine(line, kill);

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

	/// <summary>Toca a fala da sequência, na voz da Iris: cada uma tem o seu
	/// arquivo (voz_double_kill…; docs/SONS_IA.md). Sem o arquivo, cai na
	/// fala genérica (voz_multi_kill) se existir, senão fica em silêncio —
	/// melhor que outra voz.</summary>
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

	/// <summary>
	/// Abate com arma épica ou lendária: a linha inteira acende na cor da
	/// raridade (pedido do Jhon, 18/11: "o brilho dela, neon, em toda a
	/// linha; aumentar o desejo dos players de terem a arma"). Borda e halo
	/// na cor, fundo tingido, um reflexo que varre a linha e o halo
	/// pulsando — a lendária mais forte e por mais tempo.
	/// </summary>
	private static void Shine(PanelContainer line, Kill kill)
	{
		if (kill.Cause is KillCause.Wolf or KillCause.Knife or KillCause.Grenade or KillCause.Fall) return;
		CatalogItem item = Catalog.Get(kill.Weapon);
		if (item == null || item.Rarity < 3) return;

		bool legendary = item.Rarity >= 4;
		Color neon = UiTokens.Rarity[Mathf.Clamp(item.Rarity, 0, 4)];
		var box = new StyleBoxFlat
		{
			BgColor = new Color(0.03f, 0.035f, 0.05f, 0.9f).Lerp(new Color(neon, 0.9f), legendary ? 0.3f : 0.22f),
			BorderColor = neon.Lightened(0.25f),
			ShadowColor = new Color(neon, legendary ? 0.75f : 0.55f),
			ShadowSize = legendary ? 14 : 9,
			ContentMarginLeft = UiTokens.S3, ContentMarginRight = UiTokens.S3,
			ContentMarginTop = 4, ContentMarginBottom = 4
		};
		box.SetBorderWidthAll(legendary ? 2 : 1);
		box.BorderWidthLeft = 4;
		line.AddThemeStyleboxOverride("panel", box);
		line.ClipContents = true;

		// O halo respira.
		int low = box.ShadowSize - 4, high = box.ShadowSize + (legendary ? 8 : 4);
		Tween pulse = line.CreateTween().SetLoops();
		pulse.TweenMethod(Callable.From<float>(v => box.ShadowSize = Mathf.RoundToInt(v)), (float)low, (float)high, 0.45).SetTrans(Tween.TransitionType.Sine);
		pulse.TweenMethod(Callable.From<float>(v => box.ShadowSize = Mathf.RoundToInt(v)), (float)high, (float)low, 0.45).SetTrans(Tween.TransitionType.Sine);

		// O reflexo que varre a linha.
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(1, 1, 1, 0));
		gradient.AddPoint(0.5f, new Color(neon.Lightened(0.6f), legendary ? 0.55f : 0.4f));
		gradient.SetColor(2, new Color(1, 1, 1, 0));
		var layer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddChild(layer);
		var glint = new TextureRect
		{
			Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 4, FillTo = new Vector2(1, 0.35f) },
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Size = new Vector2(90, 60),
			Position = new Vector2(-120, -12)
		};
		layer.AddChild(glint);
		Tween sweep = glint.CreateTween().SetLoops(legendary ? 4 : 2);
		sweep.TweenProperty(glint, "position:x", 520f, legendary ? 0.7 : 0.9).From(-120f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
		sweep.TweenInterval(legendary ? 0.5 : 0.9);
	}

	private string _feedFocus;

	/// <summary>
	/// Na câmera de abate e no espectador, a linha do feed com o nome de
	/// quem se vê ganha a borda vermelha (pedido do Jhon, 18/11: "feed com
	/// o nome dele marcado"); volta ao normal quando a câmera sai. As
	/// linhas de arma épica ou lendária (com o brilho) ficam como estão.
	/// </summary>
	private void RestyleFeed()
	{
		string focus = KillCam.Focus;
		if (focus == _feedFocus || _feed == null) return;
		_feedFocus = focus;

		foreach (Node node in _feed.GetChildren())
		{
			if (node is not PanelContainer line || !line.HasMeta("quem") || line.ClipContents) continue;
			var who = line.GetMeta("quem").AsGodotArray();
			bool marked = !string.IsNullOrEmpty(focus) && (who[0].AsString() == focus || who[1].AsString() == focus);
			bool mine = line.GetMeta("minha").AsBool();
			line.AddThemeStyleboxOverride("panel", marked
				? HudStyle.Plate(UiTokens.Danger, padH: UiTokens.S3, padV: 3, accentWidth: 3)
				: HudStyle.Plate(mine ? UiTokens.Accent : new Color(1, 1, 1, 0.25f), padH: UiTokens.S3, padV: 3, accentWidth: mine ? 3 : 1));
		}
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
				// O especial aparece para todo mundo como aparece para quem
				// o tem pronto: o disco laranja com raios e brilhos.
				if (kill.Wolf == WolfHit.Pounce) return new SpecialBadge();
				return Icon.Make(kill.Wolf switch
				{
					WolfHit.Pounce => IconKind.Pounce,
					WolfHit.Dive => IconKind.FromAbove,
					_ => IconKind.Claw
				}, 18, UiTokens.WolfOrange);
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
