using Godot;

/// <summary>
/// Uma tela do miolo do lobby. O shell troca o miolo; o header fica.
///
/// Cada tela monta a própria interface em <see cref="Build"/> e se
/// redesenha em <see cref="Refresh"/>. Mudança de estado → Refresh, e não
/// remendo em nó solto: a tela é pequena o bastante para ser refeita, e
/// refazer é o que mantém o que aparece igual ao que é.
/// </summary>
public abstract partial class LobbyScreen : Control
{
	/// <summary>A aba do header que acende com esta tela.</summary>
	public virtual IconKind NavIcon => IconKind.None;

	/// <summary>A tela precisa da largura toda (loja, inventário, opções):
	/// o painel social abre recolhido nela.</summary>
	public virtual bool Wide => false;

	/// <summary>O shell que hospeda a tela.</summary>
	protected LobbyShell Shell => LobbyShell.Instance;

	/// <summary>A tela mostra coisa da conta (carteira, itens, missões):
	/// refaz-se quando a conta chega, muda ou sai.</summary>
	protected virtual bool FollowsAccount => false;

	public override void _Ready()
	{
		UiKit.Fill(this);
		Build();
		if (!FollowsAccount) return;
		Profile.AccountChanged += Refresh;
		// Preço, promoção e vitrine do dia também vêm do servidor.
		ServerData.Changed += Refresh;
		_ = ServerData.Refresh();
	}

	public override void _ExitTree()
	{
		if (!FollowsAccount) return;
		Profile.AccountChanged -= Refresh;
		ServerData.Changed -= Refresh;
	}

	protected abstract void Build();

	/// <summary>Refaz o conteúdo. Adiado para o fim do quadro: pode ser
	/// chamado de dentro de um sinal de um nó que a reconstrução apaga.</summary>
	public void Refresh() => Callable.From(Rebuild).CallDeferred();

	private void Rebuild()
	{
		if (!IsInsideTree()) return;
		UiKit.Clear(this);
		Build();
	}

	/// <summary>Margem padrão do miolo, com uma coluna dentro.</summary>
	protected VBoxContainer Page(int separation = UiTokens.S3)
	{
		VBoxContainer column = UiKit.Column(separation);
		Margined().AddChild(column);
		return column;
	}

	/// <summary>Margem padrão do miolo, com colunas lado a lado dentro
	/// (loja, inventário, perfil).</summary>
	protected HBoxContainer SplitPage(int separation = UiTokens.S4)
	{
		HBoxContainer split = UiKit.Row(separation);
		Margined().AddChild(split);
		return split;
	}

	private MarginContainer Margined()
	{
		MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4));
		AddChild(margin);
		return margin;
	}

	/// <summary>Cabeçalho de tela: título e, opcionalmente, um texto
	/// pequeno à direita.</summary>
	protected HBoxContainer Heading(Control parent, string title, string aside = null)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S3);
		row.AddChild(UiKit.Text(title, "Title"));
		if (!string.IsNullOrEmpty(aside))
		{
			Label small = UiKit.Text(aside, "Muted");
			small.SizeFlagsVertical = SizeFlags.ShrinkEnd;
			row.AddChild(small);
		}
		row.AddChild(UiKit.Spacer());
		parent.AddChild(row);
		return row;
	}
}
