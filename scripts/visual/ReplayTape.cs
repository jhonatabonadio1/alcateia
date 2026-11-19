using Godot;
using System.Collections.Generic;

/// <summary>
/// A gravação da partida para a câmera de abate (pedido do Jhon, 18/11:
/// "pode gravar a partida"): a cada passo da física guarda, de cada corpo
/// (jogador e bots), onde está, para onde olha (o giro e a inclinação da
/// cabeça), a arma na mão, a munição, a vida, o escudo, se é lobo — e os
/// tiros, no instante em que saem. Fica só o que cabe em
/// <see cref="Seconds"/> segundos.
///
/// A <see cref="KillCam"/> lê daqui e passa de novo os últimos segundos
/// **do olho de quem matou**, com a arma dele na mão, a mira dele e o HUD
/// dele, e põe a vítima de pé para refazer o caminho até cair.
///
/// É uma fita curta e leve, um anel por corpo. Só existe na máquina de
/// quem joga.
/// </summary>
public partial class ReplayTape : Node
{
	/// <summary>Quantos segundos a fita guarda.</summary>
	public const double Seconds = 5.0;

	private const int Size = 320;

	/// <summary>Um instante de um corpo.</summary>
	public struct Frame
	{
		public Vector3 At;
		public float Yaw, Pitch;
		public string Weapon;
		public bool Wolf;
		public float Hp, MaxHp, Shield, MaxShield;
		public int Ammo, Mag;
	}

	private sealed class Track
	{
		public readonly double[] Time = new double[Size];
		public readonly Frame[] Frames = new Frame[Size];
		public readonly List<double> Shots = new();
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
				if (!_tracks.TryGetValue(id, out Track track))
				{
					_tracks[id] = track = new Track();
					// Os tiros, no instante em que saem.
					if (body.GetNodeOrNull<Weapon>("Weapon") is Weapon gun)
						gun.Fired += () => track.Shots.Add(Now);
				}

				track.Time[track.Head] = Now;
				track.Frames[track.Head] = Read(body);
				track.Head = (track.Head + 1) % Size;
				if (track.Count < Size) track.Count++;

				// Tiro mais velho que a fita não serve para nada.
				while (track.Shots.Count > 0 && track.Shots[0] < Now - Seconds - 0.5) track.Shots.RemoveAt(0);
			}
	}

	/// <summary>O estado de agora de um corpo, como a fita guarda. Também
	/// serve ao espectador ao vivo.</summary>
	public static Frame Read(Node3D body)
	{
		var frame = new Frame { At = body.GlobalPosition, Yaw = body.GlobalRotation.Y };
		if (body.GetNodeOrNull<Node3D>("Head") is Node3D head) frame.Pitch = head.Rotation.X;
		if (body.GetNodeOrNull<Health>("Health") is Health health)
		{
			frame.Hp = health.Hp; frame.MaxHp = health.MaxHp;
			frame.Shield = health.Shield; frame.MaxShield = health.MaxShield;
		}
		if (body.GetNodeOrNull<FormController>("FormController") is FormController form) frame.Wolf = form.ShowsWolf;
		if (body.GetNodeOrNull<Weapon>("Weapon") is Weapon weapon && !frame.Wolf)
		{
			frame.Weapon = weapon.HeldId;
			frame.Ammo = weapon.AmmoInMag;
			frame.Mag = weapon.MagSize;
		}
		return frame;
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
		bool ok = Sample(body, time, out Frame frame);
		at = frame.At; yaw = frame.Yaw;
		return ok;
	}

	/// <summary>O instante <paramref name="time"/> de um corpo, com posição,
	/// giro e inclinação entre as duas amostras vizinhas. Falso se não há fita.</summary>
	public bool Sample(Node3D body, double time, out Frame frame)
	{
		frame = default;
		if (body == null || !_tracks.TryGetValue(body.GetInstanceId(), out Track track) || track.Count == 0) return false;

		// Do mais novo para o mais velho, até achar o par que cerca o instante.
		int newer = (track.Head - 1 + Size) % Size;
		if (time >= track.Time[newer]) { frame = track.Frames[newer]; return true; }
		for (int i = 1; i < track.Count; i++)
		{
			int older = (newer - 1 + Size) % Size;
			if (time >= track.Time[older])
			{
				double span = track.Time[newer] - track.Time[older];
				float u = span > 1e-6 ? (float)((time - track.Time[older]) / span) : 0f;
				Frame a = track.Frames[older], b = track.Frames[newer];
				frame = u < 0.5f ? a : b;
				frame.At = a.At.Lerp(b.At, u);
				frame.Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, u);
				frame.Pitch = Mathf.Lerp(a.Pitch, b.Pitch, u);
				return true;
			}
			newer = older;
		}

		frame = track.Frames[newer];
		return true;
	}

	/// <summary>Quantos tiros o corpo deu entre dois instantes da fita
	/// (de fora, até dentro).</summary>
	public int ShotsBetween(Node3D body, double from, double to)
	{
		if (body == null || !_tracks.TryGetValue(body.GetInstanceId(), out Track track)) return 0;
		int count = 0;
		foreach (double t in track.Shots)
			if (t > from && t <= to) count++;
		return count;
	}
}
