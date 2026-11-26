using Godot;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// Morrer não tira KP (Jhon, 26/11: "possível perda de KP ao morrer. Ao
/// morrer, não pode perder KP"; o testador: "logo depois de desbloquear arma
/// com KP e morrer em seguida, o jogo reseta"). Até 25/11 a morte tirava 100
/// e o conjunto pago era cobrado de novo a cada renascer. Agora a arma paga
/// fica desbloqueada até o fim da partida (<see cref="MatchKp"/>): morre,
/// renasce com o mesmo conjunto e não paga nada.
/// </summary>
public sealed class KpOnDeathTest : RegressionTest
{
	public override string Name => "KpNaoCaiAoMorrer";
	public override string Description => "morrer não tira KP, e renascer com o conjunto já pago na partida não cobra de novo";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Health me = b.Health;
		FormController form = b.Form;
		me.RespawnDelay = 0.2f;
		Node menu = b.Scene.FindChild("LoadoutMenu", true, false);
		b.Check(menu != null, "a tela de conjuntos está na partida");
		if (menu == null) return;

		// Um conjunto pago com itens de série (sem conta): M4A4 (200) + Desert Eagle (0) + granada (60).
		LoadoutSet before = Profile.Sets[1].Clone();
		Profile.Sets[1] = new LoadoutSet { Name = "Teste KP", Primary = Arsenal.DefaultPrimary, Secondary = Arsenal.DefaultSecondary, Utilities = new List<string> { Arsenal.GrenadeId } };
		try
		{
			LoadoutSet paid = Profile.Sets[1];
			int price = paid.TotalCost();
			RegressionBench.Note($"o conjunto custa {price} KP");
			b.Check(price > 0, "o conjunto custa KP");

			form.AddWp(400);
			float wp = form.Wp;

			// 1. Morre: o KP fica.
			await Die(b, me);
			RegressionBench.Note($"KP antes de morrer {wp:0}, depois {form.Wp:0} (até 25/11 a morte tirava 100)");
			b.Near(wp, form.Wp, 0.01, "morrer não tira KP");

			// 2. Renasce com o conjunto pago: paga uma vez.
			await Respawn(b, me, menu, 1);
			float afterFirst = form.Wp;
			RegressionBench.Note($"renasceu com o conjunto: {wp:0} → {afterFirst:0}");
			b.Near(wp - price, afterFirst, 0.01, $"a primeira vez paga o conjunto ({price})");
			b.Check(b.Weapon.PrimaryDef?.Id == Arsenal.DefaultPrimary, $"com a arma do conjunto ({b.Weapon.PrimaryDef?.Id})");

			// 3. Morre de novo e renasce com o mesmo: não perde nada nem paga de novo.
			await Die(b, me);
			b.Near(afterFirst, form.Wp, 0.01, "a segunda morte também não tira");
			b.Check(paid.CostIn(form.Kp) == 0, "o conjunto aparece como já pago");
			await Respawn(b, me, menu, 1);
			RegressionBench.Note($"renasceu de novo com o conjunto: {afterFirst:0} → {form.Wp:0}");
			b.Near(afterFirst, form.Wp, 0.01, "renascer com o conjunto já pago não cobra");
			b.Check(!me.IsDead && b.Weapon.PrimaryDef?.Id == Arsenal.DefaultPrimary, "e está vivo, com a arma dele");

			// 4. Um abate rende o novo valor.
			float beforeKill = form.Wp;
			form.OnKill();
			b.Near(beforeKill + MatchKp.PerKill, form.Wp, 0.01, $"um abate rende {MatchKp.PerKill} (era 100)");
		}
		finally
		{
			Profile.Sets[1] = before;
		}
	}

	private static async Task Die(RegressionBench b, Health me)
	{
		await b.Until(() => !me.IsProtected, 5.0);
		me.TakeDamage(99999f);
		await b.Until(() => me.IsDead, 1.0);
		await b.Seconds(0.3);
	}

	private static async Task Respawn(RegressionBench b, Health me, Node menu, int set)
	{
		await b.Seconds(me.RespawnWait + 0.2);
		MethodInfo open = menu.GetType().GetMethod("Open", BindingFlags.NonPublic | BindingFlags.Instance);
		MethodInfo choose = menu.GetType().GetMethod("Choose", BindingFlags.NonPublic | BindingFlags.Instance);
		open.Invoke(menu, null);
		choose.Invoke(menu, new object[] { set, false });
		await b.Until(() => !me.IsDead, 3.0);
		await b.Seconds(0.3);
	}
}

/// <summary>
/// O servidor de partida confere o gasto de KP (26/11: "toda compra e gasto
/// de KP só vale se o servidor conferir"). A arma paga que chega no estado
/// do jogador só vale se o KP que ele pode ter ganhado — pelo placar do
/// servidor, não pelo que o jogo diz — cobre o preço; a que não cabe vira a
/// padrão. A arma do conjunto padrão é de graça. Paga uma vez, fica
/// desbloqueada até o fim da partida.
/// </summary>
public sealed class ServerKpTest : RegressionTest
{
	public override string Name => "KpNoServidor";
	public override string Description => "o servidor de partida só aceita a arma paga com o KP que o placar dele dá; a do padrão é de graça; paga uma vez";

	private sealed class Link : HostedMatch.IMatchLink
	{
		public void Send(long peer, byte[] packet, bool reliable) { }
		public void Kick(long peer) { }
		public void Report(HostedMatch match) { }
	}

	public override Task Run(RegressionBench b)
	{
		const long Me = 1, Them = 2;
		var referee = new Referee(new[] { (Me, "Eu", Referee.Blue, 0L), (Them, "Outro", Referee.Orange, 0L) }, 50, 600, false, countdown: 0);
		var match = new HostedMatch("kp", referee, new Link(), null);
		Referee.Seat seat = referee.Find(Me);

		// Começo: só os 50 do padrão.
		RegressionBench.Note($"no começo: {match.KpLeft(Me)} KP para gastar");
		b.Check(match.PaidWeapon(Me, Arsenal.DefaultPrimary, Arsenal.DefaultPrimary) == Arsenal.DefaultPrimary, "a M4A4 (do padrão) é de graça");
		b.Check(match.PaidWeapon(Me, "ak74", Arsenal.DefaultPrimary) == Arsenal.DefaultPrimary, "a AK-74 (380) sem KP vira a padrão");
		int rejected = seat.RejectedHits;
		match.PaidWeapon(Me, "ak74", Arsenal.DefaultPrimary);
		b.Check(rejected == 1 && seat.RejectedHits == 1, $"e conta contra a conta uma vez só ({seat.RejectedHits})");

		// Quatro abates no placar do servidor: 4 × 125 + 50 = 550.
		seat.Kills = 4;
		b.Check(match.PaidWeapon(Me, "ak74", Arsenal.DefaultPrimary) == "ak74", "com 550 KP do placar, a AK-74 vale");
		RegressionBench.Note($"depois da AK-74: {match.KpLeft(Me)} KP para gastar");
		b.Check(match.KpLeft(Me) == 550 - 380, $"sobram 170 ({match.KpLeft(Me)})");
		b.Check(match.PaidWeapon(Me, "ak74", Arsenal.DefaultPrimary) == "ak74" && match.KpLeft(Me) == 170, "a mesma arma de novo (renasceu com ela): não paga outra vez");
		b.Check(match.PaidWeapon(Me, "svd", Arsenal.DefaultPrimary) == Arsenal.DefaultPrimary, "a SVD (600) com 170: vira a padrão");

		// Morrer não tira: mais mortes só dão o bônus do padrão.
		seat.Deaths = 3;
		b.Check(match.KpLeft(Me) == 170 + 3 * MatchKp.DefaultBonus, $"as mortes não tiram KP ({match.KpLeft(Me)})");
		return Task.CompletedTask;
	}
}
