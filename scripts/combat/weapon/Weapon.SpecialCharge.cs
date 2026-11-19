using Godot;

/// <summary>
/// A carga do especial do lobo (o salto do Q), em %, como a suprema de
/// Overwatch (decisão do Jhon, 18/11 — antes era uma recarga de 15 s).
///
/// Enche com o jogo: **cada abate dá um pedaço** (<see cref="KillFill"/>),
/// cada assistência um menor, e sozinha, sem ninguém fazer nada, ela enche
/// **bem devagar** (<see cref="PassiveFillTime"/>). Cheia, o Q arma;
/// saltou (ou deixou o prazo passar), zera.
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
	/// <summary>Quanto um abate enche, de 0 a 1, **fora de partida**
	/// (treino): 0,125 = oito abates. Na partida quem manda é a
	/// pontuação da sala (<see cref="KillFill"/>).</summary>
	[ExportGroup("Lobo: carga do especial")]
	[Export] public float SpecialPerKill = 0.125f;

	/// <summary>Uma assistência, como fração do abate.</summary>
	[Export] public float SpecialAssistShare = 0.4f;

	/// <summary>Segundos para encher sozinha, do zero, sem abate nenhum,
	/// **fora de partida**. Zero ou menos: não enche sozinha.</summary>
	[Export] public float SpecialFillTime = 420f;

	/// <summary>
	/// Na partida a carga anda com as regras da sala (pedido do Jhon,
	/// 18/11: "a ultimate deve demorar mais para carregar, bem mais, de
	/// acordo com o tempo escolhido do jogo e da pontuação"):
	///
	/// - abates para encher = esta fração da pontuação da sala (0,2 de 40
	///   pontos = 8 abates), nunca menos que <see cref="SpecialMinKills"/>;
	/// - sozinha, enche em <see cref="SpecialMatchShare"/> do tempo da
	///   partida (0,7 de 10 min = 7 min).
	/// </summary>
	[Export] public float SpecialScoreShare = 0.2f;
	[Export] public int SpecialMinKills = 6;
	[Export] public float SpecialMatchShare = 0.7f;

	/// <summary>Quanto um abate enche agora.</summary>
	public float KillFill => Match.Current == null
		? SpecialPerKill
		: 1f / Mathf.Max(SpecialMinKills, Match.Current.ScoreToWin * SpecialScoreShare);

	public float SpecialPerAssist => KillFill * SpecialAssistShare;

	/// <summary>Em quantos segundos enche sozinha agora.</summary>
	public float PassiveFillTime => Match.Current == null || SpecialFillTime <= 0f
		? SpecialFillTime
		: Match.Current.Duration * SpecialMatchShare;

	/// <summary>O lobo existe neste modo para este corpo? No "só humanos"
	/// não há especial: não carrega e o HUD não mostra (pedido do Jhon,
	/// 18/11).</summary>
	public bool WolfInPlay => _form == null || _form.Rule != FormRule.Humano;

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
		float time = PassiveFillTime;
		if (SpecialCharge >= 1f || time <= 0f || !WolfInPlay) return;
		if (_health.IsDead || !Match.IsLive) return;
		AddSpecial((float)(delta / time));
	}

	private void OnScored(bool kill)
	{
		if (WolfInPlay) AddSpecial(kill ? KillFill : SpecialPerAssist);
	}
}
