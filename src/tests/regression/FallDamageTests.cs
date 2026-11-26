using Godot;
using System.Threading.Tasks;

/// <summary>
/// Dano de queda (Jhon, 26/11: "humano começar a tomar dano de queda, lobo
/// também, mas menos. Não deve tomar dano de queda no ataque secundário (ao
/// pular e atacar) e com a habilidade suprema"). Até 26/11 nenhuma queda
/// feria. Solto de várias alturas no campo de treino: o humano sente a partir
/// de 4 m e morre a 12; o lobo, a partir de 7 m e com metade do dano; caindo
/// num bote ou no especial, nada.
/// </summary>
public sealed class FallDamageTest : RegressionTest
{
	public override string Name => "DanoDeQueda";
	public override string Description => "o humano se fere em queda alta e morre de muito alto (feed: queda); o lobo se fere menos; no bote e no especial, não";

	private static readonly Vector3 Spot = new(-15, 1, 13);

	/// <summary>O dano que o corpo levou (o escudo do lobo escorre sozinho: a
	/// vida e o escudo não servem de medida).</summary>
	private float _hurt;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		Health me = b.Health;
		me.Damaged += (amount, _) => _hurt += amount;
		FallProfile fall = FallProfile.Load();
		RegressionBench.Note($"humano: nada até {fall.HumanSafe} m, {fall.HumanPerMeter} por metro; lobo: nada até {fall.WolfSafe} m, {fall.WolfPerMeter} por metro");

		// ---- humano
		float low = await Drop(b, me, 3f);
		b.Near(0, low, 0.5, "humano, queda de ~2 m: nada");
		float mid = await Drop(b, me, 9f);
		RegressionBench.Note($"humano de ~8 m: {mid:0} de dano");
		b.Check(mid > 60 && mid < 140, $"humano, queda de ~8 m: se fere ({mid:0}; a conta dá uns 100)");
		b.Check(!me.IsDead, "e não morre");

		int feed = KillFeed.Recent.Count;
		me.SetHp(me.MaxHp);
		await b.Place(Spot + Vector3.Up * 14f, -90f, settle: 0.05);
		bool died = await b.Until(() => me.IsDead, 4.0);
		b.Check(died, "humano, queda de ~13 m: morre");
		Kill? last = KillFeed.Recent.Count > feed ? KillFeed.Recent[^1].Kill : null;
		b.Check(last is Kill k && k.Cause == KillCause.Fall && k.Victim == me.PublicName, $"o feed diz queda ({(last is Kill l ? l.Cause.ToString() : "nada")})");
		await b.Until(() => !me.IsDead, 8.0);
		b.CloseLoadout();
		await b.Seconds(0.5);

		// ---- lobo
		await b.BecomeWolf();
		float wolfLow = await Drop(b, me, 7f);
		b.Near(0, wolfLow, 0.5, "lobo, queda de ~6 m: nada");
		float wolfHigh = await Drop(b, me, 13f);
		RegressionBench.Note($"lobo de ~12 m: {wolfHigh:0} de dano (o humano levaria uns 200)");
		b.Check(wolfHigh > 30 && wolfHigh < 100, $"lobo, queda de ~12 m: se fere menos que o humano ({wolfHigh:0})");

		// ---- o bote: solto de ~12 m, aperta o secundário e cai mergulhando.
		await b.Place(Spot + Vector3.Up * 13f, -90f, -60f, settle: 0.05);
		_hurt = 0;
		await b.Tap("special");
		bool dived = await b.Until(() => b.Weapon.IsDiving, 1.0);
		await b.Until(() => b.Player.IsOnFloor() && !b.Weapon.IsDiving, 4.0);
		await b.Seconds(0.3);
		float diveLoss = _hurt;
		RegressionBench.Note($"bote de ~12 m: {(dived ? "mergulhou" : "não mergulhou")}, perdeu {diveLoss:0}");
		b.Check(dived, "o bote sai no ar");
		b.Near(0, diveLoss, 0.5, "caindo num bote, a queda não fere");

		// ---- o especial: de cima de uma plataforma de 10 m, salta para o chão.
		var platform = new StaticBody3D { CollisionLayer = 1, Position = Spot + new Vector3(0, 9.75f, 0) };
		platform.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3f, 0.5f, 3f) } });
		b.Scene.AddChild(platform);
		me.SetHp(me.MaxHp);
		await b.Place(Spot + new Vector3(0, 11f, 0), -90f, -40f, settle: 0.6);
		b.Weapon.SpecialFree = false;
		b.Weapon.FillSpecial();
		_hurt = 0;
		float top = b.Player.GlobalPosition.Y;
		await b.Tap("pounce");
		await b.Frames(2);
		await b.Tap("attack");
		bool flew = await b.Until(() => b.Weapon.IsPounceFlying, 1.5);
		await b.Until(() => b.Player.IsOnFloor() && b.Player.GlobalPosition.Y < top - 5f, 5.0);
		await b.Seconds(0.5);
		float pounceLoss = _hurt;
		RegressionBench.Note($"especial de cima de 10 m: {(flew ? "saltou" : "não saltou")}, caiu {RegressionBench.F(top - b.Player.GlobalPosition.Y)} m, perdeu {pounceLoss:0}");
		b.Check(flew, "o especial salta da plataforma");
		b.AtLeast(7.5, top - b.Player.GlobalPosition.Y, "e desce mais que a queda segura do lobo (m)");
		b.Near(0, pounceLoss, 0.5, "no especial, a queda não fere");
		platform.QueueFree();
	}

	/// <summary>Solta de <paramref name="height"/> m acima do chão do campo, com
	/// a vida cheia, e devolve quanto de vida e escudo foi embora.</summary>
	private async Task<float> Drop(RegressionBench b, Health me, float height)
	{
		me.SetPool(me.MaxHp, me.MaxShield, true);
		await b.Place(Spot, -90f, settle: 0.3);
		_hurt = 0;
		await b.Place(Spot + Vector3.Up * (height - 1f), -90f, settle: 0.05);
		await b.Until(() => b.Player.IsOnFloor(), 4.0);
		await b.Seconds(0.3);
		return _hurt;
	}
}
