using Godot;

/// <summary>As moedas. Na tela, desde 24/11 (Jhon): <b>Cash</b> (era
/// Presas) e <b>IPs</b>, Inipoints (era Sangue); no código, no perfil e no
/// servidor os nomes de antes continuam (presas, sangue).</summary>
public enum Currency
{
	/// <summary>Cash: ganho jogando. Compra tudo que afeta o combate.</summary>
	Presas,
	/// <summary>IPs: comprados com dinheiro real. Só aparência e
	/// conveniência — e, sem servidor, não há como comprar.</summary>
	Sangue
}

/// <summary>
/// Saldo de uma moeda: ícone, número com separador de milhar e a sigla
/// de novo depois do número — com duas moedas na mesma faixa, a
/// redundância evita ler uma pela outra.
///
/// O número tem largura mínima fixa e algarismos tabulares, para o
/// header não "pular" quando o valor muda. Toda mudança conta de A até B
/// em 400 ms e pisca verde (ganho) ou vermelho (gasto).
/// </summary>
public partial class CurrencyLabel : HBoxContainer
{
	public Currency Kind { get; private set; }

	private Label _number;
	private long _shown;
	private long _value;
	private Tween _count;

	public static Color ColorOf(Currency kind) =>
		kind == Currency.Presas ? new Color("f2c14e") : new Color("a78bfa");

	// as cores e as siglas das moedas novas (24/11): o ouro do Cash e o violeta dos IPs, como os ícones
	public static string Sigla(Currency kind) => kind == Currency.Presas ? "C" : "IP";
	public static string NameOf(Currency kind) => kind == Currency.Presas ? "Cash" : "IPs";

	public static CurrencyLabel Make(Currency kind, long value, int numberWidth = 84, bool large = false)
	{
		var label = new CurrencyLabel { Kind = kind };
		label.AddThemeConstantOverride("separation", UiTokens.S1 + 2);
		label.MouseFilter = MouseFilterEnum.Pass;
		label.TooltipText = kind == Currency.Presas
			? "Cash — ganho jogando. Compra armas e melhorias."
			: "IPs (Inipoints) — moeda comprada. Disponível quando houver servidor.";

		label.AddChild(Icon.Make(kind == Currency.Presas ? IconKind.Presa : IconKind.Sangue, large ? 22 : 16, ColorOf(kind)));

		label._number = UiKit.Text("", large ? "NumericLarge" : "Numeric");
		label._number.HorizontalAlignment = HorizontalAlignment.Right;
		label._number.CustomMinimumSize = new Vector2(numberWidth, 0);
		label.AddChild(label._number);

		label.AddChild(UiKit.Text(Sigla(kind), "Caption", ColorOf(kind)));

		label._value = label._shown = value;
		label.Render(value);
		return label;
	}

	public long Value => _value;

	public void SetValue(long value, bool animate = true)
	{
		if (value == _value && !animate) return;

		long before = _shown;
		_value = value;
		_count?.Kill();

		if (!animate || !IsInsideTree())
		{
			_shown = value;
			Render(value);
			return;
		}

		if (value != before)
			Motion.Flash(_number, value > before ? UiTokens.Success : UiTokens.Danger, UiTokens.TextPrimary);

		_count = Motion.Count(this, before, value, v =>
		{
			_shown = (long)Mathf.Round(v);
			Render(_shown);
		});
	}

	private void Render(long value) => _number.Text = UiTokens.Thousands(value);
}
