using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Clique em rajada nos golpes do lobo (alpha fechado, 26/11: "clique
/// repetido nos ataques do lobo, principal e secundário, parece não ter
/// limite de resposta: dá pra farmar clique e vencer qualquer luta"). O
/// lobo, colado num humano parado, aperta o mais rápido que dá — um clique
/// por quadro — e o teste mede, no alvo, quantos golpes chegam e quanto dano
/// por segundo:
/// - só o primário (garrada);
/// - primário e secundário alternados no chão;
/// - pulo, bote e garrada em ciclo (o secundário no ar).
///
/// O teto é o ritmo do desenho: uma garrada a
/// cada <c>ImpactDelay + ClawCancelAfter</c> (0,34 s), e o bote no máximo
/// um a cada <see cref="Weapon.LeapCooldown"/>.
/// </summary>
public sealed class WolfSpamTest : RegressionTest
{
	public override string Name => "LoboSemSpam";
	public override string Description => "clique em rajada nos golpes do lobo (primário, secundário e o ciclo pulo-bote-garra) não passa do ritmo do desenho";

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
		float claw = WolfMoves.ImpactDelay(WolfMove.Claw) + w.ClawCancelAfter;
		RegressionBench.Note($"ritmo do desenho: uma garrada a cada {RegressionBench.F(claw)} s; bote a cada {RegressionBench.F(w.LeapCooldown)} s no mínimo");

		foreach (string mode in new[] { "primario", "alternado", "pulo-bote-garra" })
		{
			await b.Place(Spot, -90f, mode == "pulo-bote-garra" ? -35f : 0f, settle: 0.5);
			bot.GlobalPosition = Spot + new Vector3(1.4f, 0, 0);
			target.SetPool(100000f, 0f, true);
			await b.Seconds(0.3);

			var hits = new List<(double At, float Amount)>();
			double clock = 0;
			void OnDamaged(float amount, Vector3 from) => hits.Add((clock, amount));
			target.Damaged += OnDamaged;

			const double span = 4.0;
			int frame = 0;
			while (clock < span)
			{
				// Um clique por quadro: aperta num, solta no outro.
				bool down = frame % 2 == 0;
				switch (mode)
				{
					case "primario":
						if (down) RegressionBench.Press("attack"); else RegressionBench.Release("attack");
						break;
					case "alternado":
						if (frame % 4 == 0) RegressionBench.Press("attack");
						else if (frame % 4 == 1) RegressionBench.Release("attack");
						else if (frame % 4 == 2) RegressionBench.Press("special");
						else RegressionBench.Release("special");
						break;
					default:
						// No chão, pula; no ar, secundário (bote) e primário, um por quadro.
						if (b.Player.IsOnFloor()) RegressionBench.Press("jump");
						else RegressionBench.Release("jump");
						if (down) RegressionBench.Press("special", "attack"); else RegressionBench.Release("special", "attack");
						break;
				}
				// O alvo fica onde está (o empurrão da garra e o bote o levariam).
				bot.GlobalPosition = Spot + new Vector3(1.4f, 0, 0);
				if (mode != "pulo-bote-garra")
				{
					b.Player.GlobalPosition = new Vector3(Spot.X, b.Player.GlobalPosition.Y, Spot.Z);
					b.Player.Rotation = new Vector3(0, Mathf.DegToRad(-90f), 0);
				}
				await b.PhysicsFrame();
				clock += 1.0 / Engine.PhysicsTicksPerSecond;
				frame++;
			}
			RegressionBench.ReleaseAll();
			target.Damaged -= OnDamaged;

			double minGap = hits.Count > 1 ? hits.Zip(hits.Skip(1), (a, c) => c.At - a.At).Min() : double.NaN;
			float total = hits.Sum(h => h.Amount);
			RegressionBench.Note($"{mode}: {hits.Count} golpes em {span:0} s ({RegressionBench.F(hits.Count / span)} por s), menor intervalo {RegressionBench.F(minGap)} s, {total:0} de dano ({total / span:0} por s); golpes: "
				+ string.Join(" ", hits.Select(h => $"{h.At:0.00}:{h.Amount:0}")));
			await b.Seconds(1.2);
		}
	}
}
