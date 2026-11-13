using Godot;
using System;
using System.Linq;

/// <summary>
/// O perfil: identidade, patente com a barra de XP, estatísticas
/// vitalícias e o histórico de patentes. Abre pelo bloco de identidade
/// do header e pelo botão "Ver perfil" da sala.
///
/// Perfil de outro jogador (Fase 2) mostra tudo menos saldo e
/// inventário — por isso o saldo não aparece aqui também: ele já está no
/// header.
/// </summary>
public partial class ProfileScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Profile;

	protected override void Build()
	{
		HBoxContainer split = SplitPage();

		split.AddChild(Identity());
		split.AddChild(Stats());
		split.AddChild(History());
	}

	// ------------------------------------------------------------------ identidade

	private Control Identity()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S5, UiTokens.S3);
		panel.CustomMinimumSize = new Vector2(440, 0);

		int rank = Profile.Rank;

		RankBadge badge = RankBadge.Make(rank, 128);
		badge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		column.AddChild(badge);

		Label name = UiKit.Text(Profile.PlayerName, "Display");
		name.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(name);

		Label rankName = UiKit.Text(Ranks.NameOf(rank).ToUpperInvariant(), "Subtitle", Ranks.ColorOf(rank));
		rankName.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(rankName);

		if (!string.IsNullOrEmpty(Profile.Title))
		{
			PanelContainer title = UiKit.Chip(Profile.Title, UiTokens.Accent);
			title.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			column.AddChild(title);
		}

		column.AddChild(UiKit.Gap(UiTokens.S2));

		// XP: barra grande, quanto falta e qual é a próxima.
		long into = Profile.Xp - Ranks.XpFor(rank);
		long span = Ranks.XpFor(rank + 1) - Ranks.XpFor(rank);
		bool max = rank >= Ranks.Count - 1;

		HBoxContainer xpHead = UiKit.Row(UiTokens.S2);
		xpHead.AddChild(UiKit.Caption("Experiência"));
		xpHead.AddChild(UiKit.Spacer());
		xpHead.AddChild(UiKit.Text(max ? "Patente máxima" : $"{UiTokens.Thousands(into)} / {UiTokens.Thousands(span)}", "Numeric"));
		column.AddChild(xpHead);

		var bar = new ProgressBar { CustomMinimumSize = new Vector2(0, 8), MaxValue = 1, Step = 0.001, Value = Ranks.Progress(Profile.Xp), ShowPercentage = false };
		column.AddChild(bar);

		if (!max)
		{
			HBoxContainer next = UiKit.Row(UiTokens.S2);
			next.AddChild(UiKit.Text("Próxima:", "Muted"));
			next.AddChild(RankBadge.Make(rank + 1, 24, true));
			next.AddChild(UiKit.Text(Ranks.NameOf(rank + 1), null, Ranks.ColorOf(rank + 1)));
			next.AddChild(UiKit.Spacer());
			next.AddChild(UiKit.Text($"faltam {UiTokens.Thousands(span - into)} XP", "Caption"));
			column.AddChild(next);
		}

		column.AddChild(UiKit.Divider());

		// Título exibível: ganho nas missões permanentes.
		column.AddChild(UiKit.Caption("Título"));
		string[] titles = new[] { "Nenhum" }.Concat(Profile.Titles).ToArray();
		int current = Math.Max(0, Array.IndexOf(titles, Profile.Title));
		OptionButton picker = UiKit.Picker(titles, current, i =>
		{
			Profile.Title = i == 0 ? "" : titles[i];
			Profile.Save();
			Profile.NotifyChanged();
			Refresh();
		});
		picker.Disabled = Profile.Titles.Count == 0;
		picker.TooltipText = Profile.Titles.Count == 0 ? "Títulos vêm das missões permanentes" : "";
		column.AddChild(picker);

		column.AddChild(UiKit.Spacer());

		// Trocar o nome exige o item da loja.
		bool canRename = Profile.Owns("troca_nome");
		Button rename = UiKit.Button(canRename ? "Trocar nome" : "Trocar nome (item da loja)", ButtonKind.Secondary,
			canRename ? Rename : () => Shell.GoShop("troca_nome"));
		column.AddChild(rename);

		Label created = UiKit.Text($"Perfil criado em {Profile.CreatedUtc.ToLocalTime():dd/MM/yyyy}", "Caption");
		created.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(created);

		return panel;
	}

	private void Rename()
	{
		LineEdit field = UiKit.Field(Profile.PlayerName, "Novo nome", Profile.NameMax);
		Label error = UiKit.Text(" ", "Caption", UiTokens.Danger);
		VBoxContainer body = UiKit.Column(UiTokens.S2);
		body.CustomMinimumSize = new Vector2(380, 0);
		body.AddChild(UiKit.Paragraph("Consome um item Troca de nome."));
		body.AddChild(field);
		body.AddChild(error);
		field.TextChanged += t => error.Text = Profile.ValidateName(t) ?? " ";

		Shell.Modal("Trocar nome", body,
			new Modal.Choice("Cancelar", ButtonKind.Secondary, null),
			new Modal.Choice("Trocar", ButtonKind.Primary, () =>
			{
				if (Profile.SetName(field.Text, out string why)) { Shell.Toast("Nome trocado", UiTokens.Success); Refresh(); }
				else Shell.Toast(why, UiTokens.Danger);
			}));
		field.CallDeferred(Control.MethodName.GrabFocus);
	}

	// ------------------------------------------------------------------ estatísticas

	private Control Stats()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S3);
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		column.AddChild(UiKit.Text("Estatísticas", "Title"));

		LifetimeStats s = Profile.Stats;
		var grid = new GridContainer { Columns = 3 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S3);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S3);
		column.AddChild(grid);

		Tile(grid, "Partidas", s.Matches.ToString());
		Tile(grid, "Vitórias", s.Wins.ToString(), $"{s.WinRate * 100:0}% das partidas");
		Tile(grid, "Derrotas · empates", $"{s.Losses} · {s.Draws}");
		Tile(grid, "Abates", UiTokens.Thousands(s.Kills));
		Tile(grid, "Mortes", UiTokens.Thousands(s.Deaths), $"K/D {s.KillDeath:0.00}");
		Tile(grid, "Assistências", UiTokens.Thousands(s.Assists));
		Tile(grid, "Na cabeça", UiTokens.Thousands(s.Headshots), s.Kills > 0 ? $"{s.Headshots * 100f / s.Kills:0}% dos abates" : null);
		Tile(grid, "MVP", s.Mvps.ToString());
		Tile(grid, "Tempo jogado", Hours(s.SecondsPlayed));

		column.AddChild(UiKit.Gap(UiTokens.S2));
		column.AddChild(UiKit.Caption("Humano contra lobo"));

		var forms = new GridContainer { Columns = 2 };
		forms.AddThemeConstantOverride("h_separation", UiTokens.S3);
		forms.AddThemeConstantOverride("v_separation", UiTokens.S3);
		column.AddChild(forms);

		Tile(forms, "Abates como humano", UiTokens.Thousands(s.KillsHuman), $"{Hours(s.SecondsHuman)} como humano", IconKind.Human);
		Tile(forms, "Abates como lobo", UiTokens.Thousands(s.KillsWolf), $"{Hours(s.SecondsWolf)} como lobo", IconKind.Wolf);

		HBoxContainer favorites = UiKit.Row(UiTokens.S3);
		column.AddChild(favorites);
		favorites.AddChild(Favorite("Arma favorita", s.FavoriteWeapon));
		favorites.AddChild(Favorite("Classe de lobo favorita", s.FavoriteBreed));

		return column;
	}

	private static string Hours(double seconds)
	{
		var t = TimeSpan.FromSeconds(seconds);
		return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : $"{t.Minutes} min";
	}

	private static void Tile(GridContainer grid, string label, string value, string note = null, IconKind icon = IconKind.None)
	{
		PanelContainer tile = UiKit.Panel("Surface1");
		tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		tile.CustomMinimumSize = new Vector2(0, 96);
		VBoxContainer column = UiKit.Column(UiTokens.S1);
		tile.AddChild(column);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		if (icon != IconKind.None) head.AddChild(Icon.Make(icon, 16, UiTokens.TextSecondary));
		head.AddChild(UiKit.Caption(label));
		column.AddChild(head);
		column.AddChild(UiKit.Text(value, "NumericLarge"));
		if (note != null) column.AddChild(UiKit.Text(note, "Caption"));
		grid.AddChild(tile);
	}

	private static Control Favorite(string label, string id)
	{
		PanelContainer tile = UiKit.Panel("Surface1");
		tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		tile.AddChild(row);

		CatalogItem item = Catalog.Get(id);
		if (item != null) row.AddChild(WeaponArt.Make(item.Id, new Vector2(140, 60)));

		VBoxContainer text = UiKit.Column(0);
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		text.AddChild(UiKit.Caption(label));
		text.AddChild(UiKit.Text(item?.Name ?? "Ainda nenhuma", "Subtitle", item == null ? UiTokens.TextDisabled : UiTokens.TextPrimary));
		row.AddChild(text);
		return tile;
	}

	// ------------------------------------------------------------------ histórico

	private Control History()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S4, UiTokens.S2);
		panel.CustomMinimumSize = new Vector2(360, 0);

		column.AddChild(UiKit.Caption("Histórico de patentes"));

		foreach (RankEvent e in Profile.RankHistory.OrderByDescending(e => e.Utc).Take(8))
		{
			HBoxContainer row = UiKit.Row(UiTokens.S2);
			row.AddChild(RankBadge.Make(e.Rank, 28));
			row.AddChild(UiKit.Truncated(Ranks.NameOf(e.Rank), null, Ranks.ColorOf(e.Rank)));
			row.AddChild(UiKit.Text(e.Utc.ToLocalTime().ToString("dd/MM/yy"), "Numeric", UiTokens.TextSecondary));
			column.AddChild(row);
		}

		column.AddChild(UiKit.Divider());
		column.AddChild(UiKit.Caption("Próximas patentes"));

		int rank = Profile.Rank;
		for (int r = rank + 1; r <= Math.Min(rank + 6, Ranks.Count - 1); r++)
		{
			HBoxContainer row = UiKit.Row(UiTokens.S2);
			row.AddChild(RankBadge.Make(r, 28));
			row.AddChild(UiKit.Truncated(Ranks.NameOf(r), null, UiTokens.TextSecondary));
			row.AddChild(UiKit.Text($"{UiTokens.Thousands(Ranks.XpFor(r))} XP", "Numeric", UiTokens.TextDisabled));
			column.AddChild(row);
		}

		column.AddChild(UiKit.Spacer());
		column.AddChild(UiKit.Paragraph("Patente não cai: ela mede o caminho percorrido, não a fase atual.", "Caption"));
		return panel;
	}
}
