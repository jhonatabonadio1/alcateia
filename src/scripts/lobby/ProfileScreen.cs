using Godot;
using System;
using System.Linq;

/// <summary>
/// O perfil: identidade, patente com a barra de XP e estatísticas
/// vitalícias. Abre pelo bloco de identidade do header e pelo botão "Ver
/// perfil" da sala. (O painel com o histórico e as próximas patentes saiu
/// a pedido do Jhon, 21/11.)
///
/// Perfil de outro jogador (Fase 2) mostra tudo menos saldo e
/// inventário — por isso o saldo não aparece aqui também: ele já está no
/// header.
/// </summary>
public partial class ProfileScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Profile;

	protected override bool FollowsAccount => true;

	protected override void Build()
	{
		// Com muita informação o perfil passa da altura da tela: rola, em vez
		// de descer para fora dela (Jhon, 25/11).
		var scroll = UiKit.Fill(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
		AddChild(scroll);
		MarginContainer margin = UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4);
		margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(margin);
		HBoxContainer split = UiKit.Row(UiTokens.S4);
		margin.AddChild(split);

		split.AddChild(Identity());
		split.AddChild(Stats());
	}

	// ------------------------------------------------------------------ identidade

	private Control Identity()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S5, UiTokens.S3);
		panel.CustomMinimumSize = new Vector2(440, 0);

		int rank = Profile.Rank;

		RankBadge badge = RankBadge.Make(rank, 240, avatar: Avatars.Selected);
		badge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		column.AddChild(badge);

		Label name = UiKit.Text(Profile.PlayerName, "Display");
		name.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(name);

		HBoxContainer level = UiKit.Row(UiTokens.S2);
		level.Alignment = BoxContainer.AlignmentMode.Center;
		level.AddChild(LevelChip.Make(rank, 22));
		// Só o nível (Jhon, 21/11: o nome da fase, "Iniciante", não aparece).
		level.AddChild(UiKit.Text(Ranks.NameOf(rank).ToUpperInvariant(), "Subtitle", Ranks.InkOf(rank)));
		column.AddChild(level);

		if (!string.IsNullOrEmpty(Profile.Title))
		{
			PanelContainer title = UiKit.Chip(Profile.Title, UiTokens.Accent);
			title.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			column.AddChild(title);
		}

		column.AddChild(UiKit.Gap(UiTokens.S2));

		// XP: barra grande, quanto falta e qual é a próxima.
		long into = Profile.RankXp - Ranks.XpFor(rank);
		long span = Ranks.XpFor(rank + 1) - Ranks.XpFor(rank);
		bool max = rank >= Ranks.Count - 1;

		HBoxContainer xpHead = UiKit.Row(UiTokens.S2);
		xpHead.AddChild(UiKit.Caption("Experiência"));
		xpHead.AddChild(UiKit.Spacer());
		xpHead.AddChild(UiKit.Text(max ? "Patente máxima" : $"{UiTokens.Thousands(into)} / {UiTokens.Thousands(span)}", "Numeric"));
		column.AddChild(xpHead);

		var bar = new ProgressBar { CustomMinimumSize = new Vector2(0, 8), MaxValue = 1, Step = 0.001, Value = Ranks.Progress(Profile.RankXp), ShowPercentage = false };
		column.AddChild(bar);

		if (!max)
		{
			HBoxContainer next = UiKit.Row(UiTokens.S2);
			next.AddChild(UiKit.Text("Próxima:", "Muted"));
			next.AddChild(RankBadge.Make(rank + 1, 24, true));
			next.AddChild(UiKit.Text(Ranks.NameOf(rank + 1), null, Ranks.InkOf(rank + 1)));
			next.AddChild(UiKit.Spacer());
			next.AddChild(UiKit.Text(Loc.F("faltam {0} XP", UiTokens.Thousands(span - into)), "Caption"));
			column.AddChild(next);
		}

		column.AddChild(UiKit.Divider());

		column.AddChild(UiKit.Caption("Foto de perfil"));
		column.AddChild(Photos());

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
		bool canRename = Profile.HasAccount && Profile.Owns(AccountSync.RenameItem);
		Button rename = UiKit.Button(canRename ? "Trocar nome" : "Trocar nome (item da loja)", ButtonKind.Secondary,
			canRename ? Rename : () => Shell.GoShop("troca_nome"));
		column.AddChild(rename);

		Label created = UiKit.Text(Loc.F("Perfil criado em {0:dd/MM/yyyy}", Profile.CreatedUtc.ToLocalTime()), "Caption");
		created.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(created);

		return panel;
	}

	/// <summary>O nome é o da conta: quem troca é o servidor, que confere
	/// se está livre e gasta o item.</summary>
	/// <summary>As fotos de perfil: as suas para escolher, as da loja com cadeado e preço.</summary>
	private Control Photos()
	{
		var grid = new GridContainer { Columns = 7 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S2);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S2);
		foreach (Avatars.Photo photo in Avatars.All)
		{
			bool mine = Avatars.Owns(photo.Id), chosen = mine && Avatars.Selected == photo.Id;
			var cell = new Button
			{
				CustomMinimumSize = new Vector2(52, 52), Flat = true, FocusMode = FocusModeEnum.None,
				TooltipText = mine ? photo.Name : Loc.F("{0} · {1} de Cash na loja", photo.Name, UiTokens.Thousands(photo.Price)),
				Disabled = !mine
			};
			var face = new TextureRect
			{
				Texture = Avatars.PhotoOf(photo.Id), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
				Modulate = mine ? Colors.White : new Color(0.4f, 0.4f, 0.45f)
			};
			face.SetAnchorsPreset(LayoutPreset.FullRect);
			face.OffsetLeft = face.OffsetTop = 3; face.OffsetRight = face.OffsetBottom = -3;
			cell.AddChild(face);
			if (!mine)
			{
				Icon lockIcon = Icon.Make(IconKind.Lock, 16, UiTokens.TextPrimary);
				lockIcon.SetAnchorsPreset(LayoutPreset.Center);
				lockIcon.MouseFilter = MouseFilterEnum.Ignore;
				cell.AddChild(lockIcon);
			}
			if (chosen)
			{
				var ring = new Panel { MouseFilter = MouseFilterEnum.Ignore };
				var box = new StyleBoxFlat { DrawCenter = false, BorderColor = UiTokens.Accent };
				box.SetBorderWidthAll(3);
				box.SetCornerRadiusAll(4);
				ring.AddThemeStyleboxOverride("panel", box);
				ring.SetAnchorsPreset(LayoutPreset.FullRect);
				cell.AddChild(ring);
			}
			string id = photo.Id;
			cell.Pressed += () =>
			{
				Avatars.Selected = id;
				Profile.NotifyChanged();
				Refresh();
			};
			grid.AddChild(cell);
		}
		return grid;
	}

	private async void DoRename(string name)
	{
		string error = Profile.ValidateName(name) ?? await AccountSync.Rename(name.Trim());
		if (!IsInsideTree()) return;
		if (error == null) Shell.Toast("Nome trocado", UiTokens.Success);
		else Shell.Toast(error, UiTokens.Danger);
		Refresh();
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
			new Modal.Choice("Trocar", ButtonKind.Primary, () => DoRename(field.Text)));
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
		Tile(grid, "Vitórias", s.Wins.ToString(), Loc.F("{0:0}% das partidas", s.WinRate * 100));
		Tile(grid, "Derrotas · empates", $"{s.Losses} · {s.Draws}");
		Tile(grid, "Abates", UiTokens.Thousands(s.Kills));
		Tile(grid, "Mortes", UiTokens.Thousands(s.Deaths), Loc.F("K/D {0:0.00}", s.KillDeath));
		Tile(grid, "Assistências", UiTokens.Thousands(s.Assists));
		Tile(grid, "Na cabeça", UiTokens.Thousands(s.Headshots), s.Kills > 0 ? Loc.F("{0:0}% dos abates", s.Headshots * 100f / s.Kills) : null);
		Tile(grid, "MVP", s.Mvps.ToString());
		Tile(grid, "Tempo jogado", Hours(s.SecondsPlayed));

		column.AddChild(UiKit.Gap(UiTokens.S2));
		column.AddChild(UiKit.Caption("Humano contra lobo"));

		var forms = new GridContainer { Columns = 2 };
		forms.AddThemeConstantOverride("h_separation", UiTokens.S3);
		forms.AddThemeConstantOverride("v_separation", UiTokens.S3);
		column.AddChild(forms);

		Tile(forms, "Abates como humano", UiTokens.Thousands(s.KillsHuman), Loc.F("{0} como humano", Hours(s.SecondsHuman)), IconKind.Human);
		Tile(forms, "Abates como lobo", UiTokens.Thousands(s.KillsWolf), Loc.F("{0} como lobo", Hours(s.SecondsWolf)), IconKind.Wolf);

		HBoxContainer favorites = UiKit.Row(UiTokens.S3);
		column.AddChild(favorites);
		favorites.AddChild(Favorite("Arma favorita", s.FavoriteWeapon));

		return column;
	}

	private static string Hours(double seconds)
	{
		var t = TimeSpan.FromSeconds(seconds);
		return t.TotalHours >= 1 ? Loc.F("{0} h {1:00} min", (int)t.TotalHours, t.Minutes) : Loc.F("{0} min", t.Minutes);
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
}
