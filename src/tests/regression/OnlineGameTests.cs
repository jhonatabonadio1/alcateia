using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// Base dos testes em que o jogo de verdade joga online (Jhon, 25/11, no
/// Docker): o <see cref="NetMatch"/> real, com o jogador, a arma e a vida
/// de sempre, liga pela rede ENet num <see cref="TestMatchServer"/> dentro
/// do próprio teste (<see cref="HostedMatch"/> e <see cref="MatchWorld"/>
/// de verdade). Um segundo jogador, sem jogo, fica do lado do servidor
/// (<see cref="FakeOther"/>): o teste manda os comandos e os acertos dele.
/// Autoritativo ou de repasse, pela propriedade <see cref="Authoritative"/>.
/// </summary>
public abstract class OnlineGameTest : RegressionTest
{
	public override string Scene => RegressionBench.Match;

	protected abstract bool Authoritative { get; }
	protected TestMatchServer Server { get; private set; }

	/// <summary>Lugares a mais na escalação (bots, por exemplo), no servidor e no jogo.</summary>
	protected virtual OnlineMatch.Seat[] ExtraSeats => Array.Empty<OnlineMatch.Seat>();

	/// <summary>O mapa da partida, no jogo e no servidor.</summary>
	protected virtual string MapId => "armazem_estendido";

	public override void Prepare()
	{
		RoomConfig.Settings = new RoomSettings { Map = MapId, TeamSize = 4 };
		RoomConfig.Members.Clear();
		RoomConfig.Members.Add(new RoomMember { Name = "Eu", Team = Teams.Blue, IsHost = true });
		RoomConfig.Active = true;

		var tree = (SceneTree)Engine.GetMainLoop();
		Server = new TestMatchServer();
		tree.CurrentScene.AddChild(Server);
		Server.Start(Authoritative, extra: ExtraSeats, map: MapId);

		// A partida online, como o saguão a deixaria (OnlineMatch.Launch).
		Set(nameof(OnlineMatch.Address), "127.0.0.1");
		Set(nameof(OnlineMatch.Port), Server.Port);
		Set(nameof(OnlineMatch.MatchKey), "teste");
		Set(nameof(OnlineMatch.AccountId), TestMatchServer.MeAccount);
		Set(nameof(OnlineMatch.Expires), DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);
		Set(nameof(OnlineMatch.Ticket), "teste");
		OnlineMatch.Roster.Clear();
		OnlineMatch.Roster.Add(new OnlineMatch.Seat(TestMatchServer.MeAccount, "Eu", Teams.Blue, 0));
		OnlineMatch.Roster.Add(new OnlineMatch.Seat(TestMatchServer.OtherAccount, "Outro", Teams.Orange, 0));
		OnlineMatch.Roster.AddRange(ExtraSeats);
		Set(nameof(OnlineMatch.Active), true);
	}

	private static void Set(string name, object value) =>
		typeof(OnlineMatch).GetProperty(name, BindingFlags.Public | BindingFlags.Static).SetValue(null, value);

	/// <summary>Espera o jogo entrar na partida e ela valer; no autoritativo,
	/// também o corpo dele aparecer no mundo do servidor.</summary>
	protected async Task<bool> Connect(RegressionBench b)
	{
		bool joined = await b.Until(() => NetMatch.Current is { Joined: true } && Match.Current?.Phase == MatchPhase.Live, 30);
		b.Check(joined, "o jogo entrou na partida online e ela está valendo");
		if (!joined) return false;
		b.Check(NetMatch.Current.Authoritative == Authoritative, $"modo da partida: {(Authoritative ? "autoritativo" : "repasse")}");
		if (Authoritative)
		{
			bool placed = await b.Until(() => Server.Match.BodyOf(TestMatchServer.MeAccount) is { Alive: true }, 10);
			b.Check(placed, "o corpo do jogador está vivo no mundo do servidor");
			return placed;
		}
		return true;
	}

	/// <summary>Autoritativo: o segundo jogador posto num lugar do chão à vista
	/// do corpo deste jogo no mundo do servidor (de 3 a 8 m), olhando para ele,
	/// e já de corpo no mundo (40 passos). Nulo se não achou lugar.</summary>
	protected async Task<FakeOther> ShooterInSight(RegressionBench b)
	{
		ServerBody mine = Server.Match.BodyOf(TestMatchServer.MeAccount);
		var space = Server.World.Space;
		Vector3 target = mine.Body.GlobalPosition;
		foreach (float reach in new[] { 6f, 4f, 8f, 3f })
			foreach (Vector3 dir in new[] { Vector3.Forward, Vector3.Back, Vector3.Left, Vector3.Right,
				new Vector3(1, 0, 1).Normalized(), new Vector3(-1, 0, 1).Normalized(), new Vector3(1, 0, -1).Normalized(), new Vector3(-1, 0, -1).Normalized() })
			{
				Vector3 c = target + dir * reach;
				var down = space.IntersectRay(PhysicsRayQueryParameters3D.Create(c + Vector3.Up * 0.5f, c + Vector3.Down * 3f));
				if (down.Count == 0) continue;
				Vector3 stand = (Vector3)down["position"] + Vector3.Up * 0.9f;
				if (Mathf.Abs(stand.Y - target.Y) > 0.5f) continue;
				// À vista: nada entre os dois (o próprio corpo dele não conta).
				var sight = PhysicsRayQueryParameters3D.Create(stand + Vector3.Up * 0.6f, target + Vector3.Up * 0.3f);
				sight.Exclude = new Godot.Collections.Array<Rid> { mine.Body.GetRid() };
				if (space.IntersectRay(sight).Count > 0) continue;
				Vector3 look = target + Vector3.Up * 0.3f - (stand + Vector3.Up * 0.6f);
				var fake = new FakeOther(Server, stand, Mathf.Atan2(-look.X, -look.Z), Mathf.Atan2(look.Y, new Vector2(look.X, look.Z).Length()));
				fake.Status();
				for (int i = 0; i < 40; i++) { fake.Step(); await b.PhysicsFrame(); }
				return fake;
			}
		return null;
	}

	/// <summary>O segundo jogador, do lado do servidor: manda os comandos
	/// (parado onde está, olhando para onde se diz) e os acertos.</summary>
	protected sealed class FakeOther
	{
		private readonly TestMatchServer _server;
		private readonly InputCommand[] _recent = new InputCommand[InputCommand.Redundancy];
		private uint _seq;
		public Vector3 At;
		public float Yaw, Pitch;
		public byte Epoch;

		/// <summary>Andar para a frente (de 0 a 127), no comando de cada passo
		/// (no eixo do jogo, para a frente é Y negativo: Input.GetVector).</summary>
		public sbyte Move;

		public FakeOther(TestMatchServer server, Vector3 at, float yaw, float pitch)
		{
			_server = server;
			At = at;
			Yaw = yaw;
			Pitch = pitch;
		}

		/// <summary>Um passo: o comando deste instante.</summary>
		public void Step()
		{
			_seq++;
			_recent[_seq % InputCommand.Redundancy] = new InputCommand
			{
				Seq = _seq, Yaw = Quant.Yaw(Yaw), Pitch = Quant.Pitch(Pitch), Epoch = Epoch, MoveY = (sbyte)(-Move),
				Buttons = Move > 0 ? InputButtons.Forward : InputButtons.None,
				HeldSlot = (byte)Slot.Primary, HasSpawn = true, Spawn = At.ToNumerics()
			};
			_server.FromOther(NetCommands.InputPacket(_recent, _seq, new byte[256]));
		}

		public void Status(string primary = "m4a4", float hp = 200f)
		{
			BinaryWriter w = NetWire.Begin(NetWire.Status, out MemoryStream stream);
			w.Write(hp); w.Write(200f); w.Write(0f); w.Write(0f); w.Write(false);
			w.Write(primary); w.Write("deagle");
			w.Write((short)30); w.Write(false); w.Write((byte)1); w.Write(0f); w.Write(0f);
			w.Write(false);
			_server.FromOther(stream.ToArray());
		}

		/// <summary>Um acerto nesta conta, como o jogo do atirador manda.</summary>
		public void Hit(long target, float amount, Vector3 from, string weapon = "m4a4")
		{
			BinaryWriter w = NetWire.Begin(NetWire.Hit, out MemoryStream stream);
			w.Write(target);
			w.Write(amount);
			w.Write(false);
			w.Write((byte)KillCause.Gun);
			w.Write(weapon);
			w.Write(from);
			w.Write(_seq + 1);
			w.Write((double)_server.Tick - NetRates.InterpolationDelay * NetRates.TickRate);
			_server.FromOther(stream.ToArray());
		}
	}

	/// <summary>Os granadas de verdade e as encenadas que estão no mundo agora.</summary>
	protected static List<Grenade> Grenades(RegressionBench b)
	{
		var found = new List<Grenade>();
		void Walk(Node node)
		{
			if (node is Grenade g) found.Add(g);
			foreach (Node child in node.GetChildren()) Walk(child);
		}
		Walk(b.GetTree().Root);
		return found;
	}
}
