using Godot;

/// <summary>
/// Zoom: segurar o botão direito, de humano, com arma de fogo na mão
/// (pedido do Jhon, 13/11). A câmera fecha (a abertura da arma,
/// <see cref="WeaponDefinition.AimFov"/>), a cruz vira a mira circular
/// (<see cref="Crosshair.PaintScope"/>), o tiro fica mais preciso e o
/// passo mais lento. A arma na mão fica onde está.
///
/// A entrada e a saída levam <see cref="AimTime"/>: mirar não é de graça,
/// e quem mira tarde atira com a dispersão de quase quem não mirou.
/// Recarregar tira da mira; de lobo o botão direito continua sendo o
/// salto e o bote.
/// </summary>
public partial class Weapon
{
	/// <summary>Segundos para entrar na mira (e para sair).</summary>
	[ExportGroup("Mira")]
	[Export] public float AimTime = 0.16f;

	private bool _aimWanted;

	/// <summary>Quanto está mirando: 0 é da cintura, 1 é na mira.</summary>
	public float AimBlend { get; private set; }

	/// <summary>Na mira (passou da metade da entrada).</summary>
	public bool IsAiming => AimBlend >= 0.5f;

	/// <summary>Dá para mirar agora: humano, vivo, arma de fogo na mão,
	/// sem estar recarregando.</summary>
	public bool CanAim => !_form.IsWolf && !_health.IsDead && Held != null && !IsReloading;

	/// <summary>O dono está pedindo a mira e dá para mirar agora: é o que
	/// a arma na mão segue para subir ao olho (<see cref="ViewModel"/>).</summary>
	public bool WantsAim => _aimWanted && CanAim;

	/// <summary>O dono pede (ou solta) a mira. Só o jogador chama.</summary>
	public void SetAiming(bool on) => _aimWanted = on;

	/// <summary>A entrada na mira já suavizada (devagar no começo e no
	/// fim), para a câmera e a arma na mão.</summary>
	public float AimEase => AimBlend * AimBlend * (3f - 2f * AimBlend);

	/// <summary>A abertura da câmera para quem abre em <paramref name="baseFov"/>.</summary>
	public float AimFovFor(float baseFov) =>
		Mathf.Lerp(baseFov, Mathf.Min(Held?.AimFov ?? baseFov, baseFov), AimEase);

	/// <summary>O que a mira faz com a dispersão (1 = nada).</summary>
	private float AimSpreadFactor => Mathf.Lerp(1f, Held?.AimSpreadMultiplier ?? 1f, AimBlend);

	/// <summary>O que a mira faz com o passo (1 = nada).</summary>
	private float AimSpeedFactor => Mathf.Lerp(1f, Held?.AimSpeedMultiplier ?? 1f, AimBlend);

	private void UpdateAim(double delta)
	{
		float target = _aimWanted && CanAim ? 1f : 0f;
		AimBlend = Mathf.MoveToward(AimBlend, target, (float)delta / Mathf.Max(AimTime, 0.01f));
	}
}
