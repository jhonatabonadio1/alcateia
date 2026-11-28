using Godot;
using System.Linq;

/// <summary>
/// A foto de perfil, numa tela só dela (Jhon, 27/11: "em perfil, a área de
/// ícones deve ficar em um lugar melhor, em outra tela"). Antes era uma
/// grade de quadradinhos de 52 px espremida embaixo da patente.
///
/// À esquerda, a foto escolhida, grande, na moldura do nível — como os
/// outros veem na sala e no placar. À direita, todas as fotos em tamanho
/// que dá para ver: as suas primeiro (clicou, trocou), depois as da loja,
/// apagadas, com o cadeado e o preço (clicar leva à loja, naquela foto).
/// </summary>
public partial class PhotoScreen : LobbyScreen
{
	public override IconKind NavIcon => IconKind.Profile;

	protected override bool FollowsAccount => true;

	private const int Cell = 124;
	private bool _shown;

	protected override void Build()
	{
		VBoxContainer page = Page(UiTokens.S3);

		HBoxContainer head = UiKit.Row(UiTokens.S3);
		head.AddChild(Deco.Title("Foto de perfil", "Perfil"));
		head.AddChild(UiKit.Spacer());
		CtaButton back = CtaButton.Make("Voltar ao perfil", CtaKind.Ghost, () => Shell.GoProfile(), 40, 17);
		back.SizeFlagsVertical = SizeFlags.ShrinkEnd;
		head.AddChild(back);
		page.AddChild(head);

		HBoxContainer split = UiKit.Row(UiTokens.S4);
		split.SizeFlagsVertical = SizeFlags.ExpandFill;
		page.AddChild(split);

		Control current = Current();
		split.AddChild(current);

		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S4, UiTokens.S3);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		split.AddChild(panel);

		Avatars.Photo[] mine = Avatars.All.Where(p => Avatars.Owns(p.Id)).ToArray();
		Avatars.Photo[] shop = Avatars.All.Where(p => !Avatars.Owns(p.Id)).ToArray();

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		VBoxContainer inside = UiKit.Column(UiTokens.S3);
		inside.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(inside);
		column.AddChild(scroll);

		inside.AddChild(Section("Suas fotos", mine.Length));
		inside.AddChild(Grid(mine));
		if (shop.Length > 0)
		{
			inside.AddChild(Section("Na loja", shop.Length));
			inside.AddChild(Grid(shop));
		}

		if (_shown) return;
		_shown = true;
		Motion.Rise(current, 0.04f, new Vector2(-28, 0));
		Motion.Rise(panel, 0.1f, new Vector2(0, 28));
	}

	private static Control Section(string name, int count)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.AddChild(Deco.Kicker(name));
		row.AddChild(UiKit.Text(count.ToString(), "Numeric", UiTokens.TextDisabled));
		Control line = Deco.Hairline();
		line.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(line);
		return row;
	}

	/// <summary>A foto de agora, como os outros a veem.</summary>
	private Control Current()
	{
		PanelContainer panel = UiKit.Card(out VBoxContainer column, UiTokens.S5, UiTokens.S3);
		panel.CustomMinimumSize = new Vector2(420, 0);
		column.Alignment = BoxContainer.AlignmentMode.Center;

		RankBadge badge = RankBadge.Make(Profile.Rank, 280, avatar: Avatars.Selected);
		badge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		column.AddChild(badge);
		column.AddChild(UiKit.Gap(UiTokens.S3));

		Label name = Deco.Big(Avatars.Get(Avatars.Selected)?.Name ?? "", 34);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(name);
		Label note = UiKit.Paragraph("É a foto que aparece na sala, no placar e na lista de amigos, dentro da moldura do seu nível.", "Caption");
		note.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(note);
		return panel;
	}

	private Control Grid(Avatars.Photo[] photos)
	{
		var flow = new HFlowContainer();
		flow.AddThemeConstantOverride("h_separation", UiTokens.S3);
		flow.AddThemeConstantOverride("v_separation", UiTokens.S3);
		foreach (Avatars.Photo photo in photos) flow.AddChild(CellOf(photo));
		return flow;
	}

	private Control CellOf(Avatars.Photo photo)
	{
		bool mine = Avatars.Owns(photo.Id), chosen = mine && Avatars.Selected == photo.Id;
		var cell = new Button
		{
			CustomMinimumSize = new Vector2(Cell, Cell + 26),
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			ClipContents = true,
			TooltipText = mine ? photo.Name : Loc.F("{0} · {1} de Cash na loja", photo.Name, UiTokens.Thousands(photo.Price))
		};
		Color edge = chosen ? UiTokens.Accent : UiTokens.Border;
		cell.AddThemeStyleboxOverride("normal", UiTheme.Box(new Color(0, 0, 0, 0.3f), edge, chosen ? 2 : 1, 0, 0, 0));
		cell.AddThemeStyleboxOverride("hover", UiTheme.Box(new Color(UiTokens.Paper, 0.08f), chosen ? UiTokens.Accent : new Color(UiTokens.Paper, 0.7f), chosen ? 2 : 1, 0, 0, 0));
		cell.AddThemeStyleboxOverride("pressed", UiTheme.Box(new Color(UiTokens.Paper, 0.12f), UiTokens.Accent, 2, 0, 0, 0));
		Sfx.Wire(cell);

		var face = new TextureRect
		{
			Texture = Avatars.PhotoOf(photo.Id),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			Modulate = mine ? Colors.White : new Color(0.38f, 0.4f, 0.46f)
		};
		face.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		face.OffsetLeft = face.OffsetTop = 4;
		face.OffsetRight = -4;
		face.OffsetBottom = Cell - 4;
		cell.AddChild(face);

		// Embaixo da foto: o nome, ou o preço de quem ainda não é sua.
		Label under = UiKit.Truncated(mine ? photo.Name : UiTokens.Thousands(photo.Price) + " C", "Caption",
			chosen ? UiTokens.Accent : mine ? UiTokens.TextSecondary : CurrencyLabel.ColorOf(Currency.Presas));
		under.AddThemeFontSizeOverride("font_size", 12);
		under.HorizontalAlignment = HorizontalAlignment.Center;
		under.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		under.OffsetTop = -24;
		under.OffsetBottom = -4;
		under.OffsetLeft = 4;
		under.OffsetRight = -4;
		cell.AddChild(under);

		if (!mine)
		{
			Icon lockIcon = Icon.Make(IconKind.Lock, 22, UiTokens.TextPrimary);
			lockIcon.SetAnchorsPreset(LayoutPreset.TopLeft);
			lockIcon.Position = new Vector2(Cell / 2f - 11, Cell / 2f - 11);
			lockIcon.Size = new Vector2(22, 22);
			cell.AddChild(lockIcon);
		}
		if (chosen)
		{
			PanelContainer tag = Deco.Tag("Em uso", UiTokens.Accent, UiTokens.AccentText);
			tag.Position = new Vector2(4, 4);
			cell.AddChild(tag);
		}

		string id = photo.Id;
		cell.Pressed += () =>
		{
			if (!mine)
			{
				Shell.GoShop("foto_" + id);
				return;
			}
			Avatars.Selected = id;
			Profile.NotifyChanged();
			Refresh();
		};
		return cell;
	}
}
