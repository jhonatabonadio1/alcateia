using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// A arma no chão online (Jhon, 26/11: "arma largada não aparece pros
/// outros jogadores do mapa, amigo e inimigo. Somente eu consigo ver minhas
/// armas e as armas do meu time. Devo conseguir pegar a arma de qualquer
/// jogador na partida"). O jogo de verdade ligado a um servidor de partida
/// no teste, com o outro jogador do lado do servidor e um bot desta máquina:
///
/// - a arma que solto (G) chega ao outro, com a mesma munição;
/// - ele pega primeiro: a arma some da minha tela, e ninguém pega duas vezes
///   (nem ele de novo, nem eu, que passo por cima depois);
/// - a arma que ele solta aparece na minha tela, e eu pego, com a munição
///   dela — na autoritativa ela vale no meu corpo do servidor mesmo sem ser
///   da minha loja;
/// - o bot desta máquina morre e a arma dele chega ao outro;
/// - o boneco do outro morre aqui e não deixa cópia só desta tela;
/// - quem entra no meio recebe as armas que estão no chão;
/// - mais de 20 no chão: a mais velha some de todas as telas.
///
/// Até 26/11 nada disto ia pela rede: o outro não recebia nada (0 pacotes
/// com a arma), e o boneco que morria aqui soltava uma cópia só daqui.
/// </summary>
public abstract class OnlineDropTest : OnlineGameTest
{
	private const long BotId = -1;

	protected override OnlineMatch.Seat[] ExtraSeats =>
		new[] { new OnlineMatch.Seat(BotId, "Robô", Teams.Orange, 0, TestMatchServer.MeAccount) };

	private record struct Drop(long Id, long Owner, string Weapon, int Ammo, int Reserve, float Age);
	private record struct Taken(long Id, long Taker, string Weapon, int Ammo);

	private static List<Drop> Drops(IEnumerable<byte[]> packets) => packets.Where(p => p[0] == NetWire.Drop).Select(p =>
	{
		using BinaryReader r = NetWire.Open(p);
		r.ReadByte();
		long id = r.ReadInt64(), owner = r.ReadInt64();
		string weapon = r.ReadString();
		int ammo = r.ReadInt16(), reserve = r.ReadInt16();
		r.ReadVector(); r.ReadVector();
		return new Drop(id, owner, weapon, ammo, reserve, r.ReadSingle());
	}).ToList();

	private static List<Taken> Takens(IEnumerable<byte[]> packets) => packets.Where(p => p[0] == NetWire.Taken).Select(p =>
	{
		using BinaryReader r = NetWire.Open(p);
		r.ReadByte();
		long id = r.ReadInt64(), taker = r.ReadInt64();
		string weapon = r.ReadString();
		return new Taken(id, taker, weapon, r.ReadInt16());
	}).ToList();

	private List<byte[]> ToOther => Server.ToOther.Select(p => p.Packet).ToList();
	private List<byte[]> ToMe => Server.ToMe.Select(p => p.Packet).ToList();

	private static byte[] DropPacket(long id, long owner, string weapon, int ammo, int reserve, Vector3 from, Vector3 velocity)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Drop, out MemoryStream stream);
		w.Write(id); w.Write(owner); w.Write(weapon); w.Write((short)ammo); w.Write((short)reserve);
		w.Write(from); w.Write(velocity);
		return stream.ToArray();
	}

	private static byte[] TakePacket(long id, long taker)
	{
		BinaryWriter w = NetWire.Begin(NetWire.Take, out MemoryStream stream);
		w.Write(id); w.Write(taker);
		return stream.ToArray();
	}

	public override async Task Run(RegressionBench b)
	{
		if (!await Connect(b)) return;
		string mode = Authoritative ? "autoritativo" : "repasse";
		Weapon gun = b.Weapon;
		FakeOther other = Authoritative ? await ShooterInSight(b) : new FakeOther(Server, Vector3.Zero, 0, 0);
		b.Check(other != null, $"{mode}: o outro jogador está na partida");
		if (other == null) return;

		// ---- 1. Solto a minha arma (G): ela chega ao outro.
		gun.SelectSlot(Slot.Primary);
		await b.Seconds(0.8);
		string mine = gun.PrimaryDef?.Id;
		int mag = gun.AmmoInMag;
		int dropsBefore = Drops(ToOther).Count;
		b.Check(gun.TryDrop(), $"{mode}: soltei a primária ({mine}, {mag} no pente)");
		await b.Until(() => Drops(ToOther).Count > dropsBefore, 3);
		Drop sent = Drops(ToOther).LastOrDefault();
		RegressionBench.Note($"{mode}: o outro recebeu {Drops(ToOther).Count - dropsBefore} aviso(s) de arma no chão: {sent}");
		b.Check(sent.Weapon == mine && sent.Ammo == mag && sent.Owner == TestMatchServer.MeAccount,
			$"{mode}: a arma que soltei chega ao outro jogador (inimigo), a mesma e com a mesma munição ({sent.Weapon}, {sent.Ammo})");
		b.Check(Server.Match.LooseWeapons == 1 && WeaponPickup.Find(sent.Id) != null, $"{mode}: o servidor guarda a arma no chão, e ela está na minha tela");

		// ---- 2. O outro pega primeiro; ninguém pega duas vezes.
		Server.FromOther(TakePacket(sent.Id, TestMatchServer.OtherAccount));
		await b.Until(() => Takens(ToMe).Any(t => t.Id == sent.Id), 3);
		await b.Frames(3);
		Taken given = Takens(ToMe).FirstOrDefault(t => t.Id == sent.Id);
		b.Check(given.Taker == TestMatchServer.OtherAccount && Takens(ToOther).Count(t => t.Id == sent.Id) == 1,
			$"{mode}: o servidor dá a arma ao primeiro que pede (o outro) e avisa os dois");
		b.Check(WeaponPickup.Find(sent.Id) == null && Server.Match.LooseWeapons == 0, $"{mode}: a arma some da minha tela e do servidor");
		Server.FromOther(TakePacket(sent.Id, TestMatchServer.OtherAccount));
		await b.Seconds(1.6);   // passa do tempo em que quem soltou não pega de volta
		b.Check(Takens(ToOther).Count(t => t.Id == sent.Id) == 1 && gun.PrimaryDef == null,
			$"{mode}: ninguém pega duas vezes (nem o outro de novo, nem eu: primária {gun.PrimaryDef?.Id ?? "vazia"})");

		// ---- 3. O outro solta um AK aos meus pés: aparece aqui e eu pego.
		// Na autoritativa a arma vale no meu corpo do servidor mesmo sem ser da minha loja.
		if (Authoritative)
		{
			var owned = (Dictionary<long, HashSet<string>>)typeof(HostedMatch)
				.GetField("_owned", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Server.Match);
			owned[TestMatchServer.MeAccount] = new HashSet<string>();
			// O outro pagou o AK com KP desta partida (26/11: o servidor confere o
			// gasto de KP pelo placar dele; sem abates, o AK dele voltaria à padrão).
			Server.Match.Referee.Find(TestMatchServer.OtherAccount).Kills = 10;
			other.Status(primary: "ak74");
			for (int i = 0; i < 4; i++) { other.Step(); await b.PhysicsFrame(); }
		}
		long theirs = TestMatchServer.OtherAccount * NetWire.DropIdBase + 1;
		Server.FromOther(DropPacket(theirs, TestMatchServer.OtherAccount, "ak74", 17, 40, b.Player.GlobalPosition + Vector3.Up * 0.6f, Vector3.Zero));
		bool seen = await b.Until(() => WeaponPickup.Find(theirs) != null || gun.PrimaryDef?.Id == "ak74", 3);
		b.Check(seen, $"{mode}: a arma que o outro (inimigo) soltou aparece na minha tela");
		bool got = await b.Until(() => gun.PrimaryDef?.Id == "ak74", 5);
		RegressionBench.Note($"{mode}: peguei {gun.PrimaryDef?.Id ?? "nada"}, pente {gun.PrimaryAmmo}, reserva {gun.PrimaryReserve}");
		b.Check(got && gun.PrimaryAmmo == 17, $"{mode}: eu pego a arma dele, com a munição dela (17 no pente)");
		b.Check(Takens(ToOther).Any(t => t.Id == theirs && t.Taker == TestMatchServer.MeAccount), $"{mode}: o outro fica sabendo que eu peguei");
		if (Authoritative)
		{
			await b.Seconds(0.6);
			ServerBody body = Server.Match.BodyOf(TestMatchServer.MeAccount);
			b.Check(body.Carries("ak74") && Server.Match.Referee.Find(TestMatchServer.MeAccount).RejectedHits == 0,
				$"{mode}: no servidor o meu corpo carrega o AK pego do chão (não volta à arma padrão nem conta contra mim)");
		}

		// ---- 4. O bot desta máquina morre: a arma dele chega ao outro.
		Bot bot = b.GetTree().GetNodesInGroup("bot").OfType<Bot>().FirstOrDefault(x => !x.Remote && x.NetId == BotId);
		b.Check(bot != null, $"{mode}: o bot desta máquina está na partida");
		if (bot != null)
		{
			var botForm = bot.GetNode<FormController>("FormController");
			var botGun = bot.GetNode<Weapon>("Weapon");
			await b.Until(() => !botForm.IsWolf && botGun.PrimaryDef != null, 5);
			string botWeapon = botGun.PrimaryDef?.Id;
			int beforeBot = Drops(ToOther).Count(d => d.Owner == BotId);
			bot.GetNode<Health>("Health").TakeDamage(99999f, 0, default, false, ignoreProtection: true);
			bool botDrop = await b.Until(() => Drops(ToOther).Count(d => d.Owner == BotId) > beforeBot, 3);
			b.Check(botDrop && Drops(ToOther).Last(d => d.Owner == BotId).Weapon == botWeapon,
				$"{mode}: a arma do bot que morreu aqui chega ao outro ({botWeapon})");
		}

		// ---- 5. O boneco do outro morre aqui: não fica cópia só desta tela.
		Bot puppet = b.GetTree().GetNodesInGroup("bot").OfType<Bot>().FirstOrDefault(x => x.Remote && x.RemoteId == TestMatchServer.OtherAccount);
		if (puppet != null)
		{
			puppet.GetNode<Health>("Health").Mirror(0, 200, 0, 0, true);
			await b.Frames(3);
		}
		int localOnly = WeaponPickup.OnGround.Count(p => GodotObject.IsInstanceValid(p) && p.NetId == 0);
		b.Check(puppet != null && localOnly == 0, $"{mode}: o boneco do outro morre sem deixar arma só nesta tela ({localOnly} cópias locais)");

		// ---- 6. Quem entra no meio recebe as armas no chão.
		gun.SelectSlot(Slot.Primary);
		await b.Seconds(0.6);
		gun.TryDrop();
		await b.Until(() => Server.Match.LooseWeapons >= 2, 3);
		int loose = Server.Match.LooseWeapons;
		int heardBefore = Drops(ToOther).Count;
		Server.Match.Admit(TestMatchServer.OtherPeer, TestMatchServer.OtherAccount);
		List<Drop> replay = Drops(ToOther).Skip(heardBefore).ToList();
		RegressionBench.Note($"{mode}: {loose} armas no chão; quem voltou recebeu {replay.Count} (idades {string.Join(" ", replay.Select(d => d.Age.ToString("0.0")))} s)");
		b.Check(replay.Count == loose && replay.All(d => d.Age > 0), $"{mode}: quem entra no meio recebe as armas que estão no chão, com o tempo delas");

		// ---- 7. Mais de 20 no chão: a mais velha some de todas as telas.
		int vanishedBefore = Takens(ToMe).Count(t => t.Taker == 0);
		if (Authoritative) other.Status(primary: "ak74");
		for (int i = 0; i < WeaponPickup.MaxOnMap; i++)
			Server.FromOther(DropPacket(theirs + 1 + i, TestMatchServer.OtherAccount, "ak74", 30, 0, new Vector3(0, 30, 0), Vector3.Zero));
		await b.Frames(4);
		b.Check(Server.Match.LooseWeapons == WeaponPickup.MaxOnMap && Takens(ToMe).Count(t => t.Taker == 0) > vanishedBefore,
			$"{mode}: no máximo {WeaponPickup.MaxOnMap} no chão; a mais velha some, e todos ficam sabendo ({Server.Match.LooseWeapons})");
	}
}

public sealed class OnlineDropRelayTest : OnlineDropTest
{
	public override string Name => "ArmaNoChaoRepasse";
	public override string Description => "online de repasse: a arma largada aparece para todos (amigo e inimigo), o primeiro que pede leva, ninguém leva duas vezes, quem entra no meio vê";
	protected override bool Authoritative => false;
}

public sealed class OnlineDropAuthoritativeTest : OnlineDropTest
{
	public override string Name => "ArmaNoChaoAutoritativo";
	public override string Description => "online autoritativo: a arma largada aparece para todos (amigo e inimigo), o primeiro que pede leva, ninguém leva duas vezes, quem entra no meio vê, e a arma pega vale no servidor";
	protected override bool Authoritative => true;
}
