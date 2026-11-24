using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Missões: diárias, semanais e permanentes, cada uma com barra de
/// progresso e recompensa. O resgate é manual — o clique de resgatar é o
/// momento da recompensa, e resgatar sozinho o desperdiçaria.
/// </summary>
public partial class MissionsScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Missions;

	protected override bool FollowsAccount => true;

	protected override void Build()
	{
		VBoxContainer page = Page();

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(UiKit.Text("Missões", "Title"));
		int ready = Profile.Missions.ClaimableCount;
		Label summary = UiKit.Text(ready > 0 ? Loc.F("{0} pronta(s) para resgatar", ready) : "Progresso conta em toda partida online terminada", "Muted",
			ready > 0 ? UiTokens.Accent : (Color?)null);
		summary.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		head.AddChild(summary);
		page.AddChild(head);

		// As missões são da conta: é o servidor que diz quais valem hoje,
		// soma o progresso e paga o prêmio.
		if (!Profile.HasAccount)
		{
			CenterContainer center = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
			center.AddChild(AccountGate.Card(Shell, "As missões são da sua conta",
				"Entre na conta para ver as missões do dia e da semana. O progresso conta nas partidas online, e o prêmio vai direto para a sua carteira."));
			page.AddChild(center);
			return;
		}

		// Ao abrir, pergunta de novo: uma partida pode ter acabado agora.
		if (!_asked)
		{
			_asked = true;
			_ = AccountSync.Refresh();
		}

		HBoxContainer columns = UiKit.Row(UiTokens.S4);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(columns);

		TimeSpan daily = Profile.Missions.UntilDailyReset();
		TimeSpan weekly = Profile.Missions.UntilWeeklyReset();

		if (Profile.Missions.Event.Count > 0)
			columns.AddChild(Section("Evento", "por tempo limitado", Profile.Missions.Event));
		columns.AddChild(Section("Diárias", Loc.F("trocam em {0} h {1:00} min", (int)daily.TotalHours, daily.Minutes), Profile.Missions.Daily));
		columns.AddChild(Section("Semanais", Loc.F("trocam em {0} d {1} h", (int)weekly.TotalDays, weekly.Hours), Profile.Missions.Weekly));
		columns.AddChild(Section("Permanentes", "cada uma dá um título", Profile.Missions.Permanent));
	}

	private bool _asked;
	private bool _claiming;

	private Control Section(string title, string note, List<MissionProgress> list)
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Text(title, "Subtitle"));
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.Text(note, "Caption"));
		column.AddChild(head);

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		VBoxContainer items = UiKit.Column(UiTokens.S2);
		items.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(items);
		column.AddChild(scroll);

		// Prontas para resgatar primeiro, resgatadas por último.
		foreach (MissionProgress p in list
					 .OrderBy(p => p.Claimed ? 2 : p.Progress >= (MissionBook.Def(p.Id)?.Target ?? 1) ? 0 : 1))
		{
			MissionDef def = MissionBook.Def(p.Id);
			if (def != null) items.AddChild(Card(def, p));
		}

		return panel;
	}

	private Control Card(MissionDef def, MissionProgress p)
	{
		bool done = p.Progress >= def.Target;
		PanelContainer card = UiKit.Panel("Surface2");
		if (done && !p.Claimed)
			card.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface2, UiTokens.Accent, 1, UiTokens.RadiusPanel, UiTokens.S3, UiTokens.S3));

		VBoxContainer column = UiKit.Column(UiTokens.S2);
		card.AddChild(column);

		HBoxContainer top = UiKit.Row(UiTokens.S2);
		top.AddChild(Icon.Make(p.Claimed ? IconKind.Check : done ? IconKind.Star : IconKind.Missions, 16,
			p.Claimed ? UiTokens.TextDisabled : done ? UiTokens.Accent : UiTokens.TextSecondary));
		Label text = UiKit.Paragraph(def.Text, p.Claimed ? "Muted" : null);
		top.AddChild(text);
		column.AddChild(top);

		HBoxContainer progress = UiKit.Row(UiTokens.S2);
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 6),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MaxValue = def.Target,
			Value = p.Progress,
			ShowPercentage = false
		};
		if (done) bar.AddThemeStyleboxOverride("fill", UiTheme.Box(p.Claimed ? UiTokens.TextDisabled : UiTokens.Success, default, 0, 0, 0, 0));
		progress.AddChild(bar);
		progress.AddChild(UiKit.Text($"{UiTokens.Thousands(p.Progress)}/{UiTokens.Thousands(def.Target)}", "Numeric", UiTokens.TextSecondary));
		column.AddChild(progress);

		HBoxContainer bottom = UiKit.Row(UiTokens.S2);
		bottom.AddChild(UiKit.Text($"+{UiTokens.Thousands(def.Presas)}", "Numeric"));
		bottom.AddChild(Icon.Make(IconKind.Presa, 13, CurrencyLabel.ColorOf(Currency.Presas)));
		bottom.AddChild(UiKit.Text(Loc.F("+{0} XP", UiTokens.Thousands(def.Xp)), "Numeric", UiTokens.TextSecondary));
		if (def.Sangue > 0)
		{
			bottom.AddChild(UiKit.Text($"+{UiTokens.Thousands(def.Sangue)}", "Numeric"));
			bottom.AddChild(Icon.Make(IconKind.Sangue, 13, CurrencyLabel.ColorOf(Currency.Sangue)));
		}
		if (!string.IsNullOrEmpty(def.ItemId))
			bottom.AddChild(UiKit.Chip(def.ItemDays > 0 ? Loc.F("{0} por {1} dias", Catalog.NameOf(def.ItemId), def.ItemDays) : Catalog.NameOf(def.ItemId), UiTokens.Accent));
		if (!string.IsNullOrEmpty(def.Title)) bottom.AddChild(UiKit.Chip(Loc.F("título: {0}", def.Title), UiTokens.Accent));
		bottom.AddChild(UiKit.Spacer());

		Button claim = UiKit.Button(p.Claimed ? "Resgatada" : "Resgatar", done && !p.Claimed ? ButtonKind.Primary : ButtonKind.Secondary,
			() => Claim(def));
		claim.Disabled = !done || p.Claimed;
		claim.CustomMinimumSize = new Vector2(120, 34);
		bottom.AddChild(claim);
		column.AddChild(bottom);

		return card;
	}

	/// <summary>Resgata no servidor: é ele que confere se completou e paga
	/// (uma vez só). A promoção de patente, se vier, o lobby mostra.</summary>
	private async void Claim(MissionDef def)
	{
		if (_claiming) return;
		_claiming = true;
		string error = await AccountSync.Claim(def.Id);
		_claiming = false;
		if (!IsInsideTree()) return;

		if (error != null)
		{
			Shell.Toast(error, UiTokens.Danger);
			Sfx.Ui("ui_negado");
			Refresh();
			return;
		}

		Sfx.Ui(Sfx.First("ui_recompensa", "abate"));
		Shell.Toast(Loc.F("Resgatada: +{0} Cash, +{1} XP", UiTokens.Thousands(def.Presas), UiTokens.Thousands(def.Xp)), UiTokens.Success);
		Refresh();
	}
}
