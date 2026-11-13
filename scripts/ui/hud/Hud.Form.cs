using Godot;
using System.Collections.Generic;

/// <summary>
/// A forma: o ícone de humano ou lobo, a troca de painel e a barra de
/// transformação no inferior centro (andamento, recusa e o porquê).
/// </summary>
public partial class Hud
{
	private Icon _formIcon;
	private Label _formLabel;
	private Label _shiftKey;
	private Label _shiftText;
	private Label _shiftState;
	private ProgressBar _shiftBar;
	private Tween _shiftPulse;
	private PanelContainer _shiftPanel;

	private void OnFormChanged(int form) => OnFormChanged(form, animate: true);

	private void OnFormChanged(int form, bool animate)
	{
		bool wolf = _form.IsWolf;

		_formIcon.Kind = wolf ? IconKind.Wolf : IconKind.Human;
		_formIcon.Tint = wolf ? UiTokens.TeamOrange : UiTokens.TextPrimary;
		_formLabel.Text = (Form)form switch
		{
			Form.ShiftingToWolf => "VIRANDO LOBO",
			Form.ShiftingToHuman => "VOLTANDO A HUMANO",
			Form.Wolf => $"LOBO · {(_form.Breed?.DisplayName ?? "").ToUpperInvariant()}",
			_ => "HUMANO"
		};
		_breedName.Text = (_form.Breed?.DisplayName ?? "Lobo").ToUpperInvariant();

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

		bool blocked = RoomConfig.Rules.Transform == TransformRule.Bloqueada;

		if (_form.IsShifting)
		{
			float total = Mathf.Max(_form.ShiftTotal, 0.01f);
			_shiftBar.Value = 1f - _form.ShiftLeft / total;
			_shiftText.Text = _form.Current == Form.ShiftingToWolf ? "TRANSFORMANDO" : "VOLTANDO";
			_shiftState.Text = $"{_form.ShiftLeft:0.0} S  ·  VULNERÁVEL";
			_shiftState.AddThemeColorOverride("font_color", UiTokens.Danger);
			// O momento de maior risco da partida pulsa.
			_shiftPulse ??= Motion.Pulse(_shiftPanel, 0.55f, 0.35f);
			return;
		}

		if (_shiftPulse != null)
		{
			_shiftPulse.Kill();
			_shiftPulse = null;
			_shiftPanel.Modulate = Colors.White;
		}

		_shiftBar.Value = blocked ? 0 : 1;

		if (_form.IsWolf)
		{
			_shiftKey.Text = "1";
			_shiftText.Text = "VOLTAR A HUMANO";
			_shiftState.Text = "OU 2 · 4 · 5";
			_shiftState.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
		}
		else
		{
			_shiftKey.Text = "3";
			_shiftText.Text = "VIRAR LOBO";
			_shiftState.Text = blocked ? "BLOQUEADO NESTA SALA" : "PRONTO";
			_shiftState.AddThemeColorOverride("font_color", blocked ? UiTokens.TextDisabled : UiTokens.Success);
		}
	}

	private void OnTransformDenied(float missing)
	{
		if (missing < 0) Warn("Lobo bloqueado nesta sala", 1.6f, UiTokens.TextSecondary);
		else Warn($"Faltam {missing:0} WP para transformar");
	}
}
