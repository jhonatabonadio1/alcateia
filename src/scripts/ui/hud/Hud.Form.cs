using Godot;
using System.Collections.Generic;

/// <summary>
/// A forma: o ícone de humano ou lobo (na insígnia ao lado da vida), a
/// troca de painel e a barra de transformação no inferior centro
/// (andamento, recusa e o porquê).
/// </summary>
public partial class Hud
{
	private Icon _formIcon;
	private HexBadge _formBadge;
	private Label _formLabel;
	private Label _shiftKey;
	private Label _shiftText;
	private Label _shiftState;
	private ProgressBar _shiftBar;
	private Tween _shiftPulse;
	private VBoxContainer _shiftPanel;

	private void OnFormChanged(int form) => OnFormChanged(form, animate: true);

	private void OnFormChanged(int form, bool animate)
	{
		bool wolf = _form.IsWolf;

		_formIcon.Kind = wolf ? IconKind.Wolf : IconKind.Human;
		_formIcon.Tint = wolf ? UiTokens.WolfOrange : UiTokens.TextPrimary;
		_formLabel.Text = (Form)form switch
		{
			Form.ShiftingToWolf => "VIRANDO LOBO",
			Form.ShiftingToHuman => "VOLTANDO A HUMANO",
			Form.Wolf => "LOBO",
			_ => "HUMANO"
		};
		_breedName.Text = "LOBO";
		
		// Dois painéis, um sai e o outro entra.
		VBoxContainer show = wolf ? _wolf : _human;
		VBoxContainer hide = wolf ? _human : _wolf;
		hide.Visible = false;
		if (!show.Visible || !animate)
		{
			show.Visible = true;
			if (animate) Motion.FadeIn(show, UiTokens.Normal);
		}

		if (!wolf) OnSlotChanged((int)_weapon.CurrentSlot);
	}

	private void UpdateTransform()
	{
		if (_shiftBar == null) return;

		bool blocked = _form.Rule != FormRule.Livre;

		// Modo sem lobo: nada de "virar lobo" na tela (pedido do Jhon, 18/11).
		_shiftPanel.Visible = _form.Rule != FormRule.Humano;
		if (!_shiftPanel.Visible) return;

		if (_form.IsShifting)
		{
			float total = Mathf.Max(_form.ShiftTotal, 0.01f);
			_shiftBar.Value = 1f - _form.ShiftLeft / total;
			_shiftText.Text = _form.Current == Form.ShiftingToWolf ? "TRANSFORMANDO" : "VOLTANDO";
			_shiftState.Text = Loc.F("{0:0.0} S  ·  VULNERÁVEL", _form.ShiftLeft);
			_shiftState.AddThemeColorOverride("font_color", UiTokens.Danger);
			// O momento de maior risco da partida pulsa.
			_shiftPulse ??= Motion.Pulse(_shiftPanel, 0.55f, 0.35f);
			return;
		}

		// O pulso fica no fim do tempo de lobo (abaixo); fora dele, para.
		bool wolfEnding = _form.Current == Form.Wolf && _form.WolfTimed && _form.WolfTimeLeft <= _form.WolfWarnAt;
		if (_shiftPulse != null && !wolfEnding)
		{
			_shiftPulse.Kill();
			_shiftPulse = null;
			_shiftPanel.Modulate = Colors.White;
		}

		_shiftBar.Value = blocked ? 0 : 1;

		if (_form.IsWolf && _form.Rule == FormRule.Lobo)
		{
			_shiftKey.Text = KeyBindings.Short("slot_3");
			_shiftText.Text = "LOBO";
			_shiftState.Text = "A PARTIDA TODA NESTE MODO";
			_shiftState.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
		}
		else if (_form.IsWolf)
		{
			_shiftKey.Text = KeyBindings.Short("slot_1");
			_shiftText.Text = "VOLTAR A HUMANO";
			if (_form.WolfTimed)
			{
				// O tempo de lobo (26/11): a barra esvazia, e nos últimos segundos fica vermelha e pulsa.
				float left = _form.WolfTimeLeft;
				bool ending = left <= _form.WolfWarnAt;
				_shiftBar.Value = left / Mathf.Max(_form.WolfTimeLimit, 0.01f);
				_shiftState.Text = ending ? Loc.F("HUMANO EM {0:0} S", Mathf.Ceil(left)) : Loc.F("LOBO · {0:0} S", Mathf.Ceil(left));
				_shiftState.AddThemeColorOverride("font_color", ending ? UiTokens.Danger : UiTokens.TextSecondary);
				if (ending) _shiftPulse ??= Motion.Pulse(_shiftPanel, 0.55f, 0.35f);
			}
			else
			{
				_shiftState.Text = Loc.F("OU {0} · {1} · {2}", KeyBindings.Short("slot_2"), KeyBindings.Short("slot_4"), KeyBindings.Short("slot_5"));
				_shiftState.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
			}
		}
		else if (!blocked && !_form.WolfReady)
		{
			// A espera depois de ser lobo: a barra enche até poder virar de novo.
			_shiftKey.Text = "3";
			_shiftText.Text = "VIRAR LOBO";
			_shiftBar.Value = 1f - _form.WolfCooldownLeft / Mathf.Max(_form.WolfCooldown, 0.01f);
			_shiftState.Text = Loc.F("ESPERE {0:0} S", Mathf.Ceil(_form.WolfCooldownLeft));
			_shiftState.AddThemeColorOverride("font_color", UiTokens.TextDisabled);
		}
		else
		{
			_shiftKey.Text = KeyBindings.Short("slot_3");
			_shiftText.Text = "VIRAR LOBO";
			_shiftState.Text = blocked ? "BLOQUEADO NESTE MODO" : "PRONTO";
			_shiftState.AddThemeColorOverride("font_color", blocked ? UiTokens.TextDisabled : UiTokens.Success);
		}

		UpdateWolfSlot();
	}

	/// <summary>O espaço 3 da barra de armas: na espera mostra os segundos
	/// que faltam ("12s"), apagado; pronto, o "3" de sempre.</summary>
	private void UpdateWolfSlot()
	{
		if (!_slots.TryGetValue(Slot.Wolf, out PanelContainer chip)) return;
		Label text = chip.GetChild<Label>(0);
		bool cooling = !_form.IsWolf && !_form.WolfReady;
		string want = cooling ? $"{Mathf.Ceil(_form.WolfCooldownLeft):0}s" : "3";
		if (text.Text != want) text.Text = want;
		chip.Modulate = cooling ? new Color(1, 1, 1, 0.45f) : Colors.White;
	}

	/// <summary>Faltam poucos segundos de lobo: avisa com texto e um bipe.</summary>
	private void OnWolfTimeWarning(float secondsLeft)
	{
		Warn(Loc.F("Você volta a humano em {0:0} s", Mathf.Ceil(secondsLeft)), 2f, UiTokens.Warning);
		Sfx.Local("contagem", -4f);
	}

	/// <summary>O tempo de lobo acabou: voltou a humano sozinho.</summary>
	private void OnWolfTimeUp()
	{
		Warn(Loc.F("O tempo de lobo acabou · lobo de novo em {0:0} s", _form.WolfCooldown), 2.4f, UiTokens.Warning);
	}

	/// <summary>Apertou 3 na espera: diz quanto falta.</summary>
	private void OnTransformCooling(float secondsLeft)
	{
		Warn(Loc.F("Lobo de novo em {0:0} s", Mathf.Ceil(secondsLeft)), 1.4f, UiTokens.TextSecondary);
		Sfx.Local("ui_negado", -6f);
	}

	private void OnTransformDenied(float missing)
	{
		if (missing < 0) Warn(_form.Rule == FormRule.Lobo ? "Neste modo você é lobo a partida toda" : "Lobo bloqueado neste modo", 1.6f, UiTokens.TextSecondary);
		else Warn(Loc.F("Faltam {0:0} KP para transformar", missing));
	}
}
