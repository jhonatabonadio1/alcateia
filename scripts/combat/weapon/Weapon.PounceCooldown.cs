using Godot;

/// <summary>
/// Recarga do salto carregado do lobo (botão direito no chão): depois de
/// saltar, o lobo espera esse tempo para carregar outro. O bote no ar
/// (pular e apertar o botão direito) é outra habilidade e não espera nada
/// por causa dela. Quem mostra a contagem na tela é o
/// <see cref="AbilityTile"/>.
///
/// É diferente do <c>_cooldown</c> da arma: aquele é o tempo em que o
/// golpe em si prende o lobo (animação); este é o intervalo entre um
/// salto e o próximo.
/// </summary>

public partial class Weapon
{
	/// <summary>Segundos até poder carregar outro salto, contados da hora
	/// em que o lobo salta. Zero ou menos desliga a recarga.</summary>
	[ExportGroup("Lobo: recarga do salto")]
	[Export] public float PounceRecharge = 6f;

	private double _pounceRechargeLeft;
	private float _pounceRechargeTotal = 1f;

	/// <summary>Quanto falta para o salto voltar, em segundos.</summary>
	public float PounceRechargeLeft => (float)_pounceRechargeLeft;

	/// <summary>A espera inteira do último salto, para o HUD calcular a fração.</summary>
	public float PounceRechargeTotal => _pounceRechargeTotal;

	public bool PounceReady => _pounceRechargeLeft <= 0;

	private void StartPounceRecharge()
	{
		_pounceRechargeTotal = Mathf.Max(PounceRecharge, 0.01f);
		_pounceRechargeLeft = Mathf.Max(PounceRecharge, 0f);
	}

	private void TickPounceRecharge(double delta)
	{
		if (_pounceRechargeLeft > 0) _pounceRechargeLeft -= delta;
	}

	/// <summary>Morreu: renasce com o salto pronto.</summary>
	private void ResetPounceRecharge() => _pounceRechargeLeft = 0;
}
