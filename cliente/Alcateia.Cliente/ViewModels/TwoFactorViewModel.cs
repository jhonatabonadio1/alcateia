using System;
using System.Text;
using System.Threading.Tasks;
using Alcateia.Cliente.Core;
using Avalonia.Media;

namespace Alcateia.Cliente.ViewModels;

/// <summary>
/// Ligar ou desligar a verificação em duas etapas (Jhon, 27/11: "dê opção
/// para o player cadastrar 2 fatores").
///
/// **Ligar**: o servidor sorteia a chave; a tela mostra o QR code dela (e a
/// chave escrita, para quem não puder apontar a câmera) e pede o primeiro
/// código do aplicativo — só com ele a proteção passa a valer, o que garante
/// que o aplicativo ficou configurado antes de a conta depender dele.
///
/// **Desligar**: pede a senha e um código. Um login esquecido aberto num
/// computador não basta para tirar a proteção.
/// </summary>
public sealed class TwoFactorViewModel : Observable
{
	private readonly MainViewModel _main;
	private string _secret = "", _code = "", _password = "", _status = "";
	private bool _busy, _statusIsError, _loading;
	private Geometry _qr;

	public TwoFactorViewModel(MainViewModel main, bool enable)
	{
		_main = main;
		IsEnable = enable;
		Confirm = new Command(() => _ = Submit(), () => !Busy && !Loading);
		Close = new Command(() => _main.CloseTwoFactor(null));
		CopyKey = new Command(() => _ = Copy());
		if (enable && !main.PreviewOnly) _ = Start();
	}

	public bool IsEnable { get; }
	public bool IsDisable => !IsEnable;
	public string Title => Tr.T(IsEnable ? "2fa.title_on" : "2fa.title_off");
	public string ConfirmText => Tr.T(Busy ? "common.wait" : IsEnable ? "2fa.turn_on" : "2fa.turn_off");

	/// <summary>O QR code da chave, e a chave em grupos de quatro letras.</summary>
	public Geometry Qr { get => _qr; private set { Set(ref _qr, value); Raise(nameof(HasQr)); } }
	public bool HasQr => _qr != null;
	public string Secret { get => _secret; private set => Set(ref _secret, value); }

	public string Code { get => _code; set => Set(ref _code, value); }
	public string Password { get => _password; set => Set(ref _password, value); }

	public bool Loading { get => _loading; private set { Set(ref _loading, value); Confirm.Refresh(); } }
	public bool Busy
	{
		get => _busy;
		private set
		{
			if (!Set(ref _busy, value)) return;
			Raise(nameof(ConfirmText));
			Confirm.Refresh();
		}
	}

	public string Status { get => _status; private set { Set(ref _status, value); Raise(nameof(HasStatus)); } }
	public bool StatusIsError { get => _statusIsError; private set => Set(ref _statusIsError, value); }
	public bool HasStatus => _status.Length > 0;

	public Command Confirm { get; }
	public Command Close { get; }
	public Command CopyKey { get; }

	private void Say(string text, bool error)
	{
		StatusIsError = error;
		Status = text ?? "";
	}

	private async Task Start()
	{
		Loading = true;
		Say(Tr.T("2fa.asking"), false);
		Reply reply = await _main.Api.TwoFactorStart(_main.Account.Token);
		Loading = false;
		if (!reply.Ok)
		{
			Say(reply.Error, true);
			return;
		}
		Show(reply.Body?["secret"]?.GetValue<string>() ?? "", reply.Body?["uri"]?.GetValue<string>() ?? "");
		Say("", false);
	}

	/// <summary>Mostra a chave e o QR code dela (as fotos das telas chamam com uma de mentira).</summary>
	internal void Show(string secret, string uri)
	{
		var grouped = new StringBuilder();
		for (int i = 0; i < secret.Length; i++)
		{
			if (i > 0 && i % 4 == 0) grouped.Append(' ');
			grouped.Append(secret[i]);
		}
		_rawSecret = secret;
		Secret = grouped.ToString();
		try
		{
			Qr = StreamGeometry.Parse(QrCode.Encode(uri).ToPath(0));
		}
		catch (Exception e) when (e is ArgumentException or FormatException)
		{
			Qr = null;   // sem o desenho, fica a chave escrita
		}
	}

	private string _rawSecret = "";

	private async Task Copy()
	{
		if (_main.CopyText == null || _rawSecret.Length == 0) return;
		await _main.CopyText(_rawSecret);
		Say(Tr.T("2fa.copied"), false);
	}

	private async Task Submit()
	{
		string code = (Code ?? "").Replace(" ", "");
		if (code.Length != 6)
		{
			Say(Tr.T("2fa.code_digits"), true);
			return;
		}
		if (IsDisable && string.IsNullOrEmpty(Password))
		{
			Say(Tr.T("2fa.need_password"), true);
			return;
		}

		Busy = true;
		Say("", false);
		Reply reply = IsEnable
			? await _main.Api.TwoFactorConfirm(_main.Account.Token, code)
			: await _main.Api.TwoFactorDisable(_main.Account.Token, Password, code);
		Busy = false;

		if (reply.Ok) _main.CloseTwoFactor(IsEnable);
		else Say(reply.Error, true);
	}
}
