using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A vida volta sozinha online (Jhon, 25/11, no Docker: "a vida não está se
/// recuperando automaticamente no modo online; ela fica indo um pouco e
/// voltando", "depois de um tempo bugado assim ela aparece do nada cheia").
/// O jogo de verdade, ligado a um servidor de partida dentro do teste, leva
/// dano e fica parado: passados os <see cref="Health.RegenDelay"/> sem dano, a
/// vida tem de subir lisa até o máximo — sem descer no meio, sem pular — e,
/// no autoritativo, junto com a do corpo do servidor.
/// </summary>
public abstract class OnlineRegenBase : OnlineGameTest
{
	/// <summary>O dano vem de outro jogador (no autoritativo, o servidor o
	/// aplica); senão, do próprio corpo (uma queda, a própria granada).</summary>
	protected virtual bool FromOther => false;
	protected virtual float Damage => 120f;
	protected virtual bool Wolf => false;

	/// <summary>Meia ida e volta: 60 ms, 120 ms de ping (com o jogo e o servidor
	/// na mesma máquina, sem atraso, a diferença não aparecia).</summary>
	protected virtual double Latency => 0.06;

	public override async Task Run(RegressionBench b)
	{
		if (!await Connect(b)) return;
		Server.Latency = Latency;
		string mode = (Authoritative ? "autoritativo" : "repasse") + (Wolf ? ", lobo" : ", humano") + (FromOther ? ", dano de outro" : ", dano próprio") + (Latency > 0 ? $", {Latency * 2000:0} ms" : "");
		Health me = b.Health;
		ServerBody mine = Authoritative ? Server.Match.BodyOf(TestMatchServer.MeAccount) : null;
		await b.Until(() => !me.IsProtected && (mine == null || !mine.Health.IsProtected), 8.0);
		if (Wolf) await b.BecomeWolf();
		await b.Seconds(0.5);

		FakeOther other = null;
		if (FromOther)
		{
			if (Authoritative)
			{
				other = await ShooterInSight(b);
				b.Check(other != null, "achou um lugar à vista para o outro atirar");
				if (other == null) return;
				Vector3 from = Server.Match.BodyOf(TestMatchServer.OtherAccount).Body.GetNode<Node3D>("Head").GlobalPosition;
				other.Hit(TestMatchServer.MeAccount, Damage, from);
			}
			else
			{
				other = new FakeOther(Server, Vector3.Zero, 0f, 0f);
				other.Hit(TestMatchServer.MeAccount, Damage, b.Player.GlobalPosition + Vector3.Forward * 5f);
			}
			bool hurt = await b.Until(() => me.Hp < me.MaxHp - 1f, 3.0);
			b.Check(hurt, $"{mode}: o acerto do outro chegou");
		}
		else
		{
			me.TakeDamage(Damage);
			// O lobo tem escudo: o dano passa por ele antes de chegar à vida.
			for (int i = 0; i < 30 && me.Hp > me.MaxHp - 80f; i++) me.TakeDamage(40f);
		}

		float start = me.Hp;
		float shieldStart = me.Shield, shieldMax = me.Shield;
		var samples = new List<(double T, float Hp, float Server)>();
		// O tempo do jogo (60 passos por segundo), não o do relógio: sem tela o
		// jogo roda mais rápido que o relógio.
		double t = 0;
		while (t < me.RegenDelay + 16.0)
		{
			if (other != null && Authoritative) other.Step();
			await b.PhysicsFrame();
			t += 1.0 / Engine.PhysicsTicksPerSecond;
			samples.Add((t, me.Hp, mine?.Health.Hp ?? -1f));
			shieldMax = Mathf.Max(shieldMax, me.Shield);
		}

		// Desceu sem dano novo, ou pulou para cima mais do que a volta de um quadro.
		int drops = 0, jumps = 0;
		float worstDrop = 0f, worstJump = 0f, worstGap = 0f;
		for (int i = 1; i < samples.Count; i++)
		{
			float d = samples[i].Hp - samples[i - 1].Hp;
			float step = me.MaxHp * me.RegenRate / Engine.PhysicsTicksPerSecond;
			if (d < -0.5f) { drops++; worstDrop = Mathf.Min(worstDrop, d); }
			if (d > step * 3f + 1f) { jumps++; worstJump = Mathf.Max(worstJump, d); }
			if (mine != null && samples[i].T > 1.0) worstGap = Mathf.Max(worstGap, Mathf.Abs(samples[i].Hp - samples[i].Server));
		}
		var every = new List<string>();
		for (double at = 2; at <= samples[^1].T; at += 2)
		{
			var s = samples.First(x => x.T >= at);
			every.Add($"{at:0}s {s.Hp:0}" + (mine != null ? $"/{s.Server:0}" : ""));
		}
		RegressionBench.Note($"{mode}: depois do dano {start:0} de {me.MaxHp:0}" + (Wolf ? $", escudo {shieldStart:0}" : "") + "; a cada 2 s (jogo" + (mine != null ? "/servidor" : "") + "): " + string.Join(", ", every));
		RegressionBench.Note($"{mode}: {drops} descida(s) sem dano (a pior {worstDrop:0.0}), {jumps} pulo(s) para cima (o maior {worstJump:0.0})" + (mine != null ? $", maior diferença jogo × servidor {worstGap:0.0}" : ""));

		float before = samples.Last(s => s.T <= me.RegenDelay - 0.6).Hp;
		b.Check(Mathf.Abs(before - start) < 0.5f, $"{mode}: nada volta antes dos {me.RegenDelay:0} s ({start:0} → {before:0})");
		b.Check(drops == 0, $"{mode}: a vida não desce voltando ({drops} descidas, a pior {worstDrop:0.0})");
		b.Check(jumps == 0, $"{mode}: nem aparece cheia de repente ({jumps} pulos, o maior {worstJump:0.0})");
		b.Check(me.Hp >= me.MaxHp - 0.5f, $"{mode}: chegou ao máximo ({me.Hp:0} de {me.MaxHp:0})");
		if (Wolf) b.Check(shieldMax <= shieldStart + 0.5f, $"{mode}: o escudo não volta sozinho ({shieldStart:0} → no máximo {shieldMax:0})");
		if (mine != null)
		{
			b.Check(mine.Health.Hp >= mine.Health.MaxHp - 0.5f, $"{mode}: o corpo do servidor também ({mine.Health.Hp:0})");
			b.Check(worstGap <= me.MaxHp * me.RegenRate * Health.RegenLag + 1f, $"{mode}: jogo e servidor juntos na volta (maior diferença {worstGap:0.0})");

			// A folga da volta não esconde dano de verdade: cheio, um dano pequeno
			// que só o jogo viu (uma queda) chega ao servidor.
			if (!FromOther)
			{
				await b.Seconds(1.0);
				me.TakeDamage(5f);
				float expected = me.Hp;
				bool reached = await b.Until(() => Mathf.Abs(mine.Health.Hp - expected) < 0.5f, 2.0);
				RegressionBench.Note($"{mode}: cheio, um dano de 5 no jogo: jogo {expected:0}, servidor {mine.Health.Hp:0}");
				b.Check(reached, $"{mode}: cheio, um dano pequeno do jogo chega ao servidor ({expected:0} × {mine.Health.Hp:0})");
			}
		}
	}
}

public sealed class OnlineRegenRelayTest : OnlineRegenBase
{
	public override string Name => "VidaVoltaRepasse";
	public override string Description => "online de repasse, 120 ms: depois do tiro de outro, a vida volta sozinha passados 15 s, lisa, sem descer nem pular";
	protected override bool Authoritative => false;
	protected override bool FromOther => true;
}

public sealed class OnlineRegenAuthoritativeTest : OnlineRegenBase
{
	public override string Name => "VidaVoltaAutoritativo";
	public override string Description => "online autoritativo, 120 ms: depois de um dano próprio, a vida volta lisa, junto com a do servidor, e enche nos dois";
	protected override bool Authoritative => true;
}

public sealed class OnlineRegenAuthoritativeShotTest : OnlineRegenBase
{
	public override string Name => "VidaVoltaAutoritativoTiro";
	public override string Description => "online autoritativo, 120 ms: depois do tiro de outro jogador (o servidor aplica), a vida volta lisa e enche nos dois";
	protected override bool Authoritative => true;
	protected override bool FromOther => true;
}

public sealed class OnlineRegenAuthoritativeWolfTest : OnlineRegenBase
{
	public override string Name => "VidaVoltaAutoritativoLobo";
	public override string Description => "online autoritativo, 120 ms, lobo: a vida volta lisa e enche nos dois; o escudo não volta sozinho";
	protected override bool Authoritative => true;
	protected override bool Wolf => true;
}
