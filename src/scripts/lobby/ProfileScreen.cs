using Godot;
using System;
using System.Linq;

/// <summary>
/// O perfil: identidade, patente com a barra de XP e estatísticas
/// vitalícias. Abre pelo bloco de identidade do header e pelo botão "Ver
/// perfil" da sala. (O painel com o histórico e as próximas patentes saiu
/// a pedido do Jhon, 21/11.)
///
/// **A foto de perfil se escolhe em outra tela** (Jhon, 27/11: "em perfil,
/// a área de ícones deve ficar em um lugar melhor, em outra tela"):
/// <see cref="PhotoScreen"/>, que abre clicando na própria foto ou em
/// "Trocar foto". Aqui a grade apertava a identidade e fazia a tela rolar;
/// sem ela o perfil cabe inteiro.
///
/// Perfil de outro jogador (Fase 2) mostra tudo menos saldo e
/// inventário — por isso o saldo não aparece aqui também: ele já está no
/// header.
/// </summary>
public partial class ProfileScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Profile;

	protected override bool FollowsAccount => true;

	private bool _shown;

	protected override void Build()
	{
		VBoxContainer page = Page(UiTokens.S3);
		page.AddChild(Deco.Title("Perfil", "Sua carreira na liga"));

		HBoxContainer split = UiKit.Row(UiTokens.S4);
		split.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(split);

		Control identity = Identity();
		Control stats = Stats();
		split.AddChild(identity);
		split.AddChild(stats);

		if (_shown) return;
		_shown = true;
		Motion.Rise(identity, 0.04f, new Vector2(-28, 0));
		Motion.Rise(stats, 0.1f, new Vector2(0, 28));
	}

	// ------------------------------------------------------------------ identidade

	private Control Identity()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S5, UiTokens.S3);
		panel.CustomMinimumSize = new Vector2(440, 0);

		int rank = Profile.Rank;

		// A foto é o botão de trocar a foto.
		RankBadge badge = RankBadge.Make(rank, 232, avatar: Avatars.Selected);
		badge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		badge.MouseFilter = MouseFilterEnum.Stop;
		badge.MouseDefaultCursorShape = CursorShape.PointingHand;
		badge.TooltipText = Loc.T("Trocar a foto de perfil");
		badge.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Shell.GoPhotos();
		};
		column.AddChild(badge);

		Label name = UiKit.Text(Profile.PlayerName, "Display");
		name.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(name);

		// A tag (Nome#0042): é com ela que um amigo adiciona. Clicar copia.
		if (Backend.LoggedIn && Backend.AccountTag.Length > 0)
		{
			Button tag = UiKit.Button("#" + Backend.AccountTag, ButtonKind.Tertiary, () =>
			{
				DisplayServer.ClipboardSet(Backend.AccountHandle);
				Shell.Toast(Loc.F("{0} copiado: passe para um amigo adicionar você", Backend.AccountHandle), UiTokens.Success);
			});
			tag.TooltipText = Loc.F("A sua tag. Clique para copiar {0}", Backend.AccountHandle);
			tag.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			column.AddChild(tag);
		}

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
		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		Button photo = UiKit.Button("Trocar foto", ButtonKind.Secondary, () => Shell.GoPhotos());
		photo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		buttons.AddChild(photo);
		Button rename = UiKit.Button(canRename ? "Trocar nome" : "Trocar nome (item da loja)", ButtonKind.Secondary,
			canRename ? Rename : () => Shell.GoShop("troca_nome"));
		rename.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		buttons.AddChild(rename);
		column.AddChild(buttons);

		Label created = UiKit.Text(Loc.F("Perfil criado em {0:dd/MM/yyyy}", Profile.CreatedUtc.ToLocalTime()), "Caption");
		created.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(created);

		return panel;
	}

	/// <summary>O nome é o da conta: quem troca é o servidor, que confere
	/// se está livre e gasta o item.</summary>
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

		column.AddChild(Deco.Kicker("Estatísticas"));

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
