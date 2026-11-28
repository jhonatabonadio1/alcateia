using Godot;
using System.Threading.Tasks;

/// <summary>
/// Partida autoritativa com a rede oscilando: o servidor dá os mesmos
/// passos que o jogo deu, nem um a mais nem um a menos, então chega onde o
/// jogo previu e o jogo quase não é corrigido. Antes, cada comando que
/// atrasava virava um passo a mais no servidor (ele repetia o último) e
/// cada buraco pulado, um a menos: o jogo era puxado para a frente e para
/// trás o tempo todo (Jhon, 22/11: "o servidor dando umas bugadas, de não
/// deixar eu andar para frente").
/// </summary>
public sealed class JitterWalkTest : WallNetTest
{
	public override string Name => "AndarComRedeOscilando";
	public override string Description => "online, com a rede oscilando (atraso de 50 a 183 ms, um pacote em cada cinco perdido): andando, virando, pulando e de lado, o servidor chega onde o jogo previu e o jogo quase não é corrigido";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live, 30);
		Player p = b.Player;
		await b.Place(p.GlobalPosition, p.RotationDegrees.Y, settle: 0.5);

		MatchWorld world = await StartServer(b);
		var server = new ServerBody(1, 0, world.SpawnBody("Servidor", Teams.Blue));
		server.ApplyLoadout(Profile.OwnedWolfUpgrades());

		Player.NetQuantize = true;
		var up = new JitterLink();
		var down = new JitterLink { DropOneIn = 0 };
		var prediction = new Prediction();
		var recent = new InputCommand[InputCommand.Redundancy];
		var buffer = new byte[256];
		Vector3 spawn = p.GlobalPosition;
		uint seq = 0, tick = 0;
		bool confirmed = false;
		int fixes = 0;
		float worst = 0f;
		const int Settle = 60, End = 6 * 60;

		for (int step = 0; step < End; step++)
		{
			// Anda, vira aos poucos, pula duas vezes, vai de lado e para.
			if (step == 20) RegressionBench.Press("move_forward");
			if (step == 120 || step == 240) RegressionBench.Press("jump");
			if (step == 124 || step == 244) RegressionBench.Release("jump");
			if (step == 180) RegressionBench.Press("move_right");
			if (step == 300)
			{
				RegressionBench.Release("move_forward");
				RegressionBench.Release("move_right");
			}
			if (step >= 60 && step < 180) p.RotateY(Mathf.DegToRad(0.6f));

			await b.Frames(1);
			tick++;

			seq++;
			recent[seq % InputCommand.Redundancy] = Command(p, seq, !confirmed, spawn);
			prediction.Record(seq, p.GlobalPosition.ToNumerics(), p.Velocity.ToNumerics());
			up.Send(tick, NetCommands.InputPacket(recent, seq, buffer));

			foreach (byte[] packet in up.Arrived(tick)) NetCommands.ReadInputPacket(packet, server.Commands);
			server.Step(tick, NetRates.TickSeconds);

			if (tick % NetRates.SnapshotEvery == 0 && server.Placed)
				down.Send(tick, Snapshot(server, server.Commands.LastProcessed));
			foreach (byte[] packet in down.Arrived(tick))
			{
				(uint acked, EntityState e) = ReadSnapshot(packet);
				confirmed = true;
				if (acked == 0) continue;
				System.Numerics.Vector3 fix = prediction.Reconcile(acked, seq, e.Position);
				if (fix == System.Numerics.Vector3.Zero) continue;
				p.GlobalPosition += fix.ToGodot();
				if (step > Settle)
				{
					fixes++;
					worst = Mathf.Max(worst, fix.ToGodot().Length());
				}
			}
		}
		RegressionBench.ReleaseAll();
		Player.NetQuantize = false;

		RegressionBench.Note($"comandos {seq}; o servidor esperou comando {server.Commands.Starved} passo(s) e deu {server.Commands.Skipped} perdido(s) com o último segurado; correções {fixes}, a maior {worst * 100f:0} cm");
		b.AtLeast(20, server.Commands.Starved, "a rede oscilou de verdade: o servidor teve de esperar comando (passos)");
		// Três desde 27/11 (eram duas): o humano anda 10% mais rápido (2,82 m/s, pedido
		// do Jhon), e num dos cinco pacotes perdidos o comando segurado passa a diferir
		// o bastante para corrigir. Conferido: com a velocidade de antes, 2 correções (a
		// maior de 4 cm); com a de agora, 3 (a maior de 3 cm).
		b.AtMost(3, fixes, "correções no jogo andando, virando e pulando com a rede oscilando");
		b.AtMost(0.1f, worst, "a maior correção (m)");

		world.QueueFree();
	}
}
