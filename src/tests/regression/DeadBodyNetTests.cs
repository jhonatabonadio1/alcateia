using Godot;
using System.Threading.Tasks;

/// <summary>
/// Partida autoritativa: o corpo de quem morreu não barra ninguém no
/// servidor (Jhon, 22/11: "o servidor dando umas bugadas, de não deixar eu
/// andar para frente, mas foi só por um tempo, depois normalizou"). Morto,
/// o corpo do servidor não dá passo, então nunca assentava (é ao assentar
/// que a colisão de um morto sai): a cápsula ficava sólida, invisível, onde
/// ele morreu, até renascer. Quem passava por ali era barrado no servidor e
/// puxado de volta no jogo, que já não via ninguém. Renascido, o corpo
/// volta a ser sólido.
/// </summary>
public sealed class DeadBodyServerTest : NetcodeTest
{
	public override string Name => "MortoNaoBarra";
	public override string Description => "online: no servidor, o corpo de quem morreu não barra quem passa por onde ele caiu; renascido, volta a barrar";

	/// <summary>Um jogo de mentira: manda um comando por passo, andando
	/// para frente ou parado, sempre olhando para o mesmo lado.</summary>
	private sealed class Driver
	{
		public readonly ServerBody Server;
		public byte Epoch;
		public Vector3 Spawn;
		public bool Forward;
		private readonly float _yaw;
		private uint _seq;

		public Driver(ServerBody server, Vector3 spawn, float yaw)
		{
			Server = server;
			Spawn = spawn;
			_yaw = yaw;
		}

		public void Send()
		{
			_seq++;
			Server.Commands.Push(new InputCommand
			{
				Seq = _seq,
				Yaw = Quant.Yaw(_yaw),
				Epoch = Epoch,
				HeldSlot = (byte)Slot.Primary,
				HasSpawn = true,
				Spawn = Spawn.ToNumerics(),
				MoveY = Forward ? (sbyte)-127 : (sbyte)0,
				Buttons = Forward ? InputButtons.Forward : InputButtons.None
			});
		}
	}

	private uint _tick;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		MatchWorld world = await StartServer(b);
		PhysicsDirectSpaceState3D space = world.Space;

		// Um corredor livre de 9 m a partir de uma base, no chão.
		Vector3 start = Vector3.Inf, along = Vector3.Zero;
		foreach (Vector3 spawn in world.SpawnsFor(Referee.Blue))
		{
			Vector3 floor = Floor(space, spawn);
			if (floor == Vector3.Inf) continue;
			for (int i = 0; i < 16 && start == Vector3.Inf; i++)
			{
				float angle = Mathf.Tau * i / 16f;
				var dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
				if (Clear(space, floor, dir, 9f)) { start = floor; along = dir; }
			}
			if (start != Vector3.Inf) break;
		}
		b.Check(start != Vector3.Inf, "achou um corredor livre de 9 m no chão");
		if (start == Vector3.Inf) return;

		float yaw = Mathf.Atan2(-along.X, -along.Z);
		Vector3 deadAt = start + along * 2.5f;
		var walker = new Driver(new ServerBody(1, 0, world.SpawnBody("Andador", Referee.Blue)), start + Vector3.Up * 0.2f, yaw);
		var fallen = new Driver(new ServerBody(2, 1, world.SpawnBody("Caido", Referee.Orange)), deadAt + Vector3.Up * 0.2f, yaw);

		await Steps(b, 30, walker, fallen);
		b.Check(walker.Server.Alive && fallen.Server.Alive, "os dois no mundo do servidor");

		// Vivo no caminho: barra (o teste mede o que deve medir).
		Vector3 from = walker.Server.Body.GlobalPosition;
		walker.Forward = true;
		await Steps(b, 70, walker, fallen);
		float blockedAlive = (walker.Server.Body.GlobalPosition - from).Dot(along);
		walker.Forward = false;
		b.AtMost(2.3f, blockedAlive, "vivo no caminho, o corpo dele barra quem anda (m andados até ele)");

		// Morre ali. Quem anda passa por onde ele caiu.
		Health health = fallen.Server.Health;
		health.Mirror(0, health.MaxHp, 0, health.MaxShield, true);
		await Steps(b, 5, walker, fallen);
		b.Check(fallen.Server.Health.IsDead, "morreu no servidor");
		from = walker.Server.Body.GlobalPosition;
		walker.Forward = true;
		await Steps(b, 70, walker, fallen);
		float passed = (walker.Server.Body.GlobalPosition - deadAt).Dot(along);
		walker.Forward = false;
		RegressionBench.Note($"vivo: andou {blockedAlive:0.00} m até ele; morto: passou {passed:0.00} m além de onde ele caiu");
		b.AtLeast(1.5f, passed, "morto, o corpo dele não barra: quem anda passa por onde ele caiu (m além dele)");

		// Renasce no caminho de novo: volta a barrar.
		ServerBody.AllowTeleport = true;
		try
		{
			Vector3 ahead = walker.Server.Body.GlobalPosition + along * 2.5f;
			fallen.Epoch++;
			fallen.Spawn = ahead + Vector3.Up * 0.2f;
			await Steps(b, 10, walker, fallen);
			b.Check(fallen.Server.Alive, "renasceu no servidor");

			from = walker.Server.Body.GlobalPosition;
			walker.Forward = true;
			await Steps(b, 70, walker, fallen);
			float blockedAgain = (walker.Server.Body.GlobalPosition - from).Dot(along);
			walker.Forward = false;
			b.AtMost(2.3f, blockedAgain, "renascido, o corpo dele volta a barrar (m andados até ele)");
		}
		finally
		{
			ServerBody.AllowTeleport = false;
		}

		world.QueueFree();
	}

	private async Task Steps(RegressionBench b, int count, params Driver[] drivers)
	{
		for (int i = 0; i < count; i++)
		{
			_tick++;
			foreach (Driver d in drivers) d.Send();
			foreach (Driver d in drivers) d.Server.Step(_tick, NetRates.TickSeconds);
			await b.PhysicsFrame();
		}
	}

	private static Vector3 Floor(PhysicsDirectSpaceState3D space, Vector3 at)
	{
		var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 1.2f, at + Vector3.Down * 6f);
		Godot.Collections.Dictionary hit = space.IntersectRay(query);
		return hit.Count == 0 ? Vector3.Inf : (Vector3)hit["position"] + Vector3.Up * 0.9f;
	}

	/// <summary>Nada no caminho (na altura do joelho e do peito) e chão
	/// na mesma altura em todo ele.</summary>
	private static bool Clear(PhysicsDirectSpaceState3D space, Vector3 floor, Vector3 dir, float length)
	{
		foreach (float h in new[] { -0.5f, 0f, 0.5f })
		{
			Vector3 from = floor + Vector3.Up * h;
			if (space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + dir * length)).Count > 0) return false;
		}
		for (float d = 0.5f; d <= length; d += 0.5f)
		{
			Vector3 at = Floor(space, floor + dir * d);
			if (at == Vector3.Inf || Mathf.Abs(at.Y - floor.Y) > 0.15f) return false;
		}
		return true;
	}
}
