namespace Alcateia.UnitTests;

/// <summary>O juiz da partida online (scripts/domain/Referee.cs).</summary>
public static class RefereeTests
{
	private static Referee Two(int scoreToWin = 3, double seconds = 60, bool friendlyFire = false) =>
		new(new[] { (1L, "Ana", Referee.Blue), (2L, "Beto", Referee.Orange), (3L, "Caio", Referee.Blue) },
			scoreToWin, seconds, friendlyFire, countdown: 2);

	private static Referee Live(int scoreToWin = 3, double seconds = 60, bool friendlyFire = false)
	{
		Referee r = Two(scoreToWin, seconds, friendlyFire);
		r.Join(1); r.Join(2); r.Join(3);
		r.Tick(0.1);
		r.Tick(2.5);
		return r;
	}

	[Test("espera todos chegarem, conta e só então vale")]
	public static void StartsWhenEveryoneIsIn()
	{
		Referee r = Two();
		Check.True(r.Join(1), "escalado entra");
		Check.True(!r.Join(99), "quem não é da partida não entra");
		r.Tick(1);
		Check.Equal(RefPhase.Waiting, r.Phase, "faltando gente, espera");

		r.Join(2); r.Join(3);
		r.Tick(0.1);
		Check.Equal(RefPhase.Countdown, r.Phase, "todos dentro, conta");
		r.Tick(2.1);
		Check.Equal(RefPhase.Live, r.Phase, "contagem acabou, vale");
	}

	[Test("quem não chega não segura a partida para sempre")]
	public static void StartsWithoutLatecomers()
	{
		Referee r = Two();
		r.Join(1);
		r.Tick(19);
		Check.Equal(RefPhase.Waiting, r.Phase, "ainda dentro da espera");
		r.Tick(1.5);
		Check.Equal(RefPhase.Countdown, r.Phase, "a espera venceu");
	}

	[Test("abate só vale de quem acertou a vítima há pouco")]
	public static void KillNeedsARecentHit()
	{
		Referee r = Live();
		Check.True(r.Death(2, 1, false, null) == null, "sem acerto, sem abate");
		Check.Equal(0, r.Score(Referee.Blue), "placar intacto");

		r.Tick(2);
		Check.True(r.Hit(1, 2, 40), "acerto em inimigo passa");
		RefKill? kill = r.Death(2, 1, true, null);
		Check.True(kill != null, "com acerto, abate");
		Check.Equal(1, r.Score(Referee.Blue), "ponto do azul");
		Check.Equal(1, r.Find(1).Kills, "abate de Ana");
		Check.Equal(1, r.Find(1).Headshots, "na cabeça");
		Check.Equal(1, r.Find(2).Deaths, "morte de Beto");

		r.Tick(2);
		r.Hit(1, 2, 40);
		r.Tick(Referee.CreditWindow + 1);
		Check.True(r.Death(2, 1, false, null) == null, "acerto velho não vale");
	}

	[Test("acerto em aliado, em si mesmo, exagerado ou fora de hora é recusado")]
	public static void RefusesBadHits()
	{
		Referee r = Live();
		Check.True(!r.Hit(1, 3, 40), "aliado");
		Check.True(!r.Hit(1, 1, 40), "em si mesmo");
		Check.True(!r.Hit(1, 2, Referee.MaxHit + 1), "dano impossível");
		Check.True(!r.Hit(1, 2, float.NaN), "dano que não é número");
		Check.True(!r.Hit(1, 99, 40), "alvo que não existe");

		Referee waiting = Two();
		waiting.Join(1); waiting.Join(2);
		Check.True(!waiting.Hit(1, 2, 40), "antes de valer");

		Check.True(Live(friendlyFire: true).Hit(1, 3, 40), "com fogo amigo ligado, aliado passa");
	}

	[Test("assistência só de aliado do autor que também acertou")]
	public static void AssistsAreChecked()
	{
		Referee r = Live();
		r.Hit(3, 2, 30);
		r.Hit(1, 2, 30);
		RefKill? kill = r.Death(2, 1, false, new long[] { 3, 3, 1, 2, 99 });
		Check.Equal(1, kill.Value.Assisters.Length, "só Caio ajudou");
		Check.Equal(1, r.Find(3).Assists, "assistência de Caio");
		Check.Equal(15, r.Find(1).Points + r.Find(3).Points, "10 do abate e 5 da assistência");
	}

	[Test("a mesma morte contada duas vezes vale uma")]
	public static void DeathIsNotCountedTwice()
	{
		Referee r = Live();
		r.Hit(1, 2, 40);
		r.Death(2, 1, false, null);
		r.Hit(1, 2, 40);
		Check.True(r.Death(2, 1, false, null) == null, "no mesmo segundo, não");
		Check.Equal(1, r.Score(Referee.Blue), "um ponto só");
	}

	[Test("acaba no placar ou no relógio, e diz quem ganhou")]
	public static void EndsByScoreOrTime()
	{
		Referee r = Live(scoreToWin: 2);
		for (int i = 0; i < 2; i++)
		{
			r.Tick(2);
			r.Hit(1, 2, 40);
			r.Death(2, 1, false, null);
		}
		Check.Equal(RefPhase.Ended, r.Phase, "fez os pontos");
		Check.Equal(Referee.Blue, r.Winner, "azul ganhou");

		Referee timed = Live(seconds: 5);
		timed.Tick(6);
		Check.Equal(RefPhase.Ended, timed.Phase, "o tempo acabou");
		Check.Equal(0, timed.Winner, "empate");
	}

	[Test("partida vazia se encerra sozinha")]
	public static void EmptyMatchEnds()
	{
		Referee r = Live();
		r.Leave(1); r.Leave(2); r.Leave(3);
		r.Tick(10);
		Check.Equal(RefPhase.Live, r.Phase, "dá um tempo para voltarem");
		r.Tick(r.EmptyTimeout);
		Check.Equal(RefPhase.Ended, r.Phase, "ninguém voltou");
	}
}
