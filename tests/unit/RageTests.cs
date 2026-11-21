namespace Alcateia.UnitTests;

/// <summary>O rage (scripts/domain/Rage.cs) e o XP do tempo em chamas.</summary>
public static class RageTests
{
	[Test("dois abates seguidos põem em chamas; um só, não")]
	public static void TwoKillsIgnite()
	{
		var rage = new Rage();
		rage.Kill();
		Check.True(!rage.OnFire, "um abate não acende");
		rage.Kill();
		Check.True(rage.OnFire, "o segundo acende");
	}

	[Test("assistência ajuda: abate e duas assistências também acendem")]
	public static void AssistsCount()
	{
		var rage = new Rage();
		rage.Kill(); rage.Assist();
		Check.True(!rage.OnFire, "150 ainda não");
		rage.Assist();
		Check.True(rage.OnFire, "200 acende");
	}

	[Test("parado, o fogo apaga: 10 s de folga e depois esfria")]
	public static void CoolsDown()
	{
		var rage = new Rage();
		rage.Kill(); rage.Kill(); rage.Kill();   // 300, o teto
		bool changed = false;
		for (int i = 0; i < 100; i++) changed |= rage.Tick(0.1f);   // 10 s: a folga
		Check.True(rage.OnFire && !changed, "na folga continua em chamas");
		for (int i = 0; i < 90; i++) rage.Tick(0.1f);   // 9 s esfriando 20/s: 300 → ~120
		Check.True(rage.OnFire, "ainda acima de 100");
		for (int i = 0; i < 20; i++) rage.Tick(0.1f);   // mais 2 s: ~80
		Check.True(!rage.OnFire, "abaixo de 100 apaga");
		Check.True(rage.FireSeconds > 19f && rage.FireSeconds < 21.5f, $"uns 20 s em chamas ({rage.FireSeconds})");
	}

	[Test("um abate novo segura o fogo")]
	public static void KillKeepsIt()
	{
		var rage = new Rage();
		rage.Kill(); rage.Kill();
		// 14 s: 10 de folga e 4 esfriando, 200 → ~120, ainda em chamas. Com
		// 15 s o calor parava em 98 (a folga em float acaba um passo antes):
		// o fogo já tinha apagado e o abate não testava o que diz o nome.
		for (int i = 0; i < 140; i++) rage.Tick(0.1f);
		Check.True(rage.OnFire, "antes do abate, ainda em chamas");
		rage.Kill();
		for (int i = 0; i < 90; i++) rage.Tick(0.1f);
		Check.True(rage.OnFire, "o abate zerou a folga");
	}

	[Test("em chamas rende XP: 1 a cada 6 s, até 40")]
	public static void FireXp()
	{
		Check.Equal(0, Rewards.FireXp(5), "menos de 6 s");
		Check.Equal(10, Rewards.FireXp(60), "um minuto");
		Check.Equal(Rewards.MaxFireXp, Rewards.FireXp(100000), "o teto");
		Rewards.Gain g = Rewards.ForMatch(4, 0, won: false, draw: false, mvp: false, fireSeconds: 60);
		Check.Equal(30L + 16 + 10, g.Xp, "somado ao da partida");
	}

	[Test("anti-cheat: o tempo em chamas tem teto pelos abates, e o prêmio respeita")]
	public static void FireIsBounded()
	{
		Check.Equal(0, Rage.MaxFireSeconds(1, 1), "um abate e uma assistência não acendem");
		Check.Equal(30, Rage.MaxFireSeconds(2, 0), "dois abates: 30 s no máximo");
		Check.Equal(55, Rage.MaxFireSeconds(2, 2), "cada assistência, mais 12,5 s");

		// Ninguém fica em chamas sem abate: o servidor não paga.
		Rewards.Gain liar = Rewards.ForMatch(0, 0, won: false, draw: false, mvp: false, fireSeconds: 600);
		Check.Equal(30L, liar.Xp, "tempo em chamas sem abate não rende");
		Rewards.Gain capped = Rewards.ForMatch(2, 0, won: false, draw: false, mvp: false, fireSeconds: 600);
		Check.Equal(30L + 8 + 5, capped.Xp, "com dois abates, só os 30 s possíveis");
	}

	[Test("o jogo espelha o rage do servidor de partida")]
	public static void SyncFollowsServer()
	{
		var rage = new Rage();
		rage.Sync(250f, true, 12f);
		Check.True(rage.OnFire && rage.FireSeconds == 12f, "o que o servidor disse vale");
		rage.Sync(0f, false, 20f);
		Check.True(!rage.OnFire && rage.Heat == 0f, "e apaga quando ele apaga");
	}
}
