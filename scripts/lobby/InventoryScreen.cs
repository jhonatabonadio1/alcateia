using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O inventário, feito para se ler em três passos (pedido do Jhon, 17/11:
/// "um inventário mais fácil de entender"):
///
/// No alto, **uma** escolha: Armas, Lobo, Personagem ou Especial. Antes
/// eram duas fileiras de abas independentes (o que está equipado de um
/// lado, o que você tem do outro), e dava para olhar o lobo equipado com
/// a grade mostrando armas.
///
/// Embaixo, da esquerda para a direita:
///
/// 1. **O que está equipado.** Em Armas, os quatro conjuntos (com o
///    custo em WP de cada um — a peça central da economia: o jogador
///    monta quatro e escolhe um ao renascer) e os espaços do conjunto
///    aberto. Em Lobo, a classe e as melhorias. Em Personagem, o de cada
///    time.
/// 2. **Clicar num espaço** mostra, no meio, só o que cabe nele — o
///    espaço fica marcado e o título do meio diz o que se está
///    escolhendo. Espaço que existe mas ainda não pode ser usado mostra
///    cadeado sobre listras, diferente de espaço vazio.
/// 3. **Clicar num item** abre o detalhe à direita, com Equipar, Renovar
///    e Descartar.
/// </summary>
public partial class InventoryScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Inventory;

	private static int _side;          // a seção: 0 armas, 1 lobo, 2 personagem, 3 especial
	private static int _setIndex = 1;  // conjunto aberto
	private static int _sub;           // o filtro dentro da seção (o espaço escolhido)

	private string _selectedId;

	private static readonly string[] Sections = { "Armas", "Lobo", "Personagem", "Especial" };
	private static readonly string[][] SubTabs =
	{
		new[] { "Todas", "Primária", "Secundária", "Granada", "Corpo a corpo" },
		new[] { "Melhorias", "Classes" },
		new[] { "Todos" },
		new[] { "Todos" }
	};

	public InventoryScreen() { }

	public InventoryScreen(string focus)
	{
		_selectedId = focus;
		CatalogItem item = Catalog.Get(focus);
		if (item == null) return;
		_side = item.Kind switch
		{
			ItemKind.Tattoo or ItemKind.Breed => 1,
			ItemKind.Character => 2,
			ItemKind.Service => 3,
			_ => 0
		};
		_sub = item.Kind == ItemKind.Breed ? 1 : 0;
	}

	private LoadoutSet CurrentSet => Profile.Sets[Mathf.Clamp(_setIndex, 0, Profile.Sets.Count - 1)];

	protected override void Build()
	{
		VBoxContainer page = Page(UiTokens.S3);

		// Uma escolha só no alto: o que se está arrumando.
		HBoxContainer head = UiKit.Row(UiTokens.S4);
		head.AddChild(UiKit.Text("Inventário", "Title"));
		Control sections = Tabs.Make(Sections, _side, i => { _side = i; _sub = 0; _selectedId = null; Refresh(); });
		sections.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		head.AddChild(sections);
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.Button("Ir à loja", ButtonKind.Secondary, () => Shell.GoShop()));
		page.AddChild(head);

		HBoxContainer split = UiKit.Row(UiTokens.S4);
		split.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(split);

		split.AddChild(Equipment());
		split.AddChild(Owned());
		split.AddChild(Detail());
	}

	/// <summary>O título de um passo: o número numa ficha e o que fazer.</summary>
	private static Control Step(int number, string text)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		var chip = new PanelContainer { CustomMinimumSize = new Vector2(22, 22), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		chip.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Accent, default, 0, UiTokens.RadiusSmall, 0, 0));
		Label digit = UiKit.Text(number.ToString(), "Numeric", UiTokens.AccentText);
		digit.HorizontalAlignment = HorizontalAlignment.Center;
		digit.AddThemeFontOverride("font", UiFonts.Bold);
		chip.AddChild(digit);
		row.AddChild(chip);
		Label label = UiKit.Text(text, null, UiTokens.TextPrimary);
		label.AddThemeFontOverride("font", UiFonts.SemiBold);
		row.AddChild(label);
		return row;
	}

	// ------------------------------------------------------------------ esquerda

	private Control Equipment()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S3);
		panel.CustomMinimumSize = new Vector2(430, 0);

		column.AddChild(UiKit.Text(_side switch
		{
			0 => "Seus conjuntos",
			1 => "Seu lobo",
			2 => "Seus personagens",
			_ => "Serviços"
		}, "Subtitle"));

		if (_side == 0) HumanSide(column);
		else if (_side == 1) WolfSide(column);
		else if (_side == 2) CharacterSide(column);
		else
		{
			column.AddChild(UiKit.Paragraph(
				"Serviços são itens de usar uma vez, como a troca de nome. Os que você comprou aparecem ao lado: " +
				"clique num deles e depois em Usar."));
			column.AddChild(UiKit.Spacer());
		}

		return panel;
	}

	private void HumanSide(VBoxContainer column)
	{
		// Abas dos conjuntos, com o custo total embaixo do nome.
		column.AddChild(Step(1, "Escolha o conjunto"));
		HBoxContainer sets = UiKit.Row(UiTokens.S1);
		for (int i = 0; i < Profile.Sets.Count; i++)
		{
			int index = i;
			LoadoutSet set = Profile.Sets[i];
			bool active = i == _setIndex;

			var tab = new Button
			{
				ToggleMode = true,
				ButtonPressed = active,
				CustomMinimumSize = new Vector2(96, 54),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				ThemeTypeVariation = active ? "" : "TertiaryButton",
				FocusMode = FocusModeEnum.All,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			if (active)
			{
				StyleBoxFlat on = UiTheme.Box(UiTokens.Surface3, UiTokens.Accent, 0, UiTokens.RadiusSmall, UiTokens.S1, UiTokens.S1);
				on.BorderWidthBottom = 2;
				tab.AddThemeStyleboxOverride("normal", on);
				tab.AddThemeStyleboxOverride("pressed", on);
				tab.AddThemeStyleboxOverride("hover_pressed", on);
			}
			tab.Pressed += () => { _setIndex = index; Refresh(); };
			Sfx.Wire(tab);

			VBoxContainer label = UiKit.Fill(UiKit.Column(0));
			label.Alignment = BoxContainer.AlignmentMode.Center;
			label.MouseFilter = MouseFilterEnum.Ignore;
			Label name = UiKit.Truncated(set.Name, null, active ? UiTokens.TextPrimary : UiTokens.TextSecondary);
			name.HorizontalAlignment = HorizontalAlignment.Center;
			name.AddThemeFontOverride("font", UiFonts.SemiBold);
			label.AddChild(name);
			Label cost = UiKit.Text(set.IsDefault ? "Grátis" : Loc.F("WP {0}", set.TotalCost()), "Numeric",
				set.IsDefault ? UiTokens.Success : UiTokens.Accent);
			cost.HorizontalAlignment = HorizontalAlignment.Center;
			cost.AddThemeFontSizeOverride("font_size", UiTokens.Caption);
			label.AddChild(cost);
			tab.AddChild(label);

			sets.AddChild(tab);
		}
		column.AddChild(sets);

		LoadoutSet current = CurrentSet;

		// Nome do conjunto (o padrão tem nome fixo).
		LineEdit field = UiKit.Field(current.Name, "Nome do conjunto", LoadoutSet.MaxNameLength);
		field.Editable = !current.IsDefault;
		field.TooltipText = current.IsDefault ? "O conjunto padrão: grátis, sempre disponível, rende +50 WP. Aceita as armas comuns e a granada" : "";
		void Rename()
		{
			string name = field.Text.Trim();
			if (current.IsDefault || name.Length == 0 || name == current.Name) return;
			current.Name = name;
			Profile.Save();
			Refresh();
		}
		field.TextSubmitted += _ => Rename();
		field.FocusExited += Rename;
		column.AddChild(field);

		// Lugares do conjunto.
		column.AddChild(UiKit.Gap(UiTokens.S1));
		column.AddChild(Step(2, "Clique no espaço que quer trocar"));
		column.AddChild(SlotRow("Primária", Catalog.Get(current.Primary), () => Filter(0, 1),
			null, choosing: _sub == 1));
		column.AddChild(SlotRow("Secundária", Catalog.Get(current.Secondary), () => Filter(0, 2), null, choosing: _sub == 2));
		column.AddChild(SlotRow("Corpo a corpo", Catalog.Get(Arsenal.IsKnife(current.Melee) ? current.Melee : Arsenal.KnifeId), () => Filter(0, 4), null, choosing: _sub == 4));

		string utility = current.Utilities.FirstOrDefault();
		column.AddChild(SlotRow("Granada", Catalog.Get(utility), () => Filter(0, 3),
			utility == null ? null : () => { current.Utilities.Clear(); Profile.Save(); Refresh(); }, choosing: _sub == 3));
		column.AddChild(LockedRow("Granada 2", "Luz e fumaça: em breve"));

		column.AddChild(UiKit.Spacer());

		HBoxContainer total = UiKit.Row(UiTokens.S2);
		Label costLabel = UiKit.Caption("Custo do conjunto");
		costLabel.TooltipText = "WP é o que você gasta cada vez que renasce com este conjunto. Ganha-se WP jogando a partida.";
		costLabel.MouseFilter = MouseFilterEnum.Stop;
		total.AddChild(costLabel);
		total.AddChild(UiKit.Spacer());
		total.AddChild(UiKit.Text(current.IsDefault ? "Grátis  ·  +50 WP" : Loc.F("WP {0}", current.TotalCost()), "NumericLarge",
			current.IsDefault ? UiTokens.Success : UiTokens.Accent));
		column.AddChild(total);

		bool valid = current.Validate(out string error);
		HBoxContainer status = UiKit.Row(UiTokens.S2);
		status.AddChild(Icon.Make(valid ? IconKind.Check : IconKind.Warning, 14, valid ? UiTokens.Success : UiTokens.Danger));
		status.AddChild(UiKit.Text(valid ? "Pronto para usar na partida" : error, "Caption", valid ? UiTokens.Success : UiTokens.Danger));
		column.AddChild(status);
	}

	private void WolfSide(VBoxContainer column)
	{
		column.AddChild(Step(1, "Clique no espaço que quer trocar"));
		column.AddChild(UiKit.Caption("Classe"));
		CatalogItem breed = Catalog.Get(Profile.Breed);
		column.AddChild(SlotRow("Classe", breed, () => Filter(1, 1), null, choosing: _sub == 1));

		column.AddChild(UiKit.Gap(UiTokens.S2));
		column.AddChild(UiKit.Caption(Loc.F("Melhorias  ·  {0}/{1}", Profile.Tattoos.Count, Profile.MaxTattoos)));

		for (int i = 0; i < Profile.MaxTattoos; i++)
		{
			string id = i < Profile.Tattoos.Count ? Profile.Tattoos[i] : null;
			column.AddChild(SlotRow(Loc.F("Melhoria {0}", i + 1), Catalog.Get(id), () => Filter(1, 0),
				id == null ? null : () => { Profile.UnequipTattoo(id); Refresh(); }, choosing: _sub == 0));
		}

		column.AddChild(UiKit.Spacer());
		column.AddChild(UiKit.Paragraph(
			"A classe define escudo, velocidade, pulo e garra. As melhorias são ajustes pequenos por cima dela. " +
			"Tudo do lobo se compra com Presas, nunca com dinheiro."));
	}

	private void CharacterSide(VBoxContainer column)
	{
		foreach (int team in new[] { Teams.Blue, Teams.Orange })
		{
			column.AddChild(UiKit.Caption(Teams.TitleOf(team), Teams.ColorOf(team)));
			CatalogItem item = Catalog.Get(Profile.CharacterIdFor(team));
			string standard = team == Teams.Blue ? "Padrão (Eve)" : "Padrão (Barut)";
			column.AddChild(SlotRow("Personagem", item, () => Filter(2, 0),
				item == null ? null : () => { Profile.ClearCharacter(team); Refresh(); }, emptyName: standard));
			column.AddChild(UiKit.Gap(UiTokens.S2));
		}

		column.AddChild(UiKit.Spacer());
		column.AddChild(UiKit.Paragraph(
			"O personagem vale no time dele, com o lobo dele: escolheu a pessoa, transforma no lobo dela. " +
			"Sem personagem, o time usa o de sempre."));
	}

	/// <summary>Um lugar do conjunto: arte pequena, nome, tipo e custo.
	/// Clicar filtra a grade da direita para o que cabe ali.</summary>
	private Control SlotRow(string slot, CatalogItem item, Action browse, Action remove, bool fixedSlot = false, string emptyName = "Vazio",
		bool choosing = false)
	{
		var row = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, UiTokens.SlotHeight + 8),
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = fixedSlot ? "Fixo em todo conjunto" : Loc.F("Ver itens para {0}", slot.ToLowerInvariant())
		};
		// Marcado: é o espaço cujos itens estão na grade do meio.
		row.AddThemeStyleboxOverride("panel", UiTheme.Box(
			choosing ? new Color(UiTokens.Accent, 0.1f) : UiTheme.Veil(0.04f),
			choosing ? UiTokens.Accent : UiTokens.Border, choosing ? 2 : 1, UiTokens.RadiusSmall, UiTokens.S2, UiTokens.S1));
		row.GuiInput += e =>
		{
			if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
			Sfx.Ui("ui_clique");
			if (item != null) _selectedId = item.Id;
			browse?.Invoke();
		};

		HBoxContainer line = UiKit.Row(UiTokens.S3);
		line.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(line);

		if (item == null)
		{
			var empty = new PanelContainer { CustomMinimumSize = new Vector2(112, 52), MouseFilter = MouseFilterEnum.Ignore };
			empty.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, UiTokens.Border, 1, 0, 0, 0));
			empty.AddChild(new Striped());
			line.AddChild(empty);
		}
		else
		{
			WeaponArt art = WeaponArt.Make(item.Id, new Vector2(112, 52));
			art.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			line.AddChild(art);
		}

		VBoxContainer text = UiKit.Column(0);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.AddChild(UiKit.Caption(slot));
		Label name = UiKit.Truncated(item?.Name ?? emptyName, null, item == null ? UiTokens.TextDisabled : UiTokens.TextPrimary);
		name.AddThemeFontOverride("font", UiFonts.SemiBold);
		text.AddChild(name);
		line.AddChild(text);

		if (item != null)
		{
			string wp = ItemInfo.WpCost(item);
			if (wp != "") line.AddChild(UiKit.Text(wp, "Numeric", UiTokens.Accent));
		}

		if (remove != null)
		{
			Button x = UiKit.IconButton(IconKind.Close, "Tirar", remove, 14);
			x.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			line.AddChild(x);
		}

		// A seta diz que o espaço abre uma escolha; o fixo leva cadeado.
		Icon hint = Icon.Make(fixedSlot ? IconKind.Lock : IconKind.Expand, 14, choosing ? UiTokens.Accent : UiTokens.TextDisabled);
		line.AddChild(hint);

		return row;
	}

	private static Control LockedRow(string slot, string why)
	{
		var row = new PanelContainer { CustomMinimumSize = new Vector2(0, UiTokens.SlotHeight + 8), TooltipText = why };
		row.AddThemeStyleboxOverride("panel", UiTheme.Box(Colors.Transparent, UiTokens.Border, 1, UiTokens.RadiusSmall, 0, 0));
		row.AddChild(new Striped());

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		HBoxContainer line = UiKit.Row(UiTokens.S2);
		line.AddChild(Icon.Make(IconKind.Lock, 18, UiTokens.TextSecondary));
		line.AddChild(UiKit.Caption($"{slot}  ·  {why}"));
		center.AddChild(line);
		row.AddChild(center);
		return row;
	}

	private void Filter(int section, int sub)
	{
		_side = section;
		_sub = sub;
		Refresh();
	}

	/// <summary>O que a grade do meio está mostrando, em palavras.</summary>
	private string Choosing() => (_side, _sub) switch
	{
		(0, 1) => "Escolha a primária",
		(0, 2) => "Escolha a secundária",
		(0, 3) => "Escolha a granada",
		(0, 4) => "Corpo a corpo",
		(0, _) => "Todas as suas armas",
		(1, 0) => "Escolha as melhorias",
		(1, _) => "Escolha a classe do lobo",
		(2, _) => "Escolha o personagem",
		_ => "Seus serviços"
	};

	// ------------------------------------------------------------------ grade

	private Control Owned()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		int step = _side == 0 ? 3 : _side == 3 ? 1 : 2;
		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(Step(step, Choosing()));
		column.AddChild(head);

		// O filtro (o mesmo que os espaços da esquerda ligam), numa linha
		// própria: ao lado do título a tela não cabia em 1920.
		string[] filters = SubTabs[Mathf.Clamp(_side, 0, SubTabs.Length - 1)];
		if (filters.Length > 1)
			column.AddChild(Tabs.Make(filters, Mathf.Min(_sub, filters.Length - 1), i => { _sub = i; Refresh(); }));

		GridContainer grid = UiKit.ScrollGrid(column, 3);

		List<CatalogItem> items = Items().ToList();
		foreach (CatalogItem item in items)
			grid.AddChild(ItemCard.Make(item, CardMode.Inventory, item.Id == _selectedId, Select));

		if (items.Count == 0)
			column.AddChild(UiKit.Paragraph("Nada aqui ainda. A loja vende armas, granadas e melhorias por Presas."));

		return column;
	}

	private IEnumerable<CatalogItem> Items()
	{
		IEnumerable<CatalogItem> owned = Catalog.All.Where(i =>
			i.Kind == ItemKind.Breed
			|| Profile.Find(i.Id) != null
			|| i.Starter);

		return (_side, _sub) switch
		{
			(0, 1) => owned.Where(ItemInfo.IsPrimary),
			(0, 2) => owned.Where(ItemInfo.IsSecondary),
			(0, 3) => owned.Where(i => i.Kind == ItemKind.Utility),
			(0, 4) => owned.Where(i => Arsenal.IsKnife(i.Id)),
			(0, _) => owned.Where(i => i.Kind is ItemKind.Weapon or ItemKind.Utility),
			(1, 0) => owned.Where(i => i.Kind == ItemKind.Tattoo),
			(1, _) => owned.Where(i => i.Kind == ItemKind.Breed),
			(2, _) => owned.Where(i => i.Kind == ItemKind.Character),
			_ => owned.Where(i => i.Kind == ItemKind.Service)
		};
	}

	private void Select(CatalogItem item)
	{
		_selectedId = item.Id;
		Refresh();
	}

	// ------------------------------------------------------------------ detalhe

	private Control Detail()
	{
		CatalogItem item = Catalog.Get(_selectedId);
		if (item == null)
			return ItemDetail.Empty(_side == 0
				? "1. Escolha o conjunto.\n2. Clique no espaço que quer trocar.\n3. Clique na arma: aqui aparecem os atributos e o botão Equipar."
				: "Clique num espaço à esquerda e depois num item: aqui aparecem os detalhes e o botão de usar.");

		return ItemDetail.Make(item, CompareTarget(item), Actions(item));
	}

	/// <summary>O que está no mesmo lugar do conjunto aberto, para a
	/// comparação das barras.</summary>
	private CatalogItem CompareTarget(CatalogItem item)
	{
		if (ItemInfo.IsPrimary(item)) return Catalog.Get(CurrentSet.Primary);
		if (ItemInfo.IsSecondary(item)) return Catalog.Get(CurrentSet.Secondary);
		if (item.Kind == ItemKind.Breed) return Catalog.Get(Profile.Breed);
		if (item.Kind == ItemKind.Character)
			return Catalog.Get(Profile.CharacterIdFor(item.Team == 0 ? Teams.Orange : item.Team));
		return null;
	}

	private Control Actions(CatalogItem item)
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		bool usable = ItemInfo.Usable(item);
		bool expired = Profile.IsExpired(item.Id);

		Button equip = UiKit.Button(EquipLabel(item), ButtonKind.Primary, () => Equip(item));
		equip.Disabled = !usable || IsEquippedHere(item) || item.Kind == ItemKind.Service && !Profile.Owns(item.Id);
		column.AddChild(equip);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		Button renew = UiKit.Button(expired ? "Renovar" : "Mais tempo", ButtonKind.Secondary, () => Shell.GoShop(item.Id));
		renew.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		renew.Disabled = !item.ForSale || item.Starter || Profile.Find(item.Id)?.IsPermanent == true && !item.Consumable;
		row.AddChild(renew);

		Button discard = UiKit.Button("Descartar", ButtonKind.Danger, () => ConfirmDiscard(item));
		discard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		discard.Disabled = item.Starter || item.Kind == ItemKind.Breed || Profile.Find(item.Id) == null;
		row.AddChild(discard);
		column.AddChild(row);

		return column;
	}

	private string EquipLabel(CatalogItem item) => item.Kind switch
	{
		ItemKind.Tattoo => Profile.Tattoos.Contains(item.Id) ? "Equipada" : "Equipar melhoria",
		ItemKind.Breed => Profile.Breed == item.Id ? "Classe em uso" : "Usar esta classe",
		ItemKind.Character => IsEquippedHere(item) ? Loc.F("Em uso {0}", TeamLabel(item)) : Loc.F("Usar {0}", TeamLabel(item)),
		ItemKind.Service => "Usar",
		_ => IsEquippedHere(item) ? Loc.F("Equipada em {0}", CurrentSet.Name) : Loc.F("Equipar em {0}", CurrentSet.Name)
	};

	private bool IsEquippedHere(CatalogItem item) => item.Kind switch
	{
		ItemKind.Tattoo => Profile.Tattoos.Contains(item.Id),
		ItemKind.Breed => Profile.Breed == item.Id,
		ItemKind.Character => (item.Team is 0 or Teams.Blue ? Profile.BlueCharacter == item.Id : true)
			&& (item.Team is 0 or Teams.Orange ? Profile.OrangeCharacter == item.Id : true),
		ItemKind.Service => false,
		_ => CurrentSet.Primary == item.Id || CurrentSet.Secondary == item.Id || CurrentSet.Melee == item.Id || CurrentSet.Utilities.Contains(item.Id)
	};

	private void Equip(CatalogItem item)
	{
		switch (item.Kind)
		{
			case ItemKind.Tattoo:
				if (!Profile.EquipTattoo(item.Id, out string error)) { Shell.Toast(error, UiTokens.Danger); return; }
				Shell.Toast(Loc.F("{0} equipada", item.Name));
				break;

			case ItemKind.Breed:
				Profile.SetBreed(item.Id);
				Shell.Toast(Loc.F("Classe {0} em uso", item.Name));
				break;

			case ItemKind.Character:
				if (!Profile.SetCharacter(item.Id, out string why)) { Shell.Toast(why, UiTokens.Danger); return; }
				Shell.Toast(Loc.F("{0} em uso {1}", item.Name, TeamLabel(item)));
				break;

			case ItemKind.Service:
				Shell.GoProfile();
				return;

			default:
				if (!EquipInSet(item)) return;
				break;
		}

		Sfx.Ui("ui_equipar");
		Refresh();
	}

	private static string TeamLabel(CatalogItem item) =>
		item.Team == 0 ? "na Vanguarda e no Sindicato" : item.Team == Teams.Blue ? "na Vanguarda" : "no Sindicato";

	private bool EquipInSet(CatalogItem item)
	{
		LoadoutSet set = CurrentSet;
		// O padrão aceita as armas comuns e a granada
		// (LoadoutSet.AllowedInDefault).
		if (set.IsDefault && !LoadoutSet.AllowedInDefault(item.Id))
		{
			Shell.Toast(Loc.F("{0} não pode ir no conjunto padrão. Escolha outro conjunto para equipar.", item.Name), UiTokens.Danger);
			return false;
		}

		LoadoutSet test = set.Clone();
		if (ItemInfo.IsPrimary(item)) test.Primary = item.Id;
		else if (ItemInfo.IsSecondary(item)) test.Secondary = item.Id;
		else if (Arsenal.IsKnife(item.Id)) test.Melee = item.Id;
		else if (item.Kind == ItemKind.Utility) test.Utilities = new List<string> { item.Id };

		if (!test.Validate(out string error))
		{
			Shell.Toast(error, UiTokens.Danger);
			return false;
		}

		set.Primary = test.Primary;
		set.Secondary = test.Secondary;
		set.Melee = test.Melee;
		set.Utilities = test.Utilities;
		Profile.Save();
		Shell.Toast(Loc.F("{0} equipada em {1}", item.Name, set.Name));
		return true;
	}

	private void ConfirmDiscard(CatalogItem item)
	{
		Label body = UiKit.Paragraph(Loc.F("{0} sai do inventário. Se estiver num conjunto, o conjunto volta para o padrão naquele lugar. Não há reembolso.", item.Name));
		body.CustomMinimumSize = new Vector2(380, 0);

		// Destrutivo à esquerda e o botão seguro à direita, onde o olho
		// procura o primário: descartar por engano tem que exigir querer.
		Shell.Modal("Descartar item?", body,
			new Modal.Choice("Descartar", ButtonKind.Danger, () =>
			{
				foreach (LoadoutSet set in Profile.Sets)
				{
					if (set.Primary == item.Id) set.Primary = Arsenal.DefaultPrimary;
					if (set.Secondary == item.Id) set.Secondary = Arsenal.DefaultSecondary;
					if (set.Melee == item.Id) set.Melee = Arsenal.KnifeId;
					set.Utilities.Remove(item.Id);
				}
				Profile.Discard(item.Id);
				_selectedId = null;
				Shell.Toast(Loc.F("{0} descartada", item.Name));
				Refresh();
			}),
			new Modal.Choice("Manter", ButtonKind.Primary, null));
	}
}
