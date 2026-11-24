using Godot;
using System;

/// <summary>
/// A primeira tela do jogo (pedido do Jhon, 20/11): o login. Toda abertura
/// passa por aqui — o acesso pode estar salvo, mas entrar é um clique na
/// conta, nunca sozinho.
///
/// Mais elaborada (20/11, com o login do Overwatch de referência): o
/// fundo vivo (<see cref="LoginBackdrop"/>: polígonos que ondulam e
/// partículas que seguem o mouse), a marca grande no alto, o formulário no
/// meio, num cartão de vidro. À esquerda dele, as contas salvas neste
/// computador: clicou, entra (o login guardado é conferido no servidor; se
/// não vale mais, a conta sai da lista e o nome vai para o formulário, à
/// espera da senha). "Lembrar acesso" guarda o login desta conta para o
/// próximo clique. Embaixo, se o servidor está no ar e sair do jogo. Sem
/// conta não se joga: o "treinar sem conta" saiu (Jhon, 20/11).
///
/// O endereço do servidor é fixo no jogo por enquanto (Jhon, 20/11): a
/// tela não pergunta. A escolha do servidor mais rápido e mais perto,
/// os de partida inclusive, vem depois.
///
/// Sem conta não há progressão (decisão 0015: a carteira, os itens e a
/// patente são da conta).
/// </summary>
public partial class LoginScreen : LobbyScreen
{
	private bool _register;
	private bool _busy;
	private string _status = "";
	private string _name = "";
	private bool _keep = true;
	private bool _shown;
	private LineEdit _nameField, _password, _confirm;

	/// <summary>O servidor respondeu? (null: ainda perguntando).</summary>
	private bool? _online;

	public LoginScreen(string notice = null)
	{
		_status = notice ?? "";
	}

	protected override void Build()
	{
		_keep = Backend.KeepAccess;
		if (_online == null && !_asked) Ask();

		AddChild(new LoginBackdrop());

		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		VBoxContainer page = UiKit.Column(UiTokens.S5);
		center.AddChild(page);

		Control brand = Brand();
		page.AddChild(brand);

		HBoxContainer row = UiKit.Row(UiTokens.S4);
		row.Alignment = BoxContainer.AlignmentMode.Center;
		page.AddChild(row);
		Control saved = Backend.Saved.Count > 0 ? SavedCard() : null;
		if (saved != null) row.AddChild(saved);
		Control form = FormCard();
		row.AddChild(form);

		Control bottom = _bottom = BottomBar();
		AddChild(bottom);

		// A entrada: a marca desce, o cartão sobe (só na primeira vez; a
		// tela se refaz a cada mudança e não pisca de novo).
		if (!_shown)
		{
			_shown = true;
			Motion.FadeIn(brand, 0.7f, 0.05f);
			Motion.FadeIn(row, 0.6f, 0.25f);
			Motion.FadeIn(bottom, 0.6f, 0.5f);
		}

		// No quadro seguinte a tela pode já ter saído (entrou com a conta salva,
		// trocou de aba): foco em campo fora da árvore escrevia erro no Output.
		LineEdit focus = _nameField.Text.Length == 0 ? _nameField : _password;
		Callable.From(() => { if (IsInstanceValid(focus) && focus.IsInsideTree()) focus.GrabFocus(); }).CallDeferred();
	}

	// ------------------------------------------------------------------ a marca

	private Control Brand()
	{
		VBoxContainer brand = UiKit.Column(UiTokens.S3);
		brand.Alignment = BoxContainer.AlignmentMode.Center;

		// A logo de sempre, a mesma do site e do instalador (Jhon, 21/11:
		// "deixe padrão a logo"; sem o "humanos contra lobos" embaixo).
		var logo = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://src/assets/ui/marca/wolfleague_logo_horizontal.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			CustomMinimumSize = new Vector2(640, 83),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		brand.AddChild(logo);

		// O fio de ouro embaixo, que some nas pontas.
		var line = new TextureRect
		{
			Texture = Fade(UiTokens.Accent),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			CustomMinimumSize = new Vector2(460, 2),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		brand.AddChild(line);
		return brand;
	}

	private static Texture2D _fade;

	private static Texture2D Fade(Color color)
	{
		if (_fade != null) return _fade;
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(color, 0f));
		gradient.SetColor(1, new Color(color, 0f));
		gradient.AddPoint(0.5f, new Color(color, 0.9f));
		return _fade = new GradientTexture2D { Gradient = gradient, Width = 256, Height = 2 };
	}

	// ------------------------------------------------------------------ contas salvas

	private Control SavedCard()
	{
		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S4, UiTokens.S2);
		card.CustomMinimumSize = new Vector2(340, 0);
		card.SizeFlagsVertical = SizeFlags.ShrinkBegin;

		column.AddChild(Heading("CONTAS SALVAS"));
		column.AddChild(UiKit.Gap(UiTokens.S1));
		foreach (Backend.SavedAccess access in Backend.Saved)
			column.AddChild(AccountLine(access));
		return card;
	}

	/// <summary>Uma conta salva: a linha inteira é o botão (a inicial num
	/// círculo, o nome, a seta de entrar); o X, dentro dela, esquece.</summary>
	private Control AccountLine(Backend.SavedAccess access)
	{
		string shown = access.Name.Length > 0 ? access.Name : "Conta";

		Button enter = UiKit.Button("", ButtonKind.Secondary, () => EnterSaved(access));
		enter.SetMeta("conta", access.Name);
		enter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		enter.CustomMinimumSize = new Vector2(0, 64);
		enter.Disabled = _busy;
		enter.MouseDefaultCursorShape = CursorShape.PointingHand;
		Color accent = UiTokens.Accent;
		enter.AddThemeStyleboxOverride("normal", RowBox(new Color(1, 1, 1, 0.035f), new Color(1, 1, 1, 0.09f)));
		enter.AddThemeStyleboxOverride("hover", RowBox(new Color(accent, 0.1f), new Color(accent, 0.65f)));
		enter.AddThemeStyleboxOverride("pressed", RowBox(new Color(accent, 0.16f), accent));
		enter.AddThemeStyleboxOverride("hover_pressed", RowBox(new Color(accent, 0.16f), accent));
		enter.AddThemeStyleboxOverride("focus", RowBox(Colors.Transparent, new Color(accent, 0.65f)));
		enter.AddThemeStyleboxOverride("disabled", RowBox(new Color(1, 1, 1, 0.02f), new Color(1, 1, 1, 0.05f)));

		MarginContainer pad = UiKit.Margin(UiTokens.S3, 0, UiTokens.S2, 0);
		pad.MouseFilter = MouseFilterEnum.Ignore;
		UiKit.Fill(pad);
		enter.AddChild(pad);
		HBoxContainer inside = UiKit.Row(UiTokens.S3);
		inside.MouseFilter = MouseFilterEnum.Ignore;
		pad.AddChild(inside);

		// A inicial num círculo com o fio dourado.
		var avatar = new PanelContainer
		{
			CustomMinimumSize = new Vector2(38, 38),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		var round = new StyleBoxFlat { BgColor = new Color(accent, 0.14f), BorderColor = new Color(accent, 0.8f) };
		round.SetBorderWidthAll(2);
		round.SetCornerRadiusAll(19);
		avatar.AddThemeStyleboxOverride("panel", round);
		Label initial = UiKit.Text(shown.Substring(0, 1).ToUpperInvariant(), "Subtitle", accent);
		initial.HorizontalAlignment = HorizontalAlignment.Center;
		initial.VerticalAlignment = VerticalAlignment.Center;
		initial.MouseFilter = MouseFilterEnum.Ignore;
		avatar.AddChild(initial);
		inside.AddChild(avatar);

		Label name = UiKit.Truncated(shown, "Subtitle");
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		name.MouseFilter = MouseFilterEnum.Ignore;
		inside.AddChild(name);

		Control go = Icon.Make(IconKind.Go, 18, UiTokens.TextSecondary);
		go.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		go.MouseFilter = MouseFilterEnum.Ignore;
		inside.AddChild(go);

		Button forget = UiKit.IconButton(IconKind.Close, "Esquecer este acesso neste computador", () =>
		{
			Backend.ForgetSaved(access);
			Refresh();
		}, 16);
		forget.Disabled = _busy;
		forget.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		inside.AddChild(forget);
		return enter;
	}

	private static StyleBoxFlat RowBox(Color bg, Color border)
	{
		var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
		box.SetBorderWidthAll(1);
		box.SetCornerRadiusAll(UiTokens.RadiusPanel);
		return box;
	}

	private async void EnterSaved(Backend.SavedAccess access)
	{
		if (_busy) return;
		_busy = true;
		_status = Loc.F("Entrando como {0}…", access.Name);
		Refresh();

		Backend.Reply reply = await Backend.EnterSaved(access);
		_busy = false;
		if (!IsInstanceValid(this) || !IsInsideTree()) return;

		if (reply.Ok)
		{
			Entered();
			return;
		}

		_name = access.Name;
		_register = false;
		_status = reply.Status == 401
			? Loc.F("O acesso salvo de {0} venceu. Digite a senha para entrar.", access.Name)
			: Problem(reply);
		Sfx.Ui("ui_negado");
		Refresh();
	}

	// ------------------------------------------------------------------ entrar / criar conta

	private Control FormCard()
	{
		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S5);
		card.CustomMinimumSize = new Vector2(420, 0);
		card.SizeFlagsVertical = SizeFlags.ShrinkBegin;

		column.AddChild(Tabs.Make(new[] { "Entrar", "Criar conta" }, _register ? 1 : 0, index =>
		{
			_register = index == 1;
			_status = "";
			Keep();
			Refresh();
		}));

		if (_register)
			column.AddChild(UiKit.Paragraph("A conta guarda o seu Cash, os seus itens, as missões, a patente e o seu lugar no ranking. O nome é o que aparece no placar.", "Caption"));

		_nameField = Entry(_name, "3 a 16 caracteres", Profile.NameMax);
		_password = Entry("", _register ? "8 ou mais caracteres" : "", 64);
		_password.Secret = true;
		column.AddChild(Labeled("NOME DA CONTA", _nameField));
		column.AddChild(Labeled("SENHA", _password));
		if (_register)
		{
			_confirm = Entry("", "a mesma senha", 64);
			_confirm.Secret = true;
			column.AddChild(Labeled("REPITA A SENHA", _confirm));
			_confirm.TextSubmitted += _ => Submit();
		}
		else _confirm = null;

		column.AddChild(UiKit.Check("Lembrar acesso neste computador", _keep, on => _keep = on));

		Label status = UiKit.Paragraph(_status.Length > 0 ? _status : " ", null);
		status.AddThemeColorOverride("font_color", _busy ? UiTokens.TextSecondary : UiTokens.Warning);
		status.CustomMinimumSize = new Vector2(360, 0);
		column.AddChild(status);

		Button go = UiKit.Button(_register ? "Criar conta" : "Entrar", ButtonKind.Primary, () => Submit());
		go.Disabled = _busy;
		go.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		go.CustomMinimumSize = new Vector2(0, 52);
		go.AddThemeFontSizeOverride("font_size", UiTokens.Subtitle);
		_go = go;
		column.AddChild(go);

		_password.TextSubmitted += _ => { if (_confirm == null) Submit(); else _confirm.GrabFocus(); };
		_nameField.TextSubmitted += _ => _password.GrabFocus();
		return card;
	}

	private Button _go;

	private static LineEdit Entry(string text, string placeholder, int max)
	{
		LineEdit field = UiKit.Field(text, placeholder, max);
		field.CustomMinimumSize = new Vector2(0, 44);
		field.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return field;
	}

	/// <summary>O rótulo pequeno, em caixa alta, em cima do campo.</summary>
	private static Control Labeled(string label, Control field)
	{
		VBoxContainer box = UiKit.Column(UiTokens.S1);
		box.AddChild(Heading(label));
		box.AddChild(field);
		return box;
	}

	private static Label Heading(string text)
	{
		Label label = UiKit.Caption(text, UiTokens.TextSecondary);
		label.AddThemeFontOverride("font", new FontVariation { BaseFont = UiFonts.Label, SpacingGlyph = 2 });
		return label;
	}

	/// <summary>Guarda o que já foi digitado (a tela se refaz a cada mudança).</summary>
	private void Keep()
	{
		if (_nameField != null && IsInstanceValid(_nameField)) _name = _nameField.Text;
	}

	private async void Submit()
	{
		if (_busy) return;
		Keep();
		string name = _name.Trim(), password = _password.Text;

		string problem = name.Length == 0 ? "Digite o nome da conta."
			: _register ? Profile.ValidateName(name) : null;
		if (problem == null && password.Length == 0) problem = "Digite a senha.";
		if (problem == null && _register && password.Length < 8) problem = "A senha precisa de 8 ou mais caracteres.";
		if (problem == null && _register && _confirm?.Text != password) problem = "As duas senhas não são iguais.";
		if (problem != null)
		{
			_status = problem;
			Sfx.Ui("ui_negado");
			Refresh();
			return;
		}

		// As faíscas saem do botão.
		if (_go != null && IsInstanceValid(_go))
			LoginBackdrop.Burst(_go.GlobalPosition + _go.Size / 2f - GlobalPosition, Size);

		_busy = true;
		_status = "Falando com o servidor…";
		Refresh();

		Backend.KeepAccess = _keep;
		Backend.Reply reply = _register ? await Backend.Register(name, password) : await Backend.Login(name, password);
		_busy = false;
		if (!IsInstanceValid(this) || !IsInsideTree()) return;

		if (reply.Ok)
		{
			Entered();
			return;
		}

		if (reply.Status == 0) _online = false;
		_status = Problem(reply);
		Sfx.Ui("ui_negado");
		Refresh();
	}

	private static string Problem(Backend.Reply reply) => reply.Status == 0
		? "O servidor não respondeu. Confira a internet e tente de novo."
		: reply.Error;

	/// <summary>Entrou: a conta vem do servidor e o jogo abre nas salas.</summary>
	private void Entered()
	{
		Sfx.Ui(Sfx.First("ui_confirmar", "abate"));
		Shell.GoOnline();
	}

	// ------------------------------------------------------------------ embaixo

	private bool _asked;

	/// <summary>Pergunta uma vez se o servidor está no ar (o ponto embaixo).</summary>
	private async void Ask()
	{
		_asked = true;
		Backend.Reply reply = await Backend.Health();
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		_online = reply.Ok;
		// Só a barra de baixo: refazer a tela apagaria a senha que se digita.
		if (_bottom != null && IsInstanceValid(_bottom))
		{
			_bottom.QueueFree();
			AddChild(_bottom = BottomBar());
		}
	}

	private Control _bottom;

	private Control BottomBar()
	{
		MarginContainer margin = UiKit.Margin(UiTokens.S5, 0, UiTokens.S5, UiTokens.S4);
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		margin.GrowVertical = GrowDirection.Begin;

		HBoxContainer bar = UiKit.Row(UiTokens.S3);
		margin.AddChild(bar);

		// O servidor: no ar, fora, ou ainda perguntando.
		HBoxContainer server = UiKit.Row(UiTokens.S2);
		Color dot = _online switch { true => UiTokens.Success, false => UiTokens.Danger, _ => UiTokens.TextSecondary };
		var light = new Panel { CustomMinimumSize = new Vector2(10, 10), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		light.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = dot, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 });
		server.AddChild(light);
		if (_online == null) Motion.Pulse(light);
		server.AddChild(UiKit.Caption(_online switch
		{
			true => "Servidor no ar",
			false => "Servidor fora do ar",
			_ => "Procurando o servidor…"
		}));
		bar.AddChild(server);

		bar.AddChild(UiKit.Spacer());

		Button quit = UiKit.Button("Sair do jogo", ButtonKind.Tertiary, () => GetTree().Quit());
		bar.AddChild(quit);
		return margin;
	}
}
