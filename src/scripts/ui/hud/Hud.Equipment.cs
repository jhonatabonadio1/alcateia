using Godot;
using System.Collections.Generic;

/// <summary>
/// Inferior direito: no humano, arma, pente, reserva, granadas, slots e
/// WP; no lobo, a recarga de cada golpe e o uivo. E o marcador de acerto.
/// </summary>
public partial class Hud
{
	private static readonly StyleBoxFlat SlotOn = HudStyle.Facet(new Color(UiTokens.Accent, 0.25f), UiTokens.Accent, 1, HudStyle.Skew, 5);
	private static readonly StyleBoxFlat SlotOff = HudStyle.Facet(new Color(0.03f, 0.04f, 0.06f, 0.55f), new Color(1, 1, 1, 0.14f), 1, HudStyle.Skew, 5);
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
	/// <summary>Os quadradinhos das habilidades do lobo: 0 a garrada, 2 o
	/// bote (o 1, o especial, é o disco do meio).</summary>
	private readonly SkillSquare[] _skills = new SkillSquare[3];
	private SkillSquare _howlSquare;
	private readonly float[] _cooldownLeft = new float[3];
	private readonly float[] _cooldownTotal = { 1, 1, 1 };
	private Label _howlLabel;
	private AbilityTile _pounceTile;
		
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

		int reserve = _weapon.Reserve;
		_mag.Text = inMag.ToString();
		_mag.AddThemeColorOverride("font_color", inMag == 0 && !reloading ? UiTokens.Danger : UiTokens.TextPrimary);
		_reserve.Text = $"/ {reserve}";
		_reserve.AddThemeColorOverride("font_color", reserve == 0 ? UiTokens.Danger : UiTokens.TextSecondary);
		_reloading.Text = reloading ? "RECARREGANDO"
			: inMag == 0 && reserve == 0 ? "SEM MUNIÇÃO"
			: inMag <= magSize / 4 && magSize > 0 && reserve > 0 ? "R · RECARREGAR" : "";
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
			_ => Loc.Upper(held?.DisplayName ?? "")
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

	/// <summary>
	/// O disco do especial, nas duas formas: a carga enche também de
	/// humano (abate é abate), e cheia ela espera o jogador virar lobo.
	/// De humano, cheio, o nome lembra disso.
	/// </summary>
	private void UpdateSpecial()
	{
		// Modo sem lobo: o especial não existe, não carrega e não aparece.
		_pounceTile.Visible = _weapon.WolfInPlay;
		if (!_pounceTile.Visible) return;

		_pounceTile.SetCharge(_weapon.SpecialCharge);
		_pounceTile.Title = _weapon.SpecialArmed
			? (_weapon.SpecialArmTime > 0f ? Loc.F("CLIQUE SALTA · {0:0.0} S", _weapon.SpecialArmLeft) : "ARMADO · CLIQUE SALTA")
			: !_weapon.PounceReady ? "ESPECIAL"
			: _form.IsWolf ? "PRONTO · Q ARMA" : "PRONTO · VIRE LOBO";
	}

	private void UpdateWolf(float delta)
	{
		if (!_wolf.Visible) return;

		for (int i = 0; i < 3; i++)
		{
			_cooldownLeft[i] = Mathf.Max(0, _cooldownLeft[i] - delta);
			_skills[i]?.Set(1f - _cooldownLeft[i] / _cooldownTotal[i]);
		}
		float howl = _player?.HowlProgress ?? 0f;

		// O uivo: enche enquanto se segura o R; com o escudo cheio não há o que recarregar.
		_howlSquare.Set(_form.CanHowl || howl > 0 ? 1f : 0f, howl);
		_howlLabel.Text = howl > 0 ? "SEGURE R · ESCUDO NO FIM" : "SEGURE E · HABILIDADES";
	}
}
