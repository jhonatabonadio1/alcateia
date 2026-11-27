using Godot;

/// <summary>
/// O fôlego da corrida na tela (Jhon, 27/11: "deve mostrar em tela também,
/// pensei em colocar o ícone de habilidade no lado direito"), no mesmo
/// quadradinho das habilidades do lobo (<see cref="SkillSquare"/>): o ícone de
/// correr com a tecla embaixo. Correndo, acende e a cor esvazia com o fôlego
/// (como o uivo enquanto se segura); sem fôlego, a cortina escura recua
/// enquanto ele enche, e "SEM FÔLEGO" aparece ao lado. Só do humano: de lobo
/// some (lá ficam as habilidades dele). Reage ao sinal do corpo
/// (<see cref="Player.BreathChanged"/>), não pergunta.
/// </summary>
public partial class Hud
{
	private Control _breathRow;
	private SkillSquare _breathSquare;
	private Label _breathLabel;

	private void BuildBreath(VBoxContainer side)
	{
		HBoxContainer row = UiKit.Row(UiTokens.S2);
		row.Alignment = BoxContainer.AlignmentMode.End;
		row.MouseFilter = Control.MouseFilterEnum.Ignore;

		_breathLabel = Outlined(UiKit.Caption(Loc.T("SEM FÔLEGO"), UiTokens.Danger));
		_breathLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		_breathLabel.Visible = false;
		row.AddChild(_breathLabel);

		_breathSquare = new SkillSquare { Kind = IconKind.Run, Key = KeyBindings.Short("sprint"), Hot = UiTokens.Accent };
		row.AddChild(_breathSquare);

		_breathRow = row;
		side.AddChild(row);

		if (_player != null) _player.BreathChanged += OnBreathChanged;
		else row.Visible = false;
		OnBreathChanged(_player?.Breath.Fraction ?? 1f, _player?.Breath.Winded ?? false, false);
	}

	private void OnBreathChanged(float fraction, bool winded, bool running)
	{
		if (_breathSquare == null) return;
		if (winded) _breathSquare.Set(fraction);                  // esperando: a cortina recua
		else _breathSquare.Set(1f, running ? Mathf.Max(fraction, 0.02f) : 0f);   // correndo: aceso, esvaziando
		_breathLabel.Visible = winded;
	}

	/// <summary>De lobo a corrida do humano não existe: o quadro some.</summary>
	private void ShowBreath(bool wolf)
	{
		if (_breathRow != null) _breathRow.Visible = !wolf && _player != null;
	}

	private void UnhookBreath()
	{
		if (_player != null && IsInstanceValid(_player)) _player.BreathChanged -= OnBreathChanged;
	}
}
