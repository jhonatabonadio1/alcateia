using Godot;
using System;

/// <summary>
/// A primeira tela do jogo (pedido do Jhon, 20/11): o login. Toda abertura
/// passa por aqui — o acesso pode estar salvo, mas entrar é um clique na
/// conta, nunca sozinho.
///
/// À esquerda, as contas salvas neste computador: clicou, entra (o login
/// guardado é conferido no servidor; se não vale mais, a conta sai da
/// lista e o nome vai para o formulário, à espera da senha). À direita,
/// entrar com nome e senha, ou criar uma conta nova. "Lembrar acesso"
/// guarda o login desta conta para o próximo clique.
///
/// Sem conta não há progressão (decisão 0015: a carteira, os itens e a
/// patente são da conta); o treino com bots fica num botão discreto
/// embaixo, e não rende nada.
/// </summary>
public partial class LoginScreen : LobbyScreen
{
	private bool _register;
	private bool _busy;
	private string _status = "";
	private string _name = "";
	private string _server = "";
	private bool _keep = true;
	private LineEdit _nameField, _password, _confirm, _serverField;

	public LoginScreen(string notice = null)
	{
		_status = notice ?? "";
	}

	protected override void Build()
	{
		if (_server.Length == 0) _server = Backend.Url;
		_keep = Backend.KeepAccess;

		var center = UiKit.Fill(new CenterContainer());
		AddChild(center);

		VBoxContainer page = UiKit.Column(UiTokens.S4);
		center.AddChild(page);

		HBoxContainer brand = UiKit.Row(UiTokens.S3);
		brand.AddChild(Icon.Make(IconKind.Wolf, 48, UiTokens.Accent));
		brand.AddChild(UiKit.Text(Credits.GameName.ToUpperInvariant(), "Display"));
		page.AddChild(brand);

		HBoxContainer row = UiKit.Row(UiTokens.S4);
		page.AddChild(row);
		if (Backend.Saved.Count > 0) row.AddChild(SavedCard());
		row.AddChild(FormCard());

		// O treino com bots não precisa de conta (decisão de 13/11; não
		// rende nada, decisão 0015).
		HBoxContainer offline = UiKit.Row(UiTokens.S2);
		offline.AddChild(UiKit.Spacer());
		Button train = UiKit.Button("Treinar sem conta", ButtonKind.Tertiary, () =>
			Shell.GoRoom(RoomDirectory.Current ?? RoomDirectory.Create(new RoomSettings { Title = "Treino" })));
		train.TooltipText = "Uma sala com bots, neste computador. Sem conta, a partida não rende XP nem Presas.";
		offline.AddChild(train);
		page.AddChild(offline);

		(_nameField.Text.Length == 0 ? _nameField : _password).CallDeferred(Control.MethodName.GrabFocus);
	}

	// ------------------------------------------------------------------ contas salvas

	private Control SavedCard()
	{
		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S4);
		card.CustomMinimumSize = new Vector2(380, 0);
		card.SizeFlagsVertical = SizeFlags.ShrinkBegin;

		column.AddChild(UiKit.Text("Contas salvas", "Subtitle"));
		column.AddChild(UiKit.Paragraph("Clique na conta para entrar.", "Caption"));

		foreach (Backend.SavedAccess access in Backend.Saved)
			column.AddChild(AccountLine(access));
		return card;
	}

	private Control AccountLine(Backend.SavedAccess access)
	{
		HBoxContainer line = UiKit.Row(UiTokens.S2);

		// A conta inteira é o botão: nome grande, servidor embaixo.
		Button enter = UiKit.Button("", ButtonKind.Secondary, () => EnterSaved(access));
		enter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		enter.CustomMinimumSize = new Vector2(0, 60);
		enter.Disabled = _busy;
		enter.TooltipText = Loc.F("Entrar como {0}", access.Name);

		HBoxContainer inside = UiKit.Row(UiTokens.S3);
		inside.MouseFilter = MouseFilterEnum.Ignore;
		UiKit.Fill(inside);
		inside.AddChild(UiKit.Gap(UiTokens.S2));
		Control icon = Icon.Make(IconKind.Profile, 28, UiTokens.Accent);
		icon.MouseFilter = MouseFilterEnum.Ignore;
		icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		inside.AddChild(icon);

		VBoxContainer text = UiKit.Column(0);
		text.MouseFilter = MouseFilterEnum.Ignore;
		text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		Label name = UiKit.Text(access.Name.Length > 0 ? access.Name : "Conta", "Subtitle");
		name.MouseFilter = MouseFilterEnum.Ignore;
		text.AddChild(name);
		Label where = UiKit.Caption(ServerLabel(access.Url));
		where.MouseFilter = MouseFilterEnum.Ignore;
		text.AddChild(where);
		inside.AddChild(text);
		enter.AddChild(inside);
		line.AddChild(enter);

		Button forget = UiKit.IconButton(IconKind.Close, "Esquecer este acesso neste computador", () =>
		{
			Backend.ForgetSaved(access);
			Refresh();
		});
		forget.Disabled = _busy;
		forget.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		line.AddChild(forget);
		return line;
	}

	private static string ServerLabel(string url)
	{
		string shown = url.Replace("https://", "").Replace("http://", "");
		return url.TrimEnd('/') == Backend.DefaultUrl ? Loc.F("{0} (neste computador)", shown) : shown;
	}

	private async void EnterSaved(Backend.SavedAccess access)
	{
		if (_busy) return;
		_busy = true;
		_status = Loc.F("Entrando como {0}…", access.Name);
		Refresh();

		Backend.Reply reply = await Backend.EnterSaved(access);
		_busy = false;
		if (!IsInsideTree()) return;

		if (reply.Ok)
		{
			Entered();
			return;
		}

		_name = access.Name;
		_server = access.Url;
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
		PanelContainer card = UiKit.Card(out VBoxContainer column, UiTokens.S4);
		card.CustomMinimumSize = new Vector2(440, 0);

		column.AddChild(Tabs.Make(new[] { "Entrar", "Criar conta" }, _register ? 1 : 0, index =>
		{
			_register = index == 1;
			_status = "";
			Keep();
			Refresh();
		}));

		column.AddChild(UiKit.Paragraph(_register
			? "A conta guarda as suas Presas, os seus itens, as missões, a patente e o seu lugar no ranking. O nome é o que aparece no placar."
			: "Entre com o nome e a senha da sua conta.", "Caption"));

		_nameField = UiKit.Field(_name, "3 a 16 caracteres", Profile.NameMax);
		_password = UiKit.Field("", _register ? "8 ou mais caracteres" : "", 64);
		_password.Secret = true;
		_serverField = UiKit.Field(_server, Backend.DefaultUrl, 120);
		column.AddChild(UiKit.FormRow("Nome", _nameField, 110));
		column.AddChild(UiKit.FormRow("Senha", _password, 110));
		if (_register)
		{
			_confirm = UiKit.Field("", "a mesma senha", 64);
			_confirm.Secret = true;
			column.AddChild(UiKit.FormRow("Repita a senha", _confirm, 110));
			_confirm.TextSubmitted += _ => Submit();
		}
		else _confirm = null;
		column.AddChild(UiKit.FormRow("Servidor", _serverField, 110));

		column.AddChild(UiKit.Check("Lembrar acesso neste computador", _keep, on => _keep = on));

		Label status = UiKit.Paragraph(_status.Length > 0 ? _status : " ", null);
		status.AddThemeColorOverride("font_color", _busy ? UiTokens.TextSecondary : UiTokens.Warning);
		status.CustomMinimumSize = new Vector2(400, 0);
		column.AddChild(status);

		HBoxContainer buttons = UiKit.Row(UiTokens.S2);
		buttons.AddChild(UiKit.Spacer());
		Button go = UiKit.Button(_register ? "Criar conta" : "Entrar", ButtonKind.Primary, Submit);
		go.Disabled = _busy;
		go.CustomMinimumSize = new Vector2(160, UiTokens.ButtonHeight);
		buttons.AddChild(go);
		column.AddChild(buttons);

		_password.TextSubmitted += _ => { if (_confirm == null) Submit(); else _confirm.GrabFocus(); };
		_nameField.TextSubmitted += _ => _password.GrabFocus();
		return card;
	}

	/// <summary>Guarda o que já foi digitado (a tela se refaz a cada mudança).</summary>
	private void Keep()
	{
		if (_nameField != null && IsInstanceValid(_nameField)) _name = _nameField.Text;
		if (_serverField != null && IsInstanceValid(_serverField)) _server = _serverField.Text;
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

		_busy = true;
		_status = "Falando com o servidor…";
		Refresh();

		Backend.Url = _server;
		Backend.KeepAccess = _keep;
		Backend.Reply reply = _register ? await Backend.Register(name, password) : await Backend.Login(name, password);
		_busy = false;
		if (!IsInsideTree()) return;

		if (reply.Ok)
		{
			Entered();
			return;
		}

		_status = Problem(reply);
		Sfx.Ui("ui_negado");
		Refresh();
	}

	private static string Problem(Backend.Reply reply) => reply.Status == 0
		? Loc.F("Não achei o servidor em {0}. Ele está no ar?", Backend.Url)
		: reply.Error;

	/// <summary>Entrou: a conta vem do servidor e o jogo abre nas salas.</summary>
	private void Entered()
	{
		Sfx.Ui(Sfx.First("ui_confirmar", "abate"));
		Shell.GoOnline();
	}
}
