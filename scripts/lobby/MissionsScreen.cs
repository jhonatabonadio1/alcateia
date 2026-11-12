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

    protected override void Build()
    {
        Profile.Missions.Refresh(DateTime.Now);

        MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4));
        AddChild(margin);
        VBoxContainer page = UiKit.Column(UiTokens.S3);
        margin.AddChild(page);

        HBoxContainer head = UiKit.Row(UiTokens.S3);
        head.AddChild(UiKit.Text("Missões", "Title"));
        int ready = Profile.Missions.ClaimableCount;
        Label summary = UiKit.Text(ready > 0 ? $"{ready} pronta(s) para resgatar" : "Progresso conta em toda partida terminada", "Muted",
            ready > 0 ? UiTokens.Accent : (Color?)null);
        summary.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        head.AddChild(summary);
        page.AddChild(head);

        HBoxContainer columns = UiKit.Row(UiTokens.S4);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        page.AddChild(columns);

        TimeSpan daily = MissionBook.UntilDailyReset(DateTime.Now);
        TimeSpan weekly = MissionBook.UntilWeeklyReset(DateTime.Now);

        columns.AddChild(Section("Diárias", $"trocam em {daily.Hours} h {daily.Minutes:00} min", Profile.Missions.Daily));
        columns.AddChild(Section("Semanais", $"trocam em {(int)weekly.TotalDays} d {weekly.Hours} h", Profile.Missions.Weekly));
        columns.AddChild(Section("Permanentes", "cada uma dá um título", Profile.Missions.Permanent));
    }

    private Control Section(string title, string note, List<MissionProgress> list)
    {
        PanelContainer panel = UiKit.Panel("Surface1");
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MarginContainer margin = UiKit.Margin(UiTokens.S4);
        panel.AddChild(margin);
        VBoxContainer column = UiKit.Column(UiTokens.S3);
        margin.AddChild(column);

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
        bottom.AddChild(UiKit.Text($"+{UiTokens.Thousands(def.Xp)} XP", "Numeric", UiTokens.TextSecondary));
        if (!string.IsNullOrEmpty(def.Title)) bottom.AddChild(UiKit.Chip($"título: {def.Title}", UiTokens.Accent));
        bottom.AddChild(UiKit.Spacer());

        Button claim = UiKit.Button(p.Claimed ? "Resgatada" : "Resgatar", done && !p.Claimed ? ButtonKind.Primary : ButtonKind.Secondary,
            () => Claim(def));
        claim.Disabled = !done || p.Claimed;
        claim.CustomMinimumSize = new Vector2(120, 34);
        bottom.AddChild(claim);
        column.AddChild(bottom);

        return card;
    }

    private void Claim(MissionDef def)
    {
        if (!Profile.Missions.Claim(def.Id, out List<int> promotions))
            return;

        Sfx.Ui("abate");
        Shell.Toast($"Resgatada: +{UiTokens.Thousands(def.Presas)} Presas, +{UiTokens.Thousands(def.Xp)} XP", UiTokens.Success);
        Profile.NotifyChanged();
        Refresh();

        foreach (int rank in promotions)
            PromotionOverlay.Open(Shell.Overlay, rank);
    }
}
