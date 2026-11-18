using Godot;

/// <summary>
/// A carga do especial do lobo (o salto do Q), em %, como a suprema de
/// Overwatch (decisão do Jhon, 18/11 — antes era uma recarga de 15 s).
///
/// Enche com o jogo: **cada abate dá um bom pedaço**
/// (<see cref="SpecialPerKill"/>), cada assistência um menor, e sozinha,
/// sem ninguém fazer nada, ela enche **bem devagar**
/// (<see cref="SpecialFillTime"/>). Cheia, o Q salta; saltou, zera.
///
/// Vale para as duas formas: o abate de humano também enche, e a carga
/// espera cheia até o jogador virar lobo e usar. Morrer não tira nada — a
/// carga atravessa a vida, como a suprema; parado morto, não enche.
///
/// Online, como o WP: cada máquina conta a do corpo que move, com os
/// abates que o servidor devolve (<see cref="FormController.Scored"/>).
///
/// O bote no ar (botão direito) é outra coisa e não gasta nada daqui.
/// </summary>
public partial class Weapon
{
	/// <summary>Quanto um abate enche, de 0 a 1 (0,25 = quatro abates
	/// enchem do zero).</summary>
	[ExportGroup("Lobo: carga do especial")]
	[Export] public float SpecialPerKill = 0.25f;

	/// <summary>Quanto uma assistência enche.</summary>
	[Export] public float SpecialPerAssist = 0.1f;

	/// <summary>Segundos para encher sozinha, do zero, sem abate nenhum
	/// (só contam os vivos e com a partida valendo). Zero ou menos: não
	/// enche sozinha.</summary>
	[Export] public float SpecialFillTime = 240f;

	/// <summary>A carga, de 0 a 1.</summary>
	public float SpecialCharge { get; private set; }

	/// <summary>Especial sempre pronto e sem gastar (testes que saltam um
	/// atrás do outro).</summary>
	public bool SpecialFree { get; set; }

	public bool PounceReady => SpecialFree || SpecialCharge >= 1f;

	public void AddSpecial(float amount)
	{
		if (amount <= 0f) return;
		SpecialCharge = Mathf.Min(SpecialCharge + amount, 1f);
	}

	/// <summary>Enche de uma vez (testes, treino).</summary>
	public void FillSpecial() => SpecialCharge = 1f;

	/// <summary>Usou: zera.</summary>
	private void SpendSpecial()
	{
		if (!SpecialFree) SpecialCharge = 0f;
	}

	private void TickSpecial(double delta)
	{
		if (SpecialCharge >= 1f || SpecialFillTime <= 0f) return;
		if (_health.IsDead || !Match.IsLive) return;
		AddSpecial((float)(delta / SpecialFillTime));
	}

	private void OnScored(bool kill) => AddSpecial(kill ? SpecialPerKill : SpecialPerAssist);
}
