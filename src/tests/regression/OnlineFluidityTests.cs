using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A partida online fluida com a rede ruim (Jhon, 26/11: "microtravamentos
/// no servidor, às vezes os personagens voltam no tempo e a tela fica
/// tremendo com glitches, isso não pode acontecer, precisa ser um jogo
/// fluido, independente da condição do servidor").
///
/// O jogo de verdade, ligado pela rede a um servidor de partida no teste
/// (<see cref="TestMatchServer"/>), corre em círculo; o outro jogador
/// também. Cada condição de rede roda seis segundos medidos: atraso (meia
/// ida e volta de cada lado), um sorteio a mais em cada pacote (jitter),
/// pacotes perdidos e o servidor engasgando (para de 0,1 a 0,3 s e depois
/// recupera os passos de uma vez). Quadro a quadro, o que se vê:
///
/// - a câmera do jogador (o desenho, depois do alisamento): quadros em que
///   a velocidade muda mais de <see cref="JoltSpeed"/> de um quadro para o
///   outro (tranco, tremor) e em que ela anda para trás (volta no tempo);
/// - o boneco do outro (o modelo desenhado): o mesmo;
/// - as correções do servidor no corpo do jogador (quantas, quanto).
///
/// A rede limpa é a régua: o tranco que aparece nela é do próprio
/// movimento (encostar em algo), não da rede.
/// </summary>
public abstract partial class OnlineFluidityTest : OnlineGameTest
{
	private sealed record NetCondition(string Name, double Latency, double Jitter, double Loss, double StallEvery);

	private static readonly NetCondition[] Conditions =
	{
		new("limpa", 0, 0, 0, 0),
		new("80 ms", 0.04, 0.04, 0.02, 0),
		new("150 ms", 0.075, 0.04, 0.05, 0),
		new("250 ms", 0.125, 0.04, 0.05, 0),
		new("engasgos", 0.04, 0.02, 0.02, 1.5),
	};

	/// <summary>Quanto se mede em cada condição, em segundos (e antes, para a rede assentar).</summary>
	private const double Warm = 1.5, Measured = 6.0;

	/// <summary>Quantos radianos por segundo os dois viram, correndo: um círculo de uns 2 m de raio.</summary>
	private const float TurnRate = 1.6f;

	/// <summary>A velocidade mudando mais que isto num quadro é tranco que se vê (m/s).</summary>
	public const float JoltSpeed = 2f;

	/// <summary>O que se vê, um quadro por linha, gravado depois de todo o desenho.</summary>
	private sealed partial class Sampler : Node
	{
		public Node3D Camera, Puppet;
		public bool On;
		public readonly List<Vector3> Cam = new(), Pup = new();

		public override void _Ready() => ProcessPriority = int.MaxValue;

		public override void _Process(double delta)
		{
			if (!On) return;
			if (IsInstanceValid(Camera)) Cam.Add(Camera.GlobalPosition);
			if (IsInstanceValid(Puppet) && Puppet.IsVisibleInTree()) Pup.Add(Puppet.GlobalPosition);
		}
	}

	/// <summary>Quadros com tranco, quadros andando para trás, e o maior tranco (m/s).</summary>
	public static (int Jolts, int Backward, float Worst) Roughness(List<Vector3> path, double dt)
	{
		int jolts = 0, backward = 0;
		float worst = 0f;
		for (int i = 2; i < path.Count; i++)
		{
			Vector3 a = path[i - 1] - path[i - 2], c = path[i] - path[i - 1];
			if (a.Length() < 0.002f && c.Length() < 0.002f) continue;
			float change = (c - a).Length() / (float)dt;
			worst = Mathf.Max(worst, change);
			if (change > JoltSpeed) jolts++;
			var fa = new Vector3(a.X, 0, a.Z);
			var fc = new Vector3(c.X, 0, c.Z);
			if (fa.Length() > 0.003f && fc.Length() > 0.003f && fa.Dot(fc) < 0f) backward++;
		}
		return (jolts, backward, worst);
	}

	public override async Task Run(RegressionBench b)
	{
		if (!await Connect(b)) return;
		string mode = Authoritative ? "autoritativo" : "repasse";
		Player p = b.Player;
		Health me = b.Health;
		await b.Until(() => !me.IsProtected, 6.0);

		FakeOther other = Authoritative ? await ShooterInSight(b) : null;
		if (Authoritative && other == null)
		{
			b.Check(false, "achou lugar para o outro jogador correr");
			return;
		}
		if (other != null) other.Move = 127;

		Vector3 center = p.GlobalPosition + new Vector3(4f, 0f, 0f);
		if (!Authoritative)
		{
			var space = p.GetWorld3D().DirectSpaceState;
			var down = space.IntersectRay(PhysicsRayQueryParameters3D.Create(center + Vector3.Up * 2f, center + Vector3.Down * 4f));
			if (down.Count > 0) center = (Vector3)down["position"] + Vector3.Up * 0.875f;
		}

		var sampler = new Sampler();
		b.GetTree().Root.AddChild(sampler);
		Bot puppet = null;

		RegressionBench.Press("move_forward");
		double dt = 1.0 / Engine.PhysicsTicksPerSecond;
		float relayAngle = 0f;
		int frame = 0;
		var results = new List<(string Name, int CamJolts, int CamBack, int PupJolts, int PupBack)>();

		foreach (NetCondition net in Conditions)
		{
			Server.Latency = net.Latency;
			Server.Jitter = net.Jitter;
			Server.Loss = net.Loss;
			Server.StallEvery = net.StallEvery;
			int stallsBefore = Server.Stalls, droppedBefore = Server.Dropped;
			int fixesBefore = 0;
			float sumBefore = 0f, maxBefore = 0f;

			sampler.On = false;
			sampler.Cam.Clear();
			sampler.Pup.Clear();
			double elapsed = 0;
			while (elapsed < Warm + Measured)
			{
				// Correndo em círculo (o jogador e o outro).
				p.Rotation = new Vector3(0, p.Rotation.Y + TurnRate * (float)dt, 0);
				if (other != null)
				{
					other.Yaw += TurnRate * (float)dt;
					other.Step();
				}
				else if (frame % 2 == 0)
				{
					RelayState(center, ref relayAngle, (float)dt * 2f);
				}

				puppet ??= b.GetTree().GetNodesInGroup("bot").OfType<Bot>().FirstOrDefault(x => x.RemoteId == TestMatchServer.OtherAccount);
				sampler.Camera = p.GetViewport().GetCamera3D();
				sampler.Puppet = puppet?.GetNodeOrNull<Node3D>("Human");
				bool measuring = elapsed >= Warm;
				if (measuring && !sampler.On)
				{
					fixesBefore = NetMatch.Current.Corrections;
					sumBefore = NetMatch.Current.CorrectionSum;
					maxBefore = NetMatch.Current.CorrectionMax;
				}
				sampler.On = measuring;

				await b.PhysicsFrame();
				frame++;
				elapsed += dt;
			}
			sampler.On = false;

			var cam = Roughness(sampler.Cam, dt);
			var pup = Roughness(sampler.Pup, dt);
			int fixes = NetMatch.Current.Corrections - fixesBefore;
			float pulled = NetMatch.Current.CorrectionSum - sumBefore;
			float biggest = NetMatch.Current.CorrectionMax > maxBefore ? NetMatch.Current.CorrectionMax : 0f;
			results.Add((net.Name, cam.Jolts, cam.Backward, pup.Jolts, pup.Backward));
			RegressionBench.Note($"{mode} {net.Name,-9}: câmera {cam.Jolts} trancos / {cam.Backward} para trás (pior {cam.Worst:0.0} m/s, {sampler.Cam.Count} quadros) | " +
				$"boneco {pup.Jolts} trancos / {pup.Backward} para trás (pior {pup.Worst:0.0} m/s, {sampler.Pup.Count} quadros) | " +
				$"correções {fixes} ({pulled:0.00} m{(biggest > 0 ? $", a maior {biggest:0.00} m" : "")}) | " +
				$"engasgos {Server.Stalls - stallsBefore}, pacotes perdidos {Server.Dropped - droppedBefore}");
		}
		RegressionBench.ReleaseAll();
		sampler.QueueFree();
		Server.Latency = Server.Jitter = Server.Loss = Server.StallEvery = 0;

		// A rede limpa é a régua: o tranco que aparece nela é do próprio movimento.
		var clean = results[0];
		foreach (var r in results.Skip(1))
		{
			b.Check(r.CamJolts <= clean.CamJolts + 1 && r.CamBack == 0,
				$"{mode}, rede {r.Name}: a câmera não dá tranco nem volta no tempo ({r.CamJolts} trancos, limpa {clean.CamJolts}; {r.CamBack} para trás)");
			b.Check(r.PupJolts <= clean.PupJolts + 2 && r.PupBack == 0,
				$"{mode}, rede {r.Name}: o outro jogador anda liso, sem voltar no tempo ({r.PupJolts} trancos, limpa {clean.PupJolts}; {r.PupBack} para trás)");
		}
	}

	/// <summary>Repasse: o jogo do outro jogador conta onde ele está (30 por
	/// segundo), correndo num círculo em volta de <paramref name="center"/>.</summary>
	private void RelayState(Vector3 center, ref float angle, float step)
	{
		const float radius = 2.2f;
		angle += TurnRate * step;
		Vector3 at = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
		Vector3 velocity = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius * TurnRate;
		var state = new NetState
		{
			Position = at, Velocity = velocity, Yaw = Mathf.Atan2(-velocity.X, -velocity.Z), Grounded = true,
			Form = (byte)Form.Human, Slot = (byte)Slot.Primary, Hp = 200, MaxHp = 200,
			Primary = "m4a4", Secondary = "deagle", HasGear = true
		};
		BinaryWriter w = NetWire.Begin(NetWire.State, out MemoryStream stream);
		state.Write(w);
		Server.FromOther(stream.ToArray());
	}
}

public sealed class OnlineFluidityAuthoritativeTest : OnlineFluidityTest
{
	public override string Name => "FluidezAutoritativo";
	public override string Description => "online autoritativo com rede ruim (80/150/250 ms, jitter, perda, servidor engasgando): a câmera e o outro jogador andam lisos, sem tranco nem volta no tempo";
	protected override bool Authoritative => true;
}

public sealed class OnlineFluidityRelayTest : OnlineFluidityTest
{
	public override string Name => "FluidezRepasse";
	public override string Description => "online de repasse com rede ruim (80/150/250 ms, jitter, perda, servidor engasgando): o outro jogador anda liso, sem tranco nem volta no tempo";
	protected override bool Authoritative => false;
}
