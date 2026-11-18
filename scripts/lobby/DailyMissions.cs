using Godot;
using System;

/// <summary>
/// As missões do dia, num cartão de lateral: cada missão com o progresso,
/// quando trocam, e o atalho para a tela de missões. Fica na lateral das
/// salas online (decisão do Jhon, 17/11).
/// </summary>
public static class DailyMissions
{
	public static Control Card(LobbyShell shell)
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S2);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Caption("Missões do dia"));
		head.AddChild(UiKit.Spacer());
		TimeSpan left = MissionBook.UntilDailyReset(DateTime.Now);
		head.AddChild(UiKit.Text(Loc.F("troca em {0} h {1:00} min", left.Hours, left.Minutes), "Caption"));
		column.AddChild(head);

		Profile.Missions.Refresh(DateTime.Now);
		foreach (MissionProgress p in Profile.Missions.Daily)
		{
			MissionDef def = MissionBook.Def(p.Id);
			if (def != null) column.AddChild(Line(def, p));
		}

		column.AddChild(UiKit.Button("Ver todas as missões", ButtonKind.Tertiary, () => shell.GoMissions()));
		return panel;
	}

	public static Control Line(MissionDef def, MissionProgress p)
	{
		VBoxContainer box = UiKit.Column(UiTokens.S1);
		HBoxContainer top = UiKit.Row(UiTokens.S2);
		bool done = p.Progress >= def.Target;
		top.AddChild(Icon.Make(p.Claimed ? IconKind.Check : IconKind.Missions, 14, done ? UiTokens.Success : UiTokens.TextSecondary));
		Label text = UiKit.Truncated(def.Text, null, p.Claimed ? UiTokens.TextDisabled : UiTokens.TextPrimary);
		top.AddChild(text);
		Label count = UiKit.Text($"{p.Progress}/{def.Target}", "Numeric", done ? UiTokens.Success : UiTokens.TextSecondary);
		top.AddChild(count);
		box.AddChild(top);

		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 3),
			MaxValue = def.Target,
			Value = p.Progress,
			ShowPercentage = false
		};
		if (done) bar.AddThemeStyleboxOverride("fill", UiTheme.Box(UiTokens.Success, default, 0, 0, 0, 0));
		box.AddChild(bar);
		return box;
	}
}
