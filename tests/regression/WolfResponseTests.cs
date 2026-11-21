using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// O lobo responde na hora (pedido do Jhon, 20/11: "é jogo rápido, de agir
/// rápido; a resposta do lobo tem que ser precisa"). A corrida de quatro
/// (W W), o pulo e o bote (botão direito no ar) saem no passo seguinte ao
/// da tecla, e um corta o outro assim que o anterior fez o movimento dele:
/// o bote, depois do dano, não prende o lobo no chão se ele pula ou corre.
/// A tecla apertada um pouco antes (o pulo antes de pousar, o bote junto
/// com o pulo) fica guardada e sai no primeiro passo em que pode.
///
/// Mede em passos de física (60 por segundo): o que o jogador sente.
/// </summary>
public sealed class WolfResponseTest : RegressionTest
{
	public override string Name => "LoboResponde";
	public override string Description => "W W, pulo e bote saem no passo seguinte; um corta o outro depois do movimento feito; tecla um pouco cedo fica guardada";

	/// <summary>"Imediato": até dois passos (33 ms) da tecla ao movimento.</summary>
	private const int Immediate = 2;

	private RegressionBench _b;
	private int _leaps;

	public override async Task Run(RegressionBench b)
	{
		_b = b;
		b.RemoveBots();
		await b.BecomeWolf();
		Player p = b.Player;
		Weapon w = b.Weapon;
		Vector3 start = new(-15, 1, 13);
		// O bote pode começar e encostar no mesmo passo (saiu do chão agora):
		// conta pelo sinal, não pelo estado.
		w.WolfMoveStarted += move => { if ((WolfMove)move == WolfMove.Leap) _leaps++; };

		// 1. W W: corre de quatro no segundo toque.
		await b.Place(start, -90f, settle: 0.6);
		await b.Tap("move_forward");
		await b.Frames(3);
		int sprint = await Press("move_forward", () => p.IsSprinting, hold: true);
		int fast = await FramesUntil(() => Flat(p) > p.HumanSpeed * 0.9f * 1.3f, 1.0);
		Input.ActionRelease("move_forward");
		b.Check(sprint <= Immediate, $"W W: corre de quatro em {sprint} passo(s)");
		b.Check(fast <= 20, $"e chega à velocidade da corrida em {fast} passos ({fast / 60.0:0.00} s)");

		// 2. Pulo.
		await b.Place(start, -90f, settle: 0.6);
		int jump = await Press("jump", () => !p.IsOnFloor());
		b.Check(jump <= Immediate, $"espaço: sai do chão em {jump} passo(s)");

		// 3. Bote logo que sai do chão.
		int leaps = _leaps;
		int dive = await Press("special", () => _leaps > leaps);
		b.Check(dive <= Immediate, $"botão direito logo depois de sair do chão: o bote sai em {dive} passo(s)");
		await b.Until(() => !w.IsDiving, 2.0);
		await b.Seconds(1.2);

		// 4. Pulo e bote apertados juntos (o bote ainda no chão): fica guardado.
		await b.Place(start, -90f, settle: 0.6);
		Input.ActionPress("jump");
		Input.ActionPress("special");
		int leapsBefore = _leaps;
		int together = await FramesUntil(() => _leaps > leapsBefore, 0.5);
		RegressionBench.ReleaseAll();
		b.Check(together <= Immediate + 2, $"espaço e botão direito juntos: o bote sai assim que sai do chão ({together} passos)");
		await b.Until(() => !w.IsDiving, 2.0);
		await b.Seconds(1.2);

		// 5. O pulo apertado um pouco antes de pousar.
		await b.Place(start, -90f, settle: 0.6);
		await b.Tap("jump");
		await b.Until(() => !p.IsOnFloor() && p.Velocity.Y < -1f, 2.0);
		double before = await FallFor(p, 0.08);
		int early = await Press("jump", () => p.IsOnFloor() || p.Velocity.Y > 0f, timeout: 1.0);
		int again = await FramesUntil(() => p.Velocity.Y > 0f && !p.IsOnFloor(), 0.5);
		b.Check(again <= Immediate, $"espaço {before * 1000:0} ms antes de pousar: pula de novo ao pousar ({again} passo(s) depois de pousar)");

		// 6. Depois do bote, o pulo solta o lobo da aterrissagem.
		await Dive(b, p, w, start, sprinting: false);
		int out1 = await Press("jump", () => !p.IsOnFloor());
		b.Check(out1 <= Immediate, $"depois do dano do bote, espaço: pula em {out1} passo(s) (sem esperar a aterrissagem)");
		b.Check(!w.IsRooted, "e a aterrissagem soltou o lobo");
		await b.Seconds(1.2);

		// 7. Depois do bote, W W sai correndo.
		await Dive(b, p, w, start, sprinting: false);
		await b.Tap("move_forward");
		await b.Frames(2);
		int out2 = await Press("move_forward", () => p.IsSprinting && Flat(p) > 3f, hold: true);
		Input.ActionRelease("move_forward");
		b.Check(out2 <= Immediate + 4, $"depois do dano do bote, W W: corre em {out2} passo(s)");

		// 8. Correndo de quatro, pula e dá o bote: pousa já correndo.
		await Dive(b, p, w, start, sprinting: true);
		int out3 = await FramesUntil(() => Flat(p) > 3f, 1.0);
		Input.ActionRelease("move_forward");
		b.Check(out3 <= Immediate + 4, $"bote no meio da corrida (W segurado): pousa e segue correndo em {out3} passo(s)");

		// 9. Pulo na corrida: pousa correndo.
		await b.Place(start, -90f, settle: 0.6);
		await b.Tap("move_forward");
		await b.Frames(3);
		Input.ActionPress("move_forward");
		await b.Seconds(0.6);
		float running = Flat(p);
		await b.Tap("jump");
		await b.Until(() => !p.IsOnFloor(), 0.3);
		await b.Until(() => p.IsOnFloor(), 2.0);
		int resume = await FramesUntil(() => Flat(p) >= running * 0.9f, 1.0);
		Input.ActionRelease("move_forward");
		b.Check(p.IsSprinting || resume <= 12, $"pulo na corrida: ao pousar volta à corrida em {resume} passo(s)");
		GD.Print($"LoboResponde (passos de 1/60 s): W W {sprint}, até a velocidade {fast}; pulo {jump}; bote no ar {dive}; " +
			$"pulo+bote juntos {together}; pulo {before * 1000:0} ms antes de pousar {again}; depois do bote: pulo {out1}, W W {out2}, " +
			$"correndo {out3}; pulo na corrida, volta {resume}");
		RegressionBench.ReleaseAll();
	}

	/// <summary>Pula, dá o bote e espera o dano (o lobo encostar).
	/// Correndo: segura o W na corrida de quatro o tempo todo.</summary>
	private static async Task Dive(RegressionBench b, Player p, Weapon w, Vector3 start, bool sprinting)
	{
		await b.Place(start, -90f, pitchDegrees: -20f, settle: 0.6);
		if (sprinting)
		{
			await b.Tap("move_forward");
			await b.Frames(3);
			Input.ActionPress("move_forward");
			await b.Seconds(0.4);
		}
		await b.Tap("jump");
		await b.Seconds(0.2);
		await b.Tap("special");
		await b.Until(() => w.IsDiving, 0.4);
		await b.Until(() => !w.IsDiving, 2.0);
	}

	/// <summary>Espera cair até faltar <paramref name="lead"/> s para o chão
	/// (pela velocidade e a altura), e devolve quanto faltava.</summary>
	private static async Task<double> FallFor(Player p, double lead)
	{
		var space = p.GetWorld3D().DirectSpaceState;
		for (int i = 0; i < 200; i++)
		{
			var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(p.GlobalPosition, p.GlobalPosition + Vector3.Down * 10f, 1,
				new Godot.Collections.Array<Rid> { p.GetRid() }));
			if (hit.Count > 0)
			{
				float height = p.GlobalPosition.Y - ((Vector3)hit["position"]).Y - p.StandHeight * 0.5f;
				float speed = Mathf.Max(-p.Velocity.Y, 0.1f);
				if (height / speed <= lead) return height / speed;
			}
			await ((SceneTree)Engine.GetMainLoop()).ToSignal((SceneTree)Engine.GetMainLoop(), SceneTree.SignalName.PhysicsFrame);
		}
		return 0;
	}

	private static float Flat(Player p) => new Vector2(p.Velocity.X, p.Velocity.Z).Length();

	/// <summary>Aperta e conta os passos até <paramref name="done"/>. Solta
	/// depois de dois passos (a não ser que <paramref name="hold"/>).</summary>
	private async Task<int> Press(string action, Func<bool> done, bool hold = false, double timeout = 1.5)
	{
		Input.ActionPress(action);
		int frames = 0;
		double end = _b.Time + timeout;
		while (!done() && _b.Time < end)
		{
			await _b.PhysicsFrame();
			frames++;
			if (frames == 2 && !hold) Input.ActionRelease(action);
		}
		if (!hold) Input.ActionRelease(action);
		return done() ? frames : 999;
	}

	private async Task<int> FramesUntil(Func<bool> done, double timeout)
	{
		int frames = 0;
		double end = _b.Time + timeout;
		while (!done() && _b.Time < end)
		{
			await _b.PhysicsFrame();
			frames++;
		}
		return done() ? frames : 999;
	}
}
