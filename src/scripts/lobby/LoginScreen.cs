using Godot;
using System;
using System.Linq;

/// <summary>
/// A primeira tela do jogo (pedido do Jhon, 20/11): o login. Toda abertura
/// passa por aqui — o acesso pode estar salvo, mas entrar é um clique na
/// conta, nunca sozinho.
///
/// Refeita em 27/11 (Jhon: "refaça a HUD dos menus, inclusive a de login,
/// com Valorant de inspiração"): a tela se divide em duas. **À esquerda, o
/// painel de acesso**, uma placa escura de alto a baixo com a marca, as
/// abas Entrar e Criar conta, os campos, o botão de papel e, embaixo, as
/// contas salvas neste computador: clicou, entra (o login guardado é
/// conferido no servidor; se não vale mais, a conta sai da lista e o nome
/// vai para o formulário, à espera da senha). **À direita, os mapas**,
/// passando devagar atrás de um corte na diagonal (<see cref="MapReel"/>),
/// por cima do fundo vivo da moldura (<see cref="LoginBackdrop"/>). No pé,
/// se o servidor está no ar e sair do jogo. Sem conta não se joga: o
/// "treinar sem conta" saiu (Jhon, 20/11).
///
/// O endereço do servidor é fixo no jogo por enquanto (Jhon, 20/11): a
/// tela não pergunta.
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

	// A verificação em duas etapas (27/11, opcional, ligada pelo cliente de
	// instalação): a senha estava certa e o servidor pediu o código do
	// aplicativo autenticador (erro "codigo_necessario"). Aparece o campo do
	// código, e a senha fica guardada enquanto ele é pedido — a tela se refaz
	// a cada tentativa, e redigitar a senha a cada código errado não ajuda.
	private bool _needCode;
	private string _heldPassword = "";
	private LineEdit _codeField;

	/// <summary>O servidor respondeu? (null: ainda perguntando).</summary>
	private bool? _online;

	public LoginScreen(string notice = null)
	{
		_status = notice ?? "";
	}

	/// <summary>Largura do painel de acesso.</summary>
	private const int PanelWidth = 500;

	protected override void Build()
	{
		_keep = Backend.KeepAccess;
		if (_online == null && !_asked) Ask();

		// Aberto pelo cliente de instalação, com a conta já entrada lá: entra
		// com ela, sem pedir nada (uma vez; Backend.TakeLauncherAccess).
		if (!_busy && !Backend.LoggedIn && Backend.TakeLauncherAccess() is Backend.SavedAccess fromLauncher)
			Callable.From(() => EnterSaved(fromLauncher)).CallDeferred();

		// Os mapas, à direita do painel.
		var reel = new MapReel();
		reel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		reel.OffsetLeft = PanelWidth - 120;
		AddChild(reel);

		// O painel: de alto a baixo, com o fio da marca na beira.
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0) };
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
		var box = new StyleBoxFlat { BgColor = new Color(0.02f, 0.032f, 0.05f, 0.9f), BorderColor = new Color(UiTokens.Accent, 0.85f) };
		box.BorderWidthRight = 2;
		box.ShadowColor = new Color(0, 0, 0, 0.5f);
		box.ShadowSize = 40;
		box.ContentMarginLeft = box.ContentMarginRight = UiTokens.S6;
		box.ContentMarginTop = UiTokens.S6;
		box.ContentMarginBottom = UiTokens.S4;
		panel.AddThemeStyleboxOverride("panel", box);
		AddChild(panel);

		VBoxContainer column = UiKit.Column(UiTokens.S3);
		panel.AddChild(column);

		column.AddChild(Brand());
		column.AddChild(UiKit.Gap(UiTokens.S3));
		Form(column);
		if (Backend.Saved.Count > 0) Saved(column);
		column.AddChild(UiKit.Spacer());
		column.AddChild(_bottom = BottomBar());

		// A entrada (só na primeira vez; a tela se refaz a cada mudança e não
		// pisca de novo): o painel vem da esquerda, os mapas aparecem.
		if (!_shown)
		{
			_shown = true;
			// Pelas margens, não pela posição: o painel é ancorado de alto a
			// baixo, e mexer na posição antes do layout o deixava 72 px mais
			// alto que a tela (o rodapé caía para fora).
			panel.OffsetLeft = -80;
			panel.OffsetRight = PanelWidth - 80;
			panel.Modulate = new Color(1, 1, 1, 0);
			Tween slide = panel.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
			slide.TweenProperty(panel, "offset_left", 0f, 0.55f).SetDelay(0.05f);
			slide.TweenProperty(panel, "offset_right", (float)PanelWidth, 0.55f).SetDelay(0.05f);
			slide.TweenProperty(panel, "modulate:a", 1f, 0.4f).SetDelay(0.05f);
			Motion.FadeIn(reel, 1.2f, 0.2f);
			Motion.Cascade(column, 0.25f, 0.04f, new Vector2(-18, 0));
		}

		// No quadro seguinte a tela pode já ter saído (entrou com a conta salva,
		// trocou de aba): foco em campo fora da árvore escrevia erro no Output.
		LineEdit focus = _needCode && _codeField != null ? _codeField : _nameField.Text.Length == 0 ? _nameField : _password;
		Callable.From(() => { if (IsInstanceValid(focus) && focus.IsInsideTree() && !_busy) focus.GrabFocus(); }).CallDeferred();
	}

	// ------------------------------------------------------------------ a marca

	private static Control Brand()
	{
		VBoxContainer brand = UiKit.Column(UiTokens.S3);

		// A logo de sempre, a mesma do site e do instalador (Jhon, 21/11:
		// "deixe padrão a logo"; sem o "humanos contra lobos" embaixo).
		var logo = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://src/assets/ui/marca/wolfleague_logo_horizontal.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			CustomMinimumSize = new Vector2(0, 52),
			MouseFilter = MouseFilterEnum.Ignore
		};
		brand.AddChild(logo);
		brand.AddChild(Deco.Hairline(new Color(UiTokens.Accent, 0.8f), 2));
		return brand;
	}

	// ------------------------------------------------------------------ contas salvas

	private void Saved(VBoxContainer column)
	{
		column.AddChild(UiKit.Gap(UiTokens.S2));
		HBoxContainer head = UiKit.Row(UiTokens.S2);
		head.AddChild(Deco.Kicker("Contas salvas", UiTokens.TextSecondary));
		Control line = Deco.Hairline(new Color(UiTokens.Paper, 0.14f));
		line.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		head.AddChild(line);
		column.AddChild(head);
		foreach (Backend.SavedAccess access in Backend.Saved)
			column.AddChild(AccountLine(access));
	}

	/// <summary>Uma conta salva: a linha inteira é o botão (a inicial num
	/// quadro, o nome, a seta de entrar); o X, dentro dela, esquece.</summary>
	private Control AccountLine(Backend.SavedAccess access)
	{
		string shown = access.Name.Length > 0 ? access.Name : "Conta";

		Button enter = UiKit.Button("", ButtonKind.Secondary, () => EnterSaved(access));
		enter.SetMeta("conta", access.Name);
		enter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		enter.CustomMinimumSize = new Vector2(0, 56);
		enter.Disabled = _busy;
		enter.MouseDefaultCursorShape = CursorShape.PointingHand;
		Color accent = UiTokens.Accent;
		enter.AddThemeStyleboxOverride("normal", RowBox(new Color(UiTokens.Paper, 0.04f), new Color(UiTokens.Paper, 0.12f)));
		enter.AddThemeStyleboxOverride("hover", RowBox(new Color(UiTokens.Paper, 0.1f), UiTokens.Paper));
		enter.AddThemeStyleboxOverride("pressed", RowBox(new Color(accent, 0.16f), accent));
		enter.AddThemeStyleboxOverride("hover_pressed", RowBox(new Color(accent, 0.16f), accent));
		enter.AddThemeStyleboxOverride("focus", RowBox(Colors.Transparent, new Color(accent, 0.65f)));
		enter.AddThemeStyleboxOverride("disabled", RowBox(new Color(1, 1, 1, 0.02f), new Color(1, 1, 1, 0.05f)));

		MarginContainer pad = UiKit.Margin(UiTokens.S2, 0, UiTokens.S2, 0);
		pad.MouseFilter = MouseFilterEnum.Ignore;
		UiKit.Fill(pad);
		enter.AddChild(pad);
		HBoxContainer inside = UiKit.Row(UiTokens.S3);
		inside.MouseFilter = MouseFilterEnum.Ignore;
		pad.AddChild(inside);

		// A inicial num quadro com o fio dourado.
		var avatar = new PanelContainer
		{
			CustomMinimumSize = new Vector2(40, 40),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore
		};
		var square = new StyleBoxFlat { BgColor = new Color(accent, 0.14f), BorderColor = new Color(accent, 0.8f) };
		square.SetBorderWidthAll(1);
		avatar.AddThemeStyleboxOverride("panel", square);
		Label initial = UiKit.Text(shown.Substring(0, 1).ToUpperInvariant(), "Subtitle", accent);
		initial.AddThemeFontOverride("font", UiFonts.Display);
		initial.AddThemeFontSizeOverride("font_size", 24);
		initial.HorizontalAlignment = HorizontalAlignment.Center;
		initial.VerticalAlignment = VerticalAlignment.Center;
		avatar.AddChild(initial);
		inside.AddChild(avatar);

		Label name = UiKit.Truncated(shown, "Subtitle");
		name.AddThemeFontOverride("font", UiFonts.DisplaySemi);
		name.AddThemeFontSizeOverride("font_size", 22);
		name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		inside.AddChild(name);

		Control go = Icon.Make(IconKind.Go, 18, UiTokens.TextSecondary);
		go.SizeFlagsVertical = SizeFlags.ShrinkCenter;
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

	private void Form(VBoxContainer column)
	{
		column.AddChild(Tabs.Make(new[] { "Entrar", "Criar conta" }, _register ? 1 : 0, index =>
		{
			_register = index == 1;
			_status = "";
			ForgetCode();
			Keep();
			Refresh();
		}));

		if (_register)
			column.AddChild(UiKit.Paragraph("A conta guarda o seu Cash, os seus itens, as missões, a patente e os seus amigos. O nome é o que aparece no placar.", "Caption"));

		_nameField = Entry(_name, "3 a 16 caracteres", Profile.NameMax);
		_password = Entry(_needCode ? _heldPassword : "", _register ? "8 ou mais caracteres" : "", 64);
		_password.Secret = true;
		column.AddChild(Labeled("NOME DA CONTA", _nameField));
		column.AddChild(Labeled("SENHA", _password));
		_codeField = null;
		if (_needCode && !_register)
		{
			_codeField = Entry("", "6 dígitos", 6);
			_codeField.TextSubmitted += _ => Submit();
			column.AddChild(Labeled("CÓDIGO DO AUTENTICADOR", _codeField));
		}
		if (_register)
		{
			_confirm = Entry("", "a mesma senha", 64);
			_confirm.Secret = true;
			column.AddChild(Labeled("REPITA A SENHA", _confirm));
			_confirm.TextSubmitted += _ => Submit();
		}
		else _confirm = null;

		column.AddChild(UiKit.Check("Lembrar acesso neste computador", _keep, on => _keep = on));

		// Falando com o servidor: o carregador gira ao lado do recado.
		HBoxContainer statusRow = UiKit.Row(UiTokens.S2);
		statusRow.CustomMinimumSize = new Vector2(0, 40);
		if (_busy) statusRow.AddChild(Loader.Make(22));
		Label status = UiKit.Paragraph(_status.Length > 0 ? _status : " ", null);
		status.AddThemeColorOverride("font_color", _busy ? UiTokens.TextSecondary : UiTokens.Warning);
		status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		statusRow.AddChild(status);
		column.AddChild(statusRow);

		CtaButton go = CtaButton.Make(_register ? "Criar conta" : "Entrar", CtaKind.Paper, () => Submit(), 58, 26);
		go.Arrow = true;
		go.Disabled = _busy;
		go.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_go = go;
		column.AddChild(go);

		_password.TextSubmitted += _ =>
		{
			if (_confirm != null) _confirm.GrabFocus();
			else if (_codeField != null) _codeField.GrabFocus();
			else Submit();
		};
		_nameField.TextSubmitted += _ => _password.GrabFocus();
	}

	private Button _go;

	private static LineEdit Entry(string text, string placeholder, int max)
	{
		LineEdit field = UiKit.Field(text, placeholder, max);
		field.CustomMinimumSize = new Vector2(0, 46);
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
		label.AddThemeFontSizeOverride("font_size", 12);
		return label;
	}

	/// <summary>Esquece o pedido de código e a senha guardada para ele.</summary>
	private void ForgetCode()
	{
		_needCode = false;
		_heldPassword = "";
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
		string code = _needCode && !_register && _codeField != null ? _codeField.Text.Trim() : null;

		string problem = name.Length == 0 ? "Digite o nome da conta."
			: _register ? Profile.ValidateName(name) : null;
		if (problem == null && password.Length == 0) problem = "Digite a senha.";
		if (problem == null && _register && password.Length < 8) problem = "A senha precisa de 8 ou mais caracteres.";
		if (problem == null && _register && _confirm?.Text != password) problem = "As duas senhas não são iguais.";
		if (problem == null && code != null && (code.Length != 6 || !code.All(char.IsDigit))) problem = "Digite o código de 6 dígitos do aplicativo autenticador.";
		// A tela se refaz já: com o código pedido, a senha continua no campo.
		if (_needCode) _heldPassword = password;
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
		Backend.Reply reply = _register ? await Backend.Register(name, password) : await Backend.Login(name, password, code);
		_busy = false;
		if (!IsInstanceValid(this) || !IsInsideTree()) return;

		if (reply.Ok)
		{
			Entered();
			return;
		}

		if (reply.Status == 0) _online = false;
		// A senha passou e falta o código (ou ele veio errado): o campo do
		// código aparece (ou fica) e a senha continua. Qualquer outro erro
		// volta ao começo.
		if (reply.Code is "codigo_necessario" or "codigo_invalido")
		{
			_needCode = true;
			_heldPassword = password;
		}
		else if (reply.Status != 0) ForgetCode();
		_status = Problem(reply);
		// Pedir o código não é erro (a senha estava certa): sem o som de negado.
		Sfx.Ui(reply.Code == "codigo_necessario" ? "ui_abrir" : "ui_negado");
		Refresh();
	}

	private static string Problem(Backend.Reply reply) => reply.Status == 0
		? "O servidor não respondeu. Confira a internet e tente de novo."
		: reply.Error;

	/// <summary>Entrou: a cortina passa e o jogo abre no início (27/11: era
	/// nas salas). A ligação do saguão abre junto, para os amigos.</summary>
	private void Entered()
	{
		Sfx.Ui(Sfx.First("ui_confirmar", "abate"));
		ForgetCode();
		_ = Shell.EnsureLink();
		Shell.GoWithWipe(() => new HomeScreen());
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
			Node parent = _bottom.GetParent();
			_bottom.QueueFree();
			parent.AddChild(_bottom = BottomBar());
		}
	}

	private Control _bottom;

	private Control BottomBar()
	{
		MarginContainer margin = UiKit.Margin(0);

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
