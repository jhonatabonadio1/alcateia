using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;

namespace Alcateia.Cliente.ViewModels;

/// <summary>Uma conta salva, na tela de entrada: clicar entra, o X esquece.</summary>
public sealed class SavedAccountItem
{
	public SavedAccount Account { get; init; }
	public string Name => Account.Name;
	public string Tag => Account.Tag.Length == 0 ? "" : "#" + Account.Tag;
	public string Initial => Account.Name.Length > 0 ? Account.Name[..1].ToUpperInvariant() : "?";
	public Command Enter { get; init; }
	public Command Forget { get; init; }
}

/// <summary>
/// A entrada: entrar ou criar conta (as mesmas contas do jogo, no mesmo
/// servidor) e as contas salvas neste computador.
/// </summary>
public sealed class LoginViewModel : Observable
{
	private readonly MainViewModel _main;
	private string _name = "", _password = "", _confirm = "", _status = "", _code = "";
	private bool _register, _busy, _remember = true, _statusIsError, _needsCode;

	public LoginViewModel(MainViewModel main)
	{
		_main = main;
		Submit = new Command(() => _ = Enter(), () => !Busy);
		ShowLogin = new Command(() => IsRegister = false);
		ShowRegister = new Command(() => IsRegister = true);
		Reload();
	}

	public string Name { get => _name; set => Set(ref _name, value); }
	public string Password { get => _password; set => Set(ref _password, value); }
	public string Confirm { get => _confirm; set => Set(ref _confirm, value); }

	/// <summary>O código do aplicativo autenticador, e se a conta o pede (o
	/// campo só aparece depois que o servidor diz que precisa).</summary>
	public string Code { get => _code; set => Set(ref _code, value); }
	public bool NeedsCode { get => _needsCode; private set => Set(ref _needsCode, value); }
	public bool Remember { get => _remember; set => Set(ref _remember, value); }

	public bool IsRegister
	{
		get => _register;
		set
		{
			if (!Set(ref _register, value)) return;
			SetStatus("", false);
			NeedsCode = false;
			Raise(nameof(IsLogin));
			Raise(nameof(SubmitText));
		}
	}

	public bool IsLogin => !IsRegister;
	public string SubmitText => Tr.T(Busy ? "common.wait" : IsRegister ? "login.create" : "login.enter");

	public bool Busy
	{
		get => _busy;
		private set
		{
			if (!Set(ref _busy, value)) return;
			Raise(nameof(SubmitText));
			Submit.Refresh();
		}
	}

	public string Status { get => _status; private set { Set(ref _status, value); Raise(nameof(HasStatus)); } }
	public bool StatusIsError { get => _statusIsError; private set => Set(ref _statusIsError, value); }
	public bool HasStatus => Status.Length > 0;

	public ObservableCollection<SavedAccountItem> Saved { get; } = new();
	public bool HasSaved => Saved.Count > 0;
	public string ServerText => AppInfo.ServerOverride == null ? "" : Tr.F("login.server", AppInfo.ServerUrl);

	public Command Submit { get; }
	public Command ShowLogin { get; }
	public Command ShowRegister { get; }

	public void SetStatus(string text, bool error)
	{
		Status = text ?? "";
		StatusIsError = error;
	}

	/// <summary>Relê as contas salvas (entrou, saiu, esqueceu).</summary>
	public void Reload()
	{
		Saved.Clear();
		foreach (SavedAccount account in _main.Settings.Accounts)
		{
			SavedAccount captured = account;
			Saved.Add(new SavedAccountItem
			{
				Account = account,
				Enter = new Command(() => _ = EnterSaved(captured), () => !Busy),
				Forget = new Command(() =>
				{
					_main.Settings.Forget(captured.Url, captured.Id);
					Reload();
				})
			});
		}
		Raise(nameof(HasSaved));
	}

	private async Task Enter()
	{
		if (Busy) return;
		string name = (Name ?? "").Trim();
		if (name.Length == 0 || string.IsNullOrEmpty(Password))
		{
			SetStatus(Tr.T("login.need_both"), true);
			return;
		}
		if (IsRegister && Password != Confirm)
		{
			SetStatus(Tr.T("login.mismatch"), true);
			return;
		}

		Busy = true;
		SetStatus(Tr.T(IsRegister ? "login.creating" : "login.entering"), false);
		Reply reply = IsRegister ? await _main.Api.Register(name, Password) : await _main.Api.Login(name, Password, NeedsCode ? Code : null);
		Busy = false;

		if (!reply.Ok)
		{
			// A senha estava certa e a conta tem duas etapas: aparece o campo do código.
			if (reply.Code == "codigo_necessario")
			{
				NeedsCode = true;
				Code = "";
				SetStatus(reply.Error, false);
				return;
			}
			SetStatus(reply.Error, true);
			return;
		}
		Password = Confirm = Code = "";
		NeedsCode = false;
		SetStatus("", false);
		_main.Entered(Api.AccountOf(reply.Body["profile"] as System.Text.Json.Nodes.JsonObject, reply.Body["token"]?.GetValue<string>()), Remember);
	}

	/// <summary>Entra com um acesso salvo: confere no servidor se o login
	/// ainda vale. Venceu, o acesso sai da lista e a tela pede a senha.</summary>
	public async Task<bool> EnterSaved(SavedAccount saved, bool quiet = false)
	{
		if (Busy) return false;
		Busy = true;
		SetStatus(Tr.F("login.entering_as", saved.Name), false);
		Reply reply = await _main.Api.Profile(saved.Token);
		Busy = false;

		if (reply.Ok)
		{
			SetStatus("", false);
			_main.Entered(Api.AccountOf(reply.Body, saved.Token), true);
			return true;
		}
		if (reply.Status is 401 or 403)
		{
			_main.Settings.Forget(saved.Url, saved.Id);
			Reload();
			Name = saved.Name;
			IsRegister = false;
			SetStatus(reply.Status == 403 ? reply.Error : Tr.F("login.expired", saved.Name), true);
		}
		else SetStatus(quiet ? Tr.T("login.no_reply_saved") : reply.Error, true);
		return false;
	}
}
