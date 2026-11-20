using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A loja, em três colunas que se leem da esquerda para a direita
/// (pedido do Jhon, 17/11: "uma loja mais fácil de entender"):
///
/// 1. **Categorias** numa lista só, à esquerda, agrupadas — Destaques,
///    Humano (primárias, secundárias, granadas), Lobo (melhorias,
///    classes) e Conta —, cada uma com quantos itens tem. Antes eram
///    abas em dois níveis mais um personagem em 3D que não tinha a ver
///    com a compra.
/// 2. **Os itens** da categoria, em cartões com a foto da arma.
/// 3. **O item escolhido**: atributos, o seletor de duração
///    (1d · 7d · 30d · 90d · Perm), preço ao vivo, preço por dia, o selo
///    "melhor valor" e o Comprar.
///
/// Comprar abre uma confirmação com o saldo antes e depois — loja que
/// esconde o saldo depois da compra é desenhada para arrepender. E o
/// Confirmar fica num lugar diferente de onde estava o Comprar, para
/// ninguém comprar por inércia.
///
/// Tudo aqui custa Presas, a moeda ganha jogando. Sangue não compra nada
/// que afete o combate (e, sem servidor, não compra nada).
/// </summary>
public partial class ShopScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Shop;

	private static int _category;

	private string _selectedId;
	private Duration _duration = Duration.Day7;

	/// <summary>Uma categoria da lista: o grupo em que aparece, o nome e
	/// o que entra nela.</summary>
	private readonly record struct Category(string Group, string Name, Func<IEnumerable<CatalogItem>> Items);

	/// <summary>O que aparece na loja: o que não vem com a conta e que o
	/// servidor não desligou.</summary>
	private static IEnumerable<CatalogItem> ForSale => Catalog.All.Where(i => (!i.Starter || i.Kind == ItemKind.Breed) && ServerData.ItemOn(i.Id));

	protected override bool FollowsAccount => true;

	private static readonly Category[] Categories =
	{
		new("Destaques", "Em destaque", () => ForSale.Where(i => i.Hot)),
		new("Destaques", "Oferta do dia", () => Catalog.Daily()),
		new("Humano", "Primárias", () => ForSale.Where(i => ItemInfo.IsPrimary(i) && i.Kind == ItemKind.Weapon)),
		new("Humano", "Secundárias", () => ForSale.Where(i => ItemInfo.IsSecondary(i) && i.Kind == ItemKind.Weapon)),
		new("Humano", "Facas", () => ForSale.Where(i => Arsenal.IsKnife(i.Id))),
		new("Humano", "Granadas", () => ForSale.Where(i => i.Kind == ItemKind.Utility)),
		new("Lobo", "Melhorias", () => ForSale.Where(i => i.Kind == ItemKind.Tattoo)),
		new("Lobo", "Classes de lobo", () => ForSale.Where(i => i.Kind == ItemKind.Breed)),
		new("Conta", "Serviços", () => ForSale.Where(i => i.Kind == ItemKind.Service))
	};

	private const int Daily = 1;

	public ShopScreen() { }

	public ShopScreen(string focus)
	{
		CatalogItem item = Catalog.Get(focus);
		if (item == null) return;

		_selectedId = item.Id;
		_category = item.Kind switch
		{
			ItemKind.Tattoo => 6,
			ItemKind.Breed => 7,
			ItemKind.Service => 8,
			ItemKind.Utility => 5,
			_ => Arsenal.IsKnife(item.Id) ? 4 : ItemInfo.IsSecondary(item) ? 3 : 2
		};
		_duration = DefaultDuration(item);
	}

	protected override void Build()
	{
		VBoxContainer page = Page(UiTokens.S3);

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(UiKit.Text("Loja", "Title"));
		Label about = UiKit.Text("Tudo aqui custa Presas, a moeda que você ganha jogando.", "Muted");
		about.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		head.AddChild(about);
		head.AddChild(UiKit.Spacer());
		Button sangue = UiKit.Button("Comprar Sangue", ButtonKind.Tertiary);
		sangue.Disabled = true;
		sangue.TooltipText = "A venda de Sangue ainda não abriu. E ele nunca compra poder.";
		head.AddChild(sangue);
		page.AddChild(head);

		HBoxContainer split = UiKit.Row(UiTokens.S4);
		split.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(split);

		split.AddChild(Sidebar());
		split.AddChild(Catalogue());
		split.AddChild(Detail());
	}

	// ------------------------------------------------------------------ categorias

	private Control Sidebar()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S3, UiTokens.S1);
		panel.CustomMinimumSize = new Vector2(280, 0);

		string group = null;
		for (int i = 0; i < Categories.Length; i++)
		{
			Category category = Categories[i];
			if (category.Group != group)
			{
				if (group != null) column.AddChild(UiKit.Gap(UiTokens.S2));
				group = category.Group;
				column.AddChild(UiKit.Caption(group));
			}

			int index = i;
			column.AddChild(NavItem(category.Name, category.Items().Count(), i == _category, () =>
			{
				_category = index;
				Refresh();
			}));
		}

		column.AddChild(UiKit.Spacer());

		// O saldo, onde o olho volta antes de comprar.
		column.AddChild(UiKit.Divider());
		HBoxContainer wallet = UiKit.Row(UiTokens.S2);
		wallet.AddChild(UiKit.Caption("Você tem"));
		wallet.AddChild(UiKit.Spacer());
		wallet.AddChild(UiKit.Text(UiTokens.Thousands(Profile.Presas), "NumericLarge", CurrencyLabel.ColorOf(Currency.Presas)));
		wallet.AddChild(Icon.Make(IconKind.Presa, 20, CurrencyLabel.ColorOf(Currency.Presas)));
		column.AddChild(wallet);
		return panel;
	}

	/// <summary>Uma linha da lista de categorias: nome à esquerda, a
	/// contagem à direita; a ativa leva a barra da cor da marca.</summary>
	public static Button NavItem(string name, int count, bool active, Action pressed)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(0, 42),
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			ThemeTypeVariation = "TertiaryButton"
		};
		if (active)
		{
			StyleBoxFlat on = UiTheme.Box(new Color(UiTokens.Accent, 0.12f), UiTokens.Accent, 0, UiTokens.RadiusSmall, UiTokens.S3, UiTokens.S1);
			on.BorderWidthLeft = 3;
			foreach (string state in new[] { "normal", "hover", "pressed" })
				button.AddThemeStyleboxOverride(state, on);
		}
		button.Pressed += pressed;
		Sfx.Wire(button);

		HBoxContainer row = UiKit.Fill(UiKit.Row(UiTokens.S2));
		row.OffsetLeft = UiTokens.S3;
		row.OffsetRight = -UiTokens.S3;
		row.MouseFilter = MouseFilterEnum.Ignore;
		Label label = UiKit.Text(name, null, active ? UiTokens.TextPrimary : UiTokens.TextSecondary);
		label.AddThemeFontOverride("font", active ? UiFonts.Bold : UiFonts.Medium);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(label);
		row.AddChild(UiKit.Text(count.ToString(), "Numeric", active ? UiTokens.Accent : UiTokens.TextDisabled));
		button.AddChild(row);
		return button;
	}

	// ------------------------------------------------------------------ itens

	private Control Catalogue()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		Category category = Categories[Mathf.Clamp(_category, 0, Categories.Length - 1)];
		List<CatalogItem> items = category.Items().ToList();

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(UiKit.Text(category.Name, "Subtitle"));
		Label count = UiKit.Text(items.Count == 1 ? "1 item" : Loc.F("{0} itens", items.Count), "Muted");
		count.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		head.AddChild(count);
		head.AddChild(UiKit.Spacer());
		if (_category == Daily)
		{
			TimeSpan left = (ServerData.DailyEndsUtc ?? DateTime.UtcNow) - DateTime.UtcNow;
			head.AddChild(UiKit.Text(items.Count == 0 ? "A vitrine do dia vem do servidor: entre em Online"
				: Loc.F("Troca em {0} h {1:00} min", (int)left.TotalHours, left.Minutes), "Caption", UiTokens.Warning));
		}
		column.AddChild(head);

		GridContainer grid = UiKit.ScrollGrid(column, 3);
		foreach (CatalogItem item in items)
			grid.AddChild(ItemCard.Make(item, CardMode.Shop, item.Id == _selectedId, Select));

		if (items.Count == 0)
			column.AddChild(UiKit.Paragraph("Nada à venda aqui agora."));

		return column;
	}

	private void Select(CatalogItem item)
	{
		if (_selectedId != item.Id) _duration = DefaultDuration(item);
		_selectedId = item.Id;
		Refresh();
	}

	/// <summary>7 dias quando existe; senão a primeira duração à venda.</summary>
	private static Duration DefaultDuration(CatalogItem item)
	{
		if (Catalog.Price(item, Duration.Day7) >= 0) return Duration.Day7;
		foreach (Duration d in Enum.GetValues<Duration>())
			if (Catalog.Price(item, d) >= 0) return d;
		return Duration.Permanent;
	}

	// ------------------------------------------------------------------ detalhe e compra

	private Control Detail()
	{
		CatalogItem item = Catalog.Get(_selectedId);
		if (item == null)
			return ItemDetail.Empty("Escolha a categoria à esquerda e clique num item: aqui aparecem os atributos, por quanto tempo comprar e o preço.");

		CatalogItem compare = null;
		LoadoutSet set = Profile.Sets[Mathf.Clamp(Profile.LastUsed, 0, Profile.Sets.Count - 1)];
		if (ItemInfo.IsPrimary(item)) compare = Catalog.Get(set.Primary);
		else if (ItemInfo.IsSecondary(item)) compare = Catalog.Get(set.Secondary);
		else if (item.Kind == ItemKind.Breed) compare = Catalog.Get(Profile.Breed);

		return ItemDetail.Make(item, compare, Purchase(item));
	}

	private Control Purchase(CatalogItem item)
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);

		if (!item.ForSale)
		{
			string why = item.Kind == ItemKind.Breed
				? ItemInfo.IsBreedUnlocked(item) ? "Já é sua. Escolha no inventário, aba Lobo." : Loc.F("Destrava na patente {0} — não está à venda.", Ranks.NameOf(item.MinRank))
				: "Vem com todo perfil.";
			column.AddChild(UiKit.Paragraph(why));
			if (item.Kind == ItemKind.Breed && ItemInfo.IsBreedUnlocked(item))
				column.AddChild(UiKit.Button("Abrir no inventário", ButtonKind.Secondary, () => Shell.GoInventory(item.Id)));
			return column;
		}

		// Seletor de duração: uma linha de botões, preço ao vivo embaixo.
		column.AddChild(UiKit.Caption("Duração"));
		HBoxContainer durations = UiKit.Row(UiTokens.S1);
		var group = new ButtonGroup();
		Duration? best = Catalog.BestValue(item);

		foreach (Duration d in Enum.GetValues<Duration>())
		{
			int cost = Catalog.Price(item, d);
			Duration value = d;
			Button button = UiKit.Button(Catalog.DurationShort[(int)d], ButtonKind.Secondary, () => { _duration = value; Refresh(); });
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = d == _duration;
			button.Disabled = cost < 0;
			button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			button.CustomMinimumSize = new Vector2(0, 36);
			if (d == _duration)
			{
				button.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(UiTokens.Accent, 0.15f), UiTokens.Accent, 2));
				button.AddThemeStyleboxOverride("pressed", UiTheme.Box(new Color(UiTokens.Accent, 0.15f), UiTokens.Accent, 2));
				button.AddThemeStyleboxOverride("hover_pressed", UiTheme.Box(new Color(UiTokens.Accent, 0.2f), UiTokens.Accent, 2));
			}
			if (best == d) button.TooltipText = "Melhor valor: menor preço por dia";
			durations.AddChild(button);
		}
		column.AddChild(durations);

		int selectedPrice = Catalog.Price(item, _duration);
		if (selectedPrice < 0)
		{
			_duration = DefaultDuration(item);
			selectedPrice = Catalog.Price(item, _duration);
		}

		HBoxContainer priceRow = UiKit.Row(UiTokens.S2);
		Label price = UiKit.Text(UiTokens.Thousands(selectedPrice), "NumericLarge");
		priceRow.AddChild(price);
		priceRow.AddChild(Icon.Make(IconKind.Presa, 20, CurrencyLabel.ColorOf(Currency.Presas)));
		priceRow.AddChild(UiKit.Text(Catalog.DurationLabels[(int)_duration], "Muted"));
		priceRow.AddChild(UiKit.Spacer());
		if (best == _duration) priceRow.AddChild(UiKit.Chip("Melhor valor", UiTokens.Success, solid: true));
		column.AddChild(priceRow);

		float perDay = Catalog.PerDay(item, _duration);
		column.AddChild(UiKit.Text(perDay > 0 ? Loc.F("{0:0} Presas por dia", perDay) : item.Consumable ? "Um uso" : "Para sempre", "Caption"));

		bool locked = ItemInfo.Locked(item);
		bool affordable = Profile.Presas >= selectedPrice;
		bool ownedForever = Profile.Find(item.Id)?.IsPermanent == true && !item.Consumable;

		HBoxContainer buy = UiKit.Row(UiTokens.S2);
		buy.AddChild(UiKit.Spacer());
		// Comprar é com a conta: o servidor confere preço, patente e saldo.
		if (!Profile.HasAccount)
		{
			Button enter = UiKit.Button(AccountGate.Loading ? "Carregando a conta…" : "Entrar na conta para comprar", ButtonKind.Primary, () => Shell.GoOnline());
			enter.CustomMinimumSize = new Vector2(260, UiTokens.ButtonHeight);
			enter.Disabled = AccountGate.Loading;
			buy.AddChild(enter);
			column.AddChild(buy);
			return column;
		}

		Button purchase = UiKit.Button(ownedForever ? "Já é permanente" : Profile.Owns(item.Id) && !item.Consumable ? "Comprar mais tempo" : "Comprar",
			ButtonKind.Primary, () => Confirm(item, _duration, selectedPrice));
		purchase.CustomMinimumSize = new Vector2(200, UiTokens.ButtonHeight);
		purchase.Disabled = locked || ownedForever || _buying;
		purchase.TooltipText = locked ? Loc.F("Exige {0}", Ranks.NameOf(item.MinRank)) : !affordable ? "Presas insuficientes" : "";
		buy.AddChild(purchase);
		column.AddChild(buy);

		if (!affordable && !locked)
			column.AddChild(UiKit.Text(Loc.F("Faltam {0} Presas", UiTokens.Thousands(selectedPrice - Profile.Presas)), "Caption", UiTokens.Danger));

		return column;
	}

	private void Confirm(CatalogItem item, Duration duration, int price)
	{
		long before = Profile.Presas;
		long after = before - price;
		bool enough = after >= 0;

		VBoxContainer body = UiKit.Column(UiTokens.S2);
		body.CustomMinimumSize = new Vector2(420, 0);

		HBoxContainer what = UiKit.Row(UiTokens.S3);
		what.AddChild(WeaponArt.Make(item.Id, new Vector2(120, 52)));
		VBoxContainer names = UiKit.Column(0);
		names.AddChild(UiKit.Text(item.Name, "Subtitle"));
		names.AddChild(UiKit.Text(Catalog.DurationLabels[(int)duration], "Muted"));
		what.AddChild(names);
		body.AddChild(what);
		body.AddChild(UiKit.Divider());

		body.AddChild(Line("Preço", price, UiTokens.TextPrimary));
		body.AddChild(Line("Saldo atual", before, UiTokens.TextSecondary));
		body.AddChild(Line("Saldo depois", after, enough ? UiTokens.TextPrimary : UiTokens.Danger));

		if (!enough)
			body.AddChild(UiKit.Text("Presas insuficientes. Jogue partidas e resgate missões para ganhar mais.", "Caption", UiTokens.Danger));

		// Confirmar à esquerda — longe de onde estava o Comprar.
		Shell.Modal("Confirmar compra", body,
			new Modal.Choice("Confirmar", ButtonKind.Primary, () => Buy(item, duration), !enough),
			new Modal.Choice("Cancelar", ButtonKind.Secondary, null));
	}

	private static Control Line(string label, long value, Color color)
	{
		HBoxContainer row = UiKit.Stat(label, UiTokens.Thousands(value), color);
		row.AddChild(Icon.Make(IconKind.Presa, 14, CurrencyLabel.ColorOf(Currency.Presas)));
		return row;
	}

	/// <summary>Uma compra de cada vez: o botão fica parado até o servidor responder.</summary>
	private bool _buying;

	private async void Buy(CatalogItem item, Duration duration)
	{
		if (_buying) return;
		_buying = true;
		Refresh();
		string error = await AccountSync.Buy(item.Id, duration);
		_buying = false;
		if (!IsInsideTree()) return;

		if (error != null)
		{
			Shell.Toast(error, UiTokens.Danger);
			Sfx.Ui("ui_negado");
			Refresh();
			return;
		}

		Sfx.Ui(Sfx.First("ui_compra", "abate"));
		Shell.Toast(Loc.F("Comprado: {0} · {1}", item.Name, Catalog.DurationLabels[(int)duration]), UiTokens.Success);
		Refresh();
	}
}
