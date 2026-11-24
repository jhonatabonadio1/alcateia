namespace Alcateia.UnitTests;

/// <summary>O rage (scripts/domain/Rage.cs) e o XP do tempo em chamas.</summary>
public static class RageTests
{
	// Os números de hoje (Rage.cs, desde 23/11): abate +100, assistência +50,
	// teto 400; acende em 300 e apaga abaixo de 150; 6 s de folga e depois
	// esfria 40/s. Eram 200 para acender, 10 s de folga e 20/s.

	[Test("três abates seguidos põem em chamas; dois, não")]
	public static void ThreeKillsIgnite()
	{
		var rage = new Rage();
		rage.Kill(); rage.Kill();
		Check.True(!rage.OnFire, "dois abates não acendem");
		rage.Kill();
		Check.True(rage.OnFire, "o terceiro acende");
	}

	[Test("assistência ajuda: dois abates e duas assistências também acendem")]
	public static void AssistsCount()
	{
		var rage = new Rage();
		rage.Kill(); rage.Kill(); rage.Assist();
		Check.True(!rage.OnFire, "250 ainda não");
		rage.Assist();
		Check.True(rage.OnFire, "300 acende");
	}

	[Test("parado, o fogo apaga: 6 s de folga e depois esfria")]
	public static void CoolsDown()
	{
		var rage = new Rage();
		rage.Kill(); rage.Kill(); rage.Kill(); rage.Kill();   // 400, o teto
		bool changed = false;
		for (int i = 0; i < 60; i++) changed |= rage.Tick(0.1f);   // 6 s: a folga
		Check.True(rage.OnFire && !changed, "na folga continua em chamas");
		for (int i = 0; i < 55; i++) rage.Tick(0.1f);   // 5,5 s esfriando 40/s: 400 → ~180
		Check.True(rage.OnFire, "ainda acima de 150");
		for (int i = 0; i < 15; i++) rage.Tick(0.1f);   // mais 1,5 s: ~120
		Check.True(!rage.OnFire, "abaixo de 150 apaga");
		Check.True(rage.FireSeconds > 11.8f && rage.FireSeconds < 13f, $"uns 12 s em chamas ({rage.FireSeconds})");
	}

	[Test("um abate novo segura o fogo")]
	public static void KillKeepsIt()
	{
		var rage = new Rage();
		rage.Kill(); rage.Kill(); rage.Kill();
		// 9 s: 6 de folga e 3 esfriando, 300 → ~180, ainda em chamas.
		for (int i = 0; i < 90; i++) rage.Tick(0.1f);
		Check.True(rage.OnFire, "antes do abate, ainda em chamas");
		rage.Kill();
		// O abate zera a folga: mais 9 s, ~280 → ~160, ainda acima de 150.
		for (int i = 0; i < 90; i++) rage.Tick(0.1f);
		Check.True(rage.OnFire, "o abate zerou a folga");
	}

	[Test("em chamas rende XP: 1 a cada 6 s, até 40")]
	public static void FireXp()
	{
		Check.Equal(0, Rewards.FireXp(5), "menos de 6 s");
		Check.Equal(10, Rewards.FireXp(60), "um minuto");
		Check.Equal(Rewards.MaxFireXp, Rewards.FireXp(100000), "o teto");
		// Com 4 abates, o tempo em chamas possível é 34 s (Rage.MaxFireSeconds): 5 de XP.
		Rewards.Gain g = Rewards.ForMatch(4, 0, won: false, draw: false, mvp: false, fireSeconds: 60);
		Check.Equal(30L + 16 + 5, g.Xp, "somado ao da partida");
	}

	[Test("anti-cheat: o tempo em chamas tem teto pelos abates, e o prêmio respeita")]
	public static void FireIsBounded()
	{
		Check.Equal(0, Rage.MaxFireSeconds(2, 0), "dois abates não acendem");
		Check.Equal(0, Rage.MaxFireSeconds(2, 1), "dois abates e uma assistência também não");
		Check.Equal(26, Rage.MaxFireSeconds(3, 0), "três abates: 26 s no máximo (6 de folga e 2,5 esfriando, cada)");
		Check.Equal(32, Rage.MaxFireSeconds(2, 2), "cada assistência, mais 7,25 s");

		// Ninguém fica em chamas sem abate: o servidor não paga.
		Rewards.Gain liar = Rewards.ForMatch(0, 0, won: false, draw: false, mvp: false, fireSeconds: 600);
		Check.Equal(30L, liar.Xp, "tempo em chamas sem abate não rende");
		Rewards.Gain capped = Rewards.ForMatch(3, 0, won: false, draw: false, mvp: false, fireSeconds: 600);
		Check.Equal(30L + 12 + 4, capped.Xp, "com três abates, só os 26 s possíveis");
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
