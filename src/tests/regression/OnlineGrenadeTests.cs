using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A granada no pé, no jogo online de verdade (Jhon, 25/11: "a granada no pé
/// não te mata: ao jogar a granada exatamente no pé, olhando 100% para
/// baixo"). Offline mata. Aqui o jogo real joga num servidor de partida
/// (<see cref="TestMatchServer"/>) e se mede o que nasce (quantas
/// granadas, de verdade ou encenadas), onde pousa, o que a vida do jogo
/// recebe, o que o servidor manda de volta e se o jogador morre e fica morto.
/// </summary>
public abstract class OnlineGrenadeBase : OnlineGameTest
{
	protected abstract float Pitch { get; }

	/// <summary>Joga a granada logo depois de renascer (a proteção de quem
	/// nasceu dura 3 s), como quem morre e testa de novo na hora.</summary>
	protected virtual bool AfterRespawn => false;

	/// <summary>Quanto o botão fica apertado antes de soltar a granada (s).</summary>
	protected virtual double Hold => 1.0;

	public override async Task Run(RegressionBench b)
	{
		if (!await Connect(b)) return;
		Player p = b.Player;
		Health me = b.Health;
		string mode = Authoritative ? "autoritativo" : "repasse";

		if (AfterRespawn)
		{
			// Morre (queda) e renasce pela escolha de conjunto, como no jogo.
			me.TakeDamage(9999f);
			b.Check(await b.Until(() => me.IsDead, 2.0), "morreu para renascer");
			await b.Seconds(0.5);
			bool back = false;
			for (int i = 0; i < 60 && !back; i++)
			{
				b.CloseLoadout();
				back = await b.Until(() => !me.IsDead, 0.5);
			}
			b.Check(back, "renasceu");
		}
		// Sem renascer agora, a proteção de quem nasceu já passou.
		else await b.Until(() => !me.IsProtected, 6.0);
		b.Weapon.Equip(b.Weapon.PrimaryDef, b.Weapon.SecondaryDef, 3);
		await b.Tap("slot_5");
		await b.Seconds(0.6 + b.Weapon.PrimaryDef.HolsterTime);
		p.GetNode<Node3D>("Head").Rotation = new Vector3(Mathf.DegToRad(Pitch), 0, 0);
		await b.Frames(3);

		ServerBody serverBody = Authoritative ? Server.Match.BodyOf(TestMatchServer.MeAccount) : null;
		int feedBefore = KillFeed.Recent.Count;
		int killsToMeBefore = Server.CountToMe(NetWire.Kill);
		Vector3 feet = p.GlobalPosition;
		int maxReal = 0, maxCosmetic = 0;
		float hpMin = me.Hp;
		var track = new List<string>();

		bool protectedAtThrow = me.IsProtected;
		RegressionBench.Press("attack");
		await b.Seconds(Hold);
		RegressionBench.Release("attack");
		double t = 0, protectedFor = 0;
		bool exploded = false;
		Grenade watched = null;
		while (t < 6.0 && !me.IsDead)
		{
			await b.Frames(6);
			t += 0.1;
			List<Grenade> all = Grenades(b);
			int real = all.Count(g => !g.Cosmetic), cosmetic = all.Count(g => g.Cosmetic);
			maxReal = Mathf.Max(maxReal, real);
			maxCosmetic = Mathf.Max(maxCosmetic, cosmetic);
			Grenade mine = all.FirstOrDefault(g => !g.Cosmetic);
			if (mine != null) watched = mine;
			if (mine != null && track.Count < 6 && (int)(t * 10) % 3 == 0)
				track.Add($"({mine.GlobalPosition.X - feet.X:0.0};{mine.GlobalPosition.Y - feet.Y:0.0};{mine.GlobalPosition.Z - feet.Z:0.0}) pousada={mine.Landed}");
			if (me.IsProtected) protectedFor = t;
			if (watched != null && !GodotObject.IsInstanceValid(watched)) exploded = true;
			hpMin = Mathf.Min(hpMin, me.Hp);
			if (exploded && t > 1.5 && !me.IsDead) { await b.Seconds(0.6); break; }
		}

		bool died = me.IsDead;
		await b.Seconds(1.5);
		bool stillDead = me.IsDead;
		await b.Seconds(1.0);
		Kill? last = KillFeed.Recent.Count > feedBefore ? KillFeed.Recent[^1].Kill : null;
		float serverHp = serverBody?.Health.Hp ?? -1f;
		bool serverDead = serverBody != null && serverBody.Health.IsDead;
		int killPackets = Server.CountToMe(NetWire.Kill) - killsToMeBefore;

		RegressionBench.Note($"{mode}, pitch {Pitch}, {(AfterRespawn ? "logo depois de renascer" : "sem proteção")}, segurou {Hold:0.0} s: protegido ao soltar={protectedAtThrow}, até {protectedFor:0.0} s depois; granadas de verdade no máximo {maxReal}, encenadas {maxCosmetic}; rota (m, a partir dos pés) {string.Join(" ", track)}; " +
			$"vida do jogo no menor {hpMin:0} (escudo {me.Shield:0}), morreu={died}, continua morto 1,5 s depois={stillDead}; " +
			$"servidor: vida {(serverBody != null ? $"{serverHp:0}, morto={serverDead}" : "n/a")}, avisos de abate ao jogo {killPackets}; " +
			$"feed: {(last is Kill k ? $"{k.Killer} → {k.Victim} {k.Cause} '{k.Weapon}'" : "nada")}");

		b.Check(maxReal >= 1, $"{mode}: a granada do jogador nasceu");
		b.Check(died, $"{mode}: a própria granada, a quem olha para baixo (pitch {Pitch}), mata");
		b.Check(last is Kill l && l.Killer == me.PublicName && l.Victim == me.PublicName && l.Cause == KillCause.Grenade,
			$"{mode}: o feed mostra a pessoa se matando, de granada");
		b.Check(killPackets >= 1, $"{mode}: o servidor anunciou a morte");
		if (Authoritative) b.Check(serverDead, $"{mode}: no servidor o corpo também morreu");
		if (died) b.Check(stillDead, $"{mode}: o jogo morreu e continua morto até renascer");
	}
}

public sealed class OnlineGrenadeAuthoritativeTest : OnlineGrenadeBase
{
	public override string Name => "GranadaNoPeOnline";
	public override string Description => "online autoritativo, com o jogo real: a própria granada lançada olhando para baixo mata, entra no feed e o servidor anuncia";
	protected override bool Authoritative => true;
	protected override float Pitch => -89f;
}

public sealed class OnlineGrenadeAfterRespawnTest : OnlineGrenadeBase
{
	public override string Name => "GranadaNoPeLogoDepoisDeRenascer";
	public override string Description => "online autoritativo, com o jogo real: a granada lançada aos próprios pés logo depois de renascer (3 s de proteção) também mata quem a lançou";
	protected override bool Authoritative => true;
	protected override float Pitch => -89f;
	protected override bool AfterRespawn => true;
	protected override double Hold => 0.05;
}

public sealed class OnlineGrenadeRelayTest : OnlineGrenadeBase
{
	public override string Name => "GranadaNoPeRepasse";
	public override string Description => "online de repasse, com o jogo real: a própria granada lançada olhando para baixo mata, entra no feed e o servidor anuncia";
	protected override bool Authoritative => false;
	protected override float Pitch => -89f;
}
