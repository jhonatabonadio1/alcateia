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

	[Test("partida em curso: lugar novo entra zerado; quem já jogou volta no lugar dele; acabada, não entra ninguém")]
	public static void SeatsInProgress()
	{
		Referee r = Live();
		Check.True(r.Hit(1, 2, 50) && r.Death(2, 1, false, null) != null, "Ana abate Beto");

		Check.True(r.AddSeat(4, "Duda", Referee.Orange), "lugar novo com a partida valendo");
		Check.True(r.Join(4), "e quem chega entra nele");
		Check.Equal(Referee.Orange, r.Find(4).Team, "no time que o saguão deu");
		Check.Equal(0, r.Find(4).Kills, "zerado");

		Check.True(!r.AddSeat(1, "Ana", Referee.Orange), "quem já tem lugar não ganha outro");
		r.Leave(1);
		Check.True(r.Join(1), "saiu e voltou");
		Check.Equal(1, r.Find(1).Kills, "com o que já tinha feito");
		Check.Equal(Referee.Blue, r.Find(1).Team, "no time de antes");

		r.Tick(61);
		Check.Equal(RefPhase.Ended, r.Phase, "o tempo acabou");
		Check.True(!r.AddSeat(5, "Eva", Referee.Blue), "acabada, não abre lugar");
	}

	[Test("o dono dos bots sai: eles passam para o próximo jogador ligado; sem ninguém, saem junto")]
	public static void BotsGoToTheNextPlayer()
	{
		var r = new Referee(new[] { (1L, "Ana", Referee.Blue, 0L), (2L, "Beto", Referee.Orange, 0L), (3L, "Caio", Referee.Blue, 0L),
			(-1L, "Fenrir", Referee.Orange, 1L), (-2L, "Garra", Referee.Blue, 1L) }, 3, 60, false, countdown: 2);
		r.Join(1); r.Join(3);

		Check.Equal(3L, r.HandBots(1), "Beto não chegou: os bots vão para Caio, o próximo ligado");
		r.Leave(1);
		Check.True(r.Find(-1).Owner == 3 && r.Find(-2).Owner == 3, "os dois bots são de Caio");
		Check.True(r.Find(-1).Connected, "e continuam na partida");
		Check.True(r.Speaks(3, -1) && !r.Speaks(1, -1), "Caio fala por eles; Ana, não mais");

		Check.Equal(0L, r.HandBots(1), "quem não tem bot não passa nada");
		Check.Equal(0L, r.HandBots(3), "sem ninguém ligado para ficar com eles");
		r.Leave(3);
		Check.True(!r.Find(-1).Connected, "e eles saem com o último dono");
	}

	[Test("bots: chegam e saem com o dono, só ele fala por eles, pontuam e não contam como gente")]
	public static void BotsFollowTheirOwner()
	{
		var r = new Referee(new[] { (1L, "Ana", Referee.Blue, 0L), (2L, "Beto", Referee.Orange, 0L), (-1L, "Fenrir", Referee.Orange, 1L) },
			3, 60, false, countdown: 2);

		Check.True(!r.Join(-1), "bot não entra sozinho");
		r.Join(1);
		Check.True(r.Find(-1).Connected, "o bot chega com o dono");
		Check.Equal(1, r.ConnectedCount, "bot não conta como gente ligada");
		Check.True(r.Speaks(1, -1) && r.Speaks(1, 1), "o dono fala por si e pelo bot");
		Check.True(!r.Speaks(2, -1) && !r.Speaks(2, 1), "o outro jogador não fala pelo bot nem pelo dono");

		r.Join(2);
		r.Tick(0.1);
		r.Tick(2.5);
		Check.Equal(RefPhase.Live, r.Phase, "com todos (bot incluído) dentro, vale");

		Check.True(r.Hit(-1, 1, 50), "o bot acerta o inimigo");
		Check.True(r.Death(1, -1, false, null) != null, "e o abate do bot vale");
		Check.Equal(1, r.Score(Referee.Orange), "ponto para o time do bot");

		r.Leave(1);
		Check.True(!r.Find(-1).Connected, "o dono saiu, o bot saiu");
		Check.True(!r.Hit(2, -1, 50), "e não leva mais acerto");
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

	[Test("abate como lobo conta à parte; quem acertou há pouco é a lista de assistências")]
	public static void WolfKillsAndHitters()
	{
		Referee r = Live(scoreToWin: 10);
		r.Hit(2, 1, 50);
		r.Hit(3, 1, 10);   // aliado da vítima com fogo amigo desligado: não conta
		Check.True(r.RecentHitters(1).Length == 1 && r.RecentHitters(1)[0] == 2, "só o inimigo que acertou");
		Check.True(r.Death(1, 2, false, r.RecentHitters(1), killerWasWolf: true) != null, "o abate vale");
		Check.Equal(1, r.Find(2).KillsWolf, "abate como lobo");
		Check.Equal(1, r.Find(2).Kills, "e conta como abate");

		r.Tick(2);
		r.Hit(1, 2, 50);
		r.Death(2, 1, true, r.RecentHitters(2));
		Check.Equal(0, r.Find(1).KillsWolf, "abate como humano não conta como lobo");
		Check.Equal(0, r.RecentHitters(2).Length, "depois da morte, a lista recomeça");
	}
}
