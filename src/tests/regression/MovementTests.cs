using Godot;
using System.Threading.Tasks;

/// <summary>Velocidades no chão (regra 9 do CLAUDE.md): o humano anda a
/// passada do clipe, o lobo é multiplicador dele.</summary>
public sealed class GroundSpeedTest : RegressionTest
{
	public override string Name => "VelocidadeNoChao";
	public override string Description => "humano 2,82 m/s, agachado 1,27, correndo (Shift) 4,91 (com a faca e a pistola também), faca 3,60; lobo 4,33, agachado 1,95";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();

		// Os números do jogo de hoje: Player.HumanSpeed 3,2 na cena (docs/MEDIDAS.md),
		// × 0,8 do humano (HumanSpeedBonus). Era × 1,08, 3,46 m/s, até 26/11 (Jhon:
		// "o personagem pode andar mais lento"; "deixar na velocidade de um humano").
		// × 0,88 desde 27/11, 2,816 m/s (Jhon: "o andar normal do humano está muito
		// lento, pode acelerar um pouco"); a corrida continua nos 4,91.
		b.Near(2.816, await Measure(b, "move_forward"), 0.02, "humano andando");
		b.Near(4.909, 2.816 * b.Player.HumanSprintFactor, 0.01, "a corrida do Shift nos 4,91 m/s de antes");
		b.Near(1.267, await Measure(b, "move_forward", "crouch"), 0.05, "humano agachado");
		// O Shift corre desde 26/11 (pedido do Jhon; era andar devagar, 1,45 m/s):
		// × HumanSprintFactor, só para a frente, em pé.
		b.Near(2.816 * b.Player.HumanSprintFactor, await Measure(b, "move_forward", "sprint"), 0.03, "humano correndo com Shift");
		b.Near(2.816, await Measure(b, "move_back", "sprint"), 0.03, "com Shift para trás não corre");
		b.Near(1.267, await Measure(b, "move_forward", "sprint", "crouch"), 0.05, "agachado, o Shift não corre");

		b.Weapon.SelectSlot(Slot.Knife);
		// A faca na mão ×1,28 desde 26/11 (era ×1,037; Jhon: "aumentar velocidade de
		// movimento com faca equipada"): 2,816 × 1,28 = 3,60 m/s (3,28 até 27/11).
		b.Near(2.816 * b.Weapon.KnifeSpeedMultiplier, await Measure(b, "move_forward"), 0.02, "humano com a faca");
		// Correndo, a faca não soma (Jhon, 27/11: "velocidade da corrida com faca pode diminuir, ficou muito
		// rápido, tem que ser igual o sprint com fuzil"): era 2,56 × 1,9176 × 1,28 = 6,28 m/s.
		b.Near(2.816 * b.Player.HumanSprintFactor, await Measure(b, "move_forward", "sprint"), 0.04, "correndo com a faca, o mesmo que com o fuzil");
		// E com a pistola (× 1,08 andando): correndo, o mesmo também.
		b.Weapon.SelectSlot(Slot.Secondary);
		await b.Seconds(0.6);
		b.Near(2.816 * b.Player.HumanSprintFactor, await Measure(b, "move_forward", "sprint"), 0.04, "correndo com a pistola, o mesmo que com o fuzil");
		b.Weapon.SelectSlot(Slot.Primary);

		// O lobo é um só desde 26/11 (decisão do Jhon: sem classes), no meio do
		// Bruto (4,15) e do Ligeiro (4,50): a base 3,2 × 1,352.
		await b.BecomeWolf();
		b.Near(4.326, await Measure(b, "move_forward"), 0.03, "lobo correndo");
		b.Near(1.947, await Measure(b, "move_forward", "crouch"), 0.05, "lobo agachado");
		b.Near(4.326, await Measure(b, "move_right"), 0.03, "lobo de lado");
		// Para trás o lobo anda a 75% (WolfBackFactor, pedido do Jhon, 15/11).
		b.Near(4.326 * b.Player.WolfBackFactor, await Measure(b, "move_back"), 0.03, "lobo para trás");
	}

	/// <summary>Velocidade média depois de embalar, no campo aberto.</summary>
	private static async Task<float> Measure(RegressionBench b, params string[] keys)
	{
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		b.Player.Breath.Reset();   // cada medida com o fôlego cheio (SprintBreath)
		RegressionBench.Press(keys);
		await b.Seconds(0.6);
		Vector3 moved = await b.Displacement(b.Player, 60);
		RegressionBench.ReleaseAll();
		return RegressionBench.Flat(moved) / 1.0f;
	}
}

/// <summary>Altura do pulo, amarrada ao mapa: o humano sobe caixote (0,8 m)
/// e não contêiner; o lobo alcança o contêiner a partir do caixote.</summary>
public sealed class JumpHeightTest : RegressionTest
{
	public override string Name => "AlturaDoPulo";
	public override string Description => "humano pula 0,99 m, lobo 2,09 m";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		b.Near(0.99, await Measure(b), 0.04, "pulo do humano");

		await b.BecomeWolf();
		// O lobo único (26/11), no meio do Bruto (1,62) e do Ligeiro (2,56): ×1,435.
		b.Near(2.09, await Measure(b), 0.05, "pulo do lobo");
	}

	private static async Task<float> Measure(RegressionBench b)
	{
		await b.Place(new Vector3(-15, 1, 13), -90f);
		float floor = b.Player.GlobalPosition.Y;
		float top = floor;

		RegressionBench.Press("jump");
		double end = b.Time + 1.6;
		while (b.Time < end)
		{
			await b.PhysicsFrame();
			top = Mathf.Max(top, b.Player.GlobalPosition.Y);
		}
		RegressionBench.ReleaseAll();
		return top - floor;
	}
}

/// <summary>Corrida na parede: 7% além do chão (decisão do Jhon,
/// v0.26.11), na altura em que grudou.</summary>
public sealed class WallRunTest : RegressionTest
{
	public override string Name => "CorridaNaParede";
	public override string Description => "o lobo corre na parede a 4,94 m/s sem subir nem descer";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.BecomeWolf();

		// No ar, encostado na parede da esquerda (face em z = −5,5),
		// andando ao longo dela com W.
		await b.Place(new Vector3(-8, 3, -5.1f), -90f, settle: 0.05);
		b.Player.Velocity = new Vector3(3.5f, 0, 0);
		RegressionBench.Press("move_forward");

		bool grabbed = await b.Until(() => b.Player.IsWallRunning, 1.0);
		b.Check(grabbed, "grudou na parede");
		await b.Seconds(1.2);

		Vector3 moved = await b.Displacement(b.Player, 30);
		RegressionBench.ReleaseAll();

		b.Check(b.Player.IsWallRunning, "continuava na parede");
		// 4,944: a base 3,2 da cena × 1,352 do lobo único (26/11; era 1,298 do Bruto,
		// 4,746) × 1,1427 da parede.
		b.Near(4.944, moved.X / 0.5, 0.05, "velocidade na parede");
		b.Near(0, moved.Y, 0.02, "altura travada");
	}
}

/// <summary>A corrida do humano (Shift, 26/11, pedido do Jhon): de arma
/// abaixada. O clique tira da corrida, e o tiro sai quando a arma volta.</summary>
public sealed class HumanSprintTest : RegressionTest
{
	public override string Name => "CorridaDoHumano";
	public override string Description => "com Shift o humano corre de arma abaixada: os braços tocam \"correr\"; o clique tira da corrida e o tiro sai quando a arma volta";

	public override async Task Run(RegressionBench b)
	{
		b.RemoveBots();
		await b.Place(new Vector3(-15, 1, 13), -90f, settle: 0.6);
		var view = b.Player.FindChild("ViewModel", true, false) as ViewModel;
		int before = b.Weapon.AmmoInMag;

		RegressionBench.Press("move_forward", "sprint");
		await b.Seconds(0.5);
		b.Check(b.Player.IsHumanSprinting, "correndo com Shift");
		b.Check(b.Player.IsWeaponLowered, "a arma está abaixada");
		if (view != null) b.Check(view.RigAnimation == "correr", $"os braços tocam \"correr\" (tocam \"{view.RigAnimation}\")");

		// Os braços andam no passo: a batida da arma cai com a pisada (Jhon, 26/11:
		// "sincronizar os passos"). Depois de um segundo, menos de 5% do ciclo de folga.
		await b.Seconds(1.0);
		if (view != null) b.AtMost(0.05, Mathf.Abs(view.RigSprintBehind), "os braços no passo do corpo (fração do ciclo fora)");

		// O clique tira da corrida: o tiro só sai depois de a arma voltar
		// (SprintRecover, 0,18 s), e o clique dado correndo não se perde.
		RegressionBench.Press("attack");
		await b.Frames(5);
		b.Check(b.Weapon.AmmoInMag == before, "a arma ainda voltando, o clique não atira");
		await b.Seconds(0.5);
		RegressionBench.ReleaseAll();
		b.Check(!b.Player.IsHumanSprinting && !b.Player.IsWeaponLowered, "o clique tirou da corrida e a arma voltou");
		b.Check(b.Weapon.AmmoInMag < before, "com a arma de volta, atira");

		// Com a pistola, um clique rápido (solta em 0,1 s, antes de a arma voltar) com o
		// Shift ainda apertado: o tiro sai quando a arma volta, e a corrida continua
		// depois. Antes soltar o botão apagava o clique, e o tiro se perdia.
		b.Weapon.SelectSlot(Slot.Secondary);
		await b.Seconds(0.8);
		RegressionBench.Press("move_forward", "sprint");
		await b.Seconds(0.5);
		b.Check(b.Player.IsHumanSprinting, "correndo com a pistola");
		int pistol = b.Weapon.AmmoInMag;
		RegressionBench.Press("attack");
		await b.Frames(6);
		RegressionBench.Release("attack");
		await b.Seconds(0.5);
		b.Check(b.Weapon.AmmoInMag == pistol - 1, $"clique rápido correndo: um tiro quando a arma volta (pente {pistol} → {b.Weapon.AmmoInMag})");
		b.Check(b.Player.IsHumanSprinting, "e volta a correr depois do tiro");

		// V correndo não faz nada: o V não vai no comando de rede, e golpear
		// correndo é o que o clique também não deixa.
		await b.Tap("melee");
		await b.Frames(3);
		b.Check(!b.Weapon.IsButting && b.Player.IsHumanSprinting, "V correndo: nem coronhada, nem sai da corrida");
		RegressionBench.ReleaseAll();
		await b.Seconds(0.4);

		// De lobo o Shift não é corrida de humano.
		await b.BecomeWolf();
		RegressionBench.Press("move_forward", "sprint");
		await b.Seconds(0.4);
		b.Check(!b.Player.IsHumanSprinting, "de lobo não há corrida de humano");
		RegressionBench.ReleaseAll();
	}
}
