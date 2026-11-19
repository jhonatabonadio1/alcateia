using Godot;
using System.Collections.Generic;

/// <summary>
/// A gravação da partida para a câmera de abate (pedido do Jhon, 18/11:
/// "pode gravar a partida"): a cada passo da física guarda onde está e
/// para onde olha cada corpo (jogador e bots), nos últimos
/// <see cref="Seconds"/> segundos. Quando alguém morre, a
/// <see cref="KillCam"/> lê daqui o caminho de quem matou e o de quem
/// morreu e passa de novo, do olho do matador.
///
/// É uma fita curta e leve: um anel por corpo, com posição e giro — 300
/// amostras de 16 bytes para cada um. Só existe na máquina de quem joga.
/// </summary>
public partial class ReplayTape : Node
{
	/// <summary>Quantos segundos a fita guarda.</summary>
	public const double Seconds = 5.0;

	private const int Size = 320;

	private sealed class Track
	{
		public readonly double[] Time = new double[Size];
		public readonly Vector3[] At = new Vector3[Size];
		public readonly float[] Yaw = new float[Size];
		public int Head, Count;
	}

	public static ReplayTape Current { get; private set; }

	/// <summary>O relógio da fita, em segundos.</summary>
	public double Now { get; private set; }

	private readonly Dictionary<ulong, Track> _tracks = new();

	public override void _EnterTree() => Current = this;
	public override void _ExitTree() { if (Current == this) Current = null; }

	public override void _PhysicsProcess(double delta)
	{
		Now += delta;
		foreach (string group in Groups.Fighters)
			foreach (Node node in GetTree().GetNodesInGroup(group))
			{
				if (node is not Node3D body) continue;
				// O corpo que está sendo reencenado não regrava por cima de si.
				if (body is Player { Replaying: true }) continue;

				ulong id = body.GetInstanceId();
				if (!_tracks.TryGetValue(id, out Track track)) _tracks[id] = track = new Track();
				track.Time[track.Head] = Now;
				track.At[track.Head] = body.GlobalPosition;
				track.Yaw[track.Head] = body.GlobalRotation.Y;
				track.Head = (track.Head + 1) % Size;
				if (track.Count < Size) track.Count++;
			}
	}

	/// <summary>Quanto tempo de fita há para este corpo, até agora.</summary>
	public double Recorded(Node3D body)
	{
		if (body == null || !_tracks.TryGetValue(body.GetInstanceId(), out Track track) || track.Count == 0) return 0;
		int oldest = (track.Head - track.Count + Size) % Size;
		return Now - track.Time[oldest];
	}

	/// <summary>Onde o corpo estava e para onde virava no instante
	/// <paramref name="time"/> (relógio da fita). Falso se não há fita.</summary>
	public bool Sample(Node3D body, double time, out Vector3 at, out float yaw)
	{
		at = Vector3.Zero; yaw = 0f;
		if (body == null || !_tracks.TryGetValue(body.GetInstanceId(), out Track track) || track.Count == 0) return false;

		// Do mais novo para o mais velho, até achar o par que cerca o instante.
		int newer = (track.Head - 1 + Size) % Size;
		if (time >= track.Time[newer]) { at = track.At[newer]; yaw = track.Yaw[newer]; return true; }
		for (int i = 1; i < track.Count; i++)
		{
			int older = (newer - 1 + Size) % Size;
			if (time >= track.Time[older])
			{
				double span = track.Time[newer] - track.Time[older];
				float u = span > 1e-6 ? (float)((time - track.Time[older]) / span) : 0f;
				at = track.At[older].Lerp(track.At[newer], u);
				yaw = Mathf.LerpAngle(track.Yaw[older], track.Yaw[newer], u);
				return true;
			}
			newer = older;
		}

		at = track.At[newer]; yaw = track.Yaw[newer];
		return true;
	}
}
