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

	/// <summary>O shell que hospeda a tela.</summary>
	protected LobbyShell Shell => LobbyShell.Instance;

	public override void _Ready()
	{
		UiKit.Fill(this);
		Build();
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
		MarginContainer margin = UiKit.Fill(UiKit.Margin(UiTokens.S5, UiTokens.S4, UiTokens.S5, UiTokens.S4));
		AddChild(margin);
		VBoxContainer column = UiKit.Column(separation);
		margin.AddChild(column);
		return column;
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
