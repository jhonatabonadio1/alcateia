using Godot;

/// <summary>
/// O que faz um bot diferente do outro, e o que o tira da ronda.
///
/// Medido em 18/11, antes disto: sete bots passavam de 71 a 81% da
/// partida em ronda, 1% do tempo de lobo, com o aliado mais próximo a 6
/// ou 8 metros — um time andando em fila, todo mundo igual, sem achar
/// ninguém. Três coisas mudam isso:
///
/// **Personalidade.** Cada bot sorteia, ao nascer, o quanto gosta de ser
/// lobo, a que distância prefere lutar e por que lado chega. Os números
/// do nível (<see cref="BotDifficulty"/>) continuam os mesmos para todos:
/// a personalidade multiplica por cima, sem tocar neles.
///
/// **Humor de lobo.** O lobo deixa de ser só desespero. De tempos em
/// tempos o bot decide se a próxima meia-minuto é de lobo ou de arma; de
/// lobo, transforma e vai para cima correndo.
///
/// **Rádio e faro.** Quem está à toa ouve o time (<see cref="TeamIntel"/>)
/// e vai para a briga; sem notícia nenhuma por um tempo, vai atrás de um
/// inimigo com um palpite largo de onde ele anda. Partida de mata-mata em
/// que ninguém se acha não é furtiva, é vazia.
/// </summary>
public partial class Bot
{
	/// <summary>Chance de o próximo humor ser de lobo. Cada bot sorteia a
	/// sua em volta disto (<see cref="WolfLoveSpread"/>): uns quase só
	/// atiram, outros vivem de quatro.</summary>
	[ExportGroup("Personalidade")]
	[Export] public float WolfLove = 0.38f;
	[Export] public float WolfLoveSpread = 0.3f;

	/// <summary>Duração média de um humor de lobo e de um humor de arma,
	/// em segundos (cada um sorteado de 0,6 a 1,4 disto).</summary>
	[Export] public float WolfMoodTime = 20f;
	[Export] public float HumanMoodTime = 16f;

	/// <summary>Quanto a distância preferida varia de um bot para o outro
	/// (0,35 = de 65% a 135% da do nível).</summary>
	[Export] public float RangeSpread = 0.35f;

	/// <summary>Chance de largar a ronda para ajudar quando o time avisa
	/// de um inimigo.</summary>
	[Export] public float Teamwork = 0.7f;

	/// <summary>Até que distância um aviso do time interessa, em metros.</summary>
	[Export] public float IntelRange = 45f;

	/// <summary>Chegando numa briga de longe, o bot mira um ponto até isto
	/// de lado do aviso, em metros — cada um o seu, para o time não entrar
	/// em fila pela mesma porta.</summary>
	[Export] public float FlankWidth = 12f;

	/// <summary>Segundos sem notícia de inimigo até o bot ir atrás de um
	/// por conta própria, e o erro do palpite, em metros.</summary>
	[Export] public float HuntAfter = 5f;
	[Export] public float HuntError = 10f;

	private float _wolfLove;
	private float _rangeTaste = 1f;
	private float _flank;

	private bool _wolfMood;
	private double _moodTimer;

	private double _intelTimer;
	private double _intelQuiet = 99;
	private bool _answers;
	private double _idleFor;

	/// <summary>A distância de luta deste bot: a do nível, ao gosto dele.</summary>
	private float TasteRange => PreferredRange * _rangeTaste;

	/// <summary>Velocidade de quem tem pressa, na forma atual.</summary>
	private float FormSpeed =>
		Speed * (_form.IsWolf && _form.Breed != null ? _form.Breed.SpeedMultiplier : 1f);

	/// <summary>Correndo de quatro (o "W W" do jogador): lobo no chão,
	/// mais rápido que o passo de lobo. Pela velocidade, e não pela ordem
	/// do cérebro, para valer também no bot que chega pela rede. A
	/// animação de galope e a poeira perguntam isto (19/11: o lobo dos bots
	/// só andava muito rápido, sem correr de quatro).</summary>
	public bool IsSprinting
	{
		get
		{
			if (!_form.IsWolf || !IsOnFloor()) return false;
			Vector3 flat = new Vector3(Velocity.X, 0, Velocity.Z);
			return flat.Length() > FormSpeed * SprintSpeedMark;
		}
	}

	/// <summary>Acima de quanto da velocidade de lobo já é galope (a corrida
	/// de quatro do bot é <see cref="WolfSprintFactor"/> = 1,3×).</summary>
	private const float SprintSpeedMark = 1.12f;

	private void RollPersona()
	{
		_wolfLove = Mathf.Clamp(WolfLove + _rng.Range(-WolfLoveSpread, WolfLoveSpread), 0.05f, 0.95f);
		_rangeTaste = 1f + _rng.Range(-RangeSpread, RangeSpread);
		_flank = _rng.Range(-FlankWidth, FlankWidth);
		RollMood();

		if (Debug)
			GD.Print($"{Name}: lobo {_wolfLove:0.00}, distância ×{_rangeTaste:0.00}, flanco {_flank:0.0} m");
	}

	private void RollMood()
	{
		_wolfMood = _rng.Chance(_wolfLove);
		_moodTimer = (_wolfMood ? WolfMoodTime : HumanMoodTime) * _rng.Range(0.6, 1.4);
	}

	/// <summary>
	/// Troca o humor quando o tempo dele acaba — menos no meio de uma
	/// troca de golpes: lobo que vira gente a dois metros do alvo morre
	/// com a arma ainda subindo.
	/// </summary>
	private void UpdateMood(double delta)
	{
		_moodTimer -= delta;
		if (_moodTimer > 0) return;

		if (State == BotState.Combat)
		{
			_moodTimer = 2.0;
			return;
		}

		RollMood();

		if (Debug) GD.Print($"{Name}: humor de {(_wolfMood ? "lobo" : "arma")}");
	}

	/// <summary>
	/// Quem não tem o que fazer procura o que fazer: primeiro o rádio do
	/// time, depois o próprio faro.
	///
	/// A resposta ao rádio é sorteada uma vez por briga, não a cada
	/// consulta — senão todo mundo acabava indo, só que um de cada vez.
	/// </summary>
	private void SeekAction(double delta)
	{
		bool busy = State == BotState.Combat
			|| (_hasBelief && _memoryTimer > 0 && _beliefAge < 2.0);

		_idleFor = busy ? 0 : _idleFor + delta;

		_intelTimer -= delta;
		if (_intelTimer > 0) return;
		_intelTimer = 0.8;

		if (busy) return;

		if (TeamIntel.Nearest(_health.Team, GlobalPosition, IntelRange, out Vector3 where))
		{
			if (_intelQuiet > 4.0) _answers = _rng.Chance(Teamwork);
			_intelQuiet = 0;

			if (_answers)
			{
				GoLook(where, flank: true);
				return;
			}
		}
		else
		{
			_intelQuiet += 0.8;
		}

		if (_idleFor < HuntAfter) return;

		Node3D prey = PickPrey();
		if (prey == null) return;

		// Faro: um palpite largo de onde um inimigo anda. Não é a posição —
		// é a região, e o bot ainda tem de achá-lo quando chegar.
		var guess = new Vector3(
			_rng.Range(-HuntError, HuntError), 0, _rng.Range(-HuntError, HuntError));

		GoLook(prey.GlobalPosition + guess, flank: false);
		_idleFor = 0;

		if (Debug) GD.Print($"{Name}: sem notícia, foi atrás");
	}

	/// <summary>
	/// Um inimigo vivo qualquer, sorteado — não o mais perto. Com o mais
	/// perto, o time inteiro farejava o mesmo e ia junto; sorteando, cada
	/// um vai para um canto do mapa.
	/// </summary>
	private Node3D PickPrey()
	{
		Node3D pick = null;
		int seen = 0;

		foreach (var (body, health) in OtherFighters())
		{
			if (health.Team == _health.Team) continue;

			// Sorteio de um entre N sem montar a lista.
			seen++;
			if (_rng.Index(seen) == 0) pick = body;
		}

		return pick;
	}

	/// <summary>Vai olhar um lugar. A crença dura o caminho até lá: com a
	/// memória curta de quem só ouviu um passo, o bot desistia no meio.</summary>
	private void GoLook(Vector3 where, bool flank)
	{
		Vector3 to = where - GlobalPosition;
		to.Y = 0;
		float distance = to.Length();

		// De longe, chega pelo lado; de perto, vai direto.
		if (flank && distance > 14f)
			where += to.Cross(Vector3.Up).Normalized() * _flank;

		Believe(where, Memory + distance / Mathf.Max(Speed, 0.1f));

		// Chegando, olha em volta um pouco antes de desistir.
		if (_scanTimer <= 0) _scanTimer = ScanTime * 0.5f;
	}
}
