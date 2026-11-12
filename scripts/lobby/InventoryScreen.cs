using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O inventário, em duas colunas: à esquerda o que está equipado, à
/// direita o que você possui.
///
/// Esquerda, aba HUMANO: os quatro conjuntos em abas, cada uma com o
/// custo total em WP embaixo do nome — a peça central da economia: o
/// jogador monta quatro e escolhe um ao renascer. Abaixo, os lugares do
/// conjunto em coluna (primária, secundária, faca, granadas). Lugar que
/// existe mas ainda não pode ser usado mostra cadeado sobre listras —
/// diferente de lugar vazio.
///
/// Esquerda, aba LOBO: a classe e as duas tatuagens. Humano e lobo nunca
/// na mesma tela: o lobo tem lugares que o humano não tem.
///
/// Direita: os itens em cartões, com abas em dois níveis, e o painel de
/// detalhe com Equipar, Renovar e Descartar.
/// </summary>
public partial class InventoryScreen : LobbyScreen
{
    public override IconKind NavIcon => IconKind.Inventory;

    private static int _side;          // 0 humano, 1 lobo
    private static int _setIndex = 1;  // conjunto aberto
    private static int _top;           // aba de 1º nível
    private static int _sub;           // aba de 2º nível

    private string _selectedId;

    private static readonly string[] TopTabs = { "Armas", "Lobo", "Especial" };
    private static readonly string[][] SubTabs =
    {
        new[] { "Todas", "Primária", "Secundária", "Granada", "Corpo a corpo" },
        new[] { "Tatuagens", "Classes" },
        new[] { "Todos" }
    };

    public InventoryScreen() { }

    public InventoryScreen(string focus)
    {
        _selectedId = focus;
        CatalogItem item = Catalog.Get(focus);
        if (item == null) return;
        _top = item.Kind is ItemKind.Tattoo or ItemKind.Breed ? 1 : item.Kind == ItemKind.Service ? 2 : 0;
        _sub = 0;
        if (_top == 1) _side = 1;
    }

    private LoadoutSet CurrentSet => Profile.Sets[Mathf.Clamp(_setIndex, 0, Profile.Sets.Count - 1)];

    protected override void Build()
    {
        MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4));
        AddChild(margin);
        HBoxContainer split = UiKit.Row(UiTokens.S4);
        margin.AddChild(split);

        split.AddChild(Equipment());
        split.AddChild(Owned());
        split.AddChild(Detail());
    }

    // ------------------------------------------------------------------ esquerda

    private Control Equipment()
    {
        PanelContainer panel = UiKit.Panel("Surface1");
        panel.CustomMinimumSize = new Vector2(430, 0);

        MarginContainer margin = UiKit.Margin(UiTokens.S3);
        panel.AddChild(margin);
        VBoxContainer column = UiKit.Column(UiTokens.S3);
        margin.AddChild(column);

        HBoxContainer head = UiKit.Row(UiTokens.S2);
        head.AddChild(UiKit.Text("Equipado", "Subtitle"));
        head.AddChild(UiKit.Spacer());
        head.AddChild(Tabs.Make(new[] { "Humano", "Lobo" }, _side, i => { _side = i; Refresh(); }));
        column.AddChild(head);

        if (_side == 0) HumanSide(column);
        else WolfSide(column);

        return panel;
    }

    private void HumanSide(VBoxContainer column)
    {
        // Abas dos conjuntos, com o custo total embaixo do nome.
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
            Label cost = UiKit.Text(set.IsDefault ? "Grátis" : $"WP {set.TotalCost()}", "Numeric",
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
        field.TooltipText = current.IsDefault ? "O conjunto padrão é fixo: grátis, sempre disponível, rende +50 WP" : "";
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
        column.AddChild(SlotRow("Primária", Catalog.Get(current.Primary), () => Filter(0, 1),
            null));
        column.AddChild(SlotRow("Secundária", Catalog.Get(current.Secondary), () => Filter(0, 2), null));
        column.AddChild(SlotRow("Corpo a corpo", Catalog.Get("faca"), () => Filter(0, 4), null, fixedSlot: true));

        string utility = current.Utilities.FirstOrDefault();
        column.AddChild(SlotRow("Granada", Catalog.Get(utility), () => Filter(0, 3),
            utility == null || current.IsDefault ? null : () => { current.Utilities.Clear(); Profile.Save(); Refresh(); }));
        column.AddChild(LockedRow("Granada 2", "Luz e fumaça: em breve"));

        column.AddChild(UiKit.Spacer());

        HBoxContainer total = UiKit.Row(UiTokens.S2);
        total.AddChild(UiKit.Caption("Custo do conjunto"));
        total.AddChild(UiKit.Spacer());
        total.AddChild(UiKit.Text(current.IsDefault ? "Grátis  ·  +50 WP" : $"WP {current.TotalCost()}", "NumericLarge",
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
        column.AddChild(UiKit.Caption("Classe"));
        CatalogItem breed = Catalog.Get(Profile.Breed);
        column.AddChild(SlotRow("Classe", breed, () => Filter(1, 1), null));

        column.AddChild(UiKit.Gap(UiTokens.S2));
        column.AddChild(UiKit.Caption($"Tatuagens  ·  {Profile.Tattoos.Count}/{Profile.MaxTattoos}"));

        for (int i = 0; i < Profile.MaxTattoos; i++)
        {
            string id = i < Profile.Tattoos.Count ? Profile.Tattoos[i] : null;
            column.AddChild(SlotRow($"Tatuagem {i + 1}", Catalog.Get(id), () => Filter(1, 0),
                id == null ? null : () => { Profile.UnequipTattoo(id); Refresh(); }));
        }

        column.AddChild(UiKit.Spacer());
        column.AddChild(UiKit.Paragraph(
            "A classe define escudo, velocidade, pulo e garra. As tatuagens são ajustes pequenos por cima dela. " +
            "Tudo do lobo se compra com Presas, nunca com dinheiro."));
    }

    /// <summary>Um lugar do conjunto: arte pequena, nome, tipo e custo.
    /// Clicar filtra a grade da direita para o que cabe ali.</summary>
    private Control SlotRow(string slot, CatalogItem item, Action browse, Action remove, bool fixedSlot = false)
    {
        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, UiTokens.SlotHeight + 8),
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = fixedSlot ? "Fixo em todo conjunto" : $"Ver itens para {slot.ToLowerInvariant()}"
        };
        bool selected = item != null && item.Id == _selectedId;
        row.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface2,
            selected ? UiTokens.Accent : UiTokens.Border, selected ? 2 : 1, UiTokens.RadiusSmall, UiTokens.S2, UiTokens.S1));
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
        Label name = UiKit.Truncated(item?.Name ?? "Vazio", null, item == null ? UiTokens.TextDisabled : UiTokens.TextPrimary);
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

    private void Filter(int top, int sub)
    {
        _top = top;
        _sub = sub;
        Refresh();
    }

    // ------------------------------------------------------------------ grade

    private Control Owned()
    {
        VBoxContainer column = UiKit.Column(UiTokens.S2);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        HBoxContainer head = UiKit.Row(UiTokens.S3);
        head.AddChild(UiKit.Text("Inventário", "Title"));
        head.AddChild(UiKit.Spacer());
        head.AddChild(UiKit.Button("Ir à loja", ButtonKind.Secondary, () => Shell.GoShop()));
        column.AddChild(head);

        column.AddChild(Tabs.Make(TopTabs, _top, i => { _top = i; _sub = 0; Refresh(); }));
        // Segundo nível só quando há o que escolher.
        if (SubTabs[_top].Length > 1)
            column.AddChild(Tabs.Make(SubTabs[_top], Mathf.Min(_sub, SubTabs[_top].Length - 1), i => { _sub = i; Refresh(); }));

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        column.AddChild(scroll);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", UiTokens.S3);
        grid.AddThemeConstantOverride("v_separation", UiTokens.S3);
        scroll.AddChild(UiKit.Margin(UiTokens.S2));
        scroll.GetChild(0).AddChild(grid);

        List<CatalogItem> items = Items().ToList();
        foreach (CatalogItem item in items)
            grid.AddChild(ItemCard.Make(item, CardMode.Inventory, item.Id == _selectedId, Select));

        if (items.Count == 0)
            column.AddChild(UiKit.Paragraph("Nada aqui ainda. A loja vende armas, granadas e tatuagens por Presas."));

        return column;
    }

    private IEnumerable<CatalogItem> Items()
    {
        IEnumerable<CatalogItem> owned = Catalog.All.Where(i =>
            i.Kind == ItemKind.Breed
            || Profile.Find(i.Id) != null
            || i.Starter);

        return (_top, _sub) switch
        {
            (0, 1) => owned.Where(ItemInfo.IsPrimary),
            (0, 2) => owned.Where(ItemInfo.IsSecondary),
            (0, 3) => owned.Where(i => i.Kind == ItemKind.Utility),
            (0, 4) => owned.Where(i => i.Id == "faca"),
            (0, _) => owned.Where(i => i.Kind is ItemKind.Weapon or ItemKind.Utility),
            (1, 0) => owned.Where(i => i.Kind == ItemKind.Tattoo),
            (1, _) => owned.Where(i => i.Kind == ItemKind.Breed),
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
            return ItemDetail.Empty("Escolha um item para ver os atributos e equipar.");

        return ItemDetail.Make(item, CompareTarget(item), Actions(item));
    }

    /// <summary>O que está no mesmo lugar do conjunto aberto, para a
    /// comparação das barras.</summary>
    private CatalogItem CompareTarget(CatalogItem item)
    {
        if (ItemInfo.IsPrimary(item)) return Catalog.Get(CurrentSet.Primary);
        if (ItemInfo.IsSecondary(item)) return Catalog.Get(CurrentSet.Secondary);
        if (item.Kind == ItemKind.Breed) return Catalog.Get(Profile.Breed);
        return null;
    }

    private Control Actions(CatalogItem item)
    {
        VBoxContainer column = UiKit.Column(UiTokens.S2);
        bool usable = ItemInfo.Usable(item);
        bool expired = Profile.IsExpired(item.Id);

        Button equip = UiKit.Button(EquipLabel(item), ButtonKind.Primary, () => Equip(item));
        equip.Disabled = !usable || IsEquippedHere(item) || item.Kind == ItemKind.Service && !Profile.Owns(item.Id) || item.Id == "faca";
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
        ItemKind.Tattoo => Profile.Tattoos.Contains(item.Id) ? "Equipada" : "Equipar tatuagem",
        ItemKind.Breed => Profile.Breed == item.Id ? "Classe em uso" : "Usar esta classe",
        ItemKind.Service => "Usar",
        _ => IsEquippedHere(item) ? $"Equipada em {CurrentSet.Name}" : $"Equipar em {CurrentSet.Name}"
    };

    private bool IsEquippedHere(CatalogItem item) => item.Kind switch
    {
        ItemKind.Tattoo => Profile.Tattoos.Contains(item.Id),
        ItemKind.Breed => Profile.Breed == item.Id,
        ItemKind.Service => false,
        _ => CurrentSet.Primary == item.Id || CurrentSet.Secondary == item.Id || CurrentSet.Utilities.Contains(item.Id)
    };

    private void Equip(CatalogItem item)
    {
        switch (item.Kind)
        {
            case ItemKind.Tattoo:
                if (!Profile.EquipTattoo(item.Id, out string error)) { Shell.Toast(error, UiTokens.Danger); return; }
                Shell.Toast($"{item.Name} equipada");
                break;

            case ItemKind.Breed:
                Profile.SetBreed(item.Id);
                Shell.Toast($"Classe {item.Name} em uso");
                break;

            case ItemKind.Service:
                Shell.GoProfile();
                return;

            default:
                if (!EquipInSet(item)) return;
                break;
        }

        Refresh();
    }

    private bool EquipInSet(CatalogItem item)
    {
        LoadoutSet set = CurrentSet;
        if (set.IsDefault)
        {
            Shell.Toast("O conjunto padrão é fixo. Escolha outro conjunto para equipar.", UiTokens.Danger);
            return false;
        }

        LoadoutSet test = set.Clone();
        if (ItemInfo.IsPrimary(item)) test.Primary = item.Id;
        else if (ItemInfo.IsSecondary(item)) test.Secondary = item.Id;
        else if (item.Kind == ItemKind.Utility) test.Utilities = new List<string> { item.Id };

        if (!test.Validate(out string error))
        {
            Shell.Toast(error, UiTokens.Danger);
            return false;
        }

        set.Primary = test.Primary;
        set.Secondary = test.Secondary;
        set.Utilities = test.Utilities;
        Profile.Save();
        Shell.Toast($"{item.Name} equipada em {set.Name}");
        return true;
    }

    private void ConfirmDiscard(CatalogItem item)
    {
        Label body = UiKit.Paragraph($"{item.Name} sai do inventário. Se estiver num conjunto, o conjunto volta para o padrão naquele lugar. Não há reembolso.");
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
                    set.Utilities.Remove(item.Id);
                }
                Profile.Discard(item.Id);
                _selectedId = null;
                Shell.Toast($"{item.Name} descartada");
                Refresh();
            }),
            new Modal.Choice("Manter", ButtonKind.Primary, null));
    }
}
