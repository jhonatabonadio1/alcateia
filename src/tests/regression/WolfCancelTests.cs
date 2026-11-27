using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Os golpes do lobo se cortam na hora, no ar também, e o golpe cortado não
/// dá dano (Jhon, 27/11: "o ataque primário e secundário do lobo devem
/// responder instantaneamente, uma cancela a animação da outra, inclusive no
/// ar; mas se cancelar, não é para dar o dano da anterior").
///
/// - No ar, garrada e logo o bote (antes da garra bater): o bote sai no mesmo
///   passo do clique e a garra não fere — só o bote.
/// - No ar, o bote e logo a garrada (antes do bote encostar): a garra sai no
///   mesmo passo e o bote não fere — só a garra.
/// - Garrada que bateu e logo o bote: os dois ferem, mas nunca a menos do
///   compasso (StrikeSpacing) um do outro.
/// </summary>
public sealed class WolfCancelTest : RegressionTest
{
	public override string Name => "LoboCortaGolpe";
	public override string Description => "no ar, garra e bote saem no passo do clique, um corta o outro, e o golpe cortado não fere; o que já bateu respeita o compasso";

	private static readonly Vector3 Spot = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(1);
		await b.Seconds(0.3);
		var bot = b.Bots()[0];
		Health target = bot.GetNode<Health>("Health");
		await b.BecomeWolf();
		await b.Until(() => !target.IsProtected, 5.0);
		RegressionBench.Freeze(bot);
		Weapon w = b.Weapon;

		var hits = new List<(ulong Frame, float Amount)>();
		void OnDamaged(float amount, Vector3 from) => hits.Add((Engine.GetPhysicsFrames(), amount));
		target.Damaged += OnDamaged;
		var started = new List<(ulong Frame, WolfMove Move)>();
		void OnStart(int move) => started.Add((Engine.GetPhysicsFrames(), (WolfMove)move));
		w.WolfMoveStarted += OnStart;

		// 1. Garrada no ar e o bote antes de ela bater.
		await Airborne(b, bot, target);
		hits.Clear(); started.Clear();
		w.TryAttack();
		ulong clawAt = Engine.GetPhysicsFrames();
		await b.PhysicsFrame();
		w.TrySpecial();
		ulong diveAt = Engine.GetPhysicsFrames();
		await b.Seconds(1.2);
		bool dove = started.Exists(s => s.Move == WolfMove.Leap && s.Frame == diveAt);
		bool clawed = started.Exists(s => s.Move is WolfMove.Claw or WolfMove.Punch && s.Frame == clawAt);
		RegressionBench.Note($"garra → bote: garra começou {clawed}, bote no passo do clique {dove}; golpes que feriram: {string.Join(" ", hits.ConvertAll(h => $"{h.Amount:0}"))}");
		b.Check(clawed && dove, "garra e bote começam no passo do clique");
		b.Check(hits.Count == 1 && hits[0].Amount > 200f, $"a garra cortada não fere, só o bote ({hits.Count} golpe(s))");

		// 2. O bote e a garrada antes de ele encostar.
		await b.Seconds(1.0);   // a espera entre botes
		// (o alvo a 3,2 m: o bote leva uns 10 passos até ele, e a garra o corta no caminho)
		await Airborne(b, bot, target, high: true, ahead: 3.2f);
		hits.Clear(); started.Clear();
		w.TrySpecial();
		await b.PhysicsFrame();
		bool diving = w.IsDiving;
		w.TryAttack();
		ulong cutAt = Engine.GetPhysicsFrames();
		await b.Seconds(1.2);
		bool clawCut = started.Exists(s => s.Move is WolfMove.Claw or WolfMove.Punch && s.Frame == cutAt);
		RegressionBench.Note($"bote → garra: mergulhando {diving}, garra no passo do clique {clawCut}, ainda mergulhando {w.IsDiving}; golpes que feriram: {string.Join(" ", hits.ConvertAll(h => $"{h.Amount:0}"))}");
		b.Check(diving && clawCut && !w.IsDiving, "a garra corta o bote no ar, no passo do clique");
		b.Check(hits.TrueForAll(h => h.Amount < 200f), "o bote cortado não fere (só a garra, se pegar)");

		// 3. A garrada que bateu e o bote logo em seguida: os dois ferem, no compasso.
		await b.Seconds(1.0);
		await Airborne(b, bot, target);
		hits.Clear();
		w.TryAttack();
		await b.Until(() => hits.Count > 0, 1.0);
		w.TrySpecial();
		await b.Seconds(1.2);
		double gap = hits.Count > 1 ? (hits[1].Frame - hits[0].Frame) / (double)Engine.PhysicsTicksPerSecond : double.NaN;
		RegressionBench.Note($"garra que bateu → bote: {hits.Count} golpes, de {string.Join(" e ", hits.ConvertAll(h => $"{h.Amount:0}"))}, a {RegressionBench.F(gap)} s um do outro (compasso {RegressionBench.F(w.StrikeSpacing)} s)");
		b.Check(hits.Count == 2, $"os dois ferem ({hits.Count})");
		if (hits.Count == 2) b.AtLeast(w.StrikeSpacing - 1.5 / 60.0, gap, "a segunda pancada não sai antes do compasso (s)");

		target.Damaged -= OnDamaged;
		w.WolfMoveStarted -= OnStart;
	}

	/// <summary>O lobo no ar, perto do alvo e de frente para ele, olhando um pouco para baixo, com o
	/// alvo cheio de vida.</summary>
	private static async Task Airborne(RegressionBench b, CharacterBody3D bot, Health target, bool high = false, float ahead = 1.6f)
	{
		await b.Place(Spot, -90f, -30f, settle: 0.4);
		bot.GlobalPosition = Spot + new Vector3(ahead, 0, 0);
		target.SetPool(100000f, 0f, true);
		Player p = b.Player;
		p.GlobalPosition += Vector3.Up * (high ? 1.6f : 1.0f);
		p.Velocity = Vector3.Zero;
		// (o corpo só deixa de contar como no chão depois do passo de física seguinte)
		await b.Until(() => !p.IsOnFloor(), 0.2);
	}
}
