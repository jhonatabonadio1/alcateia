using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Aliado não é parede (Jhon, 25/11: "ao apertar W W com o lobo e encostar
/// num aliado, o lobo vai pra frente e pra trás piscando e às vezes
/// atravessa o aliado; isso pode afetar o ataque especial e os
/// secundários"). O corpo do aliado barrava o lobo como uma parede: o
/// aliado da rede é posto no lugar que a máquina dele diz (com atraso), por
/// cima do lobo, e a física empurrava o lobo de volta a cada quadro; o bote
/// parava no aliado (contava como parede) e o salto batia nele no caminho.
/// Agora quem é do mesmo time passa através; o inimigo continua sólido.
/// </summary>
public sealed class AllyBodyTest : RegressionTest
{
	public override string Name => "AliadoNaoTrava";
	public override string Description => "o lobo de quatro passa pelo aliado sem tremer (parado ou posto em cima dele pela rede); o inimigo ainda barra; o salto passa pelo aliado; a garra entre aliado e inimigo acerta só o inimigo";

	private static readonly Vector3 Start = new(-15, 1, 13);

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(2);
		await b.Seconds(0.3);
		CharacterBody3D ally = b.Bots()[0], enemy = b.Bots()[1];
		Health allyHealth = ally.GetNode<Health>("Health"), enemyHealth = enemy.GetNode<Health>("Health");
		allyHealth.Team = b.Health.Team;
		enemyHealth.Team = Teams.Other(b.Health.Team);
		await b.Frames(2);   // cada corpo acerta os pares no passo dele, como no jogo
		RegressionBench.Freeze(ally);
		RegressionBench.Freeze(enemy);
		await b.BecomeWolf();
		Player p = b.Player;
		float sprint = p.HumanSpeed * b.Form.Breed.SpeedMultiplier * p.WolfSprintFactor;

		// 1. Correndo de quatro contra o aliado parado à frente: passa por ele,
		// sem recuar, e a câmera não salta quando ele fica entre ela e o lobo
		// (comparada com a mesma corrida sem ninguém).
		Away(enemy);
		Pass alone = await Sprint(b, ally, _ => new Vector3(40, 1, 40));
		Pass run = await Sprint(b, ally, null);
		RegressionBench.Note($"aliado parado: recuou {run.Back} quadros, terminou {RegressionBench.F(run.Ahead)} m à frente dele, {RegressionBench.F(run.Speed)} m/s; câmera no mínimo a {RegressionBench.F(run.Camera)} m (sozinho {RegressionBench.F(alone.Camera)} m)");
		b.Check(run.Back == 0, $"contra o aliado parado o lobo não vai e volta ({run.Back} quadros para trás)");
		b.AtLeast(0.6, run.Ahead, "passa pelo aliado em vez de parar nele");
		b.Near(sprint, run.Speed, 0.05 * sprint, "na velocidade da corrida de quatro (m/s)");
		b.Near(alone.Camera, run.Camera, 0.05, "a câmera não encolhe pelo aliado que ficou atrás (m)");

		// 2. O aliado da rede: posto onde a máquina dele diz, por cima do lobo,
		// a cada quadro (o boneco não anda pela física). Era o vai e volta.
		run = await Sprint(b, ally, body => body.GlobalPosition + -body.GlobalBasis.Z * 0.3f);
		RegressionBench.Note($"aliado da rede por cima: recuou {run.Back} quadros, {RegressionBench.F(run.Speed)} m/s");
		b.Check(run.Back == 0, $"com o aliado da rede em cima, o lobo não é empurrado para trás ({run.Back} quadros)");
		b.Near(sprint, run.Speed, 0.05 * sprint, "e corre como se ele não estivesse ali (m/s)");

		// 3. O inimigo continua sólido: para nele, sem tremer.
		Away(ally);
		run = await Sprint(b, enemy, null);
		RegressionBench.Note($"inimigo parado: recuou {run.Back} quadros, terminou {RegressionBench.F(run.Ahead)} m à frente dele");
		b.Check(run.Back == 0, "contra o inimigo o lobo para sem ir e voltar");
		b.AtMost(-0.4, run.Ahead, "o inimigo barra o lobo");

		// 4. O salto carregado com um aliado no caminho: passa por ele e cai
		// onde ia cair (com um inimigo ali, parava nele: SaltoCarregado).
		Away(enemy);
		b.Weapon.SpecialFree = true;
		await b.Place(Start, -90f, settle: 0.6);
		ally.GlobalPosition = Start + new Vector3(3.5f, 0, 0);
		enemy.GlobalPosition = Start + new Vector3(0, 0, 25);
		float allyTaken = 0;
		allyHealth.Damaged += (amount, _) => allyTaken += amount;
		Vector3 from = p.GlobalPosition;
		b.Check(b.Weapon.BotPounce(null, 1f), "o salto saiu");
		await b.Seconds(3.2);
		float leap = (p.GlobalPosition - from).X;
		RegressionBench.Note($"salto com aliado a 3,5 m: andou {RegressionBench.F(leap)} m (alcance {RegressionBench.F(b.Weapon.PounceMaxDistance)} m)");
		b.Near(b.Weapon.PounceMaxDistance, leap, 0.3, "o salto passa pelo aliado e anda o alcance todo (m)");
		b.Near(0, allyTaken, 0.01, "e não fere o aliado");

		// 5. A garra com o aliado colado no inimigo: só o inimigo leva.
		await b.Place(Start, -90f, settle: 0.6);
		await b.Seconds(0.8);   // a recarga do salto
		ally.GlobalPosition = Start + new Vector3(1.3f, 0, 0.45f);
		enemy.GlobalPosition = Start + new Vector3(1.3f, 0, -0.45f);
		await b.Seconds(enemyHealth.SpawnProtection + 0.2);
		allyTaken = 0;
		float enemyTaken = 0;
		enemyHealth.Damaged += (amount, _) => enemyTaken += amount;
		await b.Tap("attack");
		await b.Seconds(0.6);
		RegressionBench.Note($"garra entre os dois: aliado {allyTaken:0}, inimigo {enemyTaken:0}");
		b.AtLeast(1, enemyTaken, "a garra acerta o inimigo do lado do aliado");
		b.Near(0, allyTaken, 0.01, "e não o aliado");
	}

	private readonly record struct Pass(int Back, float Ahead, float Speed, float Camera);

	private static void Away(CharacterBody3D body) => body.GlobalPosition = new Vector3(40, 1, 40 + body.GetIndex());

	/// <summary>W W na direção de <paramref name="other"/>, 3 m à frente, por
	/// um segundo. <paramref name="stick"/>: onde pôr o outro a cada quadro (o
	/// boneco da rede).</summary>
	private static async Task<Pass> Sprint(RegressionBench b, CharacterBody3D other, System.Func<Player, Vector3> stick)
	{
		Player p = b.Player;
		await b.Place(Start, -90f, settle: 0.5);
		other.GlobalPosition = Start + new Vector3(3, 0, 0);
		await b.Frames(2);
		await b.Tap("move_forward");
		await b.Seconds(0.08);
		RegressionBench.Press("move_forward");
		await b.Seconds(0.3);   // embalar

		var arm = p.GetNode<SpringArm3D>("Head/SpringArm3D");
		float camera = float.MaxValue;
		int back = 0;
		Vector3 begin = p.GlobalPosition, last = begin;
		const int Frames = 60;
		for (int i = 0; i < Frames; i++)
		{
			if (stick != null) other.GlobalPosition = stick(p);
			await b.PhysicsFrame();
			if ((p.GlobalPosition - last).X < -0.002f) back++;
			last = p.GlobalPosition;
			camera = Mathf.Min(camera, arm.GetHitLength());
		}
		RegressionBench.ReleaseAll();
		float ahead = p.GlobalPosition.X - (stick == null ? other.GlobalPosition.X : Start.X + 3);
		return new Pass(back, ahead, (p.GlobalPosition.X - begin.X) / (Frames / 60f), camera);
	}
}
