using Godot;
using System.Collections.Generic;

/// <summary>
/// A gravação da partida para a câmera de abate (pedido do Jhon, 18/11:
/// "pode gravar a partida"; 19/11: "deve mostrar o tiro, o barulho, as
/// recargas, tudo, absolutamente tudo que tem na minha câmera").
///
/// Não é vídeo: como no Overwatch, guarda o **estado** de cada corpo
/// (jogador e bots) a cada passo da física — onde está, a velocidade, se
/// pisa o chão, para onde olha, a forma, a arma na mão, a munição, a
/// vida, o escudo, os WP, a carga do especial, as granadas — e os
/// **acontecimentos** no instante em que saem: o tiro, o arremesso, o
/// golpe do lobo, a recarga de habilidade, o acerto, a coronhada, a
/// recarga da arma. E os abates do feed. Só o que cabe em
/// <see cref="Seconds"/> segundos.
///
/// A <see cref="KillCam"/> monta uma cópia de quem matou e a move por
/// esta fita, repetindo os acontecimentos com o mesmo código do jogo —
/// por isso o replay tem o mesmo som, a mesma arma na mão e o mesmo HUD.
/// Só existe na máquina de quem joga.
/// </summary>
public partial class ReplayTape : Node
{
	/// <summary>Quantos segundos a fita guarda.</summary>
	public const double Seconds = 6.0;

	private const int Size = 400;

	/// <summary>Um instante de um corpo.</summary>
	public struct Frame
	{
		public Vector3 At, Velocity;
		public float Yaw, Pitch;
		public bool Grounded, Wolf, Dead;
		public int Form, Slot;
		public string Weapon, Primary, Secondary;
		public float Hp, MaxHp, Shield, MaxShield, Wp, Charge;
		public int Ammo, Mag, Grenades;
	}

	/// <summary>Os acontecimentos que a cópia repete.</summary>
	public enum Kind { Fired, Thrown, WolfMove, Cooldown, Hit, Butt, Reload }

	public struct Event
	{
		public double Time;
		public Kind Kind;
		public int A;
		public float B;
	}

	private sealed class Track
	{
		public readonly double[] Time = new double[Size];
		public readonly Frame[] Frames = new Frame[Size];
		public readonly List<Event> Events = new();
		public int Head, Count;
	}

	public static ReplayTape Current { get; private set; }

	/// <summary>O relógio da fita, em segundos.</summary>
	public double Now { get; private set; }

	private readonly Dictionary<ulong, Track> _tracks = new();
	private readonly List<(double Time, Kill Kill)> _kills = new();

	public override void _EnterTree()
	{
		Current = this;
		KillFeed.Reported += OnKill;
	}

	public override void _ExitTree()
	{
		KillFeed.Reported -= OnKill;
		if (Current == this) Current = null;
	}

	private void OnKill(Kill kill) => _kills.Add((Now, kill));

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
					Listen(body, track);
				}

				track.Time[track.Head] = Now;
				track.Frames[track.Head] = Read(body);
				track.Head = (track.Head + 1) % Size;
				if (track.Count < Size) track.Count++;

				// Acontecimento mais velho que a fita não serve para nada.
				int old = 0;
				while (old < track.Events.Count && track.Events[old].Time < Now - Seconds - 0.5) old++;
				if (old > 0) track.Events.RemoveRange(0, old);
			}

		while (_kills.Count > 0 && _kills[0].Time < Now - Seconds - 0.5) _kills.RemoveAt(0);
	}

	/// <summary>Os acontecimentos da arma de um corpo, no instante em que saem.</summary>
	private void Listen(Node3D body, Track track)
	{
		if (body.GetNodeOrNull<Weapon>("Weapon") is not Weapon gun) return;
		void Add(Kind kind, int a = 0, float b = 0f) => track.Events.Add(new Event { Time = Now, Kind = kind, A = a, B = b });

		gun.Fired += () => Add(Kind.Fired, (int)gun.CurrentSlot);
		gun.Thrown += () => Add(Kind.Thrown);
		gun.WolfMoveStarted += move => Add(Kind.WolfMove, move);
		gun.AttackCooldown += (kind, seconds) => Add(Kind.Cooldown, kind, seconds);
		gun.HitConfirmed += head => Add(Kind.Hit, head ? 1 : 0);
		gun.Butted += () => Add(Kind.Butt);
		gun.AmmoChanged += (_, _, reloading) =>
		{
			if (reloading && (track.Events.Count == 0 || track.Events[^1].Kind != Kind.Reload || Now - track.Events[^1].Time > 0.3))
				Add(Kind.Reload);
		};
	}

	/// <summary>O estado de agora de um corpo, como a fita guarda.</summary>
	public static Frame Read(Node3D body)
	{
		var frame = new Frame { At = body.GlobalPosition, Yaw = body.GlobalRotation.Y, Grounded = true };
		if (body is CharacterBody3D moving)
		{
			frame.Velocity = moving.Velocity;
			frame.Grounded = moving.IsOnFloor();
		}
		if (body.GetNodeOrNull<Node3D>("Head") is Node3D head) frame.Pitch = head.Rotation.X;
		if (body.GetNodeOrNull<Health>("Health") is Health health)
		{
			frame.Hp = health.Hp; frame.MaxHp = health.MaxHp;
			frame.Shield = health.Shield; frame.MaxShield = health.MaxShield;
			frame.Dead = health.IsDead;
		}
		if (body.GetNodeOrNull<FormController>("FormController") is FormController form)
		{
			frame.Wolf = form.ShowsWolf;
			frame.Form = (int)form.Current;
			frame.Wp = form.Wp;
		}
		if (body.GetNodeOrNull<Weapon>("Weapon") is Weapon weapon)
		{
			frame.Slot = (int)weapon.HumanSlot;
			frame.Primary = weapon.PrimaryDef?.Id ?? "";
			frame.Secondary = weapon.SecondaryDef?.Id ?? "";
			frame.Charge = weapon.SpecialCharge;
			frame.Grenades = weapon.Grenades;
			if (!frame.Wolf)
			{
				frame.Weapon = weapon.HeldId;
				frame.Ammo = weapon.AmmoInMag;
				frame.Mag = weapon.MagSize;
			}
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
	/// velocidade, giro e inclinação entre as duas amostras vizinhas.</summary>
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
				frame.Velocity = a.Velocity.Lerp(b.Velocity, u);
				frame.Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, u);
				frame.Pitch = Mathf.Lerp(a.Pitch, b.Pitch, u);
				return true;
			}
			newer = older;
		}

		frame = track.Frames[newer];
		return true;
	}

	/// <summary>Os acontecimentos de um corpo entre dois instantes da fita
	/// (de fora, até dentro), na ordem.</summary>
	public IEnumerable<Event> EventsBetween(Node3D body, double from, double to)
	{
		if (body == null || !_tracks.TryGetValue(body.GetInstanceId(), out Track track)) yield break;
		foreach (Event e in track.Events)
			if (e.Time > from && e.Time <= to) yield return e;
	}

	/// <summary>Quantos tiros o corpo deu entre dois instantes da fita.</summary>
	public int ShotsBetween(Node3D body, double from, double to)
	{
		int count = 0;
		foreach (Event e in EventsBetween(body, from, to))
			if (e.Kind == Kind.Fired) count++;
		return count;
	}

	/// <summary>Os abates do feed entre dois instantes da fita.</summary>
	public IEnumerable<Kill> KillsBetween(double from, double to)
	{
		foreach (var (time, kill) in _kills)
			if (time > from && time <= to) yield return kill;
	}
}
