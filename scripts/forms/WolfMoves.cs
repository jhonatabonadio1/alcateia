using System.Collections.Generic;

/// <summary>Os golpes do lobo e o uivo.</summary>
public enum WolfMove
{
	/// <summary>Clique esquerdo, primeiro braço ("swiping").</summary>
	Claw,

	/// <summary>Clique esquerdo, o outro braço ("punch").</summary>
	Punch,

	/// <summary>Botão direito no chão: o salto inteiro (o Special_Attack),
	/// com o dano na aterrissagem.</summary>
	Pounce,

	/// <summary>Botão direito no ar: o bote, um mergulho na direção da
	/// mira que bate quando encosta em algo.</summary>
	Leap,

	/// <summary>R: o rugido que devolve o escudo.</summary>
	Howl
}

/// <summary>Trecho de um clipe do lobo: de qual arquivo, de onde a onde
/// ele toca e o instante do golpe, em segundos do arquivo original.
///
/// <paramref name="StepFrom"/>, <paramref name="StepTo"/> e
/// <paramref name="Step"/>: o passo que o clipe dá, em segundos do
/// arquivo e metros do modelo. O quadril do clipe é preso no lugar (o
/// corpo é quem anda), então o corpo precisa andar essa distância nesse
/// trecho para o pé não deslizar.</summary>
public readonly record struct WolfClip(string Source, float Start, float End, float Impact,
	bool FallOnly = false, float StepFrom = 0f, float StepTo = 0f, float Step = 0f)
{
	public float Length => End - Start;
}

/// <summary>
/// O tempo de cada golpe do lobo, **medido nas animações**. É a fonte
/// única: a arma (<see cref="Weapon"/>) aplica o dano no instante do
/// golpe e trava o próximo até a animação acabar, e o corpo
/// (<see cref="WolfVisual"/>) toca o mesmo trecho na mesma velocidade.
/// Como os dois leem daqui, não tem como sair de sincronia.
///
/// Desde 15/11 os golpes, o rugido, o salto e a aterrissagem são as
/// animações do Jhon (<c>lobo_anim.glb</c>, veja <see cref="WolfOwnClips"/>),
/// **tocadas na velocidade em que ele as fez** (1×, pedido do Jhon). O
/// nome do arquivo de cada linha é o papel: "leap" é o Special_Attack. Os
/// números vieram do esqueleto, quadro a quadro (mãos, pés e quadril): o
/// golpe é o pico de velocidade da mão que bate; no salto, os pés saindo e
/// voltando ao chão e as mãos batendo no chão.
///
/// | Clipe | Animação | Medido | Trecho usado |
/// |---|---|---|---|
/// | claw | Attack_Primary (0,73 s) | mão direita a 12,4 m/s em 0,20 s; pés parados | inteiro |
/// | heavy | Attack_Secondary (0,73 s) | mão esquerda a 12,3 m/s em 0,20 s; pés parados | inteiro |
/// | pounce | Special_Attack (2,20 s) | agacha até 0,40 s (quadril a 0,57 m), pés saem do chão em 0,50 s, topo em 1,00 s (quadril a 1,39 m), pés no chão em 1,27 s, mãos batem no chão em 1,33 s (15 m/s), de pé em 2,1 s; não anda (o clipe é no lugar) | inteiro; o corpo voa do fim do agachar (0,47 s) aos pés no chão (1,27 s) — quanto, quem diz é a carga. Só a descida do quadril vale (FallOnly): a subida é do corpo, no arco |
/// | dive | o mesmo Special_Attack | do alto, encolhido (0,80 s), às mãos no chão (1,33 s) | só a queda |
/// | dive_land | o mesmo Special_Attack | da pancada (1,33 s) a de pé | a aterrissagem do bote |
/// | howl | Howl (1,53 s) | os braços sobem e descem; pés parados | inteiro |
/// | jump_land | Jump_Land (0,77 s) | encolhe até o quadril a 0,65 m em 0,13 s, de pé no fim | inteiro, a aterrissagem quando o lobo chega devagar |
/// | jump | mutant jumping do Mixamo (3,17 s) | só sem o Jump_Air do Jhon: o voo, de 1,27 a 1,87 s | reserva |
/// | death | mutant dying do Mixamo (4,60 s) | parado até 0,43 s, a queda começa de verdade em 0,6 s | 0,55–4,60 s |
///
/// Trocou um clipe? Meça de novo e corrija a linha dele.
/// </summary>
public static class WolfMoves
{
	public static readonly Dictionary<string, WolfClip> Clips = new()
	{
		["claw"] = new WolfClip("claw", 0f, 0.733f, 0.20f),
		["heavy"] = new WolfClip("heavy", 0f, 0.733f, 0.20f),
		["pounce"] = new WolfClip("leap", 0f, 2.2f, 1.33f, FallOnly: true, StepFrom: 0.47f, StepTo: 1.27f),
		["dive"] = new WolfClip("leap", 0.80f, 1.33f, 1.33f, FallOnly: true),
		["dive_land"] = new WolfClip("leap", 1.33f, 2.2f, 1.33f, FallOnly: true),
		["howl"] = new WolfClip("howl", 0f, 1.533f, 1.533f),
		["jump"] = new WolfClip("jump", 1.27f, 1.87f, 1.60f, FallOnly: true),
		["jump_land"] = new WolfClip("jump_land", 0f, 0.767f, 0f),
		["death"] = new WolfClip("death", 0.55f, 4.60f, 0.55f),
	};

	/// <summary>Clipe e velocidade de cada golpe: 1, a velocidade em que o
	/// Jhon animou (pedido dele, 15/11). A velocidade é a única coisa a
	/// mexer para deixar um golpe mais rápido ou mais pesado: a duração e o
	/// instante do dano acompanham sozinhos.
	///
	/// O bote é a exceção: a queda toca na velocidade que fizer o
	/// impacto da animação cair no quadro em que o lobo encosta
	/// (<see cref="DiveSpeed"/>).</summary>
	public static (string Clip, float Speed) Of(WolfMove move) => move switch
	{
		WolfMove.Claw => ("claw", 1f),
		WolfMove.Punch => ("heavy", 1f),
		WolfMove.Pounce => ("pounce", 1f),
		WolfMove.Leap => ("dive", 1f),
		_ => ("howl", 1f)
	};

	/// <summary>Escala do Mutant no jogo (<see cref="WolfVisual.ModelScale"/>):
	/// o passo medido no modelo vira distância no mundo por ela.</summary>
	public const float BodyScale = 1.1f;

	/// <summary>O passo do golpe no tempo do jogo: quando começa depois do
	/// clique, quanto dura e quantos metros anda. Zero se o clipe não dá
	/// passo.</summary>
	public static (float Delay, float Duration, float Distance) StepOf(WolfMove move)
	{
		var (clip, speed) = Of(move);
		WolfClip c = Clips[clip];
		if (c.StepTo <= c.StepFrom) return (0f, 0f, 0f);
		float from = System.Math.Max(c.StepFrom, c.Start);
		return ((from - c.Start) / speed, (c.StepTo - from) / speed, c.Step * BodyScale);
	}

	/// <summary>A aterrissagem do bote, do impacto até de pé.</summary>
	public static (string Clip, float Speed) Land => ("dive_land", 1f);

	/// <summary>Quanto a aterrissagem do bote prende o lobo no chão.</summary>
	public static float LandDuration => Clips[Land.Clip].Length / Land.Speed;

	/// <summary>Velocidade da queda do bote para o impacto da animação
	/// chegar em <paramref name="eta"/> segundos — com 10% de folga, para
	/// a queda nunca acabar antes de o lobo encostar (o corpo voltaria à
	/// pose parada no ar). Encostou antes, a aterrissagem entra por cima.
	/// O piso de 0,43 cobre o mergulho mais longo (1,1 s).</summary>
	public static float DiveSpeed(float eta) =>
		System.Math.Clamp(Clips["dive"].Length / (System.Math.Max(eta, 0.05f) * 1.1f), 0.43f, 4f);

	/// <summary>Quanto o golpe dura na tela, em segundos. O próximo só
	/// sai depois disso: golpe não interrompe golpe.</summary>
	public static float Duration(WolfMove move)
	{
		var (clip, speed) = Of(move);
		return Clips[clip].Length / speed;
	}

	/// <summary>Do clique ao instante em que a garra chega, em segundos.
	/// É quando o dano sai. Não vale para o bote, cujo dano sai quando
	/// ele encosta.</summary>
	public static float ImpactDelay(WolfMove move)
	{
		var (clip, speed) = Of(move);
		WolfClip c = Clips[clip];
		return (c.Impact - c.Start) / speed;
	}
}
