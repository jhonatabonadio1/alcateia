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

	/// <summary>Segundos para a luneta do fuzil de precisão chegar ao olho:
	/// mais lento que as outras miras, é o tempo da animação de subir.</summary>
	[Export] public float ScopeTime = 0.3f;

	/// <summary>Quanto da subida já passou quando a tela corta para a luneta
	/// (pedido do Jhon, 19/11: "a animação aproxima a mira do olho e pouco
	/// tempo depois corta para a mira, igual o mecanismo da Widowmaker").</summary>
	public const float ScopeCut = 0.9f;

	/// <summary>A arma na mão mira por luneta (todo fuzil de precisão).</summary>
	public bool HasScope => Held?.Class == WeaponClass.Sniper;

	/// <summary>A tela está na luneta: a arma some e fica o retículo.</summary>
	public bool Scoped => HasScope && AimBlend >= ScopeCut;

	private bool _aimWanted;

	/// <summary>Quanto está mirando: 0 é da cintura, 1 é na mira.</summary>
	public float AimBlend { get; private set; }

	/// <summary>Na mira (passou da metade da entrada).</summary>
	public bool IsAiming => AimBlend >= 0.5f;

	/// <summary>Dá para mirar agora: humano, vivo, arma de fogo na mão,
	/// sem estar recarregando.</summary>
	public bool CanAim => !_form.IsWolf && !_health.IsDead && Held != null && !IsReloading && !IsButting;

	/// <summary>O dono está pedindo a mira e dá para mirar agora: é o que
	/// a arma na mão segue para subir ao olho (<see cref="ViewModel"/>).</summary>
	public bool WantsAim => _aimWanted && CanAim;

	/// <summary>O dono pede (ou solta) a mira. Só o jogador chama.</summary>
	public void SetAiming(bool on) => _aimWanted = on;

	/// <summary>A entrada na mira já suavizada (devagar no começo e no
	/// fim), para a câmera e a arma na mão.</summary>
	public float AimEase => AimBlend * AimBlend * (3f - 2f * AimBlend);

	/// <summary>A abertura da câmera para quem abre em <paramref name="baseFov"/>.</summary>
	public float AimFovFor(float baseFov)
	{
		float aimed = Mathf.Min(Held?.AimFov ?? baseFov, baseFov);
		// Luneta: a câmera quase não fecha enquanto a arma sobe; o zoom
		// chega inteiro no corte.
		if (HasScope) return Scoped ? aimed : Mathf.Lerp(baseFov, baseFov * 0.88f, AimEase);
		return Mathf.Lerp(baseFov, aimed, AimEase);
	}

	/// <summary>O que a mira faz com a dispersão (1 = nada).</summary>
	private float AimSpreadFactor => Mathf.Lerp(1f, Held?.AimSpreadMultiplier ?? 1f, AimBlend);

	/// <summary>O que a mira faz com o passo (1 = nada).</summary>
	private float AimSpeedFactor => Mathf.Lerp(1f, Held?.AimSpeedMultiplier ?? 1f, AimBlend);

	private void UpdateAim(double delta)
	{
		float target = _aimWanted && CanAim ? 1f : 0f;
		float time = HasScope ? ScopeTime : AimTime;
		AimBlend = Mathf.MoveToward(AimBlend, target, (float)delta / Mathf.Max(time, 0.01f));
	}
}
