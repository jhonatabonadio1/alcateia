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
///
/// **O painel cabe sempre na altura da tela** (Jhon, 26/11: "a interface
/// tem que evitar scroll ao máximo; as coisas precisam ser distribuídas
/// melhor" — numa janela baixa o Comprar ficava para fora da tela, sem
/// como chegar nele). O cabeçalho e as ações têm lugar fixo; o que cede é
/// o meio: a vitrine encolhe (de <see cref="ArtMax"/> até
/// <see cref="ArtMin"/>) e, só se ainda assim não couber, os atributos
/// rolam dentro da área deles. Os atributos vêm em duas colunas e o preço
/// fica na linha do botão, para isso quase nunca acontecer.
/// </summary>
public partial class ItemDetail : PanelContainer
{
	private CatalogItem _item;
	private CatalogItem _compare;
	private VBoxContainer _tabBody;
	private VBoxContainer _column;
	private ScrollContainer _scroll;
	private Control _art;
	private float _artMax;

	/// <summary>A altura da vitrine: a de sempre (com espaço) e a menor que ela aceita (arma; o personagem em 3D
	/// pede mais).</summary>
	public const float ArtMax = 190f, ArtMin = 64f, CharacterArtMax = 300f, CharacterArtMin = 150f;

	/// <summary>A menor altura da área dos atributos, quando rola.</summary>
	public const float BodyMin = 52f;

	/// <summary>A folga entre o pé do painel e o pé da tela.</summary>
	public const float BottomGap = 16f;

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
		_column = column;

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

		bool character = _item.Character != null;
		// A vitrine começa na menor altura; o Fit dá a ela o que sobrar, até a de sempre.
		_artMax = character ? CharacterArtMax : ArtMax;
		var artFrame = new PanelContainer { CustomMinimumSize = new Vector2(0, character ? CharacterArtMin : ArtMin) };
		_art = artFrame;
		artFrame.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(ItemInfo.RarityColor(_item), 0.07f),
			new Color(ItemInfo.RarityColor(_item), 0.5f), 1, UiTokens.RadiusPanel, 0, 0));
		if (character)
		{
			// O personagem em 3D, humano e lobo: a silhueta não diz quem é.
			var preview = new CharacterPreview
			{
				HumanModel = _item.Character.HumanModel,
				WolfModel = _item.Character.WolfModel,
				SizeFlagsVertical = SizeFlags.ExpandFill
			};
			artFrame.AddChild(preview);
			column.AddChild(Tabs.Make(new[] { "Humano", "Lobo" }, 0, i => preview.Wolf = i == 1));
		}
		else
		{
			// A vitrine: o fundo da raridade e a arma grande, por cima.
			bool dim = Profile.IsExpired(_item.Id);
			var stage = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
			ItemBackdrop backdrop = UiKit.Fill(ItemBackdrop.Make(ItemInfo.RarityColor(_item), _item.Rarity, dim, _item.Id));
			backdrop.OffsetLeft = backdrop.OffsetTop = 1;
			backdrop.OffsetRight = backdrop.OffsetBottom = -1;
			stage.AddChild(backdrop);
			WeaponArt art = UiKit.Fill(WeaponArt.Make(_item.Id, Vector2.Zero, dim, showcase: true));
			art.OffsetLeft = UiTokens.S2;
			art.OffsetRight = -UiTokens.S2;
			art.OffsetTop = UiTokens.S3;
			art.OffsetBottom = -UiTokens.S3;
			stage.AddChild(art);
			artFrame.AddChild(stage);
		}
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
			lockLine.AddChild(UiKit.Text(Loc.F("Exige a patente {0}", Ranks.NameOf(_item.MinRank)), null, UiTokens.Warning));
			column.AddChild(lockLine);
		}

		Tabs tabs = Tabs.Make(new[] { "Descrição", "Atributos" }, 1, ShowTab);
		column.AddChild(tabs);

		// Os atributos, numa área que só rola se a janela for baixa demais para tudo.
		_scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			CustomMinimumSize = new Vector2(0, BodyMin),
		};
		_tabBody = UiKit.Column(UiTokens.S2);
		_tabBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_scroll.AddChild(_tabBody);
		column.AddChild(_scroll);
		ShowTab(1);

		// O que sobra de altura fica aqui: as ações moram no pé do painel.
		column.AddChild(UiKit.Spacer());
		if (actions != null) column.AddChild(actions);

		Resized += QueueFit;
		_tabBody.MinimumSizeChanged += QueueFit;
	}

	public override void _Ready()
	{
		if (_column == null) return;
		GetViewport().SizeChanged += QueueFit;
		QueueFit();
	}

	public override void _ExitTree()
	{
		if (_column != null && GetViewport() is Viewport viewport) viewport.SizeChanged -= QueueFit;
	}

	private bool _fitQueued;

	private void QueueFit()
	{
		if (_fitQueued || _column == null) return;
		_fitQueued = true;
		Callable.From(Fit).CallDeferred();
	}

	/// <summary>A altura que a área dos atributos pede agora (o conteúdo dela inteiro).</summary>
	public float BodyWanted => _tabBody?.GetCombinedMinimumSize().Y ?? 0f;

	/// <summary>A área dos atributos está rolando (não coube inteira).</summary>
	public bool BodyScrolls => _scroll != null && _scroll.CustomMinimumSize.Y < BodyWanted - 0.5f;

	/// <summary>
	/// Reparte a altura: o que a tela dá ao painel (do topo dele ao pé da
	/// tela), menos o que é fixo (cabeçalho, abas, ações), vai primeiro para
	/// os atributos inteiros e o resto para a vitrine, entre a menor e a de
	/// sempre. A conta parte da tela, não do tamanho do painel: um painel
	/// maior que a tela "cabe" no próprio tamanho e nunca encolheria.
	/// </summary>
	public void Fit()
	{
		_fitQueued = false;
		if (_column == null || !IsInsideTree()) return;

		float artMin = _artMax >= CharacterArtMax ? CharacterArtMin : ArtMin;
		float around = Size.Y - _column.Size.Y;                                   // as margens e a moldura
		float fixedParts = _column.GetCombinedMinimumSize().Y - _art.CustomMinimumSize.Y - _scroll.CustomMinimumSize.Y;
		float available = GetViewportRect().Size.Y - GlobalPosition.Y - BottomGap;
		float room = available - around - fixedParts;

		float body = Mathf.Min(BodyWanted, Mathf.Max(BodyMin, room - artMin));
		float art = Mathf.Clamp(room - body, artMin, _artMax);
		if (Mathf.Abs(_scroll.CustomMinimumSize.Y - body) > 0.5f) _scroll.CustomMinimumSize = new Vector2(0, body);
		if (Mathf.Abs(_art.CustomMinimumSize.Y - art) > 0.5f) _art.CustomMinimumSize = new Vector2(0, art);
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
			// As seis barras em duas colunas: metade da altura (o painel tem que caber na tela sem rolar).
			GridContainer stars = new() { Name = "Barras", Columns = 2 };
			stars.AddThemeConstantOverride("h_separation", UiTokens.S4);
			stars.AddThemeConstantOverride("v_separation", UiTokens.S1);
			stars.AddChild(StatBar.Stars("Dano", def.StarDamage, cmp ? other.StarDamage : -1));
			stars.AddChild(StatBar.Stars("Precisão", def.StarAccuracy, cmp ? other.StarAccuracy : -1));
			stars.AddChild(StatBar.Stars("Cadência", def.StarRate, cmp ? other.StarRate : -1));
			stars.AddChild(StatBar.Stars("Alcance", def.StarRange, cmp ? other.StarRange : -1));
			stars.AddChild(StatBar.Stars("Mobilidade", def.StarMobility, cmp ? other.StarMobility : -1));
			stars.AddChild(StatBar.Stars("Controle", def.StarRecoil, cmp ? other.StarRecoil : -1));
			bars.Add(stars);

			GridContainer facts = new() { Columns = 2 };
			facts.AddThemeConstantOverride("h_separation", UiTokens.S4);
			facts.AddThemeConstantOverride("v_separation", 2);
			Fact(facts, "Pente", def.Magazine.ToString());
			Fact(facts, "Cadência", Loc.F("{0:0} por min", def.Rpm));
			Fact(facts, "Recarga", $"{def.ReloadTactical:0.0} s");
			Fact(facts, "Velocidade", $"{def.SpeedMultiplier * 100:0}%");
			bars.Add(facts);

			if (cmp)
			{
				Label note = UiKit.Text(Loc.F("Comparando com {0}, equipada no mesmo lugar", compare.Name), "Caption");
				note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				bars.Add(note);
			}

			return bars;
		}

		if (item.Character is CharacterSkin skin)
		{
			CharacterSkin current = compare?.Character != null && compare != item ? compare.Character : null;
			float hp = FormController.DefaultHumanHp * skin.HpMultiplier;
			// Sem personagem no time, compara com o de sempre (vida 200).
			float otherHp = FormController.DefaultHumanHp * (current?.HpMultiplier ?? 1f);
			bars.Add(StatBar.Make("Vida", hp / 300f, $"{hp:0}", Mathf.IsEqualApprox(hp, otherHp) ? -1 : otherHp / 300f));

			string team = item.Team == Teams.Blue ? Teams.TitleOf(Teams.Blue) : item.Team == Teams.Orange ? Teams.TitleOf(Teams.Orange) : "Os dois";
			bars.Add(UiKit.Stat("Time", team));
			bars.Add(UiKit.Stat("Vale nas duas formas", "humano e lobo"));
			bars.Add(UiKit.Paragraph("A vida é a mesma como humano e como lobo; o lobo soma o escudo da classe por cima."));
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
			bars.Add(UiKit.Paragraph(Loc.F("Até {0} melhorias equipadas ao mesmo tempo. Vale só para você.", Profile.MaxTattoos)));
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
