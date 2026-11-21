using Godot;

/// <summary>
/// **Desligado** (Jhon, 21/11: "tire esse som por enquanto, não ficou
/// bom"): ninguém cria este nó, e os sons saíram de assets/audio (o
/// gerador continua em tools/sons/chamas.py). Para religar, gere os sons de
/// novo e ponha no HUD, a cada quadro:
/// <c>_fireSound.Track(Subject == null &amp;&amp; _match.Phase == MatchPhase.Live ? me : null, delta)</c>.
///
/// O som do "em chamas" (rage, <see cref="Rage"/>), só para o próprio
/// jogador (Jhon, 21/11: "ao ficar em chamas, deve ter um barulho de fogo e
/// mais efeitos especiais no som, somente para o jogador que está em
/// chamas"). Os outros veem a chama no TAB, mas não ouvem nada: é o prêmio
/// de quem está jogando bem, não uma pista de onde ele está.
///
/// - **Pega fogo:** <c>chamas_acender</c> (sopro subindo, brilho e o
///   "fuuum"), e o mundo abafa por um instante para o fogo passar na frente.
/// - **Em chamas:** <c>chamas_laco</c> ardendo em volta, mais alto quanto
///   mais calor; e os sons do mundo (os seus tiros, os passos) ganham peso
///   no grave (<see cref="Sfx.SetFire"/>).
/// - **Abate ou assistência em chamas:** <c>chamas_explosao</c> por cima da
///   confirmação de abate.
/// - **Apaga:** <c>chamas_apagar</c>, e o laço some em meio segundo.
///
/// Os sons são de tools/sons/chamas.py. Quem chama <see cref="Track"/> é o
/// HUD, a cada quadro, com o placar do jogador local (nulo quando está
/// assistindo outro).
/// </summary>
public partial class FireSound : Node
{
	/// <summary>Volume do laço: baixo com o calor no mínimo, cheio no máximo.</summary>
	private const float LoopQuietDb = -20f, LoopFullDb = -9f, Silent = -60f;

	/// <summary>Quanto o mundo abafa quando o fogo pega, por quanto tempo, e em quanto volta.</summary>
	private const float DuckDb = -7f, DuckHold = 0.12f, DuckRelease = 0.7f;

	private AudioStreamPlayer _loop;
	private bool _on;
	private int _score = -1;
	private float _effect;

	/// <summary>Passa o quadro. <paramref name="me"/> é o placar do jogador
	/// local; nulo (assistindo outro, fora da partida) apaga tudo.</summary>
	public void Track(CombatantStats me, float delta)
	{
		bool on = me != null && me.IsLocal && me.Rage.OnFire;
		int score = me == null ? -1 : me.Kills + me.Assists;

		if (on && !_on) Ignite();
		else if (!on && _on && me != null) Douse();   // fim de partida (me nulo): só some, sem o chiado
		else if (on && _score >= 0 && score > _score) Sfx.Local("chamas_explosao", -2f);
		_on = on;
		_score = score;

		// o laço acompanha o calor; o peso no grave entra e sai devagar
		float level = on ? me.Rage.Level : 0f;
		if (_loop != null)
		{
			float target = on ? Mathf.Lerp(LoopQuietDb, LoopFullDb, level) : Silent;
			_loop.VolumeDb = Mathf.MoveToward(_loop.VolumeDb, target, (on ? 30f : 80f) * delta);
			if (!on && _loop.VolumeDb <= Silent + 0.5f)
			{
				_loop.QueueFree();
				_loop = null;
			}
		}

		float effect = on ? 0.5f + 0.5f * level : 0f;
		if (!Mathf.IsEqualApprox(effect, _effect))
		{
			_effect = Mathf.MoveToward(_effect, effect, 2f * delta);
			Sfx.SetFire(_effect);
		}
	}

	private void Ignite()
	{
		Sfx.Local("chamas_acender");
		Sfx.DuckWorld(DuckDb, DuckHold, DuckRelease);
		if (_loop == null)
		{
			_loop = Sfx.Ambience(this, "chamas_laco", Silent);
			if (_loop != null) _loop.Bus = Sfx.BusSfx;
		}
	}

	private void Douse()
	{
		Sfx.Local("chamas_apagar", -3f);
	}

	public override void _ExitTree()
	{
		// A partida acabou ou o HUD saiu: nada de fogo nem de grave sobrando
		// no menu. Só quem acendeu desfaz (o HUD da câmera de abate também
		// tem um destes, sempre apagado, e não pode desligar o do jogador).
		if (!_on && _effect <= 0f) return;
		Sfx.SetFire(0f);
		Sfx.DuckWorld(0f, 0f, 0f);
		_effect = 0f;
		_on = false;
	}
}
