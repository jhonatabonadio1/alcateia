using Godot;
using System.Collections.Generic;

/// <summary>
/// Inferior esquerdo e a tela: vida, escudo, a vinheta de vida baixa e de
/// dano, e o aviso de morte com o relógio de renascer.
/// </summary>
public partial class Hud
{
	/// <summary>Abaixo desta fração de vida, a barra pulsa e a vinheta
	/// vermelha entra.</summary>
	[ExportGroup("Vida")]
	[Export] public float LowHealth = 0.3f;

	private Label _hpNumber;
	private Label _hpMax;
	private ProgressBar _hpBar;
	private ProgressBar _shieldBar;
	private Label _shieldNumber;
	private HBoxContainer _shieldRow;
	private Tween _lowPulse;
	private bool? _lowShown;
	private static readonly StyleBoxFlat HpFill = UiTheme.Box(UiTokens.TextPrimary, default, 0, 0, 0, 0);
	private static readonly StyleBoxFlat HpFillLow = UiTheme.Box(UiTokens.Danger, default, 0, 0, 0, 0);
	private TextureRect _vignette;
	private float _hurtFlash;
	private VBoxContainer _death;
	private Label _deathBy;
	private Label _deathTimer;

	private void OnHealthChanged(float hp, float maxHp, float shield)
	{
		if (_hpNumber == null) return;

		_hpNumber.Text = $"{Mathf.Ceil(Mathf.Max(hp, 0)):0}";
		_hpMax.Text = $"/ {maxHp:0}";
		_hpBar.Value = maxHp > 0 ? hp / maxHp : 0;

		_shieldRow.Visible = _health.MaxShield > 0;
		_shieldBar.Value = _health.MaxShield > 0 ? shield / _health.MaxShield : 0;
		_shieldNumber.Text = $"{Mathf.Ceil(shield):0}";

		// Vida baixa: a barra pulsa e a vinheta entra.
		bool low = !_health.IsDead && maxHp > 0 && hp / maxHp < LowHealth;
		// Os estilos são criados uma vez: a vida muda a cada quadro (o
		// escudo do lobo escorre), e criar um estilo por quadro acumulava
		// objetos do lado C# que derrubavam o jogo ao fechar.
		if (low != _lowShown)
		{
			_lowShown = low;
			_hpBar.AddThemeStyleboxOverride("fill", low ? HpFillLow : HpFill);
			_hpNumber.AddThemeColorOverride("font_color", low ? UiTokens.Danger : UiTokens.TextPrimary);
		}

		if (low && _lowPulse == null) _lowPulse = Motion.Pulse(_hpBar, 0.35f, 0.7f);
		if (!low && _lowPulse != null)
		{
			_lowPulse.Kill();
			_lowPulse = null;
			_hpBar.Modulate = Colors.White;
		}

		UpdateVignette();
	}

	private void OnDamaged(float amount, Vector3 from)
	{
		if (_health.IsDead) return;
		_hurtFlash = Mathf.Min(1f, _hurtFlash + 0.45f);
		UpdateVignette();
	}

	private void UpdateVignette()
	{
		if (_vignette == null) return;
		bool low = !_health.IsDead && _health.MaxHp > 0 && _health.Hp / _health.MaxHp < LowHealth;
		float lowAlpha = low ? 0.6f + 0.2f * Mathf.Sin(Time.GetTicksMsec() / 180f) : 0f;
		_vignette.Modulate = new Color(1, 1, 1, Mathf.Clamp(Mathf.Max(lowAlpha, _hurtFlash * 0.55f), 0, 1));
	}

	private void OnDied(int killerId)
	{
		_death.Visible = true;
		_warning.Text = "";
		_warningTimer = 0;
		_crosshair.Visible = false;
		Motion.FadeIn(_death, UiTokens.Normal);
	}

	private void OnRespawned()
	{
		_death.Visible = false;
		_crosshair.Visible = true;
		_hurtFlash = 0;
		UpdateVignette();
	}
}
