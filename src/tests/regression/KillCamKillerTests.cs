using Godot;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// A câmera de morte mostra quem te matou (Jhon, 25/11: "antes, quando eu
/// morria, mostrava o corpo de quem me matou em vermelho, o nome, e a
/// câmera mostrava onde ele estava no mapa"): a silhueta vermelha no corpo
/// do matador, o nome dele ("ABATIDO POR") e <see cref="Health.Killer"/>.
/// Medido de três jeitos: offline (um bot te mata), online de repasse e
/// online autoritativo (outro jogador te mata, no jogo real ligado a um
/// servidor de partida).
/// </summary>
public static class KillCamChecks
{
	/// <summary>Confere o que a morte mostra: o matador certo, a silhueta e o nome.</summary>
	public static async Task Check(RegressionBench b, string mode, Health me, Node3D killer, string killerName)
	{
		await b.Frames(4);
		KillCam cam = KillCam.Current;
		int pieces = cam?.MarkedPieces ?? 0;
		string who = cam?.WhoName ?? "";
		bool silhouette = false;
		if (killer != null && GodotObject.IsInstanceValid(killer))
		{
			var meshes = killer.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToList();
			silhouette = meshes.Any(m => BodyOverlay.Edge(m) == Silhouette.Killer);
		}
		RegressionBench.Note($"{mode}: Health.Killer={(me.Killer is Node k ? k.Name.ToString() : "nulo")} (esperado {killer?.Name}); câmera de morte={(cam != null ? cam.Mode : "nenhuma")}, " +
			$"matador da câmera={cam?.Killer?.Name.ToString() ?? "nulo"}, peças em vermelho {pieces}, nome no HUD '{who}'");

		b.Check(me.Killer == killer && killer != null, $"{mode}: Health.Killer é o corpo de quem matou");
		b.Check(cam != null, $"{mode}: abriu a câmera de morte");
		b.Check(cam?.Killer == killer && killer != null, $"{mode}: a câmera de morte tem o matador");
		b.Check(silhouette && pieces > 0, $"{mode}: o corpo do matador está com a silhueta vermelha ({pieces} peças)");
		b.Check(who == killerName.ToUpper(), $"{mode}: o nome dele aparece ('{who}', esperado '{killerName.ToUpper()}')");
	}
}

public sealed class KillCamOfflineTest : RegressionTest
{
	public override string Name => "CamDeMorteOffline";
	public override string Description => "offline: morto por um bot, a câmera de morte mostra o bot em vermelho, com o nome";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live && b.Bots().Length > 0, 30);
		Health me = b.Health;
		await b.Until(() => !me.IsProtected, 6.0);
		CharacterBody3D killer = b.Bots().First(x => x.GetNode<Health>("Health").Team != me.Team);
		RegressionBench.Freeze(killer);

		// O tiro de um bot (Weapon.Damage, o mesmo caminho da bala dele).
		Weapon weapon = killer.GetNode<Weapon>("Weapon");
		MethodInfo damage = typeof(Weapon).GetMethod("Damage", BindingFlags.NonPublic | BindingFlags.Instance);
		damage.Invoke(weapon, new object[] { me, 9999f, killer.GlobalPosition, false });

		b.Check(me.IsDead, "morreu com o tiro do bot");
		await KillCamChecks.Check(b, "offline", me, killer, killer.GetNode<Health>("Health").PublicName);
	}
}

/// <summary>
/// A própria granada que feriu e não matou deixava a "causa da morte" como
/// granada à espera da próxima morte — de qualquer um: o inimigo que
/// terminasse o serviço virava "se matou" (o <see cref="Health.Killer"/>
/// era apagado) e a câmera de morte não mostrava ninguém. É o que fica
/// de quem testa a granada aos pés e depois morre jogando (Jhon, 25/11).
/// </summary>
public sealed class KillCamAfterOwnGrenadeTest : RegressionTest
{
	public override string Name => "CamDeMorteAposGranadaPropria";
	public override string Description => "a própria granada que feriu e não matou não faz o abate seguinte, de um inimigo, virar morte sozinho: o matador continua aparecendo";
	public override string Scene => RegressionBench.Match;

	public override async Task Run(RegressionBench b)
	{
		await b.Until(() => Match.Current?.Phase == MatchPhase.Live && b.Bots().Length > 0, 30);
		Health me = b.Health;
		await b.Until(() => !me.IsProtected, 6.0);
		CharacterBody3D killer = b.Bots().First(x => x.GetNode<Health>("Health").Team != me.Team);
		RegressionBench.Freeze(killer);

		// A própria granada, a ~6 m: fere (uns 140 de 200) e não mata.
		var grenade = b.Weapon.GrenadeScene.Instantiate<Grenade>();
		grenade.Thrower = me;
		b.Scene.AddChild(grenade);
		grenade.GlobalPosition = b.Player.GlobalPosition + new Vector3(6f, 0, 0);
		typeof(Grenade).GetMethod("Explode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grenade, null);
		await b.Frames(2);
		RegressionBench.Note($"a própria granada a 6 m: vida {me.Hp:0} de {me.MaxHp:0}, morto={me.IsDead}");
		b.Check(!me.IsDead && me.Hp < me.MaxHp, "a própria granada feriu e não matou");

		// Um inimigo termina o serviço.
		Weapon weapon = killer.GetNode<Weapon>("Weapon");
		MethodInfo damage = typeof(Weapon).GetMethod("Damage", BindingFlags.NonPublic | BindingFlags.Instance);
		damage.Invoke(weapon, new object[] { me, 9999f, killer.GlobalPosition, false });
		b.Check(me.IsDead, "morreu com o tiro do bot");
		await KillCamChecks.Check(b, "depois da própria granada", me, killer, killer.GetNode<Health>("Health").PublicName);
	}
}

public abstract class KillCamOnlineBase : OnlineGameTest
{
	/// <summary>Antes do abate, a própria granada fere (e não mata), como quem
	/// testa a granada aos pés e depois morre jogando.</summary>
	protected virtual bool AfterOwnGrenade => false;

	public override async Task Run(RegressionBench b)
	{
		if (!await Connect(b)) return;
		string mode = Authoritative ? "autoritativo" : "repasse";
		Health me = b.Health;
		Player p = b.Player;
		await b.Until(() => !me.IsProtected, 6.0);

		if (AfterOwnGrenade)
		{
			var grenade = b.Weapon.GrenadeScene.Instantiate<Grenade>();
			grenade.Thrower = me;
			b.Scene.AddChild(grenade);
			grenade.GlobalPosition = p.GlobalPosition + new Vector3(6f, 0, 0);
			typeof(Grenade).GetMethod("Explode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grenade, null);
			await b.Frames(2);
			b.Check(!me.IsDead && me.Hp < me.MaxHp, $"{(Authoritative ? "autoritativo" : "repasse")}: a própria granada feriu e não matou (vida {me.Hp:0})");
		}

		// O boneco de quem vai matar, neste jogo.
		await b.Seconds(0.5);
		Bot puppet = b.GetTree().GetNodesInGroup("bot").OfType<Bot>().FirstOrDefault(x => x.RemoteId == TestMatchServer.OtherAccount);
		b.Check(puppet != null, $"{mode}: o boneco do outro jogador existe neste jogo");
		if (puppet == null) return;

		FakeOther fake = null;
		Vector3 from = Vector3.Zero;
		if (Authoritative)
		{
			ServerBody mine = Server.Match.BodyOf(TestMatchServer.MeAccount);
			fake = await ShooterInSight(b);
			b.Check(fake != null, "achou um lugar à vista do jogador para o outro atirar");
			if (fake == null) return;
			ServerBody theirs = Server.Match.BodyOf(TestMatchServer.OtherAccount);
			b.Check(theirs is { Alive: true }, $"{mode}: o outro jogador está no mundo do servidor");
			// Vida baixa no servidor: um acerto basta (a proteção de quem nasceu já passou).
			await b.Until(() => !mine.Health.IsProtected, 5.0);
			mine.Health.Mirror(5f, mine.Health.MaxHp, 0f, mine.Health.MaxShield, false);
			from = theirs.Body.GetNode<Node3D>("Head").GlobalPosition;
		}
		else
		{
			fake = new FakeOther(Server, Vector3.Zero, 0f, 0f);
			from = puppet.GlobalPosition + Vector3.Up * 0.6f;
		}

		// Os acertos, como o jogo do outro os manda (um basta no autoritativo; no
		// repasse, dois de 150 contra os 200 de vida).
		int hits = Authoritative ? 1 : 2;
		for (int i = 0; i < hits; i++)
		{
			fake.Hit(TestMatchServer.MeAccount, Authoritative ? 30f : 150f, from);
			for (int j = 0; j < 20; j++) { if (Authoritative) fake.Step(); await b.PhysicsFrame(); }
		}
		b.Check(await b.Until(() => me.IsDead, 3.0), $"{mode}: morreu pelo acerto do outro jogador");
		await KillCamChecks.Check(b, mode, me, puppet, "Outro");
	}
}

public sealed class KillCamRelayTest : KillCamOnlineBase
{
	public override string Name => "CamDeMorteRepasse";
	public override string Description => "online de repasse: morto por outro jogador, a câmera de morte mostra o boneco dele em vermelho, com o nome";
	protected override bool Authoritative => false;
}

public sealed class KillCamAuthoritativeTest : KillCamOnlineBase
{
	public override string Name => "CamDeMorteAutoritativo";
	public override string Description => "online autoritativo: morto por outro jogador, a câmera de morte mostra o boneco dele em vermelho, com o nome";
	protected override bool Authoritative => true;
}

public sealed class KillCamAfterGrenadeRelayTest : KillCamOnlineBase
{
	public override string Name => "CamDeMorteAposGranadaRepasse";
	public override string Description => "online de repasse: a própria granada que feriu e não matou não apaga o matador do abate seguinte";
	protected override bool Authoritative => false;
	protected override bool AfterOwnGrenade => true;
}

public sealed class KillCamAfterGrenadeAuthoritativeTest : KillCamOnlineBase
{
	public override string Name => "CamDeMorteAposGranadaAutoritativo";
	public override string Description => "online autoritativo: a própria granada que feriu e não matou não apaga o matador do abate seguinte";
	protected override bool Authoritative => true;
	protected override bool AfterOwnGrenade => true;
}
