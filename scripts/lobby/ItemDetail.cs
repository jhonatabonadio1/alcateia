using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Painel de detalhe do item selecionado: nome, raridade, arte, duração,
/// e duas abas — DESCRIÇÃO e ATRIBUTOS.
///
/// Os atributos vêm em barras que mostram a diferença para o que está
/// equipado no mesmo lugar (verde se melhora, vermelho se piora). É a
/// única forma de o jogador decidir sem abrir planilha.
///
/// Embaixo, as ações de quem chamou: no inventário Equipar, Renovar e
/// Descartar; na loja, o seletor de duração e Comprar.
/// </summary>
public partial class ItemDetail : PanelContainer
{
	private CatalogItem _item;
	private CatalogItem _compare;
	private VBoxContainer _tabBody;

	public static ItemDetail Make(CatalogItem item, CatalogItem compareTo, Control actions)
	{
		var detail = new ItemDetail { _item = item, _compare = compareTo };
		detail.Build(actions);
		return detail;
	}

	/// <summary>Painel vazio, antes de qualquer seleção.</summary>
	public static PanelContainer Empty(string text)
	{
		PanelContainer panel = UiKit.Panel("Surface1");
		panel.CustomMinimumSize = new Vector2(400, 0);
		var center = new CenterContainer();
		panel.AddChild(center);
		Label label = UiKit.Paragraph(text);
		label.CustomMinimumSize = new Vector2(280, 0);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		center.AddChild(label);
		return panel;
	}

	private void Build(Control actions)
	{
		ThemeTypeVariation = "Surface1";
		CustomMinimumSize = new Vector2(400, 0);

		MarginContainer margin = UiKit.Margin(UiTokens.S4, UiTokens.S3, UiTokens.S4, UiTokens.S4);
		AddChild(margin);
		VBoxContainer column = UiKit.Column(UiTokens.S3);
		margin.AddChild(column);

		HBoxContainer head = UiKit.Row(UiTokens.S2);
		Label name = UiKit.Truncated(_item.Name, "Title");
		head.AddChild(name);
		head.AddChild(UiKit.Chip(ItemInfo.RarityName(_item), ItemInfo.RarityColor(_item)));
		column.AddChild(head);

		HBoxContainer sub = UiKit.Row(UiTokens.S2);
		sub.AddChild(UiKit.Text(_item.Type, "Muted"));
		sub.AddChild(UiKit.Text("·", "Muted"));
		sub.AddChild(UiKit.Text(ItemInfo.SlotName(_item), "Muted"));
		sub.AddChild(UiKit.Spacer());
		string wp = ItemInfo.WpCost(_item);
		if (wp != "") sub.AddChild(UiKit.Text(wp, "Numeric", UiTokens.Accent));
		column.AddChild(sub);

		var artFrame = new PanelContainer { CustomMinimumSize = new Vector2(0, 150) };
		artFrame.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(ItemInfo.RarityColor(_item), 0.07f),
			new Color(ItemInfo.RarityColor(_item), 0.35f), 1, UiTokens.RadiusPanel, UiTokens.S3, UiTokens.S3));
		artFrame.AddChild(WeaponArt.Make(_item.Id, new Vector2(0, 120), dim: ItemInfo.Locked(_item) && !ItemInfo.Usable(_item)));
		column.AddChild(artFrame);

		string duration = ItemInfo.Duration(_item, out Color durationColor);
		if (duration != "")
		{
			HBoxContainer state = UiKit.Row(UiTokens.S2);
			state.AddChild(Icon.Make(IconKind.Clock, 14, durationColor));
			state.AddChild(UiKit.Text(duration, null, durationColor));
			column.AddChild(state);
		}

		if (ItemInfo.Locked(_item) && !ItemInfo.Usable(_item))
		{
			HBoxContainer lockLine = UiKit.Row(UiTokens.S2);
			lockLine.AddChild(Icon.Make(IconKind.Lock, 14, UiTokens.Warning));
			lockLine.AddChild(UiKit.Text($"Exige a patente {Ranks.NameOf(_item.MinRank)}", null, UiTokens.Warning));
			column.AddChild(lockLine);
		}

		Tabs tabs = Tabs.Make(new[] { "Descrição", "Atributos" }, 1, ShowTab);
		column.AddChild(tabs);

		_tabBody = UiKit.Column(UiTokens.S2);
		_tabBody.SizeFlagsVertical = SizeFlags.ExpandFill;
		column.AddChild(_tabBody);
		ShowTab(1);

		if (actions != null) column.AddChild(actions);
	}

	private void ShowTab(int index)
	{
		UiKit.Clear(_tabBody);
		if (index == 0)
		{
			_tabBody.AddChild(UiKit.Paragraph(_item.Description));
			return;
		}

		foreach (Control bar in Attributes(_item, _compare))
			_tabBody.AddChild(bar);
	}

	// ------------------------------------------------------------------ atributos

	/// <summary>As barras de atributo do item, comparadas com outro do
	/// mesmo tipo (ou sem comparação, se nulo).</summary>
	public static List<Control> Attributes(CatalogItem item, CatalogItem compare)
	{
		var bars = new List<Control>();

		WeaponDefinition def = item.Weapon;
		WeaponDefinition other = compare?.Weapon;

		if (def != null)
		{
			bool cmp = other != null && other != def;
			bars.Add(StatBar.Stars("Dano", def.StarDamage, cmp ? other.StarDamage : -1));
			bars.Add(StatBar.Stars("Precisão", def.StarAccuracy, cmp ? other.StarAccuracy : -1));
			bars.Add(StatBar.Stars("Cadência", def.StarRate, cmp ? other.StarRate : -1));
			bars.Add(StatBar.Stars("Alcance", def.StarRange, cmp ? other.StarRange : -1));
			bars.Add(StatBar.Stars("Mobilidade", def.StarMobility, cmp ? other.StarMobility : -1));
			bars.Add(StatBar.Stars("Controle", def.StarRecoil, cmp ? other.StarRecoil : -1));

			GridContainer facts = new() { Columns = 2 };
			facts.AddThemeConstantOverride("h_separation", UiTokens.S4);
			facts.AddThemeConstantOverride("v_separation", 2);
			Fact(facts, "Pente", def.Magazine.ToString());
			Fact(facts, "Cadência", $"{def.Rpm:0} por min");
			Fact(facts, "Recarga", $"{def.ReloadTactical:0.0} s");
			Fact(facts, "Velocidade", $"{def.SpeedMultiplier * 100:0}%");
			bars.Add(facts);

			if (cmp)
			{
				Label note = UiKit.Text($"Comparando com {compare.Name}, equipada no mesmo lugar", "Caption");
				note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				bars.Add(note);
			}

			return bars;
		}

		if (item.Kind == ItemKind.Breed)
		{
			var breed = GD.Load<WolfBreed>(item.BreedPath);
			var cmpBreed = compare?.BreedPath != null && compare != item ? GD.Load<WolfBreed>(compare.BreedPath) : null;
			if (breed == null) return bars;

			bars.Add(StatBar.Make("Escudo", breed.Shield / 300f, $"{breed.Shield:0}", cmpBreed == null ? -1 : cmpBreed.Shield / 300f));
			bars.Add(StatBar.Make("Velocidade", breed.SpeedMultiplier / 2f, $"×{breed.SpeedMultiplier:0.00}", cmpBreed == null ? -1 : cmpBreed.SpeedMultiplier / 2f));
			bars.Add(StatBar.Make("Pulo", breed.JumpMultiplier / 2f, $"×{breed.JumpMultiplier:0.00}", cmpBreed == null ? -1 : cmpBreed.JumpMultiplier / 2f));
			bars.Add(StatBar.Make("Garra", breed.AttackDamage / 160f, $"{breed.AttackDamage:0}", cmpBreed == null ? -1 : cmpBreed.AttackDamage / 160f));
			bars.Add(StatBar.Make("Resistência", (1f - breed.BodyResistance) / 0.5f, $"{(1f - breed.BodyResistance) * 100:0}%",
				cmpBreed == null ? -1 : (1f - cmpBreed.BodyResistance) / 0.5f));
			return bars;
		}

		if (item.Kind == ItemKind.Tattoo && item.Tattoo != null)
		{
			TattooEffect fx = item.Tattoo;
			void Effect(string label, float mult, bool lowerIsBetter = false)
			{
				if (Mathf.IsEqualApprox(mult, 1f)) return;
				float pct = (mult - 1f) * 100f;
				bool good = lowerIsBetter ? pct < 0 : pct > 0;
				HBoxContainer row = UiKit.Stat(label, $"{(pct > 0 ? "+" : "")}{pct:0}%", good ? UiTokens.Success : UiTokens.Danger);
				bars.Add(row);
			}

			Effect("Velocidade do lobo", fx.Speed);
			Effect("Pulo do lobo", fx.Jump);
			Effect("Escudo máximo", fx.Shield);
			Effect("Dano da garra", fx.ClawDamage);
			Effect("Decaimento do escudo", fx.ShieldDecay, lowerIsBetter: true);
			bars.Add(UiKit.Paragraph($"Até {Profile.MaxTattoos} tatuagens equipadas ao mesmo tempo. Vale só para você."));
			return bars;
		}

		bars.Add(UiKit.Paragraph(item.Description));
		return bars;
	}

	private static void Fact(GridContainer grid, string label, string value)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(UiKit.Text(label, "Caption"));
		row.AddChild(UiKit.Spacer());
		row.AddChild(UiKit.Text(value, "Numeric"));
		grid.AddChild(row);
	}
}
