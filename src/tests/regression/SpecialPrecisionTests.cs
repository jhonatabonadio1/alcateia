using Godot;
using System.Threading.Tasks;

/// <summary>
/// O especial do lobo cai no alvo, liso (Jhon, 25/11: "quando dou o especial
/// em um personagem, ele dá uma bugada, ele vai e volta no ar e acaba errando
/// o alvo; isso precisa ser extremamente preciso e fluido").
///
/// O especial (Q arma, o clique salta no alvo travado) voava por um arco
/// refeito a cada quadro até o centro do alvo. Três coisas faziam o lobo ir e
/// voltar no ar: o ponto de queda era dentro do corpo do alvo (a física
/// barrava o lobo e o empurrava de volta); o arco, refeito do ponto de partida,
/// recuava junto com o alvo que vinha na direção do lobo (−12 cm por quadro
/// contra um lobo de quatro); e encostando em alguém no ar a física deslizava
/// o lobo de lado na cápsula dele, e ele dava a volta e caía do outro lado.
/// </summary>
public sealed class SpecialPrecisionTest : RegressionTest
{
	public override string Name => "EspecialPreciso";
	public override string Description => "o especial travado voa liso até o alvo, sem voltar nem balançar no ar, e cai colado nele: parado, andando, esquivando, vindo de quatro, o boneco da rede e alguém no caminho";

	private static readonly Vector3 Start = new(4, 1, -3.5f);

	private static Bot _puppet;
	private static CharacterBody3D _blocker;

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots(keep: 1);
		var enemy = b.Bots()[0];
		RegressionBench.Freeze(enemy);
		enemy.GetNode<Health>("Health").Team = Teams.Other(b.Health.Team);
		await b.BecomeWolf();
		b.Weapon.SpecialFree = false;

		// O boneco de verdade de outro jogador (Bot com Remote): anda pelos
		// pacotes que chegam a 30 por segundo, com a perseguição suave dele.
		_puppet = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		_puppet.Remote = true;
		_puppet.Team = Teams.Other(b.Health.Team);
		b.Scene.AddChild(_puppet);

		// Outro inimigo, parado no caminho do salto.
		var blocker = GD.Load<PackedScene>("res://src/scenes/bot.tscn").Instantiate<Bot>();
		blocker.Team = Teams.Other(b.Health.Team);
		b.Scene.AddChild(blocker);
		await b.Frames(2);
		RegressionBench.Freeze(blocker);
		_blocker = blocker;

		// (o quê, distância, como o alvo anda, boneco da rede)
		(string What, float Distance, Vector3 Walk, bool Puppet)[] cases =
		{
			("parado a 2 m", 2f, Vector3.Zero, false),
			("parado a 4 m", 4f, Vector3.Zero, false),
			("parado a 4 m, meio metro de lado", 4f, Side, false),
			("parado a 8 m", 8f, Vector3.Zero, false),
			("parado a 11 m", 11f, Vector3.Zero, false),
			("andando para o lobo a 8 m", 8f, new Vector3(3.5f, 0, 0), false),
			("andando de lado a 8 m", 8f, new Vector3(0, 0, 3.5f), false),
			("fugindo a 8 m", 8f, new Vector3(-3.5f, 0, 0), false),
			("lobo vindo de quatro a 9 m", 9f, new Vector3(7.1f, 0, 0), false),
			("lobo vindo de quatro de viés a 9 m", 9f, new Vector3(6.6f, 0, 2.6f), false),
			("esquivando (A e D) a 8 m", 8f, Dodge, false),
			("boneco da rede parado a 8 m", 8f, Vector3.Zero, true),
			("boneco da rede andando de lado a 8 m", 8f, new Vector3(0, 0, 3.5f), true),
			("boneco da rede vindo de quatro a 9 m", 9f, new Vector3(7.1f, 0, 0), true),
			("boneco da rede esquivando a 5 m", 5f, Dodge, true),
			("boneco da rede esquivando a 8 m", 8f, Dodge, true),
			("alguém no caminho a 4 m, alvo a 8 m", 8f, InTheWay, false),
		};

		foreach (var c in cases)
		{
			CharacterBody3D target = c.Puppet ? _puppet : enemy;
			foreach (CharacterBody3D other in new CharacterBody3D[] { enemy, _puppet, _blocker })
				if (other != target) Away(other);
			await Leap(b, target, c.What, c.Distance, c.Walk, c.Puppet);
		}
	}

	/// <summary>Marca: parado, 45 cm fora da linha do lobo.</summary>
	private static readonly Vector3 Side = new(0, 55, 0);

	/// <summary>Marca: esquivando de um lado para o outro (A e D), 3,5 m/s,
	/// trocando a cada 0,3 s.</summary>
	private static readonly Vector3 Dodge = new(0, 99, 0);

	/// <summary>Marca: o alvo parado, e outro inimigo no meio do caminho, 35
	/// cm fora da linha.</summary>
	private static readonly Vector3 InTheWay = new(0, 66, 0);

	private static Vector3 WalkAt(Vector3 walk, double t)
	{
		if (walk == Side) return new Vector3(0, 0, 0.45f);
		if (walk == InTheWay) return Vector3.Zero;
		if (walk != Dodge) return walk * (float)t;
		double phase = t % 0.6;
		return new Vector3(0, 0, (float)(3.5 * (phase < 0.3 ? phase : 0.6 - phase)));
	}

	private static Vector3 VelocityAt(Vector3 walk, double t)
	{
		if (walk == Side || walk == InTheWay) return Vector3.Zero;
		if (walk != Dodge) return walk;
		return new Vector3(0, 0, t % 0.6 < 0.3 ? 3.5f : -3.5f);
	}

	private static void Away(CharacterBody3D body)
	{
		// O boneco da rede volta sozinho para onde o último pacote disse: some.
		if (body is Bot { Remote: true } puppet) puppet.RemotePresent(false);
		body.GlobalPosition = new Vector3(40, 1, 40 + body.GetIndex());
	}

	private static void Send(Bot puppet, Vector3 at, Vector3 velocity) =>
		puppet.RemoteUpdate(at, velocity, Mathf.Pi * 0.5f, 0f, true, Form.Human, Slot.Primary, "ar15", "deagle", 5000f, 5000f, 0f, 0f, false);

	private static async Task Leap(RegressionBench b, CharacterBody3D enemy, string what, float distance, Vector3 walk, bool puppet)
	{
		Weapon w = b.Weapon;
		Player p = b.Player;
		Health health = enemy.GetNode<Health>("Health");
		await b.Place(Start, 90f, settle: 0.5);

		Vector3 enemyStart = Start + new Vector3(-distance, 0, 0) + WalkAt(walk, 0);
		if (puppet)
		{
			// o boneco aparece onde o dono diz e assenta
			for (int i = 0; i < 20; i++)
			{
				Send((Bot)enemy, enemyStart, Vector3.Zero);
				await b.PhysicsFrame();
			}
		}
		else
		{
			enemy.GlobalPosition = enemyStart;
			enemy.Velocity = Vector3.Zero;
		}
		if (walk == InTheWay) _blocker.GlobalPosition = Start + new Vector3(-4f, 0, 0.35f);

		// Vida de sobra para medir o dano. O boneco da rede não leva dano
		// aqui (vai para a máquina do dono): conta o que saiu para ele.
		health.SetPool(5000f, 0f, refill: true);
		Health blockerHealth = _blocker.GetNode<Health>("Health");
		blockerHealth.SetPool(5000f, 0f, refill: true);
		float sent = 0f;
		if (puppet) health.Remote = hit => sent += hit.Amount;
		await b.Until(() => !health.IsProtected && !blockerHealth.IsProtected, 4.0);

		w.FillSpecial();
		await b.Tap("pounce");
		await b.Frames(3);
		b.Check(p.LockTarget == enemy, $"{what}: trava no alvo");

		float before = health.Hp, blockerBefore = blockerHealth.Hp;
		await b.Tap("attack");
		b.Check(await b.Until(() => w.IsPounceFlying, 1.0), $"{what}: salta");

		// Quadro a quadro, no chão: o avanço na direção do alvo (de onde ele
		// está agora) e o lado (em relação à linha da partida).
		Vector3 line = (enemyStart - Start) with { Y = 0 };
		Vector3 side = line.Normalized().Cross(Vector3.Up);
		int back = 0, sway = 0, frames = 0;
		float worstBack = 0f, lastSide = 0f, gap = 0f;
		Vector3 last = p.GlobalPosition;
		double t = 0;
		while (w.IsPounceFlying && frames < 120)
		{
			t += 1.0 / Engine.PhysicsTicksPerSecond;
			Vector3 now = enemyStart + WalkAt(walk, t);
			if (puppet)
			{
				// O pacote do dono: 30 por segundo (um quadro sim, um não).
				if (frames % 2 == 0) Send((Bot)enemy, now, VelocityAt(walk, t));
			}
			else
			{
				// O bot congelado não anda pela física: a velocidade dele é a
				// do passo, como a de quem anda de verdade.
				enemy.GlobalPosition = now;
				enemy.Velocity = VelocityAt(walk, t);
			}
			await b.PhysicsFrame();
			frames++;

			Vector3 to = (enemy.GlobalPosition - last) with { Y = 0 };
			Vector3 moved = (p.GlobalPosition - last) with { Y = 0 };
			float along = to.LengthSquared() > 0.0025f ? moved.Dot(to.Normalized()) : 0f;
			gap = RegressionBench.Flat(enemy.GlobalPosition - p.GlobalPosition);
			if (along < -0.01f && gap > 0.2f)
			{
				back++;
				worstBack = Mathf.Min(worstBack, along);
			}

			// De lado: o lobo trocando de lado no ar, acima de 0,6 m/s.
			float sideSpeed = moved.Dot(side) * Engine.PhysicsTicksPerSecond;
			if (Mathf.Abs(sideSpeed) > 0.6f)
			{
				if (lastSide != 0f && Mathf.Sign(sideSpeed) != Mathf.Sign(lastSide)) sway++;
				lastSide = sideSpeed;
			}
			last = p.GlobalPosition;
		}

		await b.Until(() => !w.IsRooted, 2.5);
		float dealt = puppet ? sent : before - health.Hp;
		float blockerTaken = blockerBefore - blockerHealth.Hp;
		if (puppet) health.Remote = null;
		float blockerGap = RegressionBench.Flat(_blocker.GlobalPosition - p.GlobalPosition);

		RegressionBench.Note($"{what}: {frames} quadros no ar, {back} para trás (pior {RegressionBench.F(worstBack)} m), {sway} trocas de lado; caiu a {RegressionBench.F(gap)} m do alvo; dano {RegressionBench.F(dealt)}"
			+ (walk == InTheWay ? $"; quem estava no caminho ficou a {RegressionBench.F(blockerGap)} m e levou {RegressionBench.F(blockerTaken)}" : ""));
		b.Check(back == 0, $"{what}: no ar o lobo não volta ({back} quadros, pior {RegressionBench.F(worstBack)} m)");
		b.Check(sway <= 1, $"{what}: no ar o lobo não vai e volta de lado ({sway} trocas)");

		if (walk == InTheWay)
		{
			// Travado, o salto é de quem travou: passa por quem está no
			// caminho (sem escorregar em volta dele) e cai no alvo; e quem foi
			// atravessado volta a ser sólido.
			b.Check(!p.GetCollisionExceptions().Contains(_blocker) || blockerGap < 0.7f, $"{what}: quem foi atravessado volta a ser sólido");
		}

		b.AtMost(1.1, gap, $"{what}: cai colado no alvo (m)");
		b.AtLeast(0.5, gap, $"{what}: e não dentro dele (m)");
		b.AtLeast(50, dealt, $"{what}: acerta");
	}
}
