using Godot;
using static AnimationKit;
using System.Collections.Generic;

/// <summary>
/// O que a mão faz por cima das pernas, por quadro: recarregar (no tempo
/// da arma), armar e soltar a granada, e o esticão de quem leva tiro.
/// </summary>
public partial class CharacterAnimator
{
	/// <summary>
	/// Tempo mínimo entre dois esticões de dano.
	///
	/// Sem isto, uma rajada produz uma reação por bala e o corpo
	/// tremeluz no lugar em vez de reagir. A reação é um tempero: ela
	/// conta que você acertou, não cada vez que acertou.
	/// </summary>
	[ExportGroup("Reação a dano")]
	[Export] public float HitCooldown = 1.6f;

	private bool _hasReload;
	private bool _reloadPlaying;
	private float _reloadLength;
	private double _hitTimer;
	private bool _hasGrenade;
	private bool _grenadeShot;
	private double _grenadeClock;

	private void UpdateGrenade(double delta)
	{
		if (!_hasGrenade || _tree == null || _weapon == null || _dead) return;

		bool ready = _weapon.IsGrenadeReady;
		if (ready && !_grenadeShot)
		{
			_grenadeShot = true;
			_grenadeClock = GrenadeFrom;
			_tree.Set("parameters/GrenadeSeek/seek_request", GrenadeFrom);
			_tree.Set("parameters/GrenadeSpeed/scale", 1f);
			_tree.Set("parameters/Grenade/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			return;
		}

		if (!_grenadeShot) return;

		if (ready)
		{
			_grenadeClock += delta;
			if (_grenadeClock >= GrenadeCocked) _tree.Set("parameters/GrenadeSpeed/scale", 0f);
			return;
		}

		// Soltou: o resto do arremesso toca rápido e o disparo acaba sozinho.
		_grenadeShot = false;
		_tree.Set("parameters/GrenadeSpeed/scale", GrenadeThrowSpeed);
	}

	/// <summary>
	/// A recarga começa e termina com a da arma. A velocidade do clipe
	/// é ajustada para ele acabar junto com a recarga: a vazia, mais
	/// longa, fica mais lenta na tela também. Na escopeta o clipe toca
	/// uma vez no tempo dele, como gesto de "carregando".
	/// </summary>
	private void OnAmmoChanged(int inMag, int magSize, bool reloading)
	{
		if (!_hasReload || _tree == null || _dead) return;

		if (reloading && !_reloadPlaying)
		{
			_reloadPlaying = true;
			float duration = _weapon.ReloadDuration;
			bool shells = _weapon.Held?.ShellByShell == true;
			float scale = shells || duration <= 0.05f ? 1f : _reloadLength / duration;

			_tree.Set("parameters/ReloadSpeed/scale", Mathf.Clamp(scale, 0.4f, 3f));
			_tree.Set("parameters/Reload/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			return;
		}

		if (!reloading && _reloadPlaying)
		{
			_reloadPlaying = false;

			// Acabou cheio: deixa o clipe terminar sozinho. Interrompida
			// (trocou de arma, atirou na escopeta): corta.
			if (inMag < magSize) StopReload();
		}
	}

	private void StopReload()
	{
		_reloadPlaying = false;
		if (_hasReload && _tree != null)
			_tree.Set("parameters/Reload/request", (int)AnimationNodeOneShot.OneShotRequest.FadeOut);
	}

	/// <summary>Reação a dano. Só interrompe a locomoção, e só de vez
	/// em quando — ver <see cref="HitCooldown"/>.</summary>
	public void Hit()
	{
		if (_dead || _airborne || _hitTimer > 0) return;

		_hitTimer = HitCooldown;
		_playback?.Travel("Hit");
	}
}
