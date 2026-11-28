using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// O painel social, na direita da moldura (Jhon, 27/11: "tire esse ranking,
/// deixe a parte do social, amigos, igual Valorant, inclusive convidar,
/// aceitar pedidos"). Mora no lobby inteiro, não numa tela: os amigos
/// continuam ali na loja, no inventário, na sala.
///
/// De cima para baixo: você, com a sua tag (Nome#0042, e o botão de
/// copiar: é o que se passa a um amigo); os convites para sala e para grupo
/// que chegaram; o seu grupo (quem está, quem lidera, sair); os pedidos de
/// amizade (aceitar, recusar); os amigos conectados, com onde estão — e os
/// botões de chamar para o grupo e, se você está numa sala, para a sala; os
/// desconectados; os pedidos que você mandou; e o campo de adicionar pelo
/// nome com a tag.
///
/// Recolhido, vira uma tira com as fotos de quem está conectado (as telas
/// largas — loja, inventário — abrem com ele assim).
///
/// Quem sabe dos amigos é o servidor (<see cref="Social"/>); aqui só se
/// mostra e se pede.
/// </summary>
public partial class SocialDock : PanelContainer
{
	public const int Wide = 304, Narrow = 60;

	private bool _open = true;
	private bool _offlineOpen;
	private VBoxContainer _list;
	private bool _dirty;
	private string _typed = "";
	private double _clock;

	public bool Open
	{
		get => _open;
		set
		{
			if (_open == value) return;
			_open = value;
			Rebuild();
		}
	}

	public override void _Ready()
	{
		ThemeTypeVariation = "Bare";
		// Uma placa escura colada na beira, com um fio à esquerda.
		var box = new StyleBoxFlat { BgColor = new Color(0.02f, 0.035f, 0.055f, 0.72f), BorderColor = UiTokens.Border };
		box.BorderWidthLeft = 1;
		AddThemeStyleboxOverride("panel", box);
		SizeFlagsVertical = SizeFlags.ExpandFill;

		Social.Changed += MarkDirty;
		LobbyLink.Instance.Changed += MarkDirty;
		Profile.Changed += MarkDirty;
		Rebuild();
	}

	public override void _ExitTree()
	{
		Social.Changed -= MarkDirty;
		Profile.Changed -= MarkDirty;
		if (LobbyLink.Instance is LobbyLink link && IsInstanceValid(link)) link.Changed -= MarkDirty;
	}

	// Várias notícias no mesmo quadro (a lista e a sala) refazem uma vez só.
	private void MarkDirty()
	{
		if (_dirty) return;
		_dirty = true;
		Callable.From(() =>
		{
			_dirty = false;
			if (IsInsideTree()) Rebuild();
		}).CallDeferred();
	}

	public override void _Process(double delta)
	{
		// Os convites vencem sozinhos.
		_clock += delta;
		if (_clock < 1.0) return;
		_clock = 0;
		Social.Expire(Time.GetTicksMsec() / 1000.0);
	}

	// ------------------------------------------------------------------ montagem

	private void Rebuild()
	{
		// O que se estava digitando no campo de adicionar sobrevive.
		if (_field != null && IsInstanceValid(_field)) _typed = _field.Text;
		bool typing = _field != null && IsInstanceValid(_field) && _field.HasFocus();
		_field = null;

		UiKit.Clear(this);
		CustomMinimumSize = new Vector2(_open ? Wide : Narrow, 0);
		if (_open) BuildOpen(typing);
		else BuildStrip();
	}

	private void BuildStrip()
	{
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		MarginContainer margin = UiKit.Margin(UiTokens.S2, UiTokens.S3, UiTokens.S2, UiTokens.S3);
		margin.AddChild(column);
		AddChild(margin);

		Button expand = UiKit.IconButton(IconKind.Users, "Abrir os amigos", () => LobbyShell.Instance?.ToggleSocial(), 22);
		expand.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		column.AddChild(expand);

		int waiting = Social.Requests.Count + Social.Invites.Count + Social.GroupInvites.Count;
		if (waiting > 0)
		{
			PanelContainer badge = Deco.Tag(waiting.ToString(), UiTokens.Accent, UiTokens.AccentText);
			badge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			badge.TooltipText = Loc.T("Pedidos e convites à espera");
			column.AddChild(badge);
		}

		foreach (Social.Person friend in Social.Friends.Where(f => f.Connected).Take(9))
		{
			Control face = Face(friend, 40);
			face.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
			face.TooltipText = $"{friend.Name} · {Where(friend)}";
			face.MouseFilter = MouseFilterEnum.Pass;
			column.AddChild(face);
		}

		column.AddChild(UiKit.Spacer());
		Label count = UiKit.Text(Social.OnlineCount.ToString(), "Numeric", UiTokens.TextSecondary);
		count.HorizontalAlignment = HorizontalAlignment.Center;
		count.TooltipText = Loc.T("Amigos conectados");
		count.MouseFilter = MouseFilterEnum.Pass;
		column.AddChild(count);
	}

	private void BuildOpen(bool typing)
	{
		VBoxContainer column = UiKit.Column(0);
		AddChild(column);

		// Cabeça: SOCIAL e o botão de recolher.
		MarginContainer headPad = UiKit.Margin(UiTokens.S3, UiTokens.S2, UiTokens.S2, UiTokens.S2);
		HBoxContainer head = UiKit.Row(UiTokens.S2);
		headPad.AddChild(head);
		head.AddChild(Icon.Make(IconKind.Users, 18, UiTokens.TextPrimary));
		Label title = UiKit.Text(Loc.Upper("Social"), "Subtitle");
		title.AddThemeFontOverride("font", UiFonts.Wide);
		title.AddThemeFontSizeOverride("font_size", 18);
		head.AddChild(title);
		head.AddChild(UiKit.Spacer());
		head.AddChild(UiKit.IconButton(IconKind.Expand, "Recolher os amigos", () => LobbyShell.Instance?.ToggleSocial(), 16));
		column.AddChild(headPad);

		column.AddChild(Me());

		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_list = UiKit.Column(0);
		_list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_list);
		column.AddChild(scroll);
		FillList();

		column.AddChild(AddRow(typing));
	}

	/// <summary>Você, no alto: a foto, o nome e onde está.</summary>
	private Control Me()
	{
		var plate = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = new Color(UiTokens.Paper, 0.06f), BorderColor = UiTokens.Accent };
		box.BorderWidthLeft = 2;
		box.ContentMarginLeft = UiTokens.S3;
		box.ContentMarginRight = UiTokens.S2;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		plate.AddThemeStyleboxOverride("panel", box);

		HBoxContainer row = UiKit.Row(UiTokens.S2 + 2);
		plate.AddChild(row);
		row.AddChild(PhotoTile.Make(Avatars.Selected, 44));

		VBoxContainer text = UiKit.Column(0);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		// O nome e, apagada, a tag: juntos são o que um amigo escreve para adicionar.
		HBoxContainer who = UiKit.Row(2);
		Label name = UiKit.Truncated(Profile.PlayerName, "Subtitle");
		name.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		name.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		who.AddChild(name);
		if (Backend.AccountTag.Length > 0) who.AddChild(TagButton());
		text.AddChild(who);
		bool linked = LobbyLink.Instance.Connected;
		string where = !linked ? "Sem ligação" : LobbyLink.InRoom ? Loc.F("Na sala #{0}", LobbyLink.Instance.Room?["id"]) : "Conectado";
		text.AddChild(UiKit.Text(Loc.T(where), "Caption", linked ? UiTokens.Success : UiTokens.TextDisabled));
		row.AddChild(text);
		return plate;
	}

	/// <summary>A tag, apagada, colada no nome. Clicar copia o nome com ela.</summary>
	private static Button TagButton()
	{
		var tag = new Button
		{
			Text = "#" + Backend.AccountTag,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = Loc.F("A sua tag. Clique para copiar {0} e passar a um amigo", Backend.AccountHandle)
		};
		tag.AddThemeFontOverride("font", UiFonts.SemiBold);
		tag.AddThemeFontSizeOverride("font_size", 15);
		tag.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
		tag.AddThemeColorOverride("font_hover_color", UiTokens.Accent);
		var none = new StyleBoxEmpty();
		foreach (string state in new[] { "normal", "hover", "pressed", "focus" }) tag.AddThemeStyleboxOverride(state, none);
		tag.Pressed += () =>
		{
			DisplayServer.ClipboardSet(Backend.AccountHandle);
			LobbyShell.Instance?.Toast(Loc.F("{0} copiado: passe para um amigo adicionar você", Backend.AccountHandle), UiTokens.Success);
		};
		return tag;
	}

	private void FillList()
	{
		if (!LobbyLink.Instance.Connected)
		{
			_list.AddChild(Note("Sem ligação com o servidor: os amigos aparecem quando ela voltar.", loading: Backend.LoggedIn));
			return;
		}
		if (Social.Unsupported)
		{
			_list.AddChild(Note("Este servidor ainda não tem amigos."));
			return;
		}
		if (!Social.Loaded)
		{
			_list.AddChild(Note("Carregando os amigos…", loading: true));
			return;
		}

		foreach (Social.Invite invite in Social.Invites) _list.AddChild(InviteRow(invite));
		foreach (Social.GroupInvite invite in Social.GroupInvites) _list.AddChild(GroupInviteRow(invite));

		if (Social.InGroup)
		{
			_list.AddChild(Section("Grupo", Social.Group.Count, UiTokens.TeamBlue));
			foreach (Social.Person mate in Social.Group) _list.AddChild(GroupRow(mate));
			_list.AddChild(GroupFoot());
		}

		if (Social.Requests.Count > 0)
		{
			_list.AddChild(Section("Pedidos", Social.Requests.Count, UiTokens.Accent));
			foreach (Social.Person person in Social.Requests) _list.AddChild(RequestRow(person));
		}

		List<Social.Person> online = Social.Friends.Where(f => f.Connected).ToList();
		List<Social.Person> offline = Social.Friends.Where(f => !f.Connected).ToList();

		_list.AddChild(Section("Conectados", online.Count));
		foreach (Social.Person friend in online) _list.AddChild(FriendRow(friend));
		if (online.Count == 0)
			_list.AddChild(Note(Social.Friends.Count == 0
				? "Você ainda não tem amigos. Adicione pelo nome com a tag (Nome#0000), aqui embaixo."
				: "Nenhum amigo conectado agora."));

		if (offline.Count > 0)
		{
			_list.AddChild(Section("Desconectados", offline.Count, null, _offlineOpen, () => { _offlineOpen = !_offlineOpen; Rebuild(); }));
			if (_offlineOpen)
				foreach (Social.Person friend in offline) _list.AddChild(FriendRow(friend));
		}

		if (Social.Sent.Count > 0)
		{
			_list.AddChild(Section("Pedidos enviados", Social.Sent.Count));
			foreach (Social.Person person in Social.Sent) _list.AddChild(SentRow(person));
		}
	}

	private static Control Note(string text, bool loading = false)
	{
		MarginContainer pad = UiKit.Margin(UiTokens.S3, UiTokens.S2, UiTokens.S3, UiTokens.S2);
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		pad.AddChild(row);
		if (loading) row.AddChild(Loader.Make(18));
		Label label = UiKit.Paragraph(text, "Caption");
		row.AddChild(label);
		return pad;
	}

	/// <summary>O título de um trecho da lista: nome, contagem e, se dobra, a seta.</summary>
	private static Control Section(string name, int count, Color? color = null, bool? open = null, Action toggle = null)
	{
		var button = new Button
		{
			Flat = true,
			CustomMinimumSize = new Vector2(0, 34),
			FocusMode = toggle == null ? FocusModeEnum.None : FocusModeEnum.All,
			MouseFilter = toggle == null ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop,
			MouseDefaultCursorShape = toggle == null ? CursorShape.Arrow : CursorShape.PointingHand
		};
		if (toggle != null) button.Pressed += toggle;

		MarginContainer pad = UiKit.Fill(UiKit.Margin(UiTokens.S3, UiTokens.S2, UiTokens.S3, 0));
		pad.MouseFilter = MouseFilterEnum.Ignore;
		button.AddChild(pad);
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.MouseFilter = MouseFilterEnum.Ignore;
		pad.AddChild(row);

		Label label = UiKit.Caption(name, color ?? UiTokens.TextSecondary);
		label.AddThemeFontSizeOverride("font_size", 12);
		row.AddChild(label);
		row.AddChild(UiKit.Text(count.ToString(), "Numeric", color ?? UiTokens.TextDisabled));
		Control line = Deco.Hairline(new Color(UiTokens.Paper, 0.14f));
		line.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(line);
		if (open is bool isOpen)
		{
			Label arrow = UiKit.Text(isOpen ? "–" : "+", "Numeric", UiTokens.TextSecondary);
			row.AddChild(arrow);
		}
		return button;
	}

	// ------------------------------------------------------------------ linhas

	/// <summary>A foto com o ponto de onde a pessoa está.</summary>
	private static Control Face(Social.Person person, int size)
	{
		PhotoTile photo = PhotoTile.Make(person.Photo, size);
		Color status = StatusColor(person.Status);
		photo.Edge = person.Connected ? new Color(status, 0.9f) : new Color(1, 1, 1, 0.12f);
		if (!person.Connected) photo.Modulate = new Color(0.62f, 0.66f, 0.72f);
		return photo;
	}

	private static Color StatusColor(Social.Presence status) => status switch
	{
		Social.Presence.App => UiTokens.TextSecondary,
		Social.Presence.Online => UiTokens.Success,
		Social.Presence.Room => UiTokens.Accent,
		Social.Presence.Playing => UiTokens.TeamBlue,
		_ => UiTokens.TextDisabled
	};

	private static string Where(Social.Person person) => person.Status switch
	{
		Social.Presence.App => Loc.T("No aplicativo, com o jogo fechado"),
		Social.Presence.Online => Loc.T("No menu"),
		Social.Presence.Room => Loc.F("Na sala #{0}", person.Room),
		Social.Presence.Playing => Loc.T("Em partida"),
		_ => Loc.T("Desconectado")
	};

	/// <summary>A linha de uma pessoa: foto, nome, uma linha de estado e os
	/// botões à direita (que só aparecem com o mouse em cima, se
	/// <paramref name="onHover"/>).</summary>
	private static (PanelContainer Row, HBoxContainer Actions) PersonRow(Social.Person person, string under, Color underColor, bool onHover)
	{
		var row = new PanelContainer { CustomMinimumSize = new Vector2(0, 52), MouseFilter = MouseFilterEnum.Stop };
		var idle = new StyleBoxFlat { BgColor = Colors.Transparent };
		var hot = new StyleBoxFlat { BgColor = new Color(UiTokens.Paper, 0.07f) };
		foreach (StyleBoxFlat box in new[] { idle, hot })
		{
			box.ContentMarginLeft = UiTokens.S3;
			box.ContentMarginRight = UiTokens.S2;
		}
		row.AddThemeStyleboxOverride("panel", idle);

		HBoxContainer line = UiKit.Row(UiTokens.S2 + 2);
		line.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(line);
		line.AddChild(Face(person, 36));

		VBoxContainer text = UiKit.Column(0);
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		HBoxContainer top = UiKit.Row(UiTokens.S1 + 2);
		top.MouseFilter = MouseFilterEnum.Ignore;
		Label name = UiKit.Truncated(person.Name, null, person.Connected || !onHover ? UiTokens.TextPrimary : UiTokens.TextSecondary);
		name.AddThemeFontOverride("font", UiFonts.SemiBold);
		top.AddChild(name);
		text.AddChild(top);
		Label where = UiKit.Truncated(under, "Caption", underColor);
		where.AddThemeFontSizeOverride("font_size", 12);
		text.AddChild(where);
		line.AddChild(text);

		HBoxContainer actions = UiKit.Row(0);
		actions.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		line.AddChild(actions);

		if (onHover)
		{
			actions.Modulate = new Color(1, 1, 1, 0f);
			row.MouseEntered += () => { row.AddThemeStyleboxOverride("panel", hot); actions.Modulate = Colors.White; };
			row.MouseExited += () =>
			{
				// Sair para cima de um botão da própria linha não é sair dela.
				if (row.GetGlobalRect().HasPoint(row.GetGlobalMousePosition())) return;
				row.AddThemeStyleboxOverride("panel", idle);
				actions.Modulate = new Color(1, 1, 1, 0f);
			};
		}
		return (row, actions);
	}

	private static Button Act(IconKind icon, string tooltip, Action pressed, Color? tint = null)
	{
		Button button = UiKit.IconButton(icon, tooltip, pressed, 16);
		button.CustomMinimumSize = new Vector2(30, 30);
		if (tint is Color c)
		{
			Icon glyph = button.GetChild<Icon>(0);
			glyph.Tint = c;
			button.MouseExited += () => glyph.Tint = c;
		}
		return button;
	}

	private Control FriendRow(Social.Person friend)
	{
		(PanelContainer row, HBoxContainer actions) = PersonRow(friend, Where(friend), friend.Connected ? StatusColor(friend.Status) : UiTokens.TextDisabled, onHover: true);
		row.TooltipText = friend.Handle;

		// Para o grupo: quem está com o jogo aberto e ainda não está nele. Com
		// grupo, só o líder chama; sem grupo, chamar é o que cria um.
		if (friend.InGame && !Social.InMyGroup(friend.Id) && (!Social.InGroup || Social.LeadsGroup) && Social.Group.Count < Social.GroupMax)
			actions.AddChild(Act(IconKind.Users, Loc.F("Chamar {0} para o grupo: vocês entram juntos nas salas, no mesmo time", friend.Name), () =>
			{
				Social.InviteToGroup(friend.Id);
				LobbyShell.Instance?.Toast(Loc.F("Convite para o grupo enviado a {0}", friend.Name));
			}, UiTokens.TeamBlue));

		// Numa sala, dá para chamar quem está no jogo e não está jogando.
		if (LobbyLink.InRoom && friend.InGame && friend.Status != Social.Presence.Playing)
		{
			int mine = LobbyLink.Instance.Room?["id"]?.GetValue<int>() ?? -1;
			if (friend.Room != mine)
				actions.AddChild(Act(IconKind.Plus, Loc.F("Convidar {0} para a sua sala", friend.Name), () =>
				{
					Social.InviteToRoom(friend.Id);
					LobbyShell.Instance?.Toast(Loc.F("Convite enviado para {0}", friend.Name));
				}, UiTokens.Accent));
		}

		actions.AddChild(Act(IconKind.Close, Loc.F("Desfazer a amizade com {0}", friend.Name), () =>
		{
			Label body = UiKit.Paragraph(Loc.F("{0} sai da sua lista, e você da dele.", friend.Name));
			body.CustomMinimumSize = new Vector2(380, 0);
			LobbyShell.Instance?.Modal("Desfazer a amizade?", body,
				new Modal.Choice("Desfazer", ButtonKind.Danger, () => Social.Remove(friend.Id)),
				new Modal.Choice("Manter", ButtonKind.Primary, null));
		}));
		return row;
	}

	private static Control RequestRow(Social.Person person)
	{
		(PanelContainer row, HBoxContainer actions) = PersonRow(person, Loc.T("Quer ser seu amigo"), UiTokens.Accent, onHover: false);
		actions.AddChild(Act(IconKind.Check, Loc.F("Aceitar {0}", person.Name), () => Social.Accept(person.Id), UiTokens.Success));
		actions.AddChild(Act(IconKind.Close, Loc.F("Recusar {0}", person.Name), () => Social.Decline(person.Id), UiTokens.Danger));
		return row;
	}

	private static Control SentRow(Social.Person person)
	{
		(PanelContainer row, HBoxContainer actions) = PersonRow(person, Loc.T("Aguardando resposta"), UiTokens.TextDisabled, onHover: false);
		actions.AddChild(Act(IconKind.Close, Loc.F("Cancelar o pedido a {0}", person.Name), () => Social.Decline(person.Id)));
		return row;
	}

	/// <summary>Um convite para sala: quem chamou, para onde, entrar ou recusar.</summary>
	private static Control InviteRow(Social.Invite invite)
	{
		var card = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = new Color(UiTokens.Accent, 0.12f), BorderColor = UiTokens.Accent };
		box.BorderWidthLeft = 2;
		box.ContentMarginLeft = UiTokens.S3;
		box.ContentMarginRight = UiTokens.S2;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		card.AddThemeStyleboxOverride("panel", box);

		VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
		card.AddChild(column);
		column.AddChild(Deco.Kicker("Convite para sala"));
		HBoxContainer who = UiKit.Row(UiTokens.S2);
		who.AddChild(PhotoTile.Make(invite.From.Photo, 32));
		VBoxContainer text = UiKit.Column(0);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		Label name = UiKit.Truncated(invite.From.Name);
		name.AddThemeFontOverride("font", UiFonts.SemiBold);
		text.AddChild(name);
		string map = string.IsNullOrEmpty(invite.Map) ? "" : " · " + Maps.Get(invite.Map).Name;
		Label where = UiKit.Truncated(Loc.F("Sala #{0}{1}", invite.Room, map), "Caption");
		where.AddThemeFontSizeOverride("font_size", 12);
		text.AddChild(where);
		who.AddChild(text);
		column.AddChild(who);

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		CtaButton enter = CtaButton.Make("Entrar", CtaKind.Gold, () => Social.AcceptInvite(invite), 32, 16);
		enter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		enter.Cut = 8;
		buttons.AddChild(enter);
		CtaButton no = CtaButton.Make("Recusar", CtaKind.Ghost, () => Social.DeclineInvite(invite), 32, 16);
		no.Cut = 8;
		buttons.AddChild(no);
		column.AddChild(buttons);
		return card;
	}

	// ------------------------------------------------------------------ grupo

	/// <summary>Alguém do grupo: onde está, e — para o líder — passar a
	/// liderança ou tirar do grupo.</summary>
	private static Control GroupRow(Social.Person mate)
	{
		bool me = mate.Id == Backend.AccountId, leader = mate.Id == Social.GroupLeader;
		string under = (leader ? Loc.T("Líder") + " · " : "") + (me ? Loc.T("Você") : Where(mate));
		(PanelContainer row, HBoxContainer actions) = PersonRow(mate, under, leader ? UiTokens.Accent : StatusColor(mate.Status), onHover: true);
		row.TooltipText = mate.Handle;

		if (Social.LeadsGroup && !me)
		{
			actions.AddChild(Act(IconKind.Crown, Loc.F("Passar a liderança do grupo para {0}", mate.Name), () => Social.PromoteInGroup(mate.Id), UiTokens.Accent));
			actions.AddChild(Act(IconKind.Close, Loc.F("Tirar {0} do grupo", mate.Name), () => Social.KickFromGroup(mate.Id)));
		}
		return row;
	}

	/// <summary>O pé do grupo: o que ele faz, em uma linha, e sair.</summary>
	private static Control GroupFoot()
	{
		MarginContainer pad = UiKit.Margin(UiTokens.S3, UiTokens.S1, UiTokens.S2, UiTokens.S2);
		VBoxContainer column = UiKit.Column(UiTokens.S2);
		pad.AddChild(column);
		Label how = UiKit.Paragraph(Social.LeadsGroup
			? "Você lidera: a sala em que você entrar leva o grupo junto, no mesmo time."
			: "O líder escolhe a sala e o grupo entra junto, no mesmo time.", "Caption");
		how.AddThemeFontSizeOverride("font_size", 12);
		column.AddChild(how);
		CtaButton leave = CtaButton.Make("Sair do grupo", CtaKind.Ghost, Social.LeaveGroup, 30, 15);
		leave.Cut = 8;
		column.AddChild(leave);
		return pad;
	}

	/// <summary>Um convite para grupo: quem chamou, entrar ou recusar.</summary>
	private static Control GroupInviteRow(Social.GroupInvite invite)
	{
		var card = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = new Color(UiTokens.TeamBlue, 0.14f), BorderColor = UiTokens.TeamBlue };
		box.BorderWidthLeft = 2;
		box.ContentMarginLeft = UiTokens.S3;
		box.ContentMarginRight = UiTokens.S2;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2;
		card.AddThemeStyleboxOverride("panel", box);

		VBoxContainer column = UiKit.Column(UiTokens.S1 + 2);
		card.AddChild(column);
		column.AddChild(Deco.Kicker("Convite para grupo", UiTokens.TeamBlue));
		HBoxContainer who = UiKit.Row(UiTokens.S2);
		who.AddChild(PhotoTile.Make(invite.From.Photo, 32));
		VBoxContainer text = UiKit.Column(0);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		Label name = UiKit.Truncated(invite.From.Name);
		name.AddThemeFontOverride("font", UiFonts.SemiBold);
		text.AddChild(name);
		Label what = UiKit.Truncated(Loc.T("Jogar juntos, no mesmo time"), "Caption");
		what.AddThemeFontSizeOverride("font_size", 12);
		text.AddChild(what);
		who.AddChild(text);
		column.AddChild(who);

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		CtaButton enter = CtaButton.Make("Entrar", CtaKind.Gold, () => Social.AcceptGroup(invite), 32, 16);
		enter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		enter.Cut = 8;
		buttons.AddChild(enter);
		CtaButton no = CtaButton.Make("Recusar", CtaKind.Ghost, () => Social.DeclineGroup(invite), 32, 16);
		no.Cut = 8;
		buttons.AddChild(no);
		column.AddChild(buttons);
		return card;
	}

	// ------------------------------------------------------------------ adicionar

	private LineEdit _field;

	private Control AddRow(bool typing)
	{
		var plate = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.25f), BorderColor = UiTokens.Border };
		box.BorderWidthTop = 1;
		box.ContentMarginLeft = box.ContentMarginRight = UiTokens.S2 + 4;
		box.ContentMarginTop = box.ContentMarginBottom = UiTokens.S2 + 2;
		plate.AddThemeStyleboxOverride("panel", box);

		HBoxContainer row = UiKit.Row(UiTokens.S2);
		plate.AddChild(row);
		// O nome, o # e os quatro algarismos da tag.
		_field = UiKit.Field(_typed, "Adicionar: Nome#0000", Profile.NameMax + 5);
		_field.TextSubmitted += _ => Ask();
		row.AddChild(_field);
		Button add = UiKit.IconButton(IconKind.Plus, "Enviar pedido de amizade", Ask, 18);
		add.CustomMinimumSize = new Vector2(UiTokens.FieldHeight, UiTokens.FieldHeight);
		row.AddChild(add);

		if (typing) Callable.From(() => { if (IsInstanceValid(_field) && _field.IsInsideTree()) { _field.GrabFocus(); _field.CaretColumn = _field.Text.Length; } }).CallDeferred();
		return plate;
	}

	private void Ask()
	{
		string name = _field?.Text.Trim() ?? "";
		if (name.Length == 0) return;
		if (!LobbyLink.Instance.Connected || Social.Unsupported)
		{
			LobbyShell.Instance?.Toast("Sem ligação com o servidor", UiTokens.Danger);
			return;
		}
		// Sem a tag o servidor recusa: diz aqui, sem ir e voltar, e o que foi escrito fica.
		if (!name.Contains('#') || name.EndsWith('#'))
		{
			LobbyShell.Instance?.Toast("Escreva o nome com a tag, assim: Nome#0000. A tag do seu amigo aparece no painel social dele.", UiTokens.Danger);
			return;
		}
		// A resposta vem pela lista (o pedido aparece em "enviados") ou por um
		// erro do servidor, que o lobby mostra.
		Social.Add(name);
		_field.Clear();
		_typed = "";
	}
}
