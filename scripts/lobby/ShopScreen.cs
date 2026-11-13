using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A loja: o inventário invertido. Mesma grade, mesmo cartão, com preço e
/// seletor de duração no lugar da duração restante — custo cognitivo zero
/// para quem já conhece o inventário.
///
/// À esquerda, o personagem em 3D (humano ou lobo) e um painel com os
/// atributos do que você usa hoje. No meio, as abas (DESTAQUE e DO DIA
/// são as comerciais; o resto espelha as categorias do inventário) e a
/// grade de três colunas. À direita, o detalhe do item com o seletor de
/// duração: 1d · 7d · 30d · 90d · Perm, preço ao vivo, preço por dia e o
/// selo "melhor valor".
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

	private static int _top;
	private static int _sub;
	private static bool _wolfView;

	private string _selectedId;
	private Duration _duration = Duration.Day7;

	private static readonly string[] TopTabs = { "Destaque", "Do dia", "Armas", "Lobo", "Especial" };
	private static readonly string[][] SubTabs =
	{
		new[] { "Todos" },
		new[] { "Hoje" },
		new[] { "Todas", "Primária", "Secundária", "Granada" },
		new[] { "Tatuagens", "Classes" },
		new[] { "Todos" }
	};

	public ShopScreen() { }

	public ShopScreen(string focus)
	{
		CatalogItem item = Catalog.Get(focus);
		if (item == null) return;

		_selectedId = item.Id;
		_top = item.Kind switch
		{
			ItemKind.Tattoo or ItemKind.Breed => 3,
			ItemKind.Service => 4,
			_ => 2
		};
		_sub = 0;
		_duration = DefaultDuration(item);
	}

	protected override void Build()
	{
		MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4));
		AddChild(margin);
		HBoxContainer split = UiKit.Row(UiTokens.S4);
		margin.AddChild(split);

		split.AddChild(Showcase());
		split.AddChild(Catalogue());
		split.AddChild(Detail());
	}

	// ------------------------------------------------------------------ vitrine

	private Control Showcase()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S3);
		column.CustomMinimumSize = new Vector2(470, 0);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(UiKit.Text("Loja", "Title"));
		head.AddChild(UiKit.Spacer());
		head.AddChild(Tabs.Make(new[] { "Humano", "Lobo" }, _wolfView ? 1 : 0, i => { _wolfView = i == 1; Refresh(); }));
		column.AddChild(head);

		var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		frame.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTokens.Surface1, UiTokens.Border, 1, UiTokens.RadiusPanel, 0, 0));
		var preview = new CharacterPreview { Wolf = _wolfView, SizeFlagsVertical = SizeFlags.ExpandFill };
		frame.AddChild(preview);
		column.AddChild(frame);

		column.AddChild(StatsPanel());
		return column;
	}

	/// <summary>O que você usa hoje, em barras: a arma principal do
	/// último conjunto (humano) ou a classe com as tatuagens (lobo).</summary>
	private Control StatsPanel()
	{
		PanelContainer panel = UiKit.Panel("Surface2");
		VBoxContainer column = UiKit.Column(UiTokens.S1);
		panel.AddChild(column);

		if (_wolfView)
		{
			WolfBreed breed = Profile.BuildBreed();
			CatalogItem item = Catalog.Get(Profile.Breed);
			column.AddChild(UiKit.Caption($"Seu lobo · {item?.Name}" + (Profile.Tattoos.Count > 0 ? $" + {Profile.Tattoos.Count} tatuagem(ns)" : "")));
			if (breed != null)
			{
				column.AddChild(StatBar.Make("Escudo", breed.Shield / 300f, $"{breed.Shield:0}"));
				column.AddChild(StatBar.Make("Velocidade", breed.SpeedMultiplier / 2f, $"×{breed.SpeedMultiplier:0.00}"));
				column.AddChild(StatBar.Make("Pulo", breed.JumpMultiplier / 2f, $"×{breed.JumpMultiplier:0.00}"));
				column.AddChild(StatBar.Make("Garra", breed.AttackDamage / 160f, $"{breed.AttackDamage:0}"));
			}
			return panel;
		}

		LoadoutSet set = Profile.Sets[Mathf.Clamp(Profile.LastUsed, 0, Profile.Sets.Count - 1)];
		CatalogItem primary = Catalog.Get(set.Primary);
		column.AddChild(UiKit.Caption($"Conjunto em uso · {set.Name} · {primary?.Name}"));
		WeaponDefinition def = primary?.Weapon;
		if (def != null)
		{
			column.AddChild(StatBar.Stars("Dano", def.StarDamage));
			column.AddChild(StatBar.Stars("Precisão", def.StarAccuracy));
			column.AddChild(StatBar.Stars("Cadência", def.StarRate));
			column.AddChild(StatBar.Stars("Mobilidade", def.StarMobility));
		}
		return panel;
	}

	// ------------------------------------------------------------------ catálogo

	private Control Catalogue()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(UiKit.Text("Presas compram tudo que afeta o combate", "Muted"));
		head.AddChild(UiKit.Spacer());
		Button sangue = UiKit.Button("Comprar Sangue", ButtonKind.Tertiary);
		sangue.Disabled = true;
		sangue.TooltipText = "A moeda paga só existe com servidor. E ela nunca compra poder.";
		head.AddChild(sangue);
		column.AddChild(head);

		column.AddChild(Tabs.Make(TopTabs, _top, i => { _top = i; _sub = 0; Refresh(); }));

		HBoxContainer subRow = UiKit.Row(UiTokens.S3);
		// Segundo nível só quando há o que escolher.
		if (SubTabs[_top].Length > 1)
			subRow.AddChild(Tabs.Make(SubTabs[_top], Mathf.Min(_sub, SubTabs[_top].Length - 1), i => { _sub = i; Refresh(); }));
		subRow.AddChild(UiKit.Spacer());
		if (_top == 1)
		{
			TimeSpan left = MissionBook.UntilDailyReset(DateTime.Now);
			subRow.AddChild(UiKit.Text($"Troca em {left.Hours} h {left.Minutes:00} min  ·  20% de desconto", "Caption", UiTokens.Warning));
		}
		column.AddChild(subRow);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		column.AddChild(scroll);

		var grid = new GridContainer { Columns = 3 };
		grid.AddThemeConstantOverride("h_separation", UiTokens.S3);
		grid.AddThemeConstantOverride("v_separation", UiTokens.S3);
		MarginContainer pad = UiKit.Margin(UiTokens.S2);
		pad.AddChild(grid);
		scroll.AddChild(pad);

		foreach (CatalogItem item in Items())
			grid.AddChild(ItemCard.Make(item, CardMode.Shop, item.Id == _selectedId, Select));

		return column;
	}

	private IEnumerable<CatalogItem> Items()
	{
		IEnumerable<CatalogItem> all = Catalog.All.Where(i => !i.Starter || i.Kind == ItemKind.Breed);

		return (_top, _sub) switch
		{
			(0, _) => all.Where(i => i.Hot),
			(1, _) => Catalog.Daily(DateTime.Now),
			(2, 1) => all.Where(i => ItemInfo.IsPrimary(i) && i.Kind == ItemKind.Weapon),
			(2, 2) => all.Where(i => ItemInfo.IsSecondary(i) && i.Kind == ItemKind.Weapon),
			(2, 3) => all.Where(i => i.Kind == ItemKind.Utility),
			(2, _) => all.Where(i => i.Kind is ItemKind.Weapon or ItemKind.Utility),
			(3, 0) => all.Where(i => i.Kind == ItemKind.Tattoo),
			(3, _) => all.Where(i => i.Kind == ItemKind.Breed),
			_ => all.Where(i => i.Kind == ItemKind.Service)
		};
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
			return ItemDetail.Empty("Escolha um item para ver os atributos, as durações e o preço.");

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
				? ItemInfo.IsBreedUnlocked(item) ? "Já é sua. Escolha no inventário, aba Lobo." : $"Destrava na patente {Ranks.NameOf(item.MinRank)} — não está à venda."
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
		column.AddChild(UiKit.Text(perDay > 0 ? $"{perDay:0} Presas por dia" : item.Consumable ? "Um uso" : "Para sempre", "Caption"));

		bool locked = ItemInfo.Locked(item);
		bool affordable = Profile.Presas >= selectedPrice;
		bool ownedForever = Profile.Find(item.Id)?.IsPermanent == true && !item.Consumable;

		HBoxContainer buy = UiKit.Row(UiTokens.S2);
		buy.AddChild(UiKit.Spacer());
		Button purchase = UiKit.Button(ownedForever ? "Já é permanente" : Profile.Owns(item.Id) && !item.Consumable ? "Comprar mais tempo" : "Comprar",
			ButtonKind.Primary, () => Confirm(item, _duration, selectedPrice));
		purchase.CustomMinimumSize = new Vector2(200, UiTokens.ButtonHeight);
		purchase.Disabled = locked || ownedForever;
		purchase.TooltipText = locked ? $"Exige {Ranks.NameOf(item.MinRank)}" : !affordable ? "Presas insuficientes" : "";
		buy.AddChild(purchase);
		column.AddChild(buy);

		if (!affordable && !locked)
			column.AddChild(UiKit.Text($"Faltam {UiTokens.Thousands(selectedPrice - Profile.Presas)} Presas", "Caption", UiTokens.Danger));

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
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.AddChild(UiKit.Text(label, "Muted"));
		row.AddChild(UiKit.Spacer());
		row.AddChild(UiKit.Text(UiTokens.Thousands(value), "Numeric", color));
		row.AddChild(Icon.Make(IconKind.Presa, 14, CurrencyLabel.ColorOf(Currency.Presas)));
		return row;
	}

	private void Buy(CatalogItem item, Duration duration)
	{
		if (!Profile.Buy(item.Id, duration, out string error))
		{
			Shell.Toast(error, UiTokens.Danger);
			Sfx.Ui("ui_negado");
			return;
		}

		Sfx.Ui("abate");
		Shell.Toast($"Comprado: {item.Name} · {Catalog.DurationLabels[(int)duration]}", UiTokens.Success);
		Refresh();
	}
}
