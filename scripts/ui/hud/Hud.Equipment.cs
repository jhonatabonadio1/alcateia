using Godot;
using System.Collections.Generic;

/// <summary>
/// Inferior direito: no humano, arma, pente, reserva, granadas, slots e
/// WP; no lobo, a recarga de cada golpe e o uivo. E o marcador de acerto.
/// </summary>
public partial class Hud
{
	private static readonly StyleBoxFlat SlotOn = UiTheme.Box(new Color(UiTokens.Accent, 0.2f), UiTokens.Accent, 1, 0, 0, 0);
	private static readonly StyleBoxFlat SlotOff = UiTheme.Box(new Color(1, 1, 1, 0.04f), new Color(1, 1, 1, 0.1f), 1, 0, 0, 0);
	private Label _wp;
	private VBoxContainer _human;
	private VBoxContainer _wolf;
	private Label _weaponName;
	private WeaponArt _weaponArt;
	private Label _mag;
	private Label _reserve;
	private Label _reloading;
	private Label _grenades;
	private readonly Dictionary<Slot, PanelContainer> _slots = new();
	private Label _breedName;
	private readonly ProgressBar[] _cooldownBars = new ProgressBar[3];
	private readonly float[] _cooldownLeft = new float[3];
	private readonly float[] _cooldownTotal = { 1, 1, 1 };
	private ProgressBar _howlBar;
	private Label _howlLabel;

	private void OnWpChanged(float wp, float max)
	{
		if (_wp == null) return;
		string text = $"{Mathf.Floor(wp):0}";
		bool changed = _wp.Text != text && _wp.Text != "0" || wp > 0 && _wp.Text == "0";
		_wp.Text = text;
		if (changed && IsInsideTree()) Motion.Pop(_wp, 1.25f);
	}

	private void OnAmmoChanged(int inMag, int magSize, bool reloading)
	{
		if (_mag == null || _form.IsWolf) return;

		_mag.Text = inMag.ToString();
		_mag.AddThemeColorOverride("font_color", inMag == 0 && !reloading ? UiTokens.Danger : UiTokens.TextPrimary);
		_reloading.Text = reloading ? "RECARREGANDO" : inMag <= magSize / 4 && magSize > 0 ? "R · RECARREGAR" : "";
	}

	private void OnSlotChanged(int slot)
	{
		if (_weaponName == null) return;

		WeaponDefinition held = _weapon.Held;
		Slot current = _weapon.CurrentSlot;

		_weaponArt.ItemId = _weapon.HeldId;
		_weaponName.Text = current switch
		{
			Slot.Knife => "FACA",
			Slot.Grenade => "GRANADA DE CHOQUE",
			_ => (held?.DisplayName ?? "").ToUpperInvariant()
		};

		bool showAmmo = current is Slot.Primary or Slot.Secondary;
		_mag.Visible = _reserve.Visible = showAmmo;

		// Espaço sem arma (soltou com B) fica apagado.
		_slots[Slot.Primary].Modulate = _weapon.HasPrimary ? Colors.White : new Color(1, 1, 1, 0.3f);
		_slots[Slot.Secondary].Modulate = _weapon.HasSecondary ? Colors.White : new Color(1, 1, 1, 0.3f);

		foreach (var (s, chip) in _slots)
		{
			bool on = s == current;
			chip.AddThemeStyleboxOverride("panel", on ? SlotOn : SlotOff);
			chip.GetChild<Label>(0).AddThemeColorOverride("font_color", on ? UiTokens.Accent : UiTokens.TextSecondary);
		}

		OnAmmoChanged(_weapon.AmmoInMag, _weapon.MagSize, _weapon.IsReloading);
	}

	private void OnGrenadesChanged(int left)
	{
		if (_grenades == null) return;
		_grenades.Text = left.ToString();
		_slots[Slot.Grenade].Modulate = left > 0 ? Colors.White : new Color(1, 1, 1, 0.35f);
	}

	private void OnHitConfirmed(bool headshot)
	{
		_hitMarker?.Flash(headshot);
	}

	private void OnAttackCooldown(int kind, float seconds)
	{
		if (kind < 0 || kind > 2) return;
		_cooldownTotal[kind] = Mathf.Max(seconds, 0.01f);
		_cooldownLeft[kind] = seconds;
	}

	private void UpdateWolf(float delta)
	{
		if (!_wolf.Visible) return;

		for (int i = 0; i < 3; i++)
		{
			_cooldownLeft[i] = Mathf.Max(0, _cooldownLeft[i] - delta);
			_cooldownBars[i].Value = 1f - _cooldownLeft[i] / _cooldownTotal[i];
		}

		float howl = _player?.HowlProgress ?? 0f;
		_howlBar.Value = howl;
		_howlLabel.Text = howl > 0 ? "SEGURE R · ESCUDO NO FIM" : "UIVO · SEGURE R";
	}
}
