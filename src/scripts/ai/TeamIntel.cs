using Godot;
using System.Collections.Generic;

/// <summary>
/// O rádio do time: onde alguém do time viu (ou levou tiro de) um
/// inimigo, e quando.
///
/// Sem isto cada bot só sabe o que ele mesmo viu e ouviu, e o resultado
/// medido era um time que passava três quartos da partida em ronda
/// enquanto um aliado trocava tiro a quarenta metros. Com o rádio, quem
/// está à toa vai para a briga — que é o que um time faz.
///
/// É informação de time, não onisciência: só entra aqui o que um bot do
/// time de fato percebeu, e ela envelhece em poucos segundos.
/// </summary>
public static class TeamIntel
{
	/// <summary>Por quantos segundos um avistamento ainda vale.</summary>
	public const double Fresh = 5.0;

	private struct Sighting
	{
		public Vector3 Position;
		public ulong Frame;
	}

	private static readonly Dictionary<(int Team, ulong Enemy), Sighting> Seen = new();
	private static readonly List<(int, ulong)> Stale = new();

	private static ulong FreshFrames => (ulong)(Fresh * Engine.PhysicsTicksPerSecond);

	/// <summary>Um bot do <paramref name="team"/> percebeu um inimigo em
	/// <paramref name="position"/>. <paramref name="enemy"/> nulo é tiro
	/// de origem desconhecida: só o lugar.</summary>
	public static void Report(int team, Node enemy, Vector3 position)
	{
		Seen[(team, enemy?.GetInstanceId() ?? 0)] = new Sighting
		{
			Position = position,
			Frame = Engine.GetPhysicsFrames()
		};
	}

	/// <summary>O avistamento fresco mais perto de <paramref name="from"/>,
	/// até <paramref name="range"/> metros.</summary>
	public static bool Nearest(int team, Vector3 from, float range, out Vector3 position)
	{
		position = Vector3.Zero;
		ulong now = Engine.GetPhysicsFrames();
		float best = range;
		bool found = false;

		Stale.Clear();

		foreach (var (key, sighting) in Seen)
		{
			// Partida nova recomeça a contagem de quadros? Não: ela só
			// cresce. O que ficou de outra partida envelhece e sai aqui.
			if (now < sighting.Frame || now - sighting.Frame > FreshFrames)
			{
				Stale.Add(key);
				continue;
			}

			if (key.Team != team) continue;

			float distance = from.DistanceTo(sighting.Position);
			if (distance >= best) continue;

			best = distance;
			position = sighting.Position;
			found = true;
		}

		foreach (var key in Stale) Seen.Remove(key);

		return found;
	}

	public static void Clear() => Seen.Clear();
}
