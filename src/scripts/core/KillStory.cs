using System.Collections.Generic;

/// <summary>
/// O que um abate significa na história da partida, além de "A matou B"
/// (pedido do Jhon, 17/11): a **sequência** de quem matou — dois, três,
/// quatro abates com pouco tempo entre um e outro — e a **vingança**,
/// quando a vítima é quem tinha matado o autor por último.
///
/// Sai do próprio feed de abates, em cada máquina: não precisa de nada
/// novo na rede. Sem Godot aqui dentro, para dar para testar.
/// </summary>
public sealed class KillStory
{
	/// <summary>Tempo máximo entre dois abates para a sequência continuar.</summary>
	public double Window = 5.0;

	/// <summary>O que este abate foi.</summary>
	public readonly record struct Note(int Streak, bool Revenge);

	private readonly Dictionary<string, (int Count, double At)> _streaks = new();
	private readonly Dictionary<string, string> _lastKillerOf = new();

	/// <summary>Registra o abate (no instante <paramref name="now"/>, em
	/// segundos) e diz o que ele foi. Suicídio e queda não contam.</summary>
	public Note Add(string killer, string victim, double now)
	{
		// Morreu: a sequência dele acaba.
		_streaks.Remove(victim);
		if (string.IsNullOrEmpty(killer) || killer == victim) return new Note(0, false);

		int count = _streaks.TryGetValue(killer, out var streak) && now - streak.At <= Window ? streak.Count + 1 : 1;
		_streaks[killer] = (count, now);

		bool revenge = _lastKillerOf.TryGetValue(killer, out string rival) && rival == victim;
		if (revenge) _lastKillerOf.Remove(killer);
		_lastKillerOf[victim] = killer;

		return new Note(count, revenge);
	}

	/// <summary>O nome da sequência: DOUBLE KILL, TRIPLE KILL… Vazio para
	/// um abate só.</summary>
	public static string StreakName(int streak) => streak switch
	{
		< 2 => "",
		2 => "DOUBLE KILL",
		3 => "TRIPLE KILL",
		4 => "QUADRA KILL",
		5 => "PENTA KILL",
		6 => "HEXA KILL",
		7 => "HEPTA KILL",
		_ => "OCTA KILL"
	};

	/// <summary>O arquivo da fala em assets/audio (sem extensão):
	/// voz_double_kill, voz_triple_kill…</summary>
	public static string StreakVoice(int streak) =>
		streak < 2 ? "" : "voz_" + StreakName(streak).ToLowerInvariant().Replace(' ', '_');

	public const string TeamKillName = "TEAM KILL";
	public const string TeamKillVoice = "voz_team_kill";
	public const string RevengeVoice = "voz_vinganca";

	/// <summary>A fala que vale para qualquer sequência enquanto a
	/// específica não foi gravada.</summary>
	public const string FallbackVoice = "voz_multi_kill";
}
