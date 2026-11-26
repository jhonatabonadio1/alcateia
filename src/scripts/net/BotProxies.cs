using Godot;
using System.Collections.Generic;

/// <summary>
/// Os bots no mundo do servidor de partida (partida autoritativa).
///
/// O bot é movido pela máquina do dono (decisão 0008), e o servidor não
/// tinha corpo nenhum para ele: no jogo, o lobo que corria, arranhava ou
/// dava o bote em cima de um bot batia na cápsula dele e parava; no
/// servidor, o mesmo passo atravessava o vazio. A diferença voltava como
/// correção, e o lobo era puxado para dentro do bot e para trás de novo
/// (Jhon, 25/11: "com movimentos complexos dá uma lagada, volta no tempo";
/// "avança no inimigo, mata (ou não) e volta para trás").
///
/// Cada bot ganha aqui uma cápsula do tamanho do corpo dele, posta onde o
/// dono disse por último (<see cref="NetWire.BotState"/>) e andando com a
/// velocidade dele até <see cref="MaxAhead"/> s depois — o mesmo que o
/// boneco faz nas outras máquinas. É um corpo de personagem
/// (<see cref="CharacterBody3D"/>, com <see cref="Health"/> e time): o
/// movimento trata gente diferente de parede (não corre na parede de um
/// bot, o salto do especial atravessa quem não é o alvo, o bote para em
/// inimigo à frente), e aqui ele tem de ser tratado igual. Aliado
/// atravessa (<see cref="TeamCollision"/>). Morto, sai.
///
/// Não é confiança nova no dono: ele já decide onde o bot dele está (é
/// contra isso que o servidor confere o tiro no bot, HostedMatch.PoseOf).
/// </summary>
public sealed class BotProxies
{
	/// <summary>Quanto tempo depois do último estado a cápsula ainda anda
	/// com a velocidade dele (o estado vem a 30 por segundo).</summary>
	public const double MaxAhead = 0.1;

	/// <summary>Sem estado novo há tanto tempo (o dono caiu e ninguém herdou
	/// o bot), a cápsula sai: não fica um bot invisível parado no mapa.</summary>
	public const double StaleAfter = 1.0;

	/// <summary>A cápsula agachada (a do humano, Player.HumanCrouchHeight: o bot
	/// agacha de arma na mão), encolhida em torno do centro, como a dele.</summary>
	public const float CrouchHeight = Player.DefaultHumanCrouchHeight, StandHeight = 1.75f;

	private sealed class Proxy
	{
		public CharacterBody3D Body;
		public CapsuleShape3D Capsule;
		public Health Health;
		public Vector3 At, Velocity;
		public double Age;
		public int CollisionTeam = TeamCollision.None;
	}

	private readonly Node _world;
	private readonly Dictionary<long, Proxy> _proxies = new();

	public BotProxies(Node world) => _world = world;

	public int Count => _proxies.Count;

	/// <summary>A cápsula do bot, se ele está no mundo (para o teste).</summary>
	public CharacterBody3D BodyOf(long id) => _proxies.TryGetValue(id, out Proxy p) ? p.Body : null;

	/// <summary>O dono contou onde o bot está e se está vivo.</summary>
	public void Report(long id, int team, Vector3 position, Vector3 velocity, bool dead, bool crouching = false)
	{
		if (dead || !position.IsFinite() || !velocity.IsFinite())
		{
			Remove(id);
			return;
		}

		if (!_proxies.TryGetValue(id, out Proxy proxy))
		{
			proxy = new Proxy();
			proxy.Body = Build(id, out proxy.Capsule, out proxy.Health);
			_world.AddChild(proxy.Body);
			_proxies[id] = proxy;
		}
		proxy.Health.Team = team;
		proxy.Capsule.Height = crouching ? CrouchHeight : StandHeight;
		proxy.At = position;
		proxy.Velocity = velocity;
		proxy.Age = 0;
		Place(proxy);
	}

	public void Remove(long id)
	{
		if (!_proxies.Remove(id, out Proxy proxy)) return;
		if (GodotObject.IsInstanceValid(proxy.Body)) proxy.Body.QueueFree();
	}

	public void Clear()
	{
		foreach (long id in new List<long>(_proxies.Keys)) Remove(id);
	}

	/// <summary>Um passo do servidor, antes dos corpos dos jogadores: cada
	/// cápsula anda o que a velocidade do bot manda, até
	/// <see cref="MaxAhead"/> depois do último estado.</summary>
	public void Step(double delta)
	{
		List<long> stale = null;
		foreach (var (id, proxy) in _proxies)
		{
			proxy.Age += delta;
			if (proxy.Age > StaleAfter) (stale ??= new List<long>()).Add(id);
			else Place(proxy);
		}
		if (stale != null)
			foreach (long id in stale) Remove(id);
	}

	/// <summary>As cápsulas, para quem procura só a parede do mapa (a
	/// conferência do tiro) as deixar de fora.</summary>
	public IEnumerable<Rid> Rids()
	{
		foreach (Proxy proxy in _proxies.Values)
			if (GodotObject.IsInstanceValid(proxy.Body)) yield return proxy.Body.GetRid();
	}

	private static void Place(Proxy proxy)
	{
		if (!proxy.Body.IsInsideTree()) return;
		float ahead = (float)System.Math.Min(proxy.Age, MaxAhead);
		proxy.Body.GlobalPosition = proxy.At + proxy.Velocity * ahead;
		proxy.Body.Velocity = proxy.Velocity;
		// Aliado atravessa: o par se refaz quando o time muda (e na chegada).
		TeamCollision.Keep(proxy.Body, proxy.Health, ref proxy.CollisionTeam);
	}

	/// <summary>A cápsula: a mesma do bot (bot.tscn), num corpo de
	/// personagem que não anda sozinho.</summary>
	private static CharacterBody3D Build(long id, out CapsuleShape3D capsule, out Health health)
	{
		var body = new CharacterBody3D { Name = $"Bot{id}" };
		capsule = new CapsuleShape3D { Radius = 0.3f, Height = StandHeight };
		body.AddChild(new CollisionShape3D { Shape = capsule });
		health = new Health { Name = "Health", AutoRespawn = false };
		body.AddChild(health);
		body.AddToGroup(Groups.ServerBodies);
		return body;
	}
}
