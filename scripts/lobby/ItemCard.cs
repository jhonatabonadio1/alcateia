using Godot;
using System;

public enum CardMode { Inventory, Shop }

/// <summary>
/// O cartão de item, o mesmo na loja e no inventário — se os dois
/// parecem telas de jogos diferentes, o jogador sente.
///
/// Horizontal e grande: a arte ocupa a metade de cima, e o texto fica
/// pequeno embaixo (nome à esquerda; tipo, custo em WP e duração à
/// direita). O estado vem por rótulo e por moldura, em camadas que
/// convivem no mesmo cartão:
///
/// equipado ("EQUIPADO" sobre a arte), especial (moldura na cor da
/// raridade, com brilho), expirando (âmbar), expirado (vermelho, arte
/// apagada), bloqueado por patente (cadeado sobre fundo listrado), e na
/// loja o selo da moeda no canto — o jogador filtra pelo que pode pagar
/// antes de ler qualquer preço.
/// </summary>
public partial class ItemCard : PanelContainer
{
    public CatalogItem Item { get; private set; }

    public static readonly Vector2 Size2 = new(296, 196);

    private Action<CatalogItem> _clicked;
    private bool _selected;

    public static ItemCard Make(CatalogItem item, CardMode mode, bool selected, Action<CatalogItem> clicked)
    {
        var card = new ItemCard { Item = item, _clicked = clicked, _selected = selected };
        card.Build(mode);
        return card;
    }

    private void Build(CardMode mode)
    {
        CustomMinimumSize = Size2;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.All;

        bool locked = ItemInfo.Locked(Item) && !(mode == CardMode.Inventory && Profile.Owns(Item.Id));
        bool expired = Profile.IsExpired(Item.Id);
        Color rarity = ItemInfo.RarityColor(Item);

        Style(rarity);

        VBoxContainer column = UiKit.Column(0);
        column.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(column);

        // ------------------------------------------------ arte
        var art = new Control { CustomMinimumSize = new Vector2(0, 116), MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        column.AddChild(art);

        var backdrop = UiKit.Fill(new ColorRect { Color = new Color(rarity, 0.06f), MouseFilter = MouseFilterEnum.Ignore });
        art.AddChild(backdrop);

        if (locked)
        {
            art.AddChild(UiKit.Fill(new Striped()));
        }

        WeaponArt picture = UiKit.Fill(WeaponArt.Make(Item.Id, Vector2.Zero, dim: locked || expired));
        picture.OffsetLeft = UiTokens.S3;
        picture.OffsetRight = -UiTokens.S3;
        picture.OffsetTop = UiTokens.S3;
        picture.OffsetBottom = -UiTokens.S2;
        art.AddChild(picture);

        // Cantos da arte numa camada própria: estado à esquerda, selo à
        // direita, e, embaixo, o cadeado com a patente quando bloqueado.
        MarginContainer corners = UiKit.Fill(UiKit.Margin(UiTokens.S2));
        corners.MouseFilter = MouseFilterEnum.Ignore;
        art.AddChild(corners);
        VBoxContainer layers = UiKit.Column(0);
        layers.MouseFilter = MouseFilterEnum.Ignore;
        corners.AddChild(layers);

        HBoxContainer top = UiKit.Row(UiTokens.S1);
        top.MouseFilter = MouseFilterEnum.Ignore;
        layers.AddChild(top);

        Control topLeft = TopLeftTag(mode, expired);
        if (topLeft != null) top.AddChild(topLeft);
        top.AddChild(UiKit.Spacer());

        // Na loja, o selo da moeda; no inventário, a raridade.
        top.AddChild(mode == CardMode.Shop && Item.ForSale
            ? UiKit.Chip("Presas", CurrencyLabel.ColorOf(Currency.Presas), solid: true)
            : UiKit.Chip(ItemInfo.RarityName(Item), rarity));

        layers.AddChild(UiKit.Spacer());

        if (locked)
        {
            var lockChip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            lockChip.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTokens.Bg, 0.92f), UiTokens.BorderStrong, 1, UiTokens.RadiusSmall, UiTokens.S2, 2));
            HBoxContainer lockRow = UiKit.Row(UiTokens.S1);
            lockRow.AddChild(Icon.Make(IconKind.Lock, 14, UiTokens.Warning));
            lockRow.AddChild(UiKit.Caption(Ranks.NameOf(Item.MinRank), UiTokens.TextPrimary));
            lockChip.AddChild(lockRow);
            layers.AddChild(lockChip);
        }

        // ------------------------------------------------ texto
        MarginContainer textMargin = UiKit.Margin(UiTokens.S3, UiTokens.S2, UiTokens.S3, UiTokens.S2);
        textMargin.MouseFilter = MouseFilterEnum.Ignore;
        textMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
        column.AddChild(textMargin);

        HBoxContainer info = UiKit.Row(UiTokens.S2);
        info.MouseFilter = MouseFilterEnum.Ignore;
        textMargin.AddChild(info);

        VBoxContainer left = UiKit.Column(0);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        left.MouseFilter = MouseFilterEnum.Ignore;
        Label name = UiKit.Truncated(Item.Name, null);
        name.AddThemeFontOverride("font", UiFonts.SemiBold);
        name.AddThemeFontSizeOverride("font_size", 17);
        left.AddChild(name);
        left.AddChild(UiKit.Text(Item.Type, "Caption"));
        info.AddChild(left);

        VBoxContainer right = UiKit.Column(0);
        right.MouseFilter = MouseFilterEnum.Ignore;
        right.SizeFlagsVertical = SizeFlags.ShrinkEnd;

        string wp = ItemInfo.WpCost(Item);
        if (wp != "")
        {
            Label wpLabel = UiKit.Text(wp, "Numeric", UiTokens.Accent);
            wpLabel.HorizontalAlignment = HorizontalAlignment.Right;
            right.AddChild(wpLabel);
        }

        if (mode == CardMode.Shop)
            right.AddChild(PriceLine());
        else
        {
            Label duration = UiKit.Text(ItemInfo.Duration(Item, out Color color), "Caption", color);
            duration.HorizontalAlignment = HorizontalAlignment.Right;
            right.AddChild(duration);
        }

        info.AddChild(right);
    }

    private void Style(Color rarity)
    {
        bool special = ItemInfo.IsSpecial(Item);
        StyleBoxFlat box = UiTheme.Box(UiTokens.Surface2,
            _selected ? UiTokens.Accent : special ? new Color(rarity, 0.8f) : UiTokens.Border,
            _selected || special ? 2 : 1, UiTokens.RadiusPanel, 0, 0);

        if (special)
        {
            // Brilho da moldura especial.
            box.ShadowColor = new Color(rarity, 0.28f);
            box.ShadowSize = 10;
        }

        AddThemeStyleboxOverride("panel", box);
    }

    private Control TopLeftTag(CardMode mode, bool expired)
    {
        if (mode == CardMode.Inventory)
        {
            if (expired) return UiKit.Chip("Expirado", UiTokens.Danger, solid: true);
            if (Profile.IsEquipped(Item.Id)) return UiKit.Chip("Equipado", UiTokens.TextPrimary, solid: false);
            return null;
        }

        if (Profile.Owns(Item.Id) && !Item.Consumable)
        {
            ItemInfo.Duration(Item, out _);
            return UiKit.Chip(Profile.Find(Item.Id)?.IsPermanent == true || Item.Starter ? "Possui" : "Possui · renovar",
                UiTokens.Success);
        }

        if (Catalog.IsDaily(Item, DateTime.Now)) return UiKit.Chip("−20% hoje", UiTokens.Warning, solid: true);
        if (Item.Hot) return UiKit.Chip("Destaque", UiTokens.Accent);
        return null;
    }

    /// <summary>Preço no formato "duração : valor moeda".</summary>
    private Control PriceLine()
    {
        HBoxContainer row = UiKit.Row(UiTokens.S1);
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.Alignment = BoxContainer.AlignmentMode.End;

        if (!Item.ForSale)
        {
            row.AddChild(UiKit.Text(Item.Starter ? "Incluído" : Item.Kind == ItemKind.Breed ? "Por patente" : "—", "Caption"));
            return row;
        }

        Duration first = Duration.Day1;
        foreach (Duration d in Enum.GetValues<Duration>())
            if (Catalog.Price(Item, d) >= 0) { first = d; break; }

        row.AddChild(UiKit.Text($"{Catalog.DurationShort[(int)first]} :", "Caption"));
        Label price = UiKit.Text(UiTokens.Thousands(Catalog.Price(Item, first)), "Numeric");
        row.AddChild(price);
        row.AddChild(Icon.Make(IconKind.Presa, 12, CurrencyLabel.ColorOf(Currency.Presas)));
        return row;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
            || @event.IsActionPressed("ui_accept"))
        {
            AcceptEvent();
            Sfx.Ui("ui_clique");
            _clicked?.Invoke(Item);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseEnter && !_selected) Modulate = new Color(1.08f, 1.08f, 1.08f);
        if (what == NotificationMouseExit) Modulate = Colors.White;
    }
}
