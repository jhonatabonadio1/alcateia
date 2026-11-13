using Godot;

/// <summary>
/// Um nível de dificuldade dos bots, como dado: um .tres por nível em
/// resources/bots/ (facil, medio, dificil), editável no Inspector sem
/// recompilar. Ao entrar na partida, o nível escolhido sobrescreve estes
/// campos no bot (<see cref="Difficulty"/>).
///
/// O que muda entre os níveis não é vida nem dano — é competência. Um bot
/// fácil não é um bot fraco que morre fácil; é um bot que percebe tarde,
/// reage devagar, erra muito e não sustenta fogo. O teste Dificuldade
/// confere que cada nível é mais competente que o anterior em tudo, e que
/// nem o difícil acerta sempre.
///
/// Os valores padrão daqui são os do médio.
/// </summary>
[GlobalClass]
public partial class BotDifficulty : Resource
{
	/// <summary>Nome no menu e na sala.</summary>
	[Export] public string Label = "MÉDIO";

	/// <summary>Uma linha sob o nome, no menu de dificuldade.</summary>
	[Export] public string Summary = "reage em meio segundo, erra um quinto dos tiros";

	/// <summary>Até onde enxerga, em metros.</summary>
	[ExportGroup("Percepção")]
	[Export] public float SightRange = 40f;

	/// <summary>Campo de visão, em graus.</summary>
	[Export] public float FieldOfView = 140f;

	/// <summary>Quanto tempo lembra de onde viu o alvo, em segundos.</summary>
	[Export] public float Memory = 3f;

	/// <summary>Multiplicador do raio dos ruídos que ouve.</summary>
	[Export] public float Hearing = 1f;

	/// <summary>Erro, em metros, de onde acha que o som veio.</summary>
	[Export] public float HearingError = 2.5f;

	/// <summary>Chance de ir ver de onde veio um som (cada bot ainda
	/// sorteia a sua personalidade em volta disto).</summary>
	[Export] public float Curiosity = 0.6f;

	/// <summary>Tempo até se virar e atirar ao ver ou levar tiro, em
	/// segundos.</summary>
	[ExportGroup("Reação")]
	[Export] public float ReactionTime = 0.4f;

	/// <summary>Variação sorteada na reação, em segundos.</summary>
	[Export] public float ReactionJitter = 0.2f;

	/// <summary>Velocidade de virar quando alertado.</summary>
	[Export] public float AlertTurnSpeed = 4.5f;

	/// <summary>Velocidade de virar normal.</summary>
	[Export] public float TurnSpeed = 8f;

	/// <summary>Desvio da mira, em graus.</summary>
	[ExportGroup("Mira e fogo")]
	[Export] public float AimError = 4.5f;

	/// <summary>Fração dos tiros que vai longe de propósito.</summary>
	[Export] public float MissChance = 0.2f;

	/// <summary>Quanto tempo atira antes de pausar, em segundos.</summary>
	[Export] public float BurstTime = 0.7f;

	/// <summary>Pausa entre rajadas, em segundos.</summary>
	[Export] public float BurstPause = 0.65f;

	/// <summary>Até onde troca tiro, em metros.</summary>
	[Export] public float EngageRange = 40f;

	/// <summary>Velocidade de andar, em m/s.</summary>
	[ExportGroup("Movimento")]
	[Export] public float Speed = 3.0f;

	/// <summary>Fração da velocidade no strafe.</summary>
	[Export] public float StrafeFactor = 0.8f;

	/// <summary>De quanto em quanto tempo troca o lado do strafe.</summary>
	[Export] public float StrafeInterval = 1.4f;

	/// <summary>Distância que tenta manter do alvo, em metros.</summary>
	[Export] public float PreferredRange = 9f;

	/// <summary>Vontade de avançar em vez de segurar posição, de 0 a 1.</summary>
	[Export] public float Aggression = 0.8f;

	/// <summary>Fração da velocidade na ronda.</summary>
	[Export] public float PatrolSpeedFactor = 0.55f;

	/// <summary>Pausa entre um ponto da ronda e o próximo, em segundos.</summary>
	[Export] public float PatrolPause = 1.5f;

	/// <summary>Chance por segundo de tentar um especial com o alvo à vista.</summary>
	[ExportGroup("Lobo")]
	[Export] public float WolfSpecialRate = 0.5f;

	/// <summary>Erro na carga do salto. Nunca zero: um salto que sempre
	/// cai na cabeça é injusto, não difícil.</summary>
	[Export] public float PounceError = 0.12f;

	/// <summary>Escreve o nível no bot.</summary>
	public void ApplyTo(Bot bot)
	{
		bot.SightRange = SightRange;
		bot.FieldOfView = FieldOfView;
		bot.Memory = Memory;
		bot.Hearing = Hearing;
		bot.HearingError = HearingError;
		bot.Curiosity = Curiosity;

		bot.ReactionTime = ReactionTime;
		bot.ReactionJitter = ReactionJitter;
		bot.AlertTurnSpeed = AlertTurnSpeed;
		bot.TurnSpeed = TurnSpeed;

		bot.AimError = AimError;
		bot.MissChance = MissChance;
		bot.BurstTime = BurstTime;
		bot.BurstPause = BurstPause;
		bot.EngageRange = EngageRange;

		bot.Speed = Speed;
		bot.StrafeFactor = StrafeFactor;
		bot.StrafeInterval = StrafeInterval;
		bot.PreferredRange = PreferredRange;
		bot.Aggression = Aggression;
		bot.PatrolSpeedFactor = PatrolSpeedFactor;
		bot.PatrolPause = PatrolPause;

		bot.WolfSpecialRate = WolfSpecialRate;
		bot.PounceError = PounceError;
	}
}
